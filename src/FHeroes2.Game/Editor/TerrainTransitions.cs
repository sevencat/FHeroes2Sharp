using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Editor;

/// <summary>
/// Recomputes the terrain image of one tile so that it blends with its eight neighbours: the port of
/// <c>updateTerrainTransitionOnTile</c> and <c>setTerrainBoundaries</c> from <c>map_format_helper.cpp</c>
/// of the reference.
/// </summary>
/// <remarks>
/// <para>
/// A tile does not store its ground in MP2: the ground is implied by the terrain image index, and the
/// index itself says how the tile ends. Every ground owns a block of <c>GROUND32.TIL</c> images (water
/// at 0, grass at 30, snow at 92, ... beach at 415); the offset inside a block is the transition, and
/// the block is flipped through the shape bits for the other three sides. The "plain" image of a block
/// starts at +38 for grass, snow, swamp, lava, desert and wasteland, at +16 for water and dirt, and at
/// +0 for beach, each with eight variants.
/// </para>
/// <para>
/// The reference picks among those variants with <c>Rand::Get</c>. A painted tile has to be
/// reproducible — the same click must produce the same bytes, and a screenshot baseline cannot depend
/// on a random generator — so the variant is derived from the tile coordinates instead. That is the
/// only deliberate difference from the reference, besides not falling back to another ground type
/// when no image exists.
/// </para>
/// </remarks>
internal static class TerrainTransitions
{
    // Direction bits, mirroring Direction:: of the reference.
    private const int TopLeft = 0x01;
    private const int Top = 0x02;
    private const int TopRight = 0x04;
    private const int Right = 0x08;
    private const int BottomRight = 0x10;
    private const int Bottom = 0x20;
    private const int BottomLeft = 0x40;
    private const int Left = 0x80;
    private const int Center = 0x0100;

    private const int TopRow = TopLeft | Top | TopRight;
    private const int BottomRow = BottomLeft | Bottom | BottomRight;
    private const int CenterRow = Left | Center | Right;
    private const int All = TopRow | BottomRow | CenterRow;

    /// <summary>Neighbours in the order the reference walks them.</summary>
    private static readonly (int Direction, int Dx, int Dy)[] Neighbours =
    [
        (TopLeft, -1, -1),
        (Top, 0, -1),
        (TopRight, 1, -1),
        (Right, 1, 0),
        (BottomRight, 1, 1),
        (Bottom, 0, 1),
        (BottomLeft, -1, 1),
        (Left, -1, 0),
    ];

    /// <summary>
    /// The image and shape a tile should hold to blend with its surroundings.
    /// </summary>
    /// <param name="map">Map the tile belongs to.</param>
    /// <param name="tile">Tile to look at, must be inside the map.</param>
    /// <param name="imageIndex">Terrain image index to write.</param>
    /// <param name="shape">Terrain shape (bits 0 and 1) to write.</param>
    /// <returns>
    /// Whether there is an image for this combination. <see langword="false"/> means "leave the tile as
    /// it is": the reference tries another ground type in that case, which an editor should not do
    /// behind the user's back. Beach tiles never need a transition.
    /// </returns>
    public static bool TryGetBlend(Mp2Map map, Point tile, out ushort imageIndex, out byte shape)
    {
        imageIndex = 0;
        shape = 0;

        MapGround ground = GroundAt(map, tile.X, tile.Y);

        if (ground is MapGround.Unknown or MapGround.Beach)
        {
            return false;
        }

        // Dirt only blends with water and beach; every other ground blends with itself. The centre bit
        // is set when the tile itself already has the ground, which makes "all bits" mean "nothing to
        // blend with".
        int direction = ground == MapGround.Dirt
            ? All & ~(GetDirection(map, tile, MapGround.Water) | GetDirection(map, tile, MapGround.Beach))
            : GetDirection(map, tile, ground) | Center;

        if (direction == All)
        {
            // Nothing nearby to blend with: a transition image has to go back to a plain one.
            if (!IsTransitionImage(map.Tile(tile.X, tile.Y).TerrainImageIndex))
            {
                return false;
            }

            imageIndex = PlainImageIndex(ground, tile);
            return true;
        }

        int beach = ground is MapGround.Water or MapGround.Dirt
            ? 0
            : GetDirection(map, tile, MapGround.Water) | GetDirection(map, tile, MapGround.Beach);

        return TryGetBoundaries(map, tile, direction, beach, MapGrounds.StartImageIndex(ground), out imageIndex, out shape);
    }

