using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the upgrade relation — <c>Monster::GetUpgrade()</c> (<c>monster.cpp:285-336</c>),
/// <c>GetDowngrade()</c> (<c>monster.cpp:232-283</c>), <c>GetUpgradeCost()</c> (<c>monster.cpp:830-838</c>) —
/// and the two things built on it: <c>Monster::Upgrade()</c> and <c>Troops::UpgradeMonsters()</c>
/// (<c>army.cpp:324-332</c>).
/// </summary>
/// <remarks>
/// Two of the tests are table-wide rather than point checks: every upgraded creature must keep its race and its
/// level, be stronger and cost more, and its downgrade must lead back. A typo in one of the twenty-one rows is
/// exactly the kind of mistake that a hand-written "archer becomes ranger" test would not catch, while an
/// invariant sweep does.
/// </remarks>
public sealed class MonsterUpgradeTests
{
    private static readonly MonsterType FirstRealMonster = MonsterType.Peasant;

    private static IEnumerable<MonsterType> RealMonsters()
    {
        for (int id = (int)FirstRealMonster; id < (int)MonsterType.RandomMonster; ++id)
        {
            yield return (MonsterType)id;
        }
    }

    private static ArmyStack NewArmy() => new();

    [Fact]
    public void TheDragonChainIsWalkedOneStepAtATime()
    {
        Assert.Equal(MonsterType.RedDragon, MonsterUpgrades.GetUpgrade(MonsterType.GreenDragon));
        Assert.Equal(MonsterType.BlackDragon, MonsterUpgrades.GetUpgrade(MonsterType.RedDragon));

        // The top of the chain answers itself, so the upgrade button of a black dragon does nothing rather
        // than wrapping around to a green one.
        Assert.Equal(MonsterType.BlackDragon, MonsterUpgrades.GetUpgrade(MonsterType.BlackDragon));
        Assert.True(MonsterUpgrades.IsAllowUpgrade(MonsterType.GreenDragon));
        Assert.True(MonsterUpgrades.IsAllowUpgrade(MonsterType.RedDragon));
        Assert.False(MonsterUpgrades.IsAllowUpgrade(MonsterType.BlackDragon));
    }

    [Fact]
    public void TwentyOneMonstersUpgrade()
    {
        Dictionary<MapRace, int> perRace = [];
        int total = 0;

        foreach (MonsterType monster in RealMonsters())
        {
            if (!MonsterUpgrades.IsAllowUpgrade(monster))
            {
                continue;
            }

            MapRace race = MonsterStatistics.GetGeneralStats(monster).Race;
            perRace[race] = perRace.GetValueOrDefault(race) + 1;
            ++total;
        }

        Assert.Equal(21, total);

        // Each race upgrades everything but its level-1 creature and its two specials — except the
        // necromancer, whose seventh unit (the bone dragon) has no upgrade either, so it keeps four.
        Assert.Equal(5, perRace[MapRace.Knight]);
        Assert.Equal(3, perRace[MapRace.Barbarian]);
        Assert.Equal(4, perRace[MapRace.Necromancer]);
        Assert.Equal(3, perRace[MapRace.Sorceress]);
        Assert.Equal(3, perRace[MapRace.Warlock]);
        Assert.Equal(3, perRace[MapRace.Wizard]);
    }

    [Fact]
    public void EveryUpgradeKeepsTheRaceTheLevelAndIsStronger()
    {
        foreach (MonsterType monster in RealMonsters())
        {
            MonsterType upgrade = MonsterUpgrades.GetUpgrade(monster);

            if (upgrade == monster)
            {
                continue;
            }

            MonsterGeneralStats before = MonsterStatistics.GetGeneralStats(monster);
            MonsterGeneralStats after = MonsterStatistics.GetGeneralStats(upgrade);

            Assert.Equal(before.Race, after.Race);
            Assert.Equal(before.Level, after.Level);

            Assert.True(
                MonsterStatistics.GetMonsterStrength(upgrade) > MonsterStatistics.GetMonsterStrength(monster),
                $"{upgrade} is not stronger than {monster}");
        }
    }

    [Fact]
    public void TheDowngradeLeadsBackToTheMonsterThatUpgraded()
    {
        foreach (MonsterType monster in RealMonsters())
        {
            MonsterType upgrade = MonsterUpgrades.GetUpgrade(monster);

            if (upgrade == monster)
            {
                continue;
            }

            // Downgrading an upgrade must give the original back, dragons included: red goes back to green,
            // black goes back to red.
            Assert.Equal(monster, MonsterUpgrades.GetDowngrade(upgrade));
        }
    }

