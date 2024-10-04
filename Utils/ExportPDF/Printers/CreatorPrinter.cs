using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class CreatorPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
        public override string RegistryName => "PDFCreator";
        public override int OverallRating => 2;


        public override void InitializePrinter()
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string openViewerKey = System.IO.Path.Combine(RegistryPath, "OpenViewer");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "Enabled", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "OpenWithPdfArchitect", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", "<InputFilePath>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "CompressionLevel", "medium");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "False");
        }


        public override void ResetPrinterSettings()
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string openViewerKey = System.IO.Path.Combine(RegistryPath, "OpenViewer");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "OpenWithPdfArchitect", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", string.Empty);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "CompressionLevel", "high");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "True");
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", folder);
            return PrintHandler.PrintSheet(doc, folder, model);
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }

    }

}
