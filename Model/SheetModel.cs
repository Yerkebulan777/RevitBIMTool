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
    public double DigitNumber { get; private set; }
    public string StringNumber { get; private set; }
    public string SheetFullName { get; private set; }
    public string PaperName => SheetPapeSize.PaperName;
    public object OrganizationGroupName { get; internal set; }


    public static string GetSheetNumber(ViewSheet sequenceSheet)
    {
        string stringNumber = sequenceSheet?.SheetNumber.TrimStart('0');

        if (!string.IsNullOrEmpty(stringNumber))
        {
            string invalidChars = new(Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray());
            string escapedInvalidChars = Regex.Escape(invalidChars);
            Regex regex = new($"(?<=\\d){escapedInvalidChars}");
            stringNumber = regex.Replace(stringNumber, ".");
            
        }

        return stringNumber;
    }


    public static string GetOrganizationGroupName(Document doc, ViewSheet viewSheet)
    {
        Regex matchPrefix = new(@"^(\s*)");
        StringBuilder stringBuilder = new();
        BrowserOrganization organization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(doc);
        foreach (FolderItemInfo folderInfo in organization.GetFolderItems(viewSheet.Id))
        {
            if (folderInfo.IsValidObject)
            {
                string folderName = folderInfo.Name;
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

        SheetFullName = string.IsNullOrWhiteSpace(groupName)
            ? StringHelper.NormalizeLength($"Лист - {sheetNumber} - {sheetName}.{extension}")
            : StringHelper.NormalizeLength($"Лист - {groupName}-{sheetNumber} - {sheetName}.{extension}");

        string sheetDigits = Regex.Replace(sheetNumber, @"[^0-9.]", string.Empty);

        OrganizationGroupName = groupName;

        if (string.IsNullOrWhiteSpace(groupName))
        {
            OrganizationGroupName = Regex.Replace(sheetNumber, @"[0-9.]", string.Empty);
        }

        if (double.TryParse(sheetDigits, out double number))
        {
            if (!groupName.StartsWith("#"))
            {
                StringNumber = sheetNumber;
                DigitNumber = number;
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
            .Where(sm => sm.IsValid)
            .OrderBy(sm => sm.OrganizationGroupName)
            .ThenBy(sm => sm.DigitNumber).ToList();
    }


    public void Dispose()
    {
        ViewSheet.Dispose();
    }



}
