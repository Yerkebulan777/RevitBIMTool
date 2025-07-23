using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using System.IO;
using System.Windows.Documents;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class BioPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\bioPDF\PDF Writer - bioPDF";
    public override string PrinterName => "PDF Writer - bioPDF";
    public override bool IsInternalPrinter => false;

    public string LocalSettingsDirectory;
    public string GlobalSettingsPath;

    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        const string BioPdfPrinterPath = @"PDF Writer\PDF Writer - bioPDF";

        string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        LocalSettingsDirectory = Path.Combine(localAppDataPath, BioPdfPrinterPath);
        GlobalSettingsPath = Path.Combine(programDataPath, BioPdfPrinterPath);
    }


    public override void ReleasePrinterSettings()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        CreateRunonceFile(desktopPath, string.Empty);

        PrinterStateManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        CreateRunonceFile(folder, model.SheetName);

        return PrintHelper.ExecutePrint(doc, model, folder);
    }


    public void CreateRunonceFile(string outputPath, string title)
    {
        string runonceFilePath = GenerateUniqueRunonceFile(title);

        if (File.Exists(runonceFilePath))
        {
            File.Delete(runonceFilePath);
        }

        string runonce =
            "[PDF Printer]\r\n" +
            "Output=" + outputPath + "\r\n" +
            "DisableOptionDialog=yes\r\n" +
            "ShowSettings=never\r\n" +
            "ShowSaveAS=never\r\n" +
            "ShowProgress=yes\r\n" +
            "ShowPDF=never\r\n" +
            "Title=" + title + "\r\n" +
            "Subject=Generated Document\r\n" +
            "Creator=Application Name\r\n" +
            "ShowProgressFinished=no\r\n" +
            "ConfirmOverwrite=no\r\n" +
            "Target=printer\r\n";

        File.WriteAllText(runonceFilePath, runonce);
    }


    public string GenerateUniqueRunonceFile(string documentName)
    {
        if (string.IsNullOrEmpty(documentName))
        {
            throw new ArgumentException(nameof(documentName));
        }

        string encodedName = Uri.EscapeDataString(documentName);
        string runonceFileName = $"runonce_{encodedName}_{Guid.NewGuid()}.ini";
        string runoncePath = Path.Combine(LocalSettingsDirectory, runonceFileName);

        return runoncePath;
    }



}