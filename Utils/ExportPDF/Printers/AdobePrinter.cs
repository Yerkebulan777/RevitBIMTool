﻿using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class AdobePdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Adobe\Acrobat Distiller\Printer";
    public override string PrinterName => "Adobe PDF";
    public override bool IsInternalPrinter => false;


    public override void InitializePrinter()
    {
        Log.Debug("Initialize Adobe PDF printer");

        PrinterStateManager.ReservePrinter(PrinterName);

        string autoSave = Path.Combine(RegistryPath, "AutoSave");
        string outputDirKey = Path.Combine(RegistryPath, "OutputDir");
        string promptUserKey = Path.Combine(RegistryPath, "PromptForAdobePDF");

        RegistryHelper.SetValue(Registry.CurrentUser, autoSave, "Enabled", "True");
        RegistryHelper.SetValue(Registry.CurrentUser, promptUserKey, "Enabled", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, outputDirKey, "Directory", "<InputFilePath>");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<InputFilename>");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "ShowAllNotifications", "False");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "SkipPrintDialog", "True");
    }


    public override void ReleasePrinterSettings()
    {
        Log.Debug("Release print settings");

        PrinterStateManager.ReleasePrinter(PrinterName);

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", desktop);
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "FileNameTemplate", "<Title>");
    }


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string directory = folder.Replace("\\", "\\\\");
        RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "OutputDir", directory);
        return PrintHelper.ExecutePrint(doc, model, folder);
    }
}
