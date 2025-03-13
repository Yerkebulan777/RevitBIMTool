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

            Log.Debug($"Exporting to PDF using the internal printer, destination: {model.FilePath}");

            string folderPath = Path.GetDirectoryName(model.FilePath);

            PDFExportOptions options = new()
            {
                Combine = false,
                StopOnError = true,
                HideScopeBoxes = true,
                HideCropBoundaries = true,
                HideReferencePlane = true,
                FileName = model.SheetName,
                PaperFormat = ExportPaperFormat.Default,
                RasterQuality = RasterQualityType.Medium,
                ExportQuality = PDFExportQualityType.DPI300,
                ColorDepth = colorDepthType,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100,
            };

            IList<ElementId> viewIds = [model.ViewSheet.Id];

            if (doc.Export(folderPath, viewIds, options))
            {
                model.IsSuccessfully = true;
                Thread.Sleep(100);
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
