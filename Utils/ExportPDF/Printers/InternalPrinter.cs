using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class InternalPrinter : PrinterControl
{
    public override string RegistryPath => "Undefined registry path";
    public override string PrinterName => "Internal Printer";
    public override string RevitFilePath { get; set; }
    public override bool IsInternal => true;

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
        PrinterStateManager.ReleasePrinter(PrinterName);
    }

    public override void ResetPrinterSettings()
    {
        PrinterStateManager.ReleasePrinter(PrinterName);
    }
}
