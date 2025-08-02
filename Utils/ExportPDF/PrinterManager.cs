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


        public static bool TryGetPrinter(string revitFilePath, out PrinterControl reservedPrinter)
        {
            reservedPrinter = null;

            try
            {
                PrinterService printerService = GetPrinterServiceInstance();

                string[] printerNames = [.. printerControllers.Where(p => p.IsPrinterInstalled()).Select(p => p.PrinterName)];

                if (printerService.TryGetAvailablePrinter(revitFilePath, printerNames, out string reservedPrinterName))
                {
                    reservedPrinter = printerControllers.FirstOrDefault(p => string.Equals(p.PrinterName, reservedPrinterName));

                    if (reservedPrinter is not null)
                    {
                        StringBuilder logMessage = new();
                        reservedPrinter.RevitFilePath = revitFilePath;
                        _ = logMessage.AppendLine($"Printer reserved: {reservedPrinter.PrinterName}");
                        _ = logMessage.AppendLine($"Total printers: {printerNames?.Length ?? 0}");
                        Log.Information(logMessage.ToString());

                        return printerService.TryReservePrinter(reservedPrinterName, revitFilePath);
                    }
                }

                Log.Warning("No available printers found for file: {RevitFilePath}", revitFilePath);

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while trying to get printer: {Message}", ex.Message);
            }

            return false;
        }


        public static void ReleasePrinter(string printerName)
        {
            PrinterService printerService = GetPrinterServiceInstance();

            if (printerService.TryReleasePrinter(printerName))
            {
                Log.Information("Released {PrinterName}", printerName);

                if (0 < printerService.CleanupExpiredReservations())
                {
                    Log.Information("Cleaned up expired printer reservations");
                }
            }
            else
            {
                Log.Error("Failed to release printer {PrinterName}", printerName);
                throw new InvalidOperationException($"Failed to release printer {printerName}!");
            }
        }


        private static PrinterService InitializePrinterService()
        {
            return new PrinterService(lockTimeoutMinutes: 30);
        }

        private static PrinterService GetPrinterServiceInstance()
        {
            return printerServiceInstance.Value;
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