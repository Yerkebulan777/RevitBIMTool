using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;


namespace RevitBIMTool.Commands;

/// External command entry point invoked from the Revit interface /// 

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class AppInfoCommand : ExternalCommand
{
    public override void Execute()
    {
        TaskDialog.Show("RevitBIMTool", "DEMO");
    }
}