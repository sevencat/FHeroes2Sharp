using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The entry point of the random map generator, <c>Maps::Random_Generator::generateMap()</c>
/// (<c>map_random_generator.cpp:621-981</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的十一小步在这里收成五段：算配置与摆区域（<see cref="RandomMapTerrainGenerator"/>）、铺地形
/// （<see cref="RandomMapBuilder"/>）、放物件（<see cref="RandomMapObjectPasses"/>）——桩桩都是一层，调用方只认这一个入口。
/// </para>
/// <para>
/// <b>种子的规矩照参考</b>：<see cref="RandomMapConfig.Seed"/> 大于零就用它，否则现摇一个 0…999999 的——
/// 配置文件里留 0 表示「每次都要新图」。
/// </para>
/// </remarks>
public static class RandomMapGenerator
{
    /// <summary>Generates a map of <paramref name="width"/> by <paramref name="height"/> tiles.</summary>
    /// <param name="config">The configuration the map is generated from.</param>
    /// <param name="width">Edge length of the map.</param>
    /// <param name="height">Edge length of the map.</param>
    /// <param name="terrain">
    /// The laid out terrain, for the caller's logging, or <see langword="null"/> when nothing was generated.
    /// </param>
    /// <returns>The map, or <see langword="null"/> when the configuration or the size cannot be used.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/>.</exception>
    public static Mp2Map? Generate(
        RandomMapConfig config, int width, int height, out RandomMapTerrain? terrain) =>
        Generate(config, width, height, out terrain, out _);

    /// <summary>
    /// Generates a map and reports the seed it actually used, which is what names the saved file.
    /// </summary>
    /// <param name="config">The configuration the map is generated from.</param>
    /// <param name="width">Edge length of the map.</param>
    /// <param name="height">Edge length of the map.</param>
    /// <param name="terrain">
    /// The laid out terrain, for the caller's logging, or <see langword="null"/> when nothing was generated.
    /// </param>
    /// <param name="seed">
    /// The seed that was used: the configured one, or the one drawn in its place when the configuration left it at
    /// <c>0</c>.
    /// </param>
    /// <returns>The map, or <see langword="null"/> when the configuration or the size cannot be used.</returns>
    /// <remarks>
    /// 种子要单独回报，因为地图名字段只有 16 字节（ADR-283）：<c>Random map 20261126</c> 存下来只剩 <c>Random map 2026</c>，
    /// 从名字里读回去会读错。存档用它命名文件。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/>.</exception>
    public static Mp2Map? Generate(
        RandomMapConfig config, int width, int height, out RandomMapTerrain? terrain, out uint seed)
    {
        ArgumentNullException.ThrowIfNull(config);

        terrain = null;

        seed = config.Seed > 0 ? (uint)config.Seed : (uint)Rand.Get(999999);

        GameLog.Info($"Generating a random map with seed {seed}, {width}x{height} tiles.");

        Pcg32 randomGenerator = new(seed);

        terrain = RandomMapTerrainGenerator.Generate(config, width, height, randomGenerator);

        if (terrain is null)
        {
            GameLog.Warn("The random map generator could not fill the map with regions.");

            return null;
        }

        Mp2Map? map = RandomMapBuilder.Build(terrain);

        if (map is null)
        {
            return null;
        }

        // Steps 5 to 11: castles, mines, connectors, trees, treasures, decorations, guards and pickups, in that order.
        if (!RandomMapObjectPasses.Run(map, terrain, config, randomGenerator))
        {
            GameLog.Warn("The random map generator could not place every starting castle.");

            return null;
        }

        // The header last: it is read off the objects that were just placed.
        map = RandomMapHeader.Apply(map, config, seed);

        GameLog.Info(
            $"The random map generator laid out {terrain.Regions.Count} region(s) on a {map.Width}x{map.Height} map.");

        return map;
    }

    /// <summary>Generates a map without asking for the terrain, for the callers that only need the result.</summary>
    /// <param name="config">The configuration the map is generated from.</param>
    /// <param name="width">Edge length of the map.</param>
    /// <param name="height">Edge length of the map.</param>
    /// <returns>The map, or <see langword="null"/> when the configuration or the size cannot be used.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/>.</exception>
    public static Mp2Map? Generate(RandomMapConfig config, int width, int height) =>
        Generate(config, width, height, out _);
}
