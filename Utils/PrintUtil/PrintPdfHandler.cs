using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using Serilog;
using System.Diagnostics;
using System.IO;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using PaperSize = System.Drawing.Printing.PaperSize;
using PrintRange = Autodesk.Revit.DB.PrintRange;



namespace RevitBIMTool.Utils.PrintUtil;
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


    public static Dictionary<string, List<SheetModel>> GetSheetPrintedData(ref Document doc)
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

                        SheetModel sheetModel = new SheetModel(viewSheet, papeSize, orientType);

                        string formatName = sheetModel.GetFormatNameWithSheetOrientation();

                        if (!sheetPrintData.TryGetValue(formatName, out List<SheetModel> sheetList))
                        {
                            RevitPrinterUtil.SetPrintSettings(doc, sheetModel, formatName);
                            sheetList = [sheetModel];
                        }
                        else
                        {
                            sheetList.Add(sheetModel);
                        }

                        sheetPrintData[formatName] = sheetList;

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
#if R19 || R21
        ParameterValueProvider pvp = new(new ElementId(BuiltInParameter.SHEET_NUMBER));
        FilterStringRule filterRule = new(pvp, new FilterStringEquals(), sheetNumber, false);
#else
        ParameterValueProvider pvp = new(new ElementId(BuiltInParameter.SHEET_NUMBER));
        FilterStringRule filterRule = new(pvp, new FilterStringEquals(), sheetNumber);
#endif

        FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
        collector = collector.WherePasses(new ElementParameterFilter(filterRule));
        return collector.FirstElement();
    }


    public static List<SheetModel> PrintSheetData(ref Document doc, Dictionary<string, List<SheetModel>> sheetDict, string tempDirectory)
    {
        List<SheetModel> resultFilePaths = new(sheetDict.Values.Count);

        List<PrintSetting> printAllSettings = RevitPrinterUtil.GetPrintSettings(doc);

        using (Mutex mutex = new(false, "Global\\{{{ExportPDFMutex}}}"))
        {
            PrintManager printManager = doc.PrintManager;

            foreach (string settingName in sheetDict.Keys)
            {
                PrintSetting printSetting = printAllSettings.FirstOrDefault(set => set.Name == settingName);

                if (printSetting != null && sheetDict.TryGetValue(settingName, out List<SheetModel> sheetModels))
                {
                    using Transaction trx = new(doc, settingName);
                    if (TransactionStatus.Started == trx.Start())
                    {
                        printManager.PrintSetup.CurrentPrintSetting = printSetting;
                        printManager.Apply(); // Set print settings

                        for (int idx = 0; idx < sheetModels.Count; idx++)
                        {
                            SheetModel currentModel = sheetModels[idx];

                            if (mutex.WaitOne(Timeout.Infinite))
                            {
                                try
                                {
                                    string sheetName = currentModel.GetSheetNameWithExtension(doc, "pdf");
                                    string sheetFullPath = Path.Combine(tempDirectory, sheetName);
                                    ViewSheet viewSheet = currentModel.ViewSheet;
                                    printManager.PrintToFileName = sheetFullPath;

                                    if (File.Exists(sheetFullPath))
                                    {
                                        File.Delete(sheetFullPath);
                                    }

                                    if (printManager.SubmitPrint(viewSheet))
                                    {
                                        resultFilePaths.Add(currentModel);
                                        Debug.WriteLine(sheetName);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    throw new Exception(ex.Message);
                                }
                                finally
                                {
                                    mutex.ReleaseMutex();
                                }
                            }
                        }

                        _ = trx.RollBack();
                    }
                }
            }
        }

        return resultFilePaths;
    }



}
