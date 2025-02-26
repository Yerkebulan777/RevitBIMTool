using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBIMTool.Model;
using RevitBIMTool.Services;


namespace RevitBIMTool.Commands
{
    /// <summary>
    /// External command for marking lintels in Revit
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MarkLintelsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get active document
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Create marking service
                MarkingConfig config = new()
                {
                    Threshold = 50,
                    MinGroupSize = 3,
                    MarkPrefix = "ПР-",
                    ThicknessParam = "Толщина стены",
                    WidthParam = "Ширина проема",
                    HeightParam = "Высота",
                    MarkParam = "BI_марка_изделия"
                };

                LintelMarkingService service = new(doc, config);

                // Find and mark lintels
                ICollection<FamilyInstance> lintels = service.FindLintels();

                if (lintels.Count == 0)
                {
                    _ = TaskDialog.Show("Information", "No lintel families found in the model.");
                    return Result.Succeeded;
                }

                int marked = service.MarkLintels(lintels);

                // Show results
                _ = TaskDialog.Show("Success", $"Successfully marked {marked} of {lintels.Count} lintels.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// External command for marking lintels with custom parameters
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CustomMarkLintelsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Get active document
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Show dialog to get parameters
                TaskDialog mainDialog = new("Lintel Marking")
                {
                    MainInstruction = "Mark lintels with custom parameters?",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes
                };

                if (mainDialog.Show() == TaskDialogResult.Yes)
                {
                    // Create custom configuration
                    // In a real application, you would use a proper UI form to collect these values
                    MarkingConfig config = new()
                    {
                        Threshold = 50,
                        MinGroupSize = 3,
                        MarkPrefix = "ПР-",
                        ThicknessParam = "Толщина стены",
                        WidthParam = "Ширина проема",
                        HeightParam = "Высота",
                        MarkParam = "BI_марка_изделия"
                    };

                    LintelMarkingService service = new(doc, config);

                    // Find and mark lintels
                    int marked = service.MarkAllLintels();

                    // Show results
                    _ = TaskDialog.Show("Success", $"Successfully marked {marked} lintels with custom parameters.");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

}
