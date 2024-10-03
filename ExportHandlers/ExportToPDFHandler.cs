using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;


namespace RevitBIMTool.ExportHandlers;
internal sealed class ExportToPDFHandler
{
    private Dictionary<string, List<SheetModel>> sheetData;


    public void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        Document doc = uidoc.Document;

        Log.Debug("Start export to PDF...");

        string section = RevitPathHelper.GetSectionName(revitFilePath);
        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string tempFolder = Path.Combine(Path.GetTempPath(), $"{revitFileName}TMP");
        string exportFullPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

        PrinterControl printer = PrintHandler.GetAvailablePrinter(out string printerName);

        sheetData = PrintHandler.GetSheetData(doc, printerName, revitFileName, section is not ("KJ" or "KR" or "KG"));

        if (sheetData.Count > 0)
        {
            printer.InitializePrinter();

            RevitPathHelper.EnsureDirectory(tempFolder);
            RevitPathHelper.EnsureDirectory(exportDirectory);

            List<SheetModel> sheetModels = PrintHandler.PrintSheetData(doc, printer, sheetData, tempFolder);

            if (sheetModels.Count > 0)
            {
                SystemFolderOpener.OpenFolder(exportDirectory);
                Log.Information($"Total printed sheets: ({sheetModels.Count})");
                MergeHandler.CombinePDFsFromFolder(sheetModels, tempFolder, exportFullPath);
                RevitPathHelper.DeleteDirectory(tempFolder);
            }
        }

    }

}
