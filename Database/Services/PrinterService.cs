using Dapper;
using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Data.Odbc;
using System.Threading;

namespace Database.Services
{
    public sealed class PrinterService(int lockTimeoutMinutes = 30) : IDisposable
    {
        private readonly int _lockTimeoutMinutes = lockTimeoutMinutes;
        private readonly ILogger _logger = LoggerFactory.CreateLogger<PrinterService>();
        private bool _disposed = false;

        public bool TryGetAvailablePrinter(string revitFileName, string printerName)
        {
            _logger.Debug($"Trying to reserve printer {printerName} for file {revitFileName}");

            string reservedPrinterName = TransactionHelper.RunInTransaction((connection, transaction) =>
            {
                InitializePrinter(connection, transaction, printerName);
                return GetSpecificPrinter(connection, transaction, printerName)?.PrinterName;
            });

            return !string.IsNullOrWhiteSpace(reservedPrinterName);
        }


        public bool TryReservePrinter(string printerName, string revitFilePath)
        {
            _logger.Debug($"Try reserve printer {printerName} for file {revitFilePath}");

            int affectedRows = TransactionHelper.Execute(PrinterSqlStore.ReservePrinter, new { printerName, revitFilePath });

            bool success = affectedRows > 0;

            return success;
        }


        public bool TryReleasePrinter(string printerName, string revitFileName = null)
        {
            _logger.Debug($"Try release printer {printerName} for file {revitFileName}");

            int affectedRows = TransactionHelper.Execute(PrinterSqlStore.ReleasePrinter, new { printerName, revitFileName });

            bool success = affectedRows > 0;

            return success;
        }


        public int CleanupExpiredReservations()
        {
            DateTime cutoffTime = DateTime.UtcNow.AddMinutes(-_lockTimeoutMinutes);

            int cleanedCount = TransactionHelper.Execute(PrinterSqlStore.CleanupExpiredReservations, new { cutoffTime });

            return cleanedCount;
        }


        private static void InitializePrinter(OdbcConnection connection, OdbcTransaction transaction, string printerName)
        {
            int commandTimeout = TransactionHelper.CommandTimeout;
            int maxAttempts = TransactionHelper.MaxRetryAttempts;
            bool success = false;
            int attempts = 0;

            while (!success)
            {
                try
                {
                    attempts++;
                    Thread.Sleep(commandTimeout * attempts);
                    string sql = PrinterSqlStore.InitializePrinter;
                    if (0 < connection.Execute(sql, new { printerName }, transaction, commandTimeout))
                    {
                        success = true;
                    }
                }
                finally
                {
                    if (attempts > maxAttempts)
                    {
                        success = true;
                    }
                }
            }
        }


        private static PrinterInfo GetSpecificPrinter(OdbcConnection connection, OdbcTransaction transaction, string printerName)
        {
            int commandTimeout = TransactionHelper.CommandTimeout;
            string sql = PrinterSqlStore.GetSpecificPrinterWithLock;
            return connection.QuerySingleOrDefault<PrinterInfo>(sql, new { printerName }, transaction, commandTimeout);
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