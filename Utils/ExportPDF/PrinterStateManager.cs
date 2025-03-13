using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF;

internal static class PrinterStateManager
{
    private const string MutexName = "Global\\RevitPrinterManagerMutex";

    /// <summary>
    /// Попытка получить доступный принтер для печати
    /// </summary>
    public static bool TryRetrievePrinter(out PrinterControl availablePrinter, int maxRetries = 3)
    {
        availablePrinter = null;

        using (Mutex mutex = new Mutex(false, MutexName))
        {
            try
            {
                if (mutex.WaitOne(5000))
                {
                    try
                    {
                        // Перебираем все доступные принтеры
                        for (int retryCount = 0; retryCount < maxRetries; retryCount++)
                        {
                            if (retryCount > 0)
                            {
                                Log.Debug($"Попытка {retryCount + 1}/{maxRetries} поиска принтера...");
                                Thread.Sleep(2000);
                            }

                            foreach (PrinterControl printer in GetPrinters())
                            {
                                try
                                {
                                    if (printer.IsAvailable())
                                    {
                                        availablePrinter = printer;
                                        Log.Debug($"Найден доступный принтер: {printer.PrinterName}");
                                        return true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"Ошибка при проверке принтера {printer.PrinterName}: {ex.Message}");
                                }
                            }
                        }

                        Log.Error("Не найден доступный принтер после нескольких попыток.");
                    }
                    finally
                    {
                        // Освобождаем Mutex
                        mutex.ReleaseMutex();
                    }
                }
                else
                {
                    Log.Warning("Не удалось получить доступ к мьютексу принтера (таймаут)");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка при поиске принтера: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Получить список принтеров в порядке приоритета
    /// </summary>
    public static List<PrinterControl> GetPrinters()
    {
        return new List<PrinterControl>
        {
            new Pdf24Printer(),
            new CreatorPrinter(),
            new ClawPdfPrinter(),
            new InternalPrinter()
        };
    }
}