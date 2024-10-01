using Microsoft.Win32;
using RevitBIMTool.Utils.ExportPdfUtil;
using System.IO;


namespace RevitBIMTool.Utils.Printers
{
    internal sealed class AdobePdfPrinter : PrinterBase
    {
        private readonly string registryKey = @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";
        public override string Name => "Adobe PDF";

        string revitVersion;

        public void ActivateSettingsForAdobePdf(string outputFile)
        {
            string directory = Path.GetDirectoryName(outputFile);

            string application = "C:\\Program Files\\Autodesk\\Revit 2023\\Revit.exe";

            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, application, outputFile);
        }


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                RevitBIMToolApp.
                string deskPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "LastPdfPortFolder - Revit.exe", string.Empty);


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
