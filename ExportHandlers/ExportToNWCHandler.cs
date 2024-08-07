using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using RevitBIMTool.Utils.Performance;
using RevitBIMTool.Utils.SystemUtil;
using Serilog;
using System.IO;
using System.Text;


namespace RevitBIMTool.ExportHandlers;

internal static class ExportToNWCHandler
{
    public static string ExportToNWC(UIDocument uidoc, string revitFilePath)
    {
        StringBuilder sb = new();
        Document doc = uidoc.Document;

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportPathHelper.ExportDirectory(revitFilePath, "05_NWC");
        string exportFullPath = Path.Combine(exportBaseDirectory, revitFileName + ".nwc");

        if (!ExportPathHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
        {
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
                uidoc.ActiveView = view;

                if (doc.ActiveView == view)
                {
                    uidoc.RefreshActiveView();
                    Log.Debug("3D view activated");
                }

                List<Element> instansesToHide = [];

                const BuiltInCategory ductCat = BuiltInCategory.OST_DuctAccessory;
                const BuiltInCategory strFCat = BuiltInCategory.OST_StructuralFraming;

                instansesToHide.AddRange(BIMHelper.FilterPipesAndFittingsByMaxDiameter(doc, 30));
                instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, strFCat, "(Отверстия)").ToElements());
                instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, strFCat, "(Отверстия)").ToElements());
                instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, strFCat, "(элемент_перемычки)").ToElements());
                instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, ductCat, "(клапан)kazvent_bm-h").ToElements());
                instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, strFCat, "(задание)на _отверстие").ToElements());
                instansesToHide.AddRange(CollectorHelper.GetInstancesBySymbolName(doc, ductCat, "(клапан)анемостат_10авп").ToElements());

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
                    DivideFileIntoLevels = true,

                    ExportRoomAsAttribute = true,
                    ExportRoomGeometry = false,

                    ExportParts = false,
                    ExportLinks = false,
                    ExportUrls = false,

                    ViewId = view3d.Id
                };

                try
                {

                    doc.Export(exportBaseDirectory, revitFileName, options);
                    SystemFolderOpener.OpenFolder(exportBaseDirectory);
                    _ = sb.AppendLine(exportBaseDirectory);
                }
                catch (Exception ex)
                {
                    _ = sb.AppendLine(ex.Message);
                }
                finally
                {
                    view3d?.Dispose();
                }
            }
        }

        return sb.ToString();
    }

}

