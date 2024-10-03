using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class InternalPrinter : PrinterControl
    {
        public override string Name => "Microsoft Print to PDF";

        public override int OverallRating => 5;


        public override void InitializePrinter()
        {
        }


        public override void ResetPrinterSettings()
        {
        }


        public override void SetPrinterOutput(string filePath)
        {
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
#if R23
            return PrintHandler.ExportSheet(doc, folder, model);
#else
            return PrintHandler.PrintSheet(doc, folder, model);
#endif
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }
    }

}
