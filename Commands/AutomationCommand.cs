using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Globalization;


namespace RevitBIMTool.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AutomationCommand : IExternalCommand, IExternalCommandAvailability
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (commandData.Application == null) { return Result.Cancelled; }

        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;

        return Result.Succeeded;
    }


    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        UIDocument uidoc = applicationData?.ActiveUIDocument;
        return uidoc != null && uidoc.Document.IsDetached.Equals(false);
    }

}
