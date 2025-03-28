﻿using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class InternalPrinter : PrinterControl
{
    public override string RegistryPath => "Undefined registry path";
    public override string PrinterName => "Internal Printer";
    public override bool IsInternalPrinter => true;


    public override bool DoPrint(Document doc, SheetModel model, string folder)
    {
        try
        {
            return PrintHelper.ExportSheet(doc, model, folder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error export to pdf: {SheetName}", model.SheetName);
            throw new InvalidOperationException(model.SheetName, ex);
        }
    }


    public override void InitializePrinter()
    {
        Log.Debug("Initialize PDFCreator printer");

        PrinterStateManager.ReleasePrinter(PrinterName);
    }


    public override void ReleasePrinterSettings()
    {
        Log.Debug("Release print settings");

        PrinterStateManager.ReleasePrinter(PrinterName);
    }


}
