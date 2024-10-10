using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal class MicrosoftPrinter : PrinterControl
    {
        public override string RegistryPath => @"Printers\Settings";
        public override string PrinterName => "Microsoft Print to PDF";


        public override void InitializePrinter()
        {
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 1);
        }


        public override void ResetPrinterSettings()
        {
            _ = RegistryHelper.SetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName, 0);
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            return PrintHandler.PrintSheet(doc, folder, model);
        }


    }

}
