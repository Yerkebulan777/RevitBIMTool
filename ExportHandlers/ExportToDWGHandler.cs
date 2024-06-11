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
        string exportZipPath = Path.Combine(exportBaseDirectory, revitFileName + ".zip");
        string exportFolder = Path.Combine(exportBaseDirectory, revitFileName);
        string tempFolder = Path.Combine(tempDirectory, revitFileName);

        if (!ExportHelper.IsTargetFileUpdated(exportZipPath, revitFilePath))
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet));

            Log.Information("Start export to DWG...");
            int sheetCount = collector.GetElementCount();

            DWGExportOptions dwgOptions = new()
            {
                Colors = ExportColorMode.TrueColorPerView,
                PropOverrides = PropOverrideMode.ByEntity,
                ExportOfSolids = SolidGeometry.ACIS,
                TextTreatment = TextTreatment.Exact,
                TargetUnit = ExportUnit.Millimeter,
                FileVersion = ACADVersion.R2007,
                HideUnreferenceViewTags = true,
                HideReferencePlane = true,
                SharedCoords = true,
                HideScopeBox = true,
                MergedViews = true,
            };

            if (sheetCount > 0)
            {
                List<SheetModel> sheetModels = [];

                RevitPathHelper.EnsureDirectory(tempFolder);
                RevitPathHelper.EnsureDirectory(exportFolder);

                List<ElementId> views = new List<ElementId>();

                TimeSpan interval = TimeSpan.FromSeconds(100);

                foreach (ViewSheet sheet in collector.Cast<ViewSheet>())
                {
                    if (sheet.CanBePrinted)
                    {
                        if (!sheet.IsPlaceholder)
                        {
                            SheetModel model = new(sheet);
                            model.SetSheetNameWithExtension(doc, "dwg");
                            if (model.IsValid)
                            {
                                sheetModels.Add(model);
                                views.Add(model.ViewSheet.Id);
                            }
                        }
                    }
                }

                Log.Information($"Total valid sheet count: ({sheetModels.Count})");

                if (doc.Export(tempFolder, revitFileName, views, dwgOptions))
                {
                    printCount++;
                }


                //foreach (SheetModel model in SheetModel.SortSheetModels(sheetModels))
                //{
                //    using Mutex mutex = new(false, "Global\\{{{ExportDWGMutex}}}");

                //    string sheetFullName = model.SheetFullName;

                //    ViewSheet sheet = model.ViewSheet;

                //    if (mutex.WaitOne(Timeout.Infinite))
                //    {
                //        try
                //        {
                //            ICollection<ElementId> collection = [sheet.Id];
                //            Log.Verbose("Start export file: " + sheetFullName);
                //            string sheetTempPath = Path.Combine(tempFolder, sheetFullName);

                //            RevitPathHelper.DeleteExistsFile(sheetTempPath);

                //            if (doc.Export(tempFolder, sheetFullName, collection, dwgOptions))
                //            {
                //                RevitPathHelper.CheckFile(sheetTempPath, interval);
                //                Log.Verbose("Exported dwg: " + sheetFullName);
                //                printCount++;
                //            }
                //        }
                //        catch (Exception ex)
                //        {
                //            _ = sb.AppendLine(ex.Message);
                //            Log.Error(ex, ex.Message);
                //        }
                //        finally
                //        {
                //            mutex.ReleaseMutex();
                //        }
                //    }
                //}

                //foreach (SheetModel model in SheetModel.SortSheetModels(sheetModels))
                //{
                //    Log.Debug($"Sheet name: {model.SheetFullName}");
                //    Log.Debug($"Organization group name: {model.OrganizationGroupName}");
                //    Log.Debug($"Sheet number: {model.StringNumber} ({model.DigitNumber})");

                //    string filePath = SheetModel.FindFileInDirectory(tempFolder, model.SheetFullName);
                //    string sheetFullPath = Path.Combine(exportFolder, model.SheetFullName);
                //    RevitPathHelper.DeleteExistsFile(sheetFullPath);

                //    if (File.Exists(filePath))
                //    {
                //        try
                //        {
                //            File.Copy(filePath, sheetFullPath, true);
                //            File.Delete(filePath);
                //        }
                //        catch (Exception ex)
                //        {
                //            Log.Error($"Failed copy: {ex.Message}");
                //        }
                //    }
                //}

                _ = sb.AppendLine(exportBaseDirectory);
                _ = sb.AppendLine($"Printed: {printCount} in {sheetCount}");
                ExportHelper.ZipTheFolder(exportFolder, exportBaseDirectory);
                SystemFolderOpener.OpenFolderInExplorerIfNeeded(tempFolder);
                //RevitPathHelper.DeleteDirectory(tempFolder);
            }
        }

        return sb.ToString();
    }
}
