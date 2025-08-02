using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Data.Odbc;

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
            string localReservedPrinterName = null;

            _logger.Debug($"Starting reservation for {revitFileName}");

            (bool success, TimeSpan elapsed) = TransactionHelper.RunInTransaction((connection, transaction) =>
            {
                InitializePrinters(connection, transaction, availablePrinterNames);

                PrinterInfo selectedPrinter = GetAvailablePrinter(connection, transaction, availablePrinterNames);
                if (selectedPrinter == null)
                {
                    return false;
                }

                if (ReservePrinterInternal(connection, transaction, selectedPrinter, revitFileName))
                {
                    localReservedPrinterName = selectedPrinter.PrinterName;
                    return true;
                }
                return false;
            });

            LogOperationResult("Reserve any printer", localReservedPrinterName ?? "none", success, elapsed);

            reservedPrinterName = localReservedPrinterName;
            return success;
        }

        public bool TryReserveSpecificPrinter(string printerName, string revitFileName)
        {
            _logger.Debug($"Starting reservation of {printerName}");

            (bool success, TimeSpan elapsed) = TransactionHelper.RunInTransaction((connection, transaction) =>
            {
                PrinterInfo printerInfo = GetPrinterInfoWithLock(connection, transaction, printerName);

                return (printerInfo?.IsAvailable) == true && ReservePrinterInternal(connection, transaction, printerInfo, revitFileName);
            });

            LogOperationResult("Reserve specific printer", printerName, success, elapsed);

            return success;
        }

        public bool TryReleasePrinter(string printerName, string revitFileName)
        {
            (int affectedRows, TimeSpan elapsed) = TransactionHelper.Execute(PrinterSqlStore.ReleasePrinter, new { printerName, revitFileName });

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

        // Вспомогательные методы
        private void LogOperationResult(string operation, string printerName, bool success, TimeSpan elapsed)
        {
            string status = success ? "success" : "failed";
            _logger.Information($"{operation} {printerName}: {status} in {elapsed.TotalMilliseconds:F0}ms");
        }

        // Приватные методы для работы с принтерами остаются те же...
        private void InitializePrinters(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames) { /* ... */ }
        private PrinterInfo GetAvailablePrinter(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames) { /* ... */ }
        private PrinterInfo GetPrinterInfoWithLock(OdbcConnection connection, OdbcTransaction transaction, string printerName) { /* ... */ }
        private bool ReservePrinterInternal(OdbcConnection connection, OdbcTransaction transaction, PrinterInfo printerInfo, string revitFileName) { /* ... */ }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}