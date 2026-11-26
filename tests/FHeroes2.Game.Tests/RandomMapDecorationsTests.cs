using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The decorative block of a ground and the object a mine uses, <c>treeTypeFromGroundType()</c>,
/// <c>mountainTypeFromGroundType()</c> and <c>getFakeMP2MineType()</c>
/// (<c>map_random_generator_helper.cpp:128-207</c>).
/// </summary>
public sealed class RandomMapDecorationsTests
{
    [Theory]
    [InlineData(MapGround.Grass, 0)]
    [InlineData(MapGround.Swamp, 0)]
    [InlineData(MapGround.Snow, 30)]
    [InlineData(MapGround.Lava, 6)]
    [InlineData(MapGround.Desert, 24)]
    [InlineData(MapGround.Beach, 24)]
    [InlineData(MapGround.Dirt, 18)]
    [InlineData(MapGround.Wasteland, 12)]
    [InlineData(MapGround.Water, 0)]
    [InlineData(MapGround.Unknown, 0)]
    public void TheTreeBlockFollowsTheGround(MapGround ground, int expected)
    {
        Assert.Equal(expected, RandomMapDecorations.TreeIndexFor(ground));
    }

    [Theory]
    [InlineData(MapGround.Grass, 6)]
    [InlineData(MapGround.Snow, 12)]
    [InlineData(MapGround.Swamp, 18)]
    [InlineData(MapGround.Lava, 24)]
    [InlineData(MapGround.Desert, 30)]
    [InlineData(MapGround.Dirt, 36)]
    [InlineData(MapGround.Wasteland, 44)]
    [InlineData(MapGround.Beach, 0)]
    [InlineData(MapGround.Water, 0)]
    public void TheMountainBlockFollowsTheGround(MapGround ground, int expected)
    {
        Assert.Equal(expected, RandomMapDecorations.MountainIndexFor(ground));
    }

    [Theory]
    [InlineData(Mp2ResourceType.Wood, Mp2ObjectType.Sawmill)]
    [InlineData(Mp2ResourceType.Ore, Mp2ObjectType.Sawmill)]
    [InlineData(Mp2ResourceType.Crystal, Mp2ObjectType.Mine)]
    [InlineData(Mp2ResourceType.Sulfur, Mp2ObjectType.Mine)]
    [InlineData(Mp2ResourceType.Gems, Mp2ObjectType.Mine)]
    [InlineData(Mp2ResourceType.Mercury, Mp2ObjectType.Mine)]
    [InlineData(Mp2ResourceType.Gold, Mp2ObjectType.AbandonedMine)]
    [InlineData(Mp2ResourceType.None, Mp2ObjectType.None)]
    public void AMineUsesTheObjectOfItsResource(Mp2ResourceType resource, Mp2ObjectType expected)
    {
        Assert.Equal(expected, RandomMapEconomy.MineObjectType(resource));
    }
}
