using Serilog;

namespace RevitBIMTool.Utils.Printers
{
    internal class BullzipPrinter : PrinterBase
    {
        private dynamic pdfPrinter;

        public override string Name => "Bullzip PDF Printer";


        public override void InitializePrinter()
        {
            try
            {
                pdfPrinter = Activator.CreateInstance(Type.GetTypeFromProgID("Bullzip.PDFPrinterSettings"));

                if (pdfPrinter is null)
                {
                    throw new Exception("Failed to initialize Bullzip PDF Printer");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error occurred while initializing the printer: {ex.Message}");
            }
        }


        public override void ResetPrinterSettings()
        {
            try
            {
                pdfPrinter.ResetSettings();
                pdfPrinter.WriteSettings(true);
                Log.Debug("Printer settings have been reset.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error occurred while resetting printer settings: {ex.Message}");
            }
        }


        public override void SetPrinterOutput(string filePath)
        {
            try
            {
                pdfPrinter.SetValue("Output", filePath);
                pdfPrinter.WriteSettings(true);
                Log.Debug("Set output path");
            }
            catch (Exception ex)
            {
                Log.Error($"Error occurred while setting the output file path: {ex.Message}");
            }
        }


    }
}
