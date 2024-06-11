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
        string tempDirectory = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User);
        string exportFolder = Path.Combine(exportBaseDirectory, revitFileName);
        string tempFolder = Path.Combine(tempDirectory, revitFileName);

        DWGExportOptions exportOptions = new()
        {
            Colors = ExportColorMode.TrueColorPerView,
            PropOverrides = PropOverrideMode.ByEntity,
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
        };

        FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).WhereElementIsNotElementType();
        
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
                            string sheetTempPath = Path.Combine(tempFolder, sheetFullName);
                            if (!ExportHelper.IsTargetFileUpdated(sheetTempPath, revitFilePath))
                            {
                                if (doc.Export(tempFolder, sheetFullName, collection, exportOptions))
                                {
                                    RevitPathHelper.CheckFile(sheetTempPath, interval);
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

            RevitPathHelper.EnsureDirectory(exportFolder);
            RevitPathHelper.ClearDirectory(exportFolder);

            foreach (SheetModel model in SheetModel.SortSheetModels(sheetModels))
            {
                Log.Debug($"Sheet name: {model.SheetFullName}");
                Log.Debug($"Organization group name: {model.OrganizationGroupName}");
                Log.Debug($"Sheet number: {model.StringNumber} ({model.DigitNumber})");

                string filePath = SheetModel.FindFileInDirectory(tempFolder, model.SheetFullName);
                string sheetFullPath = Path.Combine(exportFolder, model.SheetFullName);

                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Copy(filePath, sheetFullPath, true);
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed copy: {ex.Message}");
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
