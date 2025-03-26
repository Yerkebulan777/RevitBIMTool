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
    public override bool IsInternalPrinter => false;


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

        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputFile", string.Empty);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "0");
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string filePath = Path.Combine(folder,  $"{model.SheetName}.pdf").Replace("\\", "\\\\");

        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputFile", filePath);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "BypassSaveAs", "1");

        return PrintHelper.ExecutePrint(doc, model, folder);
    }


}