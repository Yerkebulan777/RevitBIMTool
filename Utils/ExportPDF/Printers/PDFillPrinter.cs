using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using System.Threading;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class PDFillPrinter : PrinterControl
{
    // Путь к основным настройкам PDFill в реестре
    public override string RegistryPath => @"SOFTWARE\PlotSoft\Writer\";

    // Имя принтера как оно отображается в системе
    public override string PrinterName => "PDFill PDF&Image Writer";

    // Это не внутренний принтер Revit
    public override bool IsInternalPrinter => false;

    // Дополнительные пути к важным ключам реестра
    private string OutputOptionsPath => Path.Combine(RegistryPath, "OutputOption");
    private string PdfOptimizationPath => Path.Combine(RegistryPath, "PDF_Optimization");
    private string OutputFilePath => Path.Combine(RegistryPath, "OutputFile");

    /// <summary>
    /// Инициализирует принтер PDFill, устанавливая все необходимые параметры для работы без интерфейса
    /// </summary>
    public override void InitializePrinter()
    {
        Log.Debug("Инициализация принтера {PrinterName}", PrinterName);
        PrinterStateManager.ReservePrinter(PrinterName);

        // Настройки вывода
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "HIDE_DIALOG", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FOLDER", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FILENAME", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_PRINT_JOBNAME", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "TIME_STAMP", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "VIEW_FILE", 0);

        // Отключение подтверждения перезаписи
        RegistryHelper.SetValue(Registry.CurrentUser, OutputFilePath, "CONFIRM_OVERWRITE", 0);

        // Настройки оптимизации PDF
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "USEOPTIMIZATION", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "Auto_Rotate_Page", 2); // 2 = Page by Page - оптимальный вариант для правильной ориентации
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "Resolution", 300);
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "Color_Model", 1); // 1 = Device RGB
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "CompressFont", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "Settings", 2); // 2 = Printers (оптимизация для принтеров)

        // Дополнительные настройки в корневом пути
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AUTO_NAME", 1);
    }

    /// <summary>
    /// Восстанавливает предыдущие настройки принтера
    /// </summary>
    public override void ReleasePrinterSettings()
    {
        Log.Debug("Освобождение настроек принтера {PrinterName}", PrinterName);

        // Восстановление настроек интерфейса до более дружественных для пользователя
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "DEFAULT_FILENAME", string.Empty);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FOLDER", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FILENAME", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_PRINT_JOBNAME", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "HIDE_DIALOG", 0);

        // Восстановление запроса при перезаписи
        RegistryHelper.SetValue(Registry.CurrentUser, OutputFilePath, "CONFIRM_OVERWRITE", 1);

        // Отключение оптимизации
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "USEOPTIMIZATION", 0);

        // Освобождение принтера для других процессов
        PrinterStateManager.ReleasePrinter(PrinterName);
    }

    /// <summary>
    /// Выполняет печать документа с использованием PDFill PDF Writer
    /// </summary>
    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        try
        {
            // Установка пути для сохранения файла
            RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "DEFAULT_FOLDER_PATH", folder);
            RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "DEFAULT_FILENAME", model.SheetName);

            // Устанавливаем правильную ориентацию на основе модели
            if (model.SheetOrientation == PageOrientationType.Landscape)
            {
                // Дополнительные параметры для ландшафтной ориентации
                Log.Debug("Устанавливаем ландшафтную ориентацию для {SheetName}", model.SheetName);
                RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FORCE_LANDSCAPE", 1);
            }
            else
            {
                // Для портретной ориентации
                Log.Debug("Устанавливаем портретную ориентацию для {SheetName}", model.SheetName);
                RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FORCE_LANDSCAPE", 0);
            }

            // Непосредственно печать документа
            bool result = PrintHelper.ExecutePrint(doc, model, folder);

            // Пауза для завершения процесса PDFill - увеличена для надежности
            Thread.Sleep(800);

            // Проверяем, был ли создан файл
            string filePath = Path.Combine(folder, model.SheetName);
            bool fileExists = File.Exists(filePath);

            if (!fileExists)
            {
                Log.Warning("Файл не был создан после печати: {FilePath}", filePath);
                // Дополнительная пауза и повторная проверка
                Thread.Sleep(1000);
                fileExists = File.Exists(filePath);
                Log.Debug("Повторная проверка файла: {Exists}", fileExists);
            }

            return result && fileExists;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при печати через PDFill: {Message}", ex.Message);
            return false;
        }
    }
}