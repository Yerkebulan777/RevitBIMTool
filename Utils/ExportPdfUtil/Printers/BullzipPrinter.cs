using RevitBIMTool.Utils.ExportPdfUtil;
using Serilog;
using System.Runtime.InteropServices;

namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal class BullzipPrinter : PrinterBase
    {
        private readonly string registryKey = @"SOFTWARE\Bullzip\PDF Printer\Settings";
        public override string Name => "Bullzip PDF Printer";

        private dynamic pdfPrinter;


        public override void InitializePrinter()
        {
            if (RegistryHelper.IsRegistryKeyExists(registryKey))
            {
                try
                {
                    pdfPrinter = Activator.CreateInstance(Type.GetTypeFromProgID("Bullzip.PDFPrinterSettings"));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error occurred while initializing the printer: {ex.Message}");
                }

                return;
            }

            throw new InvalidOperationException($"Registry key not found for printer: {Name}");
        }


        public override void ResetPrinterSettings()
        {
            try
            {
                pdfPrinter.RemoveSettings(true);
                pdfPrinter.WriteSettings(true);
            }
            catch (Exception ex)
            {
                Log.Error($"Error occurred while resetting printer settings: {ex.Message}");
            }
            finally
            {
                if (pdfPrinter != null)
                {
                    Marshal.ReleaseComObject(pdfPrinter);
                    pdfPrinter = null;
                }
            }
        }


        public override void SetPrinterOutput(string filePath)
        {
            try
            {
                pdfPrinter.SetValue("ShowPdf", "no");
                pdfPrinter.SetValue("Output", filePath);
                pdfPrinter.SetValue("ShowProgress", "no");
                pdfPrinter.SetValue("ShowSettings", "never");
                pdfPrinter.SetValue("ShowProgressFinished", "no");
                pdfPrinter.WriteSettings(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error occurred while setting the output file path: {ex.Message}");
            }
            finally
            {
                Thread.Sleep(100);
            }
        }

    }

}
