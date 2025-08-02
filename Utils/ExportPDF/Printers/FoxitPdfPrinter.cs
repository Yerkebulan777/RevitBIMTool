using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class FoxitPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Foxit Software\Printer\Foxit Reader PDF Printer";
    public override string PrinterName => "Foxit PDF Editor Printer";
    public override bool IsInternalPrinter => false;
    public override string RevitFilePath { get; set; }


    public override void InitializePrinter()
    {
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSave", "1");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoOverwrite", "1");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowSaveDialog", "0");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowPrintProgress", "0");
    }


    public override void ReleasePrinterSettings()
    {
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowSaveDialog", "1");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowPrintProgress", "1");

        PrinterManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        return PrintHelper.ExecutePrint(doc, model, folder);
    }



}
