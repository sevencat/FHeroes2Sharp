using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using Xunit;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the stack spreading of <c>army.cpp:834-1016</c>: <c>Troops::SplitTroopIntoFreeSlots</c>,
/// <c>addNewTroopsToFreeSlots</c>, <c>mergeWeakestTroopsIfNeeded</c>,
/// <c>splitStackOfWeakestUnitsIntoFreeSlots</c>, <c>AssignToFirstFreeSlot</c> and <c>JoinAllTroopsOfType</c>.
/// </summary>
/// <remarks>
/// Every test also checks the total, because the whole point of these five methods is that a split moves
/// creatures around and never creates or destroys them.
/// </remarks>
public sealed class ArmySplitTests
{
    [Fact]
    public void TenOverThreeSlotsIsFourThreeThree()
    {
        ArmyStack army = new();

        army.AddNewTroopsToFreeSlots(new Troop(MonsterType.Archer, 10), 3);

        Assert.Equal(3u, army.OccupiedSlotCount);
        Assert.Equal(4u, army.GetTroop(0).Count);
        Assert.Equal(3u, army.GetTroop(1).Count);
        Assert.Equal(3u, army.GetTroop(2).Count);
        Assert.False(army.GetTroop(3).IsValid);
        Assert.Equal(10u, army.GetCountMonsters(MonsterType.Archer));
    }

    [Fact]
    public void TheRemainderGoesToTheFirstSlots()
    {
        ArmyStack army = new();

        army.AddNewTroopsToFreeSlots(new Troop(MonsterType.Archer, 7), 3);

        Assert.Equal(3u, army.GetTroop(0).Count);
        Assert.Equal(2u, army.GetTroop(1).Count);
        Assert.Equal(2u, army.GetTroop(2).Count);
    }

    [Fact]
    public void TheRequestIsClampedToTheFreeSlots()
    {
        ArmyStack army = new();

        for (int slot = 0; slot < 4; ++slot)
        {
            army.GetTroop(slot).Set(MonsterType.Peasant, 1);
        }

        army.AddNewTroopsToFreeSlots(new Troop(MonsterType.Archer, 10), 5);

        // Five slots were asked for but only one was free, so all ten archers land in it.
        Assert.Equal(5u, army.OccupiedSlotCount);
        Assert.Equal(10u, army.GetTroop(4).Count);
    }

    [Fact]
    public void SplitTroopIntoFreeSlotsIgnoresASlotOfAnotherArmy()
    {
        ArmyStack army = new();
        ArmyStack other = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 5);

        army.SplitTroopIntoFreeSlots(new Troop(MonsterType.Peasant, 5), other.GetTroop(1), 2);

