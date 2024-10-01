using Microsoft.Win32;
using RevitBIMTool.Utils.ExportPdfUtil;


namespace RevitBIMTool.Utils.Printers
{
    internal sealed class AdobePdfPrinter : PrinterBase
    {
        private readonly string registryKey = @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";
        public override string Name => "Adobe PDF";

        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "bExecViewer", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "bShowSaveDialog", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "sPDFFileName", "$fileName");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "bPromptForPDFFilename", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "sOutputDir", @"C:\PDFs");

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
        }


        public override void ResetPrinterSettings()
        {
            string deskPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "bExecViewer", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "bShowSaveDialog", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "sPDFFileName", string.Empty);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "bPromptForPDFFilename", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "sOutputDir", deskPath);
        }

        public override void SetPrinterOutput(string filePath)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "sOutputDir", filePath);
        }
    }

}
