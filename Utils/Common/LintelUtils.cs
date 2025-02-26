namespace RevitBIMTool.Utils.Common
{
    using Autodesk.Revit.DB;
    using System;

    /// <summary>
    /// Утилиты для работы с перемычками
    /// </summary>
    public static class LintelUtils
    {
        /// <summary>
        /// Устанавливает наименование типа в системный параметр
        /// </summary>
        public static void SetTypeName(FamilyInstance lintel, string name)
        {
            // Получаем встроенный параметр для наименования типа
            Parameter param = lintel.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME);

            // Устанавливаем значение, если параметр доступен для записи
            if (param != null && !param.IsReadOnly)
            {
                param.Set(name);
            }
        }

        /// <summary>
        /// Устанавливает марку в общий параметр проекта
        /// </summary>
        public static void SetMark(FamilyInstance lintel, string paramName, string mark)
        {
            // Ищем проектный параметр BI_марка_изделия
            Parameter prm = lintel.LookupParameter(paramName);

            if (prm != null && !prm.IsReadOnly)
            {
                prm.Set(mark);
            }
        }

        /// <summary>
        /// Получает значение числового параметра
        /// </summary>
        public static double GetParamValue(FamilyInstance instance, string paramName)
        {
            Parameter prm = instance.LookupParameter(paramName);

            if (prm != null && prm.HasValue)
            {
                if (prm.StorageType == StorageType.Double)
                {
                    return UnitManager.FootToMm(prm.AsDouble());
                }
            }

            return 0;
        }
    }

}
