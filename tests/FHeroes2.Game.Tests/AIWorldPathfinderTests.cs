using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the AI's side of the pathfinder, <c>AIWorldPathfinder</c>
/// (<c>world_pathfinding.cpp:543-1434</c>): the guard strength it compares against, the cost function without
/// a movement-point budget, the teleports it walks through, and the cut of <c>buildPath()</c>.
/// </summary>
public sealed class AIWorldPathfinderTests
{
    /// <summary>First plain grass image of the <c>GROUND32.TIL</c> block.</summary>
    private const int GrassImageIndex = 68;

    /// <summary>A water image; anything below 30 is water.</summary>
    private const int WaterImageIndex = 1;

    /// <summary>Sprite shared by both ends of a stone liths pair (<c>world.cpp:725</c>).</summary>
    private const byte LithsSprite = 116;

    /// <summary>An army that beats every guard these tests put on a map.</summary>
    private const double OverwhelmingArmy = 1_000_000;

    /// <summary>An army that loses to every guard these tests put on a map.</summary>
    private const double WeakArmy = 1;

    [Fact]
    public void FindsAStraightRouteThroughGrass()
    {
        World world = LoadMap(4, 4);

        AIWorldPathfinder pathfinder = Search(world, 0, OverwhelmingArmy);

        // The AI models an army without a movement point limit, so there is no "last move" discount
        // either: every step costs exactly its terrain penalty.
        Assert.Equal(300u, pathfinder.GetDistance(3));

        List<RouteStep> path = pathfinder.BuildPath(3, accountNearestObject: false);

        Assert.Equal(3, path.Count);
        Assert.Equal(0, path[0].From);
        Assert.Equal(3, path[^1].Index);
        Assert.All(path, step => Assert.Equal(100u, step.Penalty));
    }

    [Fact]
    public void ChargesADiagonalStepHalfAgainAsMuch()
    {
        World world = LoadMap(4, 4);

        AIWorldPathfinder pathfinder = Search(world, 0, OverwhelmingArmy);

        Assert.Equal(150u, pathfinder.GetDistance(5));

        RouteStep step = Assert.Single(pathfinder.BuildPath(5, accountNearestObject: false));

        Assert.Equal(Direction.BottomRight, step.Direction);
        Assert.Equal(150u, step.Penalty);
    }

    [Fact]
    public void AStrongGuardSealsTheTileNextToIt()
    {
        // Tile 2 holds a dragon. Tile 1 is walkable ground, but the guard would pounce on any army that
        // steps there, so the AI may not stand on it — and without tile 1 the rest of the corridor is out
        // of reach even though nobody stands on it.
        World world = LoadMap(1, 5);

        PutMonster(world, 2, MonsterType.BlackDragon, 1);

        AIWorldPathfinder pathfinder = Search(world, 0, WeakArmy);

        Assert.Equal(0u, pathfinder.GetDistance(1));
        Assert.Equal(0u, pathfinder.GetDistance(2));
        Assert.Empty(pathfinder.BuildPath(3, accountNearestObject: false));
    }

    [Fact]
    public void AMonsterThatCannotBeBeatenIsReachedButNotPassed()
    {
        // The start tile needs no entry check, so the AI may walk straight into the monster to fight it.
        // Walking on, however, needs the fight to be won first.
        World world = LoadMap(1, 3);

        PutMonster(world, 1, MonsterType.BlackDragon, 1);

        AIWorldPathfinder pathfinder = Search(world, 0, WeakArmy);

        Assert.Equal(100u, pathfinder.GetDistance(1));
        Assert.Equal(0u, pathfinder.GetDistance(2));
    }

    [Fact]
    public void AMonsterThatCanBeBeatenIsWalkedThrough()
    {
        World world = LoadMap(1, 3);

        PutMonster(world, 1, MonsterType.Peasant, 1);

        AIWorldPathfinder pathfinder = Search(world, 0, OverwhelmingArmy);

        Assert.Equal(100u, pathfinder.GetDistance(1));

        // Leaving the monster tile costs the step into it once more: the army fought there and what it
        // takes to get out is the reference's estimate, because a real route never leaves the tile.
        Assert.Equal(300u, pathfinder.GetDistance(2));
    }

    [Fact]
    public void StoneLithsLinkTheTwoHalvesOfTheMap()
    {
        // A corridor cut in two by water. The only way across is the pair of liths, which costs nothing
        // and is why the far half is reachable at all. The walk is horizontal: a liths sprite sits on the
        // upper half of its tile, so its passability only opens from the sides and the bottom. Every other
        // row is water, which keeps the two halves apart without a detour.
        World world = LoadMap(8, 8, index => index switch
        {
            1 => Liths(11),
            2 => Water(),
            4 => Liths(12),
            _ => index < 8 ? Grass() : Water()
        });

        AIWorldPathfinder pathfinder = Search(world, 0, OverwhelmingArmy);

        Assert.Equal(100u, pathfinder.GetDistance(1));

        // The step out of the liths is not a step the walk may take: the teleport is the only way in.
        Assert.Equal(0u, pathfinder.GetDistance(2));
        Assert.Equal(200u, pathfinder.GetDistance(5));

        List<RouteStep> path = pathfinder.BuildPath(5, accountNearestObject: false);

        Assert.Equal(3, path.Count);
        Assert.Equal(1, path[1].From);
        Assert.Equal(4, path[1].Index);
        Assert.Equal(0u, path[1].Penalty);
    }

