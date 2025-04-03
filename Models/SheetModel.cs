using Autodesk.Revit.DB;
using Serilog;
using PaperSize = System.Drawing.Printing.PaperSize;

namespace RevitBIMTool.Models;

public class SheetModel : IDisposable
{
    public readonly ViewSheet ViewSheet;

    public readonly PaperSize SheetPapeSize;

    public readonly PageOrientationType SheetOrientation;


    public SheetModel(ViewSheet sheet)
    {
        ViewSheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    public SheetModel(ViewSheet sheet, PaperSize size, PageOrientationType orientation) : this(sheet)
    {
        SheetPapeSize = size ?? throw new ArgumentNullException(nameof(size));
        SheetOrientation = orientation;
    }

    /// <summary>
    /// Получает или устанавливает флаг валидности листа
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Получает или устанавливает имя листа
    /// </summary>
    public string SheetName { get; set; }

    /// <summary>
    /// Получает или устанавливает числовой номер листа
    /// </summary>
    public double DigitNumber { get; set; }

    /// <summary>
    /// Получает или устанавливает строковый номер листа
    /// </summary>
    public string StringNumber { get; set; }

    /// <summary>
    /// Получает или устанавливает имя организационной группы
    /// </summary>
    public string OrganizationGroupName { get; set; }

    /// <summary>
    /// Получает или устанавливает флаг включения цвета
    /// </summary>
    public bool IsColorEnabled { get; set; }

    /// <summary>
    /// Получает или устанавливает флаг успешности операции
    /// </summary>
    public bool IsSuccessfully { get; set; }

    /// <summary>
    /// Получает или устанавливает путь к файлу
    /// </summary>
    public string TempFilePath { get; set; }

    /// <summary>
    /// Получает или устанавливает путь к файлу Revit
    /// </summary>
    public string RevitFilePath { get; set; }

    /// <summary>
    /// Получает имя формата бумаги
    /// </summary>
    public string PaperName => SheetPapeSize?.PaperName;

    /// <summary>
    /// Устанавливает свойства листа на основе данных
    /// </summary>
    internal void SetProperties(string sheetName, string stringNumber, double digitNumber, string groupName, bool isValid)
    {
        OrganizationGroupName = groupName;
        StringNumber = stringNumber;
        DigitNumber = digitNumber;
        SheetName = sheetName;
        IsValid = isValid;
    }


    /// <summary>
    /// Освобождает ресурсы, используемые объектом SheetModel
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Освобождает управляемые и неуправляемые ресурсы
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            ViewSheet?.Dispose();
        }
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~SheetModel()
    {
        Dispose(false);
    }

}