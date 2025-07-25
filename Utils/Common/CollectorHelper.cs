using Autodesk.Revit.DB;
using Serilog;
using Document = Autodesk.Revit.DB.Document;
using Level = Autodesk.Revit.DB.Level;

namespace RevitBIMTool.Utils.Common;

public static class CollectorHelper
{
    public static FilteredElementCollector GetInstancesByFamilyName(Document doc, BuiltInCategory bic, string familyName)
    {
        List<ElementFilter> filters = [];

        filters.Add(new Autodesk.Revit.DB.Architecture.RoomFilter());

        FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Family));

        foreach (Family family in collector.OfType<Family>().Where(family => family.Name.Contains(familyName)))
        {
            foreach (ElementId symbolId in family.GetFamilySymbolIds())
            {
                filters.Add(new FamilyInstanceFilter(doc, symbolId));
            }
        }

        LogicalOrFilter orFilter = new(filters);
        FilteredElementCollector instanceCollector = new(doc);
        instanceCollector = instanceCollector.OfCategory(bic);
        instanceCollector = instanceCollector.WherePasses(orFilter);
        instanceCollector = instanceCollector.WhereElementIsViewIndependent();

        Log.Debug("Total elems by {Count} count", instanceCollector.GetElementCount());

        return instanceCollector;
    }

    #region FilteredBySymbolName

    public static FilteredElementCollector GetInstancesBySymbolName(Document doc, BuiltInCategory bic, string symbolName)
    {
        ElementId typeParamId = new(BuiltInParameter.ELEM_TYPE_PARAM);
        ElementId symbolParamId = new(BuiltInParameter.SYMBOL_NAME_PARAM);

#if R19 || R21
        FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, symbolName, false);
        FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, symbolName, false);
#else
        FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, symbolName);
        FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, symbolName);
#endif

        ElementParameterFilter typeFilter = new(typeRule);
        ElementParameterFilter symbolFilter = new(symbolRule);
        LogicalOrFilter logicOrFilter = new(typeFilter, symbolFilter);

        FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
        collector = collector.WherePasses(logicOrFilter).WhereElementIsNotElementType();

        Log.Debug($"Total elems by {symbolName} {collector.GetElementCount()} count");

        return collector;
    }

    #endregion


    #region Category filter

    public static IList<Category> GetCategories(Document doc, CategoryType categoryType)
    {
        List<Category> categories = [];

        foreach (ElementId catId in ParameterFilterUtilities.GetAllFilterableCategories())
        {
            Category category = Category.GetCategory(doc, catId);
            if (category != null && category.CanAddSubcategory)
            {
                if (category.CategoryType == categoryType)
                {
                    categories.Add(category);
                }
            }
        }

        return categories;
    }

    #endregion


    #region Level filter

    public static List<Level> GetInValidLevels(Document doc, double maxHeightInMeters = 100)
    {
        double maximum = UnitManager.MmToFoot(maxHeightInMeters);
        ParameterValueProvider provider = new(new ElementId(BuiltInParameter.LEVEL_ELEV));
        FilterDoubleRule rule = new(provider, new FilterNumericGreaterOrEqual(), maximum, 5E-3);
        return new FilteredElementCollector(doc).OfClass(typeof(Level)).WherePasses(new ElementParameterFilter(rule))
            .Cast<Level>().OrderBy(x => x.ProjectElevation)
            .GroupBy(x => x.ProjectElevation)
            .Select(x => x.First())
            .ToList();
    }

    #endregion

}
