namespace RevitBIMTool.Model
{
    /// <summary>
    /// Configuration parameters for lintel marking algorithm
    /// </summary>
    public class MarkingConfig
    {
        /// <summary>
        /// Maximum allowed deviation in mm for merging elements
        /// </summary>
        public int Threshold { get; set; } = 50;

        /// <summary>
        /// Minimum count of elements to consider a group as "large"
        /// </summary>
        public int MinGroupSize { get; set; } = 3;

        /// <summary>
        /// Mark prefix (default: "ПР-")
        /// </summary>
        public string MarkPrefix { get; set; } = "ПР-";

        /// <summary>
        /// Parameter name for wall thickness
        /// </summary>
        public string ThicknessParam { get; set; } = "Толщина стены";

        /// <summary>
        /// Parameter name for opening width
        /// </summary>
        public string WidthParam { get; set; } = "Ширина проема";

        /// <summary>
        /// Parameter name for opening height
        /// </summary>
        public string HeightParam { get; set; } = "Высота";

        /// <summary>
        /// Parameter name for mark
        /// </summary>
        public string MarkParam { get; set; } = "BI_марка_изделия";
    }
}
