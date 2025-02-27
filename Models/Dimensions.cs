namespace RevitBIMTool.Models
{
    public struct Dimensions : IEquatable<Dimensions>
    {
        public double Thick { get; }
        public double Width { get; }
        public double Height { get; }

        public Dimensions(double thick, double width, double height)
        {
            Thick = thick;
            Width = width;
            Height = height;
        }

        public override bool Equals(object obj)
        {
            return obj is Dimensions dimensions && Equals(dimensions);
        }

        public bool Equals(Dimensions other)
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