        // The slot belongs to the other army, so the call is refused and neither army changes.
        Assert.Equal(1u, army.OccupiedSlotCount);
        Assert.Equal(5u, army.GetTroop(0).Count);
        Assert.Equal(0u, other.OccupiedSlotCount);
    }

    [Fact]
    public void SplitTroopIntoFreeSlotsSpreadsOverTheFreeSlots()
    {
        ArmyStack army = new();

        Troop slot = army.GetTroop(0);

        slot.Set(MonsterType.Peasant, 5);

        // The caller has already halved the stack, and the half that was taken out is what gets spread.
        army.SplitTroopIntoFreeSlots(new Troop(MonsterType.Peasant, 5), slot, 2);

        Assert.Equal(3u, army.OccupiedSlotCount);
        Assert.Equal(5u, slot.Count);
        Assert.Equal(3u, army.GetTroop(1).Count);
        Assert.Equal(2u, army.GetTroop(2).Count);
        Assert.Equal(10u, army.GetCountMonsters(MonsterType.Peasant));
    }

    [Fact]
    public void AFullHouseMergesItsWeakestDuplicate()
    {
        ArmyStack army = FullHouse();

        Assert.True(army.MergeWeakestTroopsIfNeeded());

        Assert.Equal(4u, army.OccupiedSlotCount);
        Assert.Equal(20u, army.GetCountMonsters(MonsterType.Peasant));
        Assert.Equal(1u, army.GetCountMonsters(MonsterType.GreenDragon));
    }

    [Fact]
    public void AFullHouseOfDifferentMonstersCannotMerge()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 10);
        army.GetTroop(1).Set(MonsterType.Archer, 10);
        army.GetTroop(2).Set(MonsterType.Pikeman, 10);
        army.GetTroop(3).Set(MonsterType.Swordsman, 10);
        army.GetTroop(4).Set(MonsterType.Cavalry, 10);

        Assert.False(army.MergeWeakestTroopsIfNeeded());

        Assert.Equal(5u, army.OccupiedSlotCount);
        Assert.Equal(10u, army.GetCountMonsters(MonsterType.Peasant));
    }

    [Fact]
    public void AnArmyWithRoomIsAlreadyMerged()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 5);
        army.GetTroop(1).Set(MonsterType.Peasant, 5);

        Assert.True(army.MergeWeakestTroopsIfNeeded());

        // Nothing to do: two slots of five are fine as long as a third slot is free.
        Assert.Equal(2u, army.OccupiedSlotCount);
        Assert.Equal(5u, army.GetTroop(1).Count);
    }

    [Fact]
    public void TheWeakestStackGivesOneCreaturePerFreeSlot()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 5);
        army.GetTroop(1).Set(MonsterType.GreenDragon, 2);
        army.GetTroop(2).Set(MonsterType.GreenDragon, 2);
        army.GetTroop(3).Set(MonsterType.Pikeman, 1);

        army.SplitStackOfWeakestUnitsIntoFreeSlots();

        // One free slot, and the peasants can spare four, so exactly one peasant moves out.
        Assert.Equal(5u, army.OccupiedSlotCount);
        Assert.Equal(4u, army.GetTroop(0).Count);
        Assert.Equal(1u, army.GetTroop(4).Count);
        Assert.Equal(MonsterType.Peasant, army.GetTroop(4).Monster);
        Assert.Equal(5u, army.GetCountMonsters(MonsterType.Peasant));
    }

    [Fact]
    public void ASingleCreatureStackIsNotSplit()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 1);
        army.GetTroop(1).Set(MonsterType.GreenDragon, 2);

        army.SplitStackOfWeakestUnitsIntoFreeSlots();

        Assert.Equal(2u, army.OccupiedSlotCount);
        Assert.Equal(1u, army.GetTroop(0).Count);
    }

    [Fact]
    public void AssignToFirstFreeSlotWritesTheFirstEmptySlot()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 1);
        army.AssignToFirstFreeSlot(new Troop(MonsterType.Archer, 7), 3);

        Assert.Equal(MonsterType.Archer, army.GetTroop(1).Monster);
        Assert.Equal(3u, army.GetTroop(1).Count);
        Assert.False(army.GetTroop(2).IsValid);
    }

    [Fact]
    public void JoinAllTroopsOfTypeGathersEveryStackIntoTheTarget()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 3);
        army.GetTroop(1).Set(MonsterType.Archer, 2);
        army.GetTroop(2).Set(MonsterType.Peasant, 4);

        army.JoinAllTroopsOfType(army.GetTroop(2));

        Assert.Equal(7u, army.GetTroop(2).Count);
        Assert.False(army.GetTroop(0).IsValid);
        Assert.Equal(2u, army.OccupiedSlotCount);
        Assert.Equal(7u, army.GetCountMonsters(MonsterType.Peasant));
    }

    /// <summary>
    /// Five slots: two peasant stacks (the duplicated weak monster), a strong dragon and two others.
    /// </summary>
    private static ArmyStack FullHouse()
    {
        ArmyStack army = new();

        army.GetTroop(0).Set(MonsterType.Peasant, 10);
        army.GetTroop(1).Set(MonsterType.Pikeman, 1);
        army.GetTroop(2).Set(MonsterType.Archer, 1);
        army.GetTroop(3).Set(MonsterType.GreenDragon, 1);
        army.GetTroop(4).Set(MonsterType.Peasant, 10);

        return army;
    }
}
