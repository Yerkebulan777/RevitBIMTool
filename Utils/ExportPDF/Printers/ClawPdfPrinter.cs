using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;


namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class ClawPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\clawSoft\clawPDF\Settings\ConversionProfiles\0";
    public override string PrinterName => "clawPDF";
    public override bool IsInternalPrinter => false;


    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        string autoSaveKey = Path.Combine(RegistryPath, "AutoSave");
        RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
        RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", "<InputFilePath>");
    }


    public override void ReleasePrinterSettings()
    {
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");

        PrinterStateManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string directory = folder.Replace("\\", "\\\\");
        string autoSaveKey = Path.Combine(RegistryPath, "AutoSave");
        RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", directory);
        return PrintHelper.ExecutePrint(doc, model, folder);
    }


}
