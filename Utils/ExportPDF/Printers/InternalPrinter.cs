﻿using Autodesk.Revit.DB;
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


    public override bool DoPrint(Document doc, SheetModel model)
    {
        return DoPrintAsync(doc, model).Result;
    }

    public async Task<bool> DoPrintAsync(Document doc, SheetModel model)
    {
#if R23
        ColorDepthType colorDepthType = model.IsColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;

        Log.Debug("Exporting to PDF using the internal printer, destination: {TempFilePath}", model.TempFilePath);

        string sheetName = Path.GetFileNameWithoutExtension(model.SheetName);

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
            Log.Debug("Exported to PDF: {SheetName}", model.SheetName);

            if (await PathHelper.AwaitExistsFileAsync(model.TempFilePath))
            {
                Log.Debug("Export to PDF: {SheetName}", model.SheetName);
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
        throw new NotImplementedException();
    }

    public override void ResetPrinterSettings()
    {
        throw new NotImplementedException();
    }
}
