using Dapper;
using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Data.Odbc;

namespace Database.Services
{
    public sealed class PrinterService : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _commandTimeout;
        private readonly int _lockTimeoutMinutes;
        private readonly ILogger _logger;
        private bool _disposed = false;

        public PrinterService(string connectionString, int commandTimeout = 60, int lockTimeoutMinutes = 30)
        {
            _connectionString = connectionString;
            _commandTimeout = commandTimeout;
            _lockTimeoutMinutes = lockTimeoutMinutes;
            _logger = LoggerFactory.CreateLogger<PrinterService>();
        }

        public bool TryReserveAvailablePrinter(string revitFileName, string[] availablePrinterNames, out string reservedPrinterName)
        {
            reservedPrinterName = null;
            string localReservedPrinterName = null;

            _logger.Debug($"Starting reservation for {revitFileName}");

            var (success, elapsed) = DatabaseTransactionHelper.ExecuteInTransaction(_connectionString, (connection, transaction) =>
            {
                InitializePrinters(connection, transaction, availablePrinterNames);

                PrinterInfo selectedPrinter = GetAvailablePrinter(connection, transaction, availablePrinterNames);
                if (selectedPrinter == null) return false;

                if (ReservePrinterInternal(connection, transaction, selectedPrinter, revitFileName))
                {
                    localReservedPrinterName = selectedPrinter.PrinterName;
                    return true;
                }
                return false;
            });

            if (success)
            {
                _logger.Information($"Reserved printer {localReservedPrinterName} in {elapsed.TotalMilliseconds:F0}ms");
            }
            else
            {
                _logger.Warning($"Failed to reserve printer in {elapsed.TotalMilliseconds:F0}ms");
            }

            reservedPrinterName = localReservedPrinterName;
            return success;
        }

        public bool TryReserveSpecificPrinter(string printerName, string revitFileName)
        {
            _logger.Debug($"Starting reservation of {printerName}");

            var (success, elapsed) = DatabaseTransactionHelper.ExecuteInTransaction(_connectionString, (connection, transaction) =>
            {
                PrinterInfo printerInfo = GetPrinterInfoWithLock(connection, transaction, printerName);

                if (printerInfo?.IsAvailable != true) return false;

                return ReservePrinterInternal(connection, transaction, printerInfo, revitFileName);
            });

            _logger.Information($"Printer {printerName} reservation: {(success ? "success" : "failed")} in {elapsed.TotalMilliseconds:F0}ms");

            return success;
        }

        public bool TryReleasePrinter(string printerName, string revitFileName = null)
        {
            if (string.IsNullOrEmpty(printerName)) return false;

            var (success, elapsed) = DatabaseTransactionHelper.ExecuteInTransaction(_connectionString, (connection, transaction) =>
            {
                var parameters = new { printerName, revitFileName };
                int rowsAffected = connection.Execute(PrinterSqlStore.ReleasePrinter, parameters, transaction, _commandTimeout);
                return rowsAffected > 0;
            });

            _logger.Information($"Release printer {printerName}: {(success ? "success" : "failed")} in {elapsed.TotalMilliseconds:F0}ms");

            return success;
        }

        public int CleanupExpiredReservations()
        {
            DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-_lockTimeoutMinutes);

            var (cleanedCount, elapsed) = DatabaseTransactionHelper.ExecuteInTransaction(_connectionString, (connection, transaction) =>
            {
                return connection.Execute(PrinterSqlStore.CleanupExpiredReservations,
                    new { cutoffTime }, transaction, _commandTimeout);
            });

            _logger.Information($"Cleaned up {cleanedCount} expired reservations in {elapsed.TotalMilliseconds:F0}ms");

            return cleanedCount;
        }

        // Приватные методы остаются без изменений...

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}