namespace FHeroes2.Core.Geometry;

/// <summary>
/// Two-dimensional integer position. Plays the role of <c>fheroes2::Point</c>.
/// </summary>
public readonly record struct Point(int X, int Y) : IComparable<Point>
{
    public static readonly Point Zero = new(0, 0);

    public static Point operator +(Point left, Point right) => new(left.X + right.X, left.Y + right.Y);

    public static Point operator -(Point left, Point right) => new(left.X - right.X, left.Y - right.Y);

    public static Point operator *(Point point, int factor) => new(point.X * factor, point.Y * factor);

    public Point Offset(int dx, int dy) => new(X + dx, Y + dy);

    public int CompareTo(Point other) => X != other.X ? X.CompareTo(other.X) : Y.CompareTo(other.Y);

    public override string ToString() => $"({X}, {Y})";
}
