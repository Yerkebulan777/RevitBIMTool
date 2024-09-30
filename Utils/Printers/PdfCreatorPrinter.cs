using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RevitBIMTool.Utils.Printers
{
    internal sealed class PdfCreatorPrinter : AbstractPrinter
    {
        public override string Name => "PDFCreator";


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
