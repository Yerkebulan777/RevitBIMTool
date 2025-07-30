using Database;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.Configuration;

namespace RevitBIMTool.Utils.ExportPDF
{
    /// <summary>
    /// Упрощенный менеджер состояния принтеров.
    /// Использует только базу данных без XML fallback согласно требованиям.
    /// </summary>
    internal static class PrinterStateManager
    {
        private static PrinterService _printerService;
        private static readonly int _lockTimeoutMinutes;
        private static readonly string _connectionString;
        private static readonly object _lockObject = new();

        static PrinterStateManager()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;
            _lockTimeoutMinutes = int.TryParse(ConfigurationManager.AppSettings["PrinterLockTimeoutMinutes"], out int timeout) ? timeout : 10;

            if (string.IsNullOrEmpty(_connectionString))
            {
                Log.Error("PrinterDatabase connection string is not configured");
            }
        }

        /// <summary>
        /// Thread-safe получение сервиса принтеров (singleton pattern).
        /// </summary>
        private static PrinterService GetPrinterService()
        {
            if (_printerService is null)
            {
                lock (_lockObject)
                {
                    if (_printerService == null)
                    {
                        _printerService = new PrinterService(
                            _connectionString,
                            commandTimeout: 30,
                            maxRetryAttempts: 3,
                            baseRetryDelayMs: 100,
                            lockTimeoutMinutes: _lockTimeoutMinutes);

                        // Инициализируем известные принтеры
                        List<PrinterControl> availablePrinters = GetAvailablePrinterControls();
                        string[] printerNames = [.. availablePrinters.Select(p => p.PrinterName)];
                        _printerService.InitializePrinters(printerNames);

                        Log.Information("PrinterStateManager initialized with {Count} printers", printerNames.Length);
                    }
                }
            }
            return _printerService;
        }

        /// <summary>
        /// Пытается получить доступный принтер для использования.
        /// </summary>
        public static bool TryGetPrinter(string revitFilePath, out PrinterControl availablePrinter)
        {
            availablePrinter = null;

            try
            {
                List<PrinterControl> printerControls = GetAvailablePrinterControls();
                string[] preferredPrinters = printerControls
                    .Where(p => p.IsPrinterInstalled())
                    .Select(p => p.PrinterName)
                    .ToArray();

                if (preferredPrinters.Length == 0)
                {
                    Log.Warning("No installed printers found on this machine");
                    return false;
                }

                string reservedPrinterName = GetPrinterService()
                    .TryReserveAnyAvailablePrinter(revitFilePath, preferredPrinters);

                if (!string.IsNullOrEmpty(reservedPrinterName))
                {
                    availablePrinter = printerControls.FirstOrDefault(p =>
                        string.Equals(p.PrinterName, reservedPrinterName, StringComparison.OrdinalIgnoreCase));

                    if (availablePrinter != null)
                    {
                        availablePrinter.RevitFilePath = revitFilePath;
                        Log.Information("Reserved printer {PrinterName} for file {FileName}",
                            reservedPrinterName, System.IO.Path.GetFileName(revitFilePath));
                        return true;
                    }
                }

                Log.Warning("No available printers to reserve for file {FileName}",
                    System.IO.Path.GetFileName(revitFilePath));
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while trying to get printer: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Резервирует конкретный принтер.
        /// </summary>
        public static void ReservePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new ArgumentException("Printer name cannot be empty", nameof(printerName));
            }

            try
            {
                string dummyFilePath = $"Manual_Reservation_{Environment.UserName}_{DateTime.Now:yyyyMMdd_HHmmss}.rvt";
                bool success = GetPrinterService().TryReserveSpecificPrinter(printerName, dummyFilePath);

                if (success)
                {
                    Log.Information("Successfully reserved printer {PrinterName} manually", printerName);
                }
                else
                {
                    Log.Warning("Failed to reserve printer {PrinterName} (may be already in use)", printerName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reserving printer {PrinterName}: {Message}", printerName, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Освобождает принтер.
        /// </summary>
        public static void ReleasePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return;
            }

            try
            {
                bool success = GetPrinterService().ReleasePrinter(printerName);

                if (success)
                {
                    Log.Information("Successfully released printer {PrinterName}", printerName);
                }
                else
                {
                    Log.Debug("Printer {PrinterName} was not reserved or already released", printerName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error releasing printer {PrinterName}: {Message}", printerName, ex.Message);
            }
        }

        /// <summary>
        /// Очищает зависшие блокировки принтеров.
        /// </summary>
        public static int CleanupExpiredReservations()
        {
            try
            {
                int cleanedCount = GetPrinterService().CleanupExpiredReservations();

                if (cleanedCount > 0)
                {
                    Log.Information("Cleaned up {Count} expired printer reservations", cleanedCount);
                }

                return cleanedCount;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up expired reservations: {Message}", ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Получает список всех доступных контроллеров принтеров в порядке приоритета.
        /// </summary>
        private static List<PrinterControl> GetAvailablePrinterControls()
        {
            return
            [
                new Pdf24Printer(),
                new BioPdfPrinter(),
                new CreatorPrinter(),
                new ClawPdfPrinter(),
                new InternalPrinter(),
            ];
        }
    }
}