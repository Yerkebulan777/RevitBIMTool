﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using iTextSharp.text;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;
using Serilog;
using System.Collections.Generic;
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

            if (ExportFileToDWG(uidoc, exportFolder, sheetModels))
            {
                ExportHelper.CreateZipTheFolder(revitFileName, exportDirectory);
            }

        }

    }


    private static bool ExportFileToDWG(UIDocument uidoc, string exportFolder, List<SheetModel> sheetModels)
    {
        bool result = sheetModels.Count > 0;

        foreach (SheetModel sheetModel in SheetModel.SortSheetModels(sheetModels))
        {
            string exportFullPath = Path.Combine(exportFolder, $"{sheetModel.SheetName}.dwg");

            lock (uidoc)
            {
                try
                {
                    Task.Run(() =>
                    {
                        Thread.Sleep(1000);

                        if (File.Exists(exportFullPath))
                        {
                            File.Delete(exportFullPath);
                        }

                        ViewSheet sheet = sheetModel.ViewSheet;

                        ICollection<ElementId> elementId = [sheet.Id];

                        result = uidoc.Document.Export(exportFolder, sheetModel.SheetName, elementId, dwgOptions);

                    }).RunSynchronously();

                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }
        }

        return result;
    }


}

