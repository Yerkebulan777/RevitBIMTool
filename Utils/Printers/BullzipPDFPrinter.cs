using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitBIMTool.Utils.Printers
{
    internal class BullzipPDFPrinter : PrinterBase
    {
        
        public override string Name => "Bullzip PDF Printer";

        public override void ResetPrinterSettings()
        {
            throw new NotImplementedException();
        }


        public override void SetPrinterOutput(string filePath)
        {
            string iniPath = @"C:\path\to\bullzip.ini";

            System.IO.File.WriteAllText(iniPath, $"[Settings]\nOutput={filePath}\nShowSaveDialog=no");
        }
    }
}
