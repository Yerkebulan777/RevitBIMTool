using Database;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.Configuration;

namespace RevitBIMTool.Utils.ExportPDF
{
    /// <summary>
    /// Безопасный менеджер состояния принтеров для Revit API
    /// </summary>
    internal static class SafePrinterStateManager
    {
        private static readonly object _lockObject = new object();
        private static SafePostgreSqlPrinterService _printerService;
        private static readonly string _connectionString;
        private static readonly int _lockTimeoutMinutes;

        static SafePrinterStateManager()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["PrinterDatabase"]?.ConnectionString;

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("PrinterDatabase connection string is not configured");
            }

            _lockTimeoutMinutes = int.TryParse(ConfigurationManager.AppSettings["PrinterLockTimeoutMinutes"], out int timeout) ? timeout : 10;
        }

        /// <summary>
        /// Получает сервис принтеров (thread-safe singleton)
        /// </summary>
        private static SafePostgreSqlPrinterService GetPrinterService()
        {
            if (_printerService == null)
            {
                lock (_lockObject)
                {
                    if (_printerService == null)
                    {
                        _printerService = new SafePostgreSqlPrinterService(_connectionString);

                        // Инициализируем известные принтеры
                        List<PrinterControl> availablePrinters = GetAvailablePrinterControls();
                        string[] printerNames = availablePrinters.Select(p => p.PrinterName).ToArray();
                        _printerService.InitializePrinters(printerNames);

                        Log.Information("Initialized {Count} printers in database", printerNames.Length);
                    }
                }
            }
            return _printerService;
        }

        /// <summary>
        /// Пытается получить доступный принтер для использования
        /// </summary>
        public static bool TryGetPrinter(string revitFilePath, out PrinterControl availablePrinter)
        {
            availablePrinter = null;

            try
            {
                // Очищаем зависшие блокировки
                CleanupExpiredReservations();

                List<PrinterControl> printerControls = GetAvailablePrinterControls();
                string[] preferredPrinters = printerControls
                    .Where(p => p.IsPrinterInstalled())
                    .Select(p => p.PrinterName)
                    .ToArray();

                if (preferredPrinters.Length == 0)
                {
                    Log.Warning("No installed printers found");
                    return false;
                }

                string userName = Environment.UserName;
                string reservedPrinterName = GetPrinterService().TryReserveAnyAvailablePrinter(userName, preferredPrinters);

                if (!string.IsNullOrEmpty(reservedPrinterName))
                {
                    availablePrinter = printerControls.FirstOrDefault(p =>
                        string.Equals(p.PrinterName, reservedPrinterName, StringComparison.OrdinalIgnoreCase));

                    if (availablePrinter != null)
                    {
                        availablePrinter.RevitFilePath = revitFilePath;
                        Log.Information("Reserved printer: {PrinterName} for user: {UserName}",
                            reservedPrinterName, userName);
                        return true;
                    }
                }

                Log.Warning("No available printers to reserve");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while trying to get printer: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Резервирует конкретный принтер
        /// </summary>
        public static void ReservePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("PrinterName cannot be null or empty", nameof(printerName));

            try
            {
                string userName = Environment.UserName;
                bool success = GetPrinterService().TryReserveSpecificPrinter(printerName, userName);

                if (success)
                {
                    Log.Information("Successfully reserved printer: {PrinterName} for user: {UserName}",
                        printerName, userName);
                }
                else
                {
                    Log.Warning("Failed to reserve printer: {PrinterName}", printerName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reserving printer {PrinterName}: {Message}", printerName, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Освобождает принтер
        /// </summary>
        public static void ReleasePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return;

            try
            {
                string userName = Environment.UserName;
                bool success = GetPrinterService().ReleasePrinter(printerName, userName);

                if (success)
                {
                    Log.Information("Successfully released printer: {PrinterName} by user: {UserName}",
                        printerName, userName);
                }
                else
                {
                    Log.Warning("Failed to release printer: {PrinterName} (may not be reserved by current user)",
                        printerName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error releasing printer {PrinterName}: {Message}", printerName, ex.Message);
            }
        }

        /// <summary>
        /// Очищает зависшие блокировки
        /// </summary>
        public static int CleanupExpiredReservations()
        {
            try
            {
                TimeSpan maxAge = TimeSpan.FromMinutes(_lockTimeoutMinutes);
                int cleanedCount = GetPrinterService().CleanupExpiredReservations(maxAge);

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
        /// Получает список всех доступных контроллеров принтеров
        /// </summary>
        private static List<PrinterControl> GetAvailablePrinterControls()
        {
            return new List<PrinterControl>
            {
                new BioPdfPrinter(),
                new Pdf24Printer(),
                new CreatorPrinter(),
                new ClawPdfPrinter(),
                new AdobePdfPrinter(),
                new InternalPrinter()
            };
        }
    }
}