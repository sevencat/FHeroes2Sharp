using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the building tables — <see cref="CastleBuildings.GetCost"/> (<c>buildinginfo.cpp:203-212</c>),
/// <see cref="CastleBuildings.GetRequirement"/> (<c>castle_building_info.cpp:1097-1350</c>),
/// <see cref="CastleBuildings.GetUpgradeForBuilding"/> (<c>castle_building_info.cpp:980-1095</c>) and
/// <see cref="CastleBuildings.IsDwelling"/> (<c>buildinginfo.cpp:253-273</c>).
/// </summary>
/// <remarks>
/// The tables are 86 price rows and 60-odd dependency rows of transcribed numbers, so most of these tests sweep
/// rather than point: "every dwelling a town can reach is priced, and nothing else is", "every prerequisite is
/// itself buildable in that town", "the castle's upgrade chain and the monster's agree". Spot checks are reserved
/// for the handful of numbers a reader would want to see written down.
/// </remarks>
public sealed class CastleBuildingsTests
{
    private static readonly MapRace[] TownRaces =
    [
        MapRace.Knight, MapRace.Barbarian, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard, MapRace.Necromancer
    ];

    /// <summary>The buildings every town has, at the same price, <c>Race::ALL</c> rows.</summary>
    private static readonly BuildingType[] SharedBuildings =
    [
        BuildingType.ThievesGuild, BuildingType.Tavern, BuildingType.Shipyard, BuildingType.Well,
        BuildingType.Statue, BuildingType.LeftTurret, BuildingType.RightTurret, BuildingType.Marketplace,
        BuildingType.Moat, BuildingType.Castle, BuildingType.Captain, BuildingType.MageGuild1,
        BuildingType.MageGuild2, BuildingType.MageGuild3, BuildingType.MageGuild4, BuildingType.MageGuild5
    ];

    private static readonly BuildingType[] Dwellings =
    [
        BuildingType.DwellingMonster1, BuildingType.DwellingMonster2, BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4, BuildingType.DwellingMonster5, BuildingType.DwellingMonster6,
        BuildingType.DwellingUpgrade2, BuildingType.DwellingUpgrade3, BuildingType.DwellingUpgrade4,
        BuildingType.DwellingUpgrade5, BuildingType.DwellingUpgrade6, BuildingType.DwellingUpgrade7
    ];

    private static IEnumerable<BuildingType> SingleBuildings()
    {
        foreach (BuildingType building in Enum.GetValues<BuildingType>())
        {
            uint value = (uint)building;

            if (value != 0 && (value & (value - 1)) == 0)
            {
                yield return building;
            }
        }
    }

    private static IEnumerable<BuildingType> Bits(BuildingType mask)
    {
        foreach (BuildingType building in SingleBuildings())
        {
            if ((mask & building) != 0)
            {
                yield return building;
            }
        }
    }

    /// <summary>The dwellings a town can build, walking the upgrade chain from the six base dwellings.</summary>
    /// <param name="race">The town.</param>
    /// <returns>The reachable dwellings.</returns>
    private static HashSet<BuildingType> ReachableDwellings(MapRace race)
    {
        HashSet<BuildingType> reachable = [];

        for (int level = 1; level <= 6; ++level)
        {
            BuildingType dwelling = (BuildingType)((uint)BuildingType.DwellingMonster1 << (level - 1));

            while (reachable.Add(dwelling))
            {
                dwelling = CastleBuildings.GetUpgradeForBuilding(race, dwelling);
            }
        }

        return reachable;
    }

    [Fact]
    public void TheCastleCostsFiveThousandGoldTwentyWoodAndTwentyOre()
    {
        Assert.Equal(
            new Funds(20, 20, 0, 0, 0, 0, 5000),
            CastleBuildings.GetCost(BuildingType.Castle, MapRace.Knight));
    }

    [Fact]
    public void TheSpecialBuildingIsWhereTheTownsDiffer()
    {
        Assert.Equal(new Funds(15, 5, 0, 0, 0, 0, 1500), CastleBuildings.GetCost(BuildingType.Spec, MapRace.Knight));
        Assert.Equal(new Funds(10, 10, 0, 0, 0, 0, 2000), CastleBuildings.GetCost(BuildingType.Spec, MapRace.Barbarian));
        Assert.Equal(new Funds(0, 0, 0, 0, 10, 0, 1500), CastleBuildings.GetCost(BuildingType.Spec, MapRace.Sorceress));
        Assert.Equal(new Funds(10, 5, 0, 0, 0, 0, 3000), CastleBuildings.GetCost(BuildingType.Spec, MapRace.Warlock));
        Assert.Equal(new Funds(5, 5, 5, 5, 5, 5, 1500), CastleBuildings.GetCost(BuildingType.Spec, MapRace.Wizard));
        Assert.Equal(new Funds(0, 0, 10, 10, 0, 0, 1000), CastleBuildings.GetCost(BuildingType.Spec, MapRace.Necromancer));
    }