    [Fact]
    public void TheStartTileNeverUsesATeleport()
    {
        // The reference only looks for teleports on tiles that were reached, not on the first one: a hero
        // standing on a liths still has to walk off it.
        World world = LoadMap(3, 8, index => index switch
        {
            0 => Liths(21),
            1 => Water(),
            2 => Liths(22),
            _ => Water()
        });

        AIWorldPathfinder pathfinder = Search(world, 0, OverwhelmingArmy);

        Assert.Equal(0u, pathfinder.GetDistance(1));
        Assert.Equal(0u, pathfinder.GetDistance(2));
    }

    [Fact]
    public void AccountNearestObjectCutsTheRouteAtTheObject()
    {
        // The AI takes a resource while walking over its tile, so the route may end on it. The plain
        // buildPath() returns that whole chain; accountNearestObject asks for the walk to stop where the
        // object is taken, which is the step before it.
        World world = LoadMap(4, 1);

        world.GetTile(2).SetMainObjectType(Mp2ObjectType.ActionResource);

        AIWorldPathfinder pathfinder = Search(world, 0, OverwhelmingArmy);

        // 200 to reach the resource and 200 more to tile 3: the step out of the resource is counted
        // twice, because the AI walks over it and the reference charges leaving an object that asks for
        // a front-facing interaction as another step.
        Assert.Equal(400u, pathfinder.GetDistance(3));

        List<RouteStep> full = pathfinder.BuildPath(3, accountNearestObject: false);

        Assert.Equal(3, full.Count);
        Assert.Equal(3, full[^1].Index);

        List<RouteStep> cut = pathfinder.BuildPath(3, accountNearestObject: true);

        Assert.Equal(2, cut.Count);
        Assert.Equal(2, cut[^1].Index);
        Assert.Equal(1, cut[^1].From);
    }

    [Fact]
    public void ReusesTheSearchWhenNothingChanged()
    {
        World world = LoadMap(4, 1);

        AIWorldPathfinder pathfinder = Search(world, 0, OverwhelmingArmy);

        Assert.Equal(300u, pathfinder.GetDistance(3));

        // The search is not re-run for the same question, so the new guard does not change the answer...
        PutMonster(world, 1, MonsterType.BlackDragon, 1);
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, OverwhelmingArmy, SkillLevel.None);

        Assert.Equal(300u, pathfinder.GetDistance(3));

