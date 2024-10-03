﻿using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using RevitBIMTool.Utils.ExportPDF;
using Serilog;
using System.Runtime.InteropServices;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal class BullzipPrinter : PrinterControl
    {
        public override string RegistryPath => @"SOFTWARE\Bullzip\PDF Printer\Settings";
        public override string RegistryName => "Bullzip PDF Printer";
        public override int OverallRating => 3;

        private dynamic pdfPrinter;


        public override void InitializePrinter()
        {
            try
            {
                pdfPrinter = Activator.CreateInstance(Type.GetTypeFromProgID("Bullzip.PDFPrinterSettings"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error occurred while initializing the printer: {ex.Message}");
            }
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


        public override bool Print(Document doc, string folder, SheetModel model)
        {
            return PrintHandler.PrintSheet(doc, folder, model);
        }


        public override bool IsPrinterInstalled()
        {
            return base.IsPrinterInstalled();
        }


        public override bool IsPrinterEnabled()
        {
            return base.IsPrinterEnabled();
        }

    }

}