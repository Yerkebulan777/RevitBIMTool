using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using UIDocument = Autodesk.Revit.UI.UIDocument;

namespace RevitBIMTool.ExportHandlers;

internal sealed class ExportToPDFHandler
{
    private PrinterControl printer;
    private List<SheetModel> sheetModels;
    private Dictionary<string, List<SheetModel>> sheetData;

    public void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        DirectoryInfo tempBase = Directory.GetParent(Path.GetTempPath());
        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string tempDirectory = Path.Combine(tempBase.FullName, $"{revitFileName}");
        string sectionName = RevitPathHelper.GetSectionName(revitFilePath);

        bool colorTypeEnabled = sectionName is not ("KJ" or "KR" or "KG");

        if (!PrinterStateManager.TryRetrievePrinter(out printer))
        {
            Log.Fatal("No available printer found!");
            RevitFileHelper.CloseRevitApplication();
        }

        sheetData = PrintHelper.GetData(uidoc.Document, printer.PrinterName, colorTypeEnabled);

        Log.Information($"Total sheets: {sheetData.Values.Sum(lst => lst.Count)}");
        Log.Information($"Available printer: {printer.PrinterName}");
        Log.Information($"Temp directory: {tempDirectory}");
        Log.Information($"Section: {sectionName}");

        if (sheetData.Count > 0)
        {
            printer.InitializePrinter();

            Log.Debug("Start export to PDF...");

            RevitPathHelper.EnsureDirectory(tempDirectory);
            RevitPathHelper.EnsureDirectory(exportDirectory);

            string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

            sheetModels = PrintHelper.PrintSheetData(uidoc.Document, printer, sheetData, tempDirectory);

            Log.Information($"Total valid sheets: {sheetModels.Count}");

            if (sheetModels.Count > 0)
            {
                MergeHandler.Combine(sheetModels, exportPath);
                SystemFolderOpener.OpenFolder(exportDirectory);
                RevitPathHelper.DeleteDirectory(tempDirectory);
            }
        }
    }



}