    /// <summary>The plain image of a ground, one of the eight variants of its block.</summary>
    /// <param name="ground">Ground to draw.</param>
    /// <param name="tile">Tile being drawn, only used to pick the variant.</param>
    /// <returns>Terrain image index of the plain tile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ground"/> has no images.</exception>
    public static ushort PlainImageIndex(MapGround ground, Point tile)
    {
        return (ushort)(MapGrounds.StartImageIndex(ground) + MapGrounds.PlainImageOffset(ground) + Variant(tile, 8));
    }

    /// <summary>Whether a terrain image belongs to the transition part of its ground block.</summary>
    /// <param name="imageIndex">Terrain image index from a tile.</param>
    /// <returns>Whether the image draws a transition to another ground.</returns>
    private static bool IsTransitionImage(ushort imageIndex)
    {
        MapGround ground = MapGrounds.FromTerrainImageIndex(imageIndex);

        return ground switch
        {
            MapGround.Water or MapGround.Dirt => imageIndex < MapGrounds.StartImageIndex(ground) + 16,
            MapGround.Beach or MapGround.Unknown => false,
            _ => imageIndex < MapGrounds.StartImageIndex(ground) + 38,
        };
    }

    private static MapGround GroundAt(Mp2Map map, int x, int y)
        => MapGrounds.FromTerrainImageIndex(map.Tile(x, y).TerrainImageIndex);

    /// <summary>
    /// The direction bits under which the given ground surrounds (or covers) the tile. Neighbours
    /// outside the map are clamped onto the border, so a map edge is not a transition.
    /// </summary>
    private static int GetDirection(Mp2Map map, Point tile, MapGround ground)
    {
        int bits = GroundAt(map, tile.X, tile.Y) == ground ? Center : 0;

        foreach ((int direction, int dx, int dy) in Neighbours)
        {
            int x = Math.Clamp(tile.X + dx, 0, map.Width - 1);
            int y = Math.Clamp(tile.Y + dy, 0, map.Height - 1);

            if (GroundAt(map, x, y) == ground)
            {
                bits |= direction;
            }
        }

        return bits;
    }

    private static int Variant(Point tile, int count) => MapGrounds.VariantOffset(tile.X, tile.Y, count);

    private static bool Has(int value, int bits) => (value & bits) == bits;

    private static bool HasNone(int value, int bits) => (value & bits) == 0;

    private static bool Result(ushort index, bool horizontal, bool vertical, out ushort imageIndex, out byte shape)
    {
        imageIndex = index;
        shape = (byte)((vertical ? 1 : 0) | (horizontal ? 2 : 0));
        return true;
    }

