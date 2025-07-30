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
    /// Сервис управления принтерами с простым собственным логгером.
    /// </summary>
    public sealed class PrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private readonly int _maxRetryAttempts;
        private readonly int _baseRetryDelayMs;
        private readonly int _lockTimeoutMinutes;
        private readonly ILogger _logger;
        private static readonly object _initLock = new object();
        private volatile bool _schemaInitialized = false;
        private bool _disposed = false;

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

            LoggerFactory.Initialize(LoggerLevel.Information);
            _logger = LoggerFactory.CreateLogger<PrinterService>();

            _logger.Information($"PrinterService initialized with {lockTimeoutMinutes}min timeout");

            EnsureSchemaInitialized();
        }

        /// <summary>
        /// Thread-safe инициализация схемы базы данных.
        /// </summary>
        private void EnsureSchemaInitialized()
        {
            if (!_schemaInitialized)
            {
                lock (_initLock)
                {
                    if (!_schemaInitialized)
                    {
                        _logger.Information("Initializing database schema");

                        _ = ExecuteWithRetry(conn =>
                        {
                            conn.Execute(PrinterSqlStore.CreatePrinterStatesTable, commandTimeout: _commandTimeout);
                            _logger.Information("Database schema initialized successfully");
                            return 0;
                        });

                        _schemaInitialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// Инициализирует принтеры в системе.
        /// </summary>
        public void InitializePrinters(params string[] printerNames)
        {
            if (printerNames?.Length == 0)
            {
                _logger.Warning("No printer names provided for initialization");
                return;
            }

            _logger.Information($"Initializing {printerNames.Length} printers");

            var validPrinters = printerNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new { printerName = name })
                .ToList();

            int insertedCount = ExecuteWithRetry(conn =>
            {
                return conn.Execute(PrinterSqlStore.InitializePrinter, validPrinters, commandTimeout: _commandTimeout);
            });

            _logger.Information($"Initialized {insertedCount} printers in database");
        }

        /// <summary>
        /// Резервирует любой доступный принтер из списка предпочтений.
        /// </summary>
        public string TryReserveAnyAvailablePrinter(string revitFilePath, params string[] preferredPrinters)
        {
            if (string.IsNullOrWhiteSpace(revitFilePath))
            {
                throw new ArgumentException("Revit file path cannot be empty", nameof(revitFilePath));
            }

            string revitFileName = Path.GetFileName(revitFilePath);
            _logger.Information($"Attempting to reserve printer for file: {revitFileName}");

            // Автоматическая очистка перед резервированием
            int cleanedCount = CleanupExpiredReservations();
            if (cleanedCount > 0)
            {
                _logger.Information($"Cleaned up {cleanedCount} expired reservations before new reservation");
            }

            int processId = Process.GetCurrentProcess().Id;

            return ExecuteWithRetry(conn =>
            {
                using OdbcTransaction transaction = conn.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    string sql = PrinterSqlStore.GetAvailablePrintersWithLock;
                    List<PrinterState> availablePrinters = conn.Query<PrinterState>(
                        sql: sql,
                        transaction: transaction,
                        commandTimeout: _commandTimeout).ToList();

                    if (!availablePrinters.Any())
                    {
                        _logger.Warning($"No available printers found for {revitFileName}");
                        transaction.Rollback();
                        return null;
                    }

                    _logger.Debug($"Found {availablePrinters.Count} available printers");

                    List<PrinterState> orderedPrinters = OrderByPreference(availablePrinters, preferredPrinters);

                    foreach (PrinterState printer in orderedPrinters)
                    {
                        _logger.Debug($"Attempting to reserve printer: {printer.PrinterName}");

                        int affected = conn.Execute(
                            PrinterSqlStore.ReservePrinter,
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
                            _logger.Information($"Successfully reserved printer {printer.PrinterName} for {revitFileName}");
                            return printer.PrinterName;
                        }
                        else
                        {
                            _logger.Debug($"Failed to reserve printer {printer.PrinterName} (token conflict)");
                        }
                    }

                    transaction.Rollback();
                    _logger.Warning($"Failed to reserve any printer for {revitFileName}");
                    return null;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.Error($"Error during printer reservation for {revitFileName}", ex);
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

            string revitFileName = Path.GetFileName(revitFilePath);
            _logger.Information($"Attempting to reserve specific printer {printerName} for {revitFileName}");

            int processId = Process.GetCurrentProcess().Id;

            return ExecuteWithRetry(conn =>
            {
                using OdbcTransaction transaction = conn.BeginTransaction(IsolationLevel.Serializable);
                try
                {
                    PrinterState printer = conn.QuerySingleOrDefault<PrinterState>(
                        "SELECT * FROM printer_states WHERE printer_name = @printerName FOR UPDATE",
                        new { printerName = printerName.Trim() },
                        transaction,
                        _commandTimeout);

                    if (printer == null)
                    {
                        _logger.Warning($"Printer {printerName} not found in database");
                        transaction.Rollback();
                        return false;
                    }

                    if (!printer.IsAvailable)
                    {
                        _logger.Warning($"Printer {printerName} is not available (reserved by {printer.ReservedByFile})");
                        transaction.Rollback();
                        return false;
                    }

                    int affected = conn.Execute(
                        PrinterSqlStore.ReservePrinter,
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
                        _logger.Information($"Successfully reserved specific printer {printerName} for {revitFileName}");
                        return true;
                    }

                    transaction.Rollback();
                    _logger.Warning($"Failed to reserve specific printer {printerName} (token conflict)");
                    return false;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.Error($"Error reserving specific printer {printerName}", ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Освобождает принтер.
        /// </summary>
        public bool ReleasePrinter(string printerName, string revitFileName = null)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new ArgumentException("Printer name cannot be empty", nameof(printerName));
            }

            string fileInfo = revitFileName != null ? $" for file {revitFileName}" : " (administrative release)";
            _logger.Information($"Releasing printer {printerName}{fileInfo}");

            return ExecuteWithRetry(conn =>
            {
                int affected = conn.Execute(
                    PrinterSqlStore.ReleasePrinter,
                    new { printerName = printerName.Trim(), revitFileName },
                    commandTimeout: _commandTimeout);

                if (affected > 0)
                {
                    _logger.Information($"Successfully released printer {printerName}");
                    return true;
                }

                _logger.Debug($"No printer {printerName} was released (may not be reserved by this file)");
                return false;
            });
        }

        /// <summary>
        /// Очищает зависшие резервирования.
        /// </summary>
        public int CleanupExpiredReservations()
        {
            DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-_lockTimeoutMinutes);
            _logger.Debug($"Cleaning up reservations older than {cutoffTime:yyyy-MM-dd HH:mm:ss}");

            return ExecuteWithRetry(conn =>
            {
                int cleaned = conn.Execute(
                    PrinterSqlStore.CleanupExpiredReservations,
                    new { cutoffTime },
                    commandTimeout: _commandTimeout);

                if (cleaned > 0)
                {
                    _logger.Information($"Cleaned up {cleaned} expired printer reservations");
                }
                else
                {
                    _logger.Debug("No expired reservations found to clean up");
                }

                return cleaned;
            });
        }

        /// <summary>
        /// Получает список доступных принтеров.
        /// </summary>
        public IEnumerable<PrinterState> GetAvailablePrinters()
        {
            _logger.Debug("Retrieving available printers");

            return ExecuteWithRetry(conn =>
            {
                List<PrinterState> printers = conn.Query<PrinterState>(PrinterSqlStore.GetAvailablePrinters, commandTimeout: _commandTimeout).ToList();
                _logger.Debug($"Found {printers.Count} available printers");
                return printers;
            });
        }

        private OdbcConnection CreateConnection()
        {
            OdbcConnection connection = new OdbcConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private static List<PrinterState> OrderByPreference(IEnumerable<PrinterState> printers, string[] preferredPrinters)
        {
            if (preferredPrinters?.Length > 0)
            {
                HashSet<string> preferredSet = new HashSet<string>(
                    preferredPrinters.Where(p => !string.IsNullOrWhiteSpace(p)),
                    StringComparer.OrdinalIgnoreCase);

                return printers
                    .OrderBy(p => preferredSet.Contains(p.PrinterName) ? 0 : 1)
                    .ThenBy(p => p.PrinterName)
                    .ToList();
            }

            return printers.OrderBy(p => p.PrinterName).ToList();
        }

        private T ExecuteWithRetry<T>(Func<OdbcConnection, T> operation)
        {
            for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
            {
                try
                {
                    using OdbcConnection connection = CreateConnection();
                    return operation(connection);
                }
                catch (OdbcException ex) when (IsSerializationFailure(ex) && attempt < _maxRetryAttempts)
                {
                    int delay = _baseRetryDelayMs * (int)Math.Pow(2, attempt);
                    _logger.Warning($"Serialization failure on attempt {attempt}, retrying in {delay}ms");
                    Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Database operation failed on attempt {attempt}", ex);
                    throw;
                }
            }

            throw new InvalidOperationException("Maximum retry attempts exceeded for database operation");

        }


        private static bool IsSerializationFailure(OdbcException ex)
        {
            string[] serializationErrorCodes = { "40001", "40P01", "25P02" };
            return serializationErrorCodes.Any(code => ex.Message.Contains(code));
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.Information("PrinterService disposed");
                _disposed = true;
            }
        }



    }
}