using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF
{
    internal static class PrinterMemoryManager
    {
        private static readonly object _syncLock = new object();
        private const string SettingsFileName = "revit-printers.txt";
        private static readonly string _settingsFilePath;

        // Инициализация пути к файлу настроек
        static PrinterMemoryManager()
        {
            string tempPath = Path.GetTempPath();
            _settingsFilePath = Path.Combine(tempPath, SettingsFileName);
        }

        /// <summary>
        /// Попытка получить доступный принтер для печати
        /// </summary>
        /// <param name="availablePrinter">Найденный принтер</param>
        /// <param name="maxRetries">Максимальное количество попыток</param>
        /// <returns>True, если принтер найден</returns>
        public static bool TryRetrievePrinter(out PrinterControl availablePrinter, int maxRetries = 3)
        {
            availablePrinter = null;

            // Попытка использовать последний успешный принтер
            string lastUsedPrinter = GetLastSuccessfulPrinter();
            if (!string.IsNullOrEmpty(lastUsedPrinter))
            {
                Log.Debug($"Последний успешно использованный принтер: {lastUsedPrinter}");
                foreach (PrinterControl printer in GetPrinters())
                {
                    if (printer.PrinterName == lastUsedPrinter)
                    {
                        try
                        {
                            if (printer.IsAvailable())
                            {
                                availablePrinter = printer;
                                Log.Debug($"Используется последний успешный принтер: {printer.PrinterName}");
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Ошибка при проверке последнего успешного принтера {printer.PrinterName}: {ex.Message}");
                        }
                    }
                }
            }

            // Если последний принтер недоступен, перебираем все доступные принтеры
            for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            {
                if (retryCount > 0)
                {
                    Log.Debug($"Попытка {retryCount + 1}/{maxRetries} поиска принтера...");
                    Thread.Sleep(2000); // Разумная пауза между попытками
                }

                foreach (PrinterControl printer in GetPrinters())
                {
                    try
                    {
                        if (printer.IsAvailable())
                        {
                            availablePrinter = printer;
                            SaveSuccessfulPrinter(printer.PrinterName);
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

        /// <summary>
        /// Сохранение информации об успешно использованном принтере
        /// </summary>
        public static void SaveSuccessfulPrinter(string printerName)
        {
            if (string.IsNullOrEmpty(printerName))
                return;

            lock (_syncLock)
            {
                try
                {
                    // Записываем имя принтера и текущую дату/время
                    string data = $"{printerName}|{DateTime.Now.Ticks}";
                    File.WriteAllText(_settingsFilePath, data);

                    Log.Debug($"Сохранена информация об успешном использовании принтера: {printerName}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Ошибка при сохранении информации о принтере: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Получение имени последнего успешно использованного принтера
        /// </summary>
        private static string GetLastSuccessfulPrinter()
        {
            lock (_syncLock)
            {
                try
                {
                    if (File.Exists(_settingsFilePath))
                    {
                        string data = File.ReadAllText(_settingsFilePath);
                        string[] parts = data.Split('|');
                        if (parts.Length >= 1)
                        {
                            // Проверяем, не устарела ли запись (более 24 часов)
                            if (parts.Length >= 2 && long.TryParse(parts[1], out long ticks))
                            {
                                DateTime lastTime = new DateTime(ticks);
                                if ((DateTime.Now - lastTime).TotalHours > 24)
                                {
                                    Log.Debug("Информация о последнем принтере устарела (более 24 часов)");
                                    return null;
                                }
                            }
                            return parts[0];
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Ошибка при получении информации о последнем принтере: {ex.Message}");
                }
                return null;
            }
        }
    }
}