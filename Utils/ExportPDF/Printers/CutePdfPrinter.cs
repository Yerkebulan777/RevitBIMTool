using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class CutePdfPrinter : PrinterControl
{
    // Обновленный путь к настройкам CutePDF Writer
    public override string RegistryPath => @"SOFTWARE\Acro Software Inc\CPW\CutePDF Writer";
    public override string PrinterName => "CutePDF Writer";
    public override bool IsInternalPrinter => false;

    public override void InitializePrinter()
    {
        Log.Debug("Initialize CutePDF Writer printer");

        try
        {
            PrinterStateManager.ReservePrinter(PrinterName);

            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "1");

            Log.Debug("CutePDF Writer initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize CutePDF Writer: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to initialize CutePDF Writer", ex);
        }
    }

    public override void ReleasePrinterSettings()
    {
        Log.Debug("Reset CutePDF Writer settings");

        try
        {
            // Возвращаем настройки к исходным значениям
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "0");

            // Удаляем установленный путь вывода
            if (RegistryHelper.IsValueExists(Registry.CurrentUser, RegistryPath, "OutputFile"))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputFile", string.Empty);
            }

            PrinterStateManager.ReleasePrinter(PrinterName);

            Log.Debug("CutePDF Writer settings reset successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset CutePDF Writer settings: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to reset CutePDF Writer settings", ex);
        }
    }

    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        try
        {
            // Полный путь к файлу PDF
            string filePath = Path.Combine(folder, $"{model.SheetName}.pdf");

            Log.Debug("Setting CutePDF output file: {FilePath}", filePath);

            // Устанавливаем выходной файл перед печатью
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputFile", filePath);

            // Включаем режим обхода диалога сохранения
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "1");

            // Запускаем процесс печати
            bool result = PrintHelper.ExecutePrint(doc, model, folder);

            if (result && File.Exists(filePath))
            {
                model.TempFilePath = filePath;
                model.IsSuccessfully = true;
                Log.Information("Document successfully printed to PDF: {FileName}", model.SheetName);
                return true;
            }
            else
            {
                Log.Warning("Failed to print document to PDF or file not created: {FilePath}", filePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during PDF printing with CutePDF: {Message}", ex.Message);
            throw new InvalidOperationException("Error during PDF printing with CutePDF", ex);
        }
    }

    public override bool IsPrinterInstalled()
    {
        bool isInstalled = base.IsPrinterInstalled();

        if (isInstalled)
        {
            try
            {
                // Проверяем доступ к настройкам принтера
                RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "0");
                Log.Debug("CutePDF Writer is installed and accessible");
            }
            catch (Exception ex)
            {
                Log.Warning("CutePDF Writer is installed but settings are not accessible: {Message}", ex.Message);
                isInstalled = false;
            }
        }

        return isInstalled;
    }
}
