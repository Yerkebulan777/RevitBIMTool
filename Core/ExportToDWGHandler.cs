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

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportHelper.ExportDirectory(revitFilePath, "02_DWG", true);

        IEnumerable<ViewSheet> sheets = new FilteredElementCollector(document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>();
        List<ViewSheet> sheetList = sheets.Where(s => s.CanBePrinted).OrderBy(s => s.SheetNumber.Length).ThenBy(s => s.SheetNumber).ToList();
        string exportFolder = Path.Combine(exportBaseDirectory, revitFileName);

        RevitPathHelper.EnsureDirectory(exportFolder);

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
                    string sheetFullName = $"{revitFileName} - Лист - {sheetNum} - {sheetName}";
                    string sheetFullPath = Path.Combine(exportFolder, sheetFullName + ".dwg");

                    if (!ExportHelper.IsTargetFileUpdated(sheetFullPath, revitFilePath))
                    {
                        if (document.Export(exportFolder, sheetFullName, collection, exportOptions))
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

        _ = sb.AppendLine(exportBaseDirectory);
        _ = sb.AppendLine($"Printed: {printCount} in {sheetList.Count}");
        ExportHelper.ZipTheFolder(exportFolder, exportBaseDirectory);
        SystemFolderOpener.OpenFolder(exportBaseDirectory);

        return sb.ToString();
    }
}
