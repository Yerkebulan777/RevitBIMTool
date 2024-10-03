using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.SystemHelpers;
using System.IO;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal sealed class ClawPdfPrinter : PrinterControl
    {
        private readonly string registryKey = @"SOFTWARE\clawSoft\clawPDF\Settings\ConversionProfiles\0";
        public override string Name => "clawPDF";
        public override int OverallRating => 4;


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(RegistryHive.CurrentUser, registryKey))
            {
                string autoSaveKey = Path.Combine(registryKey, "AutoSave");
                RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "OpenViewer", "False");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "SkipPrintDialog", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<InputFilename>");
                RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", "<InputFilePath>");

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
        }


        public override void ResetPrinterSettings()
        {
            string autoSaveKey = Path.Combine(registryKey, "AutoSave");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "OpenViewer", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "SkipPrintDialog", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", string.Empty);
        }


        public override void SetPrinterOutput(string filePath)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "TargetDirectory", filePath);
            Thread.Sleep(100);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            return PrintHandler.PrintSheet(doc, folder, model);
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }

    }

}
