using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class ClawPdfPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\clawSoft\clawPDF\Settings\ConversionProfiles\0";
        public override string PrinterName => "clawPDF";


        public override void InitializePrinter()
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", "<InputFilePath>");
            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
        }


        public override void ResetPrinterSettings()
        {
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", folder.Replace("\\", "\\\\"));
            return PrintHandler.PrintSheet(doc, folder, model);
        }

    }

}
