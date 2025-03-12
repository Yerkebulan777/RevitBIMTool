using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using Serilog;
using System.IO;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class InternalPrinter : PrinterControl
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
#if R23
            ColorDepthType colorDepthType = model.IsColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;

            Log.Debug("Экспорт в PDF с встроенного механизма Revit...");

            PDFExportOptions options = new()
            {
                FileName = model.SheetName,
                RasterQuality = RasterQualityType.Medium,
                ExportQuality = PDFExportQualityType.DPI300,
                ColorDepth = colorDepthType,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100,
            };

            IList<ElementId> viewIds = [model.ViewSheet.Id];

            if (doc.Export(folder, viewIds, options))
            {
                model.SheetPath = Path.Combine(folder, model.SheetName);
                return true;
            }
#endif
            return false;
        }


        public override bool IsAvailable()
        {
            return int.TryParse(RevitBIMToolApp.Version, out int version) && version >= 2023;
        }

    }

}
