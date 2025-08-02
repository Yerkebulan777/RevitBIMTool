using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using Serilog;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class InternalPrinter : PrinterControl
{
    public override string RegistryPath => "Undefined registry path";
    public override string PrinterName => "Internal Printer";
    public override bool IsInternalPrinter => true;
    public override string RevitFilePath { get; set; }


    public override void InitializePrinter(string revitFilePath)
    {
        Log.Information("Printer {Printer} initialized!", PrinterName);

        RevitFilePath = revitFilePath;
    }


    public override void RestoreDefaultSettings()
    {
        // Не требует освобождения настроек!
        // Его нет в базе данных принтеров!
    }


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


}
