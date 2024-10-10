using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using Document = Autodesk.Revit.DB.Document;
using Element = Autodesk.Revit.DB.Element;
using PaperSize = System.Drawing.Printing.PaperSize;
using PrintRange = Autodesk.Revit.DB.PrintRange;


namespace RevitBIMTool.Utils.ExportPDF;
internal static class PrintHandler
{
    public const string StatusPath = @"Printers\Settings\Wizard";

    public static bool TryGetAvailablePrinter(out PrinterControl availablePrinter)
    {
        availablePrinter = null;

        foreach (PrinterControl printer in GetInstalledPrinters())
        {
            if (GetPrinterStatus(printer, StatusPath) == 0)
            {
                availablePrinter = printer;
                return true;
            }
        }

        return false;
    }


    private static List<PrinterControl> GetInstalledPrinters()
    {
        List<PrinterControl> printers =
        [
            new Pdf24Printer(),
            new CreatorPrinter(),
            new ClawPdfPrinter(),
            new InternalPrinter(),
        ];

        List<PrinterControl> result = new(printers.Count);

        foreach (PrinterControl printer in printers)
        {
            if (printer.IsPrinterInstalled())
            {
                result.Add(printer);
            }
        }

        return result;
    }


    private static int GetPrinterStatus(PrinterControl printer, string statusPath)
    {
        string name = printer.PrinterName;
        return RegistryHelper.IsSubKeyExists(Registry.CurrentUser, statusPath)
            ? Convert.ToInt32(RegistryHelper.GetValue(Registry.CurrentUser, statusPath, name))
            : Convert.ToInt32(RegistryHelper.SetValue(Registry.CurrentUser, statusPath, name, 0));
    }


    private static void ResetAndApplyPrinterSettings(Document doc, string printerName)
    {
        if (string.IsNullOrEmpty(printerName))
        {
            throw new ArgumentException("PrinterName");
        }

        PrintManager printManager = doc.PrintManager;

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


    public static Dictionary<string, List<SheetModel>> GetData(Document doc, string printerName, string title, bool isColorEnabled = true)
    {
        ResetAndApplyPrinterSettings(doc, printerName);

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

                    model.SetSheetName(doc, title, "pdf");

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

                            if (printer.Print(doc, folder, model))
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
                Log.Debug("Reset printer settings");

                printer.ResetPrinterSettings();

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
        Log.Debug("Start export to pdf...");

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
            model.TempPath = Path.Combine(folder, model.SheetName);
            return true;
        }
#endif
        return false;
    }


    public static bool PrintSheet(Document doc, string folder, SheetModel model)
    {
        Log.Debug("Start submit print...");

        string filePath = Path.Combine(folder, model.SheetName);

        PrintManager printManager = doc.PrintManager;

        printManager.PrintToFileName = filePath;

        RevitPathHelper.DeleteExistsFile(filePath);

        if (printManager.SubmitPrint(model.ViewSheet))
        {
            if (RevitPathHelper.AwaitExistsFile(filePath))
            {
                model.TempPath = filePath;
                return true;
            }
        }

        return false;
    }


}
