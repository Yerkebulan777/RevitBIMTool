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
        private static readonly int lockTimeoutMin;
        private static PrinterService printerService;
        private static readonly string сonnectionString;
        private static readonly object lockObject = new();

        private static string[] PrinterNames { get; set; }
        private static List<PrinterControl> printers { get; set; }

        static PrinterStateManager()
        {
            printers = GetPrinterControllers();
            lockTimeoutMin = int.TryParse(ConfigurationManager.AppSettings["PrinterLockTimeoutMinutes"], out int timeout) ? timeout : 5;
            сonnectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            Log.Information("Initializing lock timeout {Timeout} minutes", lockTimeoutMin);

            if (string.IsNullOrEmpty(сonnectionString))
            {
                Log.Error("PrinterDatabase connection string is not configured");
            }
        }

        /// <summary>
        /// Thread-safe получение сервиса принтеров (singleton pattern).
        /// </summary>
        private static PrinterService GetPrinterService()
        {
            if (printerService is null)
            {
                lock (lockObject)
                {
                    printerService = new PrinterService(
                                        сonnectionString,
                                        commandTimeout: 30,
                                        maxRetryAttempts: 3,
                                        baseRetryDelayMs: 100,
                                        lockTimeoutMinutes: lockTimeoutMin);

                    PrinterNames = [.. printers.Where(p => p.IsPrinterInstalled()).Select(p => p.PrinterName)];
                    Log.Information("PrinterStateManager initialized with {Count} printers", PrinterNames?.Length);

                    if (PrinterNames?.Length == 0)
                    {
                        Log.Warning("No installed printers");
                    }
                }
            }

            return printerService;
        }

        /// <summary>
        /// Пытается получить доступный принтер для использования.
        /// </summary>
        public static bool TryGetPrinter(string revitFilePath, out PrinterControl availablePrinter)
        {
            availablePrinter = null;

            try
            {
                printerService = GetPrinterService();

                string reservedPrinterName = printerService.TryReserveAnyAvailablePrinter(revitFilePath, PrinterNames);

                if (!string.IsNullOrEmpty(reservedPrinterName))
                {
                    availablePrinter = printers.FirstOrDefault(p => string.Equals(p.PrinterName, reservedPrinterName, StringComparison.OrdinalIgnoreCase));

                    if (availablePrinter is not null)
                    {
                        availablePrinter.RevitFilePath = revitFilePath;
                        string revitFileName = System.IO.Path.GetFileNameWithoutExtension(revitFilePath);
                        Log.Information("Reserved {PrinterName} for file {FileName}", reservedPrinterName, revitFileName);
                        return true;
                    }
                }

                Log.Warning("No available printers to reserve for file {FileName}", System.IO.Path.GetFileName(revitFilePath));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while trying to get printer: {Message}", ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Резервирует конкретный принтер.
        /// </summary>
        public static void ReservePrinter(string printerName)
        {
            try
            {
                printerService = GetPrinterService();

                string dummyFilePath = $"Reservation_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (printerService.TryReserveSpecificPrinter(printerName, dummyFilePath))
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
        private static List<PrinterControl> GetPrinterControllers()
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