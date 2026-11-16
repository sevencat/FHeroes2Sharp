using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the six fixed prices of <c>payment.cpp</c> (<see cref="PaymentConditions"/>).
/// </summary>
public sealed class PaymentConditionsTests
{
    [Fact]
    public void EveryFixedPriceIsTheReferenceAmount()
    {
        // payment.cpp:36-57, in the reference's `Cost` order { gold, wood, mercury, ore, sulfur, crystal, gems }.
        Assert.Equal(new Funds { Gold = 1000, Wood = 10 }, PaymentConditions.BuyBoat());
        Assert.Equal(new Funds { Gold = 500 }, PaymentConditions.BuySpellBook());
        Assert.Equal(new Funds { Gold = 2500 }, PaymentConditions.RecruitHero());
        Assert.Equal(new Funds { Gold = 750 }, PaymentConditions.ForAlchemist());
        Assert.Equal(new Funds { Gold = 1000 }, PaymentConditions.GetMagellansMapsPurchasePrice());
    }

    [Fact]
    public void TheBoatIsGoldAndWoodAndNotTheOtherWayRound()
    {
        // The trap of the `Cost` initializer: a thousand gold and ten wood, not a thousand wood and ten gold.
        Funds boat = PaymentConditions.BuyBoat();

        Assert.Equal(1000, boat.Gold);
        Assert.Equal(10, boat.Wood);
        Assert.Equal(0, boat.Ore);
        Assert.Equal(0, boat.Mercury);
    }

    [Fact]
    public void ABuildingIsPricedByItsRace()
    {
        // The warlock's sixth dwelling (payment.cpp:31 forwards to buildinginfo.cpp:203-212). The knight has a
        // sixth dwelling too, and it does not cost the same.
        Assert.Equal(
            new Funds { Gold = 15000, Ore = 30, Sulfur = 20 },
            PaymentConditions.BuyBuilding(MapRace.Warlock, BuildingType.DwellingMonster6));

        Assert.NotEqual(
            PaymentConditions.BuyBuilding(MapRace.Warlock, BuildingType.DwellingMonster6),
            PaymentConditions.BuyBuilding(MapRace.Knight, BuildingType.DwellingMonster6));
    }

    [Fact]
    public void ABuildingTheRaceCannotBuildCostsNothing()
    {
        // The black dragon lair's upgrade is the warlock's alone.
        Assert.Equal(default, PaymentConditions.BuyBuilding(MapRace.Knight, BuildingType.DwellingUpgrade7));
        Assert.NotEqual(default, PaymentConditions.BuyBuilding(MapRace.Warlock, BuildingType.DwellingUpgrade7));
    }

    [Fact]
    public void TheForwardingToTheBuildingTableKeepsTheWholeTable()
    {
        // Not a spot check: every building of every race must answer exactly what the castle's own table says.
        foreach (MapRace race in new[]
                 {
                     MapRace.Knight, MapRace.Barbarian, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard,
                     MapRace.Necromancer,
                 })
        {
            foreach (BuildingType building in Enum.GetValues<BuildingType>())
            {
                Assert.Equal(CastleBuildings.GetCost(building, race), PaymentConditions.BuyBuilding(race, building));
            }
        }
    }
}
