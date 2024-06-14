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

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportHelper.ExportDirectory(revitFilePath, "03_PDF", true);
        string tempFolder = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User);
        string exportFullPath = Path.Combine(exportBaseDirectory, revitFileName + ".pdf");

        if (!ExportHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
        {
            Log.Information("Start export to PDF...");

            RegistryHelper.ActivateSettingsForPDFCreator(tempFolder);
            PrintPdfHandler.ResetPrintSettings(doc, printerName);

            tempFolder = Path.Combine(tempFolder, revitFileName);
            RevitPathHelper.EnsureDirectory(tempFolder);

            string defaultPrinter = PrinterApiUtility.GetDefaultPrinter();

            Log.Debug($"TEMP directory: {tempFolder}");

            if (!defaultPrinter.Equals(printerName))
            {
                throw new ArgumentException(printerName + "is not defined");
            }

            Dictionary<string, List<SheetModel>> sheetData = PrintPdfHandler.GetSheetPrintedData(doc, revitFileName);
            List<SheetModel> sheetModels = PrintPdfHandler.PrintSheetData(ref doc, sheetData, tempFolder);
            Log.Information($"Total valid sheet count: ({sheetModels.Count})");

            if (sheetModels.Count > 0)
            {
                sb.AppendLine(Path.GetDirectoryName(exportBaseDirectory));
                PdfMergeHandler.CombinePDFsFromFolder(sheetModels, tempFolder, exportFullPath);
                SystemFolderOpener.OpenFolderInExplorerIfNeeded(exportBaseDirectory);
                RevitPathHelper.DeleteDirectory(tempFolder);
            }

            return sb.ToString();
        }

        return sb.ToString();
    }
}
