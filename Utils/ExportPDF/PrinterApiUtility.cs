using System.Diagnostics;
using System.Drawing.Printing;
using System.Text;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitBIMTool.Utils.ExportPDF;
public static class PrinterApiUtility
{
    public static string GetDefaultPrinter()
    {
        PrinterSettings settings = new PrinterSettings();
        string defaultPrinter = settings?.PrinterName;
        return defaultPrinter;
    }


    public static void ResetDefaultPrinter(string printerName)
    {
        PrintDocument printDocument = new PrintDocument();

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

        StringBuilder strBuilder = new StringBuilder();
        PrinterSettings prntSettings = new PrinterSettings();

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
        // форматы надо всегда добавлять в вертикальной ориентации
        double widthSideInMm = Math.Min(widthInMm, heightInMm);
        double heighSideInMm = Math.Max(widthInMm, heightInMm);

        string formName = $"Custom {Math.Round(widthSideInMm)} x {Math.Round(heighSideInMm)}";

        PrinterApiWrapper.PrinterDefaults defaults = new PrinterApiWrapper.PrinterDefaults
        {
            pDatatype = null,
            pDevMode = IntPtr.Zero,
            DesiredAccess = PrinterApiWrapper.PRINTER_ACCESS_ADMINISTER | PrinterApiWrapper.PRINTER_ACCESS_USE
        };

        if (PrinterApiWrapper.OpenPrinter(printerName, out IntPtr hPrinter, ref defaults))
        {
            try
            {
                if (PrinterApiWrapper.DeleteForm(hPrinter, formName))
                {
                    Debug.WriteLine("Deleted: " + formName);
                }

                PrinterApiWrapper.FormInfo1 formInfo = new PrinterApiWrapper.FormInfo1
                {
                    Flags = 0,
                    pName = formName
                };

                // для перевода в сантиметры умножить на 10000
                formInfo.Size.width = (int)(widthSideInMm * 1000.0);
                formInfo.Size.height = (int)(heighSideInMm * 1000.0);

                formInfo.ImageableArea.left = 0;
                formInfo.ImageableArea.right = formInfo.Size.width;
                formInfo.ImageableArea.top = 0;
                formInfo.ImageableArea.bottom = formInfo.Size.height;

                if (!PrinterApiWrapper.AddForm(hPrinter, 1, ref formInfo))
                {
                    throw new Exception($"Failed currentSize {formName} to the printer {printerName}");
                }
            }
            finally
            {
                if (PrinterApiWrapper.ClosePrinter(hPrinter))
                {
                    Thread.Sleep(1000);
                }
            }
        }
        else
        {
            throw new Exception($"Failed to open  {printerName} printer");
        }

        return formName;
    }


}
