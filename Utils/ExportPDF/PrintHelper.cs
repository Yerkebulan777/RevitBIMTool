using Autodesk.Revit.DB;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.Diagnostics;
using System.IO;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using PaperSize = System.Drawing.Printing.PaperSize;

using SheetModel = RevitBIMTool.Models.SheetModel;

namespace RevitBIMTool.Utils.ExportPDF;

internal static class PrintHelper
{
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


    public static Dictionary<string, List<SheetModel>> GetData(Document doc, PrinterControl printer)
    {
        FilteredElementCollector collector = new(doc);
        collector = collector.OfCategory(BuiltInCategory.OST_TitleBlocks);
        collector = collector.OfClass(typeof(FamilyInstance));
        collector = collector.WhereElementIsNotElementType();

        Dictionary<string, List<SheetModel>> sheetPrintData = new(collector.GetElementCount());

        string revitFileName = Path.GetFileNameWithoutExtension(printer.RevitFilePath);

        foreach (FamilyInstance titleBlock in collector.Cast<FamilyInstance>())
        {
            double sheetWidth = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH).AsDouble();
            double sheetHeigh = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT).AsDouble();
            string sheetNumber = titleBlock.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();

            double widthInMm = UnitManager.FootToMm(sheetWidth);
            double heighInMm = UnitManager.FootToMm(sheetHeigh);

            Element sheetElem = GetViewSheetByNumber(doc, sheetNumber);

            if (sheetElem is ViewSheet viewSheet && viewSheet.CanBePrinted)
            {
                if (!PrinterApiUtility.GetPaperSize(widthInMm, heighInMm, out _) || !printer.IsInternal)
                {
                    Log.Debug(PrinterApiUtility.AddFormat(printer.PrinterName, widthInMm, heighInMm));
                }

                if (PrinterApiUtility.GetPaperSize(widthInMm, heighInMm, out PaperSize papeSize))
                {
                    PageOrientationType orientType = PrintSettingsManager.GetOrientation(widthInMm, heighInMm);

                    SheetModel model = new(viewSheet, papeSize, orientType);

                    model.SetSheetName(doc, revitFileName, "pdf");

                    if (model.IsValid)
                    {
                        model.IsColorEnabled = printer.IsColorEnabled;

                        string formatName = model.GetFormatNameWithSheetOrientation();

                        if (!sheetPrintData.TryGetValue(formatName, out List<SheetModel> sheetList))
                        {
                            sheetList = [model];
                        }
                        else
                        {
                            sheetList.Add(model);
                        }

                        sheetPrintData[formatName] = sheetList;
                    }

                }

            }

        }

        return sheetPrintData;
    }


    public static List<SheetModel> PrintSheetData(Document doc, PrinterControl printer, Dictionary<string, List<SheetModel>> sheetData, string folder)
    {
        List<PrintSetting> printAllSettings = PrintSettingsManager.CollectPrintSettings(doc);

        List<SheetModel> successfulSheetModels = new(sheetData.Values.Count);

        List<string> existingFiles = [.. Directory.GetFiles(folder)];

        using Transaction trx = new(doc, "ExportToPDF");

        string revitFilePath = printer.RevitFilePath;

        if (TransactionStatus.Started == trx.Start())
        {
            try
            {
                foreach (string settingName in sheetData.Keys)
                {
                    PrintManager printManager = doc.PrintManager;

                    PrintSetting printSetting = printAllSettings.FirstOrDefault(set => set.Name == settingName);

                    if (printSetting != null && sheetData.TryGetValue(settingName, out List<SheetModel> sheetModels))
                    {
                        printManager.PrintSetup.CurrentPrintSetting = printSetting;

                        printManager.Apply(); // Set print settings

                        for (int idx = 0; idx < sheetModels.Count; idx++)
                        {
                            SheetModel model = sheetModels[idx];

                            string filePath = Path.Combine(folder, model.SheetName);

                            bool isPrinted = FileValidator.IsFileNewer(filePath, revitFilePath);

                            if (isPrinted || printer.DoPrint(doc, model, folder))
                            {
                                Debug.WriteLine("Exported {SheetName}", model.SheetName);

                                if (FileValidator.VerifyFile(ref existingFiles, filePath))
                                {
                                    Log.Debug("File exist!");
                                    model.IsSuccessfully = true;
                                    model.TempFilePath = filePath;
                                    successfulSheetModels.Add(model);
                                }
                            }
                        }
                    }
                }

                _ = trx.Commit();
            }
            catch (Exception ex)
            {
                if (!trx.HasEnded())
                {
                    _ = trx.RollBack();
                    Log.Error(ex, ex.Message);
                }
            }
            finally
            {
                printer?.ReleasePrinterSettings();
            }
        }

        return successfulSheetModels;
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