        // ...but a different army strength is a different question.
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, WeakArmy, SkillLevel.None);

        Assert.Equal(100u, pathfinder.GetDistance(1));
        Assert.Equal(0u, pathfinder.GetDistance(3));
    }

    [Fact]
    public void TheWorldOwnsOneAiPathfinderAndHandsItToThePlanner()
    {
        World world = LoadMap(4, 4);

        AIWorldPathfinder pathfinder = world.AiPathfinder;

        Assert.Same(pathfinder, world.AiPathfinder);

        // reset() writes the tunables back to their defaults, which is the observable side of it: the
        // cached distances are not the thing to look at, because reset() keeps the node cache and the next
        // search overwrites it.
        pathfinder.SetMinimalArmyStrengthAdvantage(2.5);
        world.ResetPathfinder();

        Assert.Equal(1.0, pathfinder.MinimalArmyStrengthAdvantage);

        // The planner got this same object, so its own reset() reaches it too.
        pathfinder.SetMinimalArmyStrengthAdvantage(3.0);
        AiPlanner.Instance.ResetPathfinder();

        Assert.Equal(1.0, pathfinder.MinimalArmyStrengthAdvantage);
    }

    [Fact]
    public void ReEvaluateThrowsForATileOutsideTheMap()
    {
        World world = LoadMap(4, 1);

        AIWorldPathfinder pathfinder = new(world);

        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.ReEvaluateIfNeeded(4, PlayerColor.Blue, 0, SkillLevel.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.BuildPath(4, accountNearestObject: false));
    }

    [Fact]
    public void ResetRestoresTheTunablesToTheirDefaults()
    {
        AIWorldPathfinder pathfinder = new(LoadMap(4, 4));

        pathfinder.SetMinimalArmyStrengthAdvantage(2.5);
        pathfinder.SetSpellPointsReserveRatio(0.25);

        Assert.Equal(2.5, pathfinder.MinimalArmyStrengthAdvantage);
        Assert.Equal(0.25, pathfinder.SpellPointsReserveRatio);

        // reset() writes both defaults back, so a planner that wants a margin sets it again.
        pathfinder.Reset();

        Assert.Equal(1.0, pathfinder.MinimalArmyStrengthAdvantage);
        Assert.Equal(0.5, pathfinder.SpellPointsReserveRatio);
    }

    [Fact]
    public void TheStateRestorerPutsBothTunablesBack()
    {
        AIWorldPathfinder pathfinder = new(LoadMap(4, 4));

        // Whatever the planner left behind before the guarded decision.
        pathfinder.SetMinimalArmyStrengthAdvantage(1.75);
        pathfinder.SetSpellPointsReserveRatio(0.25);

        using (new AIWorldPathfinderStateRestorer(pathfinder))
        {
            pathfinder.SetMinimalArmyStrengthAdvantage(3.0);
            pathfinder.SetSpellPointsReserveRatio(0.75);

            Assert.Equal(3.0, pathfinder.MinimalArmyStrengthAdvantage);
        }

        Assert.Equal(1.75, pathfinder.MinimalArmyStrengthAdvantage);
        Assert.Equal(0.25, pathfinder.SpellPointsReserveRatio);

        // The second Dispose() of a guard leaves the tunables alone: the reference restores in a destructor, which
        // runs once, and a using block plus an explicit call would otherwise roll back the values set in between.
        AIWorldPathfinderStateRestorer restorer = new(pathfinder);
        restorer.Dispose();
        pathfinder.SetMinimalArmyStrengthAdvantage(0.5);
        restorer.Dispose();

        Assert.Equal(0.5, pathfinder.MinimalArmyStrengthAdvantage);
    }

    [Fact]
    public void TheTunablesRejectValuesOutsideTheirRange()
    {
        AIWorldPathfinder pathfinder = new(LoadMap(4, 4));

        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.SetMinimalArmyStrengthAdvantage(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.SetMinimalArmyStrengthAdvantage(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.SetSpellPointsReserveRatio(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pathfinder.SetSpellPointsReserveRatio(1.1));
    }

    /// <summary>
    /// Builds a world of the given size and reveals it for the blue player, so that the fog — which is
    /// impassable — does not decide the tests.
    /// </summary>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="tileFactory">Builds tile <c>i</c>; grass by default.</param>
    /// <returns>The loaded world.</returns>
    private static World LoadMap(int width, int height, Func<int, Mp2TileInfo>? tileFactory = null)
    {
        var world = new World();

        world.LoadMap(SyntheticMap.Create(width, height, tileFactory ?? (_ => Grass())));

        for (int index = 0; index < world.Size; ++index)
        {
            world.GetTile(index).ClearFog(PlayerColor.Blue);
        }

        return world;
    }

    /// <summary>Searches from one tile for the blue player, the way the planner asks for a distance.</summary>
    /// <param name="world">The world to search.</param>
    /// <param name="startIndex">Index of the tile the AI starts from.</param>
    /// <param name="armyStrength">Strength of the army that would walk the path.</param>
    /// <returns>The pathfinder, with the search done.</returns>
    private static AIWorldPathfinder Search(World world, int startIndex, double armyStrength)
    {
        AIWorldPathfinder pathfinder = new(world);
        pathfinder.ReEvaluateIfNeeded(startIndex, PlayerColor.Blue, armyStrength, SkillLevel.None);

        return pathfinder;
    }

    /// <summary>Puts a wandering stack of <paramref name="count"/> creatures on a tile.</summary>
    /// <param name="world">The world the tile belongs to.</param>
    /// <param name="index">Index of the tile.</param>
    /// <param name="monster">The creature of the stack.</param>
    /// <param name="count">Number of creatures.</param>
    private static void PutMonster(World world, int index, MonsterType monster, uint count) =>
        world.GetTile(index).SetMonster(world, monster, count);

    /// <summary>A plain grass tile.</summary>
    /// <returns>The tile data.</returns>
    private static Mp2TileInfo Grass() => SyntheticMap.BlankTile() with { TerrainImageIndex = GrassImageIndex };

    /// <summary>A water tile.</summary>
    /// <returns>The tile data.</returns>
    private static Mp2TileInfo Water() => SyntheticMap.BlankTile() with { TerrainImageIndex = WaterImageIndex };

    /// <summary>A stone liths on grass, <c>OBJ_STONE_LITHS</c>.</summary>
    /// <param name="uid">Object UID; both ends of a pair share the sprite, not the UID.</param>
    /// <returns>The tile data.</returns>
    private static Mp2TileInfo Liths(uint uid) =>
        SyntheticMap.BlankTile() with
        {
            TerrainImageIndex = GrassImageIndex,
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnMul2 << 2),
            BottomIcnImageIndex = LithsSprite,
            Quantity1 = (byte)ObjectLayerType.Object,
            ObjectType = Mp2ObjectType.ActionStoneLiths,
            Level1ObjectUid = uid
        };
}
