using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class PdfXchangePrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Tracker Software\PDF-XChange Standard";
    public override string PrinterName => "PDF-XChange Standard";
    public override bool IsInternalPrinter => false;

    // Дополнительный путь для настроек сохранения
    private const string PrintSettingsPath = @"SOFTWARE\Tracker Software\PDF-XChange Standard\Printing";

    public override void InitializePrinter()
    {
        Log.Debug("Initialize PDF-XChange Standard printer");

        try
        {
            PrinterStateManager.ReservePrinter(PrinterName);

            // Настройки для автоматического сохранения
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSave", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "PromptForFileName", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ConfirmOverwrite", 0);

            // Дополнительные настройки печати, если доступны
            if (RegistryHelper.IsKeyExists(Registry.CurrentUser, PrintSettingsPath))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, PrintSettingsPath, "Silent", 1);
                RegistryHelper.SetValue(Registry.CurrentUser, PrintSettingsPath, "ShowPreview", 0);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize PDF-XChange Standard: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to initialize PDF-XChange Standard", ex);
        }
    }

    public override void ReleasePrinterSettings()
    {
        Log.Debug("Reset PDF-XChange Standard printer settings");

        try
        {
            // Восстановление стандартных настроек
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "PromptForFileName", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSave", 0);

            // Сброс дополнительных настроек
            if (RegistryHelper.IsKeyExists(Registry.CurrentUser, PrintSettingsPath))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, PrintSettingsPath, "Silent", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, PrintSettingsPath, "ShowPreview", 1);
            }

            PrinterStateManager.ReleasePrinter(PrinterName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset PDF-XChange Standard settings: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to reset PDF-XChange Standard settings", ex);
        }
    }

    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        try
        {
            string directory = folder.Replace("\\", "\\\\");

            // Устанавливаем путь для сохранения
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "DefaultSaveFolder", directory);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "DefaultFileName", model.SheetName);

            bool result = PrintHelper.ExecutePrint(doc, model, folder);

            if (result)
            {
                // Проверка создания файла
                string expectedFilePath = Path.Combine(folder, $"{model.SheetName}.pdf");
                if (File.Exists(expectedFilePath))
                {
                    model.TempFilePath = expectedFilePath;
                    model.IsSuccessfully = true;
                    Log.Information("Document successfully printed: {SheetName}", model.SheetName);
                    return true;
                }
            }

            Log.Warning("Failed to print document: {SheetName}", model.SheetName);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error printing with PDF-XChange Standard: {Message}", ex.Message);
            throw new InvalidOperationException("Error printing with PDF-XChange Standard", ex);
        }
    }

}