﻿using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal abstract class PrinterControl
{
    public abstract string RegistryPath { get; }
    public abstract string PrinterName { get; }

    public abstract void InitializePrinter();

    public abstract void ResetPrinterSettings();

    public abstract bool DoPrint(Document doc, SheetModel model);

    public virtual bool IsPrinterInstalled()
    {
        const string printersPath = @"SYSTEM\CurrentControlSet\Control\DoPrint\Printers";

        bool isInstalled = RegistryHelper.IsKeyExists(Registry.LocalMachine, Path.Combine(printersPath, PrinterName));

        bool isPathExists = RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath);

        Log.Debug($"Is {PrinterName} isInstalled {isInstalled} and {RegistryPath} exists {isPathExists}!");

        return isInstalled && isPathExists;
    }

    public virtual bool IsAvailable()
    {
        if (IsPrinterInstalled())
        {
            if (PrinterStateManager.IsPrinterAvailable(PrinterName))
            {
                Log.Debug($"{PrinterName} printer is available!");

                return true;
            }
        }

        Log.Debug($"{PrinterName} printer not available!");

        return false;
    }

}
