using Autodesk.Revit.DB;
using Serilog;
using Document = Autodesk.Revit.DB.Document;
using Level = Autodesk.Revit.DB.Level;


namespace RevitBIMTool.Utils;

public static class CollectorHelper
{

    #region FilteredByFamilylName

    public static FilteredElementCollector GetInstancesByFamilyName(Document doc, BuiltInCategory bic, string nameStart)
    {
        IList<ElementFilter> filters = [];

        filters.Add(new Autodesk.Revit.DB.Architecture.RoomFilter());

        FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Family));

        FilteredElementCollector instanceCollector = new(doc);

        foreach (Family family in collector.OfType<Family>())
        {
            if (family.Name.StartsWith(nameStart, StringComparison.OrdinalIgnoreCase))
            {
                foreach (ElementId symbolId in family.GetFamilySymbolIds())
                {
                    filters.Add(new FamilyInstanceFilter(doc, symbolId));
                }
            }
        }

        LogicalOrFilter orFilter = new(filters);
        instanceCollector = instanceCollector.OfCategory(bic);
        instanceCollector = instanceCollector.WherePasses(orFilter);
        instanceCollector = instanceCollector.WhereElementIsViewIndependent();

        Log.Debug($"Instances {nameStart} {instanceCollector.GetElementCount()} count");

        return instanceCollector;
    }

    #endregion


    #region FilteredBySymbolName

    public static FilteredElementCollector GetInstancesBySymbolName(Document doc, BuiltInCategory bic, string symbolName)
    {
        ElementId typeParamId = new(BuiltInParameter.ELEM_TYPE_PARAM);
        ElementId symbolParamId = new(BuiltInParameter.SYMBOL_NAME_PARAM);

#if R23
        FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, symbolName);
        FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, symbolName);
#elif R19 || R21
        FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, symbolName, false);
        FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, symbolName, false);
#endif

        ElementParameterFilter typeFilter = new(typeRule);
        ElementParameterFilter symbolFilter = new(symbolRule);
        LogicalOrFilter logicOrFilter = new(typeFilter, symbolFilter);

        FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
        collector = collector.WherePasses(logicOrFilter).WhereElementIsNotElementType();

        Log.Debug($"Instances {symbolName} {collector.GetElementCount()} count");

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
