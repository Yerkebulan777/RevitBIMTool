using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class ClawPdfPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\clawSoft\clawPDF\Settings\ConversionProfiles\0";
        public override string PrinterName => "clawPDF";


        public override void InitializePrinter()
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", "<InputFilePath>");
            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
        }


        public override void ResetPrinterSettings()
        {
            Log.Debug("Reset print settings");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OpenViewer", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowProgress", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
        }


        public override bool DoPrint(Document doc, SheetModel model)
        {
            string autoSaveKey = System.IO.Path.Combine(RegistryPath, "AutoSave");
            string folder = Path.GetDirectoryName(model.FilePath).Replace("\\", "\\\\");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", folder);
            return PrintHandler.ExecutePrintAsync(doc, folder, model).Result;
        }

    }



}