    [Fact]
    public void TheShrineCostsFourThousandGoldTenWoodAndTenCrystal()
    {
        // The necromancer's stand-in for the tavern; the other five towns never build it.
        Assert.Equal(
            new Funds(0, 10, 0, 0, 10, 0, 4000),
            CastleBuildings.GetCost(BuildingType.Shrine, MapRace.Necromancer));

        foreach (MapRace race in TownRaces)
        {
            if (race != MapRace.Necromancer)
            {
                Assert.Equal(new Funds(), CastleBuildings.GetCost(BuildingType.Shrine, race));
            }
        }
    }

    [Fact]
    public void TheBuildingsEveryTownSharesCostTheSameEverywhere()
    {
        foreach (BuildingType building in SharedBuildings)
        {
            Funds first = CastleBuildings.GetCost(building, MapRace.Knight);

            Assert.True(first > new Funds(), $"{building} is free");

            foreach (MapRace race in TownRaces)
            {
                Assert.Equal(first, CastleBuildings.GetCost(building, race));
            }
        }
    }

    [Fact]
    public void EveryDwellingATownCanReachIsPricedAndNothingElseIs()
    {
        foreach (MapRace race in TownRaces)
        {
            HashSet<BuildingType> reachable = ReachableDwellings(race);

            foreach (BuildingType dwelling in Dwellings)
            {
                Funds cost = CastleBuildings.GetCost(dwelling, race);

                if (reachable.Contains(dwelling))
                {
                    Assert.True(cost > new Funds(), $"{race} cannot afford {dwelling}: it has no price");
                }
                else
                {
                    Assert.Equal(new Funds(), cost);
                }
            }
        }
    }

    [Fact]
    public void TheUpgradeChainEndsAtABuildingThatUpgradesToItself()
    {
        foreach (MapRace race in TownRaces)
        {
            foreach (BuildingType building in SingleBuildings())
            {
                BuildingType current = building;
                int steps = 0;

                while (true)
                {
                    BuildingType next = CastleBuildings.GetUpgradeForBuilding(race, current);
                    Assert.True(++steps <= 16, $"{race} {building} upgrade chain does not terminate");

                    if (next == current)
                    {
                        break;
                    }

                    current = next;
                }
            }
        }
    }

    [Fact]
    public void TheMageGuildIsUpgradedOneLevelAtATimeAndTheTentBecomesACastle()
    {
        Assert.Equal(BuildingType.Castle, CastleBuildings.GetUpgradeForBuilding(MapRace.Knight, BuildingType.Tent));
        Assert.Equal(BuildingType.MageGuild2, CastleBuildings.GetUpgradeForBuilding(MapRace.Warlock, BuildingType.MageGuild1));
        Assert.Equal(BuildingType.MageGuild3, CastleBuildings.GetUpgradeForBuilding(MapRace.Warlock, BuildingType.MageGuild2));
        Assert.Equal(BuildingType.MageGuild4, CastleBuildings.GetUpgradeForBuilding(MapRace.Warlock, BuildingType.MageGuild3));
        Assert.Equal(BuildingType.MageGuild5, CastleBuildings.GetUpgradeForBuilding(MapRace.Warlock, BuildingType.MageGuild4));
        Assert.Equal(BuildingType.MageGuild5, CastleBuildings.GetUpgradeForBuilding(MapRace.Warlock, BuildingType.MageGuild5));
    }

    [Fact]
    public void TheBlackDragonLairIsTheOnlyUpgradeThatNeedsAnotherUpgrade()
    {
        Assert.Equal(
            BuildingType.DwellingUpgrade6,
            CastleBuildings.GetRequirement(MapRace.Warlock, BuildingType.DwellingUpgrade7));

        Assert.Equal(
            BuildingType.DwellingUpgrade7,
            CastleBuildings.GetUpgradeForBuilding(MapRace.Warlock, BuildingType.DwellingUpgrade6));

        // And the lair itself is the end of that line.
        Assert.Equal(
            BuildingType.DwellingUpgrade7,
            CastleBuildings.GetUpgradeForBuilding(MapRace.Warlock, BuildingType.DwellingUpgrade7));

        // No other town has a second dragon lair, so nobody else may build it.
        foreach (MapRace race in TownRaces)
        {
            if (race != MapRace.Warlock)
            {
                Assert.Equal(new Funds(), CastleBuildings.GetCost(BuildingType.DwellingUpgrade7, race));
            }
        }
    }

