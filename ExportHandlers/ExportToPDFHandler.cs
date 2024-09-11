using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.ExportPdfUtil;
using RevitBIMTool.Utils.PrintUtil;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using System.IO;


namespace RevitBIMTool.ExportHandlers;
internal static class ExportToPDFHandler
{
    private const string printerName = "PDFCreator";

    public static void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        Document doc = uidoc.Document;

        Log.Debug("Start export to PDF...");

        string sectionName = RevitPathHelper.GetSectionName(revitFilePath);
        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string tempFolder = Path.Combine(Path.GetTempPath(), $"{revitFileName}TMP");
        string targetFullPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

        RevitPathHelper.EnsureDirectory(tempFolder);
        RevitPathHelper.EnsureDirectory(exportDirectory);
        PrintHandler.ResetPrintSettings(doc, printerName);

        string defaultPrinter = PrinterApiUtility.GetDefaultPrinter();

        if (!defaultPrinter.Equals(printerName))
        {
            throw new ArgumentException(printerName + "is not defined");
        }

        ColorDepthType colorType = ColorDepthType.Color;

        if (sectionName.Equals("KJ") || sectionName.Equals("KR") || sectionName.Equals("KG"))
        {
            colorType = ColorDepthType.BlackLine;
        }

        Dictionary<string, List<SheetModel>> sheetData = PrintHandler.GetSheetData(doc, revitFileName, colorType);

        if (sheetData.Count > 0)
        {
            List<SheetModel> sheetModels = PrintHandler.PrintSheetData(doc, sheetData, tempFolder);

            Log.Information($"Total valid sheets: ({sheetModels.Count})");

            if (sheetModels.Count > 0)
            {
                MergeHandler.CombinePDFsFromFolder(sheetModels, tempFolder, targetFullPath);
                SystemFolderOpener.OpenFolder(exportDirectory);
                RevitPathHelper.DeleteDirectory(tempFolder);
            }
        }

    }
}
