using Autodesk.Revit.DB;
using PaperSize = System.Drawing.Printing.PaperSize;

namespace RevitBIMTool.Models;

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
    public List<SheetModel> Sheets { get; } = [];

    /// <summary>
    /// Возвращает тип цвета для настройки печати
    /// </summary>
    public ColorDepthType GetColorDepthType()
    {
        return IsColorEnabled ? ColorDepthType.Color : ColorDepthType.BlackLine;
    }


}