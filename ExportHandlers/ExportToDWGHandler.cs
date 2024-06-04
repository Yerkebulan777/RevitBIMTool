using Autodesk.Revit.DB;
using RevitBIMTool.Model;
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
            RevitPathHelper.EnsureDirectory(exportFolder);

            List<SheetModel> sheetModels = [];

            foreach (ViewSheet sheet in collector.Cast<ViewSheet>())
            {
                if (sheet.CanBePrinted)
                {
                    SheetModel model = new(sheet);
                    model.SetSheetNameWithExtension(document, "dwg");
                    if (model.IsValid)
                    {
                        sheetModels.Add(model);
                    }
                }
            }

            foreach (SheetModel model in SheetModel.SortSheetModels(sheetModels))
            {
                using Mutex mutex = new(false, "Global\\{{{ExportDWGMutex}}}");

                string sheetFullName = model.SheetFullName;

                if (mutex.WaitOne(Timeout.Infinite))
                {
                    try
                    {
                        ICollection<ElementId> collection = [model.ViewSheet.Id];
                        string sheetFullPath = Path.Combine(exportFolder, sheetFullName);

                        if (!ExportHelper.IsTargetFileUpdated(sheetFullPath, revitFilePath))
                        {
                            if (document.Export(exportFolder, sheetFullName, collection, exportOptions))
                            {
                                Log.Debug("Printed: " + sheetFullName);
                                printCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"Sheet: {sheetFullName} failed: {ex.Message}");
                        _ = sb.AppendLine($"Sheet: {sheetFullName} failed: {ex.Message}");
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }

            _ = sb.AppendLine(exportBaseDirectory);
            _ = sb.AppendLine($"Printed: {printCount} in {sheetCount}");
            ExportHelper.ZipTheFolder(exportFolder, exportBaseDirectory);
            SystemFolderOpener.OpenFolderInExplorerIfNeeded(exportBaseDirectory);
        }

        return sb.ToString();
    }
}
