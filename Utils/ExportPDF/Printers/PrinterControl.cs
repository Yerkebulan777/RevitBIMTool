﻿using Autodesk.Revit.DB;
using RevitBIMTool.Model;


namespace RevitBIMTool.Utils.ExportPdfUtil.Printers
{
    internal abstract class PrinterControl
    {
        public abstract string Name { get; }

        public abstract int OverallRating { get; }

        public abstract void InitializePrinter();


        public abstract void ResetPrinterSettings();


        public abstract void SetPrinterOutput(string filePath);


        public abstract bool Print(Document doc, string folder, SheetModel model);

    }
}
