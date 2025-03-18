using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class InternalPrinter : PrinterControl
{
    public override string RegistryPath => "Undefined registry path";
    public override string PrinterName => "Internal Printer";
    public override string RevitFilePath { get; set; }
    public override bool IsInternal => true;

    public override bool DoPrint(Document doc, SheetModel model)
    {
#if R23
        ColorDepthType colorDepthType = model.IsColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;

        Log.Debug("Exporting to PDF, destination: {TempFilePath}", model.TempFilePath);

        string sheetName = Path.GetFileNameWithoutExtension(model.TempFilePath);
        string folderPath = Path.GetDirectoryName(model.TempFilePath);

        PDFExportOptions options = new()
        {
            Combine = false,
            StopOnError = true,
            FileName = sheetName,
            HideScopeBoxes = true,
            HideCropBoundaries = true,
            HideReferencePlane = true,
            ColorDepth = colorDepthType,
            PaperFormat = ExportPaperFormat.Default,
            RasterQuality = RasterQualityType.Medium,
            ExportQuality = PDFExportQualityType.DPI300,
            ZoomType = ZoomType.Zoom,
            ZoomPercentage = 100,
        };

        IList<ElementId> viewIds = [model.ViewSheet.Id];

        if (doc.Export(folderPath, viewIds, options))
        {
            Log.Debug("Printed {SheetName}.", sheetName);

            if (PathHelper.AwaitExistsFile($"{model.TempFilePath}.pdf"))
            {
                model.IsSuccessfully = true;
                Thread.Sleep(100);
                return true;
            }
        }
#endif
        return false;
    }


    public override void InitializePrinter()
    {
        /// No need to initialize the internal printer
    }

    public override void ResetPrinterSettings()
    {
        /// No need to reset the internal printer settings
    }
}
