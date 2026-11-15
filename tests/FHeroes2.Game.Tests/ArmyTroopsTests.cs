using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the container half of <c>Army</c> — <c>Troops</c> of <c>army.cpp:324-700</c> — and the army-level
/// questions built on it: merging, optimizing, ordering, the reinforcement value, the strength comparison,
/// the melee test and the two repair helpers (<c>army.cpp:1563-1603</c>, <c>army.cpp:1758-1782</c>).
/// </summary>
/// <remarks>
/// The reference keeps <c>Troops</c> (a vector) and <c>Army</c> (five slots + commander) apart; the port has
/// one class, so every container operation happens on the five slots. That is why the tests here never look at
/// a <c>Troops</c> type — they look at <see cref="ArmyStack.Slots"/>.
/// </remarks>
public sealed class ArmyTroopsTests
{
    private static ArmyStack NewArmy() => new();

    private static double StrengthOf(MonsterType monster, uint count) =>
        MonsterStatistics.GetMonsterStrength(monster) * count;

    [Fact]
    public void MergingSumsEveryDuplicateIntoTheFirstSlot()
    {
        ArmyStack army = NewArmy();

        army.JoinTroop(MonsterType.Peasant, 5);
        army.JoinTroop(MonsterType.Archer, 3);
        army.JoinTroop(MonsterType.Peasant, 7);

        Assert.Equal(3u, army.OccupiedSlotCount);
        Assert.Equal(12u, army.GetCountMonsters(MonsterType.Peasant));

        army.MergeSameMonsterTroops();

        Assert.Equal(2u, army.OccupiedSlotCount);
        Assert.Equal(12u, army.Slots[0].Count);
        Assert.Equal(MonsterType.Peasant, army.Slots[0].Monster);
        Assert.Equal(3u, army.Slots[1].Count);
        Assert.Equal(MonsterType.Archer, army.Slots[1].Monster);
        Assert.True(army.Slots[2].IsEmpty);
    }

    [Fact]
    public void MergingOnceFreesOneSlotPerCall()
    {
        // This is the loop the reference's "make room" code runs: merge, look at the slot count, merge again.
        ArmyStack army = NewArmy();

        army.JoinTroop(MonsterType.Peasant, 5);
        army.JoinTroop(MonsterType.Peasant, 7);
        army.JoinTroop(MonsterType.Peasant, 3);

        Assert.Equal(3u, army.OccupiedSlotCount);

        Assert.True(army.MergeSameMonsterOnce());
        Assert.Equal(2u, army.OccupiedSlotCount);
        Assert.Equal(12u, army.Slots[0].Count);

        Assert.True(army.MergeSameMonsterOnce());
        Assert.Equal(1u, army.OccupiedSlotCount);
        Assert.Equal(15u, army.Slots[0].Count);

        Assert.False(army.MergeSameMonsterOnce());
        Assert.Equal(15u, army.Slots[0].Count);
    }

    [Fact]
    public void MergingOnceDoesNothingOnAnArmyWithoutDuplicates()
    {
        ArmyStack army = NewArmy();

        army.JoinTroop(MonsterType.Peasant, 5);
        army.JoinTroop(MonsterType.Archer, 5);

        Assert.False(army.MergeSameMonsterOnce());
        Assert.Equal(2u, army.OccupiedSlotCount);
    }

    [Fact]
    public void TheOptimizedArmyKeepsTheFirstAppearanceOrder()
    {
        ArmyStack army = NewArmy();

        army.JoinTroop(MonsterType.Archer, 2);
        army.JoinTroop(MonsterType.Peasant, 5);
        army.JoinTroop(MonsterType.Archer, 4);

        List<Troop> optimized = army.GetOptimized();

        Assert.Equal(2, optimized.Count);
        Assert.Equal(MonsterType.Archer, optimized[0].Monster);
        Assert.Equal(6u, optimized[0].Count);
        Assert.Equal(MonsterType.Peasant, optimized[1].Monster);
        Assert.Equal(5u, optimized[1].Count);
    }

    [Fact]
    public void TheOptimizedArmyIsACopy()
    {
        // The reference returns a new container of new Troop objects; the port copies the stacks so that a
        // caller can sort and truncate the result without touching the army.
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Peasant, 5);

        List<Troop> optimized = army.GetOptimized();
        optimized[0].Count = 99;
        optimized.Add(new Troop(MonsterType.Archer, 1));

