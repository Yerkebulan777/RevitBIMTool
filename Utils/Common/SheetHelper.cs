﻿using Autodesk.Revit.DB;
using RevitBIMTool.Models;
using Serilog;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RevitBIMTool.Utils.Common;

internal static class SheetHelper
{

    #region Форматирование имен и номеров листов

    /// <summary>
    /// Получает номер листа
    /// </summary>
    public static string GetSheetNumber(ViewSheet sheet)
    {
        if (sheet == null)
        {
            throw new ArgumentNullException(nameof(sheet));
        }

        string sheetNumber = StringHelper.ReplaceInvalidChars(sheet.SheetNumber);

        if (!string.IsNullOrWhiteSpace(sheetNumber))
        {
            sheetNumber = sheetNumber.TrimStart('0');
            sheetNumber = sheetNumber.TrimEnd('.');
        }

        return sheetNumber.Trim();
    }

    /// <summary>
    /// Получает имя организационной группы
    /// </summary>
    public static string GetOrganizationGroupName(Document doc, ViewSheet viewSheet)
    {
        Regex matchPrefix = new(@"^(\s*)");
        StringBuilder stringBuilder = new();

        try
        {
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
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
        }

        return StringHelper.ReplaceInvalidChars(stringBuilder.ToString());
    }

    /// <summary>
    /// Форматирует имя листа по заданным параметрам
    /// </summary>
    public static string FormatSheetName(Document doc, ViewSheet viewSheet, string projectName, string extension = null)
    {
        string sheetNumber = GetSheetNumber(viewSheet);

        string groupName = GetOrganizationGroupName(doc, viewSheet);

        string sheetTitle = string.IsNullOrWhiteSpace(groupName)
            ? StringHelper.NormalizeLength($"{projectName} - Лист-{sheetNumber} - {viewSheet.Name}")
            : StringHelper.NormalizeLength($"{projectName} - Лист - {groupName}-{sheetNumber} - {viewSheet.Name}");

        return StringHelper.ReplaceInvalidChars(string.IsNullOrEmpty(extension) ? sheetTitle : $"{sheetTitle}.{extension}");
    }

    /// <summary>
    /// Парсит номер листа для получения числового значения
    /// </summary>
    public static double ParseSheetNumber(string sheetNumber)
    {
        if (string.IsNullOrEmpty(sheetNumber))
        {
            return 0;
        }

        string digitNumber = Regex.Replace(sheetNumber, @"[^0-9,.]", string.Empty);

        return double.TryParse(digitNumber, NumberStyles.Any, CultureInfo.InvariantCulture, out double number) ? number : 0;
    }

    /// <summary>
    /// Получает имя формата с ориентацией листа
    /// </summary>
    public static string GetFormatNameWithOrientation(string paperName, PageOrientationType orientation)
    {
        string orientationText = Enum.GetName(typeof(PageOrientationType), orientation);

        return $"{paperName} {orientationText}";
    }

    #endregion


    #region Создание и инициализация моделей листов

    /// <summary>
    /// Устанавливает имя листа на основе документа и имени проекта
    /// </summary>
    public static void SetSheetName(this SheetModel model, Document doc, string projectName, string extension = null)
    {
        string sheetNumber = GetSheetNumber(model.ViewSheet);
        string groupName = GetOrganizationGroupName(doc, model.ViewSheet);
        string sheetName = FormatSheetName(doc, model.ViewSheet, projectName, extension);

        double digitNumber = 0;

        bool isValid = false;

        if (!groupName.StartsWith("#"))
        {
            digitNumber = ParseSheetNumber(sheetNumber);

            if (digitNumber is > 0 and < 500)
            {
                isValid = model.ViewSheet.CanBePrinted;
            }
        }

        string organizationGroup = groupName;

        if (string.IsNullOrWhiteSpace(groupName))
        {
            organizationGroup = Regex.Replace(sheetNumber, @"[0-9.]", string.Empty);
        }

        model.SetProperties(sheetName, sheetNumber, digitNumber, organizationGroup, isValid);
    }

    #endregion


    #region Операции с коллекциями листов

    /// <summary>
    /// Сортирует модели листов 
    /// </summary>
    public static List<SheetModel> SortSheetModels(List<SheetModel> sheetModels)
    {
        return sheetModels?
            .Where(sm => sm.IsValid)
            .OrderBy(sm => sm.OrganizationGroupName)
            .ThenBy(sm => sm.DigitNumber).ToList();
    }

    #endregion

}