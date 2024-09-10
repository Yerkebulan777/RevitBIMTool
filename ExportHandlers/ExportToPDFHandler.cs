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

    public static string ExportToPDF(UIDocument uidoc, string revitFilePath, string sectionName)
    {
        StringBuilder sb = new();
        Document doc = uidoc.Document;
        string temp = Path.GetTempPath();

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        //string randomName = Regex.Replace(Path.GetRandomFileName(), @"[\p{P}\p{S}]", string.Empty);
        string baseDirectory = ExportHelper.SetDirectory(revitFilePath, "03_PDF", true);
        string exportFullPath = Path.Combine(baseDirectory, $"{revitFileName}.pdf");
        string tempFolder = Path.Combine(Path.GetTempPath(), revitFileName);

        if (!ExportHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
        {
            Log.Debug("Start export to PDF...");

            RevitPathHelper.EnsureDirectory(tempFolder);
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
                    sheetModels = SheetModel.SortSheetModels(sheetModels);
                    sheetModels.ForEach(model => Log.Debug(model.SheetName));

                    MergeHandler.CombinePDFsFromFolder(sheetModels, tempFolder, exportFullPath);

                    _ = sb.AppendLine(Path.GetDirectoryName(baseDirectory));

                    SystemFolderOpener.OpenFolder(baseDirectory);
                    RevitPathHelper.DeleteDirectory(tempFolder);
                }
            }

            return sb.ToString();
        }

        return sb.ToString();
    }
}
