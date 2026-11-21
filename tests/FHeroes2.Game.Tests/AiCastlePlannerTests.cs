using FHeroes2.Assets.Maps;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;
using GameHero = FHeroes2.Game.Heroes.Hero;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Behaviour tests for the four members of <c>ai_planner_castle.cpp</c>: what a castle does before a battle, on a
/// defensive turn, when it is reinforced, and what the turn puts on the kingdom's budget.
/// </summary>
/// <remarks>
/// The planner is a singleton and the budget outlives a test, so every test that fills it empties it again; a
/// peaceful castle turn is not tested here because it reads the region stats that <c>evaluateRegionSafety()</c> has
/// to fill first.
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AiCastlePlannerTests : IDisposable
{
    private readonly World _previousWorld = World.Current;

    public void Dispose()
    {
        World.Current = _previousWorld;
        Settings.Reset();
    }

    [Fact]
    public void EasyDevelopsEveryOtherDayAndNeverBuildsItsSixthDwelling()
    {
        // Easy skips the odd days, the first day of the game included, and a campaign lifts the restriction.
        Assert.False(Difficulty.AllowAiToDevelopCastlesOnDay(GameDifficulty.Easy, false, 1));
        Assert.True(Difficulty.AllowAiToDevelopCastlesOnDay(GameDifficulty.Easy, false, 2));
        Assert.True(Difficulty.AllowAiToDevelopCastlesOnDay(GameDifficulty.Easy, true, 1));

        Assert.True(Difficulty.AllowAiToDevelopCastlesOnDay(GameDifficulty.Normal, false, 1));

        // Easy is not allowed the last dwelling nor the last mage guild, and neither is restricted by the campaign.
        Assert.False(Difficulty.AllowAiToBuildCastleBuilding(GameDifficulty.Easy, false, BuildingType.DwellingMonster6));
        Assert.False(Difficulty.AllowAiToBuildCastleBuilding(GameDifficulty.Easy, false, BuildingType.MageGuild5));

        // Only the sixth dwelling itself is held back: its upgrade and the fifth dwelling are not.
        Assert.True(Difficulty.AllowAiToBuildCastleBuilding(GameDifficulty.Easy, false, BuildingType.DwellingUpgrade6));
        Assert.True(Difficulty.AllowAiToBuildCastleBuilding(GameDifficulty.Easy, false, BuildingType.DwellingMonster5));
        Assert.True(Difficulty.AllowAiToBuildCastleBuilding(GameDifficulty.Normal, false, BuildingType.MageGuild5));
        Assert.True(Difficulty.AllowAiToBuildCastleBuilding(GameDifficulty.Easy, true, BuildingType.DwellingMonster6));
    }

    [Fact]
    public void ACastleWithNoGuestHeroHasNothingToArrangeBeforeABattle()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);
        castle.Army.JoinTroop(MonsterType.Archer, 4);

        AiPlanner.CastlePreBattle(castle);

        // Without a hero to hand the troops to, the garrison is what defends the castle and stays where it is.
        Assert.Equal(4u, castle.Army.GetCount());
        Assert.Equal(1u, castle.Army.OccupiedSlotCount);
    }

    [Fact]
    public void TheGarrisonJoinsTheGuestHeroBeforeABattle()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);
        castle.Army.JoinTroop(MonsterType.Archer, 4);
        castle.Army.JoinTroop(MonsterType.Pikeman, 2);

        GameHero hero = NewGuestHero(world, castle);
        hero.Army.JoinTroop(MonsterType.Peasant, 10);

        AiPlanner.CastlePreBattle(castle);

        // The garrison moved into the hero's free slots and is left empty.
        Assert.Equal(16u, hero.Army.GetCount());
        Assert.Equal(3u, hero.Army.OccupiedSlotCount);
        Assert.Equal(0u, castle.Army.GetCount());

        // The shooters take the front slot and the melee stacks follow, fastest first, because the troops moved.
        Assert.Equal(MonsterType.Archer, hero.Army.GetTroop(0).Monster);
        Assert.Equal(MonsterType.Pikeman, hero.Army.GetTroop(1).Monster);
        Assert.Equal(MonsterType.Peasant, hero.Army.GetTroop(2).Monster);
    }

    [Fact]
    public void ADefensiveCastleHiresFirstAndBuildsOnlyDefensiveStructures()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        kingdom.Treasury = Rich;

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);

        Build(castle, BuildingType.Castle);
        Build(castle, BuildingType.DwellingMonster1);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);

        AiPlanner.Instance.CastleTurn(castle, true);

        // The dwellings were emptied into the garrison before anything was built.
        Assert.True(castle.Army.HasMonster(MonsterType.Peasant));
        Assert.True(castle.GetMonstersInDwelling(BuildingType.DwellingMonster1) < shelf);

        // The first turret is the one building the permit allows today, so the supporting list gets nothing and no
        // dwelling is built either: a dwelling a captured castle leaves behind would arm the enemy.
        Assert.True(castle.IsBuild(BuildingType.LeftTurret));
        Assert.False(castle.IsBuild(BuildingType.MageGuild1));
        Assert.False(castle.IsBuild(BuildingType.RightTurret));
        Assert.False(castle.IsBuild(BuildingType.Statue));
        Assert.False(castle.IsBuild(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void ADefensiveCastleWithNobodyToDefendItPaysForNoTurrets()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        kingdom.Treasury = Rich;

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);
        Build(castle, BuildingType.Castle);

        // No dwelling and no garrison: there is nobody to man a turret, so nothing is built.
        AiPlanner.Instance.CastleTurn(castle, true);

        Assert.Equal(0u, castle.Army.GetCount());
        Assert.False(castle.IsBuild(BuildingType.LeftTurret));
        Assert.False(castle.IsBuild(BuildingType.MageGuild1));
    }

    [Fact]
    public void ReinforcingACastleHiresAStackFromItsDwelling()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        kingdom.Treasury = Rich;

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);
        Build(castle, BuildingType.DwellingMonster1);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);

        Assert.True(shelf > 0, "a finished dwelling has creatures on its shelf");

        Funds before = kingdom.Treasury;

        AiPlanner.Instance.ReinforceCastle(castle);

        Assert.Equal(shelf, castle.Army.GetCount());
        Assert.True(castle.Army.HasMonster(MonsterType.Peasant));
        Assert.Equal(0u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));

        // Peasants cost gold only, and the price was taken from the kingdom's purse.
        Assert.True(kingdom.Treasury.Gold < before.Gold);
        Assert.Equal(before.Wood, kingdom.Treasury.Wood);
    }

    [Fact]
    public void ReinforcingAFullGarrisonDismissesTheWeakestStackOnlyWhenItIsWeaker()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        kingdom.Treasury = Rich;

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);
        Build(castle, BuildingType.DwellingMonster1);

        // Every slot holds one creature, and none of them is a peasant, so the hired stack cannot merge. The archer
        // is the weakest of the five, which is what makes the test decide which stack goes.
        castle.Army.JoinTroop(MonsterType.Archer, 1);
        castle.Army.JoinTroop(MonsterType.Swordsman, 1);
        castle.Army.JoinTroop(MonsterType.Cavalry, 1);
        castle.Army.JoinTroop(MonsterType.Paladin, 1);
        castle.Army.JoinTroop(MonsterType.Champion, 1);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);

        AiPlanner.Instance.ReinforceCastle(castle);

        // A whole shelf of peasants outweighs a single archer, so the archer was dismissed to make room.
        Assert.Equal(5u, castle.Army.OccupiedSlotCount);
        Assert.False(castle.Army.HasMonster(MonsterType.Archer));
        Assert.True(castle.Army.HasMonster(MonsterType.Peasant));
        Assert.Equal(shelf, TotalTroopCount(castle.Army, MonsterType.Peasant));
    }

    [Fact]
    public void AFullGarrisonMakesRoomByHandingAStackToTheGuestHero()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        kingdom.Treasury = Rich;

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);

        // The archers' dwelling needs the peasants' one, and both are hired from.
        Build(castle, BuildingType.DwellingMonster2);

        uint peasantShelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);

        castle.Army.JoinTroop(MonsterType.Peasant, 5);
        castle.Army.JoinTroop(MonsterType.Pikeman, 1);
        castle.Army.JoinTroop(MonsterType.Swordsman, 1);
        castle.Army.JoinTroop(MonsterType.Cavalry, 1);
        castle.Army.JoinTroop(MonsterType.Champion, 1);

        GameHero hero = NewGuestHero(world, castle);

        // The hero is full as well, and the only stack it shares with the garrison is the peasants'. A hundred of
        // them keep every garrison stack the weaker one, so the swapping that follows the reinforcement does not
        // move anything out of the garrison again.
        hero.Army.JoinTroop(MonsterType.Peasant, 100);
        hero.Army.JoinTroop(MonsterType.BlackDragon, 50);
        hero.Army.JoinTroop(MonsterType.Titan, 50);
        hero.Army.JoinTroop(MonsterType.BoneDragon, 50);
        hero.Army.JoinTroop(MonsterType.Phoenix, 50);

        AiPlanner.Instance.ReinforceCastle(castle);

        // The garrison's peasants merged into the hero's stack, which freed the slot the archers were hired into.
        // The peasant dwelling then hires into the hero as well, because the garrison took the archers instead.
        Assert.Equal(1u, CountTroops(hero.Army, MonsterType.Peasant));
        Assert.Equal(105u + peasantShelf, TotalTroopCount(hero.Army, MonsterType.Peasant));
        Assert.True(castle.Army.HasMonster(MonsterType.Archer));
        Assert.Equal(5u, castle.Army.OccupiedSlotCount);
    }

    [Fact]
    public void TheBudgetMarksWhatTheKingdomIsShortOfAndDoublesItsWeight()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);
        kingdom.AddCastle(castle);

        // An empty purse and an unbuilt castle: wood, ore and gold are all short, gems are not needed at all.
        kingdom.Treasury = new Funds();

        try
        {
            AiPlanner.Instance.UpdateKingdomBudget(kingdom);

            Assert.Equal(2.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Gold, false), 3);
            Assert.Equal(250.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Wood, false), 3);
            Assert.Equal(250.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Ore, false), 3);

            // Nothing on the build order of a knight town costs gems, so its weight is untouched.
            Assert.Equal(250.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Gems, false), 3);
        }
        finally
        {
            ResetBudget(world);
        }
    }

    [Fact]
    public void ACastleThatCanFieldItsStrongestUnitKeepsNeedingTheUpgradedOne()
    {
        World world = NewWorld(PlayerColor.Blue, PlayerColor.Blue, GameDifficulty.Normal);
        GameKingdom kingdom = world.GetKingdom(PlayerColor.Blue);

        CastleTown castle = NewCastle(world, 10, PlayerColor.Blue);
        castle.AddBuildings(BuildingType.Castle | BuildingType.DwellingMonster6);
        kingdom.AddCastle(castle);

        // Rich enough for everything, so nothing is missing and nothing is a priority.
        kingdom.Treasury = Rich;

        try
        {
            AiPlanner.Instance.UpdateKingdomBudget(kingdom);

            // The sixth dwelling keeps hiring, so the price of the crusader it upgrades to stays a running cost.
            Assert.Equal(1.5, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Gold, false), 3);

            // A resource the crusader does not cost keeps its plain weight.
            Assert.Equal(125.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Wood, false), 3);
        }
        finally
        {
            ResetBudget(world);
        }
    }

    /// <summary>Enough of everything for any building and any recruitment in these tests.</summary>
    private static Funds Rich => new(999, 999, 999, 999, 999, 999, 1_000_000);

    /// <summary>
    /// Empties the planner's budget by giving it a kingdom without castles, so that the singleton does not leak the
    /// entries of one test into the next.
    /// </summary>
    /// <param name="world">The world, whose red kingdom owns nothing here.</param>
    private static void ResetBudget(World world) => AiPlanner.Instance.UpdateKingdomBudget(world.GetKingdom(PlayerColor.Red));

    /// <summary>Counts the stacks of one monster in an army.</summary>
    /// <param name="army">The army to scan.</param>
    /// <param name="monster">The monster to look for.</param>
    /// <returns>The number of stacks, empty slots excluded.</returns>
    private static uint CountTroops(GameArmy army, MonsterType monster)
    {
        uint count = 0;

        for (int slot = 0; slot < GameArmy.MaximumTroopCount; ++slot)
        {
            if (army.GetTroop(slot).Monster == monster)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>Adds up the creatures of one monster in an army.</summary>
    /// <param name="army">The army to scan.</param>
    /// <param name="monster">The monster to add up.</param>
    /// <returns>The total count of that monster.</returns>
    private static uint TotalTroopCount(GameArmy army, MonsterType monster)
    {
        uint total = 0;

        for (int slot = 0; slot < GameArmy.MaximumTroopCount; ++slot)
        {
            Troop troop = army.GetTroop(slot);

            if (troop.Monster == monster)
            {
                total += troop.Count;
            }
        }

        return total;
    }

    /// <summary>Builds a building and everything it needs, then hands the building permit back.</summary>
    /// <param name="castle">The town.</param>
    /// <param name="building">The building to buy.</param>
    private static void Build(CastleTown castle, BuildingType building)
    {
        if (castle.IsBuild(building))
        {
            return;
        }

        // The tent has no prerequisites of its own: buying the castle is what replaces it.
        if (!castle.IsCastle && building != BuildingType.Castle)
        {
            Build(castle, BuildingType.Castle);
        }

        BuildingType requirement = CastleBuildings.GetRequirement(castle.Race, building);

        foreach (BuildingType prerequisite in Enum.GetValues<BuildingType>())
        {
            if (!IsSingleBuilding(prerequisite))
            {
                continue;
            }

            if ((requirement & prerequisite) != 0)
            {
                Build(castle, prerequisite);
            }
        }

        Funds treasury = Rich;

        castle.ActionNewDay();

        Assert.True(castle.BuyBuilding(building, ref treasury), $"{building} should be for sale");

        castle.ActionNewDay();
    }

    /// <summary>Whether a building type names exactly one building rather than a mask.</summary>
    /// <param name="building">The building type to check.</param>
    /// <returns><see langword="true"/> for a single bit.</returns>
    private static bool IsSingleBuilding(BuildingType building)
    {
        int value = (int)building;

        return value != 0 && (value & (value - 1)) == 0;
    }

    /// <summary>Starts a fresh game world on a square of empty tiles.</summary>
    /// <param name="colors">The colours in play.</param>
    /// <param name="computerColors">The colours the computer plays.</param>
    /// <param name="difficulty">The difficulty written to the settings and the kingdoms.</param>
    /// <returns>The world, also installed as <see cref="World.Current"/>.</returns>
    private static World NewWorld(PlayerColor colors, PlayerColor computerColors, GameDifficulty difficulty)
    {
        Settings.Reset();
        Settings.Get().GameDifficulty = difficulty;
        Settings.Get().Players.Init(colors);

        World world = new();
        world.GenerateUninitializedMap(8);
        world.Kingdoms.Init(colors, difficulty, computerColors);
        World.Current = world;

        return world;
    }

    /// <summary>Builds a knight town of the given colour; it is not registered in any table.</summary>
    /// <param name="world">The world it stands in.</param>
    /// <param name="tileIndex">Index of the entrance tile.</param>
    /// <param name="color">The owner.</param>
    /// <returns>The town.</returns>
    private static CastleTown NewCastle(World world, int tileIndex, PlayerColor color)
    {
        CastleTown castle = new(tileIndex, MapRace.Knight);
        castle.ChangeColor(color);

        return castle;
    }

    /// <summary>Puts an AI hero of the castle's colour on the castle's tile.</summary>
    /// <param name="world">The world to change.</param>
    /// <param name="castle">The castle the hero stands in.</param>
    /// <returns>The hero.</returns>
    private static GameHero NewGuestHero(World world, CastleTown castle)
    {
        world.Heroes.Init(MapVersion.SuccessionWars);

        GameHero hero = world.Heroes.Get(GameHero.LordKilburn)!;
        hero.SetColor(castle.Color);

        // Both halves of "standing on the castle": the tile owner and the hero's own position, which is what the
        // planner looks at when it asks who visits a castle.
        world.GetTile(castle.TileIndex).SetHero(world, hero);
        hero.SetMapIndex(castle.TileIndex);

        return hero;
    }
}
