using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF
{
    /// <summary>
    /// Менеджер состояний принтеров для многопроцессной среды
    /// </summary>
    internal static class PrinterStateManager
    {
        private const string MutexName = "Global\\RevitPrinterStateMutex";
        private const string StateFileName = "revit-printer-state.txt";
        private static readonly string _stateFilePath;

        // Модель данных для хранения информации о принтере
        public class PrinterInfo
        {
            public string PrinterName { get; set; }
            public DateTime LastSuccessTime { get; set; }
            public int IsAvailable { get; set; }

            public PrinterInfo()
            {
                // Конструктор без параметров
            }

            public PrinterInfo(string name, bool isAvailable)
            {
                PrinterName = name;
                LastSuccessTime = DateTime.Now;
                IsAvailable = isAvailable ? 1 : 0;
            }

            // Преобразование в строку для записи в файл
            public string ToFileString()
            {
                return $"{PrinterName}|{IsAvailable}|{LastSuccessTime.Ticks}";
            }

            // Создание объекта из строки файла
            public static PrinterInfo FromFileString(string fileString)
            {
                string[] parts = fileString.Split('|');
                if (parts.Length != 3)
                    return null;

                PrinterInfo info = new PrinterInfo();
                info.PrinterName = parts[0];

                if (int.TryParse(parts[1], out int available))
                    info.IsAvailable = available;

                if (long.TryParse(parts[2], out long ticks))
                    info.LastSuccessTime = new DateTime(ticks);

                return info;
            }
        }

        // Инициализация пути к файлу состояния
        static PrinterStateManager()
        {
            string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appDataFolder = Path.Combine(myDocumentsPath, "RevitBIMTool");

            // Обеспечиваем существование директории
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            _stateFilePath = Path.Combine(appDataFolder, StateFileName);
            Log.Debug($"Файл состояния принтеров будет сохранен по пути: {_stateFilePath}");
        }

        /// <summary>
        /// Попытка получить доступный принтер для печати
        /// </summary>
        /// <param name="availablePrinter">Найденный принтер</param>
        /// <returns>True, если принтер найден</returns>
        public static bool TryRetrievePrinter(out PrinterControl availablePrinter)
        {
            availablePrinter = null;
            int retryCount = 0;

            while (retryCount < 1000)  // Используем тот же цикл, что и в оригинале
            {
                retryCount++;
                Thread.Sleep(1000);  // Пауза между попытками
                Log.Debug($"Поиск доступного принтера... Попытка #{retryCount}/1000");

                // Используем мьютекс для синхронизации доступа между процессами
                using (Mutex mutex = new Mutex(false, MutexName))
                {
                    try
                    {
                        if (mutex.WaitOne(5000))  // Ожидаем доступ к ресурсу с таймаутом
                        {
                            try
                            {
                                // Перебираем принтеры в заданном порядке приоритета
                                foreach (PrinterControl printer in GetPrinters())
                                {
                                    try
                                    {
                                        if (printer.IsAvailable())
                                        {
                                            availablePrinter = printer;
                                            Log.Debug($"Найден доступный принтер: {printer.PrinterName}");

                                            // Сохраняем информацию о рабочем принтере
                                            SavePrinterState(printer.PrinterName, true);

                                            return true;
                                        }
                                        else
                                        {
                                            Log.Debug($"Принтер {printer.PrinterName} недоступен");
                                            SavePrinterState(printer.PrinterName, false);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error($"Ошибка при проверке принтера {printer.PrinterName}: {ex.Message}");
                                        SavePrinterState(printer.PrinterName, false);
                                    }
                                }
                            }
                            finally
                            {
                                mutex.ReleaseMutex();  // Всегда освобождаем мьютекс
                            }
                        }
                        else
                        {
                            Log.Warning("Не удалось получить доступ к мьютексу принтера (таймаут)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Ошибка синхронизации при поиске принтера: {ex.Message}");
                    }
                }
            }

            Log.Error("Не найден доступный принтер после 1000 попыток.");
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
        /// Сохранение информации о состоянии принтера
        /// </summary>
        private static void SavePrinterState(string printerName, bool isAvailable)
        {
            try
            {
                // Загружаем текущие данные о принтерах
                Dictionary<string, PrinterInfo> printerDict = LoadPrinterStates();

                // Обновляем информацию о принтере
                if (printerDict.ContainsKey(printerName))
                {
                    printerDict[printerName].IsAvailable = isAvailable ? 1 : 0;
                    printerDict[printerName].LastSuccessTime = DateTime.Now;
                }
                else
                {
                    printerDict[printerName] = new PrinterInfo(printerName, isAvailable);
                }

                // Сохраняем все записи в файл
                List<string> lines = new List<string>();
                lines.Add($"# Состояние принтеров Revit, обновлено: {DateTime.Now}");

                foreach (PrinterInfo info in printerDict.Values)
                {
                    lines.Add(info.ToFileString());
                }

                File.WriteAllLines(_stateFilePath, lines);

                Log.Debug($"Сохранено состояние принтера {printerName}: {(isAvailable ? "доступен" : "недоступен")}");
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка при сохранении состояния принтера: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка информации о принтерах из файла
        /// </summary>
        private static Dictionary<string, PrinterInfo> LoadPrinterStates()
        {
            Dictionary<string, PrinterInfo> result = new Dictionary<string, PrinterInfo>();

            try
            {
                if (File.Exists(_stateFilePath))
                {
                    string[] lines = File.ReadAllLines(_stateFilePath);

                    foreach (string line in lines)
                    {
                        // Пропускаем комментарии и пустые строки
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        PrinterInfo info = PrinterInfo.FromFileString(line);
                        if (info != null && !string.IsNullOrEmpty(info.PrinterName))
                        {
                            result[info.PrinterName] = info;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка при чтении файла состояний принтеров: {ex.Message}");
            }

            return result;
        }
    }
}