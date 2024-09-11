using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;


namespace RevitBIMTool.Utils
{
    internal static class RevitSystemsHelper
    {
        private static List<Element> GetPipesAndDucts(Document doc)
        {
            List<Element> elementsList = new(100);

            FilteredElementCollector pipeCollector = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).OfCategory(BuiltInCategory.OST_PipeCurves);
            FilteredElementCollector ductCollector = new FilteredElementCollector(doc).OfClass(typeof(Duct)).OfCategory(BuiltInCategory.OST_DuctCurves);

            elementsList.AddRange(pipeCollector.WhereElementIsCurveDriven().WhereElementIsNotElementType().ToElements());
            elementsList.AddRange(ductCollector.WhereElementIsCurveDriven().WhereElementIsNotElementType().ToElements());

            return elementsList;
        }


        private static double GetPipeWallThickness(Element elem)
        {
            if (elem is Pipe pipe)
            {
                Parameter outerDiameterParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER);
                Parameter innerDiameterParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_INNER_DIAM_PARAM);

                if (outerDiameterParam != null && innerDiameterParam != null)
                {
                    double outerDiameter = outerDiameterParam.AsDouble();
                    double innerDiameter = innerDiameterParam.AsDouble();

                    if (outerDiameter > 0 && innerDiameter > 0)
                    {
                        return (outerDiameter - innerDiameter) / 2;
                    }
                }
            }

            return 0;
        }


        private static double GetDuctWallThickness(Element elem)
        {
            if (elem is Pipe duct)
            {
                Parameter heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                Parameter widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);

                if (heightParam != null && widthParam != null)
                {
                    double height = heightParam.AsDouble();
                    double width = widthParam.AsDouble();

                    if (height > 0 && width > 0)
                    {
                        return (height - width) / 2;
                    }
                }
            }

            return 0;
        }


        public static List<Element> FilterPipesAndFittingsByMaxDiameter(Document doc, double diameter)
        {
            List<Element> result = [];

            diameter += double.Epsilon;

            List<BuiltInCategory> categories =
            [
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory
            ];

            BuiltInParameter bipCalcSize = BuiltInParameter.RBS_CALCULATED_SIZE;
            BuiltInParameter bipDiameter = BuiltInParameter.RBS_PIPE_DIAMETER_PARAM;

            ElementMulticategoryFilter filter = new(categories);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);
            collector = collector.WhereElementIsNotElementType();
            collector = collector.WhereElementIsCurveDriven();

            IList<Element> elements = collector.ToElements();

            for (int idx = 0; idx < elements.Count; idx++)
            {
                Element elem = elements[idx];

                Parameter paramCalcSize = elem.get_Parameter(bipCalcSize);
                Parameter paramDiameter = elem.get_Parameter(bipDiameter);

                if (paramCalcSize != null && paramCalcSize.HasValue)
                {
                    double value = UnitManager.FootToMm(paramCalcSize.AsDouble());

                    if (0 < value && value < diameter)
                    {
                        result.Add(elem);
                        continue;
                    }
                }

                if (paramDiameter != null && paramDiameter.HasValue)
                {
                    double value = UnitManager.FootToMm(paramDiameter.AsDouble());

                    if (0 < value && value < diameter)
                    {
                        result.Add(elem);
                        continue;
                    }
                }

            }

            return result;
        }


        public static List<double> GetAvailablePipeSegmentSizes(Document doc)
        {
            HashSet<double> sizes = [];

            FilteredElementCollector collectorPipeType = new(doc);
            collectorPipeType = collectorPipeType.OfClass(typeof(PipeType));
            IList<Element> pipeTypes = collectorPipeType.ToElements();

            foreach (PipeType pipeType in pipeTypes.Cast<PipeType>())
            {
                RoutingPreferenceManager rpm = pipeType.RoutingPreferenceManager;

                int segmentCount = rpm.GetNumberOfRules(RoutingPreferenceRuleGroupType.Segments);

                for (int index = 0; index < segmentCount; ++index)
                {
                    RoutingPreferenceRule segmentRule = rpm.GetRule(RoutingPreferenceRuleGroupType.Segments, index);

                    Element element = doc.GetElement(segmentRule.MEPPartId);

                    if (element is Segment segment)
                    {
                        foreach (MEPSize size in segment.GetSizes())
                        {
                            _ = sizes.Add(size.NominalDiameter);
                        }
                    }
                }
            }

            List<double> sizesSorted = sizes.ToList();
            sizesSorted.Sort();
            return sizesSorted;
        }

    }
}
