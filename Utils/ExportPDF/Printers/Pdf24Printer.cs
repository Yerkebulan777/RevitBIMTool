using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class Pdf24Printer : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\PDF24\Services\PDF";
    public override string PrinterName => "PDF24";
    public override string RevitFilePath { get; set; }
    public override bool IsInternal => false;

    public override void InitializePrinter()
    {
        Log.Debug("Initialize PDF24 printer");

        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOpenDir", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "Handler", "autoSave");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveShowProgress", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveOverwriteFile", 1);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveUseFileChooser", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveFilename", "$fileName");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveProfile", "default/medium");

        PrinterStateManager.ReservePrinter(PrinterName);
    }


    public override void ResetPrinterSettings()
    {
        Log.Debug("Reset print settings");

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveDir", desktop);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveUseFileCmd", 0);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShellCmd", string.Empty);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "(Default)", string.Empty);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveFilename", "$fileName");

        PrinterStateManager.ReleasePrinter(PrinterName);
    }


    public override bool DoPrint(Document doc, SheetModel model)
    {
        string folder = Path.GetDirectoryName(model.TempFilePath).Replace("\\", "\\\\");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "AutoSaveDir", folder);
        return PrintHelper.ExecutePrintAsync(doc, folder, model).Result;
    }



}
