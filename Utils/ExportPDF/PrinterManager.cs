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
            availablePrinter = null;

            StringBuilder logMessage = new();

            PrinterService printerService = GetPrinterServiceInstance();

            foreach (PrinterControl control in printerControllers)
            {
                availablePrinter = null;

                if (control.IsPrinterInstalled())
                {
                    try
                    {
                        if (printerService.TryGetAvailablePrinter(revitFilePath, control.PrinterName))
                        {
                            _ = logMessage.AppendLine($"Printer reserved: {control.PrinterName}");

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

            return false;
        }


        public static void ReleasePrinter(string printerName)
        {
            PrinterService printerService = GetPrinterServiceInstance();

            if (printerService.TryReleasePrinter(printerName))
            {
                Log.Information("Released {PrinterName}", printerName);
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