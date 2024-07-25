using Autodesk.Revit.DB;
using Document = Autodesk.Revit.DB.Document;
using Level = Autodesk.Revit.DB.Level;


namespace RevitBIMTool.Utils
{
    public static class ElementCollectorHelper
    {

        #region Advance Filtered Element

        public static FilteredElementCollector GetInstancesByElementTypeId(Document doc, in ElementId typeId)
        {
            FilterRule rule = ParameterFilterRuleFactory.CreateEqualsRule(new ElementId(BuiltInParameter.ELEM_TYPE_PARAM), typeId);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(new ElementParameterFilter(rule));
            return collector.WhereElementIsNotElementType();
        }


        public static FilteredElementCollector GetInstanceBySymbolName(Document doc, in string name)
        {
            ElementId typeParamId = new(BuiltInParameter.ELEM_TYPE_PARAM);
            ElementId symbolParamId = new(BuiltInParameter.SYMBOL_NAME_PARAM);

            FilterRule typeRule = ParameterFilterRuleFactory.CreateEqualsRule(typeParamId, name, false);
            FilterRule symbolRule = ParameterFilterRuleFactory.CreateEqualsRule(symbolParamId, name, false);

            LogicalOrFilter logicFilter = new(new ElementParameterFilter(symbolRule), new ElementParameterFilter(typeRule));

            return new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WherePasses(logicFilter);
        }


        public static FamilySymbol GetFamilySymbolByName(Document doc, in string familyName, in string symbolName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Family));
            foreach (Family family in collector.OfType<Family>())
            {
                if (family.Name.Contains(familyName))
                {
                    ISet<ElementId> ids = family.GetFamilySymbolIds();
                    foreach (ElementId id in ids)
                    {
                        FamilySymbol symbol = doc.GetElement(id) as FamilySymbol;
                        if (string.IsNullOrEmpty(symbolName) || symbol.Name.Contains(symbolName))
                        {
                            return symbol;
                        }
                    }
                }
            }
            return null;
        }


        public static FamilySymbol GetFamilySymbolByName(Document doc, string name)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).FirstOrDefault(q => q.Name.Equals(name)) as FamilySymbol;
        }


        public static ElementType GetElementTypeByName(Document doc, string name)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(ElementType)).FirstOrDefault(q => q.Name.Equals(name)) as ElementType;
        }


        public static List<View3D> Get3DViews(Document document, in string name = null, bool template = false)
        {
            List<View3D> elements = [];
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View3D));
            foreach (View3D view in collector.OfType<View3D>())
            {
                if (template.Equals(view.IsTemplate))
                {
                    if (string.IsNullOrEmpty(name) || view.Name.Contains(name))
                    {
                        elements.Add(view);
                    }
                }
            }
            collector.Dispose();
            return elements;
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


        public static IDictionary<string, Category> GetEngineerCategories(Document doc)
        {
            IDictionary<string, Category> result = new SortedDictionary<string, Category>();
            IList<BuiltInCategory> builtInCats =
            [
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_MechanicalEquipment
            ];
            foreach (BuiltInCategory catId in builtInCats)
            {
                Category cat = Category.GetCategory(doc, catId);
                if (cat != null)
                {
                    result[cat.Name] = cat;
                }
            }
            return result;
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
}
