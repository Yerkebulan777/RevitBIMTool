﻿using Autodesk.Revit.DB;
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
    static int printCount = 0;

    public static string ExportToDWG(UIDocument uidoc, string revitFilePath)
    {
        StringBuilder sb = new();

        Document doc = uidoc.Document;

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportHelper.ExportDirectory(revitFilePath, "02_DWG", true);
        string exportZipPath = Path.Combine(exportBaseDirectory, revitFileName + ".zip");
        string exportFolder = Path.Combine(exportBaseDirectory, revitFileName);

        if (!ExportHelper.IsTargetFileUpdated(exportZipPath, revitFilePath))
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet));

            Log.Information("Start export to DWG...");
            _ = sb.AppendLine(exportBaseDirectory);

            if (collector.GetElementCount() > 0)
            {
                List<SheetModel> sheetModels = [];

                RevitPathHelper.EnsureDirectory(exportFolder);

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
                            }
                        }
                    }
                }

                ExportExecuteAsync(uidoc, exportFolder, sheetModels).Wait();
                ExportHelper.ZipTheFolder(exportFolder, exportBaseDirectory);
                SystemFolderOpener.OpenFolderInExplorerIfNeeded(exportFolder);

                Log.Information($"Printed: {printCount} in {sheetModels.Count}");
                sb.AppendLine($"Printed: {printCount} in {sheetModels.Count}");
            }
        }

        return sb.ToString();
    }


    private static async Task ExportExecuteAsync(UIDocument uidoc, string exportFolder, List<SheetModel> sheetModels)
    {
        DWGExportOptions dwgOptions = new()
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

        foreach (SheetModel model in SheetModel.SortSheetModels(sheetModels))
        {
            await RevitViewHelper.ActivateViewAndCloseOthersAsync(uidoc, model.ViewSheet);

            using Mutex mutex = new(false, "Global\\{{{ExportDWGMutex}}}");

            string sheetFullName = model.SheetFullName;

            if (mutex.WaitOne(Timeout.Infinite))
            {
                try
                {
                    Log.Verbose("Start export file: " + sheetFullName);
                    ICollection<ElementId> collection = [model.ViewSheet.Id];
                    if (uidoc.Document.Export(exportFolder, sheetFullName, collection, dwgOptions))
                    {
                        Log.Verbose("Exported sheet: " + sheetFullName);
                        printCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }
}
