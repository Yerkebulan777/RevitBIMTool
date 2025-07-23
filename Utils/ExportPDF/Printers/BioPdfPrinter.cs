using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using Serilog;
using System.IO;
using System.Text;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class BioPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\bioPDF\PDF Writer - bioPDF";
    public override string PrinterName => "PDF Writer - bioPDF";
    public override bool IsInternalPrinter => false;

    public string LocalSettingsDir;
    public string GlobalSettingsDir;

    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        const string BioPdfPrinterPath = @"PDF Writer\PDF Writer - bioPDF";
        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        LocalSettingsDir = Path.Combine(localAppDataPath, BioPdfPrinterPath);
        GlobalSettingsDir = Path.Combine(programDataPath, BioPdfPrinterPath);
    }


    public override void ReleasePrinterSettings()
    {
        PrinterStateManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string revitFileName = Path.GetFileNameWithoutExtension(model.RevitFilePath);
        string statusFileName = $"{Uri.EscapeDataString(model.SheetName)}_{Guid.NewGuid()}.ini";
        string statusFilePath = Path.Combine(Path.GetTempPath(), statusFileName);

        CreateBioPdfRunonce(revitFileName, model.SheetName, statusFilePath);

        return PrintHelper.ExecutePrint(doc, model, folder);
    }


    public void ConfigureGhostscriptOptimization()
    {
        string globalIniPath = Path.Combine(GlobalSettingsDir, "global.ini");

        Dictionary<string, string> config = new()
        {
            ["GhostscriptTimeout"] = "7200",
            ["GSGarbageCollection"] = "no",
            ["MaxMemory"] = "1073741824",
            ["Quality"] = "screen"
        };

        WriteIniSettings(globalIniPath, "PDF Printer", config);
    }


    public void CreateBioPdfRunonce(string title, string outputPath, string statusPath)
    {
        string runoncePath = Path.Combine(LocalSettingsDir, "runonce.ini");

        if (File.Exists(runoncePath))
        {
            File.Delete(runoncePath);
        }

        Dictionary<string, string> settings = new()
        {
            ["Title"] = title,
            ["Subject"] = "Exported Document",
            ["Creator"] = "Revit BIM Tool",
            ["DisableOptionDialog"] = "yes",
            ["ShowProgressFinished"] = "no",
            ["ConfirmOverwrite"] = "yes",
            ["Output"] = outputPath,
            ["StatusFile"] = statusPath,
            ["ShowSettings"] = "never",
            ["ShowSaveAS"] = "never",
            ["ShowProgress"] = "no",
            ["ShowPDF"] = "no",
            ["Target"] = "printer",
            ["Quality"] = "printer",
            ["ColorModel"] = "rgb",
        };

        WriteIniSettings(runoncePath, "PDF Printer", settings);
    }


    private static void WriteIniSettings(string filePath, string section, Dictionary<string, string> settings)
    {
        string directory = Path.GetDirectoryName(filePath);

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory does not exist: {directory}");
        }

        try
        {
            StringBuilder content = new(settings.Count * 50);

            _ = content.AppendLine($"[{section}]");

            foreach (KeyValuePair<string, string> kvp in settings.Where(kvp => !string.IsNullOrEmpty(kvp.Key)))
            {
                _ = content.AppendLine($"{kvp.Key}={kvp.Value ?? string.Empty}");
            }

            File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
            Log.Debug("Created bioPDF ini settings: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to write INI settings to {FilePath}: {ErrorMessage}", filePath, ex.Message);
            throw new InvalidOperationException($"Cannot write INI file: {Path.GetFileName(filePath)}", ex);
        }
    }



}