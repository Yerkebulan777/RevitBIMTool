using Database.Services;
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
        private static readonly string сonnectionString;
        private static readonly List<PrinterControl> printerControllers;
        private static readonly Lazy<PrinterService> printerServiceInstance;
        internal static string[] PrinterNames { get; set; }

        static PrinterStateManager()
        {
            lockTimeoutMin = int.TryParse(ConfigurationManager.AppSettings["PrinterLockTimeoutMinutes"], out int timeout) ? timeout : 5;
            сonnectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;
            printerServiceInstance = new Lazy<PrinterService>(CreatePrinterService, isThreadSafe: true);

            Log.Information("Initializing lock timeout {Timeout} minutes", lockTimeoutMin);

            printerControllers = GetPrinterControllers();

            if (string.IsNullOrEmpty(сonnectionString))
            {
                Log.Error("PrinterDatabase connection string is not configured");
            }
        }

        /// <summary>
        /// Создает экземпляр сервиса принтеров.
        /// </summary>
        private static PrinterService CreatePrinterService()
        {
            return new PrinterService(
                    сonnectionString,
                    commandTimeout: 30,
                    maxRetryAttempts: 3,
                    baseRetryDelayMs: 100,
                    lockTimeoutMinutes: lockTimeoutMin);
        }

        /// <summary>
        /// Thread-safe получение сервиса принтеров (singleton pattern).
        /// </summary>
        private static PrinterService GetPrinterService()
        {
            PrinterNames = [.. printerControllers.Where(p => p.IsPrinterInstalled()).Select(p => p.PrinterName)];
            Log.Information("Total printers found: {Count}", PrinterNames?.Length ?? 0);

            if (PrinterNames.Length == 0)
            {
                Log.Warning("No installed printerControllers");
            }

            // Value обеспечивает потокобезопасную инициализацию
            return printerServiceInstance.Value;
        }

        /// <summary>
        /// Пытается получить доступный принтер для использования.
        /// </summary>
        public static bool TryGetPrinter(string revitFilePath, out PrinterControl availablePrinter)
        {
            availablePrinter = null;

            try
            {
                PrinterService printerService = GetPrinterService();

                string reservedPrinterName = printerService.TryReserveAvailablePrinter(revitFilePath, PrinterNames);

                if (!string.IsNullOrEmpty(reservedPrinterName))
                {
                    availablePrinter = printerControllers.FirstOrDefault(p => string.Equals(p.PrinterName, reservedPrinterName));

                    if (availablePrinter is not null)
                    {
                        availablePrinter.RevitFilePath = revitFilePath;
                        string revitFileName = System.IO.Path.GetFileNameWithoutExtension(revitFilePath);
                        Log.Information("Reserved {PrinterName} for file {FileName}", reservedPrinterName, revitFileName);
                        return true;
                    }
                }

                Log.Warning("No available printerControllers to reserve for file {FileName}", System.IO.Path.GetFileName(revitFilePath));
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
        public static bool TryReservePrinter(string printerName)
        {
            try
            {
                PrinterService printerService = GetPrinterService();

                string dummyFilePath = $"Reservation_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (printerService.TryReserveSpecificPrinter(printerName, dummyFilePath))
                {
                    Log.Information("Successfully reserved printer {PrinterName} manually", printerName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reserving printer {PrinterName}: {Message}", printerName, ex.Message);
            }

            Log.Warning("Failed to reserve printer {PrinterName}", printerName);

            return false;
        }

        /// <summary>
        /// Освобождает принтер.
        /// </summary>
        public static void ReleasePrinter(string printerName)
        {
            try
            {
                PrinterService printerService = GetPrinterService();

                if (printerService.ReleasePrinter(printerName))
                {
                    Log.Information("Successfully released printer {PrinterName}", printerName);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to release printer {printerName}!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error releasing printer {PrinterName}: {Message}", printerName, ex.Message);
                throw new InvalidOperationException($"Error releasing printer {printerName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Очищает зависшие блокировки принтеров.
        /// </summary>
        public static int CleanupExpiredReservations()
        {
            try
            {
                PrinterService printerService = GetPrinterService();

                int cleanedCount = printerService.CleanupExpiredReservations();

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