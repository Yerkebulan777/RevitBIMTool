using Microsoft.Win32;
using RevitBIMTool.Utils.ExportPdfUtil;
using System.IO;


namespace RevitBIMTool.Utils.Printers
{
    internal sealed class PdfCreatorPrinter : PrinterBase
    {
        public readonly string registryKey = @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
        public override string Name => "PDFCreator";


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<InputFilename>");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "Enabled", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "OpenViewer"), "Enabled", "False");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "OpenViewer"), "OpenWithPdfArchitect", "False");

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
        }


        public override void ResetPrinterSettings()
        {
            string deskPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "TargetDirectory", deskPath);
        }


        public override void SetPrinterOutput(string filePath)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "TargetDirectory", filePath);
        }
    }
}
