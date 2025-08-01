using Dapper;
using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Text;

namespace Database.Services
{
    public sealed class PrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private readonly int _maxRetryAttempts;
        private readonly int _baseRetryDelayMs;
        private readonly int _lockTimeoutMinutes;
        private readonly ILogger _logger;
        private bool _disposed = false;

        public PrinterService(
            string connection,
            int commandTimeout = 30,
            int maxRetryAttempts = 10,
            int baseRetryDelayMs = 100,
            int lockTimeoutMinutes = 30)
        {
            _connectionString = connection;
            _commandTimeout = commandTimeout;
            _maxRetryAttempts = maxRetryAttempts;
            _baseRetryDelayMs = baseRetryDelayMs;
            _lockTimeoutMinutes = lockTimeoutMinutes;

            LoggerFactory.Initialize(LoggerLevel.Debug);

            _logger = LoggerFactory.CreateLogger<PrinterService>();

            _logger.Information($"Initialized with timeout {commandTimeout}s, max retries {maxRetryAttempts}");
        }

        /// <summary>
        /// Пытается зарезервировать доступный принтер.
        /// </summary>
        public bool TryReserveAvailablePrinter(string revitFileName, string[] availablePrinterNames, out string reservedPrinterName)
        {
            reservedPrinterName = null;
            StringBuilder logBuilder = new();

            _ = logBuilder.AppendLine($"Starting reservation attempt for file: {revitFileName}");
            _ = logBuilder.AppendLine($"Available printers: [{string.Join(", ", availablePrinterNames)}]");

            string localReservedPrinterName = null;

            bool success = ExecuteWithRetry(() =>
            {
                using OdbcConnection connection = new OdbcConnection(_connectionString);
                using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    InitializePrinters(connection, transaction, availablePrinterNames);

                    PrinterInfo selectedPrinter = GetAvailablePrinter(connection, transaction, availablePrinterNames);

                    if (selectedPrinter is null)
                    {
                        transaction.Rollback();
                        _logger.Debug(logBuilder.ToString());
                        return false;
                    }

                    if (ReservePrinterInternal(connection, transaction, selectedPrinter, revitFileName))
                    {
                        transaction.Commit();
                        localReservedPrinterName = selectedPrinter.PrinterName;

                        _ = logBuilder.AppendLine($"Successfully reserved printer {selectedPrinter.PrinterName}");
                        _logger.Information(logBuilder.ToString());
                        return true;
                    }

                    transaction.Rollback();
                    _ = logBuilder.AppendLine($"Failed to reserve printer {selectedPrinter.PrinterName}");
                    _logger.Warning(logBuilder.ToString());
                    return false;
                }
                catch
                {
                    transaction.Rollback();
                    _logger.Error(logBuilder.ToString());
                    throw;
                }
            });

