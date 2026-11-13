namespace FHeroes2.Core.Geometry;

/// <summary>
/// Axis aligned rectangle described by its top-left corner and its size.
/// Plays the role of <c>fheroes2::Rect</c>; half-open on the right/bottom edges,
/// which matches the C++ blitting semantics.
/// </summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public static readonly Rect Empty = new(0, 0, 0, 0);

    public Rect(Point position, Size size)
        : this(position.X, position.Y, size.Width, size.Height)
    {
    }

    public int Left => X;

    public int Top => Y;

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public int Area => Width * Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public Point Position => new(X, Y);

    public Size Size => new(Width, Height);

    public Point Center => new(X + (Width / 2), Y + (Height / 2));

    public bool Contains(Point point) => point.X >= X && point.Y >= Y && point.X < Right && point.Y < Bottom;

    public bool Contains(in Rect other) => !other.IsEmpty && other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    public bool Intersects(in Rect other) => !IsEmpty && !other.IsEmpty && X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public Rect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    public Rect Inflate(int dx, int dy) => new(X - dx, Y - dy, Width + (dx * 2), Height + (dy * 2));

    /// <summary>Returns the overlapping part of both rectangles or <see cref="Empty"/>.</summary>
    public Rect Intersect(in Rect other)
    {
        if (!Intersects(other))
        {
            return Empty;
        }

        int left = Math.Max(X, other.X);
        int top = Math.Max(Y, other.Y);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);

        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>Clips this rectangle by <paramref name="bounds"/>.</summary>
    public Rect Clip(in Rect bounds) => Intersect(bounds);

    public static Rect Union(in Rect first, in Rect second)
    {
        if (first.IsEmpty)
        {
            return second;
        }

        if (second.IsEmpty)
        {
            return first;
        }

        int left = Math.Min(first.X, second.X);
        int top = Math.Min(first.Y, second.Y);
        int right = Math.Max(first.Right, second.Right);
        int bottom = Math.Max(first.Bottom, second.Bottom);

        return new Rect(left, top, right - left, bottom - top);
    }

    public override string ToString() => $"[{X}, {Y}, {Width}x{Height}]";
}
