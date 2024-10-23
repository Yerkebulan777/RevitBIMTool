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

            bool result = RegistryHelper.IsSubKeyExists(Registry.LocalMachine, Path.Combine(printersPath, PrinterName));

            Log.Debug($"Is {PrinterName} printer installed {result}");

            return result;

        }


        public virtual bool IsAvailable()
        {
            if (RegistryHelper.IsSubKeyExists(Registry.CurrentUser, RegistryPath) && IsPrinterInstalled())
            {
                if (0 == Convert.ToInt32(RegistryHelper.GetValue(Registry.CurrentUser, PrintHandler.StatusPath, PrinterName)))
                {
                    Log.Debug($"{PrinterName} is available!");
                    return true;
                }
            }

            return false;
        }

    }
}
