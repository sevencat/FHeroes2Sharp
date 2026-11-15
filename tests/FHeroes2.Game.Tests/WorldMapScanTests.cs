using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the scans and the guard queries of <c>maps.cpp:56-182</c> and <c>:570-689</c>.
/// </summary>
public sealed class WorldMapScanTests
{
    private const int MapSize = 7;

    /// <summary>Centre of the map, used by most cases.</summary>
    private static readonly int Center = Index(3, 3);

    [Fact]
    public void TheApproximateDistanceCountsADiagonalStepAsOneAndAHalf()
    {
        World world = CreateWorld(10);

        // The reference's own example: five columns right and seven rows down is nine tiles of travel.
        int from = Index(2, 2, 10);
        int to = Index(7, 9, 10);

        Assert.Equal(9u, world.Geometry!.Value.GetApproximateDistance(from, to));
        Assert.Equal(9u, world.Geometry!.Value.GetApproximateDistance(to, from));
    }

    [Fact]
    public void TheStraightLineDistanceTruncatesTheHypotenuse()
    {
        World world = CreateWorld(10);

        int from = Index(2, 2, 10);
        int to = Index(7, 9, 10);

        // sqrt( 5^2 + 7^2 ) = 8.6, truncated to 8.
        Assert.Equal(8u, world.Geometry!.Value.GetStraightLineDistance(from, to));
    }

    [Fact]
    public void TheSquaredDistanceIsTheComparisonOfTheReference()
    {
        MapGeometry geometry = new(MapSize, MapSize);

        Assert.Equal(0, geometry.GetSquaredDistance(Center, Center));
        Assert.Equal(1, geometry.GetSquaredDistance(Center, Index(3, 2)));
        Assert.Equal(2, geometry.GetSquaredDistance(Center, Index(4, 4)));
        Assert.Equal(25 + 49, new MapGeometry(10, 10).GetSquaredDistance(Index(2, 2, 10), Index(7, 9, 10)));
    }

    [Fact]
    public void ScanAroundObjectLooksAtOneRingOnly()
    {
        World world = CreateWorld();

        int neighbour = Index(2, 2);
        int farAway = Index(5, 5);

        world.GetTile(neighbour).SetMainObjectType(Mp2ObjectType.ActionWindmill);
        world.GetTile(farAway).SetMainObjectType(Mp2ObjectType.ActionWindmill);

        Assert.Equal([neighbour], world.ScanAroundObject(Center, Mp2ObjectType.ActionWindmill));
    }

    [Fact]
    public void HasNearbyObjectIsTrueForANeighbourButNotForTheTileItself()
    {
        World world = CreateWorld();

        world.GetTile(Center).SetMainObjectType(Mp2ObjectType.ActionSign);

        // The tile itself is not "nearby": the ring excludes the centre.
        Assert.False(world.HasNearbyObject(Center, Mp2ObjectType.ActionSign));

        world.GetTile(Index(2, 3)).SetMainObjectType(Mp2ObjectType.ActionSign);

        Assert.True(world.HasNearbyObject(Center, Mp2ObjectType.ActionSign));
    }

    [Fact]
    public void ScanAroundObjectWithDistanceOrdersTheHitsByDistance()
    {
        World world = CreateWorld();

        int close = Index(3, 1);
        int far = Index(6, 6);

        world.GetTile(close).SetMainObjectType(Mp2ObjectType.ActionWindmill);
        world.GetTile(far).SetMainObjectType(Mp2ObjectType.ActionWindmill);

        Assert.Equal([close, far], world.ScanAroundObjectWithDistance(Center, 3, Mp2ObjectType.ActionWindmill));
    }

    [Fact]
    public void GetObjectPositionsSortsTheWholeMapAroundACentre()
    {
        World world = CreateWorld();

        int far = Index(0, 0);
        int close = Index(4, 3);
        int middle = Index(1, 1);

        world.GetTile(far).SetMainObjectType(Mp2ObjectType.ActionWindmill);
        world.GetTile(close).SetMainObjectType(Mp2ObjectType.ActionWindmill);
        world.GetTile(middle).SetMainObjectType(Mp2ObjectType.ActionWindmill);

        List<int> positions = world.GetObjectPositions(Center, Mp2ObjectType.ActionWindmill, ignoreHeroes: true);

        Assert.Equal([close, middle, far], positions);
    }

