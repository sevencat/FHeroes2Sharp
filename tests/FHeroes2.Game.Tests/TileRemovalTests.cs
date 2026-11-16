using FHeroes2.Assets.Maps;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the removal half of <c>maps_tiles_helper.cpp</c> (<c>:1858-2061</c>): restoring an abandoned
/// mine, removing the main object of a tile, removing an object by type and removing every piece of an
/// object by UID.
/// </summary>
public sealed class TileRemovalTests
{
    [Fact]
    public void RemovingTheMainObjectTakesThePartAway()
    {
        Tile tile = MakeTile(out World world);
        tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 55, Mp2ObjectIcnType.TreDeci, 5));
        tile.SetMainObjectType(Mp2ObjectType.Trees);

        Assert.True(tile.RemoveMainObject(world));

        Assert.Equal(Mp2ObjectIcnType.Unknown, tile.MainObjectPart.IcnType);
        Assert.Equal(Mp2ObjectType.None, tile.MainObjectType);
    }

    [Fact]
    public void AnObjectThatIsNotThereIsNotRemoved()
    {
        Tile tile = MakeTile(out World world);
        tile.SetMainObjectType(Mp2ObjectType.Trees);

        Assert.False(tile.RemoveMainObject(world));
    }

    [Fact]
    public void RemovingByTypeFindsTheObjectOnTheTopLevel()
    {
        Tile tile = MakeTile(out World world);
        tile.PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, 77, Mp2ObjectIcnType.TreDeci, 5));

        Assert.True(tile.RemoveObjectByType(world, Mp2ObjectType.Trees));

        // The main part has no UID of the object, so only the top level part goes.
        Assert.DoesNotContain(tile.TopObjectParts, part => part.Uid == 77);
        Assert.Equal(Mp2ObjectIcnType.ObjnHaun, tile.MainObjectPart.IcnType);
    }

    [Fact]
    public void RemovingByUidWalksTheNeighbouringTiles()
    {
        Tile left = MakeTile(out World world);
        Tile right = world.GetTile(6);

        // One object drawn on two tiles, which is how every large object is stored.
        left.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 700, Mp2ObjectIcnType.MtnGras, 82));
        left.SetMainObjectType(Mp2ObjectType.ActionMine);
        right.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 700, Mp2ObjectIcnType.MtnGras, 83));
        right.SetMainObjectType(Mp2ObjectType.ActionMine);

        Assert.True(Tile.RemoveObjectFromMapByUID(world, left.Index, 700));

        Assert.Equal(Mp2ObjectIcnType.Unknown, left.MainObjectPart.IcnType);
        Assert.Equal(Mp2ObjectIcnType.Unknown, right.MainObjectPart.IcnType);
    }

    [Fact]
    public void RemovingByUidStopsAtTheTilesWithoutTheObject()
    {
        Tile tile = MakeTile(out World world);
        Tile neighbour = world.GetTile(6);
        neighbour.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 701, Mp2ObjectIcnType.MtnGras, 83));

        Tile.RemoveObjectFromMapByUID(world, tile.Index, 700);

        // Another object of another UID on the neighbouring tile is untouched.
        Assert.Equal(701u, neighbour.MainObjectPart.Uid);
    }

    [Fact]
    public void RestoringAnAbandonedMineSwapsTheSpritesOfBothHalves()
    {
        Tile mine = MakeTile(out World world);
        mine.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 900, Mp2ObjectIcnType.ObjnGras, 6));
        mine.SetMainObjectType(Mp2ObjectType.ActionAbandonedMine);

        Tile right = world.GetTile(6);
        right.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 900, Mp2ObjectIcnType.ObjnDirt, 9));
        right.SetMainObjectType(Mp2ObjectType.AbandonedMine);

        mine.RestoreAbandonedMine(world, Resource.Gold);

        Assert.Equal(Mp2ObjectIcnType.MtnGras, mine.MainObjectPart.IcnType);
        Assert.Equal((byte)82, mine.MainObjectPart.IcnIndex);

        Assert.Equal(Mp2ObjectIcnType.MtnDirt, right.MainObjectPart.IcnType);
        Assert.Equal((byte)113, right.MainObjectPart.IcnIndex);

        // The right half was an abandoned mine of the same object, so it becomes a working mine.
        Assert.Equal(Mp2ObjectType.Mine, right.MainObjectType);
    }

    [Fact]
    public void RestoringAnAbandonedMineWritesTheResourceIntoTheMetadata()
    {
        Tile mine = MakeTile(out World world);
        mine.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 900, Mp2ObjectIcnType.ObjnGras, 6));
        mine.SetMainObjectType(Mp2ObjectType.ActionAbandonedMine);

        mine.RestoreAbandonedMine(world, Resource.Gems);

        Assert.Equal(Resource.Gems, mine.GetResourceOnTile());
        Assert.Equal(1u, mine.GetResourceCountOnTile());
    }

    [Fact]
    public void TheMinerOverlayFollowsTheResource()
    {
        Tile mine = MakeTile(out World world);
        mine.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 900, Mp2ObjectIcnType.ExtraOvr, 5));
        mine.SetMainObjectType(Mp2ObjectType.ActionAbandonedMine);

        mine.RestoreAbandonedMine(world, Resource.Crystal);

        Assert.Equal(Mp2ObjectIcnType.ExtraOvr, mine.MainObjectPart.IcnType);
        Assert.Equal((byte)2, mine.MainObjectPart.IcnIndex);
    }

    private static Tile MakeTile(out World world)
    {
        world = new World();
        world.GenerateUninitializedMap(4);

        Tile tile = world.GetTile(5);
        tile.Index = 5;
        tile.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 101, Mp2ObjectIcnType.ObjnHaun, 0));

        return tile;
    }
}
