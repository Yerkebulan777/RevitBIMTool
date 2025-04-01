using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class SevenPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\7-PDF\Printer";
    public override string PrinterName => "7-PDF Printer";
    public override bool IsInternalPrinter => false;


    public override void InitializePrinter()
    {
        try
        {
            PrinterStateManager.ReservePrinter(PrinterName);

            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSave", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowSaveDialog", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OverwriteExisting", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "DefaultFileName", "<InputFilename>");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize 7-PDF Printer: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to initialize 7-PDF Printer", ex);
        }
    }


    public override void ReleasePrinterSettings()
    {
        try
        {
            PrinterStateManager.ReleasePrinter(PrinterName);

            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSave", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowSaveDialog", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "DefaultFileName", string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset 7-PDF Printer settings: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to reset 7-PDF Printer settings", ex);
        }
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        try
        {
            string directory = folder.Replace("\\", "\\\\");

            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputFolder", directory);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileName", model.SheetName);

            return PrintHelper.ExecutePrint(doc, model, folder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error printing with 7-PDF Printer: {Message}", ex.Message);
            throw new InvalidOperationException("Error printing with 7-PDF Printer", ex);
        }
    }


}