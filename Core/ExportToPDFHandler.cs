﻿using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using RevitBIMTool.PrintUtil;
using RevitBIMTool.Utils;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Text;


namespace RevitBIMTool.Core;
internal static class ExportToPDFHandler
{
    const string printerName = "PDFCreator";

    public static string ExportToPDF(Document document, string revitFilePath)
    {
        StringBuilder sb = new();

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportHelper.ExportDirectory(revitFilePath, "03_PDF", true);
        string tempPath = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User);
        string exportFullPath = Path.Combine(exportBaseDirectory, revitFileName + ".pdf");

        tempPath = Path.Combine(tempPath, revitFileName);
        RevitPathHelper.EnsureDirectory(tempPath);
        RevitPathHelper.ClearDirectory(tempPath);

        Log.Information("Start export to PDF...");

        if (!ExportHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
        {
            RegistryHelper.ActivateSettingsForPDFCreator(tempPath);
            MainPrintHandler.ResetPrintSettings(document, printerName);
            string defaultPrinter = PrinterApiUtility.GetDefaultPrinter();

            if (!defaultPrinter.Equals(printerName))
            {
                throw new ArgumentException(printerName + "is not defined");
            }

            Dictionary<string, List<SheetModel>> sheetData = MainPrintHandler.GetSheetPrintedData(ref document);
            List<SheetModel> sheetModels = MainPrintHandler.PrintSheetData(ref document, sheetData, tempPath);
            Debug.Print($"Total sheet count: ({sheetModels.Count})");

            if (sheetModels.Count > 0)
            {
                PdfMergeHandler.CombinePDFsFromFolder(sheetModels, tempPath, exportFullPath);
                string directory = Path.GetDirectoryName(exportBaseDirectory);
                SystemFolderOpener.OpenFolder(exportBaseDirectory);
                _ = sb.AppendLine(directory);
            }

            return sb.ToString();
        }

        return sb.ToString();
    }
}
