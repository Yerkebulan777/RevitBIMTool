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

            bool isInstalled = RegistryHelper.IsKeyExists(Registry.LocalMachine, Path.Combine(printersPath, PrinterName));

            bool isPathExists = RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath);

            Log.Debug($"Is {PrinterName} isInstalled {isInstalled} and {RegistryPath} exists {isPathExists}!");

            return isInstalled && isPathExists;
        }


        public virtual bool IsAvailable()
        {
            if (IsPrinterInstalled())
            {
                if (0 == PrintHandler.GetPrinterStatus(this))
                {
                    Log.Debug($"{PrinterName} printer is available!");

                    return true;
                }
            }

            return false;
        }

    }
}
