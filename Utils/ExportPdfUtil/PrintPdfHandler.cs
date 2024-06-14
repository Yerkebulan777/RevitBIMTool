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
internal static class PrintPdfHandler
{


    private static string defaultPrinterName;
    private static readonly object syncLocker = new();


    public static void ResetPrintSettings(Document doc, string printerName)
    {
        defaultPrinterName = printerName;
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
                printManager.PrintRange = PrintRange.Visible;
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


    public static Dictionary<string, List<SheetModel>> GetSheetPrintedData(Document doc, string revitFileName)
    {
        FilteredElementCollector collector = new(doc);
        collector = collector.OfCategory(BuiltInCategory.OST_TitleBlocks);
        collector = collector.OfClass(typeof(FamilyInstance));
        collector = collector.WhereElementIsNotElementType();

        int sheetCount = collector.GetElementCount();

        Log.Information($"Found {sheetCount} sheets");

        Dictionary<string, List<SheetModel>> sheetPrintData = new(sheetCount);

        foreach (FamilyInstance titleBlock in collector.Cast<FamilyInstance>())
        {
            lock (syncLocker)
            {
                double sheetWidth = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH).AsDouble();
                double sheetHeigh = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT).AsDouble();
                string sheetNumber = titleBlock.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();

                double widthInMm = UnitManager.FootToMm(sheetWidth);
                double heighInMm = UnitManager.FootToMm(sheetHeigh);

                Element sheetElem = GetViewSheetByNumber(ref doc, sheetNumber);

                if (sheetElem is ViewSheet viewSheet && viewSheet.CanBePrinted)
                {
                    if (!PrinterApiUtility.GetPaperSize(widthInMm, heighInMm, out _))
                    {
                        PrinterApiUtility.AddFormat(defaultPrinterName, widthInMm, heighInMm);
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
                                RevitPrinterUtil.SetPrintSettings(doc, model, formatName);
                                sheetList = [model];
                            }
                            else
                            {
                                sheetList.Add(model);
                            }

                            sheetPrintData[formatName] = sheetList;
                        }
                    }
                    else
                    {
                        throw new Exception($"Not defined: " + viewSheet.Name);
                    }

                }
            }
        }

        return sheetPrintData;
    }


    private static Element GetViewSheetByNumber(ref Document document, string sheetNumber)
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


    public static List<SheetModel> PrintSheetData(ref Document doc, Dictionary<string, List<SheetModel>> sheetDict, string tempDirectory)
    {
        List<PrintSetting> printAllSettings = RevitPrinterUtil.GetPrintSettings(doc);

        List<SheetModel> resultFilePaths = new(sheetDict.Values.Count);

        using Mutex mutex = new(false, "Global\\{{{ExportToPDF}}}");

        RevitPathHelper.EnsureDirectory(tempDirectory);

        using Transaction trx = new(doc, "ExportToPDF");

        if (TransactionStatus.Started == trx.Start())
        {
            if (mutex.WaitOne(Timeout.Infinite))
            {
                try
                {
                    PrintManager printManager = doc.PrintManager;

                    foreach (string settingName in sheetDict.Keys)
                    {
                        PrintSetting printSetting = printAllSettings.FirstOrDefault(set => set.Name == settingName);

                        if (printSetting != null && sheetDict.TryGetValue(settingName, out List<SheetModel> sheetModels))
                        {
                            printManager.PrintSetup.CurrentPrintSetting = printSetting;

                            printManager.Apply(); // Set print settings

                            for (int idx = 0; idx < sheetModels.Count; idx++)
                            {
                                SheetModel model = sheetModels[idx];

                                string sheetFullName = model.SheetName;

                                Log.Verbose($"StaRT Print: {sheetFullName}");

                                string sheetTempPath = Path.Combine(tempDirectory, sheetFullName);

                                printManager.PrintToFileName = sheetTempPath;

                                RevitPathHelper.DeleteExistsFile(sheetTempPath);

                                if (printManager.SubmitPrint(model.ViewSheet))
                                {
                                    resultFilePaths.Add(model);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = trx.RollBack();
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    mutex.ReleaseMutex();
                    if (!trx.HasEnded())
                    {
                        _ = trx.Commit();
                    }
                }
            }
        }

        return resultFilePaths;
    }


}
