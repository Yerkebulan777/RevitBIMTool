using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class FoxitPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Foxit Software\Printer\Foxit Reader PDF Printer";
    public override string PrinterName => "Foxit PDF Editor Printer";
    public override bool IsInternalPrinter => false;

    public override void InitializePrinter()
    {
        Log.Debug("Initialize Foxit PDF Printer");

        try
        {
            PrinterStateManager.ReservePrinter(PrinterName);

            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSave", "1");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoOverwrite", "1");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowSaveDialog", "0");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowPrintProgress", "0");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Foxit PDF Printer: {0}", ex.Message);
            throw new InvalidOperationException("Failed to initialize Foxit PDF Printer", ex);
        }
    }

    public override void ReleasePrinterSettings()
    {
        Log.Debug("Reset Foxit PDF Printer settings");

        try
        {
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowSaveDialog", "1");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowPrintProgress", "1");

            PrinterStateManager.ReleasePrinter(PrinterName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reset Foxit PDF Printer settings: {0}", ex.Message);
            throw new InvalidOperationException("Failed to reset Foxit PDF Printer settings", ex);
        }
    }

    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        try
        {
            string filePath = Path.Combine(folder, $"{model.SheetName}.pdf");

            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SavePath", folder);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "Filename", Path.GetFileName(filePath));

            return PrintHelper.ExecutePrint(doc, model, folder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Foxit PDF printing: {0}", ex.Message);
            throw new InvalidOperationException("Error during Foxit PDF printing", ex);
        }
    }
}
