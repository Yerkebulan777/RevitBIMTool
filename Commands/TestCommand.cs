using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using System.Globalization;
using System.Text;


namespace RevitBIMTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TestCommand : IExternalCommand, IExternalCommandAvailability
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData.Application == null) { return Result.Cancelled; }
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            string output = VisibilityHelper.HideElementBySymbolName(doc, BuiltInCategory.OST_StructuralColumns, "450mm");

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
