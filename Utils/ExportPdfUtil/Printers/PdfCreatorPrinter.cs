using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil;
using System.IO;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal sealed class PdfCreatorPrinter : PrinterControl
    {
        public readonly string registryKey = @"SOFTWARE\pdfforge\PDFCreator\Settings\ConversionProfiles\0";
        public override string Name => "PDFCreator";


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                string autoSaveKey = Path.Combine(registryKey, "AutoSave");
                string openViewerKey = Path.Combine(registryKey, "OpenViewer");
                RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "Enabled", "False");
                RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "OpenWithPdfArchitect", "False");
                RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", "<InputFilePath>");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<InputFilename>");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "ShowAllNotifications", "False");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "CompressionLevel", "medium");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "SkipPrintDialog", "True");
                RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "ShowProgress", "False");

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
        }


        public override void ResetPrinterSettings()
        {
            string autoSaveKey = Path.Combine(registryKey, "AutoSave");
            string openViewerKey = Path.Combine(registryKey, "OpenViewer");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "Enabled", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "Enabled", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, openViewerKey, "OpenWithPdfArchitect", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, autoSaveKey, "TargetDirectory", string.Empty);
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "FileNameTemplate", "<Title>");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "ShowAllNotifications", "True");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "CompressionLevel", "high");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "SkipPrintDialog", "False");
            RegistryHelper.SetValue(Registry.CurrentUser, registryKey, "ShowProgress", "True");
        }


        public override void SetPrinterOutput(string filePath)
        {
            RegistryHelper.SetValue(Registry.CurrentUser, Path.Combine(registryKey, "AutoSave"), "TargetDirectory", filePath);
            Thread.Sleep(100);
        }


        public override bool PrintSheet(Document doc, string folder, SheetModel model)
        {
            throw new NotImplementedException();
        }

    }

}
