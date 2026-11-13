namespace FHeroes2.Core.Geometry;

/// <summary>
/// Two-dimensional size. Plays the role of <c>fheroes2::Size</c>.
/// </summary>
public readonly record struct Size(int Width, int Height)
{
    public static readonly Size Empty = new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public int Area => Width * Height;

    public static Size operator *(Size size, int factor) => new(size.Width * factor, size.Height * factor);

    public static Size operator /(Size size, int divisor) => new(size.Width / divisor, size.Height / divisor);

    public Size Union(Size other) => new(Math.Max(Width, other.Width), Math.Max(Height, other.Height));

    public override string ToString() => $"{Width}x{Height}";
}
