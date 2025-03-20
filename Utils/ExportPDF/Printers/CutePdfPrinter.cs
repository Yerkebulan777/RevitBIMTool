using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class CutePdfPrinter : PrinterControl
{
    public override string RegistryPath => @"Software\CutePDF Writer";
    public override string PrinterName => "CutePDF Writer";
    public override string RevitFilePath { get; set; }
    public override bool IsInternal => false;


    public override void InitializePrinter()
    {
        Log.Debug("Initialize CutePDF Writer printer");

        PrinterStateManager.ReservePrinter(PrinterName);

        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "1");
    }


    public override void ReleasePrinterSettings()
    {
        Log.Debug("Reset CutePDF Writer settings");

        PrinterStateManager.ReleasePrinter(PrinterName);

        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "0");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputFile", string.Empty);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string outputFilePath = Path.Combine(folder, model.SheetName);
        Log.Debug("Setting CutePDF output file: {FilePath}", outputFilePath);

        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputFile", outputFilePath);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "1");

        return PrintHelper.ExecutePrint(doc, model, folder);
    }


}