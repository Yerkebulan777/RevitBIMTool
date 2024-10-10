using Autodesk.Revit.DB;
using Document = Autodesk.Revit.DB.Document;


namespace RevitBIMTool.Utils.Performance;
public sealed class RevitPurginqHelper
{
    public static IDictionary<int, ElementId> PurgeAndGetValidConstructionTypeIds(Document doc)
    {
        //  Categories whose types will be purged
        List<BuiltInCategory> purgeBuiltInCats =
        [
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
        ];

        FilteredElementCollector collector;

        ElementMulticategoryFilter multiCat = new(purgeBuiltInCats);

        IDictionary<int, ElementId> validTypeIds = new Dictionary<int, ElementId>(100);
        IDictionary<int, ElementId> invalidTypeIds = new Dictionary<int, ElementId>(25);

        collector = new FilteredElementCollector(doc).WherePasses(multiCat);

        foreach (Element elm in collector.WhereElementIsNotElementType())
        {
            ElementId etypeId = elm.GetTypeId();

            int typeIntId = etypeId.IntegerValue;

            if (!validTypeIds.ContainsKey(typeIntId))
            {
                validTypeIds[typeIntId] = etypeId;
            }
        }


        collector = new FilteredElementCollector(doc).WherePasses(multiCat);

        foreach (Element etp in collector.WhereElementIsElementType())
        {
            int typeIntId = etp.Id.IntegerValue;

            if (!validTypeIds.ContainsKey(typeIntId))
            {
                invalidTypeIds[typeIntId] = etp.Id;
            }
        }


        using (TransactionGroup tg = new(doc))
        {
            TransactionStatus status = tg.Start("Purge types");

            foreach (KeyValuePair<int, ElementId> item in invalidTypeIds)
            {
                if (DocumentValidation.CanDeleteElement(doc, item.Value))
                {
                    using Transaction trx = new(doc, "DeleteElement type");

                    FailureHandlingOptions failOpt = trx.GetFailureHandlingOptions();
                    failOpt = failOpt.SetFailuresPreprocessor(new WarningSwallower());
                    failOpt = failOpt.SetClearAfterRollback(true);
                    trx.SetFailureHandlingOptions(failOpt);

                    if (TransactionStatus.Started == trx.Start())
                    {
                        try
                        {
                            _ = doc.Delete(item.Value);
                            status = trx.Commit();
                        }
                        finally
                        {
                            if (!trx.HasEnded())
                            {
                                status = trx.RollBack();
                            }
                        }
                    }
                }
            }
            status = tg.Assimilate();
        }

        return validTypeIds;
    }


    private static List<ElementId> GetPurgeableElements(Document doc, List<PerformanceAdviserRuleId> adviserRuleIds)
    {
        List<ElementId> result = [];
        PerformanceAdviser adviser = PerformanceAdviser.GetPerformanceAdviser();
        FailureResolutionType failureType = FailureResolutionType.DeleteElements;
        IList<FailureMessage> failureMessages = adviser.ExecuteRules(doc, adviserRuleIds);
        if (failureMessages.Count > 0)
        {
            for (int i = 0; i < failureMessages.Count; i++)
            {
                FailureMessage failure = failureMessages[i];
                if (failure.HasResolutionOfType(failureType))
                {
                    result.AddRange(failure.GetFailingElements());
                }
            }
        }
        return result;
    }


    public static void Purge(Document doc)
    {
        //The internal GUID of the Performance Adviser Rule 
        const string PurgeGuid = "e8c63650-70b7-435a-9010-ec97660c1bda";

        List<PerformanceAdviserRuleId> performanceAdviserRuleIds = [];

        //Iterating through all Performance rules looking to filled that which matches PURGE_GUID
        PerformanceAdviser adviser = PerformanceAdviser.GetPerformanceAdviser();
        foreach (PerformanceAdviserRuleId performanceAdviserRuleId in adviser.GetAllRuleIds())
        {
            if (performanceAdviserRuleId.Guid.ToString() == PurgeGuid)
            {
                performanceAdviserRuleIds.Add(performanceAdviserRuleId);
                break;
            }
        }

        //Attempting to recover all purgeable elements and delete them from the docmodel
        List<ElementId> purgeableIds = GetPurgeableElements(doc, performanceAdviserRuleIds);
        if (purgeableIds != null && purgeableIds.Count > 0)
        {
            TransactionHelpers.DeleteElements(doc, purgeableIds);
        }
    }


    public static ICollection<ElementId> GetLinkedAndImportedCADIds(Document doсument)
    {
        FilteredElementCollector collector = new(doсument);
        IList<Type> typeList = [typeof(CADLinkType), typeof(ImportInstance)];
        collector = collector.WherePasses(new ElementMulticlassFilter(typeList)).WhereElementIsNotElementType();
        return collector.ToElementIds();
    }



}




