using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The capturable object table of a map file, the mine loop of <c>World::LoadMapMP2()</c>
/// (<c>world_loadmap.cpp:382-430</c>).
/// </summary>
public sealed class WorldCaptureObjectsTests
{
    private const int MapSize = 36;

    [Theory]
    [InlineData(0x00, Mp2ObjectType.Sawmill)]
    [InlineData(0x01, Mp2ObjectType.AlchemistLab)]
    [InlineData(0x02, Mp2ObjectType.Mine)]
    [InlineData(0x06, Mp2ObjectType.Mine)]
    [InlineData(0x64, Mp2ObjectType.Lighthouse)]
    [InlineData(0x65, Mp2ObjectType.DragonCity)]
    [InlineData(0x67, Mp2ObjectType.AbandonedMine)]
    public void EveryTypeOfTheTableBecomesACapturedObject(int rawType, Mp2ObjectType expected)
    {
        World world = LoadWorld(new Mp2CaptureObject(7, 9, rawType, expected));
        int index = (9 * MapSize) + 7;

        Assert.True(world.HasCapturedObject(index));
        Assert.Equal(PlayerColor.None, world.ColorCapturedObject(index));
        Assert.Equal(expected, world.GetCapturedObject(index).ObjectType);
    }

    [Fact]
    public void TheWholeTableIsRegistered()
    {
        World world = LoadWorld(
            new Mp2CaptureObject(1, 1, 0x00, Mp2ObjectType.Sawmill),
            new Mp2CaptureObject(2, 2, 0x01, Mp2ObjectType.AlchemistLab),
            new Mp2CaptureObject(3, 3, 0x02, Mp2ObjectType.Mine));

        Assert.Equal(3, world.CapturedObjectCount);
        Assert.Equal(1u, world.CountCapturedObject(Mp2ObjectType.Mine, PlayerColor.None));
    }

    [Fact]
    public void AnUnknownTypeIsSkippedAndTheRestIsKept()
    {
        World world = LoadWorld(
            new Mp2CaptureObject(4, 4, 0x42, Mp2ObjectType.None),
            new Mp2CaptureObject(5, 5, 0x64, Mp2ObjectType.Lighthouse));

        Assert.False(world.HasCapturedObject((4 * MapSize) + 4));
        Assert.True(world.HasCapturedObject((5 * MapSize) + 5));
        Assert.Equal(1, world.CapturedObjectCount);
    }

    [Fact]
    public void APositionOutsideTheMapIsSkipped()
    {
        World world = LoadWorld(new Mp2CaptureObject(90, 3, 0x00, Mp2ObjectType.Sawmill));

        Assert.Equal(0, world.CapturedObjectCount);
    }

    private static World LoadWorld(params Mp2CaptureObject[] captureObjects)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.StartImageIndex(MapGround.Grass)) with
        {
            CaptureObjects = [.. captureObjects],
        };

        World world = new();
        world.LoadMap(map);

        return world;
    }
}
