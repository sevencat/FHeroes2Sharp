using System.Numerics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using ArmyTroops = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Recruiting from a town's dwellings and upgrading a stack in that town: the shelf, the purse and the building
/// that makes an upgrade possible.
/// </summary>
public sealed class CastleRecruitTests
{
    /// <summary>Enough of everything for any recruit or upgrade in these tests, several times over.</summary>
    private static Funds Rich => new(999, 999, 999, 999, 999, 999, 1_000_000);

    [Fact]
    public void TheShelfAndThePurseTakeTurnsBeingTheLimit()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);
        Funds one = MonsterStatistics.GetGeneralStats(MonsterType.Archer).GetCost();

        Assert.True(shelf > 3, "the archers' dwelling should start with more than three archers");

        // Three creatures' worth of money buys three creatures, whatever the shelf holds.
        Assert.Equal(3u, castle.GetRecruitLimit(MonsterType.Archer, one * 3u));

        // Rich enough for anything, and the shelf becomes the limit.
        Assert.Equal(shelf, castle.GetRecruitLimit(MonsterType.Archer, Rich));

        // Exactly nothing to spend is nothing to buy.
        Assert.Equal(0u, castle.GetRecruitLimit(MonsterType.Archer, new Funds()));
    }

    [Fact]
    public void ACreatureOfAnotherRaceIsNotForSale()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        // The shelf is full of archers; a skeleton is not in this town at all, and its "dwelling" bit happens to
        // exist here, so the race check is what answers no.
        Assert.Equal(0u, castle.GetRecruitLimit(MonsterType.Skeleton, Rich));
        Assert.True(castle.GetRecruitLimit(MonsterType.Archer, Rich) > 0);
    }

    [Fact]
    public void AnUpgradedDwellingSellsTheUpgradedCreature()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingUpgrade2);

        // One shelf, two names: the archers' dwelling and the rangers' tower answer with the same stock.
        Assert.Equal(
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster2),
            castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade2));

        Assert.Equal(castle.GetRecruitLimit(MonsterType.Archer, Rich), castle.GetRecruitLimit(MonsterType.Ranger, Rich));
        Assert.True(castle.GetRecruitLimit(MonsterType.Ranger, Rich) > 0);
    }

    [Fact]
    public void RecruitingTakesTheMoneyAndTheCreatures()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);
        Funds treasury = Rich;
        Funds before = treasury;
        Funds one = MonsterStatistics.GetGeneralStats(MonsterType.Archer).GetCost();

        Assert.True(castle.RecruitMonster(new Troop(MonsterType.Archer, 4), ref treasury));

        Assert.Equal(4u, castle.Army.GetCount());
        Assert.True(castle.Army.HasMonster(MonsterType.Archer));
        Assert.Equal(4u, castle.Army.GetTroop(0).Count);
        Assert.Equal(shelf - 4, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));

        // Only what the creatures cost: four archers, gold only.
        Assert.Equal(before.Gold - (one.Gold * 4), treasury.Gold);
        Assert.Equal(before.Wood, treasury.Wood);
        Assert.Equal(before.Ore, treasury.Ore);
    }

    [Fact]
    public void TheGarrisonIsServedBeforeTheHero()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        ArmyTroops heroArmy = new();
        Funds treasury = Rich;

        Assert.True(castle.RecruitMonster(new Troop(MonsterType.Archer, 2), ref treasury, heroArmy));

        Assert.True(castle.Army.HasMonster(MonsterType.Archer));
        Assert.Equal(0u, heroArmy.GetCount());
    }

    [Fact]
    public void TheHeroGetsTheStackWhenTheGarrisonIsFull()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        // Five different creatures fill the garrison without filling any of them to the brim.
        castle.Army.JoinTroop(MonsterType.Peasant, 1);
        castle.Army.JoinTroop(MonsterType.Pikeman, 1);
        castle.Army.JoinTroop(MonsterType.Swordsman, 1);
        castle.Army.JoinTroop(MonsterType.Cavalry, 1);
        castle.Army.JoinTroop(MonsterType.Paladin, 1);

        Assert.Equal(5u, castle.Army.OccupiedSlotCount);

        ArmyTroops heroArmy = new();
        Funds treasury = Rich;

        Assert.True(castle.RecruitMonster(new Troop(MonsterType.Archer, 2), ref treasury, heroArmy));

        Assert.False(castle.Army.HasMonster(MonsterType.Archer));
        Assert.Equal(2u, heroArmy.GetTroop(0).Count);
        Assert.True(heroArmy.HasMonster(MonsterType.Archer));
    }

    [Fact]
    public void NoRoomAnywhereMeansNoSale()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        Fill(castle.Army);

        ArmyTroops heroArmy = new();

        Fill(heroArmy);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);
        Funds treasury = Rich;
        Funds before = treasury;

        Assert.False(castle.RecruitMonster(new Troop(MonsterType.Archer, 2), ref treasury, heroArmy));

        // Nothing was paid and nothing left the shelf.
        Assert.Equal(before, treasury);
        Assert.Equal(shelf, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void ShortOfMoneyMeansNoSale()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);
        Funds treasury = new Funds();
        Funds before = treasury;

        Assert.False(castle.RecruitMonster(new Troop(MonsterType.Archer, 2), ref treasury));

        Assert.Equal(before, treasury);
        Assert.Equal(shelf, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
        Assert.Equal(0u, castle.Army.GetCount());
    }

    [Fact]
    public void AskingForMoreThanTheShelfHoldsSellsWhatTheShelfHasAndGivesTheRestAway()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingMonster2);

        uint shelf = castle.GetMonstersInDwelling(BuildingType.DwellingMonster2);
        Funds treasury = Rich;
        Funds before = treasury;
        Funds one = MonsterStatistics.GetGeneralStats(MonsterType.Archer).GetCost();

        // The reference clamps only the shelf, not the price and not the stack that joins: the caller (the recruit
        // dialog) is what keeps the count honest. The port reproduces the asymmetry on purpose so that a dialog
        // ported later behaves like the original one.
        Assert.True(castle.RecruitMonster(new Troop(MonsterType.Archer, shelf + 5), ref treasury));

        Assert.Equal(0u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
        Assert.Equal(shelf + 5, castle.Army.GetCount());
        Assert.Equal(before.Gold - (one.Gold * (int)(shelf + 5)), treasury.Gold);
    }

    [Fact]
    public void AnUpgradeNeedsTheTownToOwnTheUpgradedDwelling()
    {
        CastleTown castle = Town(MapRace.Knight);
        castle.Army.JoinTroop(MonsterType.Swordsman, 3);

        Funds treasury = Rich;
        Funds before = treasury;

        castle.Army.UpgradeTroops(castle, ref treasury);

        // The upgraded dwelling is not built, so the stack stays and nothing is paid.
        Assert.Equal(MonsterType.Swordsman, castle.Army.GetTroop(0).Monster);
        Assert.Equal(before, treasury);

        Build(castle, BuildingType.DwellingUpgrade4);

        Funds price = new Troop(MonsterType.Swordsman, 3).GetTotalUpgradeCost();

        castle.Army.UpgradeTroops(castle, ref treasury);

        Assert.Equal(MonsterType.MasterSwordsman, castle.Army.GetTroop(0).Monster);
        Assert.Equal(3u, castle.Army.GetTroop(0).Count);
        Assert.Equal(before.Gold - price.Gold, treasury.Gold);
    }

    [Fact]
    public void AnUpgradeIsRefusedByATownOfAnotherRace()
    {
        CastleTown castle = Town(MapRace.Necromancer);
        castle.Army.JoinTroop(MonsterType.Swordsman, 3);
        Build(castle, BuildingType.DwellingUpgrade4);

        Funds treasury = Rich;
        Funds before = treasury;

        castle.Army.UpgradeTroops(castle, ref treasury);

        Assert.Equal(MonsterType.Swordsman, castle.Army.GetTroop(0).Monster);
        Assert.Equal(before, treasury);
    }

    [Fact]
    public void EachUpgradeIsPaidForOnItsOwn()
    {
        CastleTown castle = Town(MapRace.Knight);
        Build(castle, BuildingType.DwellingUpgrade2);
        Build(castle, BuildingType.DwellingUpgrade4);

        castle.Army.JoinTroop(MonsterType.Swordsman, 2);
        castle.Army.JoinTroop(MonsterType.Archer, 2);

        Funds first = new Troop(MonsterType.Swordsman, 2).GetTotalUpgradeCost();
        Funds treasury = first;

        castle.Army.UpgradeTroops(castle, ref treasury);

        // The swordsmen were upgraded and the money is gone; the archers are untouched and still paid for later.
        Assert.Equal(MonsterType.MasterSwordsman, castle.Army.GetTroop(0).Monster);
        Assert.Equal(MonsterType.Archer, castle.Army.GetTroop(1).Monster);
        Assert.Equal(0, treasury.Gold);
    }

    [Fact]
    public void EveryDwellingShelvesTheCreatureItsOwnShelfNames()
    {
        foreach (MapRace race in new[]
        {
            MapRace.Knight, MapRace.Barbarian, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard, MapRace.Necromancer,
        })
        {
            foreach (BuildingType dwelling in Enum.GetValues<BuildingType>())
            {
                if (!CastleBuildings.IsDwelling(dwelling) || dwelling == BuildingType.DwellingMonsters)
                {
                    continue;
                }

                MonsterType monster = DwellingMonsters.FromDwelling(race, dwelling);

                Assert.True(monster != MonsterType.Unknown, $"{race} {dwelling} should hold a creature");

                // The reverse table answers by creature alone, so a town without an upgrade for it still answers
                // the base dwelling — the shelves are what must line up, not the bits.
                Assert.True(
                    ShelfOf(DwellingMonsters.GetDwelling(monster)) == ShelfOf(dwelling),
                    $"{race} {dwelling} holds {monster}, whose dwelling is {DwellingMonsters.GetDwelling(monster)}");
            }
        }
    }

    [Fact]
    public void AStackKnowsTheDwellingItComesFrom()
    {
        Assert.Equal(BuildingType.DwellingMonster1, new Troop(MonsterType.Peasant, 1).GetDwelling());
        Assert.Equal(BuildingType.DwellingUpgrade2, new Troop(MonsterType.Ranger, 1).GetDwelling());
        Assert.Equal(BuildingType.DwellingUpgrade7, new Troop(MonsterType.BlackDragon, 1).GetDwelling());
        Assert.Equal(BuildingType.DwellingMonster6, new Troop(MonsterType.BoneDragon, 1).GetDwelling());

        // The dragons' two-step chain ends at two different dwellings, which is what makes the black dragon's
        // lair a separate building.
        Assert.Equal(BuildingType.DwellingUpgrade6, new Troop(MonsterType.RedDragon, 1).GetDwelling());
        Assert.Equal(BuildingType.DwellingMonster6, new Troop(MonsterType.GreenDragon, 1).GetDwelling());

        // Neutrals live nowhere.
        Assert.Equal(BuildingType.Nothing, new Troop(MonsterType.Genie, 1).GetDwelling());
        Assert.Equal(BuildingType.Nothing, new Troop().GetDwelling());
    }

    /// <summary>
    /// A town that already has its castle and its building permit, so that a test can start from the interesting
    /// part.
    /// </summary>
    /// <param name="race">The race of the town.</param>
    /// <returns>The town, with the castle built.</returns>
    private static CastleTown Town(MapRace race)
    {
        CastleTown castle = new(0, race, MapVersion.PriceOfLoyalty) { HasSeaAccess = true };
        Funds treasury = Rich;

        Assert.True(castle.BuyBuilding(BuildingType.Castle, ref treasury));

        castle.ActionNewDay();

        return castle;
    }

    /// <summary>
    /// Buys one building and everything it needs, then hands the building permit back.
    /// </summary>
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

    /// <summary>Fills every slot of an army with a different creature.</summary>
    /// <param name="army">The army to fill.</param>
    private static void Fill(ArmyTroops army)
    {
        foreach (MonsterType monster in new[]
        {
            MonsterType.Peasant, MonsterType.Pikeman, MonsterType.Swordsman, MonsterType.Cavalry, MonsterType.Paladin,
        })
        {
            Assert.True(army.JoinTroop(monster, 1));
        }
    }

    /// <summary>
    /// Which of the six shelves a dwelling or an upgrade uses — the same mapping as the town's own.
    /// </summary>
    /// <param name="dwelling">A dwelling bit.</param>
    /// <returns>The shelf index, or <c>-1</c>.</returns>
    private static int ShelfOf(BuildingType dwelling) => dwelling switch
    {
        BuildingType.DwellingMonster1 => 0,
        BuildingType.DwellingMonster2 or BuildingType.DwellingUpgrade2 => 1,
        BuildingType.DwellingMonster3 or BuildingType.DwellingUpgrade3 => 2,
        BuildingType.DwellingMonster4 or BuildingType.DwellingUpgrade4 => 3,
        BuildingType.DwellingMonster5 or BuildingType.DwellingUpgrade5 => 4,
        BuildingType.DwellingMonster6 or BuildingType.DwellingUpgrade6 or BuildingType.DwellingUpgrade7 => 5,
        _ => -1,
    };

    /// <summary>The buildings, without the three masks <see cref="BuildingType"/> throws in for convenience.</summary>
    /// <returns>Every building whose value has one bit.</returns>
    private static IEnumerable<BuildingType> SingleBuildings() =>
        Enum.GetValues<BuildingType>().Where(building => BitOperations.PopCount((uint)building) == 1);
}
