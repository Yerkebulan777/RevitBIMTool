using Autodesk.Revit.DB;
using RevitBIMTool.Utils;
using System.Text;


namespace RevitBIMTool.ExportHandlers;

internal static class VisibilityHelper
{
    public static string HideElementBySymbolName(Document doc, BuiltInCategory bic, string symbolName)
    {
        List<ElementId> hideIds = [];

        StringBuilder strBuilder = new();

        View activeView = doc.ActiveView;

        FilteredElementCollector instanses = CollectorHelper.GetInstancesBySymbolName(doc, bic, symbolName);

        strBuilder.AppendLine($"Number of elements found: {instanses.GetElementCount()}");

        foreach (Element instance in instanses.ToElements())
        {
            _ = strBuilder.AppendLine($"Name: {instance.Name}");

            if (instance.CanBeHidden(activeView))
            {
                if (instance.IsHidden(activeView))
                {
                    hideIds.Add(instance.Id);
                }
            }
        }

        if (hideIds.Count > 0)
        {
            using Transaction trx = new(doc, "HideElements");

            TransactionStatus status = trx.Start();

            try
            {
                if (status == TransactionStatus.Started)
                {
                    activeView.HideElements(hideIds);
                    status = trx.Commit();
                }
            }
            catch (Exception ex)
            {
                strBuilder.AppendLine(ex.Message);

                if (!trx.HasEnded())
                {
                    status = trx.RollBack();
                }
            }
        }

        return strBuilder.ToString();
    }

}
