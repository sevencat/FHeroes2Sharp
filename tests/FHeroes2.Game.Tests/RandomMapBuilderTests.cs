using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Writing the laid out terrain into a real map, step 4 of <c>generateMap()</c>
/// (<c>map_random_generator.cpp:732-749</c>) on the port's <see cref="Mp2Map"/>.
/// </summary>
public sealed class RandomMapBuilderTests : IDisposable
{
    private readonly string _filePath = Path.Combine(
        Path.GetTempPath(), $"fh2-random-map-{Guid.NewGuid():N}.mp2");

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    [Fact]
    public void ASizeTheFormatDoesNotKnowHasNoMap()
    {
        RandomMapTerrain terrain = Terrain(24);

        Assert.Null(RandomMapBuilder.Build(terrain));
    }

    [Fact]
    public void TheMapKeepsTheGroundOfEveryTile()
    {
        RandomMapTerrain terrain = Terrain(72);
        Mp2Map map = Build(terrain);

        Assert.Equal(72, map.Width);
        Assert.Equal(72, map.Height);
        Assert.Equal(72 * 72, map.TileCount);

        for (int y = 0; y < map.Height; ++y)
        {
            for (int x = 0; x < map.Width; ++x)
            {
                MapGround painted = MapGrounds.FromTerrainImageIndex(map.Tile(x, y).TerrainImageIndex);

                Assert.Equal(terrain.GroundAt(new Point(x, y)), painted);
            }
        }
    }

    [Fact]
    public void EveryTileCarriesAnImage()
    {
        Mp2Map map = Build(Terrain(36));

        for (int y = 0; y < map.Height; ++y)
        {
            for (int x = 0; x < map.Width; ++x)
            {
                Assert.NotEqual(Mp2TileInfo.EmptyImageIndex, map.Tile(x, y).TerrainImageIndex);
                Assert.True(map.Tile(x, y).TerrainImageIndex < MapGrounds.MaxImageIndex);
            }
        }
    }

    [Fact]
    public void TheSameTerrainBuildsTheSameMap()
    {
        RandomMapTerrain terrain = Terrain(36);

        Mp2Map first = Build(terrain);
        Mp2Map second = Build(terrain);

        for (int y = 0; y < first.Height; ++y)
        {
            for (int x = 0; x < first.Width; ++x)
            {
                Assert.Equal(first.Tile(x, y).TerrainImageIndex, second.Tile(x, y).TerrainImageIndex);
                Assert.Equal(first.Tile(x, y).TerrainFlags & 0x03, second.Tile(x, y).TerrainFlags & 0x03);
            }
        }
    }

    [Fact]
    public void TheMapSurvivesAFileRoundTrip()
    {
        RandomMapTerrain terrain = Terrain(36);
        Mp2Map map = Build(terrain);

        // 从零生成的图没有源文件可以补丁，只能用 WriteAll（Save 会去读源文件）。
        Mp2MapWriter.WriteAll(_filePath, map);

        Mp2Map loaded = Mp2Map.Read(_filePath);

        Assert.Equal(map.Width, loaded.Width);
        Assert.Equal(map.Height, loaded.Height);
        Assert.Equal(map.TileCount, loaded.TileCount);

        for (int y = 0; y < loaded.Height; ++y)
        {
            for (int x = 0; x < loaded.Width; ++x)
            {
                MapGround expected = terrain.GroundAt(new Point(x, y));
                MapGround actual = MapGrounds.FromTerrainImageIndex(loaded.Tile(x, y).TerrainImageIndex);

                Assert.Equal(expected, actual);
            }
        }
    }

    private static Mp2Map Build(RandomMapTerrain terrain) =>
        RandomMapBuilder.Build(terrain) ?? throw new InvalidOperationException("The map was not built.");

    /// <summary>Lays out a terrain of the given size with a fixed seed.</summary>
    private static RandomMapTerrain Terrain(int size) =>
        RandomMapTerrainGenerator.Generate(new RandomMapConfig { Seed = 20261126 }, size, size, new Pcg32(7))
        ?? throw new InvalidOperationException("The terrain was not generated.");
}
