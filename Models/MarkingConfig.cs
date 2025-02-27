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
        public int Threshold { get; set; } = 150;

        /// <summary>
        /// Минимальное количество элементов для "большой" группы
        /// </summary>
        public int MinCount { get; set; } = 3;

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
        public string ThicknessParam { get; set; } = "Толщина стены";

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
    }
}
