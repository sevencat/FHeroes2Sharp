using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The configuration and the two sizing formulas of the random map generator,
/// <c>Maps::Random_Generator</c> (<c>map_random_generator.cpp:57-109</c>, <c>:551-619</c>).
/// </summary>
public sealed class RandomMapConfigTests
{
    [Fact]
    public void TheDefaultsAreTheOnesOfTheReference()
    {
        RandomMapConfig config = new();

        Assert.Equal(2, config.PlayerCount);
        Assert.Equal(0, config.WaterPercentage);
        Assert.Equal(0, config.Seed);
        Assert.Equal(RandomMapLayout.Mirrored, config.Layout);
        Assert.Equal(RandomMapResourceDensity.Normal, config.ResourceDensity);
        Assert.Equal(RandomMapMonsterStrength.Normal, config.MonsterStrength);
        Assert.True(config.IsValid);
    }

    [Theory]
    [InlineData(RandomMapResourceDensity.Scarce, 1, 2, 0, 1, 1, 2, 8500)]
    [InlineData(RandomMapResourceDensity.Normal, 1, 4, 0, 2, 1, 3, 15000)]
    [InlineData(RandomMapResourceDensity.Abundant, 1, 4, 1, 2, 2, 5, 25000)]
    public void TheThreeDensityRowsAreTheTableOfTheReference(
        RandomMapResourceDensity density,
        byte castles,
        byte mines,
        byte goldMines,
        byte objects,
        byte powerUps,
        byte treasures,
        int treasureLimit)
    {
        RandomMapRegionalObjects row = RandomMapRegionalObjects.For(density);

        Assert.Equal(castles, row.CastleCount);
        Assert.Equal(mines, row.MineCount);
        Assert.Equal(goldMines, row.GoldMineCount);
        Assert.Equal(objects, row.ObjectCount);
        Assert.Equal(powerUps, row.PowerUpsCount);
        Assert.Equal(treasures, row.TreasureCount);
        Assert.Equal(treasureLimit, row.TreasureValueLimit);
    }

    [Fact]
    public void TheNamesAreTheMsgIdsOfTheReference()
    {
        Assert.Equal("map_layout|Mirrored", RandomMapSizing.LayoutName(RandomMapLayout.Mirrored));
        Assert.Equal("map_layout|Quest", RandomMapSizing.LayoutName(RandomMapLayout.Quest));
        Assert.Equal("resource_density|Scarce", RandomMapSizing.ResourceDensityName(RandomMapResourceDensity.Scarce));
        Assert.Equal("resource_density|Abundant", RandomMapSizing.ResourceDensityName(RandomMapResourceDensity.Abundant));
        Assert.Equal("monster_strength|Weak", RandomMapSizing.MonsterStrengthName(RandomMapMonsterStrength.Weak));
        Assert.Equal("monster_strength|Deadly", RandomMapSizing.MonsterStrengthName(RandomMapMonsterStrength.Deadly));
    }

    [Theory]
    [InlineData(2, 36, 53)]
    [InlineData(6, 144, 93)]
    [InlineData(2, 12, 0)]
    [InlineData(0, 36, 0)]
    [InlineData(2, 0, 0)]
    public void MaximumWaterKeepsOneStartingRegionPerPlayerPlusTheNeutralOne(
        int playerCount, int width, int expected)
    {
        Assert.Equal(expected, RandomMapSizing.MaximumWaterPercentage(playerCount, width));
    }

    [Fact]
    public void TheDefaultConfigGivesTheDefaultRegionSize()
    {
        // 两名玩家、正常密度、72x72、无水：一个区域要放 187 格东西，补足 40% 空地后 311，加 69 格边距即 380；
        // 陆地块数能放 13 个区域，上限是玩家数乘三即 6 个，于是 5184 / 6。
        Assert.Equal(864, RandomMapSizing.RegionSizeLimit(new RandomMapConfig(), 72, 72));
    }

    [Theory]
    [InlineData(RandomMapResourceDensity.Scarce, 324)]
    [InlineData(RandomMapResourceDensity.Normal, 432)]
    [InlineData(RandomMapResourceDensity.Abundant, 432)]
    public void ARicherDensityMakesBiggerRegionsAndSoFewerOfThem(
        RandomMapResourceDensity density, int expected)
    {
        RandomMapConfig config = new() { ResourceDensity = density };

        // 36x36 太小，「玩家数 + 1」这个地板把后两档压成同一个答案；三档的严格大小关系见下一个用例。
        Assert.Equal(expected, RandomMapSizing.RegionSizeLimit(config, 36, 36));
    }

    [Fact]
    public void TheThreeDensitiesAreOrderedOnAMapWhereTheFloorDoesNotBind()
    {
        int scarce = RandomMapSizing.RegionSizeLimit(new RandomMapConfig { ResourceDensity = RandomMapResourceDensity.Scarce }, 45, 45);
        int normal = RandomMapSizing.RegionSizeLimit(new RandomMapConfig { ResourceDensity = RandomMapResourceDensity.Normal }, 45, 45);
        int abundant = RandomMapSizing.RegionSizeLimit(new RandomMapConfig { ResourceDensity = RandomMapResourceDensity.Abundant }, 45, 45);

        Assert.True(scarce < normal, $"scarce {scarce} should be smaller than normal {normal}");
        Assert.True(normal < abundant, $"normal {normal} should be smaller than abundant {abundant}");

        // 更富的区域更大，所以同样的陆地块数被切得更少：337 / 405 / 506。
        Assert.Equal(337, scarce);
        Assert.Equal(405, normal);
        Assert.Equal(506, abundant);
    }

    [Fact]
    public void WaterTakesTilesAwayFromTheRegions()
    {
        RandomMapConfig config = new() { WaterPercentage = 50 };

        Assert.Equal(216, RandomMapSizing.RegionSizeLimit(config, 36, 36));
    }

    [Fact]
    public void SixPlayersOnAHugeMapGetARegionEach()
    {
        RandomMapConfig config = new() { PlayerCount = 6 };

        Assert.Equal(1152, RandomMapSizing.RegionSizeLimit(config, 144, 144));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(7, 0)]
    public void AnUnusablePlayerCountHasNoRegionSize(int playerCount, int expected)
    {
        RandomMapConfig config = new() { PlayerCount = playerCount };

        Assert.False(config.IsValid);
        Assert.Equal(expected, RandomMapSizing.RegionSizeLimit(config, 72, 72));
    }

    [Fact]
    public void AnUnusableWaterPercentageHasNoRegionSize()
    {
        RandomMapConfig config = new() { WaterPercentage = 101 };

        Assert.False(config.IsValid);
        Assert.Equal(0, RandomMapSizing.RegionSizeLimit(config, 72, 72));
    }
}
