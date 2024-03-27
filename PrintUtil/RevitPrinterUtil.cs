﻿using Autodesk.Revit.DB;
using RevitBIMTool.Model;


namespace RevitBIMTool.PrintUtil;
internal static class RevitPrinterUtil
{
    public static PageOrientationType GetOrientation(double width, double height)
    {
        return width > height ? PageOrientationType.Landscape : PageOrientationType.Portrait;
    }


    public static void SetPrintSettings(Document doc, SheetModel sheetModel, string formatName)
    {
        PrintManager printManager = doc.PrintManager;
        PrintSetup printSetup = printManager.PrintSetup;
        printSetup.CurrentPrintSetting = printSetup.InSession;
        IPrintSetting currentPrintSetting = printSetup.CurrentPrintSetting;

        currentPrintSetting.PrintParameters.ZoomType = ZoomType.Zoom;
        currentPrintSetting.PrintParameters.ColorDepth = ColorDepthType.Color;
        currentPrintSetting.PrintParameters.RasterQuality = RasterQualityType.Medium;
        currentPrintSetting.PrintParameters.PaperPlacement = PaperPlacementType.Margins;
        currentPrintSetting.PrintParameters.HiddenLineViews = HiddenLineViewsType.VectorProcessing;

        currentPrintSetting.PrintParameters.PageOrientation = sheetModel.SheetOrientation;

        currentPrintSetting.PrintParameters.ViewLinksinBlue = false;
        currentPrintSetting.PrintParameters.HideReforWorkPlanes = true;
        currentPrintSetting.PrintParameters.HideUnreferencedViewTags = true;
        currentPrintSetting.PrintParameters.HideCropBoundaries = true;
        currentPrintSetting.PrintParameters.HideScopeBoxes = true;
        currentPrintSetting.PrintParameters.ReplaceHalftoneWithThinLines = false;
        currentPrintSetting.PrintParameters.MaskCoincidentLines = false;

        using (Transaction trx = new Transaction(doc, "SavePrintSettings"))
        {
            if (TransactionStatus.Started == trx.Start())
            {
                try
                {
                    foreach (PaperSize pSize in printManager.PaperSizes)
                    {
                        string paperSizeName = pSize.Name;

                        if (paperSizeName.Equals(sheetModel.PaperName))
                        {
                            currentPrintSetting.PrintParameters.Zoom = 100;
                            currentPrintSetting.PrintParameters.PaperSize = pSize;
                            currentPrintSetting.PrintParameters.MarginType = MarginType.PrinterLimit;

                            printManager.PrintSetup.CurrentPrintSetting = currentPrintSetting;

                            if (printSetup.SaveAs(formatName))
                            {
                                printManager.Apply();
                                _ = trx.Commit();
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Source);
                }
                finally
                {
                    if (!trx.HasEnded())
                    {
                        _ = trx.RollBack();
                    }
                }
            }
        }
    }


    public static List<PrintSetting> GetPrintSettings(Document doc)
    {
        return new FilteredElementCollector(doc).OfClass(typeof(PrintSetting)).Cast<PrintSetting>().ToList();
    }



}
