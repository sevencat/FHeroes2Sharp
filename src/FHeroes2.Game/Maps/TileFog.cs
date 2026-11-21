using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Recomputing the fog of war of tiles, <c>Maps::updateFogDirectionsInArea</c> of
/// <c>maps/maps_tiles_helper.cpp</c> (<c>2086-2212</c>).
/// </summary>
/// <remarks>
/// <para>
/// The fog direction of a tile is the answer to "which of my neighbours are still under the fog as well": only the
/// edges that touch a clear tile are drawn, which is what makes the border of the unexplored area look torn. Every
/// direction bit is only a function of the eight neighbours, so the reference caches the fog state of a one tile
/// wider rectangle once and then fills the directions of the inner rectangle in a single sweep without touching
/// <c>Tile.IsFogForAll</c> nine times per tile.
/// </para>
/// <para>
/// <b>Deviation: the height of the map is taken from <c>World.Height</c>.</b> The reference reads
/// <c>world.w()</c> for <c>worldHeight</c> as well, which is a typo that only survives because the fog helpers are
/// called on square maps. The port uses the real height: the loops clamp with it, and an under-sized clamp would walk
/// past the last row of a wide map.
/// </para>
/// </remarks>
public static class TileFog
{
    /// <summary>
    /// Recomputes the fog directions of a rectangle of tiles, <c>updateFogDirectionsInArea()</c>.
    /// </summary>
    /// <param name="minPos">Top-left tile of the rectangle, inclusive.</param>
    /// <param name="maxPos">Bottom-right tile of the rectangle, inclusive.</param>
    /// <param name="colors">Colors whose fog the current player shares.</param>
    public static void UpdateFogDirectionsInArea(Point minPos, Point maxPos, PlayerColor colors)
    {
        int worldWidth = World.Current.Width;
        int worldHeight = World.Current.Height;

        // Never leave the map.
        int minX = Math.Max(minPos.X, 0);
        int minY = Math.Max(minPos.Y, 0);

        // One past the requested corner, the loops below are exclusive.
        int maxX = Math.Min(maxPos.X + 1, worldWidth);
        int maxY = Math.Min(maxPos.Y + 1, worldHeight);

        // The fog cache is one tile wider on every side: the directions of a tile look at its neighbours.
        int fogMinX = Math.Max(minX - 1, 0);
        int fogMinY = Math.Max(minY - 1, 0);
        int fogMaxX = Math.Min(maxX + 1, worldWidth);
        int fogMaxY = Math.Min(maxY + 1, worldHeight);

        int fogDataWidth = maxX - minX + 2;
        int fogDataSize = fogDataWidth * (maxY - minY + 2);

        // 1 is "under the fog", 0 is "clear". Tiles outside the map stay 1, as in the reference: their fog is drawn.
        byte[] fogData = new byte[fogDataSize];

        Array.Fill(fogData, (byte)1);

        // Index of the top-left cache entry relative to the tile index arithmetic below.
        int fogDataOffset = 1 - minX + ((1 - minY) * fogDataWidth);

        for (int y = fogMinY; y < fogMaxY; ++y)
        {
            int fogTileOffsetY = y * worldWidth;
            int fogDataOffsetY = (y * fogDataWidth) + fogDataOffset;

            for (int x = fogMinX; x < fogMaxX; ++x)
            {
                fogData[x + fogDataOffsetY] = World.Current.Tiles[x + fogTileOffsetY].IsFogForAll(colors) ? (byte)1 : (byte)0;
            }
        }

        // Cache index offset from the tile towards its top-left neighbour.
        int topLeftDirectionOffset = -1 - fogDataWidth;

        for (int y = minY; y < maxY; ++y)
        {
            int fogCenterDataOffsetY = (y * fogDataWidth) + fogDataOffset;

            for (int x = minX; x < maxX; ++x)
            {
                Tile tile = World.Current.GetTile(x, y);

                int fogDataIndex = x + fogCenterDataOffsetY;

                if (fogData[fogDataIndex] == 0)
                {
                    // A clear tile has no fog to draw in any direction.
                    tile.SetFogDirection(Direction.Unknown);

                    continue;
                }

                // The tile itself is under the fog, so its centre bit is set; the eight neighbours follow the cache.
                Direction fogDirection = Direction.Center;

                fogDataIndex += topLeftDirectionOffset;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.TopLeft;
                }

                ++fogDataIndex;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.Top;
                }

                ++fogDataIndex;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.TopRight;
                }

                // Left neighbour of the next cache row.
                fogDataIndex += fogDataWidth - 2;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.Left;
                }

                // Skip the centre entry, it is already accounted for.
                fogDataIndex += 2;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.Right;
                }

                fogDataIndex += fogDataWidth - 2;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.BottomLeft;
                }

                ++fogDataIndex;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.Bottom;
                }

                ++fogDataIndex;

                if (fogData[fogDataIndex] == 1)
                {
                    fogDirection |= Direction.BottomRight;
                }

                tile.SetFogDirection(fogDirection);
            }
        }
    }
}
