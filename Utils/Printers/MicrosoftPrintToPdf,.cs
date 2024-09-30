using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitBIMTool.Utils.Printers
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
            throw new NotImplementedException();
        }
    }
}
