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


        public static bool TryGetPrinter(string revitFilePath, out PrinterControl availablePrinter)
        {
            StringBuilder logMessage = new();

            PrinterService printerService = GetPrinterServiceInstance();

            string[] printerNames = [.. printerControllers.Where(p => p.IsPrinterInstalled()).Select(p => p.PrinterName)];

            foreach (PrinterControl control in printerControllers)
            {
                if (control.IsPrinterInstalled())
                {
                    try
                    {
                        availablePrinter = null;

                        if (printerService.TryGetAvailablePrinter(revitFilePath, control.PrinterName))
                        {
                            _ = logMessage.AppendLine($"Printer reserved: {control.PrinterName}");
                            _ = logMessage.AppendLine($"Total printers: {printerNames?.Length ?? 0}");

                            if (printerService.TryReservePrinter(control.PrinterName, revitFilePath))
                            {
                                control.InitializePrinter(revitFilePath);
                                Log.Information(logMessage.ToString());
                                availablePrinter = control;
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while trying to get available printer: {Message}", ex.Message);
                    }
                }
            }

            Log.Warning("No available printers found for file: {RevitFilePath}", revitFilePath);
            availablePrinter = null;
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
                    Log.Information("Cleaned up expired availablePrinter reservations");
                }
            }
            else
            {
                Log.Error("Failed to release availablePrinter {PrinterName}", printerName);
                throw new InvalidOperationException($"Failed to release availablePrinter {printerName}!");
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