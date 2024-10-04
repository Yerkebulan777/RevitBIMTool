using Autodesk.Revit.DB;
using Microsoft.Win32;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPdfUtil.Printers;
using RevitBIMTool.Utils.SystemHelpers;


namespace RevitBIMTool.Utils.ExportPDF.Printers
{
    internal sealed class InternalPrinter : PrinterControl
    {
        public override string StatusPath => throw new NotImplementedException();
        public override string RegistryPath => @"SOFTWARE\Autodesk\Revit";
        public override string PrinterName => "Microsoft Print to PDF";
        public override int OverallRating => 5;


        public override void InitializePrinter()
        {
            RegistryHelper.SetValue(Registry.CurrentUser, StatusPath, "StatusMonitor", 1);
        }


        public override void ResetPrinterSettings()
        {
        }


        public override bool Print(Document doc, string folder, SheetModel model)
        {
#if R23
            return PrintHandler.ExportSheet(doc, folder, model);
#else
            return PrintHandler.PrintSheet(doc, folder, model);
#endif
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }


        public override bool IsPrinterEnabled()
        {
            return true;
        }

    }

}
