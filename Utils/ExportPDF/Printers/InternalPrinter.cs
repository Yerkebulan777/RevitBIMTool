using Autodesk.Revit.DB;
using RevitBIMTool.Model;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal sealed class InternalPrinter : PrinterControl
    {
        public override string Name => string.Empty;

        public override int OverallRating => 5;


        public override void InitializePrinter()
        {
            throw new NotImplementedException();
        }

        public override bool PrintSheet(Document doc, string folder, SheetModel model)
        {
            throw new NotImplementedException();
        }

        public override void ResetPrinterSettings()
        {
            throw new NotImplementedException();
        }

        public override void SetPrinterOutput(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
