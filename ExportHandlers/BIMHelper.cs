using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using RevitBIMTool.Utils;
using System.Text;


namespace RevitBIMTool.ExportHandlers
{
    internal static class BIMHelper
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


        public static List<Element> RetrievePipesAndFittings(Document doc)
        {
            List<Element> result = [];
            StringBuilder builder = new();

            List<BuiltInCategory> categories =
            [
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_MechanicalEquipment
            ];

            var bipCalcSize = BuiltInParameter.RBS_CALCULATED_SIZE;
            var bipDiameter = BuiltInParameter.RBS_PIPE_DIAMETER_PARAM;

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

                Parameter parameter = null;

                if (paramCalcSize != null)
                {
                    parameter = paramCalcSize;
                }

                if (paramDiameter != null)
                {
                    parameter = paramDiameter;
                }

                if (parameter is null)
                {
                    throw new Exception(elem.Category.Name);
                }

                double value = UnitManager.FootToMm(parameter.AsDouble());

                if (parameter.HasValue && value < 30)
                {
                    result.Add(elem);
                }

            }

            return result;
        }


        public static Parameter GetBuiltinParameter(Element elem)
        {
            BuiltInParameter paraIndex = BuiltInParameter.WALL_BASE_OFFSET;
            Parameter parameter = elem.get_Parameter(paraIndex);

            return parameter;
        }

    }
}
