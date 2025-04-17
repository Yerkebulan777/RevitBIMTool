using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class AdobePdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";

    private readonly string RevitExePath = $@"C:\Program Files\Autodesk\Revit {RevitBimToolApp.Version}\Revit.exe";
    public override string PrinterName => "Adobe PDF";
    public override bool IsInternalPrinter => false;



    public override void InitializePrinter()
    {
        Log.Debug("Initialize Adobe PDF printer");

        PrinterStateManager.ReservePrinter(PrinterName);

        if (!RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath))
        {
            Log.Warning("PrinterJobControl path not found in registry, will be created during printing");
        }

        Log.Debug("Adobe PDF printer initialized");
    }

    public override void ReleasePrinterSettings()
    {
        Log.Debug("Release print settings");
        PrinterStateManager.ReleasePrinter(PrinterName);
    }

    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        try
        {
            // Полное имя файла PDF, включая путь
            string fullFilePath = Path.Combine(folder, model.SheetName);

            Log.Debug("Setting Adobe PDF printer destination: {Path}", fullFilePath);

            // Настраиваем назначение PDF-файла по методу, показанному в примере
            SetPDFSettings(fullFilePath, folder);

            // Выполняем печать с использованием принтера Adobe PDF
            return PrintHelper.ExecutePrint(doc, model, folder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error printing with Adobe PDF: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Устанавливает назначение для следующего PDF-файла, чтобы предотвратить запрос на ввод имени файла
    /// </summary>
    private void SetPDFSettings(string destFileName, string dirName)
    {
        try
        {
            Log.Debug("Setting PDF destination: File={DestFile}, Directory={DirName}", Path.GetFileName(destFileName), dirName);

            using (RegistryKey pjcKey = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (pjcKey == null)
                {
                    Log.Warning("Could not open PrinterJobControl registry key");
                    return;
                }

                // Устанавливаем полный путь к файлу как значение для ключа с именем пути к Revit.exe
                pjcKey.SetValue(RevitExePath, destFileName);

                // Устанавливаем директорию как значение для специального ключа LastPdfPortFolder для Revit
                pjcKey.SetValue("LastPdfPortFolder - Revit.exe", dirName);

                Log.Debug("PDF settings applied successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Couldn't access PDF driver registry settings: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to set PDF driver registry settings", ex);
        }
    }
}