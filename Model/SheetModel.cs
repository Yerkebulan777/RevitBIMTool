using Autodesk.Revit.DB;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.Globalization;
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
    public string TempPath { get; internal set; }
    public string PaperName => SheetPapeSize.PaperName;
    public object OrganizationGroupName { get; internal set; }


    public static string GetSheetNumber(ViewSheet sheet)
    {
        string sheetNumber = StringHelper.ReplaceInvalidChars(sheet.SheetNumber);

        if (!string.IsNullOrWhiteSpace(sheetNumber))
        {
            sheetNumber = sheetNumber.TrimStart('0');
            sheetNumber = sheetNumber.TrimEnd('.');
        }

        return sheetNumber.Trim();
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
        string sheetName = StringHelper.ReplaceInvalidChars(ViewSheet.Name);
        projectName = projectName.Substring(0, Math.Min(30, projectName.Length));

        sheetName = string.IsNullOrWhiteSpace(groupName)
            ? StringHelper.NormalizeLength($"{projectName} - Лист - {sheetNumber} - {sheetName}")
            : StringHelper.NormalizeLength($"{projectName} - Лист - {groupName}-{sheetNumber} - {sheetName}");

        OrganizationGroupName = groupName;

        if (string.IsNullOrWhiteSpace(groupName))
        {
            OrganizationGroupName = Regex.Replace(sheetNumber, @"[0-9.]", string.Empty);
        }

        string digitNumber = Regex.Replace(sheetNumber, @"[^0-9.]", string.Empty);

        SheetName = string.IsNullOrEmpty(extension) ? sheetName : $"{sheetName}.{extension}";

        if (double.TryParse(digitNumber, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
        {
            Log.Debug($"{sheetNumber} = {digitNumber} > {number}");

            if (!groupName.StartsWith("#") && number < 500)
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
