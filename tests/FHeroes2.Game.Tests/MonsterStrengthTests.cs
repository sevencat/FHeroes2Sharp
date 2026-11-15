using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using Xunit;
using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the strength chain of <c>monster_info.cpp:45</c> (<c>getMonsterBaseStrength</c>),
/// <c>monster.cpp:105</c> (<c>Monster::GetMonsterStrength</c>), <c>army_troop.cpp:52/57</c>
/// (<c>Troop::GetStrength</c> / <c>GetStrengthWithBonus</c>) and <c>army.cpp:1291</c> (<c>Army::GetStrength</c>).
/// </summary>
/// <remarks>
/// The expected numbers are not read back from the port: they come from an independent transcription of the
/// reference formula over the reference's own stats and ability tables, so a missing factor or a swapped row
/// shows up as a wrong number rather than as a matching one.
/// </remarks>
public class MonsterStrengthTests
{
    [Theory]
    [InlineData(MonsterType.Peasant, 0.8)]
    [InlineData(MonsterType.Archer, 6.0)]
    [InlineData(MonsterType.Ranger, 9.8994949366117)]
    [InlineData(MonsterType.Dwarf, 6.1967733539319)]
    [InlineData(MonsterType.Ghost, 33.5)]
    [InlineData(MonsterType.Cyclops, 51.9615242270663)]
    [InlineData(MonsterType.Medusa, 20.0798406367722)]
    [InlineData(MonsterType.Phoenix, 87.0)]
    [InlineData(MonsterType.Skeleton, 3.1622776601684)]
    public void TheBaseStrengthIsTheStatAndAbilityFold(MonsterType monster, double expected)
    {
        Assert.Equal(expected, MonsterStatistics.GetMonsterBaseStrength(monster), 6);
    }

    [Theory]
    [InlineData(MonsterType.Peasant, 0.92)]
    [InlineData(MonsterType.Archer, 9.9)]
    [InlineData(MonsterType.Ranger, 16.3341666454222)]
    [InlineData(MonsterType.Dwarf, 11.464030704774)]
    [InlineData(MonsterType.Crusader, 131.560859372155)]
    [InlineData(MonsterType.Ghost, 72.025)]
    [InlineData(MonsterType.GreenDragon, 345.320720490282)]
    [InlineData(MonsterType.Lich, 57.0372756404685)]
    [InlineData(MonsterType.Titan, 450.333209967888)]
    public void TheStrengthIsTheBaseStrengthTimesAttackAndDefense(MonsterType monster, double expected)
    {
        Assert.Equal(expected, MonsterStatistics.GetMonsterStrength(monster), 6);
    }

    [Fact]
    public void TheAttackAndDefenseFactorIsOnePlusTenAndFivePercent()
    {
        // 1 + attack * 0.1 + defense * 0.05, applied to the base strength: a peasant with 11 attack and 1
        // defense is worth 1 + 1.1 + 0.05 = 2.15 times its base.
        Assert.Equal(0.8 * 2.15, MonsterStatistics.GetMonsterStrength(MonsterType.Peasant, 11, 1), 9);

        // Handing the creature's own stats in is the same as not handing anything in.
        MonsterBattleStats stats = MonsterStatistics.GetBattleStats(MonsterType.GreenDragon);
        Assert.Equal(
            MonsterStatistics.GetMonsterStrength(MonsterType.GreenDragon),
            MonsterStatistics.GetMonsterStrength(MonsterType.GreenDragon, stats.Attack, stats.Defense),
            9);
    }

    [Fact]
    public void TheSpeedShiftsTheSpecialFactor()
    {
        // Speed is remapped to -0.2...+0.15 around AVERAGE: a peasant (VERYSLOW, two steps down) loses 0.2,
        // the ghost (FAST, one step up) gains 0.05 on top of flying 0.3 and soul eater 2.
        // sqrt(5 * 20) = 10, so the ghost is worth 10 * 3.35.
        Assert.Equal(10 * 3.35, MonsterStatistics.GetMonsterBaseStrength(MonsterType.Ghost), 9);

        // The ghost is the loudest case of the asymmetry: +1 step of speed is worth half of what one step
        // below AVERAGE costs.
        Assert.Equal(0.8, MonsterStatistics.GetMonsterBaseStrength(MonsterType.Peasant), 9);
    }

