using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal abstract class PrinterControl
{
    public abstract string RegistryPath { get; }
    public abstract string PrinterName { get; }
    public abstract bool IsInternalPrinter { get; }
    public abstract string RevitFilePath { get; set; }


    public virtual bool IsPrinterInstalled()
    {
        return IsInternalPrinter
        ? int.TryParse(RevitBimToolApp.Version, out int version) && version >= 2023
        : RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath);
    }

    public abstract void InitializePrinter(string revitFilePath);

    public abstract void RestoreDefaultSettings();

    public abstract bool DoPrint(Document doc, SheetModel model, string folder);


}
