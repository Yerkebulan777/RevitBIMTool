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
using System.Text.RegularExpressions;


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

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string randomName = Regex.Replace(Path.GetRandomFileName(), @"[\p{P}\p{S}]", string.Empty);
        string baseDirectory = ExportPathHelper.ExportDirectory(revitFilePath, "03_PDF", true);
        string exportFullPath = Path.Combine(baseDirectory, $"{revitFileName}.pdf");
        string tempFolder = Path.Combine(temp, randomName);

        if (!ExportPathHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
        {
            Log.Debug("Start export to PDF...");
            Log.Debug($"TEMP directory: {tempFolder}");

            RevitPathHelper.EnsureDirectory(tempFolder);
            PrintPdfHandler.ResetPrintSettings(doc, printerName);

            string defaultPrinter = PrinterApiUtility.GetDefaultPrinter();

            if (!defaultPrinter.Equals(printerName))
            {
                throw new ArgumentException(printerName + "is not defined");
            }

            ColorDepthType colorType = ColorDepthType.BlackLine;

            //if (colorType == ColorDepthType.BlackLine)

            Dictionary<string, List<SheetModel>> sheetData = PrintPdfHandler.GetSheetPrintedData(doc, revitFileName, colorType);
            List<SheetModel> sheetModels = PrintPdfHandler.PrintSheetData(ref doc, sheetData, tempFolder);
            Log.Information($"Total valid sheet count: ({sheetModels.Count})");

            if (sheetModels.Count > 0)
            {
                _ = sb.AppendLine(Path.GetDirectoryName(baseDirectory));
                PdfMergeHandler.CombinePDFsFromFolder(sheetModels, tempFolder, exportFullPath);
                SystemFolderOpener.OpenFolder(baseDirectory);
                RevitPathHelper.DeleteDirectory(tempFolder);
            }

            return sb.ToString();
        }

        return sb.ToString();
    }
}
