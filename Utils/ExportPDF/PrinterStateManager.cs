using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using System.Xml.Serialization;

namespace RevitBIMTool.Utils.ExportPDF;

/// <summary>
/// Модель данных для хранения состояния принтеров
/// </summary>
[Serializable]
[XmlRoot("PrinterStates")]
internal class PrinterStates
{
    [XmlElement("LastUpdate")]
    public DateTime LastUpdate { get; set; }

    [XmlArray("Printers")]
    [XmlArrayItem("Printer")]
    public List<PrinterInfo> Printers { get; set; } = [];
}

/// <summary>
/// Модель данных для хранения информации о принтере
/// </summary>
[Serializable]
internal class PrinterInfo
{
    [XmlAttribute("Name")]
    public string PrinterName { get; set; }

    [XmlElement("IsAvailable")]
    public bool IsAvailable { get; set; }

    // Конструктор без параметров для сериализации
    public PrinterInfo() { }

    public PrinterInfo(string name, bool isAvailable)
    {
        IsAvailable = isAvailable;
        PrinterName = name;
    }
}

internal static class PrinterStateManager
{
    private const string StateMutexName = "Global\\RevitPrinterStateMutex";
    private static readonly string userDocsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static readonly string appDataFolder = Path.Combine(userDocsPath, "RevitBIMTool");
    private static readonly string _stateFilePath = Path.Combine(appDataFolder, "PrinterState.xml");

    static PrinterStateManager()
    {
        EnsureStateFileExists();
    }

    /// <summary>
    /// Создает файл состояния принтеров, если он отсутствует
    /// </summary>
    private static void EnsureStateFileExists()
    {
        if (!File.Exists(_stateFilePath))
        {
            PrinterStates initialState = new()
            {
                LastUpdate = DateTime.Now,
                Printers = []
            };

            // Добавляем информацию о всех принтерах
            foreach (PrinterControl printer in GetPrinters())
            {
                initialState.Printers.Add(new PrinterInfo(printer.PrinterName, true));
            }

            _ = XmlHelper.SaveToXml(initialState, _stateFilePath);
        }
    }

    /// <summary>
    /// Попытка получить доступный принтер для печати
    /// </summary>
    public static bool TryRetrievePrinter(out PrinterControl availablePrinter)
    {
        int retryCount = 0;
        availablePrinter = null;
        const int maxRetries = 1000;
        const int retryDelay = 1000;

        while (retryCount < maxRetries)
        {
            retryCount++;
            Thread.Sleep(retryDelay);
            Log.Debug($"Поиск доступного принтера...");

            foreach (PrinterControl printer in GetPrinters())
            {
                try
                {
                    if (printer.IsAvailable())
                    {
                        if (SetPrinterAvailability(printer.PrinterName, false))
                        {
                            Log.Debug($"Доступный принтер: {printer.PrinterName}");
                            availablePrinter = printer;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }
        }

        Log.Error($"Не найден доступный принтер!");

        return false;
    }

    /// <summary>
    /// Получить список принтеров в порядке приоритета
    /// </summary>
    public static List<PrinterControl> GetPrinters()
    {
        return
        [
            new Pdf24Printer(),
            new CreatorPrinter(),
            new ClawPdfPrinter(),
            new InternalPrinter()
        ];
    }

    /// <summary>
    /// Проверяет, доступен ли принтер
    /// </summary>
    public static bool IsPrinterAvailable(string printerName)
    {
        using Mutex mutex = new(false, StateMutexName);

        if (mutex.WaitOne(5000))
        {
            try
            {
                PrinterStates states = XmlHelper.LoadFromXml<PrinterStates>(_stateFilePath);

                if (states == null)
                {
                    Log.Warning("Не удалось загрузить файл состояния принтеров");
                    return false;
                }

                PrinterInfo printerInfo = states.Printers.Find(p => p.PrinterName == printerName);

                if (printerInfo == null)
                {
                    // Если принтера нет в списке, добавляем его
                    printerInfo = new PrinterInfo(printerName, true);
                    states.Printers.Add(printerInfo);
                    states.LastUpdate = DateTime.Now;
                    XmlHelper.SaveToXml(states, _stateFilePath);
                }

                return printerInfo.IsAvailable;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Ошибка при проверке доступности принтера {printerName}: {ex.Message}");
                return false;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        else
        {
            Log.Warning("Таймаут ожидания доступа к файлу состояния принтеров");
            return false;
        }
    }

    /// <summary>
    /// Устанавливает доступность принтера
    /// </summary>
    public static bool SetPrinterAvailability(string printerName, bool isAvailable)
    {
        using Mutex mutex = new(false, StateMutexName);

        if (mutex.WaitOne(5000))
        {
            try
            {
                PrinterStates states = XmlHelper.LoadFromXml<PrinterStates>(_stateFilePath);

                if (states == null)
                {
                    Log.Warning("Не удалось загрузить файл состояния принтеров");
                    return false;
                }

                PrinterInfo printerInfo = states.Printers.Find(p => p.PrinterName == printerName);

                if (printerInfo == null)
                {
                    // Если принтера нет в списке, добавляем его
                    printerInfo = new PrinterInfo(printerName, isAvailable);
                    states.Printers.Add(printerInfo);
                }
                else
                {
                    // Меняем состояние существующего принтера
                    printerInfo.IsAvailable = isAvailable;
                }

                states.LastUpdate = DateTime.Now;
                return XmlHelper.SaveToXml(states, _stateFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Ошибка при изменении доступности принтера {printerName}: {ex.Message}");
                return false;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        else
        {
            Log.Warning("Таймаут ожидания доступа к файлу состояния принтеров");
            return false;
        }
    }

    /// <summary>
    /// Освобождает принтер, делая его доступным для других процессов
    /// </summary>
    public static bool ReleasePrinter(string printerName)
    {
        return SetPrinterAvailability(printerName, true);
    }
}