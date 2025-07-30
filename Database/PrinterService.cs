using Dapper;
using Serilog;
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
    /// Единственный сервис управления принтерами для Revit приложений.
    /// Использует ODBC подключение к PostgreSQL с уровнем изоляции SERIALIZABLE.
    /// Обеспечивает thread-safe операции и автоматическую очистку зависших резервирований.
    /// </summary>
    public sealed class PrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private readonly int _maxRetryAttempts;
        private readonly int _baseRetryDelayMs;
        private readonly int _lockTimeoutMinutes;
        private static readonly object _initLock = new object();
        private static volatile bool _schemaInitialized = false;
        private bool _disposed = false;

        #region SQL Queries - все запросы в одном месте для простоты

        /// <summary>
        /// Создание таблицы принтеров без индексов (согласно требованию).
        /// Используется универсальный ODBC синтаксис для PostgreSQL.
        /// </summary>
        private const string CreateTableSql = @"
            CREATE TABLE IF NOT EXISTS printer_states (
                id SERIAL PRIMARY KEY,
                printer_name VARCHAR(200) NOT NULL UNIQUE,
                is_available BOOLEAN NOT NULL DEFAULT true,
                reserved_by_file VARCHAR(500),
                reserved_at TIMESTAMPTZ,
                process_id INTEGER,
                change_token UUID NOT NULL DEFAULT gen_random_uuid(),
                created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
                
                -- Constraint для логической целостности резервирования
                CONSTRAINT chk_reservation_logic CHECK (
                    (is_available = true AND reserved_by_file IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR
                    (is_available = false AND reserved_by_file IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)
                )
            );";

        /// <summary>
        /// Безопасная вставка принтера с защитой от дублирования.
        /// </summary>
        private const string InsertPrinterSql = @"
            INSERT INTO printer_states (printer_name, is_available, change_token)
            VALUES (@printerName, true, gen_random_uuid())
            ON CONFLICT (printer_name) DO NOTHING;";

        /// <summary>
        /// Получение доступных принтеров с блокировкой для предотвращения race conditions.
        /// FOR UPDATE обеспечивает эксклюзивную блокировку до конца транзакции.
        /// </summary>
        private const string GetAvailablePrintersWithLockSql = @"
            SELECT id, printer_name, is_available, reserved_by_file, reserved_at, process_id, change_token
            FROM printer_states
            WHERE is_available = true
            ORDER BY printer_name
            FOR UPDATE;";

        /// <summary>
        /// Резервирование принтера с оптимистичным блокированием через change_token.
        /// </summary>
        private const string ReservePrinterSql = @"
            UPDATE printer_states SET
                is_available = false,
                reserved_by_file = @revitFileName,
                reserved_at = @reservedAt,
                process_id = @processId,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND change_token = @expectedToken
              AND is_available = true;";

        /// <summary>
        /// Освобождение принтера с проверкой прав доступа.
        /// </summary>
        private const string ReleasePrinterSql = @"
            UPDATE printer_states SET
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE printer_name = @printerName
              AND (reserved_by_file = @revitFileName OR @revitFileName IS NULL);";

        /// <summary>
        /// Автоматическая очистка зависших резервирований по timeout.
        /// </summary>
        private const string CleanupExpiredReservationsSql = @"
            UPDATE printer_states 
            SET 
                is_available = true,
                reserved_by_file = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = gen_random_uuid(),
                updated_at = CURRENT_TIMESTAMP
            WHERE 
                is_available = false 
                AND reserved_at < @cutoffTime
                AND reserved_at IS NOT NULL;";

        #endregion

        public PrinterService(
            string connectionString,
            int commandTimeout = 30,
            int maxRetryAttempts = 3,
            int baseRetryDelayMs = 100,
            int lockTimeoutMinutes = 10)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
            _maxRetryAttempts = maxRetryAttempts;
            _baseRetryDelayMs = baseRetryDelayMs;
            _lockTimeoutMinutes = lockTimeoutMinutes;

            EnsureSchemaInitialized();
            Log.Information("PrinterService initialized with {TimeoutMinutes}min cleanup timeout", _lockTimeoutMinutes);
        }

        /// <summary>
        /// Thread-safe инициализация схемы базы данных.
        /// Выполняется один раз при первом создании сервиса.
        /// </summary>
        private void EnsureSchemaInitialized()
        {
            if (!_schemaInitialized)
            {
                lock (_initLock)
                {
                    if (!_schemaInitialized)
                    {
                        ExecuteWithRetry(conn =>
                        {
                            conn.Execute(CreateTableSql, commandTimeout: _commandTimeout);
                            Log.Debug("Database schema initialized successfully");
                            return 0;
                        });
                        _schemaInitialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// Инициализирует принтеры в системе.
        /// Идемпотентная операция - безопасно вызывать многократно.
        /// </summary>
        public void InitializePrinters(params string[] printerNames)
        {
            if (printerNames?.Length == 0)
            {
                Log.Warning("No printer names provided for initialization");
                return;
            }

            var validPrinters = printerNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new { printerName = name });

            int insertedCount = ExecuteWithRetry(conn =>
            {
                return conn.Execute(InsertPrinterSql, validPrinters, commandTimeout: _commandTimeout);
            });

            Log.Information("Initialized {Count} printers in database", insertedCount);
        }

        /// <summary>
        /// Пытается зарезервировать любой доступный принтер из предпочтительного списка.
        /// Использует SERIALIZABLE изоляцию для предотвращения race conditions.
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string revitFilePath, params string[] preferredPrinters)
        {
            if (string.IsNullOrWhiteSpace(revitFilePath))
            {
                throw new ArgumentException("Revit file path cannot be empty", nameof(revitFilePath));
            }

            // Автоматическая очистка перед резервированием
            CleanupExpiredReservations();

            string revitFileName = Path.GetFileName(revitFilePath);
            int processId = Process.GetCurrentProcess().Id;

            return ExecuteWithRetry(conn =>
            {
                using var transaction = conn.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    // Получаем доступные принтеры с блокировкой
                    var availablePrinters = conn.Query<PrinterState>(
                        GetAvailablePrintersWithLockSql,
                        transaction: transaction,
                        commandTimeout: _commandTimeout).ToList();

                    if (!availablePrinters.Any())
                    {
                        Log.Debug("No available printers found for reservation");
                        transaction.Rollback();
                        return null;
                    }

                    // Сортируем принтеры по предпочтениям
                    var orderedPrinters = OrderByPreference(availablePrinters, preferredPrinters);

                    // Пытаемся зарезервировать первый доступный
                    foreach (var printer in orderedPrinters)
                    {
                        int affected = conn.Execute(
                            ReservePrinterSql,
                            new
                            {
                                printerName = printer.PrinterName,
                                revitFileName,
                                reservedAt = DateTime.UtcNow,
                                processId,
                                expectedToken = printer.ChangeToken
                            },
                            transaction,
                            _commandTimeout);

                        if (affected > 0)
                        {
                            transaction.Commit();
                            Log.Information("Successfully reserved printer {PrinterName} for {FileName}",
                                printer.PrinterName, revitFileName);
                            return printer.PrinterName;
                        }
                    }

                    transaction.Rollback();
                    Log.Warning("Failed to reserve any printer for {FileName}", revitFileName);
                    return null;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Log.Error(ex, "Error during printer reservation for {FileName}", revitFileName);
                    throw;
                }
            });
        }

        /// <summary>
        /// Резервирует конкретный принтер.
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string revitFilePath)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new ArgumentException("Printer name cannot be empty", nameof(printerName));
            }

            if (string.IsNullOrWhiteSpace(revitFilePath))
            {
                throw new ArgumentException("Revit file path cannot be empty", nameof(revitFilePath));
            }

            string revitFileName = Path.GetFileName(revitFilePath);
            int processId = Process.GetCurrentProcess().Id;

            return ExecuteWithRetry(conn =>
            {
                using var transaction = conn.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    // Получаем состояние конкретного принтера с блокировкой
                    var printer = conn.QuerySingleOrDefault<PrinterState>(
                        "SELECT * FROM printer_states WHERE printer_name = @printerName FOR UPDATE",
                        new { printerName = printerName.Trim() },
                        transaction,
                        _commandTimeout);

                    if (printer == null || !printer.IsAvailable)
                    {
                        transaction.Rollback();
                        Log.Debug("Printer {PrinterName} is not available for reservation", printerName);
                        return false;
                    }

                    int affected = conn.Execute(
                        ReservePrinterSql,
                        new
                        {
                            printerName = printer.PrinterName,
                            revitFileName,
                            reservedAt = DateTime.UtcNow,
                            processId,
                            expectedToken = printer.ChangeToken
                        },
                        transaction,
                        _commandTimeout);

                    if (affected > 0)
                    {
                        transaction.Commit();
                        Log.Information("Successfully reserved specific printer {PrinterName} for {FileName}",
                            printerName, revitFileName);
                        return true;
                    }

                    transaction.Rollback();
                    return false;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Log.Error(ex, "Error reserving specific printer {PrinterName}", printerName);
                    throw;
                }
            });
        }

        /// <summary>
        /// Освобождает принтер. Если revitFileName указан, проверяет права доступа.
        /// </summary>
        public bool ReleasePrinter(string printerName, string revitFileName = null)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new ArgumentException("Printer name cannot be empty", nameof(printerName));
            }

            return ExecuteWithRetry(conn =>
            {
                int affected = conn.Execute(
                    ReleasePrinterSql,
                    new { printerName = printerName.Trim(), revitFileName },
                    commandTimeout: _commandTimeout);

                if (affected > 0)
                {
                    Log.Information("Successfully released printer {PrinterName}", printerName);
                    return true;
                }

                Log.Debug("No printer {PrinterName} was released (may not be reserved by this file)", printerName);
                return false;
            });
        }

        /// <summary>
        /// Автоматически очищает зависшие резервирования.
        /// Вызывается автоматически перед каждым резервированием.
        /// </summary>
        public int CleanupExpiredReservations()
        {
            DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-_lockTimeoutMinutes);

            return ExecuteWithRetry(conn =>
            {
                int cleaned = conn.Execute(
                    CleanupExpiredReservationsSql,
                    new { cutoffTime },
                    commandTimeout: _commandTimeout);

                if (cleaned > 0)
                {
                    Log.Information("Cleaned up {Count} expired printer reservations", cleaned);
                }

                return cleaned;
            });
        }

        /// <summary>
        /// Получает список всех доступных принтеров.
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by_file, reserved_at, process_id, change_token
                FROM printer_states
                WHERE is_available = true
                ORDER BY printer_name";

            return ExecuteWithRetry(conn =>
            {
                return conn.Query<PrinterState>(sql, commandTimeout: _commandTimeout).ToList();
            });
        }

        #region Private Methods

        /// <summary>
        /// Создает ODBC подключение к PostgreSQL.
        /// </summary>
        private OdbcConnection CreateConnection()
        {
            var connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Сортирует принтеры по предпочтениям пользователя.
        /// </summary>
        private static List<PrinterState> OrderByPreference(
            IEnumerable<PrinterState> printers,
            string[] preferredPrinters)
        {
            if (preferredPrinters?.Length > 0)
            {
                var preferredSet = new HashSet<string>(
                    preferredPrinters.Where(p => !string.IsNullOrWhiteSpace(p)),
                    StringComparer.OrdinalIgnoreCase);

                return printers
                    .OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1)
                    .ThenBy(p => p.PrinterName)
                    .ToList();
            }

            return printers.OrderBy(p => p.PrinterName).ToList();
        }

        /// <summary>
        /// Выполняет операцию с retry логикой для обработки сбоев сериализации.
        /// Использует exponential backoff для снижения нагрузки при конкурентном доступе.
        /// </summary>
        private T ExecuteWithRetry<T>(Func<OdbcConnection, T> operation)
        {
            for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
            {
                try
                {
                    using var connection = CreateConnection();
                    return operation(connection);
                }
                catch (OdbcException ex) when (IsSerializationFailure(ex) && attempt < _maxRetryAttempts)
                {
                    int delay = _baseRetryDelayMs * (int)Math.Pow(2, attempt - 1);
                    Thread.Sleep(delay);
                    Log.Debug("Serialization failure on attempt {Attempt}, retrying in {Delay}ms", attempt, delay);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Database operation failed on attempt {Attempt}", attempt);
                    throw;
                }
            }

            // Финальная попытка без retry
            using var finalConnection = CreateConnection();
            return operation(finalConnection);
        }

        /// <summary>
        /// Определяет, является ли исключение ошибкой сериализации, которую можно повторить.
        /// </summary>
        private static bool IsSerializationFailure(OdbcException ex)
        {
            // PostgreSQL коды ошибок сериализации
            string[] serializationErrorCodes = { "40001", "40P01", "25P02" };
            return serializationErrorCodes.Any(code => ex.Message.Contains(code, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                Log.Debug("PrinterService disposed");
                _disposed = true;
            }
        }
    }
}