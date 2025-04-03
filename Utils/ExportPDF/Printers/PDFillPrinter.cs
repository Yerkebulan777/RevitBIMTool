using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class PDFillPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\PlotSoft\Writer\";
    public override string PrinterName => "PDFill PDF&Image Writer";
    public override bool IsInternalPrinter => false;


    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        string outputOptionsPath = Path.Combine(RegistryPath, "OutputOption");
        string pdfOptimizationPath = Path.Combine(RegistryPath, "PDF_Optimization");

        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "HIDE_DIALOG", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "USE_DEFAULT_FOLDER", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "USE_DEFAULT_FILENAME", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "USE_PRINT_JOBNAME", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "TIME_STAMP", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "VIEW_FILE", 0);

        RegistryHelper.SetValue(Registry.CurrentUser, pdfOptimizationPath, "USEOPTIMIZATION", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, pdfOptimizationPath, "Auto_Rotate_Page", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, pdfOptimizationPath, "Resolution", 300);
        RegistryHelper.SetValue(Registry.CurrentUser, pdfOptimizationPath, "Color_Model", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, pdfOptimizationPath, "CompressFont", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, pdfOptimizationPath, "Settings", 2);
    }


    public override void ReleasePrinterSettings()
    {
        string outputOptionsPath = Path.Combine(RegistryPath, "OutputOption");
        string pdfOptimizationPath = Path.Combine(RegistryPath, "PDF_Optimization");

        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "DEFAULT_FILENAME", string.Empty);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "USE_DEFAULT_FOLDER", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "USE_DEFAULT_FILENAME", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "USE_PRINT_JOBNAME", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "HIDE_DIALOG", 0);

        RegistryHelper.SetValue(Registry.CurrentUser, pdfOptimizationPath, "USEOPTIMIZATION", 0);

        PrinterStateManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string outputOptionsPath = Path.Combine(RegistryPath, "OutputOption");

        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "DEFAULT_FOLDER_PATH", folder);
        RegistryHelper.SetValue(Registry.CurrentUser, outputOptionsPath, "DEFAULT_FILENAME", model.SheetName);

        return PrintHelper.ExecutePrint(doc, model, folder);
    }



}