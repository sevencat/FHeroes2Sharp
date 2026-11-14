namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// One piece of fog: either a whole tile of <c>CLOF32</c> or a <c>CLOP32</c> frame drawn at the
/// corner of a tile.
/// </summary>
/// <param name="IsFullTile">
/// <c>true</c> when the tile is fogged on all eight sides and the whole tile is one of the four
/// <c>CLOF32</c> images; <c>false</c> when <paramref name="Index"/> is a <c>CLOP32</c> frame.
/// </param>
/// <param name="Index">Frame of the sprite source to draw.</param>
/// <param name="Flipped">The frame is mirrored horizontally, as <c>BlitOnTile( ..., revert )</c> does.</param>
public readonly record struct FogFrame(bool IsFullTile, int Index, bool Flipped);

/// <summary>
/// The sprite choice of <c>Maps::drawFog</c>: which <c>CLOF32</c> tile or which <c>CLOP32</c> frame
/// (and whether it is mirrored) covers a tile with a given fog mask.
/// </summary>
/// <remarks>
/// The reference writes this as one long if/else chain with a TODO to turn it into a lookup table;
/// the chain is ported in its original order because the order decides between the nine masks that
/// several branches accept.
/// </remarks>
public static class FogFrames
{
    /// <summary>Number of frames in <c>CLOP32.ICN</c>, the tile sized pieces of the fog boundary.</summary>
    public const int BoundaryFrameCount = 34;

    /// <summary>Number of images in <c>CLOF32.TIL</c>, the four whole tiles of the fog.</summary>
    public const int FullTileCount = 4;

    private const FogDirection Top = FogDirection.Top;
    private const FogDirection TopRight = FogDirection.TopRight;
    private const FogDirection Right = FogDirection.Right;
    private const FogDirection BottomRight = FogDirection.BottomRight;
    private const FogDirection Bottom = FogDirection.Bottom;
    private const FogDirection BottomLeft = FogDirection.BottomLeft;
    private const FogDirection Left = FogDirection.Left;
    private const FogDirection TopLeft = FogDirection.TopLeft;
    private const FogDirection Straight = Top | Bottom | Left | Right;

