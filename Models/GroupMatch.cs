using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitBIMTool.Models
{
    /// <summary>
    /// Класс для хранения информации о совпадении групп
    /// </summary>
    /// <param name="Source"></param>
    /// <param name="Target"></param>
    /// <param name="Score"></param>
    /// 
    public readonly record struct GroupMatch
    {
        /// <summary>
        /// Исходная группа (малая)
        /// </summary>
        public SizeKey Source { get; }

        /// <summary>
        /// Целевая группа для объединения
        /// </summary>
        public SizeKey Target { get; }

        /// <summary>
        /// Оценка сходства (меньше — лучше)
        /// </summary>
        public double Score { get; }

        public GroupMatch(SizeKey source, SizeKey target, double score)
        {
            Source = source;
            Target = target;
            Score = score;
        }
    }
}
