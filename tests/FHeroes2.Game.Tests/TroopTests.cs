using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Troop</c> of <c>src/fheroes2/army/army_troop.h</c> / <c>army_troop.cpp</c> as ported by
/// <see cref="Troop"/>: the "a monster and a pile of it" slot of an army.
/// </summary>
/// <remarks>
/// The reference derives <c>Troop</c> from <c>Monster</c>, so every stat asks the monster. The port keeps the
/// stats in <see cref="MonsterStatistics"/> and only the count here, which is why most of these tests are
/// about "per creature × count" and about the empty slot falling back to the unknown row.
/// </remarks>
public sealed class TroopTests
{
    [Fact]
    public void ANewTroopIsAnEmptySlot()
    {
        var troop = new Troop();

        Assert.Equal(MonsterType.Unknown, troop.Monster);
        Assert.Equal(0u, troop.Count);
        Assert.True(troop.IsEmpty);
        Assert.False(troop.IsValid);
    }

    [Fact]
    public void AStackOfZeroCreaturesIsAnEmptySlot()
    {
        var troop = new Troop(MonsterType.Peasant, 0);

        // The monster is known, but a stack of nobody is not a stack: the reference's isValid() asks both.
        Assert.Equal(MonsterType.Peasant, troop.Monster);
        Assert.True(troop.IsEmpty);
        Assert.False(troop.IsValid);
    }

    [Fact]
    public void AStackOfCreaturesIsValid()
    {
        var troop = new Troop(MonsterType.Peasant, 1);

        Assert.True(troop.IsValid);
        Assert.False(troop.IsEmpty);
        Assert.True(troop.IsMonster(MonsterType.Peasant));
        Assert.False(troop.IsMonster(MonsterType.Archer));
    }

    [Fact]
    public void AnEmptySlotAnswersFromTheUnknownRow()
    {
        var troop = new Troop();

        // monster_info.cpp's Monster::UNKNOWN row is all zeroes except the speed, which is VERYSLOW. The port
        // keeps the row as-is so that a caller that forgets IsValid gets zeroes instead of an exception.
        Assert.Equal(0u, troop.GetHitPoints());
        Assert.Equal(0u, troop.GetDamageMin());
        Assert.Equal(0u, troop.GetDamageMax());
        Assert.Equal(0, troop.MonsterLevel);
        Assert.Equal(MonsterSpeed.VerySlow, troop.GetSpeed());
        Assert.False(troop.IsArchers);
        Assert.Equal(0, troop.GetTotalCost().Gold);
    }

    [Fact]
    public void HitPointsArePerCreatureTimesCount()
    {
        // Archer: 10 hit points each.
        var troop = new Troop(MonsterType.Archer, 3);

        Assert.Equal(30u, troop.GetHitPoints());
    }

    [Fact]
    public void DamageIsPerCreatureTimesCount()
    {
        // Archer: 2-3 damage each.
        var troop = new Troop(MonsterType.Archer, 4);

        Assert.Equal(8u, troop.GetDamageMin());
        Assert.Equal(12u, troop.GetDamageMax());
    }

    [Fact]
    public void TheCostIsPerCreatureTimesCount()
    {
        // Peasant: 20 gold each, nothing else.
        var troop = new Troop(MonsterType.Peasant, 3);

        Assert.Equal(20, troop.GetCost().Gold);
        Assert.Equal(60, troop.GetTotalCost().Gold);
        Assert.Equal(0, troop.GetTotalCost().Wood);
    }

    [Fact]
    public void AnArcherKnowsItShoots()
    {
        Assert.True(new Troop(MonsterType.Archer, 1).IsArchers);
        Assert.False(new Troop(MonsterType.Pikeman, 1).IsArchers);
    }

    [Fact]
    public void TheLevelComesFromTheGeneralRow()
    {
        Assert.Equal(1, new Troop(MonsterType.Peasant, 1).MonsterLevel);
        Assert.Equal(6, new Troop(MonsterType.Paladin, 1).MonsterLevel);
    }

    [Fact]
    public void SetCopiesAnotherStack()
    {
        var source = new Troop(MonsterType.Champion, 7);
        var target = new Troop();

        target.Set(source);

        Assert.Equal(MonsterType.Champion, target.Monster);
        Assert.Equal(7u, target.Count);
        Assert.NotSame(source, target);
    }

    [Fact]
    public void SetFillsTheSlotWithoutCopying()
    {
        var troop = new Troop();

        troop.Set(MonsterType.Goblin, 12);

        Assert.Equal(MonsterType.Goblin, troop.Monster);
        Assert.Equal(12u, troop.Count);
        Assert.True(troop.IsValid);
    }

    [Fact]
    public void SetRefusesNull()
    {
        var troop = new Troop();

        Assert.Throws<ArgumentNullException>(() => troop.Set(null!));
    }

    [Fact]
    public void ResetEmptiesTheSlot()
    {
        var troop = new Troop(MonsterType.Goblin, 12);

        troop.Reset();

        Assert.Equal(MonsterType.Unknown, troop.Monster);
        Assert.Equal(0u, troop.Count);
        Assert.True(troop.IsEmpty);
    }

    [Fact]
    public void OnlyRealCreaturesAreValidMonsters()
    {
        // isValid() rejects the unknown monster and the five editor-only random markers.
        Assert.False(MonsterStatistics.IsValid(MonsterType.Unknown));
        Assert.False(MonsterStatistics.IsValid(MonsterType.RandomMonster));
        Assert.False(MonsterStatistics.IsValid(MonsterType.RandomMonsterLevel4));
        Assert.True(MonsterStatistics.IsValid(MonsterType.Peasant));
        Assert.True(MonsterStatistics.IsValid(MonsterType.Phoenix));
    }
}
