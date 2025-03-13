using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class CreatorPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
    public override string PrinterName => "PDFCreator";


    public override void InitializePrinter()
    {
        string autoSave = System.IO.Path.Combine(RegistryPath, "AutoSave");
        string openViewerKey = System.IO.Path.Combine(RegistryPath, "OpenViewer");
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

        RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
    }


    public override void ResetPrinterSettings()
    {
        Log.Debug("Reset print settings");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", "<Desktop>");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
        RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
    }


    public override bool Print(Document doc, string folder, SheetModel model)
    {
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", folder.Replace("\\", "\\\\"));
        return PrintHandler.ExecutePrintAsync(doc, folder, model).Result;
    }



}