    [Fact]
    public void AnObjectUsedFromTheFrontIsNoObstacleForDimensionDoor()
    {
        World world = CreateWorld();

        int plain = Index(1, 1);
        int windmill = Index(5, 5);
        int water = Index(6, 0);

        world.GetTile(windmill).SetMainObjectType(Mp2ObjectType.ActionWindmill);
        MakeWater(world.GetTile(water));

        Assert.True(world.IsValidForDimensionDoor(plain, isWater: false));

        // An in-game action object would have to be visited, which a teleport cannot do.
        Assert.False(world.IsValidForDimensionDoor(windmill, isWater: false));

        // ... and the side of the water has to match: a hero on foot cannot land on water.
        Assert.False(world.IsValidForDimensionDoor(water, isWater: false));
        Assert.True(world.IsValidForDimensionDoor(water, isWater: true));
    }

    [Fact]
    public void AnAdjacentMonsterGuardsTheTile()
    {
        World world = CreateWorld();

        int monster = Index(4, 4);

        PutMonster(world, monster);

        Assert.Equal([monster], world.GetMonstersProtectingTile(Center));
        Assert.True(world.IsTileUnderProtection(Center));
    }

    [Fact]
    public void AMonsterTwoTilesAwayDoesNotGuardTheTile()
    {
        World world = CreateWorld();

        PutMonster(world, Index(5, 5));

        Assert.Empty(world.GetMonstersProtectingTile(Center));
        Assert.False(world.IsTileUnderProtection(Center));
    }

    [Fact]
    public void AMonsterOnTheTileGuardsIt()
    {
        World world = CreateWorld();

        PutMonster(world, Center);

        // The monster is its own guard: the tile it stands on is the fight.
        Assert.Equal([Center], world.GetMonstersProtectingTile(Center));
        Assert.True(world.IsTileUnderProtection(Center));

        // The same answer without the "used from the front" shortcut: the monster is found as its own
        // neighbour, which is how the reference's scan reaches it.
        Assert.Equal([Center], world.GetMonstersProtectingTile(Center, checkObjectOnTile: false));
    }

    [Fact]
    public void AnObjectUsedFromTheFrontHasNoGuards()
    {
        World world = CreateWorld();

        int monster = Index(2, 2);

        PutMonster(world, monster);
        world.GetTile(Center).SetMainObjectType(Mp2ObjectType.ActionTreasureChest);

        // A chest is opened while standing next to it, so the monsters do not pounce.
        Assert.Empty(world.GetMonstersProtectingTile(Center));

        // The guard is still there for a caller that has already decided the object is harmless.
        Assert.Equal([monster], world.GetMonstersProtectingTile(Center, checkObjectOnTile: false));
    }

    [Fact]
    public void AMonsterOnTheOtherSideOfTheWaterDoesNotGuardTheTile()
    {
        World world = CreateWorld();

        int monster = Index(2, 2);

        PutMonster(world, monster);
        MakeWater(world.GetTile(monster));

        Assert.Empty(world.GetMonstersProtectingTile(Center));
    }

    [Fact]
    public void GuardsComeOutInTheOrderOfTheReference()
    {
        World world = CreateWorld();

        int topLeft = Index(2, 2);
        int right = Index(4, 3);
        int bottom = Index(3, 4);

        PutMonster(world, bottom);
        PutMonster(world, right);
        PutMonster(world, topLeft);

        // The row above, the left neighbour, the tile itself, the right neighbour, the row below.
        Assert.Equal([topLeft, right, bottom], world.GetMonstersProtectingTile(Center));
    }

    [Fact]
    public void AnIndexOutsideTheMapHasNoGuards()
    {
        World world = CreateWorld();

        int calls = 0;

        world.ForEachMonsterProtectingTile(-1, _ => ++calls);
        world.ForEachMonsterProtectingTile(MapSize * MapSize, _ => ++calls);

        Assert.Equal(0, calls);
        Assert.Empty(world.GetMonstersProtectingTile(-1));
        Assert.Empty(world.GetMonstersProtectingTile(MapSize * MapSize));
    }

    private static World CreateWorld(int size = MapSize)
    {
        var world = new World();
        world.GenerateUninitializedMap(size);

        // A fresh tile has a terrain image of zero, which counts as water: give the map grass so that the
        // water cases below are the only ones.
        for (int index = 0; index < size * size; ++index)
        {
            world.GetTile(index).SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        return world;
    }

    private static void PutMonster(World world, int index) =>
        world.GetTile(index).SetMainObjectType(Mp2ObjectType.ActionMonster);

    private static void MakeWater(Tile tile) => tile.SetTerrain(MapGrounds.WaterStartImageIndex, 0);

    private static int Index(int x, int y, int size = MapSize) => (y * size) + x;
}
