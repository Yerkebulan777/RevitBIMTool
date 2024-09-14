using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
using Serilog;
using System.IO;
using Element = Autodesk.Revit.DB.Element;


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


    public static void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        Log.Information("Start export to DWG...");

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);

        RevitPathHelper.EnsureDirectory(exportDirectory);

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

            string exportFolder = Path.Combine(exportDirectory, revitFileName);
            string tempFolder = Path.Combine(Path.GetTempPath(), revitFileName);

            if (ExportFileToDWG(uidoc.Document, tempFolder, sheetModels))
            {
                RevitPathHelper.Move(tempFolder, exportFolder);
                ExportHelper.CreateZipTheFolder(revitFileName, exportDirectory);
            }

        }

    }


    private static bool ExportFileToDWG(Document doc, string tempFolder, List<SheetModel> sheetModels)
    {
        int count = 0;

        SpinWait spinWait = new();

        int totalSheets = sheetModels.Count;

        RevitPathHelper.EnsureDirectory(tempFolder);

        foreach (SheetModel sheetModel in SheetModel.SortSheetModels(sheetModels))
        {
            try
            {
                spinWait.SpinOnce();

                ICollection<ElementId> elementId = [sheetModel.ViewSheet.Id];

                if (doc.Export(tempFolder, sheetModel.SheetName, elementId, dwgOptions))
                {
                    count++;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            finally
            {
                Thread.Sleep(100);
            }
        }

        return totalSheets == count;
    }



}

