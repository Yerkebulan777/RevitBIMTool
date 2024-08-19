using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Core;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using System.Globalization;
using System.Windows;


namespace RevitBIMTool.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
internal sealed class ExportToDWGCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        if (commandData.Application == null) { return Result.Cancelled; }

        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;

        try
        {
            RevitLinkHelper.CheckAndRemoveUnloadedLinks(doc);
            string revitFilePath = RevitPathHelper.GetRevitFilePath(doc);
            string sectionName = RevitPathHelper.GetSectionName(revitFilePath);
            message = ExportToDWGHandler.ExportExecute(uidoc, revitFilePath, sectionName);
        }
        catch (Exception ex)
        {
            _ = TaskDialog.Show("Exception", "Exception: \n" + ex);
            Clipboard.SetText(ex.ToString());
            return Result.Failed;
        }

        RevitMessageManager.ShowInfo(message);

        return Result.Succeeded;
    }


    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        UIDocument uidoc = applicationData?.ActiveUIDocument;
        return uidoc != null && uidoc.Document.IsDetached.Equals(false);
    }
}