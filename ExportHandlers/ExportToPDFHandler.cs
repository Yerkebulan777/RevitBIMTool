using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.ExportPdfUtil;
using RevitBIMTool.Utils.PrintUtil;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using System.IO;
using System.Text;


namespace RevitBIMTool.ExportHandlers;
internal static class ExportToPDFHandler
{
    private const string printerName = "PDFCreator";

    public static string ExportToPDF(UIDocument uidoc, string revitFilePath)
    {
        StringBuilder sb = new();
        Document doc = uidoc.Document;
        string temp = Path.GetTempPath();

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string tempDirectory = Path.Combine(temp, Path.GetRandomFileName());
        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string baseDirectory = ExportHelper.ExportDirectory(revitFilePath, "03_PDF", true);
        string exportFullPath = Path.Combine(baseDirectory, $"{revitFileName}.pdf");

        if (!ExportHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
        {
            Log.Information("Start export to PDF...");

            RevitPathHelper.EnsureDirectory(tempDirectory);
            PrintPdfHandler.ResetPrintSettings(doc, printerName);
            RegistryHelper.ActivateSettingsForPDFCreator(tempDirectory);

            string defaultPrinter = PrinterApiUtility.GetDefaultPrinter();

            if (!defaultPrinter.Equals(printerName))
            {
                throw new ArgumentException(printerName + "is not defined");
            }

            Dictionary<string, List<SheetModel>> sheetData = PrintPdfHandler.GetSheetPrintedData(doc, revitFileName);
            List<SheetModel> sheetModels = PrintPdfHandler.PrintSheetData(ref doc, sheetData, tempDirectory);
            Log.Information($"Total valid sheet count: ({sheetModels.Count})");

            if (sheetModels.Count > 0)
            {
                Log.Debug($"TEMP directory: {tempDirectory}");
                _ = sb.AppendLine(Path.GetDirectoryName(baseDirectory));
                SystemFolderOpener.OpenFolderInExplorerIfNeeded(baseDirectory);
                PdfMergeHandler.CombinePDFsFromFolder(sheetModels, tempDirectory, exportFullPath);
                RevitPathHelper.DeleteDirectory(tempDirectory);
            }

            return sb.ToString();
        }

        return sb.ToString();
    }
}
