using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using System.IO;
using System.Text;


namespace RevitBIMTool.Core;
internal static class ExportToDWGHandler
{
    public static string ExportToDWG(Document document, string revitFilePath)
    {
        int printCount = 0;

        StringBuilder sb = new();

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportDirectory = ExportHelper.ExportDirectory(revitFilePath, "02_DWG", true);

        IEnumerable<ViewSheet> sheets = new FilteredElementCollector(document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>();
        List<ViewSheet> sheetList = sheets.Where(s => s.CanBePrinted).OrderBy(s => s.SheetNumber.Length).ThenBy(s => s.SheetNumber).ToList();
        string exportFolderPath = Path.Combine(exportDirectory, revitFileName);

        RevitPathHelper.EnsureDirectory(exportFolderPath);

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

        foreach (ViewSheet sheet in sheetList)
        {
            if (sheet.CanBePrinted)
            {
                try
                {
                    ICollection<ElementId> collection = [sheet.Id];
                    string sheetNum = ExportHelper.GetSheetNumber(sheet);
                    string sheetName = StringHelper.NormalizeText(sheet.Name);
                    sheetName = $"{revitFileName} - Лист - {sheetNum} - {sheetName}";
                    string sheetFullName = Path.Combine(exportFolderPath, sheetName + ".dwg");

                    if (!ExportHelper.IsTargetFileUpdated(sheetFullName, revitFilePath))
                    {
                        if (document.Export(exportFolderPath, sheetName, collection, exportOptions))
                        {
                            printCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _ = sb.AppendLine($"SheetNumber ({sheet.SheetNumber}) error: " + ex.Message);
                }
            }
        }

        ExportHelper.ZipTheFolderWithSubfolders(revitFilePath, exportFolderPath);

        _ = sb.AppendLine($"Printed: {printCount} in {sheetList.Count}");
        _ = sb.AppendLine(exportDirectory);

        return sb.ToString();
    }
}
