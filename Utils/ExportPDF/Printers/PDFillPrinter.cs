using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

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

    /// <summary>
    /// Инициализирует принтер PDFill, устанавливая все необходимые параметры для работы без интерфейса
    /// </summary>
    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        // Настройки вывода
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "EXIST_PDF", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "HIDE_DIALOG", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "LAUNCH_OPTION", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "LASTPDFORIMAGE", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FOLDER", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FILENAME", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_PRINT_JOBNAME", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "TIME_STAMP", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "VIEW_FILE", 0);

        // Настройки оптимизации PDF
        RegistryHelper.SetValue(Registry.CurrentUser, PdfOptimizationPath, "USEOPTIMIZATION", 0);
    }

    /// <summary>
    /// Восстанавливает предыдущие настройки принтера
    /// </summary>
    public override void ReleasePrinterSettings()
    {
        // Восстановление настроек интерфейса до более дружественных для пользователя
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "DEFAULT_FILENAME", string.Empty);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FOLDER", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_DEFAULT_FILENAME", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "USE_PRINT_JOBNAME", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, OutputOptionsPath, "HIDE_DIALOG", 0);

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
            return PrintHelper.ExecutePrint(doc, model, folder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при печати через PDFill: {Message}", ex.Message);
            return false;
        }
    }
}