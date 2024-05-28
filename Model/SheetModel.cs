using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using Serilog;
using System.IO;
using System.Text.RegularExpressions;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitBIMTool.Model;
internal class SheetModel : IDisposable
{
    public readonly ViewSheet ViewSheet;
    public readonly int SequenceNumber;
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


    public double SheetDigit { get; private set; }
    public string SheetNumber { get; private set; }
    public string SheetFileName { get; private set; }
    public string PaperName => SheetPapeSize.PaperName;


    public string GetSheetNameWithExtension()
    {
        string groupName = StringHelper.ReplaceInvalidChars(OrganizationGroupName);
        string sheetName = StringHelper.ReplaceInvalidChars(ViewSheet.get_Parameter(BuiltInParameter.SHEET_NAME).AsString());
        string sheetNumber = StringHelper.ReplaceInvalidChars(ViewSheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString());

        SheetFileName = StringHelper.NormalizeLength($"Лист - {groupName}-{sheetNumber} - {sheetName}.pdf");

        string sheetDigits = Regex.Replace(sheetNumber, @"[^0-9.]", string.Empty);

        if (double.TryParse(sheetDigits, out double number))
        {
            Log.Information($"{SheetFileName} ({number})");
            SheetNumber = sheetNumber.TrimStart('0');
            SheetDigit = number;
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
