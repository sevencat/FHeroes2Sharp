using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the player's side of the pathfinder, <c>world_pathfinding.cpp:443-541</c>: what a step
/// costs, which steps are allowed at all, and what a route looks like.
/// </summary>
public sealed class WorldPathfinderTests
{
    /// <summary>First plain grass image of the <c>GROUND32.TIL</c> block, +38 transition images.</summary>
    private const int GrassImageIndex = 68;

    /// <summary>First plain desert image, +38 transition images of the desert block.</summary>
    private const int DesertImageIndex = 300;

    /// <summary>A plain water image; anything below 30 is water.</summary>
    private const int WaterImageIndex = 1;

    [Fact]
    public void FindsAStraightRouteThroughGrass()
    {
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(300u, pathfinder.GetDistance(3));

        List<RouteStep> path = pathfinder.BuildPath(3);

        Assert.Equal(3, path.Count);
        Assert.Equal(0, path[0].From);
        Assert.Equal(3, path[^1].Index);
        Assert.All(path, step => Assert.Equal(100u, step.Penalty));
    }

    [Fact]
    public void ChargesADiagonalStepHalfAgainAsMuch()
    {
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(150u, pathfinder.GetDistance(5));

        RouteStep step = Assert.Single(pathfinder.BuildPath(5));

        Assert.Equal(Direction.BottomRight, step.Direction);
        Assert.Equal(150u, step.Penalty);
    }

    [Fact]
    public void ChargesTheTerrainOfTheDepartureTile()
    {
        // Desert costs more than grass, and the step pays for the tile it leaves, not the one it enters.
        World world = LoadMap(2, 1, index => index == 1 ? Desert() : Grass());

        Assert.Equal(100u, Search(world, 0).GetDistance(1));

        world = LoadMap(2, 1, index => index == 1 ? Desert() : Grass());

        Assert.Equal(200u, Search(world, 1).GetDistance(0));
    }

    [Theory]
    [InlineData(SkillLevel.None, 200u)]
    [InlineData(SkillLevel.Basic, 175u)]
    [InlineData(SkillLevel.Advanced, 150u)]
    [InlineData(SkillLevel.Expert, 100u)]
    public void ChargesDesertAccordingToThePathfindingSkill(SkillLevel pathfindingSkill, uint expectedCost)
    {
        World world = LoadMap(2, 1, _ => Desert());

        PlayerWorldPathfinder pathfinder = world.Pathfinder;
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, 2000, pathfindingSkill, 2000);

