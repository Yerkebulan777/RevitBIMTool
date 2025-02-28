namespace RevitBIMTool.Model
{
    /// <summary>
    /// Конфигурация алгоритма маркировки
    /// </summary>
    public class MarkConfig
    {
        /// <summary>
        /// Максимальное допустимое отклонение для объединения групп (мм)
        /// </summary>
        public int MaxTotalDeviation { get; set; } = 150;

        /// <summary>
        /// Минимальное количество элементов для "большой" группы
        /// </summary>
        public int MinCount { get; set; } = 5;

        /// <summary>
        /// Базовое значение для округления (мм)
        /// </summary>
        public int RoundBase { get; set; } = 50;

        /// <summary>
        /// Префикс для марок
        /// </summary>
        public string Prefix { get; set; } = "ПР-";

        /// <summary>
        /// Имя параметра для толщины стены
        /// </summary>
        public string ThickParam { get; set; } = "Толщина стены";

        /// <summary>
        /// Имя параметра для ширины проема
        /// </summary>
        public string WidthParam { get; set; } = "Ширина проема";

        /// <summary>
        /// Имя параметра для высоты
        /// </summary>
        public string HeightParam { get; set; } = "Высота";

        /// <summary>
        /// Имя параметра для марки
        /// </summary>
        public string MarkParam { get; set; } = "BI_марка_изделия";

        /// <summary>
        /// Наименование семейства перемычек
        /// </summary>
        public string FamilyName { get; set; } = "Перемычка";

        /// <summary>
        /// Допуск для ширины проема (мм)
        /// </summary>
        public int WidthTolerance { get; set; } = 25;

        /// <summary>
        /// Допуск для толщины стены (мм)
        /// </summary>
        public int ThickTolerance { get; set; } = 50;

        /// <summary>
        /// Допуск для высоты (мм)
        /// </summary>
        public int HeightTolerance { get; set; } = 100;

        /// <summary>
        /// Порядок сортировки параметров при группировке (1-толщина, 2-ширина, 3-высота)
        /// </summary>
        public List<GroupingParameter> GroupingOrder { get; set; } = new List<GroupingParameter>
        {
            GroupingParameter.Thick,
            GroupingParameter.Width,
            GroupingParameter.Height
        };
    }

    /// <summary>
    /// Параметры перемычки для группировки
    /// </summary>
    public enum GroupingParameter
    {
        /// <summary>
        /// Толщина стены
        /// </summary>
        Thick,

        /// <summary>
        /// Ширина проема
        /// </summary>
        Width,

        /// <summary>
        /// Высота проема
        /// </summary>
        Height
    }
}