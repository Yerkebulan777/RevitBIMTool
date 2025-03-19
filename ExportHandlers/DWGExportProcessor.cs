using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Models;
using RevitBIMTool.Utils.Common;
using Serilog;
using System.IO;
using Element = Autodesk.Revit.DB.Element;

namespace RevitBIMTool.ExportHandlers;

internal static class DWGExportProcessor
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


    public static void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        Log.Information("Start export to DWG...");

        PathHelper.EnsureDirectory(exportDirectory);

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);

        FilteredElementCollector collector = new(uidoc.Document);

        collector = collector.OfClass(typeof(ViewSheet));

        if (0 < collector.GetElementCount())
        {
            List<SheetModel> sheetModels = [];

            foreach (Element element in collector.ToElements())
            {
                if (element is ViewSheet sheet)
                {
                    SheetModel model = new(sheet);

                    model.SetSheetName(uidoc.Document, revitFileName);

                    if (model.IsValid)
                    {
                        sheetModels.Add(model);
                    }
                }
            }

            DirectoryInfo tempBase = Directory.GetParent(Path.GetTempPath());
            string sourсeFolder = Path.Combine(tempBase.FullName, revitFileName);
            string targetFolder = Path.Combine(exportDirectory, revitFileName);

            if (ExportFileToDWG(uidoc.Document, sourсeFolder, sheetModels))
            {
                ExportManager.MoveAllFiles(sourсeFolder, targetFolder);
                ExportManager.CreateZipFolder(targetFolder, exportDirectory);
            }

        }

    }


    private static bool ExportFileToDWG(Document doc, string folder, List<SheetModel> sheetModels)
    {
        int count = 0;

        SpinWait spinWait = new();

        int totalSheets = sheetModels.Count;

        PathHelper.EnsureDirectory(folder);

        Log.Information($"Total valid sheets {totalSheets}");

        foreach (SheetModel sheetModel in SheetHelper.SortSheetModels(sheetModels))
        {
            ICollection<ElementId> elemIds = [sheetModel.ViewSheet.Id];

            if (doc.Export(folder, sheetModel.SheetName, elemIds, dwgOptions))
            {
                spinWait.SpinOnce();
                Thread.Sleep(100);
                count++;
            }
        }

        return totalSheets == count;
    }



}