    /// <summary>
    /// The transition table of the reference: given which sides are "other" ground and which are beach,
    /// picks the image offset and the flip.
    /// </summary>
    private static bool TryGetBoundaries(Mp2Map map, Point tile, int direction, int beach, ushort offset, out ushort imageIndex, out byte shape)
    {
        imageIndex = 0;
        shape = 0;

        // Water has no images for "open side and open corner": the corner bits have to be open too.
        bool notWater = GroundAt(map, tile.X, tile.Y) != MapGround.Water;

        // One corner missing: the tile is the current ground on five or six of the eight sides.
        if (direction == (TopRight | Top | BottomRow | CenterRow))
        {
            return Result((ushort)(offset + 12 + (HasNone(beach, TopLeft) ? 0 : 16) + Variant(tile, 3)), true, false, out imageIndex, out shape);
        }

        if (direction == (TopLeft | Top | BottomRow | CenterRow))
        {
            return Result((ushort)(offset + 12 + (HasNone(beach, TopRight) ? 0 : 16) + Variant(tile, 3)), false, false, out imageIndex, out shape);
        }

        if (direction == (BottomLeft | Bottom | TopRow | CenterRow))
        {
            return Result((ushort)(offset + 12 + (HasNone(beach, BottomRight) ? 0 : 16) + Variant(tile, 3)), false, true, out imageIndex, out shape);
        }

        if (direction == (BottomRight | Bottom | TopRow | CenterRow))
        {
            return Result((ushort)(offset + 12 + (HasNone(beach, BottomLeft) ? 0 : 16) + Variant(tile, 3)), true, true, out imageIndex, out shape);
        }

        // One side open.
        if (Has(direction, Left | Top | Bottom) && (notWater || Has(direction, TopLeft | BottomLeft)))
        {
            if (Has(beach, Right))
            {
                return Result((ushort)(offset + 8 + 16 + Variant(tile, 3)), false, false, out imageIndex, out shape);
            }

            if (HasNone(direction, Right))
            {
                if (Has(beach, TopRight))
                {
                    return Result((ushort)(offset + 35), false, false, out imageIndex, out shape);
                }

                if (Has(beach, BottomRight))
                {
                    return Result((ushort)(offset + 35), false, true, out imageIndex, out shape);
                }

                return Result((ushort)(offset + 8 + Variant(tile, 3)), false, false, out imageIndex, out shape);
            }
        }

        if (Has(direction, Right | Top | Bottom) && (notWater || Has(direction, TopRight | BottomRight)))
        {
            if (Has(beach, Left))
            {
                return Result((ushort)(offset + 8 + 16 + Variant(tile, 3)), true, false, out imageIndex, out shape);
            }

            if (HasNone(direction, Left))
            {
                if (Has(beach, TopLeft))
                {
                    return Result((ushort)(offset + 35), true, false, out imageIndex, out shape);
                }

                if (Has(beach, BottomLeft))
                {
                    return Result((ushort)(offset + 35), true, true, out imageIndex, out shape);
                }

                return Result((ushort)(offset + 8 + Variant(tile, 3)), true, false, out imageIndex, out shape);
            }
        }

        if (Has(direction, Bottom | Left | Right) && (notWater || Has(direction, BottomLeft | BottomRight)))
        {
            if (Has(beach, Top))
            {
                return Result((ushort)(offset + 16 + Variant(tile, 3)), false, false, out imageIndex, out shape);
            }

            if (HasNone(direction, Top))
            {
                if (Has(beach, TopRight))
                {
                    return Result((ushort)(offset + 34), false, false, out imageIndex, out shape);
                }

                if (Has(beach, TopLeft))
                {
                    return Result((ushort)(offset + 34), true, false, out imageIndex, out shape);
                }

                return Result((ushort)(offset + Variant(tile, 3)), false, false, out imageIndex, out shape);
            }
        }

        if (Has(direction, Top | Left | Right) && (notWater || Has(direction, TopLeft | TopRight)))
        {
            if (Has(beach, Bottom))
            {
                return Result((ushort)(offset + 16 + Variant(tile, 3)), false, true, out imageIndex, out shape);
            }

            if (HasNone(direction, Bottom))
            {
                if (Has(beach, BottomRight))
                {
                    return Result((ushort)(offset + 34), false, true, out imageIndex, out shape);
                }

                if (Has(beach, BottomLeft))
                {
                    return Result((ushort)(offset + 34), true, true, out imageIndex, out shape);
                }

                return Result((ushort)(offset + Variant(tile, 3)), false, true, out imageIndex, out shape);
            }
        }

        // A whole quarter open: ground to the right and bottom only.
        if (Has(direction, Right | BottomRight | Bottom))
        {
            if (Has(beach, Top | Left) || Has(beach, Top | BottomLeft) || Has(beach, TopRight | Left))
            {
                return Result((ushort)(offset + 4 + 16 + Variant(tile, 3)), true, false, out imageIndex, out shape);
            }

            if (HasNone(direction, Top | Left))
            {
                return Corner(beach, offset, tile, Top, Left, TopRight, BottomLeft, true, false, out imageIndex, out shape);
            }
        }

        // A whole quarter open: ground to the left and bottom only.
        if (Has(direction, Left | BottomLeft | Bottom))
        {
            if (Has(beach, Top | Right) || Has(beach, Top | BottomRight) || Has(beach, TopLeft | Right))
            {
                return Result((ushort)(offset + 4 + 16 + Variant(tile, 3)), false, false, out imageIndex, out shape);
            }

            if (HasNone(direction, Top | Right))
            {
                return Corner(beach, offset, tile, Top, Right, TopLeft, BottomRight, false, false, out imageIndex, out shape);
            }
        }

        // A whole quarter open: ground to the right and top only.
        if (Has(direction, Top | TopLeft | Left))
        {
            if (Has(beach, Right | Bottom) || Has(beach, Right | BottomLeft) || Has(beach, TopRight | Bottom))
            {
                return Result((ushort)(offset + 4 + 16 + Variant(tile, 3)), false, true, out imageIndex, out shape);
            }

            if (HasNone(direction, Right | Bottom))
            {
                return Corner(beach, offset, tile, Bottom, Right, BottomLeft, TopRight, false, true, out imageIndex, out shape);
            }
        }

        // A whole quarter open: ground to the left and top only.
        if (Has(direction, Top | TopRight | Right))
        {
            if (Has(beach, Left | Bottom) || Has(beach, Left | BottomRight) || Has(beach, TopLeft | Bottom))
            {
                return Result((ushort)(offset + 4 + 16 + Variant(tile, 3)), true, true, out imageIndex, out shape);
            }

            if (HasNone(direction, Left | Bottom))
            {
                return Corner(beach, offset, tile, Bottom, Left, BottomRight, TopLeft, true, true, out imageIndex, out shape);
            }
        }

        // Only the diagonals are another ground and there is no image for that: the reference paints a
        // plain tile, because the corners are barely noticeable. Water has no plain image for this.
        if (Has(direction, Top | Right | Bottom | Left) && notWater)
        {
            imageIndex = PlainImageIndex(GroundAt(map, tile.X, tile.Y), tile);
            return true;
        }

        return false;
    }

