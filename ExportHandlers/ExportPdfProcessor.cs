using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using UIDocument = Autodesk.Revit.UI.UIDocument;

namespace RevitBIMTool.ExportHandlers;

internal static class ExportPdfProcessor
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

        printer.IsColorEnabled = sectionName is not ("KJ" or "KR" or "KG");

        PrintSettingsManager.ResetPrinterSettings(uidoc.Document, printer);

        Log.Information("Available printer: {PrinterName}", printer.PrinterName);

        Dictionary<string, List<SheetModel>> sheetData = PrintHelper.GetData(uidoc.Document, printer);

        if (sheetData.Count > 0)
        {
            printer.InitializePrinter();

            Log.Information("Start export to PDF...");

            PathHelper.EnsureDirectory(tempDirectory);
            PathHelper.EnsureDirectory(exportDirectory);

            Log.Information("Total sheets: {TotalSheets}", sheetData.Values.Sum(lst => lst.Count));

            List<SheetModel> sheetModels = PrintHelper.PrintSheetData(uidoc.Document, printer, sheetData, tempDirectory);

            string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

            if (sheetModels.Count > 0)
            {
                MergeHandler.Combine(sheetModels, exportPath);
                SystemFolderOpener.OpenFolder(exportDirectory);
                PathHelper.DeleteDirectory(tempDirectory);
            }
        }

    }



}
