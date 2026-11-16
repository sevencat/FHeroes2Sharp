using System.Numerics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Castle::ActionNewWeek()</c> (<c>castle.cpp:838-940</c>): the weekly growth of the dwellings, the
/// well bonuses, the neutral half-speed towns, the monster's week and month, and the plague.
/// </summary>
/// <remarks>
/// The towns in these tests build only the first two dwellings unless a test says otherwise: the third dwelling
/// needs the well (<c>CastleBuildings</c>), and the well hands every dwelling two extra creatures, which would
/// quietly pollute the numbers of the growth tests.
/// </remarks>
public sealed class CastleWeekGrowthTests
{
    /// <summary>Enough of everything to build anything these tests need.</summary>
    private static Funds Rich => new(999, 999, 999, 999, 999, 999, 1_000_000);

    /// <summary>A week that is neither a monster's week nor a plague month.</summary>
    private static Week OrdinaryWeek => new(WeekName.Squirrel);

    [Fact]
    public void TheFirstWeekGrowsNothing()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster2);

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);

        // castle.cpp:840-843: the shelf already holds one week of growth when the dwelling is finished, so the
        // week that opens the game must not add anything.
        castle.ActionNewWeek(weekCount: 1, OrdinaryWeek, isBeginMonth: false, dayCount: 1);

        Assert.Equal(peasants, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(archers, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void EveryBuiltDwellingGrowsOneWeek()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster2);

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);

        castle.ActionNewWeek(weekCount: 2, OrdinaryWeek, isBeginMonth: false, dayCount: 8);

        Assert.False(castle.IsBuild(BuildingType.Well));

        Assert.Equal(peasants + GrowthOf(MonsterType.Peasant), castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(archers + GrowthOf(MonsterType.Archer), castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));

        // A dwelling that was never built does not appear out of nowhere.
        Assert.Equal(0u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster4));
    }

    [Fact]
    public void TheWellFeedsEveryDwelling()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster2);
        Build(castle, BuildingType.DwellingMonster3);

        Assert.True(castle.IsBuild(BuildingType.Well));

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);
        uint pikemen = castle.GetMonstersInDwelling(BuildingType.DwellingMonster3);

        castle.ActionNewWeek(weekCount: 2, OrdinaryWeek, isBeginMonth: false, dayCount: 8);

        Assert.Equal(2u, CastleTown.GrownWell);
        Assert.Equal(
            peasants + GrowthOf(MonsterType.Peasant) + CastleTown.GrownWell,
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(
            archers + GrowthOf(MonsterType.Archer) + CastleTown.GrownWell,
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
        // The knight's third dwelling holds pikemen (monster.cpp:398-401), whose growth is five; the griffin is the
        // wizard's third dwelling and would have hidden the well's bonus behind the wrong growth.
        Assert.Equal(
            pikemen + GrowthOf(MonsterType.Pikeman) + CastleTown.GrownWell,
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster3));
    }

    [Fact]
    public void TheSecondWellFeedsTheFirstDwellingOnly()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster2);
        Build(castle, BuildingType.Well);
        Build(castle, BuildingType.Well2);

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);

        castle.ActionNewWeek(weekCount: 2, OrdinaryWeek, isBeginMonth: false, dayCount: 8);

        // castle.cpp:871-874: BUILD_WEL2 is the race's landmark, and its eight creatures go to the first dwelling.
        Assert.Equal(8u, CastleTown.GrownWel2);
        Assert.Equal(
            peasants + GrowthOf(MonsterType.Peasant) + CastleTown.GrownWell + CastleTown.GrownWel2,
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(
            archers + GrowthOf(MonsterType.Archer) + CastleTown.GrownWell,
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void ANeutralTownGrowsAtHalfSpeed()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster2);

        castle.ChangeColor(PlayerColor.None);

        Assert.Equal(PlayerColor.None, castle.Color);

        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);

        castle.ActionNewWeek(weekCount: 2, OrdinaryWeek, isBeginMonth: false, dayCount: 8, new Pcg32(12345));

        // castle.cpp:877-880: half of the growth, rounded down.
        Assert.Equal(archers + GrowthOf(MonsterType.Archer) / 2, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void AMonsterWeekFeedsTheMatchingDwelling()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster2);

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);

        castle.ActionNewWeek(weekCount: 2, new Week(WeekName.Monsters, MonsterType.Archer), isBeginMonth: false, dayCount: 8);

        // castle.cpp:886-908: five extra creatures on top of the normal growth, and only for the week's monster.
        Assert.Equal(5u, CastleTown.GrownWeekOf);
        Assert.Equal(
            archers + GrowthOf(MonsterType.Archer) + CastleTown.GrownWeekOf,
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
        Assert.Equal(peasants + GrowthOf(MonsterType.Peasant), castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
    }

    [Fact]
    public void AMonsterMonthDoublesTheMatchingDwelling()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster2);

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);

        // The first day of a monster's month: the monthly share replaces the flat weekly bonus.
        castle.ActionNewWeek(weekCount: 5, new Week(WeekName.Monsters, MonsterType.Archer), isBeginMonth: true, dayCount: 29);

        uint grownArchers = archers + GrowthOf(MonsterType.Archer);

        Assert.Equal(100u, CastleTown.GrownMonthOf);
        Assert.Equal(grownArchers + grownArchers * CastleTown.GrownMonthOf / 100, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
        Assert.Equal(peasants + GrowthOf(MonsterType.Peasant), castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
    }

    [Fact]
    public void ThePlagueFreezesGrowthDuringTheMonth()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster2);

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint archers = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);

        // castle.cpp:864-866: a plague month grows nothing at all, week after week.
        castle.ActionNewWeek(weekCount: 2, new Week(WeekName.Plague), isBeginMonth: false, dayCount: 8);
        castle.ActionNewWeek(weekCount: 3, new Week(WeekName.Plague), isBeginMonth: false, dayCount: 15);

        Assert.Equal(peasants, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(archers, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void ThePlagueHalvesTheStocksOnTheFirstDayOfTheMonth()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingMonster1);
        Build(castle, BuildingType.DwellingMonster3);

        uint peasants = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        uint pikemen = castle.GetMonstersInDwelling(BuildingType.DwellingMonster3);

        // The third dwelling holds five pikemen — an odd stock, so the halving's rounding is visible.
        Assert.Equal(1u, pikemen % 2);

        castle.ActionNewWeek(weekCount: 5, new Week(WeekName.Plague), isBeginMonth: true, dayCount: 29);

        // castle.cpp:922-926: the stocks are halved, and halved means rounded down.
        Assert.Equal(peasants / 2, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(pikemen / 2, castle.GetMonstersInDwelling(BuildingType.DwellingMonster3));
    }

    [Fact]
    public void AnUpgradedDwellingTakesTheBonusForItsOwnCreature()
    {
        CastleTown castle = Town();
        Build(castle, BuildingType.DwellingUpgrade2);

        uint rangers = castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade2);

        // The upgrade drags in the third dwelling as a prerequisite, and the knight's third dwelling needs the
        // well, so the ordinary growth carries the well's two creatures on top.
        Assert.True(castle.IsBuild(BuildingType.Well));

        castle.ActionNewWeek(weekCount: 2, new Week(WeekName.Monsters, MonsterType.Archer), isBeginMonth: false, dayCount: 8);

        // castle.cpp:886-893: only the dwelling whose exact building stands counts, so an upgraded tower is not fed
        // by the archers' week even though both share a shelf.
        Assert.Equal(
            rangers + GrowthOf(MonsterType.Ranger) + CastleTown.GrownWell,
            castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade2));

        uint afterArcherWeek = castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade2);

        castle.ActionNewWeek(weekCount: 3, new Week(WeekName.Monsters, MonsterType.Ranger), isBeginMonth: false, dayCount: 15);

        Assert.Equal(
            afterArcherWeek + GrowthOf(MonsterType.Ranger) + CastleTown.GrownWell + CastleTown.GrownWeekOf,
            castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade2));
    }

    [Fact]
    public void AllCastlesTicksEveryTown()
    {
        var towns = new AllCastles(0);
        CastleTown first = Town();
        CastleTown second = Town();

        Build(first, BuildingType.DwellingMonster1);
        Build(second, BuildingType.DwellingMonster1);

        towns.Add(first);
        towns.Add(second);

        uint before = first.GetMonstersInDwelling(BuildingType.DwellingMonster1);

        towns.NewWeek(weekCount: 2, OrdinaryWeek, isBeginMonth: false, dayCount: 8);

        Assert.Equal(before + GrowthOf(MonsterType.Peasant), first.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(before + GrowthOf(MonsterType.Peasant), second.GetMonstersInDwelling(BuildingType.DwellingMonster1));
    }

    [Fact]
    public void TheDayHandsTheBuildingPermitBackToEveryTown()
    {
        var towns = new AllCastles(0);
        CastleTown first = Town();
        CastleTown second = Town();

        towns.Add(first);
        towns.Add(second);

        Funds treasury = Rich;

        Assert.True(first.BuyBuilding(BuildingType.DwellingMonster1, ref treasury));
        Assert.False(first.CanBuildToday);

        towns.NewDay();

        Assert.True(first.CanBuildToday);
        Assert.True(second.CanBuildToday);
    }

    [Fact]
    public void TheWorldGrowsItsTownsWhenTheWeekTurns()
    {
        // A world with no map is enough here: the clock does not read the terrain, and the castle table is public.
        World world = new();
        CastleTown castle = Town();

        Build(castle, BuildingType.DwellingMonster1);

        world.Castles.Add(castle);

        uint before = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);

        // Days 1 to 7 are the opening week: world.cpp:443-449 hands the week to the towns only from the second.
        for (uint day = 0; day < 7; ++day)
        {
            world.NewDay();
        }

        Assert.Equal(1u, world.WeekCount);
        Assert.Equal(before, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));

        world.NewDay();

        Assert.Equal(2u, world.WeekCount);
        Assert.Equal(before + GrowthOf(MonsterType.Peasant), castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
    }

    /// <summary>One week of a creature, <c>Monster::GetGrown()</c>.</summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The growth of its dwelling.</returns>
    private static uint GrowthOf(MonsterType monster) => MonsterStatistics.GetGeneralStats(monster).BaseGrowth;

    /// <summary>A Knight town with its fort built and nothing else, owned by the blue player.</summary>
    /// <remarks>
    /// The owner matters: an ownerless town is a neutral one and grows at half speed, so every test that is not
    /// about neutrals has to set a color.
    /// </remarks>
    private static CastleTown Town()
    {
        CastleTown castle = new(0, MapRace.Knight, MapVersion.PriceOfLoyalty) { HasSeaAccess = true };
        Funds treasury = Rich;

        Assert.True(castle.BuyBuilding(BuildingType.Castle, ref treasury));

        castle.ActionNewDay();
        castle.ChangeColor(PlayerColor.Blue);

        return castle;
    }

    /// <summary>Buys one building and everything it needs, then hands the building permit back.</summary>
    /// <param name="castle">The town.</param>
    /// <param name="building">The building to buy.</param>
    private static void Build(CastleTown castle, BuildingType building)
    {
        if (castle.IsBuild(building))
        {
            return;
        }

        BuildingType requirement = CastleBuildings.GetRequirement(castle.Race, building);

        foreach (BuildingType prerequisite in SingleBuildings())
        {
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

    /// <summary>The buildings, without the three masks <see cref="BuildingType"/> throws in for convenience.</summary>
    /// <returns>Every building whose value holds one bit.</returns>
    private static IEnumerable<BuildingType> SingleBuildings() =>
        Enum.GetValues<BuildingType>().Where(building => BitOperations.PopCount((uint)building) == 1);
}
