using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class Pdf24Printer : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\PDF24\Services\PDF";
        public override string PrinterName => "PDF24";


        public override void InitializePrinter()
        {
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOpenDir", 0);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "Handler", "autoSave");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveShowProgress", 0);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOverwriteFile", 1);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveUseFileChooser", 0);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveFilename", "$fileName");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveProfile", "default/medium");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
        }


        public override void ResetPrinterSettings()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveDir", desktop);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "Handler", "assistant");
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveUseFileCmd", 0);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShellCmd", string.Empty);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "(Default)", string.Empty);
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            _ = RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveDir", folder);
            return PrintHandler.PrintSheet(doc, folder, model);
        }

    }

}
