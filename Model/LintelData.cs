namespace RevitBIMTool.Model
{

    /// <summary>
    /// Represents lintel dimensions data
    /// </summary>
    public class LintelData
    {
        /// <summary>
        /// Округленная толщина стены
        /// </summary>
        public double ThicknessRound { get; set; }

        /// <summary>
        /// Округленная ширина проема
        /// </summary>
        public double WidthRound { get; set; }

        /// <summary>
        /// Округленная высота
        /// </summary>
        public double HeightRound { get; set; }

        /// <summary>
        /// Идентификатор группы
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Присвоенная марка
        /// </summary>
        public string Mark { get; set; }
    }

}
