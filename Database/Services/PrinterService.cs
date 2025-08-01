using Dapper;
using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;

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
            int maxRetryAttempts = 3,
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
        public string TryReserveAvailablePrinter(string revitFileName, string[] availablePrinterNames)
        {
            _logger.Information($"Attempting to reserve any available printer for file: {revitFileName}");

            return ExecuteWithRetry(() =>
            {
                using OdbcConnection connection = CreateConnection();
                using OdbcTransaction transaction = BeginSerializableTransaction(connection);

                try
                {
                    InitializePrinters(connection, transaction, availablePrinterNames);

                    PrinterInfo selectedPrinter = GetAvailablePrinter(connection, transaction, availablePrinterNames);

                    if (selectedPrinter is null)
                    {
                        _logger.Debug("No available printers found");
                        transaction.Rollback();
                        return null;
                    }

                    if (ReservePrinterInternal(connection, transaction, selectedPrinter.PrinterName, revitFileName, selectedPrinter.VersionToken))
                    {
                        transaction.Commit();
                        _logger.Information($"Successfully reserved printer {selectedPrinter.PrinterName} for {revitFileName}");
                        return selectedPrinter.PrinterName;
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
        /// Пытается зарезервировать конкретный принтер.
        /// </summary>
        public bool TryReserveSpecificPrinter(string printerName, string revitFileName)
        {
            _logger.Information($"Attempting to reserve specific printer {printerName} for file: {revitFileName}");

            return ExecuteWithRetry(() =>
            {
                using OdbcConnection connection = CreateConnection();
                using OdbcTransaction transaction = BeginSerializableTransaction(connection);

                try
                {
                    // Получаем информацию о принтере с блокировкой
                    PrinterInfo printerInfo = GetPrinterInfoWithLock(connection, transaction, printerName);

                    if (printerInfo == null)
                    {
                        _logger.Warning($"Printer {printerName} not found in database");
                        transaction.Rollback();
                        return false;
                    }

                    if (!printerInfo.IsAvailable)
                    {
                        _logger.Debug($"Printer {printerName} is already reserved");
                        transaction.Rollback();
                        return false;
                    }

                    if (ReservePrinterInternal(connection, transaction, printerName, revitFileName, printerInfo.VersionToken))
                    {
                        transaction.Commit();
                        _logger.Information($"Successfully reserved printer {printerName} for {revitFileName}");
                        return true;
                    }

                    transaction.Rollback();
                    return false;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Освобождает принтер от резервирования.
        /// </summary>
        public bool TryReleasePrinter(string printerName, string revitFileName = null)
        {
            if (!string.IsNullOrEmpty(printerName))
            {
                _logger.Information($"Attempting to release printer {printerName}");

                return ExecuteWithRetry(() =>
                {
                    using OdbcConnection connection = CreateConnection();
                    using OdbcTransaction transaction = BeginSerializableTransaction(connection);

                    try
                    {
                        string sql = PrinterSqlStore.ReleasePrinter;

                        int rowsAffected = connection.Execute(sql, new { printerName, revitFileName }, transaction, _commandTimeout);

                        if (rowsAffected > 0)
                        {
                            transaction.Commit();
                            _logger.Information($"Successfully released printer {printerName}");
                            return true;
                        }

                        _logger.Warning($"Failed to release printer {printerName} - either not found or access denied");
                        transaction.Rollback();
                        return false;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                });
            }

            return false;
        }

        /// <summary>
        /// Очищает зависшие резервирования принтеров.
        /// Освобождает принтеры, зарезервированные более указанного времени назад.
        /// </summary>
        public int CleanupExpiredReservations()
        {
            _logger.Information("Starting cleanup of expired printer reservations");

            TimeSpan lockTimeoutSpan = TimeSpan.FromMinutes(_lockTimeoutMinutes);
            DateTime cutoffTime = DateTime.UtcNow.Subtract(lockTimeoutSpan);

            return ExecuteWithRetry(() =>
            {
                using OdbcConnection connection = CreateConnection();
                using OdbcTransaction transaction = BeginSerializableTransaction(connection);

                try
                {
                    int cleanedCount = connection.Execute(
                        PrinterSqlStore.CleanupExpiredReservations,
                        new { cutoffTime },
                        transaction,
                        _commandTimeout);

                    transaction.Commit();

                    if (cleanedCount > 0)
                    {
                        _logger.Information($"Cleaned up {cleanedCount} expired printer reservations");
                    }
                    else
                    {
                        _logger.Debug("No expired reservations found");
                    }

                    return cleanedCount;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        /// <summary>
        /// Инициализирует принтеры в базе данных если они отсутствуют.
        /// </summary>
        private void InitializePrinters(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames)
        {
            foreach (string printerName in printerNames)
            {
                try
                {
                    _ = connection.Execute(PrinterSqlStore.InitializePrinter, new { printerName }, transaction, _commandTimeout);

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
            return connection.QueryFirstOrDefault<PrinterInfo>(
                PrinterSqlStore.GetSingleAvailablePrinterWithLock,
                new { printerNames },
                transaction,
                commandTimeout: _commandTimeout);
        }

        /// <summary>
        /// Получает информацию о конкретном принтере с блокировкой.
        /// </summary>
        private PrinterInfo GetPrinterInfoWithLock(OdbcConnection connection, OdbcTransaction transaction, string printerName)
        {
            string sql = PrinterSqlStore.GetAvailablePrintersWithLock + " AND printer_name = @printerName";

            return connection.QueryFirstOrDefault<PrinterInfo>(sql, new { printerName }, transaction, _commandTimeout);
        }

        /// <summary>
        /// Внутренний метод для резервирования принтера с optimistic locking.
        /// </summary>
        private bool ReservePrinterInternal(OdbcConnection connection, OdbcTransaction transaction, string printerName, string revitFileName, Guid expectedToken)
        {
            int processId = Process.GetCurrentProcess().Id;
            DateTime reservedAt = DateTime.UtcNow;

            int rowsAffected = connection.Execute(
                PrinterSqlStore.ReservePrinter,
                new { printerName, revitFileName, reservedAt, processId, expectedToken },
                transaction,
                _commandTimeout);

            bool success = rowsAffected > 0;

            if (!success)
            {
                _logger.Warning($"Failed to reserve printer {printerName} - optimistic lock conflict or printer unavailable");
            }

            return success;
        }

        /// <summary>
        /// Выполняет операцию с retry-логикой при конфликтах транзакций.
        /// </summary>
        private T ExecuteWithRetry<T>(Func<T> operation)
        {
            int delay = 1000;

            Exception lastException = null;

            for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
            {
                try
                {
                    return operation();
                }
                catch (OdbcException ex) when (IsRetryableException(ex) && attempt < _maxRetryAttempts)
                {
                    lastException = ex;

                    delay = _baseRetryDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff

                    _logger.Warning($"Retrying in {delay} ms: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Non-retryable exception on attempt {attempt}: {ex.Message}", ex);
                }
                finally
                {
                    _logger.Debug($"Attempt {attempt} completed");
                    System.Threading.Thread.Sleep(delay);
                }
            }

            _logger.Error($"Operation failed after {_maxRetryAttempts} attempts", lastException);
            throw new InvalidOperationException($"Operation failed after {_maxRetryAttempts} retry attempts", lastException);
        }

        /// <summary>
        /// Определяет, является ли исключение подходящим для повтора операции.
        /// </summary>
        private static bool IsRetryableException(OdbcException ex)
        {
            // PostgreSQL error codes для конфликтов сериализации и deadlock
            return ex.Message.Contains("serialization") ||
                   ex.Message.Contains("deadlock") ||
                   ex.Message.Contains("lock");
        }

        /// <summary>
        /// Создает подключение к базе данных.
        /// </summary>
        private OdbcConnection CreateConnection()
        {
            OdbcConnection connection = new(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Начинает транзакцию с уровнем изоляции SERIALIZABLE.
        /// Это обеспечивает максимальную консистентность для многопроцессорной работы.
        /// </summary>
        private static OdbcTransaction BeginSerializableTransaction(OdbcConnection connection)
        {
            return connection.BeginTransaction(IsolationLevel.Serializable);
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