using Dapper;
using Database.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Linq;

namespace Database
{
    /// <summary>
    /// Прямая работа с PostgreSQL через ODBC и Dapper
    /// </summary>
    public class PostgreSqlPrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private bool _disposed = false;

        private const string CreateTableSql = @"
        CREATE TABLE IF NOT EXISTS printer_states (
            id SERIAL PRIMARY KEY,
            printer_name VARCHAR(100) NOT NULL UNIQUE,
            is_available BOOLEAN NOT NULL DEFAULT true,
            reserved_by VARCHAR(100) NULL,
            reserved_at TIMESTAMP WITH TIME ZONE NULL,
            process_id INTEGER NULL,
            change_token UUID NOT NULL DEFAULT gen_random_uuid()
        );";

        private const string SelectForReadSql = @"
        SELECT change_token, is_available
        FROM printer_states 
        WHERE printer_name = @printerName 
          AND is_available = true";

        private const string ReservePrinterSql = @"
            UPDATE printer_states 
            SET is_available = false,
                reserved_by = @reservedBy,
                reserved_at = @reservedAt,
                process_id = @processId,
                change_token = @newToken
            WHERE printer_name = @printerName 
              AND change_token = @expectedToken";

        private const string ReleasePrinterSql = @"
            UPDATE printer_states 
            SET is_available = true,
                reserved_by = NULL,
                reserved_at = NULL,
                process_id = NULL,
                change_token = gen_random_uuid()
            WHERE printer_name = @printerName";

        public PostgreSqlPrinterService(string connectionString, int commandTimeout = 30)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _commandTimeout = commandTimeout;
            InitializeDatabase();
        }

        /// <summary>
        /// Создает таблицы если их нет
        /// </summary>
        private void InitializeDatabase()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();
            _ = connection.Execute(CreateTableSql, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Инициализирует список принтеров в системе
        /// </summary>
        public void InitializePrinters(params string[] printerNames)
        {
            if (printerNames == null || printerNames.Length == 0)
            {
                return;
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            const string insertSql = @"
                INSERT INTO printer_states (printer_name, is_available, change_token)
                VALUES (@printerName, true, gen_random_uuid())
                ON CONFLICT (printer_name) DO NOTHING";

            var parameters = printerNames.Select(name => new { printerName = name });
            _ = connection.Execute(insertSql, parameters, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Пытается зарезервировать любой доступный принтер
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string reservedBy, params string[] preferredPrinters)
        {
            if (string.IsNullOrEmpty(reservedBy))
            {
                throw new ArgumentException("ReservedBy cannot be null or empty", nameof(reservedBy));
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            IEnumerable<PrinterState> availablePrinters = GetAvailablePrinters(connection);

            if (!availablePrinters.Any())
            {
                return null;
            }

            IEnumerable<PrinterState> orderedPrinters = OrderByPreference(availablePrinters, preferredPrinters);

            PrinterState reservedPrinter = orderedPrinters.FirstOrDefault(printer =>
                TryReserveSpecificPrinter(connection, printer.PrinterName, reservedBy));

            return reservedPrinter?.PrinterName;
        }

        /// <summary>
        /// Резервирует конкретный принтер
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string reservedBy)
        {
            if (string.IsNullOrEmpty(printerName))
            {
                throw new ArgumentException("PrinterName cannot be null or empty", nameof(printerName));
            }

            if (string.IsNullOrEmpty(reservedBy))
            {
                throw new ArgumentException("ReservedBy cannot be null or empty", nameof(reservedBy));
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            return TryReserveSpecificPrinter(connection, printerName, reservedBy);
        }

        /// <summary>
        /// Внутренняя реализация резервирования
        /// </summary>
        private bool TryReserveSpecificPrinter(IDbConnection connection, string printerName, string reservedBy)
        {
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                // Читаем текущий токен
                (Guid changeToken, bool isAvailable) = connection.QuerySingleOrDefault<(Guid changeToken, bool isAvailable)>(
                    SelectForReadSql,
                    new { printerName },
                    transaction,
                    _commandTimeout);

                if (changeToken == Guid.Empty || !isAvailable)
                {
                    transaction.Rollback();
                    return false;
                }

                // Обновляем с новым токеном
                Guid newToken = Guid.NewGuid();
                Process currentProcess = Process.GetCurrentProcess();

                int affectedRows = connection.Execute(
                    ReservePrinterSql,
                    new
                    {
                        printerName,
                        reservedBy,
                        reservedAt = DateTime.UtcNow,
                        processId = currentProcess.Id,
                        newToken,
                        expectedToken = changeToken
                    },
                    transaction,
                    _commandTimeout);

                if (affectedRows > 0)
                {
                    transaction.Commit();
                    return true;
                }
                else
                {
                    transaction.Rollback();
                    return false;
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Освобождает зарезервированный принтер
        /// </summary>
        public bool ReleasePrinter(string printerName)
        {
            if (string.IsNullOrEmpty(printerName))
            {
                throw new ArgumentException("PrinterName cannot be null or empty", nameof(printerName));
            }

            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            int affectedRows = connection.Execute(
                ReleasePrinterSql,
                new { printerName },
                commandTimeout: _commandTimeout);

            return affectedRows > 0;
        }

        /// <summary>
        /// Получает список всех доступных принтеров
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            return GetAvailablePrinters(connection);
        }

        /// <summary>
        /// Внутренняя реализация получения доступных принтеров
        /// </summary>
        private IEnumerable<PrinterState> GetAvailablePrinters(IDbConnection connection)
        {
            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, process_id, change_token
                FROM printer_states 
                WHERE is_available = true";

            return connection.Query<PrinterState>(sql, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Получает полную информацию о всех принтерах
        /// </summary>
        public IEnumerable<PrinterState> GetAllPrinters()
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();

            const string sql = @"
                SELECT id, printer_name, is_available, reserved_by, reserved_at, process_id, change_token
                FROM printer_states";

            return connection.Query<PrinterState>(sql, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Очищает зависшие резервирования
        /// </summary>
        public int CleanupExpiredReservations(TimeSpan maxAge)
        {
            using OdbcConnection connection = new OdbcConnection(_connectionString);

            connection.Open();

            DateTime cutoffTime = DateTime.UtcNow.Subtract(maxAge);

            const string sql = @"
                UPDATE printer_states 
                SET is_available = true,
                    reserved_by = NULL,
                    reserved_at = NULL,
                    process_id = NULL,
                    change_token = gen_random_uuid()
                WHERE is_available = false 
                  AND reserved_at < @cutoffTime";

            return connection.Execute(sql, new { cutoffTime }, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Упорядочивает принтеры по предпочтениям
        /// </summary>
        private static IEnumerable<PrinterState> OrderByPreference(IEnumerable<PrinterState> printers, string[] preferredPrinters)
        {
            if (preferredPrinters is null || preferredPrinters.Length == 0)
            {
                return printers;
            }

            HashSet<string> preferredSet = new HashSet<string>(preferredPrinters, StringComparer.OrdinalIgnoreCase);

            return printers.OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1);
        }


        #region Dispose methode

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion


    }
}