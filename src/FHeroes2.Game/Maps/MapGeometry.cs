using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The geometry half of <c>src/fheroes2/maps/maps.cpp</c>: the index ↔ point conversions, the
/// direction queries and <c>getAroundIndexes</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference reads the map dimensions from the global <c>world</c> inside every one of these
/// functions. The port has no <c>world</c> yet, and baking width and height into a static field would
/// make the helpers untestable and thread-hostile, so the dimensions are the instance a caller passes
/// around: a <see cref="MapGeometry"/> is the pair <c>(world.w(), world.h())</c> and the functions keep
/// the reference's signature afterwards. When <c>world</c> lands it will hold one of these and forward.
/// </para>
/// <para>
/// Everything here is O(1) arithmetic on a tile index, the same as the reference; none of these
/// functions touch a tile, so they stay usable while a map is still being decoded.
/// </para>
/// </remarks>
public readonly record struct MapGeometry
{
    /// <summary>
    /// Creates the geometry of a map of the given size.
    /// </summary>
    /// <param name="width">Map width in tiles, the reference's <c>world.w()</c>.</param>
    /// <param name="height">Map height in tiles, the reference's <c>world.h()</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
    public MapGeometry(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    /// <summary>Map width in tiles (<c>world.w()</c>).</summary>
    public int Width { get; }

    /// <summary>Map height in tiles (<c>world.h()</c>).</summary>
    public int Height { get; }

    /// <summary>Number of tiles on the map (<c>world.w() * world.h()</c>).</summary>
    public int TileCount => Width * Height;

    /// <summary>
    /// The direction that leads from one tile to another — <c>Maps::GetDirection</c>.
    /// </summary>
    /// <param name="from">Source tile index.</param>
    /// <param name="to">Target tile index.</param>
    /// <returns>The direction of the step, <see cref="Direction.Center"/> for the same tile, or <see cref="Direction.Unknown"/> if the two are not neighbours.</returns>
    public Direction GetDirection(int from, int to)
    {
        if (from == to)
        {
            return Direction.Center;
        }

        int difference = to - from;

        return difference switch
        {
            _ when difference == -Width - 1 => Direction.TopLeft,
            _ when difference == -Width => Direction.Top,
            _ when difference == -Width + 1 => Direction.TopRight,
            -1 => Direction.Left,
            1 => Direction.Right,
            _ when difference == Width - 1 => Direction.BottomLeft,
            _ when difference == Width => Direction.Bottom,
            _ when difference == Width + 1 => Direction.BottomRight,
            _ => Direction.Unknown,
        };
    }

    /// <summary>
    /// The index of the neighbour in the given direction — <c>Maps::GetDirectionIndex</c>.
    /// </summary>
    /// <param name="from">Source tile index.</param>
    /// <param name="direction">Direction of the step, a single bit.</param>
    /// <returns>The neighbour index, or <c>-1</c> if the direction is not a step.</returns>
    public int GetDirectionIndex(int from, Direction direction) => direction switch
    {
        Direction.Top => from - Width,
        Direction.TopRight => from - Width + 1,
        Direction.Right => from + 1,
        Direction.BottomRight => from + Width + 1,
        Direction.Bottom => from + Width,
        Direction.BottomLeft => from + Width - 1,
        Direction.Left => from - 1,
        Direction.TopLeft => from - Width - 1,
        _ => -1,
    };

    /// <summary>
    /// The point one step away in the given direction — <c>Maps::getDirectionPoint</c>.
    /// </summary>
    /// <param name="from">Source point.</param>
    /// <param name="direction">Direction of the step.</param>
    /// <returns>The neighbour point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The direction is not one of the nine.</exception>
    public static Point GetDirectionPoint(Point from, Direction direction) => direction switch
    {
        Direction.Top => new Point(from.X, from.Y - 1),
        Direction.TopRight => new Point(from.X + 1, from.Y - 1),
        Direction.Right => new Point(from.X + 1, from.Y),
        Direction.BottomRight => new Point(from.X + 1, from.Y + 1),
        Direction.Bottom => new Point(from.X, from.Y + 1),
        Direction.BottomLeft => new Point(from.X - 1, from.Y + 1),
        Direction.Left => new Point(from.X - 1, from.Y),
        Direction.TopLeft => new Point(from.X - 1, from.Y - 1),
        Direction.Center => from,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a direction: the reference asserts here."),
    };

    /// <summary>
    /// Whether a step in the given direction stays on the map — <c>Maps::isValidDirection</c>.
    /// </summary>
    /// <param name="from">Source tile index.</param>
    /// <param name="vector">Direction of the step, a single bit.</param>
    /// <returns><c>true</c> if the neighbour would still be inside the map.</returns>
    public bool IsValidDirection(int from, Direction vector) => vector switch
    {
        Direction.Top => from >= Width,
        Direction.Right => from % Width < Width - 1,
        Direction.Bottom => from < Width * (Height - 1),
        Direction.Left => from % Width != 0,
        Direction.TopRight => from >= Width && from % Width < Width - 1,
        Direction.BottomRight => from < Width * (Height - 1) && from % Width < Width - 1,
        Direction.BottomLeft => from < Width * (Height - 1) && from % Width != 0,
        Direction.TopLeft => from >= Width && from % Width != 0,
        _ => false,
    };

    /// <summary>
    /// The point of a tile index — <c>Maps::GetPoint</c>.
    /// </summary>
    /// <param name="index">Tile index.</param>
    /// <returns>The column and the row of the tile.</returns>
    public Point GetPoint(int index) => new(index % Width, index / Width);

    /// <summary>
    /// Whether the index addresses a tile of this map — <c>Maps::isValidAbsIndex</c>.
    /// </summary>
    /// <param name="index">Tile index to test.</param>
    /// <returns><c>true</c> if the index is inside <c>[0, width * height)</c>.</returns>
    public bool IsValidAbsIndex(int index) => index >= 0 && index < TileCount;

    /// <summary>
    /// Whether the point addresses a tile of this map — <c>Maps::isValidAbsPoint</c>.
    /// </summary>
    /// <param name="x">Column to test.</param>
    /// <param name="y">Row to test.</param>
    /// <returns><c>true</c> if the point is inside the map.</returns>
    public bool IsValidAbsPoint(int x, int y) => x >= 0 && Width > x && y >= 0 && Height > y;

    /// <summary>
    /// The index of a point — <c>Maps::GetIndexFromAbsPoint</c>.
    /// </summary>
    /// <param name="point">Column and row, may be outside the map.</param>
    /// <returns>The index, or <c>-1</c> if either coordinate is negative.</returns>
    public int GetIndexFromAbsPoint(Point point) => GetIndexFromAbsPoint(point.X, point.Y);

    /// <summary>
    /// The index of a point — <c>Maps::GetIndexFromAbsPoint</c>.
    /// </summary>
    /// <param name="x">Column, may be outside the map.</param>
    /// <param name="y">Row, may be outside the map.</param>
    /// <returns>The index, or <c>-1</c> if either coordinate is negative.</returns>
    public int GetIndexFromAbsPoint(int x, int y) => x < 0 || y < 0 ? -1 : (y * Width) + x;

    /// <summary>
    /// The squared euclidean distance between two tiles, the comparison of <c>ComparisonDistance</c>
    /// (<c>maps.cpp:184</c>).
    /// </summary>
    /// <remarks>
    /// The reference has no such function: the comparator of <c>maps.cpp</c> builds the squared distance of
    /// both indexes inline every time it is asked. The port keeps the arithmetic in one place because two
    /// other functions need it — <see cref="World.GetObjectPositions(int, FHeroes2.Assets.Maps.Mp2ObjectType, bool)"/>
    /// and <see cref="World.ScanAroundObjectWithDistance"/> both sort by it — and because a comparator
    /// that cannot be called on its own cannot be tested on its own.
    /// </remarks>
    /// <param name="from">Source tile index.</param>
    /// <param name="to">Target tile index.</param>
    /// <returns>The squared distance, in tiles.</returns>
    public int GetSquaredDistance(int from, int to)
    {
        int diffX = (from % Width) - (to % Width);
        int diffY = (from / Width) - (to / Width);

        return (diffX * diffX) + (diffY * diffY);
    }

    /// <summary>
    /// The distance a hero would have to walk between two tiles, <c>Maps::GetApproximateDistance()</c>
    /// (<c>maps.cpp:691</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference's own example: a tile seven rows below and five columns to the right is reached by
    /// five diagonal steps and two straight ones, which costs <c>2 + 5 * 1.5 = 9.5</c> tiles of straight
    /// travel — nine after truncation. The formula is <c>max + min / 2</c> on the two deltas, with the
    /// division truncating, which is exactly that.
    /// </para>
    /// <para>
    /// The result is <b>approximate</b>: it knows nothing about terrain penalties, roads or obstacles. The
    /// reference says the same and tells callers to keep it for cases where precision does not matter.
    /// </para>
    /// </remarks>
    /// <param name="from">Source tile index.</param>
    /// <param name="to">Target tile index.</param>
    /// <returns>The distance in tiles.</returns>
    public uint GetApproximateDistance(int from, int to)
    {
        int diffX = Math.Abs((from % Width) - (to % Width));
        int diffY = Math.Abs((from / Width) - (to / Width));

        return (uint)(Math.Max(diffX, diffY) + (Math.Min(diffX, diffY) / 2));
    }

    /// <summary>
    /// The straight line distance between two tiles, <c>Maps::GetStraightLineDistance()</c>
    /// (<c>maps.cpp:704</c>).
    /// </summary>
    /// <remarks>
    /// <c>std::hypot</c> truncated to an integer, so the result is the floor of the hypotenuse. Like
    /// <see cref="GetApproximateDistance"/> this ignores the terrain, and the reference warns that it is
    /// <b>not</b> a movement point count: a diagonal step costs 1.5 times a straight one, which the
    /// hypotenuse does not express.
    /// </remarks>
    /// <param name="from">Source tile index.</param>
    /// <param name="to">Target tile index.</param>
    /// <returns>The distance in tiles, truncated.</returns>
    public uint GetStraightLineDistance(int from, int to)
    {
        int diffX = Math.Abs((from % Width) - (to % Width));
        int diffY = Math.Abs((from / Width) - (to / Width));

        return (uint)Math.Sqrt((diffX * diffX) + (diffY * diffY));
    }

    /// <summary>
    /// The indexes of the tiles around one tile — <c>Maps::getAroundIndexes</c>.
    /// </summary>
    /// <param name="tileIndex">Index of the centre tile.</param>
    /// <param name="maxDistanceFromTile">Chebyshev radius of the square around the centre, at least 1.</param>
    /// <returns>The indexes of the square without the centre tile, clipped to the map.</returns>
    public List<int> GetAroundIndexes(int tileIndex, int maxDistanceFromTile = 1) =>
        GetAroundIndexes(tileIndex, Width, Height, maxDistanceFromTile);

    /// <summary>
    /// The indexes of the tiles around one tile — <c>Maps::getAroundIndexes</c>, the overload that
    /// takes the dimensions explicitly so it can be called for a map that is not loaded yet.
    /// </summary>
    /// <param name="tileIndex">Index of the centre tile.</param>
    /// <param name="width">Width of the map the index belongs to.</param>
    /// <param name="height">Height of the map the index belongs to.</param>
    /// <param name="maxDistanceFromTile">Chebyshev radius of the square around the centre, at least 1.</param>
    /// <returns>The indexes of the square without the centre tile, clipped to the map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
    public static List<int> GetAroundIndexes(int tileIndex, int width, int height, int maxDistanceFromTile)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        // The reference checks the index against width * height and returns an empty list when it is out
        // of range; the radius has to be at least 1 for the square to have a ring. The comparison is
        // 'index > width * height' and not '>=', which the reference does too: an index of exactly
        // width * height still produces the ring of the row below the map, clipped to what exists.
        if (tileIndex < 0 || tileIndex > width * height || maxDistanceFromTile < 1)
        {
            return [];
        }

        var results = new List<int>((maxDistanceFromTile * 2 + 1) * (maxDistanceFromTile * 2 + 1) - 1);

        int centerX = tileIndex % width;
        int centerY = tileIndex / width;

        // We avoid getting out of map boundaries.
        int minTileX = Math.Max(centerX - maxDistanceFromTile, 0);
        int minTileY = Math.Max(centerY - maxDistanceFromTile, 0);
        int maxTileX = Math.Min(centerX + maxDistanceFromTile + 1, width);
        int maxTileY = Math.Min(centerY + maxDistanceFromTile + 1, height);

        for (int tileY = minTileY; tileY < maxTileY; ++tileY)
        {
            int indexOffsetY = tileY * width;
            bool isCenterY = tileY == centerY;

            for (int tileX = minTileX; tileX < maxTileX; ++tileX)
            {
                // Skip the center tile.
                if (isCenterY && tileX == centerX)
                {
                    continue;
                }

                results.Add(indexOffsetY + tileX);
            }
        }

        return results;
    }
}
