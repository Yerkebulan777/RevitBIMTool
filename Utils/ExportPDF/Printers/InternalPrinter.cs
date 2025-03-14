using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class InternalPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Autodesk\Revit";
    public override string PrinterName => "InternalPrinter";

    public int revitVersion;

    public override void InitializePrinter()
    {
        Log.Debug("Initialize Internal printer");
    }

    public override void ResetPrinterSettings()
    {
        Log.Debug("Reset print settings");
    }

    public override bool DoPrint(Document doc, SheetModel model)
    {
        return DoPrintAsync( doc, model).Result;
    }


    public async Task<bool> DoPrintAsync(Document doc, SheetModel model)
    {
#if R23
        ColorDepthType colorDepthType = model.IsColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;

        Log.Debug($"Exporting to PDF using the internal printer, destination: {model.TempFilePath}");

        string sheetName = Path.GetFileNameWithoutExtension(model.SheetName);

        string folderPath = Path.GetDirectoryName(model.TempFilePath);

        PDFExportOptions options = new()
        {
            Combine = false,
            StopOnError = true,
            HideScopeBoxes = true,
            HideCropBoundaries = true,
            HideReferencePlane = true,
            PaperFormat = ExportPaperFormat.Default,
            RasterQuality = RasterQualityType.Medium,
            ExportQuality = PDFExportQualityType.DPI300,
            ColorDepth = colorDepthType,
            ZoomType = ZoomType.Zoom,
            FileName = sheetName,
            ZoomPercentage = 100,
        };

        IList<ElementId> viewIds = new List<ElementId> { model.ViewSheet.Id };

        if (doc.Export(folderPath, viewIds, options))
        {
            string filePath = Path.Combine(folderPath, $"{model.SheetName}.pdf");

            if (await PathHelper.AwaitExistsFileAsync(model.TempFilePath))
            {
                Log.Debug($"Exported to PDF successfully, destination: {model.TempFilePath}");
                model.IsSuccessfully = true;
                Thread.Sleep(100);
                return true;
            }
        }
#endif
        return false;
    }


    public override bool IsAvailable()
    {
        Log.Debug($"Revit version: {RevitBIMToolApp.Version}");

        return int.TryParse(RevitBIMToolApp.Version, out revitVersion) && revitVersion >= 2023;
    }
}
