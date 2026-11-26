using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Editor;

namespace FHeroes2.Game.Maps;

/// <summary>
/// Turns the laid out regions of <see cref="RandomMapTerrain"/> into a real map, the half of step 4 that writes tiles
/// (<c>map_random_generator.cpp:732-749</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把地形写进 <c>Map_Format::MapFormat</c>；端口写进 <see cref="Mp2Map"/>，因为那是 MP2 文件的内存形态，
/// 也是编辑器与 <see cref="Mp2MapWriter"/> 共同认的东西——写完之后 <see cref="Mp2MapWriter.Serialize"/> 就能落成
/// 一张真地图。
/// </para>
/// <para>
/// 两遍：先按每格的地形铺「纯色」图（<see cref="TerrainTransitions.PlainImageIndex"/> 的八变体之一），再把每格与
/// 八邻域混一遍（<see cref="TerrainTransitions.TryGetBlend"/>）。第二遍可以顺序走，因为混出来的图永远落在本格的
/// 地形块里，不会改变邻格看到的地形，前后顺序不影响结果。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：MP2 只认四种边长（36 / 72 / 108 / 144），别的尺寸返回 <see langword="null"/> 并记一条警告；
/// 参考的编辑器格式没有这个限制。
/// </para>
/// </remarks>
public static class RandomMapBuilder
{
    /// <summary>The shape bits of a tile, the low two bits of <see cref="Mp2TileInfo.TerrainFlags"/>.</summary>
    private const byte ShapeMask = 0x03;

    /// <summary>Builds the map of a laid out terrain, <c>setTerrainOnTile()</c> plus the transition pass.</summary>
    /// <param name="terrain">The regions and the ground of every tile.</param>
    /// <returns>
    /// The map, or <see langword="null"/> when its size is not one the MP2 format knows.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="terrain"/> is <see langword="null"/>.</exception>
    public static Mp2Map? Build(RandomMapTerrain terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);

        if (!Mp2Map.IsSupportedSize(terrain.Width, terrain.Height))
        {
            GameLog.Warn(
                $"A generated map is 36, 72, 108 or 144 tiles wide and high, not {terrain.Width}x{terrain.Height}; "
                + "the file cannot hold it.");

            return null;
        }

        Mp2Map map = Mp2Map.CreateBlank(
            terrain.Width,
            (x, y) => TerrainTransitions.PlainImageIndex(terrain.GroundAt(new Point(x, y)), new Point(x, y)));

        BlendTransitions(map);

        return map;
    }

    /// <summary>Recomputes every tile's transition image, <c>setTerrainBoundaries()</c>.</summary>
    /// <param name="map">The map to blend in place.</param>
    private static void BlendTransitions(Mp2Map map)
    {
        for (int y = 0; y < map.Height; ++y)
        {
            for (int x = 0; x < map.Width; ++x)
            {
                if (!TerrainTransitions.TryGetBlend(map, new Point(x, y), out ushort imageIndex, out byte shape))
                {
                    // 没有对应的过渡图：留着上一遍铺的纯色图。
                    continue;
                }

                Mp2TileInfo tile = map.Tile(x, y);

                map.SetTile(x, y, tile with
                {
                    TerrainImageIndex = imageIndex,
                    TerrainFlags = (byte)((tile.TerrainFlags & ~ShapeMask) | shape),
                });
            }
        }
    }
}
