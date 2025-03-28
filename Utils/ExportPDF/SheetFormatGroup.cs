using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using PaperSize = System.Drawing.Printing.PaperSize;

namespace RevitBIMTool.Utils.ExportPDF;

public class SheetFormatGroup
{
    /// <summary>
    /// Имя формата
    /// </summary>
    public string FormatName { get; set; }

    /// <summary>
    /// Размер бумаги
    /// </summary>
    public PaperSize PaperSize { get; set; }

    /// <summary>
    /// Флаг цветной печати
    /// </summary>
    public bool IsColorEnabled { get; set; }

    /// <summary>
    /// Ориентация листа
    /// </summary>
    public PageOrientationType Orientation { get; set; }

    /// <summary>
    /// Список листов в этой группе
    /// </summary>
    public List<SheetModel> SheetList { get; } = [];

}