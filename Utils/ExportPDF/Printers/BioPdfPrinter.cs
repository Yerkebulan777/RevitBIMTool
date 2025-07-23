using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class BioPdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\bioPDF\PDF Writer - bioPDF";
    public override string PrinterName => "PDF Writer - bioPDF";
    public override bool IsInternalPrinter => false;

    public string UserSettingsPath;
    public string GlobalSettingsPath;


    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        const string BioPdfPrinterPath = @"PDF Writer\PDF Writer - bioPDF";

        UserSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), BioPdfPrinterPath);

        GlobalSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), BioPdfPrinterPath);
    }


    public override void ReleasePrinterSettings()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        string runonceFilePath = Path.Combine(UserSettingsPath, "runonce.ini");

        CreateRunonceFile(desktopPath, string.Empty, runonceFilePath);

        PrinterStateManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string runonceFilePath = Path.Combine(UserSettingsPath, "runonce.ini");

        CreateRunonceFile(folder, model.SheetName, runonceFilePath);

        return PrintHelper.ExecutePrint(doc, model, folder);
    }


    private static void CreateRunonceFile(string path, string title, string runonceFilePath)
    {
        if (File.Exists(runonceFilePath))
        {
            File.Delete(runonceFilePath);
        }

        string runonce =
            "[PDF Printer]\r\n" +
            "Output=" + path + "\r\n" +
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


}