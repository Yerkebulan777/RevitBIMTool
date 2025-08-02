using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;
using System.Text;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class BioPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\bioPDF\PDF Writer - bioPDF";
    public override string PrinterName => "PDF Writer - bioPDF";
    public override bool IsInternalPrinter => false;
    public override string RevitFilePath { get; set; }
    private string RunonceIniPath { get; set; }
    private string GlobalIniPath { get; set; }


    public override void InitializePrinter(string revitFilePath)
    {
        const string BioPdfSettingsPath = @"PDF Writer\PDF Writer - bioPDF";

        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        RunonceIniPath = Path.Combine(localAppDataPath, BioPdfSettingsPath, "runonce.ini");
        GlobalIniPath = Path.Combine(programDataPath, BioPdfSettingsPath, "global.ini");

        Log.Information("Printer {Printer} initialized!", PrinterName);

        ConfigureGhostscriptOptimization();

        RevitFilePath = revitFilePath;
    }


    public override void ReleasePrinterSettings()
    {
        PrinterManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string outputPath = Path.Combine(folder, model.SheetName);
        string revitFileName = Path.GetFileNameWithoutExtension(model.RevitFilePath);
        string statusFilePath = Path.Combine(Path.GetTempPath(), $"{model.GetHashCode()}.ini");

        CreateBioPdfRunonce(revitFileName, outputPath, statusFilePath);

        return PrintHelper.ExecutePrint(doc, model, folder);
    }


    private void ConfigureGhostscriptOptimization()
    {
        Dictionary<string, string> config = new()
        {
            ["GhostscriptTimeout"] = "7200",
            ["GSGarbageCollection"] = "no",
            ["MaxMemory"] = "1073741824",
            ["Quality"] = "printer"
        };

        WriteIniSettings(GlobalIniPath, "PDF Printer", config);
    }


    private void CreateBioPdfRunonce(string title, string outputPath, string statusPath)
    {
        Dictionary<string, string> settings = new()
        {
            ["Title"] = title,
            ["Output"] = outputPath,
            ["StatusFile"] = statusPath,
            ["Subject"] = "Exported Document",
            ["Creator"] = "Revit BIM Tool",
            ["DisableOptionDialog"] = "yes",
            ["ShowProgressFinished"] = "no",
            ["ConfirmOverwrite"] = "yes",
            ["ShowSettings"] = "never",
            ["ShowSaveAS"] = "never",
            ["ShowProgress"] = "no",
            ["ShowPDF"] = "no",
            ["Target"] = "printer",
            ["Quality"] = "printer",
            ["ColorModel"] = "rgb",
        };

        WriteIniSettings(RunonceIniPath, "PDF Printer", settings);
    }


    private static void WriteIniSettings(string filePath, string section, Dictionary<string, string> settings)
    {
        string directory = Path.GetDirectoryName(filePath);
        PathHelper.DeleteExistsFile(filePath);
        PathHelper.EnsureDirectory(directory);

        try
        {
            StringBuilder content = new(settings.Count * 50);

            _ = content.AppendLine($"[{section}]");

            foreach (KeyValuePair<string, string> kvp in settings.Where(kvp => !string.IsNullOrEmpty(kvp.Key)))
            {
                _ = content.AppendLine($"{kvp.Key}={kvp.Value ?? string.Empty}");
            }

            File.WriteAllText(filePath, content.ToString(), Encoding.Default);
            Log.Debug("Created bioPDF ini settings: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to write INI settings to {FilePath}: {ErrorMessage}", filePath, ex.Message);
            throw new InvalidOperationException($"Cannot write INI file: {Path.GetFileName(filePath)}", ex);
        }



    }


}