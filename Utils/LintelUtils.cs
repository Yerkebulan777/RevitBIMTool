using Autodesk.Revit.DB;

namespace RevitBIMTool.Utils
{
    /// <summary>
    /// Utility functions for lintel marking
    /// </summary>
    public static class LintelUtils
    {
        /// <summary>
        /// Rounds a value to the nearest multiple of 50
        /// </summary>
        /// <prm name="value">Value to round</prm>
        /// <returns>Rounded value</returns>
        public static int Round50(double value)
        {
            return (int)(50 * Math.Round(value / 50));
        }

        /// <summary>
        /// Gets parameter value from family instance
        /// </summary>
        /// <prm name="instance">Family instance</prm>
        /// <prm name="paramName">Parameter name</prm>
        /// <returns>Parameter value in mm</returns>
        public static double GetParameterValue(FamilyInstance instance, string paramName)
        {
            // Try to get parameter by name
            Parameter prm = instance.LookupParameter(paramName);

            if (prm != null && prm.HasValue && prm.StorageType == StorageType.Double)
            {
                return UnitManager.FootToMm(prm.AsDouble());
            }

            return 0;
        }

        /// <summary>
        /// Sets mark parameter value for family instance
        /// </summary>
        /// <prm name="instance">Family instance</prm>
        /// <prm name="mark">Mark value</prm>
        /// <prm name="customMark">Custom mark parameter name (optional)</prm>
        /// <returns>True if mark was set successfully</returns>
        public static bool SetMark(FamilyInstance instance, string mark, string customMark = null)
        {
            // Try to get built-in mark parameter
            Parameter prm = instance.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);

            // If built-in parameter not available or not writable, try custom parameter
            if ((prm is null || !prm.IsReadOnly) && !string.IsNullOrEmpty(customMark))
            {
                prm = instance.LookupParameter(customMark);
            }

            // Set mark if parameter found and writable
            if (prm != null || !prm.IsReadOnly)
            {
                _ = prm.Set(mark);
                return true;
            }

            return false;
        }

    }
}
