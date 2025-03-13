using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using Serilog;
using System.IO;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class InternalPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\Autodesk\Revit";
        public override string PrinterName => "InternalPrinter";

        public int revitVersion;


        public override void InitializePrinter()
        {

        }


        public override void ResetPrinterSettings()
        {

        }


        public override bool DoPrint(Document doc, SheetModel model)
        {
#if R23
            ColorDepthType colorDepthType = model.IsColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;

            string folder = Path.GetDirectoryName(model.FilePath);

            Log.Debug("Export to PDF from internal Revit...");

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
                model.IsSuccessfully = true;
                Thread.Sleep(1000);
                return true;
            }
#endif
            return false;
        }


        public override bool IsAvailable()
        {
            if (int.TryParse(RevitBIMToolApp.Version, out revitVersion))
            {
                Log.Debug($"Revit version: {revitVersion}");
                return revitVersion >= 2023;
            }

            return false;
        }

    }



}
