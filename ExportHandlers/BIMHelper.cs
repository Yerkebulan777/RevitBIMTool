using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;


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


    }
}
