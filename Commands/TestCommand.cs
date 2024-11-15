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
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData.Application == null) { return Result.Cancelled; }

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            const BuiltInCategory mechCat = BuiltInCategory.OST_MechanicalEquipment;

            IList<Element> elems = CollectorHelper.GetInstancesByFamilyName(doc, mechCat, "Задание на отверстие").ToElements();

            uidoc.Selection.SetElementIds(elems.Select(elem => elem.Id).ToList());

            string output = $"\n Total elements count: {elems.Count()}";

            TaskDialogResult dialog = TaskDialog.Show("RevitBIMTool", output);

            return Result.Succeeded;
        }


        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            UIDocument uidoc = applicationData?.ActiveUIDocument;
            return uidoc != null && uidoc.Document.IsDetached.Equals(false);
        }

    }
}
