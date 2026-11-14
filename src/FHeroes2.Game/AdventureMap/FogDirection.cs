namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The eight neighbours of a tile plus the tile itself: the <c>Direction::</c> bits of
/// <c>direction.h</c>.
/// </summary>
/// <remarks>
/// A tile that is under the fog reports <see cref="Center"/> plus the bit of every neighbour that is
/// under the fog as well (<c>Maps::Tile::getFogDirection</c>). That mask is what picks the fog sprite
/// of the tile: a tile whose neighbours are all fogged as well is covered by one of the four
/// <c>CLOF32</c> tiles, every other mask is one of the thirty-four <c>CLOP32</c> pieces.
/// </remarks>
[Flags]
public enum FogDirection : ushort
{
    /// <summary>The tile is not under the fog (or the fog was never computed, which the reference treats the same).</summary>
    Unknown = 0x0000,

    TopLeft = 0x0001,
    Top = 0x0002,
    TopRight = 0x0004,
    Right = 0x0008,
    BottomRight = 0x0010,
    Bottom = 0x0020,
    BottomLeft = 0x0040,
    Left = 0x0080,

    /// <summary>The tile itself is under the fog.</summary>
    Center = 0x0100,
}

/// <summary>The direction sets of <c>direction.h</c> and the subset test they are used with.</summary>
public static class FogDirections
{
    /// <summary>All three directions of the row above (<c>DIRECTION_TOP_ROW</c>).</summary>
    public const FogDirection TopRow = FogDirection.TopLeft | FogDirection.Top | FogDirection.TopRight;

    /// <summary>All three directions of the row below (<c>DIRECTION_BOTTOM_ROW</c>).</summary>
    public const FogDirection BottomRow = FogDirection.BottomLeft | FogDirection.Bottom | FogDirection.BottomRight;

    /// <summary>The left, the tile itself and the right (<c>DIRECTION_CENTER_ROW</c>).</summary>
    public const FogDirection CenterRow = FogDirection.Left | FogDirection.Center | FogDirection.Right;

    /// <summary>The three directions of the column to the left (<c>DIRECTION_LEFT_COL</c>).</summary>
    public const FogDirection LeftColumn = FogDirection.TopLeft | FogDirection.Left | FogDirection.BottomLeft;

    /// <summary>The tile above, the tile itself and the tile below (<c>DIRECTION_CENTER_COL</c>).</summary>
    public const FogDirection CenterColumn = FogDirection.Top | FogDirection.Center | FogDirection.Bottom;

    /// <summary>The three directions of the column to the right (<c>DIRECTION_RIGHT_COL</c>).</summary>
    public const FogDirection RightColumn = FogDirection.TopRight | FogDirection.Right | FogDirection.BottomRight;

    /// <summary>Everything but the tile itself (<c>DIRECTION_NEIGHBORS</c>).</summary>
    public const FogDirection Neighbors = TopRow | BottomRow | FogDirection.Left | FogDirection.Right;

    /// <summary>Every direction, the mask of a tile in the middle of a fogged area (<c>DIRECTION_ALL</c>).</summary>
    public const FogDirection All = Neighbors | FogDirection.Center;

    /// <summary>The two directions of the upper right corner piece (<c>DIRECTION_TOP_RIGHT_CORNER</c>).</summary>
    public const FogDirection TopRightCorner = FogDirection.Top | FogDirection.TopRight | FogDirection.Right;

    /// <summary>The two directions of the lower right corner piece (<c>DIRECTION_BOTTOM_RIGHT_CORNER</c>).</summary>
    public const FogDirection BottomRightCorner = FogDirection.Right | FogDirection.BottomRight | FogDirection.Bottom;

    /// <summary>The two directions of the lower left corner piece (<c>DIRECTION_BOTTOM_LEFT_CORNER</c>).</summary>
    public const FogDirection BottomLeftCorner = FogDirection.Bottom | FogDirection.BottomLeft | FogDirection.Left;

    /// <summary>The two directions of the upper left corner piece (<c>DIRECTION_TOP_LEFT_CORNER</c>).</summary>
    public const FogDirection TopLeftCorner = FogDirection.Left | FogDirection.TopLeft | FogDirection.Top;

    /// <summary>
    /// Whether <paramref name="value"/> holds all bits of <paramref name="mask"/> — <c>contains()</c>
    /// of <c>direction.h</c>, the test the fog sprite table is written in.
    /// </summary>
    public static bool Contains(FogDirection value, FogDirection mask) => (value & mask) == mask;
}
