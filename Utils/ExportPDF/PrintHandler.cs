using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using Serilog;
using System.IO;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using PaperSize = System.Drawing.Printing.PaperSize;
using PrintRange = Autodesk.Revit.DB.PrintRange;


namespace RevitBIMTool.Utils.ExportPDF;
internal static class PrintHandler
{
    public static void ResetPrintSettings(Document doc, string printerName)
    {
        PrintManager printManager = doc.PrintManager;

        PrinterApiUtility.ResetDefaultPrinter(printerName);

        List<PrintSetting> printSettings = RevitPrinterUtil.GetPrintSettings(doc);

        using Transaction trx = new(doc, "ResetPrintSetting");

        if (TransactionStatus.Started == trx.Start())
        {
            try
            {
                printManager.SelectNewPrintDriver(printerName);
                printSettings.ForEach(set => doc.Delete(set.Id));
                printManager.PrintRange = PrintRange.Current;
                printManager.PrintToFile = true;
                printManager.Apply();
            }
            catch (Exception ex)
            {
                _ = trx.RollBack();
                Log.Error(ex, $"Reset settings: {ex.Message}");
                throw new Exception($"Reset settings: {ex.Message}");
            }
            finally
            {
                if (!trx.HasEnded())
                {
                    _ = trx.Commit();
                }
            }
        }
    }


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


    public static Dictionary<string, List<SheetModel>> GetSheetData(Document doc, string printerName, string revitFileName, string sectionName)
    {
        ColorDepthType colorType = sectionName switch
        {
            "KJ" or "KR" or "KG" => ColorDepthType.BlackLine,
            _ => ColorDepthType.Color,
        };

        FilteredElementCollector collector = new(doc);

        collector = collector.OfCategory(BuiltInCategory.OST_TitleBlocks);
        collector = collector.OfClass(typeof(FamilyInstance));
        collector = collector.WhereElementIsNotElementType();

        int sheetCount = collector.GetElementCount();

        Dictionary<string, List<SheetModel>> sheetPrintData = new(sheetCount);

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
                if (!PrinterApiUtility.GetPaperSize(widthInMm, heighInMm, out _))
                {
                    PrinterApiUtility.AddFormat(printerName, widthInMm, heighInMm);
                }

                if (PrinterApiUtility.GetPaperSize(widthInMm, heighInMm, out PaperSize papeSize))
                {
                    PageOrientationType orientType = RevitPrinterUtil.GetOrientation(widthInMm, heighInMm);

                    SheetModel model = new(viewSheet, papeSize, orientType);

                    model.SetSheetName(doc, revitFileName, "pdf");

                    if (model.IsValid)
                    {
                        string formatName = model.GetFormatNameWithSheetOrientation();

                        if (!sheetPrintData.TryGetValue(formatName, out List<SheetModel> sheetList))
                        {
                            RevitPrinterUtil.SetPrintSettings(doc, model, formatName, colorType);
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


    public static List<SheetModel> PrintSheetData(Document doc, Dictionary<string, List<SheetModel>> sheetDict, string tempFolder)
    {
        List<PrintSetting> printAllSettings = RevitPrinterUtil.GetPrintSettings(doc);

        List<SheetModel> resultFilePaths = new(sheetDict.Values.Count);

        using Transaction trx = new(doc, "PrintToPDF");

        if (TransactionStatus.Started == trx.Start())
        {
            try
            {
                foreach (string settingName in sheetDict.Keys)
                {
                    PrintManager printManager = doc.PrintManager;

                    PrintSetting printSetting = printAllSettings.FirstOrDefault(set => set.Name == settingName);

                    if (printSetting != null && sheetDict.TryGetValue(settingName, out List<SheetModel> sheetModels))
                    {
                        printManager.PrintSetup.CurrentPrintSetting = printSetting;

                        printManager.Apply(); // Set print settings

                        for (int idx = 0; idx < sheetModels.Count; idx++)
                        {
                            SheetModel model = sheetModels[idx];

                            if (ExportSheet(doc, tempFolder, model))
                            {
                                resultFilePaths.Add(model);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
            finally
            {
                if (!trx.HasEnded())
                {
                    _ = trx.RollBack();
                }
            }
        }

        return resultFilePaths;
    }


    public static bool ExportSheet(Document doc, string folder, SheetModel model)
    {
#if R23
        PDFExportOptions option = new()
        {
            FileName = model.SheetName,
            ExportQuality = PDFExportQualityType.DPI300,
            RasterQuality = RasterQualityType.Medium,
            ColorDepth = ColorDepthType.Color,
            ZoomType = ZoomType.Zoom,
            ZoomPercentage = 100,
        };

        IList<ElementId> viewIds = [model.ViewSheet.Id];

        if (doc.Export(folder, viewIds, option))
        {
            Thread.Sleep(100);
            return true;
        }
#endif
        return false;
    }


    public static bool PrintSheet(Document doc, string folder, SheetModel model)
    {
        string filePath = Path.Combine(folder, model.SheetName);

        PrintManager printManager = doc.PrintManager;

        printManager.PrintToFileName = filePath;

        RevitPathHelper.DeleteExistsFile(filePath);

        if (printManager.SubmitPrint(model.ViewSheet))
        {
            if (RevitPathHelper.AwaitExistsFile(filePath))
            {
                model.SheetTempPath = filePath;
                return true;
            }
        }

        return false;
    }

    internal static string GetPrinter()
    {
        throw new NotImplementedException();
    }
}
