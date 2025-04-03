﻿using Autodesk.Revit.DB;
using RevitBIMTool.Utils.ExportPDF.Printers;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF;

internal static class PrintSettingsManager
{
    /// <summary>
    /// Сбрасывает настройки принтера 
    /// </summary>
    public static void ResetPrinterSettings(Document doc, PrinterControl printer)
    {
        if (printer is not null && !printer.IsInternalPrinter)
        {
            PrintManager printManager = doc.PrintManager;

            List<PrintSetting> printSettings = CollectPrintSettings(doc);

            using Transaction trx = new(doc, "ResetPrintSetting");

            if (TransactionStatus.Started == trx.Start())
            {
                try
                {
                    printSettings.ForEach(set => doc.Delete(set.Id));
                    printManager.SelectNewPrintDriver(printer.PrinterName);
                    printManager.PrintRange = PrintRange.Current;
                    printManager.PrintToFile = true;
                    printManager.Apply();

                    _ = trx.Commit();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ResetPrinterSettings error!: {Message}", ex.Message);
                    throw new InvalidOperationException("ResetPrinterSettings error!", ex);
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

    /// <summary>
    /// Определяет ориентацию страницы по ширине и высоте
    /// </summary>
    public static PageOrientationType GetOrientation(double width, double height)
    {
        return width > height ? PageOrientationType.Landscape : PageOrientationType.Portrait;
    }

    /// <summary>
    /// Создает и применяет настройку печати для группы форматов
    /// </summary>
    public static bool SetupPrintSetting(Document doc, string formatName, PageOrientationType orientation, bool colorEnabled)
    {
        Log.Information("Setting up format: {FormatName}", formatName);

        SetPrintSettings(doc, formatName, orientation, colorEnabled);

        if (GetPrintSettingByName(doc, formatName) is null)
        {
            Log.Warning("Failed to create print setting by name: {FormatName}. Trying fallback method.", formatName);
            throw new InvalidOperationException($"Failed to create print setting: {formatName}");
        }

        return true;
    }

    /// <summary>
    /// Устанавливает настройки печати для документа
    /// </summary>
    private static void SetPrintSettings(Document doc, string formatName, PageOrientationType orientation, bool color)
    {
        PrintManager printManager = doc.PrintManager;
        PrintSetup printSetup = printManager.PrintSetup;
        printSetup.CurrentPrintSetting = printSetup.InSession;

        IPrintSetting currentPrintSetting = printSetup.CurrentPrintSetting;

        try
        {
            currentPrintSetting.PrintParameters.ViewLinksinBlue = false;
            currentPrintSetting.PrintParameters.HideReforWorkPlanes = true;
            currentPrintSetting.PrintParameters.HideUnreferencedViewTags = true;
            currentPrintSetting.PrintParameters.HideCropBoundaries = true;
            currentPrintSetting.PrintParameters.HideScopeBoxes = true;

            currentPrintSetting.PrintParameters.MaskCoincidentLines = false;
            currentPrintSetting.PrintParameters.ReplaceHalftoneWithThinLines = false;

            currentPrintSetting.PrintParameters.ZoomType = ZoomType.Zoom;
            currentPrintSetting.PrintParameters.RasterQuality = RasterQualityType.Medium;
            currentPrintSetting.PrintParameters.PaperPlacement = PaperPlacementType.Margins;
            currentPrintSetting.PrintParameters.HiddenLineViews = HiddenLineViewsType.VectorProcessing;
            currentPrintSetting.PrintParameters.ColorDepth = color ? ColorDepthType.Color : ColorDepthType.BlackLine;

            currentPrintSetting.PrintParameters.PageOrientation = orientation;

            foreach (PaperSize pSize in printManager.PaperSizes)
            {
                string paperSizeName = pSize.Name;

                if (paperSizeName.Equals(formatName))
                {
                    using Transaction trx = new(doc, formatName);

                    if (TransactionStatus.Started == trx.Start())
                    {
                        currentPrintSetting.PrintParameters.Zoom = 100;
                        currentPrintSetting.PrintParameters.PaperSize = pSize;
                        currentPrintSetting.PrintParameters.MarginType = MarginType.PrinterLimit;
                        printManager.PrintSetup.CurrentPrintSetting = currentPrintSetting;

                        Log.Debug("Print setting created: {FormatName}", formatName);

                        if (printSetup.SaveAs(formatName))
                        {
                            Thread.Sleep(100);
                            break;
                        }
                    }

                    trx.Commit();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Setting print settings error: {0}!", ex.Message);
            throw new InvalidOperationException("Setting print settings error!", ex);
        }
        finally
        {
            printManager.Apply();
        }
    }

    /// <summary>
    /// Собирает все настройки печати из документа
    /// </summary>
    private static List<PrintSetting> CollectPrintSettings(Document doc)
    {
        return [.. new FilteredElementCollector(doc).OfClass(typeof(PrintSetting)).Cast<PrintSetting>()];
    }

    /// <summary>
    ///  Получает настройку печати по имени
    /// </summary>
    private static PrintSetting GetPrintSettingByName(Document doc, string formatName)
    {
        return CollectPrintSettings(doc).FirstOrDefault(ps => ps.Name.Equals(formatName));
    }



}
