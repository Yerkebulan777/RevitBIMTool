using Microsoft.Win32;
using RevitBIMTool.Utils.ExportPdfUtil;
using System.IO;


namespace RevitBIMTool.Utils.Printers
{
    internal sealed class PdfCreatorPrinter : PrinterBase
    {
        private string registryKey;
        public override string Name => "PDFCreator";


        public override void InitializePrinter()
        {
            registryKey = @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";

            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<InputFilename>");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "Enabled", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "OpenViewer"), "Enabled", "False");
                RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "OpenViewer"), "OpenWithPdfArchitect", "False");

                return;
            }

            throw new Exception();
        }


        public override void ResetPrinterSettings()
        {
            // тут незнаю 
            // сброс настроек по умолчанию
        }


        public override void SetPrinterOutput(string filePath)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "TargetDirectory", filePath);
        }
    }
}
