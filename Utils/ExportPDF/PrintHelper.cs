using Autodesk.Revit.DB;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;
using System.IO;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using PaperSize = System.Drawing.Printing.PaperSize;
using PrintRange = Autodesk.Revit.DB.PrintRange;
using SheetModel = RevitBIMTool.Models.SheetModel;

namespace RevitBIMTool.Utils.ExportPDF;

internal static class PrintHelper
{
    private static void SetupPrinterSettings(Document doc, string printerName)
    {
        PrintManager printManager = doc.PrintManager;

        List<PrintSetting> printSettings = RevitPrinterUtil.GetPrintSettings(doc);

        using Transaction trx = new(doc, "ResetPrintSetting");

        if (TransactionStatus.Started == trx.Start())
        {
            try
            {
                if (!string.IsNullOrEmpty(printerName))
                {
                    printManager.SelectNewPrintDriver(printerName);
                }

                printSettings.ForEach(set => doc.Delete(set.Id));
                printManager.PrintRange = PrintRange.Current;
                printManager.PrintToFile = true;
                printManager.Apply();
            }
            catch (Exception ex)
            {
                trx.RollBack();
                Log.Error(ex, $"Reset settings: {ex.Message}");
            }
            finally
            {
                if (!trx.HasEnded())
                {
                    trx.Commit();
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


    public static Dictionary<string, List<SheetModel>> GetData(Document doc, string printerName, bool isColorEnabled = true)
    {
        string projectTitle = doc.Title;

        SetupPrinterSettings(doc, printerName);

        FilteredElementCollector collector = new(doc);

        collector = collector.OfCategory(BuiltInCategory.OST_TitleBlocks);
        collector = collector.OfClass(typeof(FamilyInstance));
        collector = collector.WhereElementIsNotElementType();

        ColorDepthType colorType = isColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;

        Dictionary<string, List<SheetModel>> sheetPrintData = new(collector.GetElementCount());

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
                    Log.Debug(PrinterApiUtility.AddFormat(printerName, widthInMm, heighInMm));
                }

                if (PrinterApiUtility.GetPaperSize(widthInMm, heighInMm, out PaperSize papeSize))
                {
                    PageOrientationType orientType = RevitPrinterUtil.GetOrientation(widthInMm, heighInMm);

                    SheetModel model = new(viewSheet, papeSize, orientType);

                    model.SetSheetName(doc, projectTitle, "pdf");

                    if (model.IsValid)
                    {
                        model.IsColorEnabled = isColorEnabled;

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


    public static List<SheetModel> PrintSheetData(Document doc, PrinterControl printer, Dictionary<string, List<SheetModel>> sheetData, string folder)
    {
        List<PrintSetting> printAllSettings = RevitPrinterUtil.GetPrintSettings(doc);

        List<SheetModel> resultFilePaths = new(sheetData.Values.Count);

        using Transaction trx = new(doc, "ExportToPDF");

        if (TransactionStatus.Started == trx.Start())
        {
            try
            {
                Log.Debug("Start transaction ...");

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

                            model.TempFilePath = Path.Combine(folder, model.SheetName);

                            if (!File.Exists(model.TempFilePath) && printer.DoPrint(doc, model))
                            {
                                model.IsSuccessfully = true;
                                resultFilePaths.Add(model);
                            }
                        }
                    }
                }

                trx.Commit();
            }
            catch (Exception ex)
            {
                if (!trx.HasEnded())
                {
                    trx.RollBack();
                    Log.Error(ex, ex.Message);
                }
            }
            finally
            {
                printer?.ResetPrinterSettings();
            }
        }

        return resultFilePaths;
    }


    public static async Task<bool> ExecutePrintAsync(Document doc, string folder, SheetModel model)
    {
        Log.Debug("Start submit print...");

        string filePath = Path.Combine(folder, model.SheetName);

        PrintManager printManager = doc.PrintManager;

        printManager.PrintToFileName = filePath;

        PathHelper.DeleteExistsFile(filePath);

        if (printManager.SubmitPrint(model.ViewSheet))
        {
            if (await PathHelper.AwaitExistsFileAsync(filePath))
            {
                model.IsSuccessfully = true;
                return true;
            }
        }

        return false;
    }



}
