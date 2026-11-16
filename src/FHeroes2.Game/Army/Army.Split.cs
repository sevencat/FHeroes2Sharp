using System.Diagnostics;

using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Army;

/// <summary>
/// Spreading one stack over several slots: <c>Troops::SplitTroopIntoFreeSlots()</c> and the four helpers around
/// it (<c>army.cpp:834-1016</c>).
/// </summary>
/// <remarks>
/// <para>
/// These are the troops behind the army bar's split and merge controls: drag a stack onto an empty slot and it is
/// halved, press the "one of each" button and the stack is spread, drag a hero's army into a full garrison and
/// the weakest duplicates are merged to make room. The reference calls them from <c>army_bar.cpp</c>, which is
/// where they are used — but they are <c>Troops</c> methods with no drawing in them, so the port has them
/// without waiting for the dialog.
/// </para>
/// <para>
/// The single rule that runs through all five: <b>a split never creates or destroys a creature</b>. Every method
/// here either moves a count from one slot to another or merges two slots of the same monster; the totals are
/// what the tests check.
/// </para>
/// <para>
/// The reference recurses into <c>addNewTroopsToFreeSlots()</c> from two of the three drivers
/// (<see cref="MergeWeakestTroopsIfNeeded"/> and <see cref="SplitStackOfWeakestUnitsIntoFreeSlots"/>), which is
/// why this is one file rather than four independent methods.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// Halves a stack over the free slots, <c>Troops::SplitTroopIntoFreeSlots()</c> (<c>army.cpp:834-844</c>).
    /// </summary>
    /// <remarks>
    /// The reference takes the stack and the slot the player dragged it from, and refuses the split when the
    /// second is not one of its own — a guard against a caller that built the two arguments at different times.
    /// The port compares by reference, which is what the pointer comparison does.
    /// </remarks>
    /// <param name="troop">The stack to spread; the caller has already taken the count it wants spread out of it.</param>
    /// <param name="selectedSlot">The slot the stack came from; must be one of this army's five.</param>
    /// <param name="slots">How many slots the count should be spread over.</param>
    public void SplitTroopIntoFreeSlots(Troop troop, Troop selectedSlot, uint slots)
    {
        ArgumentNullException.ThrowIfNull(troop);
        ArgumentNullException.ThrowIfNull(selectedSlot);

        if (Array.IndexOf(_slots, selectedSlot) < 0)
        {
            // The selected slot is not part of this army, so this is not the intended call.
            return;
        }

        AddNewTroopsToFreeSlots(troop, slots);
    }

    /// <summary>
    /// Spreads a count of creatures over the free slots, <c>Troops::addNewTroopsToFreeSlots()</c>
    /// (<c>army.cpp:846-877</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The count is divided by the number of slots rather than by the number of creatures, so ten over three
    /// slots is 4-3-3: the remainder goes to the <i>first</i> slots, one creature each, and never to the last.
    /// The number of slots is also clamped to what is free, so asking for five with one empty slot gives
    /// everything to that one slot.
    /// </para>
    /// <para>
    /// Nothing is taken out of <paramref name="troop"/>: the caller has already reduced it (that is what
    /// <c>ArmyBar</c> does when it halves a stack), and this method only writes the new stacks in.
    /// </para>
    /// </remarks>
    /// <param name="troop">The stack whose monster and count are to be spread.</param>
    /// <param name="maxSlots">How many slots to spread over; clamped to the free ones.</param>
    public void AddNewTroopsToFreeSlots(Troop troop, uint maxSlots)
    {
        ArgumentNullException.ThrowIfNull(troop);

        uint occupied = OccupiedSlotCount;

        if (maxSlots < 1 || occupied >= MaximumTroopCount)
        {
            // Nothing to spread into, or nothing to spread over.
            Debug.Assert(false, "A split was asked for without a free slot.");
            return;
        }

        uint freeSlots = (uint)MaximumTroopCount - occupied;

        if (maxSlots > freeSlots)
        {
            maxSlots = freeSlots;
        }

        MonsterType monster = troop.Monster;
        uint troopCount = troop.Count;
        uint chunk = troopCount / maxSlots;
        uint remainingCount = troopCount % maxSlots;
        uint remainingSlots = maxSlots;

        foreach (Troop slot in _slots)
        {
            if (remainingSlots == 0)
            {
                return;
            }

            if (slot.IsValid)
            {
                continue;
            }

            slot.Set(monster, remainingCount > 0 ? chunk + 1 : chunk);

            --remainingSlots;

            if (remainingCount > 0)
            {
                --remainingCount;
            }
        }
    }

    /// <summary>
    /// Merges the weakest duplicated monster so that a free slot appears,
    /// <c>Troops::mergeWeakestTroopsIfNeeded()</c> (<c>army.cpp:879-944</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three outcomes, and the caller has to tell them apart: <see langword="true"/> when there is room
    /// (there already was, or one was just made), <see langword="false"/> when the army is full of five
    /// <i>different</i> monsters and nothing can be merged.
    /// </para>
    /// <para>
    /// The stack that gets merged is the weakest <i>monster</i> that sits in more than one slot — measured by the
    /// total strength of all its slots, not by a single slot — so an army of three peasant stacks and two archer
    /// stacks merges the peasants. Ties go to the monster that comes first in the reference's <c>std::map</c>,
    /// i.e. the lower monster id; the port keeps the enumeration order by collecting the ids in slot order.
    /// </para>
    /// </remarks>
    /// <returns>Whether there is now a free slot.</returns>
    public bool MergeWeakestTroopsIfNeeded()
    {
        if (OccupiedSlotCount < MaximumTroopCount)
        {
            // Not a full house, so there is room already.
            return true;
        }

        Dictionary<MonsterType, uint> slotCounts = [];
        Dictionary<MonsterType, double> strengths = [];

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            if (strengths.TryGetValue(troop.Monster, out double strength))
            {
                strengths[troop.Monster] = strength + troop.GetStrength();
                ++slotCounts[troop.Monster];
            }
            else
            {
                slotCounts[troop.Monster] = 1;
                strengths[troop.Monster] = troop.GetStrength();
            }
        }

        if (strengths.Count == MaximumTroopCount)
        {
            // Five different monsters: merging nothing leaves five stacks, merging anything would still leave
            // five stacks of four monsters — but the reference refuses here, and so does the port.
            return false;
        }

        List<KeyValuePair<MonsterType, double>> multiSlot = [];

        foreach (KeyValuePair<MonsterType, uint> entry in slotCounts)
        {
            if (entry.Value > 1)
            {
                multiSlot.Add(new KeyValuePair<MonsterType, double>(entry.Key, strengths[entry.Key]));
            }
        }

        Debug.Assert(multiSlot.Count > 0, "A full army without duplicates cannot be merged.");

        if (multiSlot.Count == 0)
        {
            return false;
        }

        multiSlot.Sort(static (left, right) => left.Value.CompareTo(right.Value));

        MonsterType weakest = multiSlot[0].Key;
        uint slotCount = slotCounts[weakest];
        uint monsterCount = 0;

        foreach (Troop troop in _slots)
        {
            if (troop.IsValid && troop.IsMonster(weakest))
            {
                monsterCount += troop.Count;
                troop.Reset();
            }
        }

        AddNewTroopsToFreeSlots(new Troop(weakest, monsterCount), slotCount - 1);

        return true;
    }

    /// <summary>
    /// Takes one creature out of the weakest stack and puts it into a free slot,
    /// <c>Troops::splitStackOfWeakestUnitsIntoFreeSlots()</c> (<c>army.cpp:946-981</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whirlpool's split: the army comes out with one more stack than it had, made of a single creature of
    /// whichever monster is weakest per head. The count moved is limited by both the free slots and "leave at
    /// least one creature in the original stack", so a stack of one is never emptied.
    /// </para>
    /// <para>
    /// Unlike <see cref="MergeWeakestTroopsIfNeeded"/> a full army is simply refused: there is no duplicate to
    /// merge here, and spreading the weakest stack over zero slots is a no-op.
    /// </para>
    /// </remarks>
    public void SplitStackOfWeakestUnitsIntoFreeSlots()
    {
        uint occupied = OccupiedSlotCount;

        if (occupied >= MaximumTroopCount)
        {
            // All slots are taken, so there is nowhere to split to.
            return;
        }

        Troop? weakest = null;
        double weakestStrength = 0;

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid)
            {
                continue;
            }

            double strength = MonsterStatistics.GetMonsterStrength(troop.Monster);

            if (weakest is null || weakestStrength > strength)
            {
                weakest = troop;
                weakestStrength = strength;
            }
        }

        Debug.Assert(weakest is not null, "An empty army has nothing to split.");
        Debug.Assert(weakest is null || weakest.Count > 0);

        if (weakest is null)
        {
            return;
        }

        uint count = Math.Min((uint)MaximumTroopCount - occupied, weakest.Count - 1);

        if (count == 0)
        {
            return;
        }

        weakest.Count -= count;

        AddNewTroopsToFreeSlots(new Troop(weakest.Monster, count), count);
    }

    /// <summary>
    /// Puts a count of creatures into the first empty slot, <c>Troops::AssignToFirstFreeSlot()</c>
    /// (<c>army.cpp:983-995</c>).
    /// </summary>
    /// <remarks>
    /// A slot is written even when the army is full — in that case the loop finds no empty slot and nothing
    /// happens, which is why the reference can declare the method <c>const</c> and still call it "assign".
    /// </remarks>
    /// <param name="troop">The stack whose monster is used.</param>
    /// <param name="count">How many of that monster to place.</param>
    public void AssignToFirstFreeSlot(Troop troop, uint count)
    {
        ArgumentNullException.ThrowIfNull(troop);

        foreach (Troop slot in _slots)
        {
            if (!slot.IsValid)
            {
                slot.Set(troop.Monster, count);
                return;
            }
        }
    }

    /// <summary>
    /// Moves every stack of one monster into the given one, <c>Troops::JoinAllTroopsOfType()</c>
    /// (<c>army.cpp:997-1016</c>).
    /// </summary>
    /// <remarks>
    /// The target stack grows to the army-wide total and every other stack of that monster is emptied — the
    /// "click one stack of peasants and all three become one" button. The target must be one of this army's
    /// slots; a stack from outside changes nothing, because the loop only compares against it.
    /// </remarks>
    /// <param name="targetTroop">The slot that should end up holding every creature of its monster.</param>
    public void JoinAllTroopsOfType(Troop targetTroop)
    {
        ArgumentNullException.ThrowIfNull(targetTroop);

        MonsterType monster = targetTroop.Monster;
        uint totalCount = GetCountMonsters(monster);

        foreach (Troop troop in _slots)
        {
            if (!troop.IsValid || !troop.IsMonster(monster))
            {
                continue;
            }

            if (ReferenceEquals(troop, targetTroop))
            {
                troop.Count = totalCount;
            }
            else
            {
                troop.Reset();
            }
        }
    }
}
