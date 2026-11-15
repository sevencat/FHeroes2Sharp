using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Army</c> of <c>src/fheroes2/army/army.h</c> / <c>army.cpp</c> as ported by
/// <c>FHeroes2.Game.Army.Army</c>: the five slots, the slowest/fastest queries the movement points depend on,
/// joining, cleaning and the starting army.
/// </summary>
/// <remarks>
/// The reference <c>Army</c> holds a <c>HeroBase * commander</c> that supplies the race and the colour. The
/// port has neither a base class nor a back-pointer, so the two are plain properties and nothing here tests
/// them beyond "the race picks the starting creatures".
/// </remarks>
public sealed class ArmyTests
{
    [Fact]
    public void ANewArmyHasFiveEmptySlots()
    {
        var army = new ArmyStack();

        Assert.Equal(5, ArmyStack.MaximumTroopCount);
        Assert.Equal(5, army.Slots.Count);
        Assert.False(army.IsValid);
        Assert.Equal(0u, army.OccupiedSlotCount);
        Assert.Null(army.GetFirstValidTroop());
        Assert.Null(army.GetSlowestTroop());
        Assert.Null(army.GetFastestTroop());
        Assert.Equal(0u, army.GetCount());
        Assert.Equal(0u, army.GetTotalHitPoints());
        Assert.False(army.HasMonster(MonsterType.Peasant));
    }

    [Fact]
    public void EverySlotOfANewArmyIsAnObject()
    {
        var army = new ArmyStack();

        // Empty slots are Troop objects, not nulls: the reference has a vector of Troop* that are always
        // allocated, and the army's own methods dereference them unconditionally.
        foreach (Troop troop in army.Slots)
        {
            Assert.NotNull(troop);
            Assert.True(troop.IsEmpty);
        }
    }

    [Fact]
    public void TheSlotAccessorsAcceptANegativeIndex()
    {
        var army = new ArmyStack();

        for (int i = 0; i < ArmyStack.MaximumTroopCount; ++i)
        {
            army.JoinTroop((MonsterType)(i + 1), 4);
        }

        Assert.Equal(MonsterType.VeteranPikeman, army[-1].Monster);
        Assert.Same(army[-1], army.GetTroop(4));
        Assert.Same(army[0], army.GetTroop(0));
    }

