using RevitBIMTool.Utils.Common;
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
[XmlRoot("PrinterStateData")]
public class PrinterStateData
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
public class PrinterInfo
{
    [XmlAttribute("PrinterName")]
    public string PrinterName { get; set; }

    [XmlElement("IsAvailable")]
    public bool IsAvailable { get; set; }

    public PrinterInfo() { }

    public PrinterInfo(string name, bool isAvailable)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        IsAvailable = isAvailable;
        PrinterName = name;
    }
}


internal static class PrinterStateManager
{
    private static readonly string userDocsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static readonly string appDataFolder = Path.Combine(userDocsPath, "RevitBIMTool");
    private static readonly string stateFilePath = Path.Combine(appDataFolder, "PrinterState.xml");

    static PrinterStateManager()
    {
        EnsureStateFileExists();
    }

    /// <summary>
    /// Создает файл состояния принтеров, если он отсутствует
    /// </summary>
    private static void EnsureStateFileExists()
    {
        PathHelper.EnsureDirectory(appDataFolder);

        if (!File.Exists(stateFilePath))
        {
            PrinterStateData initialState = new()
            {
                LastUpdate = DateTime.Now,
                Printers = []
            };

            foreach (PrinterControl printer in GetPrinters())
            {
                initialState.Printers.Add(new PrinterInfo(printer.PrinterName, true));
            }

            if (XmlHelper.SaveToXml(initialState, stateFilePath))
            {
                Log.Debug("Printer state file created successfully!");
            }
        }
    }

