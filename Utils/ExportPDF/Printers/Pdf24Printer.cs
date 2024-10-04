﻿using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;
using RevitBIMTool.Utils.SystemHelpers;

namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class Pdf24Printer : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\PDF24\Services\PDF";
        public override string PrinterName => "PDF24";
        public string StatusPath => @"SOFTWARE\Settings";


        public override void InitializePrinter()
        {
            RegistryHelper.SetValue(Registry.CurrentUser, StatusPath, PrinterName, 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOpenDir", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "Handler", "autoSave");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOverwriteFile", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveShowProgress", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveFilename", "$fileName");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveProfile", "default/medium");
        }


        public override void ResetPrinterSettings()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            //RegistryHelper.SetValue(Registry.CurrentUser, StatusPath, PrinterName, 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOpenDir", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveDir", desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "(Default)", string.Empty);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOverwriteFile", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveProfile", "default/best");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveUseFileChooser", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveShowProgress", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveUseFileCmd", 0);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "Handler", "assistant");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "LoadInCreatorIfOpen", 1);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShellCmd", string.Empty);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveDir", folder);
            return PrintHandler.PrintSheet(doc, folder, model);
        }

        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }

    }

}
