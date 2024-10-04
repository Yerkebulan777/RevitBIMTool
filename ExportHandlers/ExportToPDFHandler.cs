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
    private List<SheetModel> sheetModels;
    private Dictionary<string, List<SheetModel>> sheetData;


    public void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        Log.Debug("Start export to PDF...");

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string folder = Path.Combine(Path.GetTempPath(), $"{revitFileName}");
        string section = RevitPathHelper.GetSectionName(revitFilePath);

        bool colorTypeEnabled = section is not ("KJ" or "KR" or "KG");

        if (PrintHandler.TryGetAvailablePrinter(out PrinterControl printer))
        {
            sheetData = PrintHandler.GetData(uidoc.Document, printer.PrinterName, revitFileName, colorTypeEnabled);

            Log.Information($"Available printer: {printer.PrinterName}");

            if (sheetData.Count > 0)
            {
                printer.InitializePrinter();

                RevitPathHelper.EnsureDirectory(folder);
                RevitPathHelper.EnsureDirectory(exportDirectory);

                Log.Information($"Total valid sheets: {sheetData.Values.Count}");

                string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

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

}
