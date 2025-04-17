﻿using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using System.Diagnostics;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class AdobePdfPrinter : PrinterControl
{
    public override string RegistryPath => @"SOFTWARE\Adobe\Acrobat Distiller\PrinterJobControl";
    private string RevitExePath => $@"C:\Program Files\Autodesk\Revit {RevitBimToolApp.Version}\Revit.exe";
    public override string PrinterName => "Adobe PDF";
    public override bool IsInternalPrinter => false;


    public override void InitializePrinter()
    {
        PrinterStateManager.ReservePrinter(PrinterName);

        if (!RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath))
        {
            Log.Warning("PrinterJobControl not found");
        }
    }

    public override void ReleasePrinterSettings()
    {
        Log.Debug("Release printer");
        PrinterStateManager.ReleasePrinter(PrinterName);
    }

    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        string fullFilePath = Path.Combine(folder, model.SheetName);

        SetPDFSettings(fullFilePath, folder);
        Log.Debug("PDF destination: {0}", fullFilePath);
        return PrintHelper.ExecutePrint(doc, model, folder);
    }


    private void SetPDFSettings(string destFileName, string dirName)
    {
        if (!RegistryHelper.IsKeyExists(Registry.CurrentUser, RegistryPath))
        {
            Log.Warning("PrinterJobControl missing");
            return;
        }

        try
        {
            // Значение полного пути для ключа Revit.exe
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, RevitExePath, destFileName);

            // Значение директории для ключа LastPdfPortFolder
            RegistryHelper.SetValue(Registry.CurrentUser, RegistryPath, "LastPdfPortFolder - Revit.exe", dirName);

            Log.Debug("PDF settings applied");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Registry access error");
            throw new InvalidOperationException("PDF registry settings failed", ex);
        }
    }


}