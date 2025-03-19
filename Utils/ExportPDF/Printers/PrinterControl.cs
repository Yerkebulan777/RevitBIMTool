using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal abstract class PrinterControl
{
    public abstract string RevitFilePath { get; set; }
    public abstract string RegistryPath { get; }
    public abstract string PrinterName { get; }
    public abstract bool IsInternal { get; }

    public abstract void InitializePrinter();

    public abstract void ResetPrinterSettings();

    public virtual bool IsPrinterInstalled()
    {
        bool result = false;

        if (IsInternal && int.TryParse(RevitBIMToolApp.Version, out int version))
        {
            result = version >= 2023;
        }
        else if (RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath))
        {
            const string printersPath = @"SYSTEM\CurrentControlSet\Control\DoPrint\Printers";
            result = RegistryHelper.IsKeyExists(Registry.LocalMachine, Path.Combine(printersPath, PrinterName));
        }

        Log.Information("{PrinterName} is installed: {IsInstalled}", PrinterName, result);

        return result;
    }

    public abstract bool DoPrint(Document doc, SheetModel model, string folder);




}