            reservedPrinterName = localReservedPrinterName;
            return success;
        }

        /// <summary>
        /// Пытается зарезервировать конкретный принтер.
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string revitFileName)
        {
            StringBuilder logBuilder = new();

            logBuilder.AppendLine($"Starting specific printer reservation: {printerName}");

            return ExecuteWithRetry(() =>
            {
                Stopwatch transactionTimer = Stopwatch.StartNew();

                using OdbcConnection connection = new OdbcConnection(_connectionString);
                using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    PrinterInfo printerInfo = GetPrinterInfoWithLock(connection, transaction, printerName);

                    if (printerInfo == null)
                    {
                        _ = logBuilder.AppendLine($"Printer {printerName} not found in database");
                        transaction.Rollback();
                        transactionTimer.Stop();
                        _ = logBuilder.AppendLine($"Transaction completed in {transactionTimer.ElapsedMilliseconds}ms (rolled back)");
                        _logger.Warning(logBuilder.ToString());
                        return false;
                    }

                    if (!printerInfo.IsAvailable)
                    {
                        _ = logBuilder.AppendLine($"Printer {printerName} is already reserved");
                        transaction.Rollback();
                        transactionTimer.Stop();
                        _ = logBuilder.AppendLine($"Transaction completed in {transactionTimer.ElapsedMilliseconds}ms (rolled back)");
                        _logger.Debug(logBuilder.ToString());
                        return false;
                    }

                    if (ReservePrinterInternal(connection, transaction, printerInfo, revitFileName))
                    {
                        transaction.Commit();
                        transactionTimer.Stop();
                        _ = logBuilder.AppendLine($"Successfully reserved printer {printerName}");
                        _ = logBuilder.AppendLine($"Transaction completed in {transactionTimer.ElapsedMilliseconds}ms (committed)");
                        _logger.Information(logBuilder.ToString());
                        return true;
                    }

                    transaction.Rollback();
                    transactionTimer.Stop();
                    _ = logBuilder.AppendLine($"Failed to reserve printer {printerName}");
                    _ = logBuilder.AppendLine($"Transaction completed in {transactionTimer.ElapsedMilliseconds}ms (rolled back)");
                    _logger.Warning(logBuilder.ToString());
                    return false;
                }
                catch
                {
                    transaction.Rollback();
                    transactionTimer.Stop();
                    _ = logBuilder.AppendLine($"Transaction failed and rolled back in {transactionTimer.ElapsedMilliseconds}ms");
                    _logger.Error(logBuilder.ToString());
                    throw;
                }
            });
        }

        /// <summary>
        /// Освобождает принтер от резервирования.
        /// </summary>
        public bool TryReleasePrinter(string printerName, string revitFileName = null)
        {
            if (string.IsNullOrEmpty(printerName))
            {
                return false;
            }

            StringBuilder logBuilder = new();

            _ = logBuilder.AppendLine($"Starting printer release: {printerName}");

            if (!string.IsNullOrEmpty(revitFileName))
            {
                _ = logBuilder.AppendLine($"File: {revitFileName}");
            }

            return ExecuteWithRetry(() =>
            {
                Stopwatch transactionTimer = Stopwatch.StartNew();

                using OdbcConnection connection = new OdbcConnection(_connectionString);
                using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    string sql = PrinterSqlStore.ReleasePrinter;

                    PrinterInfo releaseParams = new()
                    {
                        PrinterName = printerName,
                        RevitFileName = revitFileName
                    };

                    int rowsAffected = connection.Execute(sql, releaseParams, transaction, _commandTimeout);

                    if (rowsAffected > 0)
                    {
                        transaction.Commit();
                        transactionTimer.Stop();
                        _ = logBuilder.AppendLine($"Successfully released printer {printerName}");
                        _ = logBuilder.AppendLine($"Transaction completed in {transactionTimer.ElapsedMilliseconds}ms (committed)");
                        _logger.Information(logBuilder.ToString());
                        return true;
                    }

                    transaction.Rollback();
                    transactionTimer.Stop();
                    _ = logBuilder.AppendLine($"Failed to release printer {printerName} - either not found or access denied");
                    _ = logBuilder.AppendLine($"Transaction completed in {transactionTimer.ElapsedMilliseconds}ms (rolled back)");
                    _logger.Warning(logBuilder.ToString());
                    return false;
                }
                catch
                {
                    transaction.Rollback();
                    transactionTimer.Stop();
                    _ = logBuilder.AppendLine($"Transaction failed and rolled back in {transactionTimer.ElapsedMilliseconds}ms");
                    _logger.Error(logBuilder.ToString());
                    throw;
                }
            });
        }

        /// <summary>
        /// Очищает зависшие резервирования принтеров.
        /// </summary>
        public int CleanupExpiredReservations()
        {
            StringBuilder logBuilder = new();

            DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-_lockTimeoutMinutes);

            _ = logBuilder.AppendLine("Starting cleanup of expired printer reservations");
            _ = logBuilder.AppendLine($"Cutoff time: {cutoffTime:yyyy-MM-dd HH:mm:ss} UTC");

            return ExecuteWithRetry(() =>
            {
                using OdbcConnection connection = new OdbcConnection(_connectionString);
                using OdbcTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);

                try
                {
                    string sql = PrinterSqlStore.CleanupExpiredReservations;

                    int cleanedCount = connection.Execute(sql, new { cutoffTime }, transaction, _commandTimeout);

                    transaction.Commit();

                    logBuilder.AppendLine($"Cleaned up {cleanedCount} expired printer reservations");

                    _logger.Information(logBuilder.ToString());

                    return cleanedCount;
                }
                catch
                {
                    transaction.Rollback();
                    _logger.Error(logBuilder.ToString());
                    throw;
                }
            });
        }

        /// <summary>
        /// Инициализирует принтеры в базе данных если они отсутствуют.
        /// </summary>
        private void InitializePrinters(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames)
        {
            string sql = PrinterSqlStore.InitializePrinter;

            foreach (string printerName in printerNames)
            {
                try
                {
                    PrinterInfo printer = new() { PrinterName = printerName };
                    _ = connection.Execute(sql, printer, transaction, _commandTimeout);
                    _logger.Debug($"Initialized printer: {printerName}");
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Printer {printerName} already exists or initialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Получает первый доступный принтер с блокировкой.
        /// </summary>
        private PrinterInfo GetAvailablePrinter(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames)
        {
            string sql = PrinterSqlStore.GetSingleAvailablePrinterWithLock;

            return connection.QueryFirstOrDefault<PrinterInfo>(sql, new { printerNames }, transaction, commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Получает информацию о конкретном принтере с блокировкой.
        /// </summary>
        private PrinterInfo GetPrinterInfoWithLock(OdbcConnection connection, OdbcTransaction transaction, string printerName)
        {
            string sql = PrinterSqlStore.GetAvailablePrintersWithLock + " AND printer_name = @printerName";

            PrinterInfo searchParams = new() { PrinterName = printerName };

            return connection.QueryFirstOrDefault<PrinterInfo>(sql, searchParams, transaction, _commandTimeout);
        }

        /// <summary>
        /// Внутренний метод для резервирования принтера с optimistic locking.
        /// </summary>
        private bool ReservePrinterInternal(OdbcConnection connection, OdbcTransaction transaction, PrinterInfo printerInfo, string revitFileName)
        {
            string sql = PrinterSqlStore.ReservePrinter;

            int processId = Process.GetCurrentProcess().Id;
            DateTime reservedAt = DateTime.UtcNow;

            PrinterInfo reservationParams = new()
            {
                PrinterName = printerInfo.PrinterName,
                RevitFileName = revitFileName,
                ProcessId = processId,
                VersionToken = printerInfo.VersionToken
            };

            // Добавляем reservedAt как отдельный параметр, так как его нет в модели
            var parameters = new
            {
                printerName = reservationParams.PrinterName,
                revitFileName = reservationParams.RevitFileName,
                reservedAt,
                processId = reservationParams.ProcessId,
                expectedToken = reservationParams.VersionToken
            };

            int rowsAffected = connection.Execute(sql, parameters, transaction, _commandTimeout);

            bool success = rowsAffected > 0;

            if (!success)
            {
                _logger.Warning($"Failed to reserve printer {printerInfo.PrinterName} - optimistic lock conflict or printer unavailable");
            }

            return success;
        }

        /// <summary>
        /// Выполняет операцию с retry-логикой при конфликтах транзакций.
        /// </summary>
        private T ExecuteWithRetry<T>(Func<T> operation)
        {
            Exception lastException = null;
            StringBuilder logBuilder = new();
            Stopwatch totalTimer = Stopwatch.StartNew();

            _ = logBuilder.AppendLine("Starting operation with retry logic");

            for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
            {
                try
                {
                    _ = logBuilder.AppendLine($"Attempt {attempt} of {_maxRetryAttempts}");

                    T result = operation();

                    totalTimer.Stop();

                    _ = logBuilder.AppendLine($"Operation succeeded on attempt {attempt}");
                    _ = logBuilder.AppendLine($"Total duration: {totalTimer.ElapsedMilliseconds}ms");

                    _logger.Debug(logBuilder.ToString());

                    return result;
                }
                catch (OdbcException ex) when (IsRetryableException(ex) && attempt < _maxRetryAttempts)
                {
                    lastException = ex;

                    int delay = _baseRetryDelayMs * (int)Math.Pow(2, attempt - 1);
                    _ = logBuilder.AppendLine($"Retryable exception: {ex.Message}");
                    _ = logBuilder.AppendLine($"Retrying in {delay}ms");

                    System.Threading.Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    totalTimer.Stop();

                    _ = logBuilder.AppendLine($"Total duration: {totalTimer.ElapsedMilliseconds}ms");
                    _ = logBuilder.AppendLine($"Exception: {ex.Message}");

                    _logger.Error(logBuilder.ToString(), ex);
                    throw;
                }
            }

            totalTimer.Stop();

            _ = logBuilder.AppendLine($"Operation failed after {_maxRetryAttempts} attempts");
            _ = logBuilder.AppendLine($"Total duration: {totalTimer.ElapsedMilliseconds}ms");
            _ = logBuilder.AppendLine($"Last exception: {lastException?.Message}");

            _logger.Error(logBuilder.ToString(), lastException);

            throw new InvalidOperationException($"Operation failed {lastException?.Message}", lastException);
        }

        /// <summary>
        /// Определяет, является ли исключение подходящим для повтора операции.
        /// </summary>
        private static bool IsRetryableException(OdbcException ex)
        {
            string message = ex.Message;
            return message.Contains("serialization", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("lock", StringComparison.OrdinalIgnoreCase);
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                _logger.Debug("PrinterService disposed");
                _disposed = true;
            }
        }
    }
}