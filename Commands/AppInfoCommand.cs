using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace RevitBIMTool.Commands;

/// External command entry point invoked from the Revit interface /// 

[Transaction(TransactionMode.Manual)]
public class AppInfoCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        TaskDialog.Show("RevitBIMTool", "DEMO");
        return Result.Succeeded;
    }
}