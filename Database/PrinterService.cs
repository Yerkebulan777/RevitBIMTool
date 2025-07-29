using Dapper;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Database
{
    /// <summary>
    /// Сервис управления принтерами для Revit приложений с использованием 
    /// комбинированного подхода: embedded SQL ресурсы для сложных операций 
    /// и простые string constants для CRUD операций.
    /// 
    /// Преимущества такого подхода:
    /// - SQL синтаксис подсвечивается в отдельных .sql файлах
    /// - Сложная логика базы данных версионируется отдельно от C# кода  
    /// - Простые операции остаются читаемыми в коде
    /// - Легко поддерживать и модифицировать SQL запросы
    /// </summary>
    public sealed class PrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private readonly int _maxRetryAttempts;
        private readonly int _baseRetryDelayMs;
        private bool _disposed = false;

        #region Простые CRUD операции как string constants

        // Простая вставка нового принтера - классическая CRUD операция
        private const string InsertPrinter = @"
            INSERT INTO printer_states (printer_name, is_available, change_token)
            VALUES (@printerName, true, gen_random_uuid())
            ON CONFLICT (printer_name) DO NOTHING";

        // Простая выборка доступных принтеров для мониторинга
        private const string SelectAvailablePrinters = @"
            SELECT id, printer_name, is_available, reserved_by_file,
                   reserved_at, process_id, change_token
            FROM printer_states
            WHERE is_available = true
            ORDER BY printer_name";

        // Простое освобождение принтера без проверки прав доступа
        private const string ReleasePrinterSimple = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = gen_random_uuid()
            WHERE printer_name = @printerName";

        // Освобождение принтера с проверкой прав доступа
        private const string ReleasePrinterWithPermissionCheck = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = gen_random_uuid()
            WHERE printer_name = @printerName
              AND (reserved_by_file = @revitFileName OR reserved_by_file IS NULL)";

        // Чтение текущего состояния принтера для оптимистичного блокирования
        private const string ReadPrinterStateForUpdate = @"
            SELECT change_token, is_available
            FROM printer_states
            WHERE printer_name = @printerName
            FOR UPDATE";

        // Обновление состояния принтера с оптимистичным блокированием
        private const string UpdatePrinterWithOptimisticLock = @"
            UPDATE printer_states SET
                is_available = false,
                reserved_by_file = @revitFileName,
                reserved_at = @reservedAt,
                process_id = @processId,
                change_token = @newToken
            WHERE printer_name = @printerName
              AND change_token = @expectedToken";

        #endregion

        public PrinterService(
            string connectionString,
            int commandTimeout = 30,
            int maxRetryAttempts = 5,
            int baseRetryDelayMs = 50)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
            _maxRetryAttempts = maxRetryAttempts;
            _baseRetryDelayMs = baseRetryDelayMs;

            InitializeDatabase();
        }

        /// <summary>
        /// Инициализирует базу данных, используя SQL из embedded ресурса.
        /// Этот подход позволяет легко модифицировать структуру таблицы
        /// без перекомпиляции основного кода сервиса.
        /// </summary>
        private void InitializeDatabase()
        {
            // Получаем сложный DDL запрос из embedded ресурса
            string createTableSql = SqlResourceManager.CreatePrinterStatesTable;

            _ = ExecuteWithSerializableRetry(connection =>
            {
                return connection.Execute(createTableSql, commandTimeout: _commandTimeout);
            });
        }

        /// <summary>
        /// Инициализирует принтеры, используя простую CRUD операцию.
        /// Поскольку это стандартная операция вставки, оставляем SQL в коде.
        /// </summary>
        public void InitializePrinters(params string[] printerNames)
        {
            if (printerNames == null || printerNames.Length == 0)
            {
                return;
            }

            var validPrinters = printerNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new { printerName = name });

            if (!validPrinters.Any())
            {
                return;
            }

            _ = ExecuteWithSerializableRetry(connection =>
            {
                return connection.Execute(InsertPrinter, validPrinters, commandTimeout: _commandTimeout);
            });
        }

        /// <summary>
        /// Резервирует любой доступный принтер, используя комбинацию 
        /// embedded SQL для сложной логики блокировки и простого SQL для обновления.
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string revitFilePath, params string[] preferredPrinters)
        {
            if (string.IsNullOrWhiteSpace(revitFilePath))
            {
                throw new ArgumentException("Путь к файлу Revit не может быть пустым", nameof(revitFilePath));
            }

            string revitFileName = Path.GetFileName(revitFilePath);

            return ExecuteWithSerializableRetry(connection =>
            {
                using IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    // Используем embedded SQL для сложной операции получения принтеров с блокировкой
                    string getAvailablePrintersQuery = SqlResourceManager.GetAvailablePrintersWithLock;

                    List<PrinterState> availablePrinters = connection.Query<PrinterState>(
                        getAvailablePrintersQuery,
                        transaction: transaction,
                        commandTimeout: _commandTimeout).ToList();

                    if (!availablePrinters.Any())
                    {
                        return null;
                    }

                    IEnumerable<PrinterState> orderedPrinters = OrderPrintersByPreference(availablePrinters, preferredPrinters);

                    foreach (PrinterState printer in orderedPrinters)
                    {
                        if (ReservePrinterInternal(connection, transaction, printer.PrinterName, revitFileName))
                        {
                            transaction.Commit();
                            return printer.PrinterName;
                        }
                    }

                    transaction.Rollback();
                    return null;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Резервирует конкретный принтер, используя простые CRUD операции
        /// для чтения и обновления состояния.
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string revitFilePath)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new ArgumentException("Имя принтера не может быть пустым", nameof(printerName));
            }

            if (string.IsNullOrWhiteSpace(revitFilePath))
            {
                throw new ArgumentException("Путь к файлу Revit не может быть пустым", nameof(revitFilePath));
            }

            string revitFileName = Path.GetFileName(revitFilePath);

            return ExecuteWithSerializableRetry(connection =>
            {
                using IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    bool success = ReservePrinterInternal(connection, transaction, printerName, revitFileName);

                    if (success)
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.Rollback();
                    }

                    return success;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Освобождает принтер, используя простые CRUD операции.
        /// Демонстрирует выбор между двумя вариантами запроса в зависимости от параметров.
        /// </summary>
        public bool ReleasePrinter(string printerName, string revitFilePath = null)
        {
            return string.IsNullOrWhiteSpace(printerName)
                ? throw new ArgumentException("Имя принтера не может быть пустым", nameof(printerName))
                : ExecuteWithSerializableRetry(connection =>
            {
                if (!string.IsNullOrWhiteSpace(revitFilePath))
                {
                    // Освобождение с проверкой прав доступа
                    string revitFileName = Path.GetFileName(revitFilePath);
                    int affectedRows = connection.Execute(
                        ReleasePrinterWithPermissionCheck,
                        new { printerName = printerName.Trim(), revitFileName },
                        commandTimeout: _commandTimeout);

                    return affectedRows > 0;
                }
                else
                {
                    // Административное освобождение без проверки прав
                    int affectedRows = connection.Execute(
                        ReleasePrinterSimple,
                        new { printerName = printerName.Trim() },
                        commandTimeout: _commandTimeout);

                    return affectedRows > 0;
                }
            });
        }

        /// <summary>
        /// Получает доступные принтеры, используя простую CRUD операцию.
        /// Такие запросы идеально подходят для размещения прямо в коде.
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            return ExecuteWithSerializableRetry(connection =>
            {
                return connection.Query<PrinterState>(SelectAvailablePrinters, commandTimeout: _commandTimeout).ToList();
            });
        }

        /// <summary>
        /// Очищает зависшие резервирования, используя embedded SQL ресурс.
        /// Поскольку это относительно сложная операция с бизнес-логикой,
        /// выносим ее в отдельный SQL файл для лучшей читаемости.
        /// </summary>
        public int CleanupExpiredReservations(TimeSpan maxAge)
        {
            DateTime cutoffTime = DateTime.UtcNow.Subtract(maxAge);

            // Получаем SQL из embedded ресурса
            string cleanupQuery = SqlResourceManager.CleanupExpiredReservations;

            return ExecuteWithSerializableRetry(connection =>
            {
                return connection.Execute(cleanupQuery, new { cutoffTime }, commandTimeout: _commandTimeout);
            });
        }

        #region Внутренние методы

        private OdbcConnection CreateConnection()
        {
            OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Внутренняя реализация резервирования, использующая простые CRUD операции
        /// для чтения текущего состояния и оптимистичного обновления.
        /// </summary>
        private bool ReservePrinterInternal(
            IDbConnection connection,
            IDbTransaction transaction,
            string printerName,
            string revitFileName)
        {
            // Читаем текущее состояние принтера с блокировкой
            (Guid changeToken, bool isAvailable) = connection.QuerySingleOrDefault<(Guid changeToken, bool isAvailable)>(
                ReadPrinterStateForUpdate,
                new { printerName = printerName.Trim() },
                transaction,
                _commandTimeout);

            if (changeToken == Guid.Empty || !isAvailable)
            {
                return false;
            }

            // Обновляем состояние принтера с оптимистичным блокированием
            Process currentProcess = Process.GetCurrentProcess();
            int affectedRows = connection.Execute(
                UpdatePrinterWithOptimisticLock,
                new
                {
                    printerName = printerName.Trim(),
                    revitFileName,
                    reservedAt = DateTime.UtcNow,
                    processId = currentProcess.Id,
                    newToken = Guid.NewGuid(),
                    expectedToken = changeToken
                },
                transaction,
                _commandTimeout);

            return affectedRows > 0;
        }

        private static IEnumerable<PrinterState> OrderPrintersByPreference(
            IEnumerable<PrinterState> printers,
            string[] preferredPrinters)
        {
            if (preferredPrinters?.Length > 0)
            {
                HashSet<string> preferredSet = new HashSet<string>(
                    preferredPrinters
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => p.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                return printers
                    .OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1)
                    .ThenBy(p => p.PrinterName);
            }

            return printers.OrderBy(p => alendar.PrinterName);
        }

        private T ExecuteWithSerializableRetry<T>(Func<OdbcConnection, T> operation)
        {
            int attempt = 0;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    using OdbcConnection connection = CreateConnection();
                    return operation(connection);
                }
                catch (OdbcException ex) when (IsSerializationFailure(ex) && attempt < _maxRetryAttempts - 1)
                {
                    attempt++;
                    int delay = (_baseRetryDelayMs * (int)Math.Pow(2, attempt)) + new Random().Next(0, 50);
                    Thread.Sleep(delay);
                }
            }

            using OdbcConnection connection = CreateConnection();
            return operation(connection);
        }

        private static bool IsSerializationFailure(OdbcException ex)
        {
            string[] serializationErrorCodes = { "40001", "40P01" };
            return serializationErrorCodes.Any(code => ex.Message.Contains(code));
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}