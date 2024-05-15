using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using System.IO;
using System.Text.RegularExpressions;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitBIMTool.Model;
internal class SheetModel : IDisposable
{
    public readonly ViewSheet ViewSheet;
    public readonly PaperSize SheetPapeSize;
    public readonly string OrganizationGroupName;
    public readonly PageOrientationType SheetOrientation;


    public SheetModel(ViewSheet sheet, PaperSize papeSize, PageOrientationType orientType, string groupName)
    {
        ViewSheet = sheet;
        SheetPapeSize = papeSize;
        SheetOrientation = orientType;
        OrganizationGroupName = groupName;
    }


    public double SheetNumber { get; private set; }
    public string SheetFileName { get; private set; }
    public string PaperName => SheetPapeSize.PaperName;


    public string GetSheetNameWithExtension()
    {
        string groupName = OrganizationGroupName;
        string sheetName = ViewSheet.get_Parameter(BuiltInParameter.SHEET_NAME).AsString();
        string sheetNumber = ViewSheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();

        SheetFileName = StringHelper.NormalizeText($"Лист - {groupName}-{sheetNumber} - {sheetName}.pdf");

        sheetNumber = Regex.Replace(sheetNumber.TrimStart('0'), @"[^0-9.]", string.Empty);

        if (double.TryParse(sheetNumber, out double number))
        {
            SheetNumber = number;
        }

        return SheetFileName;
    }


    public string GetFormatNameWithSheetOrientation()
    {
        string orientationText = Enum.GetName(typeof(PageOrientationType), SheetOrientation);
        string formatName = $"{PaperName} {orientationText}";

        return formatName;
    }


    public string FindFileInDirectory(string directory)
    {
        string foundFile = null;

        if (Directory.Exists(directory))
        {
            IEnumerable<string> files = Directory.EnumerateFiles(directory);
            foundFile = files.FirstOrDefault(file => file.Contains(SheetFileName));
        }

        return foundFile;
    }


    public void Dispose()
    {
        ViewSheet.Dispose();
    }

}
