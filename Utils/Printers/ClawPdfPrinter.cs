using Microsoft.Win32;
using RevitBIMTool.Utils.ExportPdfUtil;
using System.IO;

namespace RevitBIMTool.Utils.Printers
{
    internal sealed class ClawPdfPrinter : PrinterBase
    {
        private readonly string registryKey = @"SOFTWARE\clawSoft\clawPDF\Settings\ConversionProfiles\0";
        public override string Name => "clawPDF";


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "OpenViewer", "False");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "SkipPrintDialog", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<InputFilename>");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "Enabled", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "TargetDirectory", "<InputFilePath>");

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
        }


        public override void ResetPrinterSettings()
        {
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "OpenViewer", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "SkipPrintDialog", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "Enabled", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "TargetDirectory", string.Empty);
        }


        public override void SetPrinterOutput(string filePath)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "TargetDirectory", filePath);
        }

    }

}
