using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
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
            string autoSave = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string outputDirKey = System.IO.Path.Combine(RegistryPath, "OutputDir");
            string promptUserKey = System.IO.Path.Combine(RegistryPath, "PromptForAdobePDF");

            RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, promptUserKey, "Enabled", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, outputDirKey, "Directory", "<InputFilePath>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");

            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
        }


        public override void ResetPrinterSettings()
        {
            Log.Debug("Reset print settings");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", desktop);
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
        }


        public override bool DoPrint(Document doc, SheetModel model)
        {
            string folder = Path.GetDirectoryName(model.FilePath).Replace("\\", "\\\\");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", folder);
            return PrintHandler.ExecutePrintAsync(doc, folder, model).Result;
        }
    }



}