    /// <summary>
    /// A quarter-open tile whose two open sides are already transitions: one of the neighbouring beach
    /// corners decides between the four remaining images.
    /// </summary>
    /// <param name="beach">Beach direction bits around the tile.</param>
    /// <param name="offset">Terrain image index the ground block starts at.</param>
    /// <param name="tile">Tile being drawn, only used to pick the variant.</param>
    /// <param name="sideA">Open side that keeps the image at +36.</param>
    /// <param name="sideB">Open side that keeps the image at +37.</param>
    /// <param name="cornerA">Open corner that keeps the image at +33.</param>
    /// <param name="cornerB">Open corner that keeps the image at +32.</param>
    /// <param name="horizontal">Whether the picked image is flipped horizontally.</param>
    /// <param name="vertical">Whether the picked image is flipped vertically.</param>
    /// <param name="imageIndex">Terrain image index to write.</param>
    /// <param name="shape">Terrain shape to write.</param>
    /// <returns>Always <see langword="true"/>: every combination has an image here.</returns>
    private static bool Corner(int beach, ushort offset, Point tile, int sideA, int sideB, int cornerA, int cornerB, bool horizontal, bool vertical, out ushort imageIndex, out byte shape)
    {
        if (Has(beach, sideA))
        {
            return Result((ushort)(offset + 36), horizontal, vertical, out imageIndex, out shape);
        }

        if (Has(beach, sideB))
        {
            return Result((ushort)(offset + 37), horizontal, vertical, out imageIndex, out shape);
        }

        if (Has(beach, cornerA))
        {
            return Result((ushort)(offset + 33), horizontal, vertical, out imageIndex, out shape);
        }

        if (Has(beach, cornerB))
        {
            return Result((ushort)(offset + 32), horizontal, vertical, out imageIndex, out shape);
        }

        return Result((ushort)(offset + 4 + Variant(tile, 3)), horizontal, vertical, out imageIndex, out shape);
    }
}
