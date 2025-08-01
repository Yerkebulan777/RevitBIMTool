using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class AdobePdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";
    public override string PrinterName => "Adobe PDF";
    public override bool IsInternalPrinter => false;
    public override string RevitFilePath { get; set; }

    public override void InitializePrinter()
    {
        if (PrinterManager.TryReservePrinter(PrinterName, RevitFilePath))
        {
            Log.Information("Adobe PDF printer initialized");
        }
    }


    public override void ReleasePrinterSettings()
    {
        PrinterManager.ReleasePrinter(PrinterName);

        _ = RegistryHelper.DeleteRegistryValues(Registry.CurrentUser, RegistryPath);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string fullFilePath = Path.Combine(folder, model.SheetName);

        SetPDFSettings(fullFilePath, folder);
        Log.Debug("PDF destination: {0}", fullFilePath);
        return PrintHelper.ExecutePrint(doc, model, folder);
    }


    private void SetPDFSettings(string destFileName, string folder)
    {
        string RevitExePath = $@"C:\Program Files\Autodesk\Revit {RevitBimToolApp.Version}\Revit.exe";

        if (RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath))
        {
            try
            {
                string RevitPdfPortFolder = "LastPdfPortFolder - Revit.exe";
                RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, RevitExePath, destFileName);
                RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, RevitPdfPortFolder, folder);

                Log.Debug("PDF settings applied");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Registry access error");
                throw new InvalidOperationException("PDF registry settings failed", ex);
            }
        }
    }



}