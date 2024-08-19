using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Utils;
using System.Globalization;


namespace RevitBIMTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TestCommand : IExternalCommand, IExternalCommandAvailability
    {
        private string output = string.Empty;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData.Application == null) { return Result.Cancelled; }

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            string[] paramNames = { "Диаметр", "Размер" };

            List<Element> elems = RevitSystemsHelper.FilterPipesAndFittingsByMaxDiameter(doc, 30);

            uidoc.Selection.SetElementIds(elems.Select(elem => elem.Id).ToList());

            output += $"\n Total elements count: {elems.Count}";

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
