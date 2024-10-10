using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal abstract class PrinterControl
    {
        public abstract string RegistryPath { get; }
        public abstract string PrinterName { get; }


        public abstract void InitializePrinter();

        public abstract void ResetPrinterSettings();

        public abstract bool Print(Document doc, string folder, SheetModel model);


        public virtual bool IsPrinterInstalled()
        {
            const string printersPath = @"SYSTEM\CurrentControlSet\Control\Print\Printers";
            return RegistryHelper.IsSubKeyExists(Registry.LocalMachine, Path.Combine(printersPath, PrinterName));
        }


        public virtual bool IsPrinterEnabled()
        {
            if (RegistryHelper.IsSubKeyExists(Registry.CurrentUser, RegistryPath))
            {
                int value = Convert.ToInt32(RegistryHelper.GetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName));

                if (value == 0)
                {
                    Log.Debug($"{PrinterName} is available!");

                    return true;
                }
            }

            return false;
        }

    }
}
