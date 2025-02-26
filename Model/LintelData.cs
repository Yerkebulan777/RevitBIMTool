﻿namespace RevitBIMTool.Model
{

    /// <summary>
    /// Represents lintel dimensions data
    /// </summary>
    public class LintelData
    {
        /// <summary>
        /// Толщина стены
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// Высота проема
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Ширина проема
        /// </summary>
        public double Width { get; set; }

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
