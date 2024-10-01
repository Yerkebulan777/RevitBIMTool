namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal sealed class MicrosoftPrinter : PrinterBase
    {
        public override string Name => "Microsoft Print to PDF";


        public override void InitializePrinter()
        {
            throw new NotImplementedException();
        }


        public override void ResetPrinterSettings()
        {
            throw new NotImplementedException();
        }


        public override void SetPrinterOutput(string filePath)
        {
            Thread.Sleep(100);
        }
    }
}
