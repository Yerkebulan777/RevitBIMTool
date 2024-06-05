using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
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

        tempFolder = Path.Combine(tempFolder, revitFileName);
        RevitPathHelper.EnsureDirectory(tempFolder);
        RevitPathHelper.ClearDirectory(tempFolder);

        Log.Information("Start export to PDF...");

        if (!ExportHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
        {
            RegistryHelper.ActivateSettingsForPDFCreator(tempFolder);
            PrintPdfHandler.ResetPrintSettings(doc, printerName);

            string defaultPrinter = PrinterApiUtility.GetDefaultPrinter();

            Log.Debug($"TEMP directory: {tempFolder}");

            if (!defaultPrinter.Equals(printerName))
            {
                throw new ArgumentException(printerName + "is not defined");
            }

            Dictionary<string, List<SheetModel>> sheetData = PrintPdfHandler.GetSheetPrintedData(ref doc);
            List<SheetModel> sheetModels = PrintPdfHandler.PrintSheetData(ref doc, sheetData, tempFolder);
            Log.Information($"Total valid sheet count: ({sheetModels.Count})");

            if (sheetModels.Count > 0)
            {
                PdfMergeHandler.CombinePDFsFromFolder(sheetModels, tempFolder, exportFullPath);
                SystemFolderOpener.OpenFolderInExplorerIfNeeded(exportBaseDirectory);
                string directory = Path.GetDirectoryName(exportBaseDirectory);

                _ = sb.AppendLine(directory);
            }

            return sb.ToString();
        }

        return sb.ToString();
    }
}
