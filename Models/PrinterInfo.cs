namespace RevitBIMTool.Utils.ExportPDF;

internal static partial class PrinterManager
{
    // Структура для хранения информации о принтерах
    private class PrinterInfo
    {
        public string PrinterName { get; set; }
        public DateTime LastSuccessTime { get; set; }
        public int SuccessCount { get; set; }
    }
}