using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class CreatorPrinter : PrinterControl
    {
        public override string StatusPath => @"SOFTWARE\pdfforge\PDFCreator\Settings";
        public override string RegistryPath => @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
        public override string PrinterName => "PDFCreator";


        public override void InitializePrinter()
        {
            string autoSave = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string openViewerKey = System.IO.Path.Combine(RegistryPath, "OpenViewer");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, StatusPath, "StatusMonitor", 1);
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


        public override void ResetPrinterSettings()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            //RegistryHelper.SetValue(Registry.CurrentUser, StatusPath, "StatusMonitor", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "True");
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "TargetDirectory", folder.Replace("\\", "\\\\"));
            return PrintHandler.PrintSheet(doc, folder, model);
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }

    }

}
