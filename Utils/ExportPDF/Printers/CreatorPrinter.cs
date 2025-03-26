using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class CreatorPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
    public override string PrinterName => "PDFCreator";
    public override bool IsInternalPrinter => false;


    public override void InitializePrinter()
    {
        Log.Debug("Initialize PDFCreator printer");

        PrinterStateManager.ReservePrinter(PrinterName);

        string autoSave = Path.Combine(RegistryPath, "AutoSave");
        string openViewerKey = Path.Combine(RegistryPath, "OpenViewer");
        RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "Enabled", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "Enabled", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "OpenWithPdfArchitect", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "ExistingFileBehaviour", "Overwrite");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", "<InputFilePath>");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowOnlyErrorNotifications", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowQuickActions", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "Name", "<DefaultProfile>");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "False");
    }


    public override void ReleasePrinterSettings()
    {
        Log.Debug("Release print settings");

        PrinterStateManager.ReleasePrinter(PrinterName);

        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", "<Desktop>");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string directory = folder.Replace("\\", "\\\\");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", directory);
        return PrintHelper.ExecutePrint(doc, model, folder);
    }


}
