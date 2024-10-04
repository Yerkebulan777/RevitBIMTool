using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class CreatorPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
        public override string PrinterName => "PDFCreator";
        public override int OverallRating => 2;


        public override void InitializePrinter()
        {
            string autoSave = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string openViewerKey = System.IO.Path.Combine(RegistryPath, "OpenViewer");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "Enabled", "True");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "Enabled", "False");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "OpenWithPdfArchitect", "False");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", "<InputFilePath>");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowOnlyErrorNotifications", "True");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "False");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowQuickActions", "False");
            //_ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveDirectory", "True");
            //_ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveEnabled", "True");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "False");
        }


        public override void ResetPrinterSettings()
        {
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", string.Empty);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "True");
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            if (RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", folder) is null)
            {
                throw new InvalidOperationException("Failed set directory!");
            }

            return PrintHandler.PrintSheet(doc, folder, model);
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }

    }

}
