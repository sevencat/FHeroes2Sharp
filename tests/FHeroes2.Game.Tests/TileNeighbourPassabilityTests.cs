using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the part of the tile that needs its neighbours: <c>Tile::updatePassability()</c>
/// (<c>src/fheroes2/maps/maps_tiles.cpp:657</c>), <c>Tile::updateObjectType()</c> (<c>:1599</c>),
/// <c>Tile::isAnyTallObjectOnTile()</c> (<c>:1767</c>), <c>Tile::isDetachedObject()</c> (<c>:1854</c>),
/// <c>Tile::getIndexOfMainTile()</c> (<c>:1798</c>) and <c>World::updatePassabilities()</c>
/// (<c>world.cpp:1177</c>).
/// </summary>
public sealed class TileNeighbourPassabilityTests
{
    private const int MapSize = 5;

    /// <summary>
    /// A 5 × 5 all grass world. The tile indexes are assigned by hand because
    /// <see cref="World.GenerateUninitializedMap"/> leaves them at zero on purpose — the map loader is
    /// the one that sets them, and every neighbour lookup is computed from <see cref="Tile.Index"/>.
    /// </summary>
    /// <returns>The world, ready for <see cref="World.UpdatePassabilities"/>.</returns>
    private static World CreateLandWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

        for (int i = 0; i < world.Size; ++i)
        {
            Tile tile = world.Tiles[i];
            tile.Index = i;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        return world;
    }

