using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;
using System.IO;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal abstract class PrinterControl
    {
        public abstract string RegistryPath { get; }
        public abstract string RegistryName { get; }
        public abstract int OverallRating { get; }


        public abstract void InitializePrinter();

        public abstract void ResetPrinterSettings();

        public abstract bool Print(Document doc, string folder, SheetModel model);


        public virtual bool IsPrinterInstalled()
        {
            const string printersPath = @"SYSTEM\CurrentControlSet\Control\Print\Printers";
            return RegistryHelper.IsSubKeyExists(Registry.LocalMachine, Path.Combine(printersPath, RegistryName));
        }


        public virtual bool IsPrinterEnabled()
        {
            return RegistryHelper.IsSubKeyExists(Registry.CurrentUser, RegistryPath);
        }

    }
}
