﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.Performance;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using System.IO;
using System.Text;
using System.Windows.Threading;


namespace RevitBIMTool.ExportHandlers;

internal static class ExportToNWCHandler
{
    public static string ExportToNWC(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        StringBuilder sb = new();
        Document doc = uidoc.Document;

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string targetFullPath = Path.Combine(exportDirectory, $"{revitFileName}.nwc");

        RevitPathHelper.EnsureDirectory(exportDirectory);

        ICollection<ElementId> cadImportIds = RevitPurginqHelper.GetLinkedAndImportedCADIds(doc);

        if (cadImportIds != null && cadImportIds.Count > 0)
        {
            TransactionHelpers.DeleteElements(doc, cadImportIds);
        }

        ViewDetailLevel detailLevel = ViewDetailLevel.Fine;
        ViewDiscipline discipline = ViewDiscipline.Coordination;
        DisplayStyle displayStyle = DisplayStyle.ShadingWithEdges;

        BuiltInCategory[] builtCatsToHide = new BuiltInCategory[]
        {
                BuiltInCategory.OST_MassForm,
                BuiltInCategory.OST_Lines
        };

        View3D view3d = RevitViewHelper.Get3dView(doc, "3DNavisView");

        if (view3d is View view)
        {
            List<Element> instansesToHide = [];

            _ = sb.AppendLine(exportDirectory);

            const BuiltInCategory ductCat = BuiltInCategory.OST_DuctAccessory;
            const BuiltInCategory sfrmCat = BuiltInCategory.OST_StructuralFraming;
            const BuiltInCategory mechCat = BuiltInCategory.OST_MechanicalEquipment;

            Dispatcher.CurrentDispatcher.Invoke(() => RevitViewHelper.OpenView(uidoc, view));

            instansesToHide.AddRange(RevitSystemsHelper.FilterPipesAndFittingsByMaxDiameter(doc, 30));

            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, sfrmCat, "(Отверстия)").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, sfrmCat, "(элемент_перемычки)").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, ductCat, "(клапан)kazvent_bm-h").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, sfrmCat, "(задание)на _отверстие").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, ductCat, "(клапан)анемостат_10авп").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesByFamilyName(doc, mechCat, "Задание на отверстие").ToElements());

            _ = sb.AppendLine($"Total number of items found for hiding: {instansesToHide.Count}");

            RevitViewHelper.SetViewSettings(doc, view, discipline, displayStyle, detailLevel);
            RevitViewHelper.SetCategoriesToVisible(doc, view, builtCatsToHide);
            RevitViewHelper.HideElementsInView(doc, instansesToHide, view);
            RevitWorksetHelper.HideWorksetsByPattern(doc, view, @"^@.+");
            RevitWorksetHelper.HideWorksetsByPattern(doc, view, @"^#.+");
            RevitWorksetHelper.SetWorksetsToVisible(doc, view);

            NavisworksExportOptions options = new()
            {
                ExportScope = NavisworksExportScope.View,
                Coordinates = NavisworksCoordinates.Shared,

                ConvertElementProperties = true,

                ExportRoomAsAttribute = true,
                DivideFileIntoLevels = true,
                ExportRoomGeometry = false,

                ExportParts = false,
                ExportLinks = false,
                ExportUrls = false,

                ViewId = view3d.Id
            };

            lock (uidoc)
            {
                Export(doc, targetFullPath, options);
            }
        }

        return sb.ToString();
    }


    private static void Export(Document doc, string exportFullPath, NavisworksExportOptions options)
    {
        string exportDirectory = Path.GetDirectoryName(exportFullPath);
        string revitFileName = Path.GetFileNameWithoutExtension(exportFullPath);

        try
        {
            Log.Debug($"Start exporting to nwc in {exportDirectory}");
            doc.Export(exportDirectory, revitFileName, options);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed export to nwc {ex.Message}");
        }
        finally
        {
            if (RevitPathHelper.AwaitExistsFile(exportFullPath))
            {
                SystemFolderOpener.OpenFolder(exportDirectory);
            }
        }
    }


}