    /// <summary>
    /// Resolves the fog piece of a tile. <paramref name="column"/> plus <paramref name="row"/> picks
    /// the whole tile variant and <paramref name="row"/> times <paramref name="width"/> plus
    /// <paramref name="column"/> is the tile index the two row pieces are split by.
    /// </summary>
    public static FogFrame Resolve(FogDirection direction, int column, int row, int width)
    {
        if (direction == FogDirections.All)
        {
            return new FogFrame(true, (column + row) % FullTileCount, false);
        }

        int tileIndex = (row * width) + column;
        int evenOdd = tileIndex % 2;
        int index;
        bool flipped = false;

        if ((direction & Straight) == 0)
        {
            index = 10;
        }
        else if (Contains(direction, Top) && (direction & (Bottom | Left | Right)) == 0)
        {
            index = 6;
        }
        else if (Contains(direction, Right) && (direction & (Top | Bottom | Left)) == 0)
        {
            index = 7;
        }
        else if (Contains(direction, Left) && (direction & (Top | Bottom | Right)) == 0)
        {
            index = 7;
            flipped = true;
        }
        else if (Contains(direction, Bottom) && (direction & (Top | Left | Right)) == 0)
        {
            index = 8;
        }
        else if (Contains(direction, FogDirections.CenterColumn) && (direction & (Left | Right)) == 0)
        {
            index = 9;
        }
        else if (Contains(direction, FogDirections.CenterRow) && (direction & (Top | Bottom)) == 0)
        {
            index = 29;
        }
        else if (direction == (FogDirections.All & ~TopRight))
        {
            index = 15;
        }
        else if (direction == (FogDirections.All & ~TopLeft))
        {
            index = 15;
            flipped = true;
        }
        else if (direction == (FogDirections.All & ~BottomRight))
        {
            index = 22;
        }
        else if (direction == (FogDirections.All & ~BottomLeft))
        {
            index = 22;
            flipped = true;
        }
        else if (direction == (FogDirections.All & ~(TopRight | BottomRight)))
        {
            index = 16;
        }
        else if (direction == (FogDirections.All & ~(TopLeft | BottomLeft)))
        {
            index = 16;
            flipped = true;
        }
        else if (direction == (FogDirections.All & ~(TopRight | BottomLeft)))
        {
            index = 17;
        }
        else if (direction == (FogDirections.All & ~(TopLeft | BottomRight)))
        {
            index = 17;
            flipped = true;
        }
        else if (direction == (FogDirections.All & ~(TopLeft | TopRight)))
        {
            index = 18;
        }
        else if (direction == (FogDirections.All & ~(BottomLeft | BottomRight)))
        {
            index = 23;
        }
        else if (direction == (FogDirections.All & ~FogDirections.TopRightCorner))
        {
            index = 13;
        }
        else if (direction == (FogDirections.All & ~FogDirections.TopLeftCorner))
        {
            index = 13;
            flipped = true;
        }
        else if (direction == (FogDirections.All & ~FogDirections.BottomRightCorner))
        {
            index = 14;
        }
        else if (direction == (FogDirections.All & ~FogDirections.BottomLeftCorner))
        {
            index = 14;
            flipped = true;
        }
        else if (Contains(direction, Left | BottomLeft | Bottom) && (direction & (Top | Right)) == 0)
        {
            index = 11;
        }
        else if (Contains(direction, Right | BottomRight | Bottom) && (direction & (Top | Left)) == 0)
        {
            index = 11;
            flipped = true;
        }
        else if (Contains(direction, Left | TopLeft | Top) && (direction & (Bottom | Right)) == 0)
        {
            index = 12;
        }
        else if (Contains(direction, Right | TopRight | Top) && (direction & (Bottom | Left)) == 0)
        {
            index = 12;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom | Top | TopLeft)
                 && (direction & (BottomLeft | BottomRight | TopRight)) == 0)
        {
            index = 19;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom | Top | TopRight)
                 && (direction & (BottomLeft | BottomRight | TopLeft)) == 0)
        {
            index = 19;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom | Top | BottomLeft)
                 && (direction & (TopRight | BottomRight | TopLeft)) == 0)
        {
            index = 20;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom | Top | BottomRight)
                 && (direction & (TopRight | BottomLeft | TopLeft)) == 0)
        {
            index = 20;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom | Top)
                 && (direction & (TopRight | BottomRight | BottomLeft | TopLeft)) == 0)
        {
            index = 21;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom | BottomLeft)
                 && (direction & (Top | BottomRight)) == 0)
        {
            index = 24;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom | BottomRight)
                 && (direction & (Top | BottomLeft)) == 0)
        {
            index = 24;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterColumn | Left | TopLeft)
                 && (direction & (Right | BottomLeft)) == 0)
        {
            index = 25;
        }
        else if (Contains(direction, FogDirections.CenterColumn | Right | TopRight)
                 && (direction & (Left | BottomRight)) == 0)
        {
            index = 25;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterColumn | BottomLeft | Left)
                 && (direction & (Right | TopLeft)) == 0)
        {
            index = 26;
        }
        else if (Contains(direction, FogDirections.CenterColumn | BottomRight | Right)
                 && (direction & (Left | TopRight)) == 0)
        {
            index = 26;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterRow | TopLeft | Top)
                 && (direction & (Bottom | TopRight)) == 0)
        {
            index = 30;
        }
        else if (Contains(direction, FogDirections.CenterRow | TopRight | Top)
                 && (direction & (Bottom | TopLeft)) == 0)
        {
            index = 30;
            flipped = true;
        }
        else if (Contains(direction, Bottom | Left) && (direction & (Top | Right | BottomLeft)) == 0)
        {
            index = 27;
        }
        else if (Contains(direction, Bottom | Right) && (direction & (Top | Left | BottomRight)) == 0)
        {
            index = 27;
            flipped = true;
        }
        else if (Contains(direction, Left | Top) && (direction & (TopLeft | Right | Bottom)) == 0)
        {
            index = 28;
        }
        else if (Contains(direction, Right | Top) && (direction & (TopRight | Left | Bottom)) == 0)
        {
            index = 28;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterRow | Top)
                 && (direction & (Bottom | TopLeft | TopRight)) == 0)
        {
            index = 31;
        }
        else if (Contains(direction, FogDirections.CenterColumn | Right)
                 && (direction & (Left | TopRight | BottomRight)) == 0)
        {
            index = 32;
        }
        else if (Contains(direction, FogDirections.CenterColumn | Left)
                 && (direction & (Right | TopLeft | BottomLeft)) == 0)
        {
            index = 32;
            flipped = true;
        }
        else if (Contains(direction, FogDirections.CenterRow | Bottom)
                 && (direction & (Top | BottomLeft | BottomRight)) == 0)
        {
            index = 33;
        }
        else if (Contains(direction, FogDirections.CenterRow | FogDirections.BottomRow) && (direction & Top) == 0)
        {
            index = evenOdd != 0 ? 0 : 1;
        }
        else if (Contains(direction, FogDirections.CenterRow | FogDirections.TopRow) && (direction & Bottom) == 0)
        {
            index = evenOdd != 0 ? 4 : 5;
        }
        else if (Contains(direction, FogDirections.CenterColumn | FogDirections.LeftColumn) && (direction & Right) == 0)
        {
            index = evenOdd != 0 ? 2 : 3;
        }
        else if (Contains(direction, FogDirections.CenterColumn | FogDirections.RightColumn) && (direction & Left) == 0)
        {
            index = evenOdd != 0 ? 2 : 3;
            flipped = true;
        }
        else
        {
            // The reference logs "invalid direction for fog" and paints a whole fogged tile instead.
            return new FogFrame(true, (column + row) % FullTileCount, false);
        }

        return new FogFrame(false, index, flipped);
    }

    private static bool Contains(FogDirection value, FogDirection mask) => (value & mask) == mask;
}
