using Autodesk.Revit.DB;


namespace RevitBIMTool.Utils.Common;
public sealed class UnitManager
{
    public const double Epsilon = 0.003;

    private const double inchToMm = 25.4;
    private const double footToMm = 12 * inchToMm;
    private const double footToMeter = footToMm * 0.001;
    private const double sqfToSqm = footToMeter * footToMeter;
    private const double cubicFootToCubicMeter = footToMeter * sqfToSqm;


    public static XYZ MmToFoot(XYZ vector)
    {
        return vector.Divide(footToMm);
    }


    public static double FootToMm(double length)
    {
        return length * footToMm;
    }


    public static double MmToFoot(double length)
    {
        return length / footToMm;
    }


    public static double FootToRoundedMm(double length, int baseVal = 50)
    {
        double millimeters = FootToMm(length);
        return baseVal * Math.Round(millimeters / baseVal);
    }


    public static double CubicFootToCubicMeter(double volume)
    {
        return volume * cubicFootToCubicMeter;
    }


    public static string GetDysplayUnitType(Parameter param)
    {

#if R19
        return LabelUtils.GetLabelFor(param.Definition.UnitType);
#elif R21
        return LabelUtils.GetLabelForSpec(param.Definition.GetSpecTypeId());
#elif R23
        return LabelUtils.GetLabelForSpec(param.Definition.GetDataType());
#endif

    }



}
