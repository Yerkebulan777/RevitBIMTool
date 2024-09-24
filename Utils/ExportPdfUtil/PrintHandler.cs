using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.PrintUtil;
using Serilog;
using System.IO;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using PaperSize = System.Drawing.Printing.PaperSize;
using PrintRange = Autodesk.Revit.DB.PrintRange;


namespace RevitBIMTool.Utils.ExportPdfUtil;
internal static class PrintHandler
{
    private static string printerName;


    private static readonly object syncLocker = new();


    public static void ResetPrintSettings(Document doc, string printerName)
    {
        PrintHandler.printerName = printerName;
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
            }
            catch (Exception ex)
            {
                _ = trx.RollBack();
                Log.Error($"Reset settings: {ex.Message}", ex);
                throw new Exception($"Reset settings: {ex.Message}", ex);
            }
            finally
            {
                printManager.Apply();
                if (!trx.HasEnded())
                {
                    _ = trx.Commit();
                }
            }
        }
    }


    public static Dictionary<string, List<SheetModel>> GetSheetData(Document doc, string revitFileName, ColorDepthType colorType)
    {
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


    public static List<SheetModel> PrintSheetData(Document doc, Dictionary<string, List<SheetModel>> sheetDict, string tempFolder)
    {
        List<PrintSetting> printAllSettings = RevitPrinterUtil.GetPrintSettings(doc);

        List<SheetModel> resultFilePaths = new(sheetDict.Values.Count);

        using Mutex mutex = new(false, $"Global\\{{{printerName}}}");

        using Transaction trx = new(doc, "PrintToPDF");

        if (mutex.WaitOne(Timeout.InfiniteTimeSpan))
        {
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
                    mutex.ReleaseMutex();

                    if (!trx.HasEnded())
                    {
                        _ = trx.RollBack();
                    }
                }
            }
        }

        return resultFilePaths;
    }


    private static bool ExportSheet(Document doc, string tempFolder, SheetModel model)
    {
        lock (syncLocker)
        {
            PrintManager printManager = doc.PrintManager;

            string tempPath = Path.Combine(tempFolder, model.SheetName);

            RegistryHelper.ActivateSettingsForPDFCreator(tempFolder);

            RevitPathHelper.DeleteExistsFile(tempPath);

            printManager.PrintToFileName = tempPath;

            if (printManager.SubmitPrint(model.ViewSheet))
            {
                if (RevitPathHelper.AwaitExistsFile(tempPath))
                {
                    Log.Debug($"Printed: {model.SheetName}");
                    model.SheetTempPath = tempPath;
                    return true;
                }
            }
        }

        return false;
    }


}
