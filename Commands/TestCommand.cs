using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.ExportHandlers;
using RevitBIMTool.Utils;
using Serilog;
using System.Text;


namespace RevitBIMTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            string output = VisibilityHelper.HideElementBySymbolName(doc, BuiltInCategory.OST_Columns, "450mm");

            _ = TaskDialog.Show("RevitBIMTool", output);

            return Result.Succeeded;
        }




    }
}
