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
    public SheetModel(ViewSheet sheet, PaperSize size, PageOrientationType orientation)
    {
        ViewSheet = sheet;
        SheetPapeSize = size;
        SheetOrientation = orientation;
    }


    public bool IsValid { get; private set; }
    public double SheetDigit { get; private set; }
    public string SheetNumber { get; private set; }
    public string SheetFullName { get; private set; }
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


    public void SetSheetNameWithExtension(Document doc, string extension)
    {
        string sheetNumber = GetSheetNumber(ViewSheet);
        string groupName = GetOrganizationGroupName(doc, ViewSheet);
        string sheetName = StringHelper.ReplaceInvalidChars(ViewSheet?.Name);

        SheetFullName = string.IsNullOrEmpty(groupName)
            ? StringHelper.NormalizeLength($"Лист - {sheetNumber} - {sheetName}.{extension}")
            : StringHelper.NormalizeLength($"Лист - {groupName}-{sheetNumber} - {sheetName}.{extension}");

        string sheetDigits = Regex.Replace(sheetNumber, @"[^0-9.]", string.Empty);

        if (double.TryParse(sheetDigits, out double number))
        {
            Log.Information($"{SheetFullName} ({number})");
            SheetNumber = sheetNumber.TrimStart('0');
            OrganizationGroupName = groupName;
            if (!groupName.StartsWith("#"))
            {
                SheetDigit = number;
                IsValid = true;
            }
        }
    }


    public string GetFormatNameWithSheetOrientation()
    {
        string orientationText = Enum.GetName(typeof(PageOrientationType), SheetOrientation);
        string formatName = $"{PaperName} {orientationText}";

        return formatName;
    }


    public static string FindFileInDirectory(string directory, string sheetName)
    {
        string foundFile = null;

        if (Directory.Exists(directory))
        {
            IEnumerable<string> files = Directory.EnumerateFiles(directory);
            foundFile = files.FirstOrDefault(file => file.Contains(sheetName));
        }

        return foundFile;
    }


    public static List<SheetModel> SortSheetModels(List<SheetModel> sheetModels)
    {
        return sheetModels
        .OrderBy(sm => sm.OrganizationGroupName).ThenBy(sm => sm.SheetNumber.Length)
        .ThenBy(sm => sm.SheetNumber, StringComparer.OrdinalIgnoreCase)
        .Where(sm => sm.IsValid).ToList();
    }


    public void Dispose()
    {
        ViewSheet.Dispose();
    }



}
