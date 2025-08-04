using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using UIDocument = Autodesk.Revit.UI.UIDocument;

namespace RevitBIMTool.ExportHandlers
{
    internal static class ExportPdfProcessor
    {
        public static void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
        {
            DirectoryInfo tempBase = Directory.GetParent(Path.GetTempPath());
            string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
            string tempDirectory = Path.Combine(tempBase.FullName, revitFileName);
            string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

            Log.Information("Starting safe PDF export");

            PathHelper.EnsureDirectory(tempDirectory);
            PathHelper.EnsureDirectory(exportDirectory);

            try
            {
                bool success = SafePrintManager.ExecuteSafePrinting(
                    printer => ProcessPrinting(uidoc, printer, revitFilePath, tempDirectory),
                    revitFilePath,
                    out List<SheetModel> sheetModels,
                    out PrinterControl usedPrinter);

                if (!success)
                {
                    Log.Fatal("Export failed");
                    RevitFileHelper.CloseRevitApplication();
                    return;
                }

                if (sheetModels?.Count > 0)
                {
                    MergeHandler.Combine(sheetModels, exportPath);
                    SystemFolderOpener.OpenFolder(exportDirectory);
                    PathHelper.DeleteDirectory(tempDirectory);
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error in PDF export");
                RevitFileHelper.CloseRevitApplication();
            }
        }

        private static List<SheetModel> ProcessPrinting(
            UIDocument uidoc,
            PrinterControl printer,
            string revitFilePath,
            string tempDirectory)
        {
            string sectionName = PathHelper.GetSectionName(revitFilePath);
            bool isColorEnabled = sectionName is not ("KJ" or "KR" or "KG");

            PrintSettingsManager.ResetPrinterSettings(uidoc.Document, printer);

            List<SheetFormatGroup> sheetFormatGroups = PrintHelper.GetData(
                uidoc.Document, printer, isColorEnabled);

            Log.Information("Total sheets: {TotalSheets}",
                sheetFormatGroups.Sum(g => g.SheetList.Count));

            return PrintHelper.PrintSheetData(
                uidoc.Document, printer, sheetFormatGroups, tempDirectory);
        }


    }
}