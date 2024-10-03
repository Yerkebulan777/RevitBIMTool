using Serilog;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Text;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitBIMTool.Utils.ExportPDF;
public static class PrinterApiUtility
{

    const int acсess = PrinterApiWrapper.PRINTER_ACCESS_ADMINISTER | PrinterApiWrapper.PRINTER_ACCESS_USE;


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


    public static bool GetPaperSize(double widthInMm, double heigthInMm, out PaperSize resultSize)
    {
        resultSize = null;
        bool result = false;

        StringBuilder strBuilder = new();
        PrinterSettings prntSettings = new();

        double widthInch = PrinterUnitConvert.Convert(widthInMm, PrinterUnit.TenthsOfAMillimeter, PrinterUnit.ThousandthsOfAnInch);
        double heightInch = PrinterUnitConvert.Convert(heigthInMm, PrinterUnit.TenthsOfAMillimeter, PrinterUnit.ThousandthsOfAnInch);

        int searсhMinSide = Convert.ToInt32(Math.Round(Math.Min(widthInch, heightInch)));
        int searchMaxSide = Convert.ToInt32(Math.Round(Math.Max(widthInch, heightInch)));

        double average = Math.Round((searсhMinSide + searchMaxSide) * 0.5);

        foreach (PaperSize currentSize in prntSettings.PaperSizes)
        {
            int currentWidth = currentSize.Width;
            int currentHeigth = currentSize.Height;
            string currentName = currentSize.PaperName;

            int currentMinSide = Math.Min(currentWidth, currentHeigth);
            int currentMaxSide = Math.Max(currentWidth, currentHeigth);

            if (currentMinSide < average && average < currentMaxSide)
            {
                _ = strBuilder.AppendLine($"Search format ({searсhMinSide} x {searchMaxSide})");
                _ = strBuilder.AppendLine($"{currentName} ({currentMinSide} x {currentMaxSide})");

                if (IsEquals(searсhMinSide, currentMinSide) && IsEquals(searchMaxSide, currentMaxSide))
                {
                    resultSize = currentSize;
                    _ = strBuilder.Clear();
                    result = true;
                    break;
                }
            }
        }

        Debug.Print(strBuilder.ToString());

        return result;
    }


    private static bool IsEquals(double val01, double val02, int tolerance = 5)
    {
        return Math.Abs(val01 - val02) < tolerance;
    }


    public static string AddFormat(string printerName, double widthInMm, double heightInMm)
    {
        double widthSideInMm = Math.Round(Math.Min(widthInMm, heightInMm), 5);
        double heighSideInMm = Math.Round(Math.Max(widthInMm, heightInMm), 5);

        string formName = $"Custom {widthSideInMm} x {heighSideInMm}";

        using (Mutex mutex = new(false, "Global\\{{{AddPrinterFormat}}}"))
        {
            int width = (int)(widthSideInMm * 1000.0);
            int height = (int)(heighSideInMm * 1000.0);

            if (mutex.WaitOne(Timeout.Infinite))
            {
                try
                {
                    PrinterApiWrapper.PrinterDefaults defaults = new()
                    {
                        pDatatype = null,
                        pDevMode = IntPtr.Zero,
                        DesiredAccess = acсess,
                    };

                    if (PrinterApiWrapper.OpenPrinter(printerName, out IntPtr hPrinter, ref defaults))
                    {
                        Log.Debug($"Deleted {formName}: {PrinterApiWrapper.DeleteForm(hPrinter, formName)}");

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
                            Log.Error($"Failed add printer form {formName}!");
                        }

                        if (PrinterApiWrapper.ClosePrinter(hPrinter))
                        {
                            Log.Debug($"Closed {formName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.ToString());
                }
                finally
                {
                    mutex.ReleaseMutex();
                    Thread.Sleep(1000);
                }
            }
        }

        return formName;
    }

}
