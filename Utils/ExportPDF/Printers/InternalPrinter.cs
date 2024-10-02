using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class InternalPrinter : PrinterControl
    {
        public override string Name => string.Empty;

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
            return PrintHandler.ExportSheet(doc, folder, model);
        }
    }
}
