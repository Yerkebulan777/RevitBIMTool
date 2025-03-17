using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using UIDocument = Autodesk.Revit.UI.UIDocument;

namespace RevitBIMTool.ExportHandlers;

internal static class ExportToPdfHandler
{
    public static void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        DirectoryInfo tempBase = Directory.GetParent(Path.GetTempPath());
        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string tempDirectory = Path.Combine(tempBase.FullName, $"{revitFileName}");
        string sectionName = PathHelper.GetSectionName(revitFilePath);

        Log.Information("Temp directory path: {TempDirectory}", tempDirectory);
        Log.Information("Export folder path: {ExportDirectory}", exportDirectory);

        if (!PrinterStateManager.TryRetrievePrinter(revitFilePath, out PrinterControl printer))
        {
            Log.Fatal("No available printer found!");
            RevitFileHelper.CloseRevitApplication();
        }

        if (PrinterStateManager.ReservePrinter(printer.PrinterName))
        {
            Dictionary<string, List<SheetModel>> sheetData;

            bool colorTypeEnabled = sectionName is not ("KJ" or "KR" or "KG");

            Log.Information("Available printer: {PrinterName}", printer.PrinterName);

            PrintSettingsManager.ResetPrinterSettings(uidoc.Document, printer.PrinterName);

            sheetData = PrintHelper.GetData(uidoc.Document, printer.PrinterName, colorTypeEnabled);

            if (sheetData.Count > 0)
            {
                printer.InitializePrinter();

                PathHelper.EnsureDirectory(tempDirectory);
                PathHelper.EnsureDirectory(exportDirectory);

                Log.Information("Start process export to PDF...");

                List<SheetModel> sheetModels = PrintHelper.PrintSheetData(uidoc.Document, printer, sheetData, tempDirectory);

                string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

                Log.Information("Total sheets: {TotalSheets}", sheetData.Values.Sum(lst => lst.Count));

                if (sheetModels.Count > 0)
                {
                    MergeHandler.Combine(sheetModels, exportPath);
                    SystemFolderOpener.OpenFolder(exportDirectory);
                    PathHelper.DeleteDirectory(tempDirectory);
                }
            }
        }
    }
}
