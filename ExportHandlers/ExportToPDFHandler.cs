using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.ExportHandlers;

internal sealed class ExportToPDFHandler
{
    private List<SheetModel> sheetModels;
    private Dictionary<string, List<SheetModel>> sheetData;

    public void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string baseTempPath = Path.GetDirectoryName(Path.GetTempPath());
        string folder = Path.Combine(baseTempPath, $"{revitFileName}");
        string section = RevitPathHelper.GetSectionName(revitFilePath);

        bool colorTypeEnabled = section is not ("KJ" or "KR" or "KG");

        if (!PrintHandler.TryRetrievePrinter(out PrinterControl printer))
        {
            Log.Error("No available printer found!");
            RevitFileHelper.CloseRevitApplication();
        }

        sheetData = PrintHandler.GetData(uidoc.Document, printer.PrinterName, revitFileName, colorTypeEnabled);

        Log.Information($"Available printer: {printer.PrinterName}");

        if (sheetData.Count > 0)
        {
            printer.InitializePrinter();

            Log.Debug("Start export to PDF...");

            RevitPathHelper.EnsureDirectory(folder);
            RevitPathHelper.EnsureDirectory(exportDirectory);

            string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

            Log.Information($"Total valid sheets: {sheetData.Values.Sum(lst => lst.Count)}");

            sheetModels = PrintHandler.PrintSheetData(uidoc.Document, printer, sheetData, folder);

            if (sheetModels.Count > 0)
            {
                MergeHandler.Combine(sheetModels, folder, exportPath);
                SystemFolderOpener.OpenFolder(exportDirectory);
                RevitPathHelper.DeleteDirectory(folder);
            }
        }
    }



}