    [Fact]
    public void TheSpellCastersWeightTheirSpellByTheChance()
    {
        // Paralyze, blind and petrify are worth the full chance: the cyclops casts paralyze with 20% (+0.2).
        Assert.Equal(51.9615242270663, MonsterStatistics.GetMonsterBaseStrength(MonsterType.Cyclops), 6);
        Assert.Equal(20.0798406367722, MonsterStatistics.GetMonsterBaseStrength(MonsterType.Medusa), 6);

        // Dispel and curse are worth a tenth of it: the archmage dispels at 20% (+0.02), the royal mummy
        // curses at 30% (+0.03) while the plain mummy only curses at 20% (+0.02).
        Assert.Equal(27.1077848596808, MonsterStatistics.GetMonsterBaseStrength(MonsterType.Archmage), 6);
        Assert.Equal(11.0667068271955, MonsterStatistics.GetMonsterBaseStrength(MonsterType.RoyalMummy), 6);
        Assert.Equal(9.5412263363448, MonsterStatistics.GetMonsterBaseStrength(MonsterType.Mummy), 6);
    }

    [Fact]
    public void TheTableHasNoSwappedRows()
    {
        // The peasant is the weakest creature and the black dragon the strongest. A row that slid one position
        // would break at least one of these.
        Assert.Equal(MonsterType.Peasant, FindExtreme(best: false));
        Assert.Equal(MonsterType.BlackDragon, FindExtreme(best: true));
    }

    private static MonsterType FindExtreme(bool best)
    {
        MonsterType found = MonsterType.Unknown;
        double value = best ? double.MinValue : double.MaxValue;

        foreach (MonsterType monster in Enum.GetValues<MonsterType>().Distinct())
        {
            if (!MonsterStatistics.IsValid(monster))
            {
                continue;
            }

            double strength = MonsterStatistics.GetMonsterStrength(monster);

            if (best ? strength > value : strength < value)
            {
                value = strength;
                found = monster;
            }
        }

        return found;
    }

    [Fact]
    public void TheUnknownAndTheRandomMonstersAreWorthNothing()
    {
        Assert.Equal(0.0, MonsterStatistics.GetMonsterStrength(MonsterType.Unknown));
        Assert.Equal(0.0, MonsterStatistics.GetMonsterStrength(MonsterType.RandomMonster));
        Assert.Equal(0.0, MonsterStatistics.GetMonsterStrength(MonsterType.RandomMonsterLevel4));
    }

    [Fact]
    public void ATroopIsWorthItsCount()
    {
        Assert.Equal(9.9 * 3, new Troop(MonsterType.Archer, 3).GetStrength(), 9);
        Assert.Equal(0.0, new Troop(MonsterType.Archer, 0).GetStrength(), 9);
        Assert.Equal(0.0, new Troop().GetStrength(), 9);
    }

    [Fact]
    public void TheBonusIsAddedToTheCreatureStatsBeforeTheFold()
    {
        // Archer: attack 5, defense 3. With +1 attack the factor goes from 1.65 to 1.75, so the stack is
        // worth 6.0 * 1.75 instead of 6.0 * 1.65.
        Assert.Equal(10.5 * 3, new Troop(MonsterType.Archer, 3).GetStrengthWithBonus(1, 0), 9);

        // Peasant: attack 1, defense 1. +1 attack is 1.25 / 1.15 of the unbuffed value.
        Assert.Equal(1.0, new Troop(MonsterType.Peasant, 1).GetStrengthWithBonus(1, 0), 9);

        // A bonus of zero has to give exactly the plain strength back.
        Assert.Equal(
            new Troop(MonsterType.Crusader, 4).GetStrength(),
            new Troop(MonsterType.Crusader, 4).GetStrengthWithBonus(0, 0),
            9);
    }

    [Fact]
    public void AnEmptyArmyIsWorthNothing()
    {
        Assert.Equal(0.0, new ArmyStack().GetStrength());
    }

    [Fact]
    public void TheArmyStrengthAppliesItsOwnMorale()
    {
        // One race of two different monsters: +1 morale, and both stacks are affected by it, so every stack is
        // worth 1 + 1/24 more.
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Peasant, 1);
        army.JoinTroop(MonsterType.Archer, 1);

        Assert.Equal(Morale.Good, army.GetMorale());
        Assert.Equal((0.92 + 9.9) * (1 + 1 / 24.0), army.GetStrength(), 9);
    }

    [Fact]
    public void TheUndeadRanksFallOutsideTheMoraleFactor()
    {
        // An archer next to a skeleton: two races plus one for the undead, so the morale is -1. The skeleton
        // ignores morale entirely, the archer loses a twelfth of its strength.
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Archer, 1);
        army.JoinTroop(MonsterType.Skeleton, 1);

        Assert.Equal(Morale.Poor, army.GetMorale());
        Assert.Equal(9.9 * (1 - 1 / 12.0) + 4.9015303733, army.GetStrength(), 6);
    }

    [Fact]
    public void TheLuckFactorIsNeutralWithoutAHero()
    {
        // Luck only ever comes from a hero, from an artifact or from a visited object, so an army on its own
        // always has the neutral factor of 1 + 0/24.
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Archer, 2);

        Assert.Equal(Luck.Normal, army.GetLuck());
        Assert.Equal(2 * 9.9, army.GetStrength(), 9);
    }
}
