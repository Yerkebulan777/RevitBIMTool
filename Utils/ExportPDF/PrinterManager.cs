using Database.Services;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.Text;

namespace RevitBIMTool.Utils.ExportPDF
{
    internal static class PrinterManager
    {
        private static readonly List<PrinterControl> printerControllers = GetPrinterControllers();
        private static readonly Lazy<PrinterService> printerServiceInstance = new(InitializePrinterService, true);

        private static PrinterService InitializePrinterService()
        {
            return new PrinterService(lockTimeoutMinutes: 30);
        }

        private static PrinterService GetPrinterService()
        {
            return printerServiceInstance.Value;
        }


        public static bool TryGetPrinter(string revitFilePath, out PrinterControl reservedPrinter)
        {
            reservedPrinter = null;

            try
            {
                PrinterService printerService = GetPrinterService();

                string[] printerNames = [.. printerControllers.Where(p => p.IsPrinterInstalled()).Select(p => p.PrinterName)];

                if (printerService.TryReserveAvailablePrinter(revitFilePath, printerNames, out string reservedPrinterName))
                {
                    reservedPrinter = printerControllers.FirstOrDefault(p => string.Equals(p.PrinterName, reservedPrinterName));

                    if (reservedPrinter is not null)
                    {
                        StringBuilder logMessage = new();
                        reservedPrinter.RevitFilePath = revitFilePath;
                        logMessage.AppendLine($"Printer reserved: {reservedPrinter.PrinterName}");
                        logMessage.AppendLine($"Total printers: {printerNames?.Length ?? 0}");
                        Log.Information(logMessage.ToString());
                        return true;
                    }
                }

                Log.Warning("No printers available!!!");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while trying to get printer: {Message}", ex.Message);
            }

            return false;
        }


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


        public static void ReleasePrinter(string printerName)
        {
            PrinterService printerService = GetPrinterService();

            // Исправлено: убран второй параметр
            if (printerService.TryReleasePrinter(printerName))
            {
                Log.Information("Successfully released printer {PrinterName}", printerName);
            }
            else
            {
                throw new InvalidOperationException($"Failed to release printer {printerName}!");
            }
        }


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