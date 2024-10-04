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
        Log.Debug("Start export to PDF...");

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string tempDirectory = Environment.GetEnvironmentVariable("TEMP");
        string folder = Path.Combine(tempDirectory, $"{revitFileName}");
        string section = RevitPathHelper.GetSectionName(revitFilePath);

        bool colorTypeEnabled = section is not ("KJ" or "KR" or "KG");

        if (PrintHandler.TryGetAvailablePrinter(out PrinterControl printer))
        {
            sheetData = PrintHandler.GetData(uidoc.Document, printer.RegistryName, revitFileName, colorTypeEnabled);

            Log.Information($"Available printer: {printer.RegistryName}");

            if (sheetData.Values.Count > 0)
            {
                printer.InitializePrinter();

                RevitPathHelper.EnsureDirectory(folder);
                RevitPathHelper.EnsureDirectory(exportDirectory);

                Log.Information($"Total valid sheets: {sheetData.Values.Count}");

                string exportPath = Path.Combine(exportDirectory, $"{revitFileName}.pdf");

                List<SheetModel> sheetModels = PrintHandler.PrintSheetData(uidoc.Document, printer, sheetData, folder);

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
