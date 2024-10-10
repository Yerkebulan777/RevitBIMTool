using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class InternalRevitPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\Autodesk\Revit";
        public override string PrinterName => string.Empty;


        public override void InitializePrinter()
        {
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, "RevitInternalPrinter", 1);
        }


        public override void ResetPrinterSettings()
        {
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, "RevitInternalPrinter", 0);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            return PrintHandler.ExportSheet(doc, folder, model);
        }


        public override bool IsAvailable()
        {
            return RevitBIMToolApp.Version == "2023";
        }

    }

}
