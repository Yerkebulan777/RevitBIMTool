﻿using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using System.IO;
using System.Text;


namespace RevitBIMTool.ExportHandlers;
internal static class ExportToDWGHandler
{

    public static string ExportToDWG(Document document, string revitFilePath)
    {
        int printCount = 0;

        StringBuilder sb = new();

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportHelper.ExportDirectory(revitFilePath, "02_DWG", true);
        FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet)).WhereElementIsNotElementType();
        string exportFolder = Path.Combine(exportBaseDirectory, revitFileName);

        DWGExportOptions exportOptions = new()
        {
            Colors = ExportColorMode.TrueColorPerView,
            PropOverrides = PropOverrideMode.ByEntity,
            ACAPreference = ACAObjectPreference.Object,
            LineScaling = LineScaling.PaperSpace,
            ExportOfSolids = SolidGeometry.ACIS,
            TextTreatment = TextTreatment.Exact,
            TargetUnit = ExportUnit.Millimeter,
            FileVersion = ACADVersion.R2007,
            HideUnreferenceViewTags = true,
            HideReferencePlane = true,
            NonplotSuffix = "NPLT",
            LayerMapping = "AIA",
            HideScopeBox = true,
            MergedViews = true,
            SharedCoords = false,
            MarkNonplotLayers = false,
            PreserveCoincidentLines = false
        };

        int sheetCount = collector.GetElementCount();
        Log.Information($"All sheets => {sheetCount}");

        if (sheetCount > 0)
        {
            string sheetFullName = string.Empty;
            RevitPathHelper.EnsureDirectory(exportFolder);

            foreach (ViewSheet sheet in collector.Cast<ViewSheet>())
            {
                if (sheet.CanBePrinted)
                {
                    try
                    {
                        ICollection<ElementId> collection = [sheet.Id];
                        string sheetNum = ExportHelper.GetSheetNumber(sheet);
                        string sheetName = StringHelper.NormalizeLength(sheet.Name);

                        sheetFullName = $"{revitFileName} - Лист - {sheetNum} - {sheetName}";
                        sheetFullName = StringHelper.ReplaceInvalidChars(sheetFullName);

                        string sheetFullPath = Path.Combine(exportFolder, $"{sheetFullName}.dwg");

                        if (!ExportHelper.IsTargetFileUpdated(sheetFullPath, revitFilePath))
                        {
                            if (document.Export(exportFolder, sheetFullName, collection, exportOptions))
                            {
                                Log.Information($"Sheet: {sheetFullName} printed");
                                printCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"Sheet: {sheetFullName} failed: {ex.Message}");
                        _ = sb.AppendLine($"Sheet: {sheetFullName} failed: {ex.Message}");
                    }
                }
            }

            _ = sb.AppendLine(exportBaseDirectory);
            _ = sb.AppendLine($"Printed: {printCount} in {sheetCount}");
            ExportHelper.ZipTheFolder(exportFolder, exportBaseDirectory);
            SystemFolderOpener.OpenFolder(exportBaseDirectory);
        }

        return sb.ToString();
    }
}
