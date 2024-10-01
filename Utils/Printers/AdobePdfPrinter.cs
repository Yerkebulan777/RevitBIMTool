using Microsoft.Win32;
using RevitBIMTool.Utils.ExportPdfUtil;
using System.IO;


namespace RevitBIMTool.Utils.Printers
{
    internal sealed class AdobePdfPrinter : PrinterBase
    {
        private readonly string registryKey = @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";
        private readonly string portFolder = "LastPdfPortFolder - Revit.exe";
        private readonly string revitVersion = RevitBIMToolApp.Version;

        public override string Name => "Adobe PDF";


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                string application = $@"C:\Program Files\Autodesk\Revit {revitVersion}\Revit.exe";
                RegistryHelper.CreateParameter(Registry.CurrentUser, registryKey, portFolder, string.Empty);
                RegistryHelper.CreateParameter(Registry.CurrentUser, registryKey, application, Path.GetTempPath());

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
        }


        public override void ResetPrinterSettings()
        {
            string application = $@"C:\Program Files\Autodesk\Revit {revitVersion}\Revit.exe";
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, portFolder, string.Empty);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, application, string.Empty);
        }


        public override void SetPrinterOutput(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            string application = $@"C:\Program Files\Autodesk\Revit {revitVersion}\Revit.exe";
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, portFolder, directory);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, application, filePath);
        }

    }

}
