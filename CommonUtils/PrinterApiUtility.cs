using System.Diagnostics;
using System.Drawing.Printing;

namespace CommonUtils
{
    public static class PrinterApiUtility
    {
        /// <summary>
        /// Сбрасывает принтер по умолчанию на указанный принтер
        /// </summary>
        public static void ResetDefaultPrinter(string printerName)
        {
            PrintDocument printDocument = new();

            if (!printDocument.PrinterSettings.PrinterName.Equals(printerName))
            {
                if (PrinterApiWrapper.SetDefaultPrinter(printerName))
                {
                    printDocument.PrinterSettings.PrinterName = printerName;
                }

                printDocument.Dispose();
            }
        }

        /// <summary>
        /// Получает существующий формат бумаги или создает новый при необходимости
        /// </summary>
        public static bool GetOrCreatePaperSize(string printerName, double widthInMm, double heightInMm, out PaperSize paperSize, int threshold = 5)
        {
            (double minSide, double maxSide) = NormalizeDimensions(widthInMm, heightInMm, threshold);

            paperSize = FindMatchingPaperSize(minSide, maxSide, threshold);

            if (paperSize != null)
            {
                return true;
            }

            string formatName = $"Custom {minSide} x {maxSide}";

            if (CreatePaperFormat(printerName, formatName, minSide, maxSide))
            {
                paperSize = FindMatchingPaperSize(minSide, maxSide, threshold);
                Debug.WriteLine("Added format: {0}", formatName);
                return paperSize != null;
            }

            return false;
        }

        /// <summary>
        /// Нормализует размеры бумаги, округляя
        /// </summary>
        private static (double minSide, double maxSide) NormalizeDimensions(double width, double height, int threshold = 5)
        {
            double minSide = Math.Round(Math.Min(width, height) / threshold) * threshold;
            double maxSide = Math.Round(Math.Max(width, height) / threshold) * threshold;

            return (minSide, maxSide);
        }

        /// <summary>
        /// Ищет существующий формат бумаги, соответствующий заданным размерам
        /// </summary>
        private static PaperSize FindMatchingPaperSize(double minSideInMm, double maxSideInMm, int threshold = 5)
        {
            PrinterSettings prntSettings = new();

            PrinterUnit unitInMm = PrinterUnit.TenthsOfAMillimeter;
            PrinterUnit unitInInch = PrinterUnit.ThousandthsOfAnInch;

            int toleranceInch = Convert.ToInt32(PrinterUnitConvert.Convert(threshold, unitInMm, unitInInch));
            int searchMinSide = Convert.ToInt32(PrinterUnitConvert.Convert(minSideInMm, unitInMm, unitInInch));
            int searchMaxSide = Convert.ToInt32(PrinterUnitConvert.Convert(maxSideInMm, unitInMm, unitInInch));

            Debug.WriteLine("Searching for paper size: min={0}mm, max={1}mm", minSideInMm, maxSideInMm);

            foreach (PaperSize size in prntSettings.PaperSizes)
            {
                int currentMinSide = Math.Min(size.Width, size.Height);
                int currentMaxSide = Math.Max(size.Width, size.Height);

                int diffMinSide = Math.Abs(searchMinSide - currentMinSide);
                int diffMaxSide = Math.Abs(searchMaxSide - currentMaxSide);

                if (diffMinSide < toleranceInch && diffMaxSide < toleranceInch)
                {
                    Debug.WriteLine("Found matching paper size: {0}", size.PaperName);
                    return size;
                }
            }

            return null;
        }

        /// <summary>
        /// Создает новый формат бумаги для принтера
        /// </summary>
        private static bool CreatePaperFormat(string printerName, string formatName, double minSideInMm, double maxSideInMm)
        {
            bool success = false;
            IntPtr hPrinter = IntPtr.Zero;

            using (Mutex mutex = new(false, "Global\\{{{AddPrinterFormat}}}"))
            {
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
                            DesiredAccess = PrinterApiWrapper.PRINTER_ACCESS_ADMINISTER | PrinterApiWrapper.PRINTER_ACCESS_USE,
                        };

                        if (PrinterApiWrapper.OpenPrinter(printerName, out hPrinter, ref defaults))
                        {
                            // Форматирование имени происходит только здесь при создании формата
                            bool deleted = PrinterApiWrapper.DeleteForm(hPrinter, formatName);
                            Debug.WriteLine("Previous format deleted: {0}", deleted);

                            PrinterApiWrapper.FormInfo1 formInfo = new()
                            {
                                Flags = 0,
                                pName = formatName
                            };

                            formInfo.Size.width = width;
                            formInfo.Size.height = height;
                            formInfo.ImageableArea.top = 0;
                            formInfo.ImageableArea.left = 0;
                            formInfo.ImageableArea.right = width;
                            formInfo.ImageableArea.bottom = height;

                            success = PrinterApiWrapper.AddForm(hPrinter, 1, ref formInfo);

                            if (!success)
                            {
                                int error = PrinterApiWrapper.GetLastError();
                                Debug.Fail($"Error code: {error}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Fail($"Error adding format: {ex.Message}");
                    }
                    finally
                    {
                        mutex.ReleaseMutex();

                        if (hPrinter != IntPtr.Zero && PrinterApiWrapper.ClosePrinter(hPrinter))
                        {
                            Debug.WriteLine("Printer closed");
                            Thread.Sleep(500);
                        }
                    }
                }
            }

            return success;
        }


    }
}
