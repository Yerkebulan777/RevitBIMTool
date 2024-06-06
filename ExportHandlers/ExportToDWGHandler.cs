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
    public static string ExportToDWG(UIDocument uidoc, string revitFilePath)
    {
        int printCount = 0;

        StringBuilder sb = new();

        Document doc = uidoc.Document;

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportHelper.ExportDirectory(revitFilePath, "02_DWG", true);
        FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).WhereElementIsNotElementType();
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
            FileVersion = ACADVersion.R2010,
            HideUnreferenceViewTags = true,
            HideReferencePlane = true,
            NonplotSuffix = "NPLT",
            LayerMapping = "AIA",
            HideScopeBox = true,
            MergedViews = true,
            SharedCoords = false
        };

        int sheetCount = collector.GetElementCount();
        Log.Information($"All sheets => {sheetCount}");

        if (sheetCount > 0)
        {
            List<SheetModel> sheetModels = [];

            RevitPathHelper.EnsureDirectory(exportFolder);

            TimeSpan interval = TimeSpan.FromSeconds(100);

            foreach (ViewSheet sheet in collector.Cast<ViewSheet>())
            {
                if (sheet.CanBePrinted)
                {
                    SheetModel model = new(sheet);
                    model.SetSheetNameWithExtension(doc, "dwg");
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

                ViewSheet sheet = model.ViewSheet;

                if (mutex.WaitOne(Timeout.Infinite))
                {
                    if (sheet.AreGraphicsOverridesAllowed())
                    {
                        try
                        {
                            ICollection<ElementId> collection = [sheet.Id];
                            RevitViewHelper.OpenAndActivateView(uidoc, sheet);
                            string sheetFullPath = Path.Combine(exportFolder, sheetFullName);
                            if (!ExportHelper.IsTargetFileUpdated(sheetFullPath, revitFilePath))
                            {
                                if (doc.Export(exportFolder, sheetFullName, collection, exportOptions))
                                {
                                    RevitPathHelper.CheckFile(sheetFullPath, interval);
                                    RevitViewHelper.CloseAllViews(uidoc, sheet);
                                    Log.Debug("Printed: " + sheetFullName);
                                    printCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            string msg = $"Sheet: {sheetFullName} failed: {ex.Message}";
                            _ = sb.AppendLine(msg);
                            Log.Error(msg);
                        }
                        finally
                        {
                            mutex.ReleaseMutex();
                        }
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
