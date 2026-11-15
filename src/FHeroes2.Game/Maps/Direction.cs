using System.Text;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The direction bits of <c>heroes/direction.h</c>: the eight neighbours of a tile plus the tile
/// itself. The numeric values are a bit mask, not an ordinal, so they must not be renumbered.
/// </summary>
/// <remarks>
/// <para>
/// The enum used to live in <c>AdventureMap/FogDirection</c> under that name, because the fog sprite
/// table was the only user: <c>Maps::Tile::getFogDirection</c> reports a tile under the fog as
/// <see cref="Center"/> plus the bit of every fogged neighbour. Porting <c>Maps::GetDirection</c> and
/// friends made it the map-wide direction type the reference has, so it moved here and was renamed.
/// </para>
/// <para>
/// The reference declares the enumeration inside <c>namespace Direction</c>, which C# cannot express
/// together with the static class of that name; the enumeration is <c>Direction</c> and the helpers
/// live in <see cref="Directions"/>.
/// </para>
/// </remarks>
[Flags]
public enum Direction : int
{
    /// <summary>Not a direction: no neighbourhood relation (<c>UNKNOWN</c>).</summary>
    Unknown = 0x0000,

    TopLeft = 0x0001,
    Top = 0x0002,
    TopRight = 0x0004,
    Right = 0x0008,
    BottomRight = 0x0010,
    Bottom = 0x0020,
    BottomLeft = 0x0040,
    Left = 0x0080,

    /// <summary>The tile itself (<c>CENTER</c>).</summary>
    Center = 0x0100,
}

/// <summary>The free functions and the mask macros of <c>direction.h</c>.</summary>
public static class Directions
{
    /// <summary>
    /// The eight neighbours in clockwise order starting at the top left
    /// (<c>Direction::allNeighboringDirections</c>).
    /// </summary>
    public static readonly Direction[] AllNeighboringDirections =
    [
        Direction.TopLeft,
        Direction.Top,
        Direction.TopRight,
        Direction.Right,
        Direction.BottomRight,
        Direction.Bottom,
        Direction.BottomLeft,
        Direction.Left,
    ];

    /// <summary>All three directions of the row above (<c>DIRECTION_TOP_ROW</c>).</summary>
    public const Direction TopRow = Direction.TopLeft | Direction.Top | Direction.TopRight;

    /// <summary>All three directions of the row below (<c>DIRECTION_BOTTOM_ROW</c>).</summary>
    public const Direction BottomRow = Direction.BottomLeft | Direction.Bottom | Direction.BottomRight;

    /// <summary>The left, the tile itself and the right (<c>DIRECTION_CENTER_ROW</c>).</summary>
    public const Direction CenterRow = Direction.Left | Direction.Center | Direction.Right;

    /// <summary>The three directions of the column to the left (<c>DIRECTION_LEFT_COL</c>).</summary>
    public const Direction LeftColumn = Direction.TopLeft | Direction.Left | Direction.BottomLeft;

    /// <summary>The tile above, the tile itself and the tile below (<c>DIRECTION_CENTER_COL</c>).</summary>
    public const Direction CenterColumn = Direction.Top | Direction.Center | Direction.Bottom;

    /// <summary>The three directions of the column to the right (<c>DIRECTION_RIGHT_COL</c>).</summary>
    public const Direction RightColumn = Direction.TopRight | Direction.Right | Direction.BottomRight;

    /// <summary>Every direction (<c>DIRECTION_ALL</c>).</summary>
    public const Direction All = TopRow | BottomRow | CenterRow;

    /// <summary>Everything but the tile itself (<c>DIRECTION_AROUND</c>).</summary>
    public const Direction Around = TopRow | BottomRow | Direction.Left | Direction.Right;

    /// <summary>The two directions of the upper right corner piece (<c>DIRECTION_TOP_RIGHT_CORNER</c>).</summary>
    public const Direction TopRightCorner = Direction.Top | Direction.TopRight | Direction.Right;

    /// <summary>The two directions of the upper left corner piece (<c>DIRECTION_TOP_LEFT_CORNER</c>).</summary>
    public const Direction TopLeftCorner = Direction.Top | Direction.TopLeft | Direction.Left;

    /// <summary>The two directions of the lower right corner piece (<c>DIRECTION_BOTTOM_RIGHT_CORNER</c>).</summary>
    public const Direction BottomRightCorner = Direction.Bottom | Direction.BottomRight | Direction.Right;

    /// <summary>The two directions of the lower left corner piece (<c>DIRECTION_BOTTOM_LEFT_CORNER</c>).</summary>
    public const Direction BottomLeftCorner = Direction.Bottom | Direction.BottomLeft | Direction.Left;

