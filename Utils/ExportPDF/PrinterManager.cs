using Database.Services;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF
{
    /// <summary>
    /// Упрощенный менеджер состояния принтеров.
    /// </summary>
    internal static class PrinterManager
    {
        private static readonly List<PrinterControl> printerControllers = GetPrinterControllers();
        private static readonly Lazy<PrinterService> printerServiceInstance = new(InitializePrinterService, true);

        /// <summary>
        /// Инициализирует сервис принтеров.
        /// </summary>
        private static PrinterService InitializePrinterService()
        {
            return new PrinterService(lockTimeoutMinutes: 30);
        }

        /// <summary>
        /// Thread-safe получение сервиса принтеров (singleton pattern).
        /// </summary>
        private static PrinterService GetPrinterService()
        {
            // Обеспечивает потокобезопасную инициализацию
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

                string[] printerNames = [.. printerControllers.Where(p => p.IsPrinterInstalled()).Select(p => p.PrinterName)];

                if (printerService.TryReserveAvailablePrinter(revitFilePath, printerNames, out string reservedPrinterName))
                {
                    availablePrinter = printerControllers.FirstOrDefault(p => string.Equals(p.PrinterName, reservedPrinterName));

                    Log.Information("Total printers: {Count}", printerNames?.Length ?? 0);

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
        public static bool TryReservePrinter(string printerName, string revitFilePath)
        {
            PrinterService printerService = GetPrinterService();

            if (printerService.TryReserveSpecificPrinter(printerName, revitFilePath))
            {
                Log.Information("Reserved printer {PrinterName}", printerName);
                return true;
            }

            Log.Warning("Failed to reserve printer {PrinterName}", printerName);

            return false;
        }

        /// <summary>
        /// Освобождает принтер.
        /// </summary>
        public static void ReleasePrinter(string printerName)
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

        /// <summary>
        /// Очищает зависшие блокировки принтеров.
        /// </summary>
        public static void CleanupExpiredReservations()
        {
            try
            {
                PrinterService printerService = GetPrinterService();

                int cleanedCount = printerService.CleanupExpiredReservations();

                if (cleanedCount > 0)
                {
                    Log.Information("Cleaned up {Count} expired printer reservations", cleanedCount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up expired reservations: {Message}", ex.Message);
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