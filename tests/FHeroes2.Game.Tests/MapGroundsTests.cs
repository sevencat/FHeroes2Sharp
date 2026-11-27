using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the ground table of <see cref="MapGrounds"/>: the terrain image index of a tile names the
/// ground it belongs to, and the eight ranges of <c>Maps::Ground::getGroundByImageIndex</c> have to
/// line up with them.
/// </summary>
public sealed class MapGroundsTests
{
    [Theory]
    [InlineData(0, MapGround.Water)]
    [InlineData(29, MapGround.Water)]
    [InlineData(30, MapGround.Grass)]
    [InlineData(91, MapGround.Grass)]
    [InlineData(92, MapGround.Snow)]
    [InlineData(145, MapGround.Snow)]
    [InlineData(146, MapGround.Swamp)]
    [InlineData(207, MapGround.Swamp)]
    [InlineData(208, MapGround.Lava)]
    [InlineData(261, MapGround.Lava)]
    [InlineData(262, MapGround.Desert)]
    [InlineData(320, MapGround.Desert)]
    [InlineData(321, MapGround.Dirt)]
    [InlineData(360, MapGround.Dirt)]
    [InlineData(361, MapGround.Wasteland)]
    [InlineData(414, MapGround.Wasteland)]
    [InlineData(415, MapGround.Beach)]
    [InlineData(431, MapGround.Beach)]
    [InlineData(432, MapGround.Unknown)]
    public void TerrainImageIndexes_FallIntoTheirGroundRange(int imageIndex, MapGround expected) =>
        Assert.Equal(expected, MapGrounds.FromTerrainImageIndex((ushort)imageIndex));
}
