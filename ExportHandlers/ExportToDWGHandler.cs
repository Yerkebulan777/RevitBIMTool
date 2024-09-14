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

        string exportFolder = Path.Combine(exportDirectory, revitFileName);

        RevitPathHelper.EnsureDirectory(exportDirectory);
        RevitPathHelper.EnsureDirectory(exportFolder);
        RevitPathHelper.ClearDirectory(exportFolder);

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

            if (ExportFileToDWG(uidoc.Document, exportFolder, sheetModels))
            {
                ExportHelper.CreateZipTheFolder(revitFileName, exportDirectory);
            }

        }

    }


    private static bool ExportFileToDWG(Document doc, string exportFolder, List<SheetModel> sheetModels)
    {
        bool result = sheetModels.Count > 0;

        string tempFolder = Path.Combine(Path.GetTempPath(), Path.GetFileName(exportFolder));

        foreach (SheetModel sheetModel in SheetModel.SortSheetModels(sheetModels))
        {
            try
            {
                Thread.Sleep(1000);

                string tempExportPath = Path.Combine(tempFolder, $"{sheetModel.SheetName}.dwg");
                string finalExportPath = Path.Combine(exportFolder, $"{sheetModel.SheetName}.dwg");

                ICollection<ElementId> elementId = new List<ElementId> { sheetModel.ViewSheet.Id };

                result = doc.Export(tempFolder, sheetModel.SheetName, elementId, dwgOptions);

                if (result)
                {
                    if (File.Exists(finalExportPath))
                    {
                        File.Delete(finalExportPath);
                    }

                    File.Move(tempExportPath, finalExportPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
                result = false;
            }
        }

        return result;
    }




}

