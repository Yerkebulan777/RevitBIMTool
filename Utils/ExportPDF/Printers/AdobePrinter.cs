using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class AdobePDFPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\Adobe\Acrobat Distiller\Printer";
        public override string PrinterName => "Adobe PDF";


        public override void InitializePrinter()
        {
            Log.Debug("Initialize Adobe PDF printer");

            string autoSave = Path.Combine(RegistryPath, "AutoSave");
            string outputDirKey = Path.Combine(RegistryPath, "OutputDir");
            string promptUserKey = Path.Combine(RegistryPath, "PromptForAdobePDF");

            RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, promptUserKey, "Enabled", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, outputDirKey, "Directory", "<InputFilePath>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");

            PrinterStateManager.SetAvailability(PrinterName, false);
        }


        public override void ResetPrinterSettings()
        {
            Log.Debug("Reset print settings");

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");

            PrinterStateManager.SetAvailability(PrinterName, true);
        }


        public override bool DoPrint(Document doc, SheetModel model)
        {
            string folder = Path.GetDirectoryName(model.TempFilePath).Replace("\\", "\\\\");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", folder);
            return PrintHelper.ExecutePrintAsync(doc, folder, model).Result;
        }
    }



}
