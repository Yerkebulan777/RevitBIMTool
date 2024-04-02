using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using System.IO;
using System.Text;


namespace RevitBIMTool.Core;

internal static class ExportToNWCHandler
{
    public static string ExportToNWC(Document doc, string revitFilePath)
    {
        StringBuilder sb = new();

        if (string.IsNullOrEmpty(revitFilePath))
        {
            throw new ArgumentNullException(nameof(revitFilePath));
        }

        string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
        string exportBaseDirectory = ExportHelper.ExportDirectory(revitFilePath, "05_NWC");
        string exportFullPath = Path.Combine(exportBaseDirectory, revitFileName + ".nwc");

        if (!ExportHelper.IsTargetFileUpdated(exportFullPath, revitFilePath))
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
            UIDocument uidoc = new(doc);

            if (view3d is View activeView)
            {
                uidoc?.RequestViewChange(view3d);

                RevitViewHelper.SetWorksetsVisible(doc, activeView);
                RevitViewHelper.SetCategoriesToVisible(doc, activeView, builtCatsToHide);
                RevitViewHelper.SetViewSettings(doc, activeView, discipline, displayStyle, detailLevel);

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

