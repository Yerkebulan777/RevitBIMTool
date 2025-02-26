namespace RevitBIMTool.Model
{

    /// <summary>
    /// Represents lintel dimensions data
    /// </summary>
    public class LintelData
    {
        /// <summary>
        /// Wall thickness in mm
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// Opening width in mm
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Opening height in mm
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Rounded wall thickness in mm
        /// </summary>
        public int ThicknessRound { get; set; }

        /// <summary>
        /// Rounded opening width in mm
        /// </summary>
        public int WidthRound { get; set; }

        /// <summary>
        /// Rounded opening height in mm
        /// </summary>
        public int HeightRound { get; set; }

        /// <summary>
        /// Group identifier based on rounded dimensions
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Assigned mark
        /// </summary>
        public string Mark { get; set; }
    }

}
