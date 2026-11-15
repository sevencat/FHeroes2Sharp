using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the objects of the map that belong to a player (<c>world.h:117-166</c>,
/// <c>world.cpp:171-275</c>, <c>world.cpp:801</c>, <c>world.cpp:815</c>).
/// </summary>
public sealed class CapturedObjectTests
{
    [Fact]
    public void AFreshWorldTracksNoObjects()
    {
        var world = new World();
        world.GenerateUninitializedMap(4);

        Assert.False(world.HasCapturedObject(0));
        Assert.Equal(PlayerColor.None, world.ColorCapturedObject(0));
        Assert.Equal(0u, world.CountCapturedObject(Mp2ObjectType.Mine, PlayerColor.Blue));
    }

    [Fact]
    public void CapturingAnObjectRecordsItsTypeAndItsOwner()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);

        world.CaptureObject(5, PlayerColor.Blue);

        Assert.Equal(PlayerColor.Blue, world.ColorCapturedObject(5));
        Assert.Equal(Mp2ObjectType.Mine, world.GetCapturedObject(5).ObjectType);
        Assert.Equal(1u, world.CountCapturedObject(Mp2ObjectType.Mine, PlayerColor.Blue));
    }

    [Fact]
    public void CapturingAgainReplacesTheOwner()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);
        world.CaptureObject(5, PlayerColor.Blue);

        world.CaptureObject(5, PlayerColor.Red);

        Assert.Equal(PlayerColor.Red, world.ColorCapturedObject(5));
        Assert.Equal(0u, world.CountCapturedObject(Mp2ObjectType.Mine, PlayerColor.Blue));
        Assert.Equal(1u, world.CountCapturedObject(Mp2ObjectType.Mine, PlayerColor.Red));
    }

    [Fact]
    public void ReadingAnUntrackedTileDoesNotCreateAnEntry()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);

        Assert.Equal(PlayerColor.None, world.ColorCapturedObject(5));
        Assert.False(world.HasCapturedObject(5));
    }

    [Fact]
    public void AskingForAnEntryCreatesIt()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);

        CapturedObject capturedObject = world.GetCapturedObject(5);

        Assert.True(world.HasCapturedObject(5));
        Assert.Equal(Mp2ObjectType.None, capturedObject.ObjectType);
        Assert.Equal(PlayerColor.None, capturedObject.Color);
    }

    [Fact]
    public void ResettingAColorDropsEveryObjectOfThatColor()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);
        world.GetTile(6).SetMainObjectType(Mp2ObjectType.Sawmill);
        world.CaptureObject(5, PlayerColor.Blue);
        world.CaptureObject(6, PlayerColor.Blue);

        world.ResetCapturedObjects(PlayerColor.Blue);

        Assert.Equal(PlayerColor.None, world.ColorCapturedObject(5));
        Assert.Equal(PlayerColor.None, world.ColorCapturedObject(6));

        // The entries stay: the object type is what a re-capture and the counters need.
        Assert.True(world.HasCapturedObject(5));
        Assert.Equal(Mp2ObjectType.Mine, world.GetCapturedObject(5).ObjectType);
    }

    [Fact]
    public void MinesAreCountedByTheResourceTheyCarry()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);
        world.GetTile(6).SetMainObjectType(Mp2ObjectType.Mine);
        world.GetTile(6).SetResourceOnTile(Resource.Gems, 1);
        world.GetTile(7).SetMainObjectType(Mp2ObjectType.Mine);
        world.GetTile(7).SetResourceOnTile(Resource.Ore, 1);

        world.CaptureObject(5, PlayerColor.Blue);
        world.CaptureObject(6, PlayerColor.Blue);
        world.CaptureObject(7, PlayerColor.Green);

        Assert.Equal(1u, world.CountCapturedMines(Resource.Ore, PlayerColor.Blue));
        Assert.Equal(1u, world.CountCapturedMines(Resource.Gems, PlayerColor.Blue));
        Assert.Equal(0u, world.CountCapturedMines(Resource.Sulfur, PlayerColor.Blue));
        Assert.Equal(1u, world.CountCapturedMines(Resource.Ore, PlayerColor.Green));
        Assert.Equal(0u, world.CountCapturedMines(Resource.Gems, PlayerColor.Green));
    }

    [Fact]
    public void TheSawmillAndTheAlchemistLabCountAsWoodAndMercuryMines()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);
        world.GetTile(6).SetMainObjectType(Mp2ObjectType.Sawmill);
        world.GetTile(7).SetMainObjectType(Mp2ObjectType.AlchemistLab);

        world.CaptureObject(6, PlayerColor.Blue);
        world.CaptureObject(7, PlayerColor.Blue);

        // Neither is MP2::OBJ_MINE, so both are counted by object type rather than by metadata.
        Assert.Equal(1u, world.CountCapturedMines(Resource.Wood, PlayerColor.Blue));
        Assert.Equal(1u, world.CountCapturedMines(Resource.Mercury, PlayerColor.Blue));
        Assert.Equal(0u, world.CountCapturedMines(Resource.Ore, PlayerColor.Blue));
    }

    [Fact]
    public void ResetDropsEveryCapturedObject()
    {
        World world = WorldWithMine(5, Resource.Ore, 2);
        world.CaptureObject(5, PlayerColor.Blue);

        world.Reset();

        Assert.Equal(0, world.CapturedObjectCount);
        Assert.Equal(PlayerColor.None, world.ColorCapturedObject(5));
    }

    private static World WorldWithMine(int index, int resourceType, uint amount)
    {
        var world = new World();
        world.GenerateUninitializedMap(4);
        world.GetTile(index).SetMainObjectType(Mp2ObjectType.Mine);
        world.GetTile(index).SetResourceOnTile(resourceType, amount);

        return world;
    }
}
