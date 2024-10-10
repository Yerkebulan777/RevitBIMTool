using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using Serilog;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class InternalRevitPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\Autodesk\Revit";
        public override string PrinterName => string.Empty;


        public override void InitializePrinter()
        {

        }


        public override void ResetPrinterSettings()
        {

        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            return PrintHandler.ExportSheet(doc, folder, model);
        }


        public override bool IsAvailable()
        {
            if (RevitBIMToolApp.Version == "2023")
            {
                Log.Debug($"RevitInternalPrinter is available!");

                return true;
            }

            return false;
        }

    }

}
