// Database/SafePostgreSqlPrinterService.cs
using Dapper;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Database
{
    /// <summary>
    /// Максимально безопасный сервис управления принтерами с SERIALIZABLE изоляцией
    /// </summary>
    public sealed class SafePostgreSqlPrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private readonly int _maxRetryAttempts;
        private readonly int _retryDelayMs;
        private readonly string _machineName;
        private bool _disposed = false;

        public SafePostgreSqlPrinterService(
            string connectionString,
            int commandTimeout = 30,
            int maxRetryAttempts = 3,
            int retryDelayMs = 100)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
            _maxRetryAttempts = maxRetryAttempts;
            _retryDelayMs = retryDelayMs;
            _machineName = Environment.MachineName;

            InitializeDatabase();
        }

        /// <summary>
        /// Создает БД с максимальными constraints для целостности данных
        /// </summary>
        private void InitializeDatabase()
        {
            StringBuilder sql = new StringBuilder();
            sql.AppendLine("CREATE TABLE IF NOT EXISTS printer_states (");
            sql.AppendLine("    id SERIAL PRIMARY KEY,");
            sql.AppendLine("    printer_name VARCHAR(100) NOT NULL,");
            sql.AppendLine("    is_available BOOLEAN NOT NULL DEFAULT true,");
            sql.AppendLine("    reserved_by VARCHAR(100) NULL,");
            sql.AppendLine("    reserved_at TIMESTAMP WITH TIME ZONE NULL,");
            sql.AppendLine("    process_id INTEGER NULL,");
            sql.AppendLine("    machine_name VARCHAR(100) NULL,");
            sql.AppendLine("    change_token UUID NOT NULL DEFAULT gen_random_uuid(),");
            sql.AppendLine("    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,");
            sql.AppendLine("    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,");
            sql.AppendLine("    CONSTRAINT uk_printer_name UNIQUE (printer_name),");
            sql.AppendLine("    CONSTRAINT chk_reserved_logic CHECK (");
            sql.AppendLine("        (is_available = true AND reserved_by IS NULL AND reserved_at IS NULL AND process_id IS NULL) OR");
            sql.AppendLine("        (is_available = false AND reserved_by IS NOT NULL AND reserved_at IS NOT NULL AND process_id IS NOT NULL)");
            sql.AppendLine("    ),");
            sql.AppendLine("    CONSTRAINT chk_printer_name_not_empty CHECK (LENGTH(TRIM(printer_name)) > 0)");
            sql.AppendLine(");");

            ExecuteWithRetry(connection =>
            {
                return connection.Execute(sql.ToString(), commandTimeout: _commandTimeout);
            });
        }

        /// <summary>
        /// Инициализирует принтеры с проверкой целостности
        /// </summary>
        public void InitializePrinters(params string[] printerNames)
        {
            if (printerNames == null || printerNames.Length == 0)
                return;

            StringBuilder sql = new StringBuilder();
            sql.AppendLine("INSERT INTO printer_states (printer_name, is_available, change_token, machine_name)");
            sql.AppendLine("VALUES (@printerName, true, gen_random_uuid(), @machineName)");
            sql.AppendLine("ON CONFLICT (printer_name) DO NOTHING");

            var parameters = printerNames.Select(name => new
            {
                printerName = name?.Trim(),
                machineName = _machineName
            }).Where(p => !string.IsNullOrEmpty(p.printerName));

            ExecuteWithRetry(connection =>
            {
                return connection.Execute(sql.ToString(), parameters, commandTimeout: _commandTimeout);
            });
        }

        /// <summary>
        /// Резервирует любой доступный принтер с максимальной изоляцией
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string reservedBy, params string[] preferredPrinters)
        {
            if (string.IsNullOrWhiteSpace(reservedBy))
                throw new ArgumentException("ReservedBy cannot be null or empty", nameof(reservedBy));

            return ExecuteWithRetry(connection =>
            {
                using IDbTransaction transaction = BeginSerializableTransaction(connection);
                try
                {
                    // Получаем доступные принтеры с блокировкой
                    List<PrinterState> availablePrinters = GetAvailablePrintersWithLock(connection, transaction);

                    if (!availablePrinters.Any())
                        return null;

                    // Упорядочиваем по предпочтениям
                    IEnumerable<PrinterState> orderedPrinters = OrderByPreference(availablePrinters, preferredPrinters);

                    // Пытаемся зарезервировать первый доступный
                    foreach (PrinterState printer in orderedPrinters)
                    {
                        if (TryReserveSpecificPrinterInternal(connection, transaction, printer.PrinterName, reservedBy))
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
        /// Резервирует конкретный принтер с максимальной безопасностью
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string reservedBy)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("PrinterName cannot be null or empty", nameof(printerName));

            if (string.IsNullOrWhiteSpace(reservedBy))
                throw new ArgumentException("ReservedBy cannot be null or empty", nameof(reservedBy));

            return ExecuteWithRetry(connection =>
            {
                using IDbTransaction transaction = BeginSerializableTransaction(connection);
                try
                {
                    bool success = TryReserveSpecificPrinterInternal(connection, transaction, printerName, reservedBy);

                    if (success)
                        transaction.Commit();
                    else
                        transaction.Rollback();

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
        /// Освобождает принтер с проверкой прав доступа
        /// </summary>
        public bool ReleasePrinter(string printerName, string releasedBy = null)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("PrinterName cannot be null or empty", nameof(printerName));

            StringBuilder sql = new StringBuilder();
            sql.AppendLine("UPDATE printer_states SET");
            sql.AppendLine("    is_available = true,");
            sql.AppendLine("    reserved_by = NULL,");
            sql.AppendLine("    reserved_at = NULL,");
            sql.AppendLine("    process_id = NULL,");
            sql.AppendLine("    machine_name = NULL,");
            sql.AppendLine("    change_token = gen_random_uuid(),");
            sql.AppendLine("    updated_at = CURRENT_TIMESTAMP");
            sql.AppendLine("WHERE printer_name = @printerName");

            // Добавляем проверку прав если указан releasedBy
            if (!string.IsNullOrWhiteSpace(releasedBy))
            {
                sql.AppendLine("  AND (reserved_by = @releasedBy OR reserved_by IS NULL)");
            }

            return ExecuteWithRetry(connection =>
            {
                int affectedRows = connection.Execute(
                    sql.ToString(),
                    new { printerName = printerName.Trim(), releasedBy = releasedBy?.Trim() },
                    commandTimeout: _commandTimeout);

                return affectedRows > 0;
            });
        }

        /// <summary>
        /// Получает все доступные принтеры (только для чтения)
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            StringBuilder sql = new StringBuilder();
            sql.AppendLine("SELECT id, printer_name, is_available, reserved_by,");
            sql.AppendLine("       reserved_at, process_id, machine_name, change_token,");
            sql.AppendLine("       created_at, updated_at");
            sql.AppendLine("FROM printer_states");
            sql.AppendLine("WHERE is_available = true");
            sql.AppendLine("ORDER BY printer_name");

            return ExecuteWithRetry(connection =>
            {
                return connection.Query<PrinterState>(sql.ToString(), commandTimeout: _commandTimeout).ToList();
            });
        }

        /// <summary>
        /// Очищает зависшие резервирования с проверкой процессов
        /// </summary>
        public int CleanupExpiredReservations(TimeSpan maxAge)
        {
            DateTime cutoffTime = DateTime.UtcNow.Subtract(maxAge);

            StringBuilder sql = new StringBuilder();
            sql.AppendLine("UPDATE printer_states SET");
            sql.AppendLine("    is_available = true,");
            sql.AppendLine("    reserved_by = NULL,");
            sql.AppendLine("    reserved_at = NULL,");
            sql.AppendLine("    process_id = NULL,");
            sql.AppendLine("    machine_name = NULL,");
            sql.AppendLine("    change_token = gen_random_uuid(),");
            sql.AppendLine("    updated_at = CURRENT_TIMESTAMP");
            sql.AppendLine("WHERE is_available = false");
            sql.AppendLine("  AND reserved_at < @cutoffTime");

            return ExecuteWithRetry(connection =>
            {
                return connection.Execute(sql.ToString(), new { cutoffTime }, commandTimeout: _commandTimeout);
            });
        }

        #region Private Methods

        /// <summary>
        /// Создает соединение с максимальным уровнем изоляции
        /// </summary>
        private OdbcConnection CreateConnection()
        {
            OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Начинает транзакцию с SERIALIZABLE изоляцией
        /// </summary>
        private IDbTransaction BeginSerializableTransaction(IDbConnection connection)
        {
            return connection.BeginTransaction(IsolationLevel.Serializable);
        }

        /// <summary>
        /// Получает доступные принтеры с явной блокировкой
        /// </summary>
        private List<PrinterState> GetAvailablePrintersWithLock(IDbConnection connection, IDbTransaction transaction)
        {
            StringBuilder sql = new StringBuilder();
            sql.AppendLine("SELECT id, printer_name, is_available, reserved_by,");
            sql.AppendLine("       reserved_at, process_id, machine_name, change_token");
            sql.AppendLine("FROM printer_states");
            sql.AppendLine("WHERE is_available = true");
            sql.AppendLine("ORDER BY printer_name");
            sql.AppendLine("FOR UPDATE"); // Явная блокировка строк

            return connection.Query<PrinterState>(
                sql.ToString(),
                transaction: transaction,
                commandTimeout: _commandTimeout).ToList();
        }

        /// <summary>
        /// Внутренняя реализация резервирования с optimistic locking
        /// </summary>
        private bool TryReserveSpecificPrinterInternal(
            IDbConnection connection,
            IDbTransaction transaction,
            string printerName,
            string reservedBy)
        {
            // Читаем текущее состояние с блокировкой
            StringBuilder selectSql = new StringBuilder();
            selectSql.AppendLine("SELECT change_token, is_available");
            selectSql.AppendLine("FROM printer_states");
            selectSql.AppendLine("WHERE printer_name = @printerName");
            selectSql.AppendLine("FOR UPDATE");

            var currentState = connection.QuerySingleOrDefault<(Guid changeToken, bool isAvailable)>(
                selectSql.ToString(),
                new { printerName = printerName.Trim() },
                transaction,
                _commandTimeout);

            if (currentState.changeToken == Guid.Empty || !currentState.isAvailable)
                return false;

            // Обновляем с новым токеном
            StringBuilder updateSql = new StringBuilder();
            updateSql.AppendLine("UPDATE printer_states SET");
            updateSql.AppendLine("    is_available = false,");
            updateSql.AppendLine("    reserved_by = @reservedBy,");
            updateSql.AppendLine("    reserved_at = @reservedAt,");
            updateSql.AppendLine("    process_id = @processId,");
            updateSql.AppendLine("    machine_name = @machineName,");
            updateSql.AppendLine("    change_token = @newToken,");
            updateSql.AppendLine("    updated_at = CURRENT_TIMESTAMP");
            updateSql.AppendLine("WHERE printer_name = @printerName");
            updateSql.AppendLine("  AND change_token = @expectedToken");

            Process currentProcess = Process.GetCurrentProcess();
            int affectedRows = connection.Execute(
                updateSql.ToString(),
                new
                {
                    printerName = printerName.Trim(),
                    reservedBy = reservedBy.Trim(),
                    reservedAt = DateTime.UtcNow,
                    processId = currentProcess.Id,
                    machineName = _machineName,
                    newToken = Guid.NewGuid(),
                    expectedToken = currentState.changeToken
                },
                transaction,
                _commandTimeout);

            return affectedRows > 0;
        }

        /// <summary>
        /// Упорядочивает принтеры по предпочтениям
        /// </summary>
        private static IEnumerable<PrinterState> OrderByPreference(IEnumerable<PrinterState> printers, string[] preferredPrinters)
        {
            if (preferredPrinters?.Length > 0)
            {
                HashSet<string> preferredSet = new HashSet<string>(preferredPrinters.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()), StringComparer.OrdinalIgnoreCase);

                return printers.OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1).ThenBy(p => p.PrinterName);
            }

            return printers.OrderBy(p => p.PrinterName);
        }

        /// <summary>
        /// Выполняет операции с retry механизмом для serialization conflicts
        /// </summary>
        private T ExecuteWithRetry<T>(Func<OdbcConnection, T> operation)
        {
            int attempt = 0;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    using OdbcConnection conn = CreateConnection();
                    return operation(conn);
                }
                catch (OdbcException ex) when (IsSerializationFailure(ex) && attempt < _maxRetryAttempts - 1)
                {
                    attempt++;
                    Thread.Sleep(_retryDelayMs * attempt); // Экспоненциальная задержка
                }
            }

            // Последняя попытка без перехвата исключений
            using OdbcConnection connFinal = CreateConnection();
            return operation(connFinal);
        }

        /// <summary>
        /// Проверяет является ли исключение serialization failure
        /// </summary>
        private static bool IsSerializationFailure(OdbcException ex)
        {
            // PostgreSQL serialization failure codes
            string[] serializationErrorCodes = { "40001", "40P01" };
            return serializationErrorCodes.Any(code => ex.Message.Contains(code));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion
    }
}