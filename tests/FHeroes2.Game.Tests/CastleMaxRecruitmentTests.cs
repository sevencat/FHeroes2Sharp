using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using ArmyCorps = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="CastleMaxRecruitment"/>: what the well's MAX button would buy.
/// </summary>
public sealed class CastleMaxRecruitmentTests
{
    private static readonly Funds Enough = new(0, 0, 0, 0, 0, 0, 100000);

    /// <summary>Builds a knight town with the dwellings in the given order, each stocked by the purchase.</summary>
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

    private static Funds UnitCost(MonsterType monster) => MonsterStatistics.GetGeneralStats(monster).GetCost();

    [Fact]
    public void ATownWithoutDwellingsHasNothingToSell()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle);

        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(castle, new ArmyCorps(), null, Enough);

        Assert.Empty(plan.Entries);
        Assert.False(plan.HasSomethingToRecruit);
        Assert.Equal(CastleMaxRecruitment.Refusal.NoCreatures, plan.Reason);
        Assert.Equal("No creatures available for purchase.", plan.RefusalText);
    }

    [Fact]
    public void TheWholeStockIsBoughtWhenMoneyIsNoObject()
    {
        CastleTown castle = CreateTownWithStock(BuildingType.DwellingMonster1);
        uint stock = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        MonsterType peasant = DwellingMonsters.FromDwelling(MapRace.Knight, BuildingType.DwellingMonster1);

        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(castle, new ArmyCorps(), null, Enough);

        CastleMaxRecruitment.Entry entry = Assert.Single(plan.Entries);
        Assert.True(plan.HasSomethingToRecruit);
        Assert.Equal(CastleMaxRecruitment.Refusal.None, plan.Reason);
        Assert.Null(plan.RefusalText);
        Assert.Equal(peasant, entry.Monster);
        Assert.Equal(stock, entry.Count);
        Assert.True(UnitCost(peasant) * entry.Count == plan.Cost);

        // One line per entry, with the singular name only for a count of one.
        string expectedName = entry.Count == 1 ? MonsterNames.GetSingular(peasant) : MonsterNames.GetMulti(peasant);
        Assert.Equal($"{expectedName} : {entry.Count}\n", plan.Summary);
    }

    [Fact]
    public void TheMoneyLimitsHowManyAreBought()
    {
        CastleTown castle = CreateTownWithStock(BuildingType.DwellingMonster1);
        uint stock = castle.GetMonstersInDwelling(BuildingType.DwellingMonster1);
        MonsterType peasant = DwellingMonsters.FromDwelling(MapRace.Knight, BuildingType.DwellingMonster1);

        Assert.True(stock >= 3);

        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(castle, new ArmyCorps(), null, UnitCost(peasant) * 3);

        CastleMaxRecruitment.Entry entry = Assert.Single(plan.Entries);
        Assert.Equal(3u, entry.Count);
        Assert.True(UnitCost(peasant) * 3 == plan.Cost);
    }

    [Fact]
    public void NotEvenOneCreatureIsReportedAsPoverty()
    {
        CastleTown castle = CreateTownWithStock(BuildingType.DwellingMonster1);

        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(castle, new ArmyCorps(), null, new Funds());

        Assert.Empty(plan.Entries);
        Assert.Equal(CastleMaxRecruitment.Refusal.NotEnoughResources, plan.Reason);
        Assert.Equal("Not enough resources to recruit creatures.", plan.RefusalText);
    }

    [Fact]
    public void AFullGarrisonWithNoHeroIsReportedAsRanksFull()
    {
        CastleTown castle = CreateTownWithStock(BuildingType.DwellingMonster1);

        ArmyCorps full = new ArmyCorps();
        Assert.True(full.JoinTroop(MonsterType.Goblin, 1));
        Assert.True(full.JoinTroop(MonsterType.Orc, 1));
        Assert.True(full.JoinTroop(MonsterType.Wolf, 1));
        Assert.True(full.JoinTroop(MonsterType.Ogre, 1));
        Assert.True(full.JoinTroop(MonsterType.Troll, 1));

        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(castle, full, null, Enough);

        Assert.Empty(plan.Entries);
        Assert.Equal(CastleMaxRecruitment.Refusal.RanksFull, plan.Reason);
        Assert.Equal("You are unable to recruit at this time, your ranks are full.", plan.RefusalText);
    }

    [Fact]
    public void TheStrongestDwellingIsBoughtFirst()
    {
        CastleTown castle = CreateTownWithStock(BuildingType.DwellingMonster1, BuildingType.DwellingMonster2);
        MonsterType archer = DwellingMonsters.FromDwelling(MapRace.Knight, BuildingType.DwellingMonster2);

        // Exactly one archer: the second level is the stronger one, so it takes the money and the first level gets none.
        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(castle, new ArmyCorps(), null, UnitCost(archer));

        CastleMaxRecruitment.Entry entry = Assert.Single(plan.Entries);
        Assert.Equal(archer, entry.Monster);
        Assert.Equal(1u, entry.Count);
    }

    [Fact]
    public void PovertyIsReportedBeforeAFullGarrison()
    {
        CastleTown castle = CreateTownWithStock(BuildingType.DwellingMonster1);

        ArmyCorps full = new ArmyCorps();
        Assert.True(full.JoinTroop(MonsterType.Goblin, 1));
        Assert.True(full.JoinTroop(MonsterType.Orc, 1));
        Assert.True(full.JoinTroop(MonsterType.Wolf, 1));
        Assert.True(full.JoinTroop(MonsterType.Ogre, 1));
        Assert.True(full.JoinTroop(MonsterType.Troll, 1));

        // Both reasons are true: the ranks are full and the treasury cannot pay. The reference asks about money first.
        CastleMaxRecruitment plan = CastleMaxRecruitment.Create(castle, full, null, new Funds());

        Assert.Equal(CastleMaxRecruitment.Refusal.NotEnoughResources, plan.Reason);
    }
}
