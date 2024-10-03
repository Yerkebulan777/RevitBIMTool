using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;
using System.IO;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal abstract class PrinterControl
    {
        public abstract string Name { get; }

        public abstract int OverallRating { get; }

        public abstract void InitializePrinter();

        public abstract void ResetPrinterSettings();

        public abstract void SetPrinterOutput(string filePath);

        public abstract bool Print(Document doc, string folder, SheetModel model);

        public virtual bool IsPrinterInstalled()
        {
            const string registryPath = @"SYSTEM\CurrentControlSet\Control\Print\Printers";
            return RegistryHelper.IsRegistryKeyExists(RegistryHive.LocalMachine, Path.Combine(registryPath, Name));
        }

    }
}