    /// <summary>The four diagonals (<c>DIRECTION_ALL_CORNERS</c>).</summary>
    public const Direction AllCorners = Direction.TopRight | Direction.BottomRight | Direction.BottomLeft | Direction.TopLeft;

    /// <summary>
    /// Whether <paramref name="direction"/> holds all bits of <paramref name="mask"/> — <c>contains()</c>
    /// of <c>direction.h</c>. The fog sprite table is written in terms of this test.
    /// </summary>
    /// <param name="direction">Direction set to test.</param>
    /// <param name="mask">Bits that all have to be present.</param>
    /// <returns><c>true</c> if every bit of the mask is set.</returns>
    public static bool Contains(Direction direction, Direction mask) => (direction & mask) == mask;

    /// <summary>
    /// The names of the set bits, comma separated, or <c>"unknown"</c> — <c>Direction::String</c>.
    /// </summary>
    /// <param name="direction">Direction set to describe.</param>
    /// <returns>The reference's debugging spelling of the set.</returns>
    public static string String(Direction direction)
    {
        // The reference appends one term per bit in this order and then trims the trailing separator.
        var text = new StringBuilder();

        void Append(Direction bit, string name)
        {
            if ((direction & bit) != 0)
            {
                text.Append(name).Append(", ");
            }
        }

        Append(Direction.Center, "center");
        Append(Direction.Top, "top");
        Append(Direction.TopRight, "top-right");
        Append(Direction.Right, "right");
        Append(Direction.BottomRight, "bottom-right");
        Append(Direction.Bottom, "bottom");
        Append(Direction.BottomLeft, "bottom-left");
        Append(Direction.Left, "left");
        Append(Direction.TopLeft, "top-left");

        string result = text.ToString().TrimEnd(' ');
        result = result.TrimEnd(',');

        return result.Length == 0 ? "unknown" : result;
    }

    /// <summary>Whether the set contains a diagonal — <c>Direction::isDiagonal</c>.</summary>
    /// <param name="direction">Direction set to test.</param>
    /// <returns><c>true</c> if any corner bit is set.</returns>
    public static bool IsDiagonal(Direction direction) => (direction & AllCorners) != Direction.Unknown;

    /// <summary>
    /// The direction on the opposite side — <c>Direction::Reflect</c>.
    /// </summary>
    /// <param name="direction">Direction to reflect, a single bit.</param>
    /// <returns>The opposite direction, or <see cref="Direction.Unknown"/> for a set.</returns>
    public static Direction Reflect(Direction direction) => direction switch
    {
        Direction.TopLeft => Direction.BottomRight,
        Direction.Top => Direction.Bottom,
        Direction.TopRight => Direction.BottomLeft,
        Direction.Right => Direction.Left,
        Direction.BottomRight => Direction.TopLeft,
        Direction.Bottom => Direction.Top,
        Direction.BottomLeft => Direction.TopRight,
        Direction.Left => Direction.Right,
        Direction.Center => Direction.Center,
        _ => Direction.Unknown,
    };

    /// <summary>
    /// Whether <paramref name="to"/> comes within four clockwise steps of <paramref name="from"/>
    /// — <c>Direction::ShortDistanceClockWise</c>.
    /// </summary>
    /// <param name="from">Starting direction, a single bit.</param>
    /// <param name="to">Target direction, a single bit.</param>
    /// <returns><c>true</c> if the clockwise arc from <paramref name="from"/> to <paramref name="to"/> is shorter than half a turn.</returns>
    public static bool ShortDistanceClockWise(Direction from, Direction to) => from switch
    {
        Direction.Top => to is Direction.TopRight or Direction.Right or Direction.BottomRight or Direction.Bottom,
        Direction.TopRight => to is Direction.Right or Direction.BottomRight or Direction.Bottom or Direction.BottomLeft,
        Direction.Right => to is Direction.BottomRight or Direction.Bottom or Direction.BottomLeft or Direction.Left,
        Direction.BottomRight => to is Direction.Bottom or Direction.BottomLeft or Direction.Left or Direction.TopLeft,
        Direction.Bottom => to is Direction.BottomLeft or Direction.Left or Direction.TopLeft,
        Direction.BottomLeft => to is Direction.Top or Direction.TopRight or Direction.Left or Direction.TopLeft,
        Direction.Left => to is Direction.Top or Direction.TopRight or Direction.Right or Direction.TopLeft,
        Direction.TopLeft => to is Direction.Top or Direction.TopRight or Direction.Right or Direction.BottomRight,
        _ => false,
    };
}