    [Fact]
    public void TheArchersNeedOnlyThePeasantHutExceptInTheSorceressTown()
    {
        Assert.Equal(
            BuildingType.DwellingMonster1 | BuildingType.Tavern,
            CastleBuildings.GetRequirement(MapRace.Sorceress, BuildingType.DwellingMonster2));

        foreach (MapRace race in TownRaces)
        {
            if (race != MapRace.Sorceress)
            {
                Assert.Equal(
                    BuildingType.DwellingMonster1,
                    CastleBuildings.GetRequirement(race, BuildingType.DwellingMonster2));
            }
        }

        // A random town has not been given a race yet, so the table answers "nothing".
        Assert.Equal(BuildingType.Nothing, CastleBuildings.GetRequirement(MapRace.Random, BuildingType.DwellingMonster2));
    }

    [Fact]
    public void OnlyTheWizardLibraryNeedsTheMageGuild()
    {
        Assert.Equal(
            BuildingType.MageGuild1,
            CastleBuildings.GetRequirement(MapRace.Wizard, BuildingType.Spec));

        foreach (MapRace race in TownRaces)
        {
            if (race != MapRace.Wizard)
            {
                Assert.Equal(BuildingType.Nothing, CastleBuildings.GetRequirement(race, BuildingType.Spec));
            }
        }
    }

    [Fact]
    public void TheBuildingsEveryTownSharesNeedNothing()
    {
        foreach (BuildingType building in SharedBuildings)
        {
            // The captain and the turrets need a castle, but the castle checks that itself — the requirement
            // table answers for the dwellings and the wizard's library only.
            foreach (MapRace race in TownRaces)
            {
                Assert.Equal(BuildingType.Nothing, CastleBuildings.GetRequirement(race, building));
            }
        }
    }

    [Fact]
    public void EveryPrerequisiteIsItselfBuildableInThatTown()
    {
        foreach (MapRace race in TownRaces)
        {
            foreach (BuildingType building in SingleBuildings())
            {
                BuildingType requirement = CastleBuildings.GetRequirement(race, building);

                Assert.Equal(BuildingType.Nothing, requirement & building);

                foreach (BuildingType needed in Bits(requirement))
                {
                    Assert.True(
                        CastleBuildings.GetCost(needed, race) > new Funds(),
                        $"{race} needs {needed} for {building} but cannot build it");
                }
            }
        }
    }

    [Fact]
    public void TheCastleUpgradesAndTheMonsterUpgradesAgree()
    {
        int upgrades = 0;

        foreach (MapRace race in TownRaces)
        {
            foreach (BuildingType dwelling in ReachableDwellings(race))
            {
                BuildingType upgradedDwelling = CastleBuildings.GetUpgradeForBuilding(race, dwelling);

                if (upgradedDwelling == dwelling)
                {
                    continue;
                }

                ++upgrades;

                // The two tables are the same fact seen from two sides: the dwelling's upgrade houses the
                // upgrade of the monster the base dwelling houses.
                Assert.Equal(
                    MonsterUpgrades.GetUpgrade(DwellingMonsters.FromDwelling(race, dwelling)),
                    DwellingMonsters.FromDwelling(race, upgradedDwelling));
            }
        }

        // Twenty-one, the same number as the monster upgrade table.
        Assert.Equal(21, upgrades);
    }

    [Fact]
    public void ExactlyTwelveValuesAreDwellings()
    {
        foreach (BuildingType dwelling in Dwellings)
        {
            Assert.True(CastleBuildings.IsDwelling(dwelling), $"{dwelling} is a dwelling");
        }

        // A mask is not a dwelling, and neither is anything else.
        BuildingType[] notDwellings =
        [
            BuildingType.Nothing, BuildingType.Tavern, BuildingType.Castle, BuildingType.Spec,
            BuildingType.Well2, BuildingType.MageGuild1, BuildingType.Tent, BuildingType.DwellingMonsters,
            BuildingType.DwellingUpgrades
        ];

        foreach (BuildingType building in notDwellings)
        {
            Assert.False(CastleBuildings.IsDwelling(building), $"{building} is not a dwelling");
        }

        Assert.Equal(12, Dwellings.Count(CastleBuildings.IsDwelling));
    }
}
