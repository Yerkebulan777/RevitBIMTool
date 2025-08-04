using System;

namespace Database
{
    public static class StringExtensions
    {
        /// <summary>
        /// Проверяет, содержит ли строка указанное значение с учетом параметров сравнения
        /// </summary>
        public static bool Contains(this string source, string value, StringComparison comparisonType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                string message = "StringExtensions.Contains: value is null or whitespace";
                throw new ArgumentNullException(message);
            }

            return source.IndexOf(value, comparisonType) >= 0;
        }
    }
}