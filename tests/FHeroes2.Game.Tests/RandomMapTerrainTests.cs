using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The layout and the terrain of a generated map, steps 1 to 4 of <c>generateMap()</c>
/// (<c>map_random_generator.cpp:657-749</c>).
/// </summary>
public sealed class RandomMapTerrainTests
{
    private const int Width = 72;
    private const int Height = 72;

    [Fact]
    public void AnUnusableConfigurationGivesNoTerrain()
    {
        RandomMapConfig config = new() { PlayerCount = 1 };

        Assert.Null(RandomMapTerrainGenerator.Generate(config, Width, Height, new Pcg32(1)));
    }

    [Fact]
    public void TheRegionTableIsTheOneOfTheReference()
    {
        RandomMapTerrain terrain = Generate();

        // 72x72、两名玩家、正常密度：陆地块数 5184、区域 864，于是中立区 4 个，内圈 2 个、外圈 4 个，
        // 加上第一个水域区域一共 7 个。
        Assert.Equal(7, terrain.Regions.Count);
        Assert.Equal(0u, terrain.Regions[0].Id);
        Assert.Equal(MapGround.Water, terrain.Regions[0].GroundType);
        Assert.Equal(RandomMapRegionType.Neutral, terrain.Regions[0].Type);
        Assert.Single(terrain.Regions[0].Nodes);
    }

    [Fact]
    public void EveryPlayerGetsOneStartingRegion()
    {
        RandomMapConfig config = new() { PlayerCount = 3 };

        RandomMapTerrain terrain = RandomMapTerrainGenerator.Generate(config, Width, Height, new Pcg32(9))
            ?? throw new InvalidOperationException("The terrain was not generated.");

        List<RandomMapRegion> starting = terrain.Regions
            .Where(region => region.Type == RandomMapRegionType.Starting)
            .ToList();

        Assert.Equal(config.PlayerCount, starting.Count);
        Assert.Equal([0, 1, 2], starting.Select(region => region.ColorIndex).Order());

        // 正常密度下玩家区与中立区的宝藏上限分别是配置值与它的两倍。
        Assert.All(starting, region => Assert.Equal(15000, region.TreasureLimit));
        Assert.All(
            terrain.Regions.Where(region => region.Id != 0 && region.Type != RandomMapRegionType.Starting),
            region => Assert.Equal(30000, region.TreasureLimit));
    }

    [Fact]
    public void TheGroundOfARegionIsTheGroundOfItsTiles()
    {
        RandomMapTerrain terrain = Generate();

        foreach (RandomMapRegion region in terrain.Regions)
        {
            if (region.Id == 0)
            {
                continue;
            }

            foreach (RandomMapNode node in region.Nodes)
            {
                Assert.Equal(region.GroundType, terrain.GroundAt(node.Index));
            }
        }

        Assert.Equal(MapGround.Unknown, terrain.GroundAt(-1));
        Assert.Equal(MapGround.Unknown, terrain.GroundAt(Width * Height));
    }

    [Fact]
    public void TheCoordinatesOfTheTerrainFollowTheMapWidth()
    {
        RandomMapTerrain terrain = Generate();

        Assert.Equal(new Point(5, 2), terrain.GetPoint((2 * Width) + 5));
        Assert.Equal((2 * Width) + 5, terrain.IndexAt(new Point(5, 2)));
        Assert.Equal(-1, terrain.IndexAt(new Point(-1, 0)));
    }

    [Fact]
    public void PlayerRegionsUseTheStartingTerrainTableAndNeutralOnesTheNeutralTable()
    {
        RandomMapTerrain terrain = Generate();

        foreach (RandomMapRegion region in terrain.Regions)
        {
            if (region.Id == 0)
            {
                continue;
            }

            MapGround[] allowed = region.Type == RandomMapRegionType.Starting
                ? RandomMapTerrainGenerator.PlayerTerrains
                : RandomMapTerrainGenerator.NeutralTerrains;

            Assert.Contains(region.GroundType, allowed);
        }
    }

    [Fact]
    public void ScarceRegionsKeepTheStandardSizeForPlayers()
    {
        RandomMapConfig scarce = new() { ResourceDensity = RandomMapResourceDensity.Scarce };
        RandomMapTerrain terrain = RandomMapTerrainGenerator.Generate(scarce, Width, Height, new Pcg32(3))
            ?? throw new InvalidOperationException("The terrain was not generated.");

        foreach (RandomMapRegion region in terrain.Regions)
        {
            if (region.Id == 0)
            {
                continue;
            }

            int expected = region.Type == RandomMapRegionType.Starting
                ? terrain.RegionSizeLimit
                : terrain.RegionSizeLimit * 6 / 5;

            Assert.Equal(expected, region.SizeLimit);
        }
    }

    [Fact]
    public void TheNeighbourhoodIsLinkedBothWays()
    {
        RandomMapTerrain terrain = Generate();

        foreach (RandomMapRegion region in terrain.Regions)
        {
            if (region.Id == 0)
            {
                continue;
            }

            foreach (uint adjacent in region.Neighbours)
            {
                Assert.Contains(region.Id, terrain.Regions[(int)adjacent].Neighbours);
            }
        }
    }

    [Fact]
    public void TheSameSeedGivesTheSameMap()
    {
        RandomMapTerrain first = Generate(seed: 1234);
        RandomMapTerrain second = Generate(seed: 1234);

        Assert.Equal(first.Regions.Count, second.Regions.Count);

        for (int index = 0; index < first.Width * first.Height; ++index)
        {
            Assert.Equal(first.GroundAt(index), second.GroundAt(index));
        }

        Assert.Equal(
            first.Regions.Select(region => region.CenterIndex),
            second.Regions.Select(region => region.CenterIndex));
    }

    [Fact]
    public void ADifferentSeedMovesTheRegions()
    {
        RandomMapTerrain first = Generate(seed: 1234);
        RandomMapTerrain second = Generate(seed: 4321);

        Assert.NotEqual(
            first.Regions.Select(region => region.CenterIndex).ToList(),
            second.Regions.Select(region => region.CenterIndex).ToList());
    }

    private static RandomMapTerrain Generate(int seed = 1, RandomMapConfig? config = null) =>
        RandomMapTerrainGenerator.Generate(config ?? new RandomMapConfig(), Width, Height, new Pcg32((ulong)seed))
        ?? throw new InvalidOperationException("The terrain was not generated.");
}
