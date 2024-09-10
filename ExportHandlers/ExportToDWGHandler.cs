using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using System.IO;
using System.Text;
using System.Windows.Threading;


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


    public static string ExportExecute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        StringBuilder sb = new();
        Document doc = uidoc.Document;

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string targetFullPath = Path.Combine(exportDirectory, revitFileName + ".zip");
        string exportFolder = Path.Combine(exportDirectory, revitFileName);

        RevitPathHelper.EnsureDirectory(exportDirectory);
        RevitPathHelper.EnsureDirectory(exportFolder);
        RevitPathHelper.ClearDirectory(exportFolder);

        Log.Information("Start export to DWG...");

        FilteredElementCollector collector = new(doc);
        collector = collector.OfClass(typeof(ViewSheet));

        if (0 < collector.GetElementCount())
        {
            List<SheetModel> sheetModels = [];

            foreach (Element element in collector.ToElements())
            {
                if (element is ViewSheet sheet)
                {
                    SheetModel model = new(sheet);
                    model.SetSheetName(doc, revitFileName);
                    if (model.IsValid)
                    {
                        sheetModels.Add(model);
                    }
                }
            }

            if (ExportToDWG(uidoc, exportFolder, sheetModels))
            {
                SystemFolderOpener.OpenFolder(exportDirectory);
                ExportHelper.CreateZipTheFolder(exportFolder, exportDirectory);
            }

        }

        _ = sb.AppendLine("Задание выполнено");

        return sb.ToString();
    }


    private static bool ExportToDWG(UIDocument uidoc, string exportFolder, List<SheetModel> sheetModels)
    {
        Document doc = uidoc.Document;

        bool result = sheetModels.Count > 0;

        foreach (SheetModel sheetModel in SheetModel.SortSheetModels(sheetModels))
        {
            string exportFullPath = Path.Combine(exportFolder, $"{sheetModel.SheetName}.dwg");

            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                using Transaction trx = new(doc, $"ExportToDWG");

                try
                {
                    if (File.Exists(exportFullPath))
                    {
                        File.Delete(exportFullPath);
                    }

                    if (TransactionStatus.Started == trx.Start())
                    {
                        ViewSheet sheet = sheetModel.ViewSheet;

                        ICollection<ElementId> elementId = [sheet.Id];

                        if (!doc.Export(exportFolder, sheetModel.SheetName, elementId, dwgOptions))
                        {
                            Log.Error($"Неудачный экспорт в DWG {sheetModel.SheetName}");

                            result = false;
                        }

                        _ = trx.Commit();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
                finally
                {
                    if (!trx.HasEnded())
                    {
                        _ = trx.RollBack();
                    }
                }
            });
        }

        return result;
    }

}

