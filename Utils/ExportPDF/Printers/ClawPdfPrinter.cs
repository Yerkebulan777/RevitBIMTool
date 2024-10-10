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
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "False");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", desktop);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
        }


        public override void ResetPrinterSettings()
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", desktop);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", folder.Replace("\\", "\\\\"));
            return PrintHandler.PrintSheet(doc, folder, model);
        }


    }

}