        Assert.Equal(5u, army.Slots[0].Count);
        Assert.Equal(1u, army.OccupiedSlotCount);
    }

    [Fact]
    public void TheOptimizedArmySkipsEmptySlots()
    {
        ArmyStack army = NewArmy();

        Assert.Empty(army.GetOptimized());

        army.JoinTroop(MonsterType.Peasant, 1);

        Assert.Single(army.GetOptimized());
    }

    [Fact]
    public void SortingStrongestPutsTheWeakestStacksLast()
    {
        ArmyStack army = NewArmy();

        army.JoinTroop(MonsterType.Peasant, 3);
        army.JoinTroop(MonsterType.Swordsman, 10);
        army.JoinTroop(MonsterType.Archer, 4);

        army.SortStrongest();

        for (int i = 1; i < ArmyStack.MaximumTroopCount; ++i)
        {
            Assert.True(
                army.Slots[i - 1].GetStrength() >= army.Slots[i].GetStrength(),
                $"slot {i - 1} is weaker than slot {i}");
        }

        Assert.Equal(3u, army.OccupiedSlotCount);
        Assert.Equal(10u, army.GetCountMonsters(MonsterType.Swordsman));
        Assert.Equal(4u, army.GetCountMonsters(MonsterType.Archer));
        Assert.Equal(3u, army.GetCountMonsters(MonsterType.Peasant));
    }

    [Fact]
    public void SortingLeavesTheEmptySlotsAtTheEnd()
    {
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Peasant, 1);

        army.SortStrongest();

        Assert.Equal(MonsterType.Peasant, army.Slots[0].Monster);

        for (int i = 1; i < ArmyStack.MaximumTroopCount; ++i)
        {
            Assert.True(army.Slots[i].IsEmpty);
        }
    }

    [Fact]
    public void TheWeakestAndTheStrongestStackAreFoundByStrength()
    {
        ArmyStack army = NewArmy();

        Assert.Null(army.GetWeakestTroop());
        Assert.Null(army.GetStrongestTroop());

        army.JoinTroop(MonsterType.Peasant, 3);
        army.JoinTroop(MonsterType.Swordsman, 10);
        army.JoinTroop(MonsterType.Archer, 4);

        Assert.Equal(MonsterType.Peasant, army.GetWeakestTroop()!.Monster);
        Assert.Equal(MonsterType.Swordsman, army.GetStrongestTroop()!.Monster);

        // The answer is a live slot, not a copy: the caller is expected to edit it.
        Assert.Same(army.Slots[1], army.GetStrongestTroop());
    }

    [Fact]
    public void CountingMonstersSumsEveryStackOfThatType()
    {
        ArmyStack army = NewArmy();

        army.JoinTroop(MonsterType.Peasant, 5);
        army.JoinTroop(MonsterType.Peasant, 7);

        Assert.Equal(12u, army.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(0u, army.GetCountMonsters(MonsterType.Archer));
        Assert.Equal(0u, army.GetCountMonsters(MonsterType.Unknown));
    }

    [Fact]
    public void TheTroopsStrengthIsThePlainSumOfTheStacks()
    {
        ArmyStack army = NewArmy();

        army.JoinTroop(MonsterType.Peasant, 5);
        army.JoinTroop(MonsterType.Archer, 2);

        double expected = StrengthOf(MonsterType.Peasant, 5) + StrengthOf(MonsterType.Archer, 2);

        Assert.Equal(expected, army.GetTroopsStrength(), 9);

        // The two strengths are different questions: the army one multiplies in morale and luck (both races are
        // the knight's, so the army is of one race and fights with a bonus), the troop one does not.
        Assert.True(army.GetStrength() > army.GetTroopsStrength());
    }

    [Fact]
    public void TheReinforcementValueOfTheSameMonsterIsItsOwnStrength()
    {
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Peasant, 10);

        ArmyStack reinforcement = NewArmy();
        reinforcement.JoinTroop(MonsterType.Peasant, 5);

        // The five peasants merge into the ten that are already there, so nothing is lost to the slot limit.
        Assert.Equal(StrengthOf(MonsterType.Peasant, 5), army.GetReinforcementValue(reinforcement), 9);
    }

    [Fact]
    public void AReinforcementThatWouldBeCutAwayIsWorthNothing()
    {
        // Five strong stacks and one weak one: the weak one is the sixth and the army only has five slots.
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Swordsman, 10);
        army.JoinTroop(MonsterType.Cavalry, 10);
        army.JoinTroop(MonsterType.Mage, 3);
        army.JoinTroop(MonsterType.GreenDragon, 1);
        army.JoinTroop(MonsterType.Pikeman, 10);

        ArmyStack reinforcement = NewArmy();
        reinforcement.JoinTroop(MonsterType.Peasant, 5);

        Assert.Equal(0.0, army.GetReinforcementValue(reinforcement), 9);
    }

    [Fact]
    public void AStrongReinforcementPaysForTheWeakestStackItReplaces()
    {
        // The reinforcement is strong enough to win a slot, but the slot it wins was not empty: the value is
        // the dragon minus the peasant it pushed out, not the dragon.
        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Peasant, 1);
        army.JoinTroop(MonsterType.Archer, 1);
        army.JoinTroop(MonsterType.Pikeman, 1);
        army.JoinTroop(MonsterType.Swordsman, 1);
        army.JoinTroop(MonsterType.Cavalry, 1);

        ArmyStack reinforcement = NewArmy();
        reinforcement.JoinTroop(MonsterType.GreenDragon, 10);

        double dragon = StrengthOf(MonsterType.GreenDragon, 10);
        double peasant = StrengthOf(MonsterType.Peasant, 1);
        double value = army.GetReinforcementValue(reinforcement);

        Assert.True(value > 0);
        Assert.True(value < dragon);
        Assert.Equal(dragon - peasant, value, 9);
    }

    [Fact]
    public void ASuperiorArmyNeedsSafetyToBeSure()
    {
        ArmyStack strong = NewArmy();
        strong.JoinTroop(MonsterType.Swordsman, 10);

        ArmyStack weak = NewArmy();
        weak.JoinTroop(MonsterType.Peasant, 5);

        Assert.True(strong.IsStrongerThan(weak));
        Assert.False(weak.IsStrongerThan(strong));

        // Against itself a fair fight is not a win, and asking for a comfortable one is worse.
        Assert.False(strong.IsStrongerThan(strong));
        Assert.False(strong.IsStrongerThan(strong, 1.5));

        // A weaker target still wins when the ratio is generous enough.
        Assert.True(strong.IsStrongerThan(weak, 2.0));
    }

    [Fact]
    public void AnEmptyTargetLosesToEverything()
    {
        // The reference returns true before comparing any numbers, so even an empty army "beats" an empty one.
        ArmyStack army = NewArmy();
        ArmyStack empty = NewArmy();

        Assert.True(army.IsStrongerThan(empty));
        Assert.True(empty.IsStrongerThan(empty));
    }

    [Fact]
    public void TheMeleeTestIgnoresShootersAndFlyers()
    {
        ArmyStack melee = NewArmy();
        melee.JoinTroop(MonsterType.Swordsman, 10);
        Assert.True(melee.IsMeleeDominantArmy());

        ArmyStack shooters = NewArmy();
        shooters.JoinTroop(MonsterType.Archer, 10);
        Assert.False(shooters.IsMeleeDominantArmy());

        ArmyStack flyers = NewArmy();
        flyers.JoinTroop(MonsterType.GreenDragon, 1);
        Assert.False(flyers.IsMeleeDominantArmy());

        // As many swordsmen as it takes to outweigh the dragon, and one more turns the balance.
        uint enough = (uint)Math.Ceiling(
            MonsterStatistics.GetMonsterStrength(MonsterType.GreenDragon)
            / MonsterStatistics.GetMonsterStrength(MonsterType.Swordsman)) + 1;

        flyers.JoinTroop(MonsterType.Swordsman, enough);
        Assert.True(flyers.IsMeleeDominantArmy());

        ArmyStack empty = NewArmy();
        Assert.False(empty.IsMeleeDominantArmy());
    }

    [Fact]
    public void TheStrongestMonsterIsTheStrongestCreatureNotTheStrongestStack()
    {
        // Enough peasants to outweigh one dragon as a stack, but the dragon is the stronger creature — and the
        // reference compares the creatures, not the stacks.
        uint enough = (uint)Math.Ceiling(
            MonsterStatistics.GetMonsterStrength(MonsterType.GreenDragon)
            / MonsterStatistics.GetMonsterStrength(MonsterType.Peasant)) + 10;

        ArmyStack army = NewArmy();
        army.JoinTroop(MonsterType.Peasant, enough);
        army.JoinTroop(MonsterType.GreenDragon, 1);

        Assert.True(army.Slots[0].GetStrength() > army.Slots[1].GetStrength());
        Assert.Equal(MonsterType.GreenDragon, army.GetStrongestMonster());

        ArmyStack empty = NewArmy();
        Assert.Equal(MonsterType.Unknown, empty.GetStrongestMonster());
    }

    [Fact]
    public void RepairingClearsTheStacksThatHoldNoCreatures()
    {
        ArmyStack army = NewArmy();

        army.Slots[0].Set(MonsterType.Peasant, 0);
        army.Slots[1].Set(MonsterType.Archer, 5);

        Assert.False(army.Slots[0].IsValid);
        Assert.Equal(MonsterType.Peasant, army.Slots[0].Monster);

        army.ResetInvalidMonsters();

        Assert.Equal(MonsterType.Unknown, army.Slots[0].Monster);
        Assert.Equal(0u, army.Slots[0].Count);

        // The valid stack is untouched.
        Assert.Equal(MonsterType.Archer, army.Slots[1].Monster);
        Assert.Equal(5u, army.Slots[1].Count);
    }
}
