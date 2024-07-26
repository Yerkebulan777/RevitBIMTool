using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using System.Globalization;


namespace RevitBIMTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TestCommand : IExternalCommand, IExternalCommandAvailability
    {
        string output = string.Empty;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData.Application == null) { return Result.Cancelled; }
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            output += VisibilityHelper.HideElementBySymbolName(doc, BuiltInCategory.OST_DuctAccessory, "(клапан)kazvent_bm-h");
            output += VisibilityHelper.HideElementBySymbolName(doc, BuiltInCategory.OST_DuctAccessory, "(клапан)анемостат_10авп");
            
            _ = TaskDialog.Show("RevitBIMTool", output);

            return Result.Succeeded;
        }


        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            UIDocument uidoc = applicationData?.ActiveUIDocument;
            return uidoc != null && uidoc.Document.IsDetached.Equals(false);
        }

    }
}
