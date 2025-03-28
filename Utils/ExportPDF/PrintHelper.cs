﻿using Autodesk.Revit.DB;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.IO;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using PaperSize = System.Drawing.Printing.PaperSize;
using SheetModel = RevitBIMTool.Models.SheetModel;

namespace RevitBIMTool.Utils.ExportPDF;

internal static class PrintHelper
{

    /// <summary>
    /// Получает и группирует данные листов для последующей печати
    /// </summary>
    public static List<SheetFormatGroup> GetData(Document doc, PrinterControl printer, bool сolorEnabled)
    {
        BuiltInCategory bic = BuiltInCategory.OST_TitleBlocks;
        string revitFileName = Path.GetFileNameWithoutExtension(printer.RevitFilePath);
        FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
        collector = collector.OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType();

        Dictionary<string, SheetFormatGroup> formatGroups = new(StringComparer.OrdinalIgnoreCase);

        foreach (FamilyInstance titleBlock in collector.Cast<FamilyInstance>())
        {
            double widthInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH).AsDouble());
            double heightInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT).AsDouble());

            PageOrientationType orientation = PrintSettingsManager.GetOrientation(widthInMm, heightInMm);

            string sheetNumber = titleBlock.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();

            Element sheetInstance = GetViewSheetByNumber(doc, sheetNumber);

            if (sheetInstance is ViewSheet viewSheet && viewSheet.CanBePrinted)
            {
                SheetModel model = null;
                PaperSize paperSize = null;
                string formatName = string.Empty;

                if (printer.IsInternalPrinter)
                {
                    model = new(viewSheet)
                    {
                        IsColorEnabled = сolorEnabled
                    };
                    model.SetSheetName(doc, revitFileName, "pdf");
                }
                else if (PrinterApiUtility.GetOrCreatePaperSize(printer.PrinterName, widthInMm, heightInMm, out paperSize))
                {
                    model = new(viewSheet, paperSize, orientation);
                    model.SetSheetName(doc, revitFileName, "pdf");
                    model.IsColorEnabled = сolorEnabled;
                    formatName = model.GetFormatName();
                }

                if (model is not null && model.IsValid)
                {
                    if (!formatGroups.TryGetValue(formatName, out SheetFormatGroup group))
                    {
                        group = new SheetFormatGroup
                        {
                            PaperSize = paperSize,
                            FormatName = formatName,
                            Orientation = orientation,
                            IsColorEnabled = сolorEnabled
                        };

                        formatGroups[formatName] = group;
                    }

                    group.SheetList.Add(model);

                }

            }
        }

        List<SheetFormatGroup> result = [.. formatGroups.Values];
        Log.Information("Found {0} sheet format groups", result.Count);

        return result;
    }

    /// <summary>
    /// Выполняет печать листов по группам форматов
    /// </summary>
    public static List<SheetModel> PrintSheetData(Document doc, PrinterControl printer, List<SheetFormatGroup> formatGroups, string folder)
    {
        List<SheetModel> successfulSheets = [];

        List<string> existingFiles = [.. Directory.GetFiles(folder)];

        using Transaction trx = new(doc, "ExportToPDF");

        if (TransactionStatus.Started == trx.Start())
        {
            try
            {
                foreach (SheetFormatGroup group in formatGroups)
                {
                    foreach (SheetModel model in group.SheetList)
                    {
                        Log.Verbose("Printing sheet {0}", model.SheetName);

                        if (PrintSingleSheet(doc, printer, model, folder, existingFiles))
                        {
                            successfulSheets.Add(model);
                        }
                    }
                }

                _ = trx.Commit();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in print process: {Message}", ex.Message);

                if (!trx.HasEnded())
                {
                    _ = trx.RollBack();
                }
            }
            finally
            {
                printer?.ReleasePrinterSettings();
            }
        }

        return successfulSheets;
    }

    /// <summary>
    /// Создает и применяет настройку печати для группы форматов
    /// </summary>
    public static bool SetupPrintSetting(Document doc, string formatName, PageOrientationType orientation, bool colorEnabled)
    {
        Log.Information("Setting up format: {FormatName}", formatName);

        PrintSettingsManager.SetPrintSettings(doc, formatName, orientation, colorEnabled);

        return PrintSettingsManager.GetPrintSettingByName(doc, formatName) != null;

        throw new InvalidOperationException($"Failed to create print setting: {formatName}");
    }

    /// <summary>
    /// Печатает один лист и проверяет результат
    /// </summary>
    private static bool PrintSingleSheet(Document doc, PrinterControl printer, SheetModel model, string folder, List<string> existingFiles)
    {
        string filePath = Path.Combine(folder, model.SheetName);
        string revitFilePath = printer.RevitFilePath;

        try
        {
            // Проверяем, есть ли уже файл
            if (FileValidator.IsFileNewer(filePath, revitFilePath))
            {
                Log.Debug("Sheet already exists: {SheetName}", model.SheetName);
                model.IsSuccessfully = true;
                model.TempFilePath = filePath;
                return true;
            }

            // Печатаем лист
            if (printer.DoPrint(doc, model, folder) && FileValidator.VerifyFile(ref existingFiles, filePath))
            {
                Log.Information("Successfully printed: {SheetName}", model.SheetName);
                model.IsSuccessfully = true;
                model.TempFilePath = filePath;
                return true;
            }

            Log.Warning("Failed to print sheet: {SheetName}", model.SheetName);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error printing sheet {SheetName}: {Message}", model.SheetName, ex.Message);
            return false;
        }
    }

    // Вспомогательный метод для получения ViewSheet по номеру
    private static Element GetViewSheetByNumber(Document document, string sheetNumber)
    {
        ParameterValueProvider pvp = new(new ElementId(BuiltInParameter.SHEET_NUMBER));

#if R19 || R21
        FilterStringRule filterRule = new(pvp, new FilterStringEquals(), sheetNumber, false);
#else
        FilterStringRule filterRule = new(pvp, new FilterStringEquals(), sheetNumber);
#endif

        FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
        collector = collector.WherePasses(new ElementParameterFilter(filterRule));

        return collector.FirstElement();
    }


    public static bool ExportSheet(Document doc, SheetModel model, string folder)
    {
#if R23
        ColorDepthType colorType = model.IsColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;
        PDFExportOptions options = new()
        {
            Combine = false,
            StopOnError = true,
            HideScopeBoxes = true,
            ColorDepth = colorType,
            HideCropBoundaries = true,
            HideReferencePlane = true,
            FileName = model.SheetName,
            PaperFormat = ExportPaperFormat.Default,
            RasterQuality = RasterQualityType.Medium,
            ExportQuality = PDFExportQualityType.DPI300,
            ZoomType = ZoomType.Zoom,
            ZoomPercentage = 100,
        };

        Log.Debug("Exporting sheet {Sheet}", model.SheetName);
        IList<ElementId> viewIds = [model.ViewSheet.Id];
        return doc.Export(folder, viewIds, options);
#else
        return false;
#endif
    }


    public static bool ExecutePrint(Document doc, SheetModel model, string folder)
    {
        string filePath = Path.Combine(folder, model.SheetName);

        PrintManager printManager = doc.PrintManager;

        printManager.PrintToFileName = filePath;

        PathHelper.DeleteExistsFile(filePath);

        return printManager.SubmitPrint(model.ViewSheet);
    }


}
