namespace RevitBIMTool.Models
{
    /// <summary>
    /// Структура для хранения и сравнения ключевых размеров перемычки
    /// </summary>
    public struct SizeKey : IEquatable<SizeKey>
    {
        /// <summary>
        /// Толщина стены
        /// </summary>
        public double Thick { get; }

        /// <summary>
        /// Ширина проема
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Высота
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Создает новый экземпляр ключа размеров
        /// </summary>
        /// <param name="thick">Толщина стены</param>
        /// <param name="width">Ширина проема</param>
        /// <param name="height">Высота</param>
        public SizeKey(double thick, double width, double height)
        {
            Thick = thick;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Сравнивает данный объект с другим объектом
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is SizeKey dimensions && Equals(dimensions);
        }

        /// <summary>
        /// Сравнивает данный экземпляр с другим экземпляром
        /// </summary>
        public bool Equals(SizeKey other)
        {
            return Thick == other.Thick &&
                   Width == other.Width &&
                   Height == other.Height;
        }

        /// <summary>
        /// Получает хеш-код для текущего экземпляра
        /// </summary>
        public override int GetHashCode()
        {
            return Convert.ToInt32((Thick * 10000) + (Width * 100) + Height);
        }

        /// <summary>
        /// Возвращает строковое представление размеров
        /// </summary>
        public override string ToString()
        {
            return $"{Thick}x{Width}x{Height}";
        }
    }
}