    [Fact]
    public void AMonsterWithoutAnUpgradeUpgradesToItselfAndCostsNothing()
    {
        // The black dragon is the top of the dragon chain: it has no upgrade, but it does have a downgrade, so
        // it belongs in the first group only.
        MonsterType[] noUpgrade =
        [
            MonsterType.Peasant, MonsterType.Goblin, MonsterType.BlackDragon, MonsterType.BoneDragon,
            MonsterType.Unknown, MonsterType.RandomMonster, MonsterType.RandomMonsterLevel3
        ];

        foreach (MonsterType monster in noUpgrade)
        {
            Assert.Equal(monster, MonsterUpgrades.GetUpgrade(monster));
            Assert.False(MonsterUpgrades.IsAllowUpgrade(monster));
            Assert.Equal(new Funds(), MonsterUpgrades.GetUpgradeCost(monster));
        }

        // And only the ones that are not an upgrade of something else downgrade to themselves.
        MonsterType[] neutral =
        [
            MonsterType.Peasant, MonsterType.Goblin, MonsterType.BoneDragon, MonsterType.Unknown,
            MonsterType.RandomMonster, MonsterType.RandomMonsterLevel3
        ];

        foreach (MonsterType monster in neutral)
        {
            Assert.Equal(monster, MonsterUpgrades.GetDowngrade(monster));
        }

        Assert.Equal(MonsterType.RedDragon, MonsterUpgrades.GetDowngrade(MonsterType.BlackDragon));
    }

    [Fact]
    public void EveryUpgradableMonsterCostsSomethingToUpgrade()
    {
        foreach (MonsterType monster in RealMonsters())
        {
            Funds cost = MonsterUpgrades.GetUpgradeCost(monster);

            if (MonsterUpgrades.IsAllowUpgrade(monster))
            {
                Assert.True(cost > new Funds(), $"{monster} upgrades for free");
                continue;
            }

            Assert.Equal(new Funds(), cost);
        }
    }

    [Fact]
    public void TheUpgradeCostIsTwiceTheDifferenceInPrice()
    {
        // Archer 150 gold, ranger 200: the difference of 50, charged twice.
        Assert.Equal(new Funds(0, 0, 0, 0, 0, 0, 100), MonsterUpgrades.GetUpgradeCost(MonsterType.Archer));

        // Red dragon 3500 gold and 1 sulfur, black dragon 4000 gold and 2 sulfur: 500 gold and a sulfur,
        // charged twice.
        Assert.Equal(
            new Funds(0, 0, 0, 2, 0, 0, 1000),
            MonsterUpgrades.GetUpgradeCost(MonsterType.RedDragon));
    }

    [Fact]
    public void UpgradingAStackKeepsItsCount()
    {
        Troop troop = new(MonsterType.GreenDragon, 7);

        Assert.True(troop.IsAllowUpgrade);
        Assert.Equal(MonsterType.RedDragon, troop.GetUpgrade());

        troop.Upgrade();

        Assert.Equal(MonsterType.RedDragon, troop.Monster);
        Assert.Equal(7u, troop.Count);

        // And once more walks the chain to the top.
        troop.Upgrade();
        Assert.Equal(MonsterType.BlackDragon, troop.Monster);
        Assert.False(troop.IsAllowUpgrade);
    }

    [Fact]
    public void TheTotalUpgradeCostGrowsWithTheStack()
    {
        Troop troop = new(MonsterType.Archer, 5);

        Assert.Equal(new Funds(0, 0, 0, 0, 0, 0, 500), troop.GetTotalUpgradeCost());
        Assert.Equal(new Funds(0, 0, 0, 0, 0, 0, 100), troop.GetUpgradeCost());

        // An empty slot holds the unknown monster, which has no upgrade and therefore no price.
        Troop empty = new();

        Assert.Equal(new Funds(), empty.GetTotalUpgradeCost());
        Assert.False(empty.IsAllowUpgrade);
    }

    [Fact]
    public void UpgradingAnArmyTouchesOnlyTheStacksOfThatMonster()
    {
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Archer, 3);
        army.JoinTroop(MonsterType.Peasant, 5);
        army.JoinTroop(MonsterType.Archer, 2);

        army.UpgradeMonsters(MonsterType.Archer);

        // Both stacks of archers became rangers and kept their counts; the peasants are untouched, in the slot
        // the reference's UpgradeMonsters leaves them in.
        Assert.Equal(0u, army.GetCountMonsters(MonsterType.Archer));
        Assert.Equal(5u, army.GetCountMonsters(MonsterType.Ranger));
        Assert.Equal(5u, army.GetCountMonsters(MonsterType.Peasant));

        Assert.Equal(MonsterType.Ranger, army.Slots[0].Monster);
        Assert.Equal(3u, army.Slots[0].Count);
        Assert.Equal(MonsterType.Peasant, army.Slots[1].Monster);
        Assert.Equal(MonsterType.Ranger, army.Slots[2].Monster);
        Assert.Equal(2u, army.Slots[2].Count);
    }
}
