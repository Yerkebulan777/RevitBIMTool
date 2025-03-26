using Serilog;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Text;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitBIMTool.Utils.ExportPDF;
public static class PrinterApiUtility
{
    private const int acсess = PrinterApiWrapper.PRINTER_ACCESS_ADMINISTER | PrinterApiWrapper.PRINTER_ACCESS_USE;

    public static string GetDefaultPrinter()
    {
        PrinterSettings settings = new();
        string defaultPrinter = settings?.PrinterName;
        return defaultPrinter;
    }

    public static void ResetDefaultPrinter(string printerName)
    {
        PrintDocument printDocument = new();

        PrinterSettings settings = printDocument.PrinterSettings;

        if (!settings.PrinterName.Equals(printerName))
        {
            if (PrinterApiWrapper.SetDefaultPrinter(printerName))
            {
                printDocument.PrinterSettings.PrinterName = printerName;
            }

            printDocument.Dispose();
        }
    }

    public static bool GetPaperSize(double widthInMm, double heightInMm, out PaperSize resultSize, int threshold = 5)
    {
        resultSize = null;
        PrinterSettings prntSettings = new();

        PrinterUnit unitInMm = PrinterUnit.TenthsOfAMillimeter;
        PrinterUnit unitInInch = PrinterUnit.ThousandthsOfAnInch;

        double minSideInMm = Math.Round(Math.Min(widthInMm, heightInMm) / threshold) * threshold;
        double maxSideInMm = Math.Round(Math.Max(widthInMm, heightInMm) / threshold) * threshold;

        int toleranceInch = Convert.ToInt32(PrinterUnitConvert.Convert(threshold, unitInMm, unitInInch));
        int searchMinSide = Convert.ToInt32(PrinterUnitConvert.Convert(minSideInMm, unitInMm, unitInInch));
        int searchMaxSide = Convert.ToInt32(PrinterUnitConvert.Convert(maxSideInMm, unitInMm, unitInInch));

        string formatName = $"Custom {minSideInMm} x {maxSideInMm}";
        Log.Debug("Searching for paper format: {0}", formatName);

        foreach (PaperSize size in prntSettings.PaperSizes)
        {
            int currentMinSide = Math.Min(size.Width, size.Height);
            int currentMaxSide = Math.Max(size.Width, size.Height);

            int diffMinSide = Math.Abs(searchMinSide - currentMinSide);
            int diffMaxSide = Math.Abs(searchMaxSide - currentMaxSide);

            if (diffMinSide < toleranceInch && diffMaxSide < toleranceInch)
            {
                resultSize = size;
                return true;
            }
        }

        return false;
    }

    public static string AddFormat(string printerName, double widthInMm, double heightInMm, int threshold = 5)
    {
        double minSideInMm = Math.Round(Math.Min(widthInMm, heightInMm) / threshold) * threshold;
        double maxSideInMm = Math.Round(Math.Max(widthInMm, heightInMm) / threshold) * threshold;

        string formName = $"Custom {minSideInMm} x {maxSideInMm}";

        using (Mutex mutex = new(false, "Global\\{{{AddPrinterFormat}}}"))
        {
            Log.Information("Adding format: {0}", formName);

            IntPtr hPrinter = IntPtr.Zero;

            if (mutex.WaitOne())
            {
                try
                {
                    int width = (int)(minSideInMm * 1000.0);
                    int height = (int)(maxSideInMm * 1000.0);

                    PrinterApiWrapper.PrinterDefaults defaults = new()
                    {
                        pDatatype = null,
                        pDevMode = IntPtr.Zero,
                        DesiredAccess = acсess,
                    };

                    if (PrinterApiWrapper.OpenPrinter(printerName, out hPrinter, ref defaults))
                    {
                        bool deleted = PrinterApiWrapper.DeleteForm(hPrinter, formName);
                        Log.Debug("Previous format deleted: {0}", deleted);

                        PrinterApiWrapper.FormInfo1 formInfo = new()
                        {
                            Flags = 0,
                            pName = formName
                        };

                        formInfo.Size.width = width;
                        formInfo.Size.height = height;
                        formInfo.ImageableArea.top = 0;
                        formInfo.ImageableArea.left = 0;
                        formInfo.ImageableArea.right = width;
                        formInfo.ImageableArea.bottom = height;

                        if (!PrinterApiWrapper.AddForm(hPrinter, 1, ref formInfo))
                        {
                            int error = PrinterApiWrapper.GetLastError();
                            Log.Error("Error code: {1}", error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error adding format: {0}", ex.Message);
                }
                finally
                {
                    mutex.ReleaseMutex();

                    if (PrinterApiWrapper.ClosePrinter(hPrinter))
                    {
                        Log.Debug("Printer closed");
                        Thread.Sleep(500);
                    }
                }
            }
        }

        return formName;
    }


}
