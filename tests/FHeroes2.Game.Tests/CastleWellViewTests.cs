using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="CastleWellDwelling"/> and <see cref="Castle.GetWellDwellings()"/>: what the well window reads from a
/// town (<c>castle_well.cpp:405-547</c>).
/// </summary>
public sealed class CastleWellViewTests
{
    private static readonly Funds Enough = new(0, 0, 0, 0, 0, 0, 100000);

    private static readonly BuildingType[] Dwellings =
    [
        BuildingType.DwellingMonster1,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingMonster6,
    ];

    /// <summary>Builds the dwellings in the given order, each stocked by the day that follows.</summary>
    private static CastleTown CreateTownWithStock(params BuildingType[] dwellings)
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle);

        foreach (BuildingType dwelling in dwellings)
        {
            Funds price = Enough;

            Assert.True(castle.BuyBuilding(dwelling, ref price));
            castle.ActionNewDay();
        }

        return castle;
    }

    private static uint BaseGrowth(BuildingType dwelling) =>
        MonsterStatistics.GetGeneralStats(DwellingMonsters.FromDwelling(MapRace.Knight, dwelling)).BaseGrowth;

    [Fact]
    public void TheWindowAlwaysHasSixRows()
    {
        CastleTown castle = new(0, MapRace.Knight);
        IReadOnlyList<CastleWellDwelling> rows = castle.GetWellDwellings();

        Assert.Equal(CastleTown.MaxNumOfDwellings, rows.Count);

        for (int index = 0; index < rows.Count; ++index)
        {
            CastleWellDwelling row = rows[index];

            Assert.Equal(index + 1, row.Level);
            Assert.Equal(Dwellings[index], row.Dwelling);
            Assert.Equal(Dwellings[index], row.ActualDwelling);
            Assert.False(row.IsBuilt);
            Assert.Equal(0u, row.Available);
        }
    }

    [Fact]
    public void AnEmptyTownShowsTheBaseStatisticsWithoutGrowthBonus()
    {
        CastleTown castle = new(0, MapRace.Knight);
        CastleWellDwelling row = castle.GetWellDwellings()[0];

        MonsterType peasant = DwellingMonsters.FromDwelling(MapRace.Knight, BuildingType.DwellingMonster1);

        Assert.Equal(peasant, row.Monster);
        Assert.Equal(MonsterStatistics.GetBattleStats(peasant), row.Stats);
        Assert.Equal(MonsterNames.GetMulti(peasant), row.MonsterName);
        Assert.Equal(BaseGrowth(BuildingType.DwellingMonster1), row.WeeklyGrowth);
    }

    [Fact]
    public void TheWellAddsTwoToEveryDwelling()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.Well);

        IReadOnlyList<CastleWellDwelling> rows = castle.GetWellDwellings();

        // The well is built, so every row carries its two whether the dwelling stands or not.
        Assert.Equal(BaseGrowth(BuildingType.DwellingMonster1) + CastleTown.GrownWell, rows[0].WeeklyGrowth);
        Assert.Equal(BaseGrowth(BuildingType.DwellingMonster6) + CastleTown.GrownWell, rows[5].WeeklyGrowth);
    }

    [Fact]
    public void TheLandmarkBuildingAddsEightToTheFirstLevelOnly()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.Well | BuildingType.Well2);

        IReadOnlyList<CastleWellDwelling> rows = castle.GetWellDwellings();

        Assert.Equal(
            BaseGrowth(BuildingType.DwellingMonster1) + CastleTown.GrownWell + CastleTown.GrownWel2,
            rows[0].WeeklyGrowth);
        Assert.Equal(BaseGrowth(BuildingType.DwellingMonster2) + CastleTown.GrownWell, rows[1].WeeklyGrowth);
    }

    [Fact]
    public void AnUpgradedDwellingShowsItsCreatureButIsNotBuiltWithoutTheBaseDwelling()
    {
        // A save can carry the upgrade bit alone; the reference then prints the upgraded creature and no growth
        // line at all, because it asks the *base* dwelling whether the row is built.
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.DwellingUpgrade2);

        CastleWellDwelling row = castle.GetWellDwellings()[1];

        Assert.Equal(BuildingType.DwellingMonster2, row.Dwelling);
        Assert.Equal(BuildingType.DwellingUpgrade2, row.ActualDwelling);
        Assert.Equal(DwellingMonsters.FromDwelling(MapRace.Knight, BuildingType.DwellingUpgrade2), row.Monster);
        Assert.NotEqual(DwellingMonsters.FromDwelling(MapRace.Knight, BuildingType.DwellingMonster2), row.Monster);
        Assert.False(row.IsBuilt);
        Assert.Equal(0u, row.Available);
    }

    [Fact]
    public void TheStockComesFromTheDwellingThatStands()
    {
        CastleTown castle = CreateTownWithStock(BuildingType.DwellingMonster1);

        CastleWellDwelling row = castle.GetWellDwellings()[0];

        Assert.True(row.IsBuilt);
        Assert.True(row.Available > 0);
        Assert.Equal(castle.GetMonstersInDwelling(BuildingType.DwellingMonster1), row.Available);
        Assert.Equal(BaseGrowth(BuildingType.DwellingMonster1), row.WeeklyGrowth);
    }
}
