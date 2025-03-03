using Autodesk.Revit.DB;
using RevitBIMTool.Models;

namespace RevitBIMTool.Model
{

    /// <summary>
    /// Represents lintel dimensions data
    /// </summary>
    public class LintelData
    {
        public readonly FamilyInstance Instance;

        public LintelData(FamilyInstance lintel)
        {
            Instance = lintel;
        }

        /// <summary>
        /// Толщина стены
        /// </summary>
        public double Thick { get; set; }

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
        public SizeKey Size { get; set; }

        /// <summary>
        /// Присвоенная марка
        /// </summary>
        public string Mark { get; set; }
    }

}
