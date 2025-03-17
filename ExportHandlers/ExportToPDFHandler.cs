using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.ExportPDF;
using RevitBIMTool.Utils.ExportPDF.Printers;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using UIDocument = Autodesk.Revit.UI.UIDocument;

namespace RevitBIMTool.ExportHandlers;

internal sealed class ExportToPdfHandler
{
    private PrinterControl printer;
    private List<SheetModel> sheetModels;
    private Dictionary<string, List<SheetModel>> sheetData;

    public void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        DirectoryInfo tempBase = Directory.GetParent(Path.GetTempPath());
        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string tempDirectory = Path.Combine(tempBase.FullName, $"{revitFileName}");
        string sectionName = PathHelper.GetSectionName(revitFilePath);

        bool colorTypeEnabled = sectionName is not ("KJ" or "KR" or "KG");

        if (!PrinterStateManager.TryRetrievePrinter(out printer))
        {
            Log.Fatal("No available printer found!");
            RevitFileHelper.CloseRevitApplication();
        }

        PrinterStateManager.ReservePrinter(printer.PrinterName);

        PrintSettingsHelper.SetupPrinterSettings(uidoc.Document, printer.PrinterName);

        sheetData = PrintHelper.GetData(uidoc.Document, printer.PrinterName, colorTypeEnabled);

        Log.Information($"Total sheets: {sheetData.Values.Sum(lst => lst.Count)}");
        Log.Information($"Available printer: {printer.PrinterName}");
        Log.Information($"Temp directory: {tempDirectory}");
        Log.Information($"Section: {sectionName}");

        if (sheetData.Count > 0)
        {
            printer.InitializePrinter();

            PathHelper.EnsureDirectory(tempDirectory);
            PathHelper.EnsureDirectory(exportDirectory);

            Log.Information("Start process export to PDF...");

            sheetModels = PrintHelper.PrintSheetData(uidoc.Document, printer, sheetData, tempDirectory);

            string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

            Log.Information($"Total valid sheets: {sheetModels.Count}");

            if (sheetModels.Count > 0)
            {
                MergeHandler.Combine(sheetModels, exportPath);
                SystemFolderOpener.OpenFolder(exportDirectory);
                PathHelper.DeleteDirectory(tempDirectory);
            }
        }
    }
}
