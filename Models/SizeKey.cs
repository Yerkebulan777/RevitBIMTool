namespace RevitBIMTool.Models
{
    public struct SizeKey : IEquatable<SizeKey>
    {
        public double Thick { get; }
        public double Width { get; }
        public double Height { get; }

        public SizeKey(double thick, double width, double height)
        {
            Thick = thick;
            Width = width;
            Height = height;
        }

        public override bool Equals(object obj)
        {
            return obj is SizeKey dimensions && Equals(dimensions);
        }

        public bool Equals(SizeKey other)
        {
            return Thick == other.Thick &&
                   Width == other.Width &&
                   Height == other.Height;
        }

        public override int GetHashCode()
        {
            return Convert.ToInt32((Thick * 10000) + (Width * 100) + Height);
        }
    }
}