    /// <summary>
    /// Puts one blocking object on a tile and promotes it to the main part, which is the only part
    /// <see cref="Tile.UpdatePassability"/> looks at. Sprite 255 of <c>OBJNCRCK</c> carries no object data,
    /// so the object type comes out as <see cref="Mp2ObjectType.None"/> and the checks about action
    /// objects, short objects and combined objects all fall through to the plain "it blocks" answer.
    /// </summary>
    /// <param name="tile">Tile to fill.</param>
    /// <param name="uid">Object UID.</param>
    private static void PutBlockingObject(Tile tile, uint uid)
    {
        tile.PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, uid, Mp2ObjectIcnType.ObjnCrck, 255));
        tile.SortObjectParts();
    }

    [Fact]
    public void AnEmptyLandWorldAllowsEveryStep()
    {
        World world = CreateLandWorld();

        world.UpdatePassabilities();

        Assert.All(world.Tiles, tile => Assert.Equal(Directions.All, tile.PassableDirections));
        Assert.All(world.Tiles, tile => Assert.Equal(Mp2ObjectType.None, tile.MainObjectType));
    }

    [Fact]
    public void AnObjectOnTheBottomRowIsImpassable()
    {
        World world = CreateLandWorld();
        PutBlockingObject(world.GetTile(22), 1);

        // Nothing can stand below the object, so the reference gives up and marks the tile inaccessible.
        world.UpdatePassabilities();

        Assert.Equal(Direction.Unknown, world.GetTile(22).PassableDirections);
    }

    [Fact]
    public void AnObjectOnALandTileAboveWaterIsImpassable()
    {
        World world = CreateLandWorld();

        // Tile 12 is land and tile 17 below it is water: nobody could disembark from tile 12.
        world.GetTile(17).SetTerrain(MapGrounds.WaterStartImageIndex, 0);
        PutBlockingObject(world.GetTile(12), 1);

        // Tile 17 is water with an object on it and water below: the rule is for land tiles only, so the
        // object keeps the mask its own sprite gives it.
        PutBlockingObject(world.GetTile(17), 2);

        world.UpdatePassabilities();

        Assert.Equal(Direction.Unknown, world.GetTile(12).PassableDirections);
        Assert.Equal(Directions.CenterRow | Directions.BottomRow, world.GetTile(17).PassableDirections);
    }

    [Fact]
    public void AnObjectContinuingOnTheTileBelowMakesThisTileImpassable()
    {
        World world = CreateLandWorld();
        PutBlockingObject(world.GetTile(12), 5);
        PutBlockingObject(world.GetTile(17), 5);

        // Same UID: the object spans both tiles, so the upper one is its continuation.
        world.UpdatePassabilities();

        Assert.Equal(Direction.Unknown, world.GetTile(12).PassableDirections);
    }

    [Fact]
    public void AnActionObjectIsNotNarrowedByItsNeighbours()
    {
        World world = CreateLandWorld();

        // Wood on the object layer: an action object that can be picked up from any side.
        world.GetTile(12).PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnRsrc, 1));
        world.GetTile(12).SortObjectParts();

        world.UpdatePassabilities();

        Assert.True(Mp2ObjectTypes.IsActionObject(world.GetTile(12).MainObjectType));
        Assert.Equal(Directions.All, world.GetTile(12).PassableDirections);
    }

    [Fact]
    public void ATallObjectIsReportedByTheTileAbove()
    {
        World world = CreateLandWorld();
        PutBlockingObject(world.GetTile(11), 3);

        // The top layer part of an overhanging object lives on the tile above the object's own tile.
        Assert.False(world.GetTile(11).IsAnyTallObjectOnTile(world));

        world.GetTile(6).PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, 3, Mp2ObjectIcnType.ObjnCrck, 20));

        Assert.True(world.GetTile(11).IsAnyTallObjectOnTile(world));

        // A different UID on the top layer is a different object.
        world.GetTile(6).TopObjectParts[0] = new ObjectPart(ObjectLayerType.Object, 4, Mp2ObjectIcnType.ObjnCrck, 20);
        Assert.False(world.GetTile(11).IsAnyTallObjectOnTile(world));
    }

    [Fact]
    public void ATallObjectOnTheLeftNeighbourForbidsTheUpwardLeftDiagonal()
    {
        World world = CreateLandWorld();

        // Tile 11 holds a tall object and is completely impassable: a blocking part and a reef part, whose
        // masks do not overlap. The reference only narrows the diagonal when the neighbour's own top
        // direction is blocked as well, and a plain object keeps its top bit.
        world.GetTile(11).PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 3, Mp2ObjectIcnType.ObjnCrck, 255));
        world.GetTile(11).PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 4, Mp2ObjectIcnType.XLoc2, 113));
        world.GetTile(6).PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, 3, Mp2ObjectIcnType.ObjnCrck, 20));

        Assert.True(world.GetTile(11).IsAnyTallObjectOnTile(world));
        Assert.Equal(Direction.Unknown, world.GetTile(11).GetTileIndependentPassability());

        world.UpdatePassabilities();

        // Tile 12 is empty, so only the diagonal to the upper left is taken away.
        Assert.Equal(Directions.All & ~Direction.TopLeft, world.GetTile(12).PassableDirections);
        Assert.True(world.GetTile(12).IsPassableTo(Direction.TopRight));
    }

    [Fact]
    public void ATallObjectKeepsTheDiagonalWhenItsOwnTopDirectionIsPassable()
    {
        World world = CreateLandWorld();

        // Same layout, but the neighbour's object is fully passable (a road), so its top bit is set and
        // the reference leaves the diagonal alone.
        world.GetTile(11).PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 4, Mp2ObjectIcnType.Road, 0));
        world.GetTile(11).SortObjectParts();
        world.GetTile(6).PushTopObjectPart(new ObjectPart(ObjectLayerType.Object, 4, Mp2ObjectIcnType.ObjnCrck, 20));

        Assert.True(world.GetTile(11).IsAnyTallObjectOnTile(world));

        world.UpdatePassabilities();

        Assert.Equal(Directions.All, world.GetTile(12).PassableDirections);
    }

    [Fact]
    public void ATileOnTheFirstRowHasNoTallObject()
    {
        World world = CreateLandWorld();
        PutBlockingObject(world.GetTile(2), 1);

        Assert.False(world.GetTile(2).IsAnyTallObjectOnTile(world));
    }

    [Fact]
    public void UpdateObjectTypeFindsTheCoastNextToWater()
    {
        World world = CreateLandWorld();
        world.GetTile(17).SetTerrain(MapGrounds.WaterStartImageIndex, 0);

        world.UpdatePassabilities();

        Assert.Equal(Mp2ObjectType.Coast, world.GetTile(12).MainObjectType);
        Assert.Equal(Mp2ObjectType.None, world.GetTile(1).MainObjectType);
        Assert.Equal(Mp2ObjectType.None, world.GetTile(17).MainObjectType);
    }

    [Fact]
    public void AWaterTileNeverBecomesACoast()
    {
        World world = CreateLandWorld();
        Tile tile = world.GetTile(12);
        tile.SetTerrain(MapGrounds.WaterStartImageIndex, 0);

        tile.UpdateObjectType(world);

        Assert.Equal(Mp2ObjectType.None, tile.MainObjectType);
    }

    [Fact]
    public void UpdateObjectTypeDropsAnEventWithoutData()
    {
        World world = CreateLandWorld();
        Tile tile = world.GetTile(12);

        // The reference resets the type when the map event data is gone (maps_tiles.cpp:1601-1611): a tile
        // that claims to be an event, but has no event in the table, stops being visitable.
        tile.SetMainObjectType(Mp2ObjectType.ActionEvent);

        tile.UpdateObjectType(world);

        Assert.Equal(Mp2ObjectType.None, tile.MainObjectType);
    }

    [Fact]
    public void IsDetachedObjectFollowsTheTypeList()
    {
        var world = new World();
        world.GenerateUninitializedMap(3);
        Tile tile = world.GetTile(4);

        // A castle is never attached to the tile below it.
        tile.SetMainObjectType(Mp2ObjectType.ActionCastle);
        Assert.True(tile.IsDetachedObject(world));

        // A castle without its action bit is only detached when its main tile is found nearby, which it is
        // not on an empty map.
        tile.SetMainObjectType(Mp2ObjectType.Castle);
        Assert.False(tile.IsDetachedObject(world));

        tile.SetMainObjectType(Mp2ObjectType.Mountains);
        Assert.False(tile.IsDetachedObject(world));
    }

    [Fact]
    public void IsDetachedObjectLooksThroughTheMainTile()
    {
        World world = CreateLandWorld();

        // Tile 12 is the main tile of the object (the action type lives there), tile 7 below it holds one
        // of its parts and only stores the plain castle type.
        world.GetTile(12).SetMainObjectType(Mp2ObjectType.ActionCastle);
        PutBlockingObject(world.GetTile(12), 9);

        world.GetTile(7).SetMainObjectType(Mp2ObjectType.Castle);
        world.GetTile(7).PushGroundObjectPart(new ObjectPart(ObjectLayerType.Object, 9, Mp2ObjectIcnType.ObjnCrck, 255));

        Assert.Equal(12, Tile.GetIndexOfMainTile(world.GetTile(7), world));
        Assert.True(world.GetTile(7).IsDetachedObject(world));
    }

    [Fact]
    public void GetIndexOfMainTileReturnsTheTileItselfWhenThereIsNoActionType()
    {
        World world = CreateLandWorld();
        Tile tile = world.GetTile(12);
        tile.SetMainObjectType(Mp2ObjectType.Mountains);

        Assert.Equal(12, Tile.GetIndexOfMainTile(tile, world));
    }
}
