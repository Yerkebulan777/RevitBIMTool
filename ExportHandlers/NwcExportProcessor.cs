using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.Common;
using RevitBIMTool.Utils.Performance;
using RevitBIMTool.Utils.SystemHelpers;
using Serilog;
using System.IO;
using System.Windows.Threading;


namespace RevitBIMTool.ExportHandlers;

internal static class NwcExportProcessor
{
    public static void Execute(UIDocument uidoc, string revitFilePath, string exportDirectory)
    {
        Document doc = uidoc.Document;

        Log.Debug("Start export to NWC...");

        PathHelper.EnsureDirectory(exportDirectory);

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string targetPath = Path.Combine(exportDirectory, $"{revitFileName}.nwc");

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

            const BuiltInCategory genCat = BuiltInCategory.OST_GenericModel;
            const BuiltInCategory ductCat = BuiltInCategory.OST_DuctAccessory;
            const BuiltInCategory sfrmCat = BuiltInCategory.OST_StructuralFraming;
            const BuiltInCategory mechCat = BuiltInCategory.OST_MechanicalEquipment;

            Dispatcher.CurrentDispatcher.Invoke(() => RevitViewHelper.OpenView(uidoc, view));

            instansesToHide.AddRange(RevitSystemsHelper.FilterPipesAndFittingsByMaxDiameter(doc, 30));
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, genCat, "(Отверстия)").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, sfrmCat, "(Отверстия)").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, sfrmCat, "(элемент_перемычки)").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, ductCat, "(клапан)kazvent_bm-h").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, sfrmCat, "(задание)на _отверстие").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, ductCat, "(клапан)анемостат_10авп").ToElements());
            instansesToHide.AddRange(CollectorHelper.GetInstancesByFamilyName(doc, mechCat, "Задание на отверстие").ToElements());

            Log.Debug($"Total number of items found for hiding: {instansesToHide.Count}");

            RevitWorksetHelper.SetWorksetsToVisible(doc, view);
            RevitWorksetHelper.HideWorksetsByPattern(doc, view, @"^@.+");
            RevitWorksetHelper.HideWorksetsByPattern(doc, view, @"^#.+");
            RevitViewHelper.SetViewSettings(doc, view, discipline, displayStyle, detailLevel);
            RevitViewHelper.SetCategoriesToVisible(doc, view, builtCatsToHide);
            RevitViewHelper.HideElementsInView(doc, instansesToHide, view);

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

            ExportToNWC(doc, targetPath, options);

        }
    }


    private static void ExportToNWC(Document doc, string exportFullPath, NavisworksExportOptions options)
    {
        Log.Debug($"Start exporting to nwc ...");
        string exportDirectory = Path.GetDirectoryName(exportFullPath);
        string revitFileName = Path.GetFileNameWithoutExtension(exportFullPath);

        try
        {
            doc.Export(exportDirectory, revitFileName, options);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed export to nwc {ex.Message}");
        }
        finally
        {
            if (File.Exists(exportFullPath))
            {
                SystemFolderOpener.OpenFolder(exportDirectory);
            }
        }
    }

}

