using Autodesk.Revit.DB;
using PaperSize = System.Drawing.Printing.PaperSize;

namespace RevitBIMTool.Models;

internal class SheetModel : IDisposable
{
    private bool _isDisposed;

    /// <summary>
    /// Получает ViewSheet Revit
    /// </summary>
    public ViewSheet ViewSheet { get; private set; }

    /// <summary>
    /// Получает размер бумаги листа
    /// </summary>
    public PaperSize SheetPapeSize { get; }

    /// <summary>
    /// Получает ориентацию листа
    /// </summary>
    public PageOrientationType SheetOrientation { get; }

    /// <summary>
    /// Получает или устанавливает флаг валидности листа
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Получает или устанавливает имя листа
    /// </summary>
    public string SheetName { get; private set; }

    /// <summary>
    /// Получает или устанавливает числовой номер листа
    /// </summary>
    public double DigitNumber { get; private set; }

    /// <summary>
    /// Получает или устанавливает строковый номер листа
    /// </summary>
    public string StringNumber { get; private set; }

    /// <summary>
    /// Получает имя формата бумаги
    /// </summary>
    public string PaperName => SheetPapeSize?.PaperName;

    /// <summary>
    /// Получает или устанавливает имя организационной группы
    /// </summary>
    public object OrganizationGroupName { get; private set; }

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
    /// Инициализирует новый экземпляр класса SheetModel
    /// </summary>
    /// <param name="sheet">Лист Revit</param>
    public SheetModel(ViewSheet sheet)
    {
        ViewSheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса SheetModel с указанным размером и ориентацией
    /// </summary>
    public SheetModel(ViewSheet sheet, PaperSize size, PageOrientationType orientation) : this(sheet)
    {
        SheetPapeSize = size ?? throw new ArgumentNullException(nameof(size));
        SheetOrientation = orientation;
    }

    public string GetFormatNameWithSheetOrientation()
    {
        string orientationText = Enum.GetName(typeof(PageOrientationType), SheetOrientation);
        string formatName = $"{PaperName} {orientationText}";
        return formatName;
    }

    /// <summary>
    /// Устанавливает свойства листа на основе данных
    /// </summary>
    internal void SetProperties(string sheetName, string stringNumber, double digitNumber, object groupName, bool isValid)
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
        if (disposing && ViewSheet != null)
        {
            ViewSheet.Dispose();
            ViewSheet = null;
        }

        _isDisposed = true;
    }

    /// <summary>
    /// Деструктор
    /// </summary>
    ~SheetModel()
    {
        Dispose(false);
    }
}