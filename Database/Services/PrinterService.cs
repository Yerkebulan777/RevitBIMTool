using Dapper;
using Database.Logging;
using Database.Models;
using Database.Stores;
using System;
using System.Data.Odbc;
using System.Threading;
using static Dapper.SqlMapper;

namespace Database.Services
{
    public sealed class PrinterService(int lockTimeoutMinutes = 30) : IDisposable
    {
        private readonly int _lockTimeoutMinutes = lockTimeoutMinutes;
        private readonly ILogger _logger = LoggerFactory.CreateLogger<PrinterService>();
        private bool _disposed = false;

        public bool TryGetAvailablePrinter(string revitFileName, string[] availablePrinterNames, out string reservedPrinterName)
        {
            _logger.Debug($"Starting reservation search for {revitFileName} among {availablePrinterNames.Length} printers");

            reservedPrinterName = TransactionHelper.RunInTransaction((connection, transaction) =>
            {
                InitializePrinters(connection, transaction, availablePrinterNames);

                PrinterInfo selectedPrinter = GetAvailablePrinter(connection, transaction, availablePrinterNames);

                PrinterInfo printerInfo = GetSpecificPrinter(connection, transaction, selectedPrinter.PrinterName);

                return printerInfo?.PrinterName;
            });

            return !string.IsNullOrEmpty(reservedPrinterName);
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


        // Унифицированный метод получения принтера с блокировкой на уровне строки
        private static PrinterInfo GetSpecificPrinter(OdbcConnection connection, OdbcTransaction transaction, string printerName)
        {
            int commandTimeout = TransactionHelper.CommandTimeout;
            string sql = PrinterSqlStore.GetSpecificPrinterWithLock;
            return connection.QuerySingleOrDefault<PrinterInfo>(sql, new { printerName }, transaction, commandTimeout);
        }


        // Оптимизированный метод получения первого доступного принтера из массива
        private static PrinterInfo GetAvailablePrinter(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames)
        {
            int commandTimeout = TransactionHelper.CommandTimeout;
            string sql = PrinterSqlStore.GetAvailablePrinterWithLock;
            return connection.QuerySingleOrDefault<PrinterInfo>(sql, new { printerNames }, transaction, commandTimeout);
        }


        // Инициализация для снижения накладных расходов на транзакции
        private void InitializePrinters(OdbcConnection connection, OdbcTransaction transaction, string[] printerNames)
        {
            foreach (string printerName in printerNames)
            {
                int commandTimeout = TransactionHelper.CommandTimeout;
                int maxAttempts = TransactionHelper.MaxRetryAttempts;
                int attempts = 0;

                while (true)
                {
                    try
                    {
                        attempts++;
                        Thread.Sleep(commandTimeout * attempts);
                        string sql = PrinterSqlStore.InitializePrinter;
                        if (0 < connection.Execute(sql, new { printerName }, transaction, commandTimeout))
                        {
                            _logger.Information($"Printer {printerName} initialized successfully");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (attempts > maxAttempts)
                        {
                            _logger.Error($"Failed to initialize printer {printerName}: {ex.Message}");
                            break;
                        }
                    }
                }
            }
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