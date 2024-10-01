using Autodesk.Revit.DB;
using System.IO;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal sealed class InternalPrinter
    {
        public bool ExportSheet(Document doc, ViewSheet viewSheet, string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string directory = Path.GetDirectoryName(filePath);

            PDFExportOptions option = new PDFExportOptions()
            {
                ExportQuality = PDFExportQualityType.DPI300,
                RasterQuality = RasterQualityType.Medium,
                ColorDepth = ColorDepthType.Color,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100,
                FileName = fileName,
            };

            IList<ElementId> viewIds = [viewSheet.Id];

            if (doc.Export(directory, viewIds, option))
            {
                Thread.Sleep(100);
                return true;
            }

            return false;
        }

    }
}
