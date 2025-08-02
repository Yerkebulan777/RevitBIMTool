using Dapper;
using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Data.Odbc;
using System.Text;
using System.Threading;

namespace Database.Services
{
    public sealed class PrinterService(int lockTimeoutMinutes = 30) : IDisposable
    {
        private readonly int _lockTimeoutMinutes = lockTimeoutMinutes;
        private readonly ILogger _logger = LoggerFactory.CreateLogger<PrinterService>();
        private bool _disposed = false;

        public bool TryReserveAvailablePrinter(string revitFileName, string[] availablePrinterNames, out string reservedPrinterName)
        {
            reservedPrinterName = null;

            _logger.Debug($"Starting reservation search for {revitFileName} among {availablePrinterNames.Length} printers");

            (PrinterInfo selectedPrinter, TimeSpan elapsed) = TransactionHelper.RunInTransaction((connection, transaction) =>
            {
                // Initialize all printers in a single batch operation
                InitializePrinters(connection, transaction, availablePrinterNames);

                // Get and lock the first available printer
                return GetAvailablePrinterWithLock(connection, transaction, availablePrinterNames);
            });

            if (selectedPrinter?.IsAvailable == true)
            {
                // Reserve the printer in a separate transaction to minimize lock time
                bool reserved = TryReserveSpecificPrinter(selectedPrinter.PrinterName, revitFileName);
                if (reserved)
                {
                    reservedPrinterName = selectedPrinter.PrinterName;
                    LogOperationResult("Reserve available printer", selectedPrinter.PrinterName, true, elapsed);
                    return true;
                }
            }

            LogOperationResult("Reserve available printer", "none found", false, elapsed);
            return false;
        }

        public bool TryReserveSpecificPrinter(string printerName, string revitFileName)
        {
            _logger.Debug($"Attempting to reserve specific printer: {printerName}");

            (bool success, TimeSpan elapsed) = TransactionHelper.RunInTransaction((connection, transaction) =>
            {
                // Initialize printer if it doesn't exist
                int initialized = connection.Execute(
                    PrinterSqlStore.InitializePrinter,
                    new { printerName },
                    transaction,
                    TransactionHelper.CommandTimeout);

                // Get printer info with row-level lock
                PrinterInfo printerInfo = GetPrinterWithLock(connection, transaction, printerName);

                if (printerInfo?.IsAvailable != true)
                {
                    return false;
                }

                // Perform atomic reservation with optimistic locking
                DateTime reservedAt = DateTime.UtcNow;
                int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

                int affectedRows = connection.Execute(
                    PrinterSqlStore.ReservePrinter,
                    new
                    {
                        printerName = printerInfo.PrinterName,
                        revitFileName,
                        reservedAt,
                        processId,
                        expectedToken = printerInfo.VersionToken
                    },
                    transaction,
                    TransactionHelper.CommandTimeout);

                bool reservationSuccess = affectedRows > 0;

                if (!reservationSuccess)
                {
                    _logger.Warning($"Failed to reserve {printerName} - concurrent access detected");
                }

                return reservationSuccess;
            });

            LogOperationResult("Reserve specific printer", printerName, success, elapsed);
            return success;
        }

        public bool TryReleasePrinter(string printerName, string revitFileName = null)
        {
            (int affectedRows, TimeSpan elapsed) = TransactionHelper.Execute(
                PrinterSqlStore.ReleasePrinter,
                new { printerName, revitFileName });

            bool success = affectedRows > 0;
            LogOperationResult("Release printer", printerName, success, elapsed);
            return success;
        }

        public int CleanupExpiredReservations()
        {
            DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-_lockTimeoutMinutes);

            (int cleanedCount, TimeSpan elapsed) = TransactionHelper.Execute(
                PrinterSqlStore.CleanupExpiredReservations,
                new { cutoffTime });

            _logger.Information($"Cleaned up {cleanedCount} expired reservations in {elapsed.TotalMilliseconds:F0}ms");
            return cleanedCount;
        }

        // Унифицированный метод получения принтера с блокировкой на уровне строки
        private PrinterInfo GetPrinterWithLock(OdbcConnection connection, OdbcTransaction transaction, string printerName)
        {
            return connection.QuerySingleOrDefault<PrinterInfo>(
                PrinterSqlStore.GetSpecificPrinterWithLock,
                new { printerName },
                transaction,
                TransactionHelper.CommandTimeout);
        }

        // Optimized method for getting first available printer from array
        private PrinterInfo GetAvailablePrinterWithLock(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames)
        {
            return connection.QuerySingleOrDefault<PrinterInfo>(
                PrinterSqlStore.GetSingleAvailablePrinterWithLock,
                new { printerNames },
                transaction,
                TransactionHelper.CommandTimeout);
        }



        // Пакетная инициализация для снижения накладных расходов на транзакции
        private void InitializePrinters(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames)
        {
            const int maxAttempts = 3;
            const int delayMs = 1000;

            foreach (string printerName in printerNames)
            {
                int attempts = 0;
                bool success = false;

                while (attempts < maxAttempts && !success)
                {
                    try
                    {
                        attempts++;
                        _ = connection.Execute(PrinterSqlStore.InitializePrinter, new { printerName }, transaction, TransactionHelper.CommandTimeout);
                        success = true;
                        _logger.Information($"Printer {printerName} initialized successfully on attempt {attempts}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"Attempt {attempts} failed for printer {printerName}: {ex.Message}");

                        if (attempts < maxAttempts)
                        {
                            Thread.Sleep(delayMs);
                        }
                        else
                        {
                            _logger.Error($"Failed to initialize printer {printerName} after {maxAttempts} attempts: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void LogOperationResult(string operation, string printerName, bool success, TimeSpan elapsed)
        {
            string status = success ? "SUCCESS" : "FAILED";
            _logger.Information($"{operation} '{printerName}': {status} in {elapsed.TotalMilliseconds:F0}ms");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}