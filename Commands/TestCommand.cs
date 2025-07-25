using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Globalization;
using System.Text;
using System.Windows;

namespace RevitBIMTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TestCommand : IExternalCommand, IExternalCommandAvailability
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            StringBuilder report = new();

            try
            {
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    _ = report.AppendLine($"Inner: {ex.InnerException.Message}");
                }

                _ = report.AppendLine($"✗ Error: {ex.Message}");

                _ = TaskDialog.Show("Database Test Failed", report.ToString());

                return Result.Failed;
            }
            finally
            {
                Clipboard.SetText(report.ToString());
            }
        }

        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData?.ActiveUIDocument != null;
        }



    }
}