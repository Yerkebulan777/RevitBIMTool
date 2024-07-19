using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitBIMTool.Model;
internal class SheetModel : IDisposable
{
    public ViewSheet ViewSheet { get; }
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
    public string SheetName { get; private set; }
    public double DigitNumber { get; private set; }
    public string StringNumber { get; private set; }
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

        return StringHelper.ReplaceInvalidChars(stringBuilder.ToString());
    }


    public void SetSheetName(Document doc, string projectName, string extension = null)
    {
        string sheetNumber = GetSheetNumber(ViewSheet);
        string groupName = GetOrganizationGroupName(doc, ViewSheet);
        string shortName = projectName.Substring(0, Math.Min(30, projectName.Length));
        string sheetName = StringHelper.ReplaceInvalidChars(ViewSheet.Name);

        sheetName = string.IsNullOrWhiteSpace(groupName)
            ? StringHelper.NormalizeLength($"{shortName} - Лист - {sheetNumber} - {sheetName}")
            : StringHelper.NormalizeLength($"{shortName} - Лист - {groupName}-{sheetNumber} - {sheetName}");

        OrganizationGroupName = groupName;

        if (string.IsNullOrWhiteSpace(groupName))
        {
            OrganizationGroupName = Regex.Replace(sheetNumber, @"[0-9.]", string.Empty);
        }

        sheetName = string.IsNullOrEmpty(extension) ? sheetName : $"{sheetName}.{extension}";

        string sheetDigits = Regex.Replace(sheetNumber, @"[^0-9.]", string.Empty);

        SheetName = StringHelper.ReplaceInvalidChars(sheetName);

        if (double.TryParse(sheetDigits, out double number))
        {
            if (number < 300 && !groupName.StartsWith("#"))
            {
                IsValid = ViewSheet.CanBePrinted;
                StringNumber = sheetNumber;
                DigitNumber = number;
            }
        }
    }


    public string GetFormatNameWithSheetOrientation()
    {
        string orientationText = Enum.GetName(typeof(PageOrientationType), SheetOrientation);
        string formatName = $"{PaperName} {orientationText}";

        return formatName;
    }


    public static string FindFileInDirectory(string directory, string fileName)
    {
        string foundFile = null;

        if (Directory.Exists(directory))
        {
            IEnumerable<string> files = Directory.EnumerateFiles(directory);
            foundFile = files.FirstOrDefault(file => file.Contains(fileName));
        }

        Debug.WriteLineIf(string.IsNullOrEmpty(foundFile), $"Not founded file: {fileName}");

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
