using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;

namespace RevitBIMTool.Utils.ExportPDF.Printers;

internal sealed class InternalPrinter : PrinterControl
{
    public override string RegistryPath => "Undefined registry path";
    public override string PrinterName => "Internal Printer";
    public override string RevitFilePath { get; set; }
    public override bool IsInternal => true;

    public override bool DoPrint(Document doc, SheetModel model)
    {
        return PrintHelper.ExportSheet(doc, model);
    }


    public override void InitializePrinter()
    {
        /// No need to initialize the internal printer
    }

    public override void ResetPrinterSettings()
    {
        /// No need to reset the internal printer settings
    }
}
