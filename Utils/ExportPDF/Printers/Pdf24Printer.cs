using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.SystemHelpers;

namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal sealed class Pdf24Printer : PrinterControl
    {
        private readonly string registryKey = @"SOFTWARE\PDF24\Services\PDF";
        public override string Name => "PDF24";
        public override int OverallRating => 1;


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(RegistryHive.CurrentUser, registryKey))
            {
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveOpenDir", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "Handler", "autoSave");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveOverwriteFile", 1);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveShowProgress", 0);
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveFilename", "$fileName");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "AutoSaveProfile", "default/medium");

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
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
