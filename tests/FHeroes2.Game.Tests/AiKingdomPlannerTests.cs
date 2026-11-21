using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameHero = FHeroes2.Game.Heroes.Hero;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Behaviour tests for the kingdom half of the AI planner, <c>ai_planner_kingdom.cpp</c>: the roles the heroes get,
/// the scan of the map with the region safety it leaves behind, the ranking of the castles and the limit hiring a new
/// hero is held to.
/// </summary>
/// <remarks>
/// <para>
/// The planner is a singleton, so a test that scans the map leaves caches behind for the next one; they are emptied by
/// scanning a kingdom that owns nothing before the world is put back.
/// </para>
/// <para>
/// A generated map has no castles and no map file, so the parts of <c>evaluateRegionSafety()</c> that only fire on a
/// castle — friendly territory worth 100, enemy territory worth -100 — are not reached here: the scan of a synthetic
/// world can only put an enemy army into a region. They are reached on a real map, which is what
/// <c>Planner::KingdomTurn()</c> runs on.
/// </para>
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AiKingdomPlannerTests : IDisposable
{
    private readonly World _previousWorld = World.Current;
    private World? _world;

    public void Dispose()
    {
        if (_world is not null)
        {
            // The caches belong to the singleton, not to the world: a scan of a kingdom that owns nothing is the
            // empty state, and it also stops the region stats of this test from reaching the next one.
            AiPlanner.Instance.ScanMap(_world.GetKingdom(PlayerColor.Red));
        }

        World.Current = _previousWorld;
        Settings.Reset();
    }

    [Fact]
    public void EasyGivesEveryHeroTheHunterRole()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Easy);

        List<GameHero> heroes = NewHeroes(world, 3);

        AiPlanner.SetHeroRoles(heroes, GameDifficulty.Easy);

        // The easiest difficulty keeps the roles of the old AI: everyone collects, nobody concentrates.
        Assert.All(heroes, hero => Assert.Equal(HeroRole.Hunter, hero.AiRole));
    }

    [Fact]
    public void TheStrongestOfTwoHeroesBecomesTheFighter()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        List<GameHero> heroes = NewHeroes(world, 2);
        heroes[0].Army.JoinTroop(MonsterType.Peasant, 100);
        heroes[1].Army.JoinTroop(MonsterType.Peasant, 1);

        AiPlanner.SetHeroRoles(heroes, GameDifficulty.Normal);

        // Two heroes are not enough for a commander and a courier, so they are ranked by strength alone: three times
        // the weaker one is the line between a fighter and a hunter.
        Assert.Equal(HeroRole.Fighter, heroes[0].AiRole);
        Assert.Equal(HeroRole.Hunter, heroes[1].AiRole);
    }

    [Fact]
    public void APatrollingHeroIsAFighterAndNoYardstick()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        List<GameHero> heroes = NewHeroes(world, 2);
        heroes[0].SetModes(HeroModes.Patrol);
        heroes[0].Army.JoinTroop(MonsterType.Peasant, 100);
        heroes[1].Army.JoinTroop(MonsterType.Peasant, 10);

        AiPlanner.SetHeroRoles(heroes, GameDifficulty.Normal);

        // A hero on patrol has nowhere to go but into a fight.
        Assert.Equal(HeroRole.Fighter, heroes[0].AiRole);

        // He is not counted towards the median either: the other hero is alone in the ranking, and nobody is three
        // times stronger than himself.
        Assert.Equal(HeroRole.Hunter, heroes[1].AiRole);
    }

    [Fact]
    public void MoreThanThreeHeroesGiveUpTheWeakestTwoToTheCourierAndTheScout()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        List<GameHero> heroes = NewHeroes(world, 5);

        AiPlanner.SetHeroRoles(heroes, GameDifficulty.Normal);

        // Neither role fights, so both leave the ranking before it is measured: the weakest hero by skill carries
        // troops, the next one scouts, and only the remaining two are weighed by their armies.
        Assert.Single(heroes, hero => hero.AiRole == HeroRole.Courier);
        Assert.Single(heroes, hero => hero.AiRole == HeroRole.Scout);
        Assert.Equal(2, heroes.Count(hero => hero.AiRole is HeroRole.Fighter or HeroRole.Hunter));

        // A generated map has no victory condition, so nobody is named as the champion and the strongest by skill
        // takes the role.
        Assert.Equal(world.GetHeroesCondWins() is null ? 1 : 0, heroes.Count(hero => hero.AiRole == HeroRole.Champion));
    }

    [Fact]
    public void TheScanOfTheMapCountsHeroesAndPutsTheEnemyArmyIntoItsRegion()
    {
        World world = NewWorld(PlayerColor.Blue | PlayerColor.Red, PlayerColor.Blue | PlayerColor.Red, GameDifficulty.Normal, 48);
        world.ComputeStaticAnalysis();

        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        world.Heroes.Init(MapVersion.SuccessionWars);

        const int HeroTile = (48 * 5) + 5;
        const int EnemyTile = (48 * 40) + 40;

        GameHero hero = world.Heroes.Get(GameHero.LordKilburn)!;
        kingdom.AddHero(hero);
        hero.LearnSkill(new SecondarySkillLevel(SecondarySkill.Wisdom, SkillLevel.Expert));
        world.GetTile(HeroTile).SetHero(world, hero);
        hero.SetMapIndex(HeroTile);

        GameHero enemy = world.Heroes.Get(GameHero.Celia)!;
        enemy.SetColor(PlayerColor.Red);
        enemy.Army.JoinTroop(MonsterType.Peasant, 10);
        world.GetTile(EnemyTile).SetHero(world, enemy);
        enemy.SetMapIndex(EnemyTile);

        int blueRegion = (int)world.GetTile(HeroTile).Region;
        int enemyRegion = (int)world.GetTile(EnemyTile).Region;

        Assert.NotEmpty(world.GetRegion((uint)enemyRegion).Neighbours);

        int availableHeroes = AiPlanner.Instance.ScanMap(kingdom);

        // The scan is also what counts the heroes that are not on patrol.
        Assert.Equal(1, availableHeroes);

        // The region of the kingdom's own hero knows about him, and the spells the region allows follow his wisdom.
        Assert.Equal(1, AiPlanner.Instance.Regions[blueRegion].FriendlyHeroes);
        Assert.Equal(5, AiPlanner.Instance.Regions[blueRegion].SpellLevel);

        // An enemy army in a region makes it contested, and the safety of a contested region is -50.
        Assert.True(AiPlanner.Instance.Regions[enemyRegion].HighestThreat > 0);
        Assert.Equal(-50, AiPlanner.Instance.Regions[enemyRegion].SafetyFactor);

        // The neighbours are valued from the region rather than the other way around: each takes a share of -50.
        foreach (uint neighbour in world.GetRegion((uint)enemyRegion).Neighbours)
        {
            Assert.True(AiPlanner.Instance.Regions[(int)neighbour].SafetyFactor < 0);
        }

        // Take the enemy off the map and the next scan forgets him: the caches do not outlive the turn.
        world.GetTile(EnemyTile).SetHero(world, null);

        AiPlanner.Instance.ScanMap(kingdom);

        // -1 is the threat of a region nothing threatens; there is no army left to beat.
        Assert.Equal(-1, AiPlanner.Instance.Regions[enemyRegion].HighestThreat);
        Assert.True(AiPlanner.Instance.Regions[enemyRegion].SafetyFactor >= 0);
    }

    [Fact]
    public void ACastleAnEnemyCanReachIsRankedBeforeOneThatIsSafe()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        world.ComputeStaticAnalysis();

        // The ranking reads the region safety of the turn, which only a scan fills.
        AiPlanner.Instance.ScanMap(world.GetKingdom(PlayerColor.Blue));

        CastleTown first = NewCastle(world, 10);
        CastleTown second = NewCastle(world, 20);
        CastleTown safe = NewCastle(world, 30);

        HashSet<int> inDanger = [first.TileIndex, second.TileIndex];

        List<AiCastle> sorted = AiPlanner.Instance.GetSortedCastleList([safe, first, second], inDanger);

        Assert.Equal(3, sorted.Count);
        Assert.True(sorted[0].UnderThreat);
        Assert.True(sorted[1].UnderThreat);
        Assert.False(sorted[2].UnderThreat);

        // The castle that is not in danger is last whatever its value: the list is the order heroes are sent in.
        Assert.Same(safe, sorted[2].Castle);
    }

    [Fact]
    public void ACastleWithAllItsHeroesHiresNobody()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        world.ComputeStaticAnalysis();

        CastleTown castle = NewCastle(world, 10);

        // One castle on the first day allows two heroes, so a kingdom that already has two hires no third one.
        bool hired = AiPlanner.Instance.PurchaseNewHeroes(
            [new AiCastle(castle, false, 0, 0)],
            new HashSet<int>(),
            availableHeroCount: 2,
            moreTasksForHeroes: true);

        Assert.False(hired);
    }

    [Fact]
    public void RevealFogStopsTheFirstMovingHeroAndIgnoresWhatIsNotAnActionObject()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);
        world.Heroes.Init(MapVersion.SuccessionWars);

        GameHero hero = world.Heroes.Get(GameHero.LordKilburn)!;
        kingdom.AddHero(hero);
        hero.SetMapIndex(20);
        world.GetTile(20).SetHero(world, hero);

        hero.Path.SetPath([
            new RouteStep(21, 20, Direction.Right, 100),
            new RouteStep(22, 21, Direction.Right, 100),
        ]);
        hero.SetMove(true);

        Assert.Equal(2, hero.Path.Count);
        Assert.True(hero.IsMoveEnabled);

        Tile windmill = world.GetTile(30);
        windmill.Index = 30;
        windmill.SetMainObjectType(Mp2ObjectType.ActionWindmill);

        AiPlanner.Instance.RevealFog(windmill, kingdom);

        // Something worth visiting came out of the fog, so the route is cut down to the step the hero is about to
        // take: the rest of it is rebuilt around the new object.
        Assert.Equal(1, hero.Path.Count);

        hero.Path.SetPath([
            new RouteStep(21, 20, Direction.Right, 100),
            new RouteStep(22, 21, Direction.Right, 100),
        ]);

        Tile grass = world.GetTile(31);
        grass.Index = 31;

        AiPlanner.Instance.RevealFog(grass, kingdom);

        // A tile with nothing on it leaves the routes of the kingdom alone.
        Assert.Equal(2, hero.Path.Count);
    }

    /// <summary>Starts a fresh game world of numbered grass tiles.</summary>
    /// <param name="colors">The colours in play.</param>
    /// <param name="computerColors">The colours the computer plays.</param>
    /// <param name="difficulty">The difficulty written to the settings and the kingdoms.</param>
    /// <param name="size">The side of the square map, in tiles.</param>
    /// <returns>The world, also installed as <see cref="World.Current"/> and remembered for <see cref="Dispose"/>.</returns>
    private World NewWorld(PlayerColor colors, PlayerColor computerColors, GameDifficulty difficulty, int size = 8)
    {
        Settings.Reset();
        Settings.Get().GameDifficulty = difficulty;
        Settings.Get().Players.Init(colors);

        World world = new();
        world.GenerateUninitializedMap(size);

        for (int index = 0; index < world.Size; ++index)
        {
            Tile tile = world.Tiles[index];

            // A generated map leaves the indexes at zero on purpose (the loaders set them), and the planner keys its
            // caches by them; the terrain has to be walkable for the regions to be computed.
            tile.Index = index;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
            tile.SetInitialPassability();
            tile.ClearFog(PlayerColor.Blue);
        }

        world.Kingdoms.Init(colors, difficulty, computerColors);

        World.Current = world;
        _world = world;

        return world;
    }

    /// <summary>Makes <paramref name="count"/> heroes of the blue kingdom, with no army and no position.</summary>
    /// <param name="world">The world to take the heroes from.</param>
    /// <param name="count">How many heroes to take.</param>
    /// <returns>The heroes, in the order they were taken.</returns>
    private static List<GameHero> NewHeroes(World world, int count)
    {
        world.Heroes.Init(MapVersion.SuccessionWars);

        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);
        List<GameHero> heroes = [];

        foreach (GameHero hero in world.Heroes)
        {
            if (heroes.Count == count)
            {
                break;
            }

            kingdom.AddHero(hero);
            heroes.Add(hero);
        }

        return heroes;
    }

    /// <summary>Builds a knight town of the given colour on a tile; it is not registered in the world's table.</summary>
    /// <param name="world">The world it stands in.</param>
    /// <param name="tileIndex">Index of the entrance tile.</param>
    /// <returns>The town.</returns>
    private static CastleTown NewCastle(World world, int tileIndex)
    {
        CastleTown castle = new(tileIndex, MapRace.Knight);
        castle.ChangeColor(PlayerColor.Blue);

        return castle;
    }
}
