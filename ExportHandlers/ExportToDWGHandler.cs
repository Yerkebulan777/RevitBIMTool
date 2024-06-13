using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using System.IO;
using System.Text;


namespace RevitBIMTool.ExportHandlers;
internal static class ExportToDWGHandler
{
    private static readonly DWGExportOptions dwgOptions = new()
    {
        ACAPreference = ACAObjectPreference.Geometry,
        Colors = ExportColorMode.TrueColorPerView,
        PropOverrides = PropOverrideMode.ByEntity,
        ExportOfSolids = SolidGeometry.ACIS,
        TextTreatment = TextTreatment.Exact,
        TargetUnit = ExportUnit.Millimeter,
        FileVersion = ACADVersion.R2007,
        PreserveCoincidentLines = true,
        HideUnreferenceViewTags = true,
        HideReferencePlane = true,
        SharedCoords = true,
        HideScopeBox = true,
        MergedViews = true,
    };

    private static readonly DXFExportOptions dxfOptions = new()
    {
        ACAPreference = ACAObjectPreference.Geometry,
        Colors = ExportColorMode.TrueColorPerView,
        PropOverrides = PropOverrideMode.ByEntity,
        ExportOfSolids = SolidGeometry.ACIS,
        TextTreatment = TextTreatment.Exact,
        TargetUnit = ExportUnit.Millimeter,
        FileVersion = ACADVersion.R2007,
        PreserveCoincidentLines = true,
        HideUnreferenceViewTags = true,
        HideReferencePlane = true,
        SharedCoords = true,
        HideScopeBox = true,
    };


    public static string Execute(UIDocument uidoc, string revitFilePath)
    {
        StringBuilder sb = new();

        Document doc = uidoc.Document;

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string baseDwgDirectory = ExportHelper.ExportDirectory(revitFilePath, "02_DWG", true);
        string exportZipPath = Path.Combine(baseDwgDirectory, revitFileName + ".zip");
        string exportFolder = Path.Combine(baseDwgDirectory, revitFileName);

        if (!ExportHelper.IsTargetFileUpdated(exportZipPath, revitFilePath))
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet));

            Log.Information("Start export to DWG...");
            _ = sb.AppendLine(baseDwgDirectory);

            if (collector.GetElementCount() > 0)
            {
                List<SheetModel> sheetModels = [];

                RevitPathHelper.EnsureDirectory(exportFolder);

                foreach (ViewSheet sheet in collector.Cast<ViewSheet>())
                {
                    SheetModel model = new(sheet);
                    model.SetSheetName(doc, revitFileName);
                    if (model.IsValid)
                    {
                        sheetModels.Add(model);
                    }
                }

                ExportToCAD(uidoc, exportFolder, sheetModels);
                ExportHelper.CreateZipTheFolder(exportFolder, baseDwgDirectory);
                SystemFolderOpener.OpenFolderInExplorerIfNeeded(baseDwgDirectory);
            }
        }

        _ = sb.AppendLine("Задание выполнено");

        return sb.ToString();
    }


    private static void ExportToCAD(UIDocument uidoc, string exportFolder, List<SheetModel> sheetModels)
    {
        Document doc = uidoc.Document;

        string sheetName = string.Empty;

        int counter = sheetModels.Count;

        if (counter > 0)
        {
            sheetModels = SheetModel.SortSheetModels(sheetModels);

            using Transaction trx = new(doc, "ExportToCAD");

            if (TransactionStatus.Started == trx.Start())
            {
                try
                {
                    ViewSet viewSet = new();

                    for (int idx = 0; idx < counter; idx++)
                    {
                        SheetModel sheet = sheetModels[idx];
                        _ = viewSet.Insert(sheet.ViewSheet);
                    }

                    if (doc.Export(exportFolder, sheetName, viewSet, new DWFExportOptions()))
                    {
                        Log.Verbose($"Exported {Path.GetFileName(exportFolder)} to DWF");
                    }

                    foreach (SheetModel sheetModel in sheetModels)
                    {
                        sheetName = sheetModel.SheetName;
                        ViewSheet sheet = sheetModel.ViewSheet;
                        RevitViewHelper.OpenSheet(uidoc, sheet);
                        ICollection<ElementId> elementIds = [sheet.Id];

                        if (doc.Export(exportFolder, sheetName, elementIds, dwgOptions))
                        {
                            Log.Verbose($"Exported sheet {sheetName} to DWG");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed {sheetName} {ex.Message}");
                }
                finally
                {
                    _ = trx.Commit();
                }
            }
        }

    }



}
