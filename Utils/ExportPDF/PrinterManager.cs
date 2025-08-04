using Database.Models;
using Database.Services;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF
{
    internal static class PrinterManager
    {
        private static readonly List<PrinterControl> printerControllers = GetPrinterControllers();

        public static bool TryGetPrinter(string revitFilePath, out PrinterControl availablePrinter)
        {
            availablePrinter = null;

            foreach (PrinterControl control in printerControllers)
            {
                if (!control.IsPrinterInstalled())
                {
                    continue;
                }

                try
                {
                    // Используем Thread-Safe Singleton с правильной сигнатурой
                    if (PrinterManagerSingleton.Instance.TryReservePrinter(
                        control.PrinterName,
                        revitFilePath,
                        out PrinterReservation reservation))
                    {
                        control.InitializePrinter(revitFilePath);
                        control.Reservation = reservation;

                        Log.Information($"Принтер зарезервирован: {control.PrinterName}");
                        availablePrinter = control;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка при резервировании принтера: {Message}", ex.Message);
                }
            }

            return false;
        }

        public static void ReleasePrinter(PrinterControl printer)
        {
            if (printer?.Reservation != null)
            {
                _ = PrinterManagerSingleton.Instance.ReleasePrinter(
                    printer.PrinterName,
                    printer.Reservation.SessionId,
                    true);

                printer.RestoreDefaultSettings();
                Log.Information("Принтер освобожден: {PrinterName}", printer.PrinterName);
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