        Assert.Equal(expectedCost, pathfinder.GetDistance(1));
    }

    [Fact]
    public void ChargesARoadToRoadStepAsARoad()
    {
        World world = LoadMap(4, 4);

        for (int index = 0; index < world.Size; ++index)
        {
            MakeRoad(world, index);
        }

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        // The road replaces the ground penalty, so grass under the road no longer matters.
        Assert.Equal(75u, pathfinder.GetDistance(1));

        // And a diagonal road step is half again 75, truncated: 112, not 112.5.
        Assert.Equal(112u, pathfinder.GetDistance(5));
    }

    [Fact]
    public void DoesNotChargeARoadWhenOnlyOneEndIsARoad()
    {
        World world = LoadMap(2, 1);

        MakeRoad(world, 0);

        // world_pathfinding.cpp: the reference asks for `fromTile.isRoad() && toTile.isRoad()`.
        Assert.Equal(100u, Search(world, 0).GetDistance(1));
    }

    [Fact]
    public void RefusesToWalkUnderTheFog()
    {
        World world = LoadMap(4, 4, reveal: false);

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(0u, pathfinder.GetDistance(1));
        Assert.Empty(pathfinder.BuildPath(1));
    }

    [Fact]
    public void WaterStopsAHeroOnFoot()
    {
        // A one tile wide corridor: grass, grass, water, grass, grass. There is no way around the water.
        World world = LoadMap(1, 5, index => index == 2 ? Water() : Grass());

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(100u, pathfinder.GetDistance(1));
        Assert.Equal(0u, pathfinder.GetDistance(3));
        Assert.Equal(0u, pathfinder.GetDistance(4));
    }

    [Fact]
    public void ABoatSailsOverWater()
    {
        World world = LoadMap(4, 4, _ => Water());

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(300u, pathfinder.GetDistance(3));
        Assert.Equal(150u, pathfinder.GetDistance(5));
    }

    [Fact]
    public void ABoatCannotCutTheCornerOfLand()
    {
        // Tile 1 is land, tile 4 is water: the boat would squeeze past the corner of tile 1, so the
        // diagonal step onto tile 5 is refused and the boat has to sail around through tile 4.
        World world = LoadMap(4, 4, index => index == 1 ? Grass() : Water());

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(200u, pathfinder.GetDistance(5));
    }

    [Fact]
    public void LastMoveSpendsWhatIsLeftInsteadOfStoppingShort()
    {
        // 100 movement points left, a diagonal step costs 150: the hero may still take it and end the
        // turn with nothing left.
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = world.Pathfinder;
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, 100, SkillLevel.None, 200);

        Assert.Equal(100u, pathfinder.GetDistance(5));
    }

    [Fact]
    public void LastMoveNeedsEnoughPointsForTheTileTheHeroStandsOn()
    {
        // One point short of the tile he is standing on: not even the last move helps.
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = world.Pathfinder;
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, 99, SkillLevel.None, 200);

        Assert.Equal(150u, pathfinder.GetDistance(5));
    }

    [Fact]
    public void LastMoveDoesNotExistWithoutARealHero()
    {
        // The AI searches the map without a hero and reports zero movement points; it must not get the
        // discount of the last move, or its distances would be shorter than the ones a hero can walk.
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = world.Pathfinder;
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, 100, SkillLevel.None, 0);

        Assert.Equal(150u, pathfinder.GetDistance(5));
    }

    [Fact]
    public void TheStartTileHasNoPathToItself()
    {
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Empty(pathfinder.BuildPath(0));
    }

    [Fact]
    public void AnUnreachableTileHasNoPath()
    {
        World world = LoadMap(1, 5, index => index == 2 ? Water() : Grass());

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Empty(pathfinder.BuildPath(4));
    }

    [Fact]
    public void ThePathIsAContiguousChainFromTheStart()
    {
        World world = LoadMap(6, 6);

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        List<RouteStep> path = pathfinder.BuildPath(35);

        Assert.NotEmpty(path);
        Assert.Equal(0, path[0].From);
        Assert.Equal(35, path[^1].Index);

        uint totalCost = 0;
        int expectedFrom = 0;

        foreach (RouteStep step in path)
        {
            Assert.Equal(expectedFrom, step.From);
            Assert.Equal(world.Geometry!.Value.GetDirection(step.From, step.Index), step.Direction);

            totalCost += step.Penalty;
            expectedFrom = step.Index;
        }

        Assert.Equal(pathfinder.GetDistance(35), totalCost);
    }

    [Fact]
    public void TheLastRowDoesNotWrapAround()
    {
        // A wrong neighbour offset shows up as a path that walks to the first tile of the next row.
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = Search(world, 12);

        Assert.Equal(300u, pathfinder.GetDistance(15));
        Assert.Equal(3, pathfinder.BuildPath(15).Count);
    }

    [Fact]
    public void MonstersCanBeReachedButNotWalkedPast()
    {
        // A one tile wide corridor with a monster in the middle: the hero can be sent to the monster,
        // but not beyond it.
        World world = LoadMap(1, 5);

        world.GetTile(2).SetMainObjectType(Mp2ObjectType.ActionMonster);

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(200u, pathfinder.GetDistance(2));
        Assert.Equal(0u, pathfinder.GetDistance(3));
        Assert.Equal(0u, pathfinder.GetDistance(4));
    }

    [Fact]
    public void ReusesTheSearchWhenNothingChanged()
    {
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = Search(world, 0);

        Assert.Equal(300u, pathfinder.GetDistance(3));

        // The search is not re-run for the same question, so the monster does not change the answer...
        world.GetTile(1).SetMainObjectType(Mp2ObjectType.ActionMonster);
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, 2000, SkillLevel.None, 2000);

        Assert.Equal(300u, pathfinder.GetDistance(3));

        // ...but any change to what the search depends on re-runs it.
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, 2001, SkillLevel.None, 2000);

        // The monster may be walked up to, but a guarded tile is not expanded any further
        // (`processCurrentNode`, world_pathfinding.cpp), so the row behind it is out of reach.
        Assert.Equal(100u, pathfinder.GetDistance(1));
        Assert.Equal(0u, pathfinder.GetDistance(3));
    }

    [Fact]
    public void ReEvaluateThrowsForATileOutsideTheMap()
    {
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder pathfinder = world.Pathfinder;

        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.ReEvaluateIfNeeded(16, PlayerColor.Blue, 0, SkillLevel.None, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.BuildPath(16));
    }

    [Fact]
    public void TheWorldOwnsOnePathfinder()
    {
        World world = LoadMap(4, 4);

        Assert.Same(world.Pathfinder, world.Pathfinder);

        // Whatever a tile change does to the world, the pathfinder is reset, not rebuilt.
        world.ResetPathfinder();

        Assert.Same(world.Pathfinder, world.Pathfinder);
    }

    [Fact]
    public void AnEmptyWorldHasNoPathfinder()
    {
        World world = new();

        Assert.Throws<ArgumentException>(() => world.Pathfinder);
    }

    [Fact]
    public void ANewMapGetsANewPathfinder()
    {
        World world = LoadMap(4, 4);

        PlayerWorldPathfinder first = world.Pathfinder;

        world.LoadMap(SyntheticMap.Create(4, 4, _ => Grass()));

        Assert.NotSame(first, world.Pathfinder);
    }

    /// <summary>
    /// Builds a world of the given size and reveals it for the blue player, so that the fog — which is
    /// impassable — does not decide the tests.
    /// </summary>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="tileFactory">Builds tile <c>i</c>; grass by default.</param>
    /// <param name="reveal">Whether to clear the fog of the blue player on every tile.</param>
    /// <returns>The loaded world.</returns>
    private static World LoadMap(int width, int height, Func<int, Mp2TileInfo>? tileFactory = null, bool reveal = true)
    {
        World world = new();

        world.LoadMap(SyntheticMap.Create(width, height, tileFactory ?? (_ => Grass())));

        if (reveal)
        {
            for (int index = 0; index < world.Size; ++index)
            {
                world.GetTile(index).ClearFog(PlayerColor.Blue);
            }
        }

        return world;
    }

    /// <summary>Searches from one tile for the blue player with a generous number of movement points.</summary>
    /// <param name="world">The world to search.</param>
    /// <param name="startIndex">Index of the tile the hero stands on.</param>
    /// <returns>The pathfinder, with the search done.</returns>
    private static PlayerWorldPathfinder Search(World world, int startIndex)
    {
        PlayerWorldPathfinder pathfinder = world.Pathfinder;
        pathfinder.ReEvaluateIfNeeded(startIndex, PlayerColor.Blue, 2000, SkillLevel.None, 2000);

        return pathfinder;
    }

    /// <summary>A plain grass tile.</summary>
    /// <returns>The tile data.</returns>
    private static Mp2TileInfo Grass() => SyntheticMap.BlankTile() with { TerrainImageIndex = GrassImageIndex };

    /// <summary>A plain desert tile.</summary>
    /// <returns>The tile data.</returns>
    private static Mp2TileInfo Desert() => SyntheticMap.BlankTile() with { TerrainImageIndex = DesertImageIndex };

    /// <summary>A water tile.</summary>
    /// <returns>The tile data.</returns>
    private static Mp2TileInfo Water() => SyntheticMap.BlankTile() with { TerrainImageIndex = WaterImageIndex };

    /// <summary>
    /// Marks a tile as a road. The road bit is derived from the ground level parts, so this is exactly what
    /// the map loader does for a road object.
    /// </summary>
    /// <param name="world">The world to change.</param>
    /// <param name="index">Index of the tile.</param>
    private static void MakeRoad(World world, int index) =>
        world.GetTile(index).PushGroundObjectPart(
            new ObjectPart(ObjectLayerType.Object, world.GetNewObjectUid(), Mp2ObjectIcnType.Road, 0));
}
