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

        const int maxRetries = 1000;
        const int retryDelay = 1000;

        while (retryCount < maxRetries)
        {
            retryCount++;
            Thread.Sleep(retryDelay);
            Log.Debug("Searching for an available printer...");

            foreach (PrinterControl printer in GetPrinters())
            {
                try
                {
                    if (printer.IsAvailable(revitFilePath))
                    {
                        availablePrinter = printer;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }
        }

        Log.Error("No available printer found!");

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
            new InternalPrinter()
        ];
    }

    /// <summary>
    /// Проверяет, доступен ли принтер
    /// </summary>
    public static bool IsPrinterAvailable(string printerName)
    {
        try
        {
            PrinterStateData states = XmlHelper.LoadFromXml<PrinterStateData>(stateFilePath);
            PrinterInfo printerInfo = EnsurePrinterExists(states, printerName);
            return printerInfo.IsAvailable;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{PrinterName}: {Message}", printerName, ex.Message);
            throw new InvalidOperationException($"{printerName}: {ex.Message}");
        }
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
    /// Сохраняет данные о состоянии принтеров с валидацией и обновлением существующих записей
    /// </summary>
    /// <param name="states">Данные о состоянии принтеров для сохранения</param>
    /// <returns>Успешность операции сохранения</returns>
    public static bool SavePrinterState(PrinterStateData states)
    {
        if (states?.Printers == null)
        {
            return false;
        }

        try
        {
            // Загружаем текущие данные из файла для синхронизации
            PrinterStateData currentStates = XmlHelper.LoadFromXml<PrinterStateData>(stateFilePath);

            // Если файл существует и данные получены успешно
            if (currentStates?.Printers != null)
            {
                // Создаем словарь нормализованных имен принтеров и их состояний
                Dictionary<string, PrinterInfo> mergedPrinters = new(StringComparer.OrdinalIgnoreCase);

                // Сначала добавляем текущие данные из файла
                foreach (PrinterInfo printer in currentStates.Printers)
                {
                    string normalizedName = printer.PrinterName.Replace(" ", "");
                    mergedPrinters[normalizedName] = printer;
                }

                // Затем обновляем или добавляем новые данные
                foreach (PrinterInfo printer in states.Printers)
                {
                    string normalizedName = printer.PrinterName.Replace(" ", "");

                    // Если принтер уже существует, обновляем его статус
                    if (mergedPrinters.TryGetValue(normalizedName, out PrinterInfo existingPrinter))
                    {
                        existingPrinter.IsAvailable = printer.IsAvailable;
                        Log.Debug("Updated printer status: {PrinterName}", existingPrinter.PrinterName);
                    }
                    else
                    {
                        // Иначе добавляем новый принтер
                        mergedPrinters[normalizedName] = printer;
                        Log.Debug("Added new printer: {PrinterName}", printer.PrinterName);
                    }
                }

                // Обновляем список принтеров с объединенными данными
                states.Printers = mergedPrinters.Values.ToList();
            }

            // Обновляем временную метку
            states.LastUpdate = DateTime.Now;

            // Сохраняем обновленные данные
            return XmlHelper.SaveToXml(states, stateFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save printer state with validation: {Message}", ex.Message);
            return false;
        }
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
    public static bool ReservePrinter(string printerName)
    {
        return SetAvailability(printerName, false);
    }

    /// <summary>
    /// Освобождает принтер, делая его доступным для других процессов
    /// </summary>
    public static bool ReleasePrinter(string printerName)
    {
        return SetAvailability(printerName, true);
    }

}