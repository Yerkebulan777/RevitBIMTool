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
    internal static class PrinterManager
    {
        private static readonly string сonnectionString;
        private static readonly List<PrinterControl> printerControllers;
        private static readonly Lazy<PrinterService> printerServiceInstance;
        internal static string[] PrinterNames { get; set; }

        static PrinterManager()
        {
            int maxRetries = GetConfigInt("PrinterReservationMaxRetries", 5);
            int commandTimeout = GetConfigInt("DatabaseCommandTimeout", 60);
            int lockTimeoutMin = GetConfigInt("PrinterLockTimeoutMinutes", 60);
            int retryDelay = GetConfigInt("PrinterReservationRetryDelayMs", 100);

            сonnectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            Log.Warning("Connection string: {ConnectionString}", сonnectionString);

            if (!string.IsNullOrEmpty(сonnectionString))
            {
                CleanupExpiredReservations();

                printerControllers = GetPrinterControllers();

                printerServiceInstance = new Lazy<PrinterService>(() =>
                    new PrinterService(
                        connectionString: сonnectionString,
                        commandTimeout: commandTimeout,
                        maxRetryAttempts: maxRetries,
                        baseRetryDelayMs: retryDelay,
                        lockTimeoutMinutes: lockTimeoutMin),
                        isThreadSafe: true);
            }
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

                if (printerService.TryReleasePrinter(printerName))
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

        /// <summary>
        /// Вспомогательный метод для безопасного чтения целочисленных настроек из конфига.
        /// </summary>
        /// <param name="key">Ключ настройки</param>
        /// <param name="defaultValue">Значение по умолчанию</param>
        /// <returns>Значение из конфига или значение по умолчанию</returns>
        private static int GetConfigInt(string key, int defaultValue)
        {
            if (int.TryParse(ConfigurationManager.AppSettings[key], out int value))
            {
                Log.Debug("Config setting {Key} = {Value}", key, value);
                return value;
            }

            Log.Debug("Config setting {Key} not found or invalid, using default: {Default}", key, defaultValue);
            return defaultValue;
        }


    }
}