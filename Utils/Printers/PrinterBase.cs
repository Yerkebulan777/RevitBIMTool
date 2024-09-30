using Org.BouncyCastle.Asn1.Mozilla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RevitBIMTool.Utils.Printers
{
    public abstract class PrinterBase
    {
        public abstract string Name { get; }

        public abstract void ResetPrinterSettings();

        public abstract void SetPrinterOutput(string filePath);

    }
}