    /// <summary>
    /// Попытка получить доступный принтер для печати
    /// </summary>
    public static bool TryRetrievePrinter(string revitFilePath, out PrinterControl availablePrinter)
    {
        int retryCount = 0;
        availablePrinter = null;
        const int maxRetries = 100;

        List<PrinterControl> printerList = GetPrinters();

        while (retryCount < maxRetries)
        {
            Thread.Sleep(maxRetries * retryCount++);

            Log.Debug("Searching for an available printer...");

            for (int idx = 0; idx < printerList.Count; idx++)
            {
                PrinterControl printer = printerList[idx];

                if (IsPrinterAvailable(printer))
                {
                    Log.Debug("Printer is available!");
                    printer.RevitFilePath = revitFilePath;
                    availablePrinter = printer;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Получить список принтеров в порядке приоритета
    /// </summary>
    public static List<PrinterControl> GetPrinters()
    {
        return
        [
            //new Pdf24Printer(),
            //new CreatorPrinter(),
            //new ClawPdfPrinter(),
            new PDFillPrinter(),
            new InternalPrinter()
        ];
    }

    /// <summary>
    /// Проверяет, доступен ли принтер
    /// </summary>
    public static bool IsPrinterAvailable(PrinterControl printer)
    {
        bool isAvailable = false;

        if (printer.IsPrinterInstalled())
        {
            try
            {
                PrinterStateData states = XmlHelper.LoadFromXml<PrinterStateData>(stateFilePath);
                PrinterInfo printerInfo = EnsurePrinterExists(states, printer.PrinterName);
                isAvailable = printerInfo.IsAvailable;
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{PrinterName}: {Message}", printer.PrinterName, ex.Message);
                throw new InvalidOperationException($"{printer.PrinterName}: {ex.Message}");
            }
            finally
            {
                Log.Debug("{PrinterName} is available: {IsAvailable}", printer.PrinterName, isAvailable);
            }
        }

        return isAvailable;
    }

    /// <summary>
    /// Устанавливает доступность принтера
    /// </summary>
    public static bool SetAvailability(string printerName, bool isAvailable)
    {
        try
        {
            PrinterStateData states = XmlHelper.LoadFromXml<PrinterStateData>(stateFilePath);
            PrinterInfo printerInfo = EnsurePrinterExists(states, printerName);
            printerInfo.IsAvailable = isAvailable;

            return XmlHelper.SaveToXml(states, stateFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{PrinterName}: {Message}", printerName, ex.Message);
            throw new InvalidOperationException($"{printerName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Синхронизирует реестр принтеров, объединяя новые данные с существующими
    /// </summary>
    public static bool SyncPrinterRegistry(PrinterStateData inputState)
    {
        // Загружаем текущее состояние
        PrinterStateData currentState = LoadCurrentState();

        // Объединяем состояния
        PrinterStateData mergedState = IntegrateStates(currentState, inputState);

        // Сохраняем результат
        if (XmlHelper.SaveToXml(mergedState, stateFilePath))
        {
            Log.Debug("Saved {Count} entries", mergedState.Printers.Count);
            return true;
        }

        Log.Warning("Failed to save states");
        return false;
    }

    /// <summary>
    /// Извлекает существующие данные о принтерах из хранилища
    /// </summary>
    private static PrinterStateData LoadCurrentState()
    {
        PrinterStateData state = XmlHelper.LoadFromXml<PrinterStateData>(stateFilePath);

        if (state is null)
        {
            Log.Information("Creating new printer registry");

            state = new PrinterStateData
            {
                LastUpdate = DateTime.Now,
                Printers = []
            };
        }

        return state;
    }

    /// <summary>
    /// Интегрирует данные о принтерах из двух источников
    /// </summary>
    private static PrinterStateData IntegrateStates(PrinterStateData currentState, PrinterStateData newState)
    {
        // Создаем словарь для быстрого поиска
        Dictionary<string, PrinterInfo> printerMap = new(StringComparer.OrdinalIgnoreCase);

        // Добавляем существующие принтеры
        if (currentState?.Printers != null)
        {
            foreach (PrinterInfo printer in currentState.Printers.Where(p => p != null && !string.IsNullOrEmpty(p.PrinterName)))
            {
                string key = XmlHelper.NormalizeString(printer.PrinterName);
                printerMap[key] = printer;
            }
        }

        // Добавляем или обновляем новые принтеры
        foreach (PrinterInfo printer in newState.Printers.Where(p => p != null && !string.IsNullOrEmpty(p.PrinterName)))
        {
            string key = XmlHelper.NormalizeString(printer.PrinterName);

            if (printerMap.TryGetValue(key, out PrinterInfo existingPrinter))
            {
                // Обновляем существующий принтер
                Log.Debug("Updating printer status: {Name} (Available: {IsAvailable})",
                    existingPrinter.PrinterName, printer.IsAvailable);
                existingPrinter.IsAvailable = printer.IsAvailable;
            }
            else
            {
                // Добавляем новый принтер
                Log.Debug("Adding new printer to registry: {Name}", printer.PrinterName);
                printerMap[key] = printer;
            }
        }

        // Обновляем результат
        newState.Printers = printerMap.Values.ToList();
        newState.LastUpdate = DateTime.Now;

        return newState;
    }

    /// <summary>
    /// Добавляет принтер в список, если его там нет то создает новый
    /// </summary>
    public static PrinterInfo EnsurePrinterExists(PrinterStateData states, string printerName, bool isAvailable = true)
    {
        PrinterInfo printerInfo = states.Printers?.Find(p => p.PrinterName == printerName);

        if (printerInfo is null)
        {
            states.Printers.Add(new PrinterInfo(printerName, isAvailable));

            if (!XmlHelper.SaveToXml(states, stateFilePath))
            {
                throw new InvalidOperationException("Failed to save printer state");
            }
        }

        return printerInfo;
    }

    /// <summary>
    /// Резервирует принтер, делая его недоступным для других процессов
    /// </summary>
    public static void ReservePrinter(string printerName)
    {
        if (SetAvailability(printerName, false))
        {
            Log.Debug("{PrinterName} is reserved", printerName);
        }
    }

    /// <summary>
    /// Освобождает принтер, делая его доступным для других процессов
    /// </summary>
    public static void ReleasePrinter(string printerName)
    {
        if (SetAvailability(printerName, true))
        {
            Log.Debug("{PrinterName} is released", printerName);
        }
    }



}