    [Fact]
    public void SlotsOutsideTheArmyThrow()
    {
        var army = new ArmyStack();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            army.GetTroop(5);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            army.GetTroop(-6);
        });
    }

    [Fact]
    public void JoinTroopFillsTheFirstEmptySlot()
    {
        var army = new ArmyStack();

        Assert.True(army.JoinTroop(MonsterType.Peasant, 10));

        Assert.Equal(MonsterType.Peasant, army[0].Monster);
        Assert.Equal(10u, army[0].Count);
        Assert.Equal(1u, army.OccupiedSlotCount);
        Assert.True(army.IsValid);
        Assert.Same(army[0], army.GetFirstValidTroop());
    }

    [Fact]
    public void JoinTroopPrefersAnEmptySlotUnlessToldOtherwise()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Peasant, 10);

        // emptySlotFirst = true (the default): a second pile of peasants opens a new slot.
        Assert.True(army.JoinTroop(MonsterType.Peasant, 5));
        Assert.Equal(10u, army[0].Count);
        Assert.Equal(5u, army[1].Count);

        // Army::Reset() and the Troop overload pass false; the merge goes into the first matching stack,
        // which is the one in slot 0, not the newest one.
        Assert.True(army.JoinTroop(MonsterType.Peasant, 5, false));
        Assert.Equal(15u, army[0].Count);
        Assert.Equal(5u, army[1].Count);
        Assert.Equal(2u, army.OccupiedSlotCount);
    }

    [Fact]
    public void JoinTroopRefusesAnInvalidMonsterOrAnEmptyStack()
    {
        var army = new ArmyStack();

        Assert.False(army.JoinTroop(MonsterType.Unknown, 5));
        Assert.False(army.JoinTroop(MonsterType.Peasant, 0));
        Assert.False(army.JoinTroop(MonsterType.RandomMonsterLevel1, 5));
        Assert.False(army.IsValid);
    }

    [Fact]
    public void JoinTroopFailsWhenTheArmyIsFull()
    {
        var army = new ArmyStack();

        for (int i = 0; i < ArmyStack.MaximumTroopCount; ++i)
        {
            Assert.True(army.JoinTroop((MonsterType)(i + 1), 1));
        }

        Assert.Equal(5u, army.OccupiedSlotCount);
        Assert.False(army.CanJoinTroop(MonsterType.Phoenix));
        Assert.False(army.JoinTroop(MonsterType.Phoenix, 1));
    }

    [Fact]
    public void JoinTroopOfAStackMergesWithItsOwnKind()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Archer, 2);

        Assert.True(army.JoinTroop(new Troop(MonsterType.Archer, 3)));

        Assert.Equal(5u, army[0].Count);
        Assert.Equal(1u, army.OccupiedSlotCount);
    }

    [Fact]
    public void JoinTroopOfAnInvalidStackIsRefused()
    {
        var army = new ArmyStack();

        Assert.False(army.JoinTroop(new Troop()));
        Assert.False(army.JoinTroop(new Troop(MonsterType.Peasant, 0)));
        Assert.Throws<ArgumentNullException>(() =>
        {
            army.JoinTroop((Troop)null!);
        });
    }

    [Fact]
    public void TheSlowestStackSetsThePace()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Champion, 1); // UltraFast
        army.JoinTroop(MonsterType.Peasant, 1); // VerySlow

        Assert.Same(army[1], army.GetSlowestTroop());
        Assert.Same(army[0], army.GetFastestTroop());
    }

    [Fact]
    public void ATieKeepsTheEarlierSlot()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Peasant, 1); // VerySlow
        army.JoinTroop(MonsterType.Archer, 1); // VerySlow

        // getBestMatchToCondition() starts from the first valid slot and only replaces it when the later one
        // is strictly slower, so a tie keeps the earlier stack.
        Assert.Same(army[0], army.GetSlowestTroop());
    }

    [Fact]
    public void SpeedsComeFromTheMonsterRows()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Halfling, 1); // Slow = 3

        var other = new ArmyStack();
        other.JoinTroop(MonsterType.Cavalry, 1); // VeryFast = 6

        Assert.Equal(MonsterSpeed.Slow, army.GetSlowestTroop()!.GetSpeed());
        Assert.Equal(MonsterSpeed.VeryFast, other.GetFastestTroop()!.GetSpeed());
    }

    [Fact]
    public void CountsAndHitPointsSkipEmptySlots()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Archer, 3); // 10 hp each
        army.JoinTroop(MonsterType.Peasant, 4); // 1 hp each

        Assert.Equal(7u, army.GetCount());
        Assert.Equal(34u, army.GetTotalHitPoints());
        Assert.Equal(2u, army.OccupiedSlotCount);
    }

    [Fact]
    public void HasMonsterLooksAtValidSlotsOnly()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Peasant, 0);

        // A zero-count slot is empty, so the monster is not in the army.
        Assert.False(army.HasMonster(MonsterType.Peasant));

        army.JoinTroop(MonsterType.Peasant, 1);

        Assert.True(army.HasMonster(MonsterType.Peasant));
    }

    [Fact]
    public void CanJoinTroopNeedsRoomOrTheSameMonster()
    {
        var full = new ArmyStack();
        for (int i = 0; i < ArmyStack.MaximumTroopCount; ++i)
        {
            full.JoinTroop((MonsterType)(i + 1), 1);
        }

        Assert.False(full.CanJoinTroop(MonsterType.Phoenix));
        Assert.True(full.CanJoinTroop(MonsterType.Peasant));

        var empty = new ArmyStack();

        Assert.True(empty.CanJoinTroop(MonsterType.Phoenix));
    }

    [Fact]
    public void CleanEmptiesEverySlot()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Peasant, 10);
        army.JoinTroop(MonsterType.Archer, 5);

        army.Clean();

        Assert.False(army.IsValid);
        Assert.Equal(0u, army.OccupiedSlotCount);

        foreach (Troop troop in army.Slots)
        {
            Assert.True(troop.IsEmpty);
        }
    }

    [Fact]
    public void ResetWithoutADefaultArmyGivesOnePeasantToAKnight()
    {
        var army = new ArmyStack(MapRace.Knight);

        army.Reset();

        Assert.Equal(MonsterType.Peasant, army[0].Monster);
        Assert.Equal(1u, army[0].Count);
        Assert.Equal(1u, army.OccupiedSlotCount);
    }

    [Fact]
    public void ResetHasNoEffectWithoutARace()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Goblin, 3);

        army.Reset();

        // The reference returns early from Reset() when the commander has no race — and it has already
        // cleaned the army by then, so the result is an empty army, not the old one.
        Assert.False(army.IsValid);
    }

    [Fact]
    public void ResetWithADefaultArmyStaysWithinTheStartingRanges()
    {
        bool sawSecondStack = false;
        bool sawSingleStack = false;

        for (ulong seed = 1; seed <= 200; ++seed)
        {
            var army = new ArmyStack(MapRace.Knight);
            army.Reset(true, new Pcg32(seed));

            Assert.Equal(MonsterType.Peasant, army[0].Monster);
            Assert.InRange(army[0].Count, 30u, 50u);

            if (army[1].IsValid)
            {
                sawSecondStack = true;

                Assert.Equal(MonsterType.Archer, army[1].Monster);
                Assert.InRange(army[1].Count, 3u, 5u);

                for (int slot = 2; slot < ArmyStack.MaximumTroopCount; ++slot)
                {
                    Assert.True(army[slot].IsEmpty);
                }
            }
            else
            {
                sawSingleStack = true;
            }
        }

        // Reset() skips the second stack one time out of ten, so both outcomes must show up.
        Assert.True(sawSecondStack);
        Assert.True(sawSingleStack);
    }

    [Theory]
    [InlineData(MonsterType.Peasant, 30u, 50u)]
    [InlineData(MonsterType.Goblin, 15u, 25u)]
    [InlineData(MonsterType.Sprite, 10u, 20u)]
    [InlineData(MonsterType.Skeleton, 6u, 10u)]
    [InlineData(MonsterType.Archer, 3u, 5u)]
    [InlineData(MonsterType.Orc, 3u, 5u)]
    [InlineData(MonsterType.Dwarf, 2u, 4u)]
    [InlineData(MonsterType.Pikeman, 0u, 0u)]
    [InlineData(MonsterType.Paladin, 0u, 0u)]
    public void TheStartingRangesFollowTheLevel(MonsterType monster, uint min, uint max)
    {
        Assert.Equal((min, max), ArmyStack.GetNumberOfMonstersInStartingArmy(monster));
    }

    [Fact]
    public void TheArmyEnumeratesItsFiveSlots()
    {
        var army = new ArmyStack();
        army.JoinTroop(MonsterType.Peasant, 2);

        int count = 0;

        foreach (Troop troop in army)
        {
            Assert.NotNull(troop);
            ++count;
        }

        Assert.Equal(ArmyStack.MaximumTroopCount, count);
    }

    [Theory]
    [InlineData(MapRace.Knight, Dwelling.Monster1, MonsterType.Peasant)]
    [InlineData(MapRace.Barbarian, Dwelling.Monster1, MonsterType.Goblin)]
    [InlineData(MapRace.Sorceress, Dwelling.Monster1, MonsterType.Sprite)]
    [InlineData(MapRace.Warlock, Dwelling.Monster1, MonsterType.Centaur)]
    [InlineData(MapRace.Wizard, Dwelling.Monster1, MonsterType.Halfling)]
    [InlineData(MapRace.Necromancer, Dwelling.Monster1, MonsterType.Skeleton)]
    [InlineData(MapRace.Knight, Dwelling.Monster2, MonsterType.Archer)]
    [InlineData(MapRace.Knight, Dwelling.Upgrade2, MonsterType.Ranger)]
    [InlineData(MapRace.Warlock, Dwelling.Upgrade2, MonsterType.Gargoyle)]
    [InlineData(MapRace.Knight, Dwelling.Monster6, MonsterType.Paladin)]
    [InlineData(MapRace.Warlock, Dwelling.Upgrade7, MonsterType.BlackDragon)]
    [InlineData(MapRace.None, Dwelling.Monster1, MonsterType.Unknown)]
    public void ADwellingNamesItsMonster(MapRace race, uint dwelling, MonsterType expected)
    {
        Assert.Equal(expected, DwellingMonsters.FromDwelling(race, dwelling));
    }
}
