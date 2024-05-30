using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using Serilog;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitBIMTool.Model;
internal class SheetModel : IDisposable
{
    public readonly ViewSheet ViewSheet;
    public SheetModel(ViewSheet sheet)
    {
        ViewSheet = sheet;
    }

    public readonly PaperSize SheetPapeSize;
    public readonly PageOrientationType SheetOrientation;
    public SheetModel(ViewSheet sheet, PaperSize papeSize, PageOrientationType orientType)
    {
        ViewSheet = sheet;
        SheetPapeSize = papeSize;
        SheetOrientation = orientType;
    }

    public double SheetDigit { get; private set; }
    public string SheetNumber { get; private set; }
    public string SheetFileName { get; private set; }
    public string PaperName => SheetPapeSize.PaperName;
    public object OrganizationGroupName { get; internal set; }


    public static string GetSheetNumber(ViewSheet sequenceSheet)
    {
        string stringNumber = sequenceSheet?.SheetNumber;

        if (!string.IsNullOrEmpty(stringNumber))
        {
            string invalidChars = new(Path.GetInvalidFileNameChars());
            string escapedInvalidChars = Regex.Escape(invalidChars);
            Regex regex = new($"(?<=\\d){escapedInvalidChars}");
            stringNumber = regex.Replace(stringNumber, ".");
        }

        return stringNumber.Trim();
    }


    public static string GetOrganizationGroupName(Document doc, ViewSheet viewSheet)
    {
        StringBuilder stringBuilder = new();
        Regex matchPrefix = new(@"^(\d\s)|(\.\w+)|(\s*)");
        BrowserOrganization organization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(doc);
        foreach (FolderItemInfo folderInfo in organization.GetFolderItems(viewSheet.Id))
        {
            if (folderInfo.IsValidObject)
            {
                string folderName = StringHelper.ReplaceInvalidChars(folderInfo.Name);
                folderName = matchPrefix.Replace(folderName, string.Empty);
                _ = stringBuilder.Append(folderName);
            }
        }

        return stringBuilder.ToString();
    }


    public string GetSheetNameWithExtension(Document doc, string extension)
    {
        string sheetNumber = GetSheetNumber(ViewSheet);
        string groupName = GetOrganizationGroupName(doc, ViewSheet);
        string sheetName = StringHelper.ReplaceInvalidChars(ViewSheet.get_Parameter(BuiltInParameter.SHEET_NAME).AsString());

        SheetFileName = StringHelper.NormalizeLength($"Лист - {groupName}-{sheetNumber} - {sheetName}.{extension}");

        string sheetDigits = Regex.Replace(sheetNumber, @"[^0-9.]", string.Empty);

        if (double.TryParse(sheetDigits, out double number))
        {
            Log.Information($"{SheetFileName} ({number})");
            SheetNumber = sheetNumber.TrimStart('0');
            OrganizationGroupName = groupName;
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


    public static List<SheetModel> SortSheetModels(List<SheetModel> sheetModels)
    {
        return sheetModels
            .OrderBy(sm => sm.OrganizationGroupName).ThenBy(sm => sm.SheetNumber.Length)
            .ThenBy(sm => sm.SheetNumber, StringComparer.OrdinalIgnoreCase).ToList();
    }


    public void Dispose()
    {
        ViewSheet.Dispose();
    }


}
