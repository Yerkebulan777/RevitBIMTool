using Microsoft.Win32;
using RevitBIMTool.Utils.ExportPdfUtil;


namespace RevitBIMTool.Utils.Printers
{
    internal sealed class Pdf24Printer : PrinterBase
    {
        private string registryKey;
        public override string Name => "PDF24";


        public override void InitializePrinter()
        {
            registryKey = @"SOFTWARE\PDF24\Services\PDF";

            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveOpenDir", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "Handler", "autoSave");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveOverwriteFile", 1);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveShowProgress", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveFilename", "$fileName");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveProfile", "default/medium");

                return;
            }

            throw new InvalidOperationException("Registry key not found: " + registryKey);
        }


        public override void ResetPrinterSettings()
        {
            string deskPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveOpenDir", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveDir", deskPath);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "(Default)", string.Empty);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveOverwriteFile", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveProfile", "default/best");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveUseFileChooser", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveShowProgress", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveUseFileCmd", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "Handler", "assistant");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "LoadInCreatorIfOpen", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "ShellCmd", string.Empty);
        }


        public override void SetPrinterOutput(string filePath)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveDir", filePath);
        }
    }

}
