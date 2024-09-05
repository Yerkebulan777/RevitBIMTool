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


    public static string ExportExecute(UIDocument uidoc, string revitFilePath, string sectionName)
    {
        StringBuilder sb = new();

        Document doc = uidoc.Document;

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string baseDwgDirectory = ExportPathHelper.ExportDirectory(revitFilePath, "02_DWG", true);
        string exportZipPath = Path.Combine(baseDwgDirectory, revitFileName + ".zip");
        string exportFolder = Path.Combine(baseDwgDirectory, revitFileName);

        if (!ExportPathHelper.IsTargetFileUpdated(exportZipPath, revitFilePath))
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

                if (ExportToDWG(uidoc, exportFolder, sheetModels))
                {
                    SystemFolderOpener.OpenFolder(baseDwgDirectory);
                    ExportPathHelper.CreateZipTheFolder(exportFolder, baseDwgDirectory);
                }

            }
        }

        _ = sb.AppendLine("Задание выполнено");

        return sb.ToString();
    }


    private static bool ExportToDWG(UIDocument uidoc, string exportFolder, List<SheetModel> sheetModels)
    {
        bool result = false;

        if (sheetModels.Count > 0)
        {
            result = true;

            Document doc = uidoc.Document;

            foreach (SheetModel sheetModel in SheetModel.SortSheetModels(sheetModels))
            {
                string exportFullPath = Path.Combine(exportFolder, $"{sheetModel.SheetName}.dwg");



                RevitPathHelper.DeleteExistsFile(exportFullPath);

                using Transaction trx = new(doc, $"Export to DWG");

                try
                {
                    ViewSheet sheet = sheetModel.ViewSheet;

                    if (TransactionStatus.Started == trx.Start())
                    {
                        ICollection<ElementId> elementIds = [sheet.Id];

                        if (!doc.Export(exportFolder, sheetModel.SheetName, elementIds, dwgOptions))
                        {
                            result = false;
                        }
                    }
                }
                finally
                {
                    if (!trx.HasEnded())
                    {
                        _ = trx.RollBack();
                    }
                }

            }

        }

        return result;
    }


}

