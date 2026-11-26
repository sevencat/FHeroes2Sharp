using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The entry point of the generator, <c>Maps::Random_Generator::generateMap()</c>
/// (<c>map_random_generator.cpp:621-981</c>).
/// </summary>
public sealed class RandomMapGeneratorTests
{
    [Fact]
    public void TheSameSeedGivesTheSameMap()
    {
        Mp2Map first = Generate(new RandomMapConfig { Seed = 77 });
        Mp2Map second = Generate(new RandomMapConfig { Seed = 77 });

        Assert.Equal(first.Width, second.Width);

        for (int y = 0; y < first.Height; ++y)
        {
            for (int x = 0; x < first.Width; ++x)
            {
                Assert.Equal(first.Tile(x, y).TerrainImageIndex, second.Tile(x, y).TerrainImageIndex);
            }
        }
    }

    [Fact]
    public void ADifferentSeedGivesADifferentMap()
    {
        Mp2Map first = Generate(new RandomMapConfig { Seed = 77 });
        Mp2Map second = Generate(new RandomMapConfig { Seed = 4711 });

        bool differs = false;

        for (int y = 0; y < first.Height && !differs; ++y)
        {
            for (int x = 0; x < first.Width && !differs; ++x)
            {
                differs = first.Tile(x, y).TerrainImageIndex != second.Tile(x, y).TerrainImageIndex;
            }
        }

        Assert.True(differs, "two different seeds produced the same picture");
    }

    [Fact]
    public void ASeedOfZeroIsDrawnByTheGenerator()
    {
        // 0 表示「现摇一个」；具体摇到几不重要，重要的是生成得出来。
        Mp2Map? map = RandomMapGenerator.Generate(new RandomMapConfig { Seed = 0 }, 72, 72);

        Assert.NotNull(map);
        Assert.Equal(72, map!.Width);
    }

    [Fact]
    public void AnUnsupportedSizeGivesNoMap()
    {
        Assert.Null(RandomMapGenerator.Generate(new RandomMapConfig { Seed = 5 }, 24, 24));
    }

    [Fact]
    public void TheTerrainIsReportedToTheCaller()
    {
        Mp2Map? map = RandomMapGenerator.Generate(
            new RandomMapConfig { Seed = 99 }, 72, 72, out RandomMapTerrain? terrain);

        Assert.NotNull(map);
        Assert.NotNull(terrain);
        Assert.Equal(72, terrain!.Width);
        Assert.Equal(
            terrain.Regions.Count,
            terrain.Regions.Select(region => region.Id).Distinct().Count());
    }

    private static Mp2Map Generate(RandomMapConfig config) =>
        RandomMapGenerator.Generate(config, 72, 72) ?? throw new InvalidOperationException("The map was not generated.");
}
