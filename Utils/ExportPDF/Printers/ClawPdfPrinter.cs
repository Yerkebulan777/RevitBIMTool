using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class ClawPdfPrinter : PrinterControl
    {
        public override string StatusPath => @"SOFTWARE\clawSoft\clawPDF\Settings\ConversionProfiles";
        public override string RegistryPath => @"SOFTWARE\clawSoft\clawPDF\Settings\ConversionProfiles\0";
        public override string PrinterName => "clawPDF";


        public override void InitializePrinter()
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            RegistryHelper.SetValue(Registry.CurrentUser, StatusPath, "StatusMonitor", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", desktop);
        }


        public override void ResetPrinterSettings()
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            //RegistryHelper.SetValue(Registry.CurrentUser, StatusPath, "StatusMonitor", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", desktop);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", folder.Replace("\\", "\\\\"));
            return PrintHandler.PrintSheet(doc, folder, model);
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }


        public override bool IsPrinterEnabled()
        {
            return base.IsPrinterEnabled();
        }

    }

}
