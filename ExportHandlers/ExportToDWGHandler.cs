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

                ExportToCAD(uidoc, exportFolder, sheetModels);
                SystemFolderOpener.OpenFolder(baseDwgDirectory);
                ExportHelper.CreateZipTheFolder(exportFolder, baseDwgDirectory);
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

            foreach (SheetModel sheetModel in sheetModels)
            {
                sheetName = sheetModel.SheetName;
                ViewSheet sheet = sheetModel.ViewSheet;
                ICollection<ElementId> elementIds = [sheet.Id];
                Dispatcher.CurrentDispatcher.Invoke(() => RevitViewHelper.OpenSheet(uidoc, sheet));

                //var titleBlockId = new FilteredElementCollector(doc, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).FirstElementId();

                if (doc.Export(exportFolder, sheetName, elementIds, dwgOptions))
                {
                    Log.Verbose($"Exported sheet {sheetName} to DWG");
                }
            }
        }
    }



}
