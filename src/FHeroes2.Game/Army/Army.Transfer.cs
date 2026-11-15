using System.Diagnostics;

using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Army;

/// <summary>
/// Moving stacks from one army to another: <c>Troops::JoinStrongest</c> (<c>army.cpp:702-832</c>), the
/// <c>Army</c>-level twins (<c>army.cpp:1426-1540</c>) and the two troop swaps (<c>army.cpp:1753-1756</c>).
/// </summary>
/// <remarks>
/// <para>
/// These are the operations behind the hero-meeting dialog and the castle dialog: a hero hands his army to a
/// garrison, a garrison hands its army to a hero, the two swap. Three rules run through all of them, and each
/// one is a lost game when a port gets it wrong — a hero must leave with at least one stack (an empty army is a
/// dead hero), a garrison may be empty, and when the two armies cannot fit into five slots the strongest stacks
/// are the ones that must stay.
/// </para>
/// <para>
/// The reference splits the work two ways: <c>Troops</c> knows the stacks and <c>Army</c> knows the five slots
/// plus the commander. The port has one class, so the split is between files instead — <c>Army.Troops.cs</c>
/// merges, orders and compares the stacks inside one army, this file moves them between two.
/// </para>
/// <para>
/// The reference's asserts are <see cref="Debug.Assert(bool)"/> calls here, as elsewhere in the port: they
/// describe a caller's precondition, and a release build does not pay for them.
/// </para>
/// <para>
/// Not here: <c>SplitTroopIntoFreeSlots</c>, <c>AssignToFirstFreeSlot</c>, <c>JoinAllTroopsOfType</c> and
/// <c>addNewTroopsToFreeSlots</c> (<c>army.cpp:834-1120</c>) — the other half of the same dialogs, the drag of
/// one stack and the "one of each" buttons. They wait for the dialog itself.
/// </para>
/// </remarks>
public sealed partial class Army
{
    /// <summary>
    /// Keeps the strongest five stacks of the two armies, <c>Troops::JoinStrongest()</c>
    /// (<c>army.cpp:702-832</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four passes, in order of how cheap the win is:
    /// </para>
    /// <list type="number">
    /// <item>the same slot index: an empty one takes the stack whole, a matching one merges the two;</item>
    /// <item>whatever is left goes into the free slots, merging with a stack of the same monster;</item>
    /// <item>if the giver still holds stacks, the five strongest of the merged army are kept — the receiver's
    /// own duplicates are merged first to free slots, then the giver's best fill the gaps, and finally the
    /// receiver's weakest is swapped for the giver's strongest for as long as that is an improvement;</item>
    /// <item>everything that did not make it goes back to the giver.</item>
    /// </list>
    /// <para>
    /// The last block is the hero rule. When the giver is a hero who would be left empty, it must get something
    /// back: a stack worth less than a twentieth of the joined army is given back whole, a heavier one is split
    /// so that the part handed back is worth about a twentieth, never more than half of it, never less than one
    /// creature. A garrison (<paramref name="keepAtLeastOneSlotForGiver"/> false) gets nothing back.
    /// </para>
    /// <para>
    /// The port trades <c>std::sort</c> for <see cref="List{T}.Sort(Comparison{T})"/> — both are unstable, so
    /// equal stacks may come out in either order, exactly as in the reference.
    /// </para>
    /// </remarks>
    /// <param name="giverArmy">The army that is being emptied; it keeps what does not fit.</param>
    /// <param name="keepAtLeastOneSlotForGiver">
    /// Whether the giver must be left with a stack. The reference passes
    /// <c>giver.commander != nullptr &amp;&amp; giver.commander-&gt;isHeroes()</c>; see
    /// <see cref="JoinStrongestFromArmy"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="giverArmy"/> is <see langword="null"/>.</exception>
    public void JoinStrongest(Army giverArmy, bool keepAtLeastOneSlotForGiver)
    {
        ArgumentNullException.ThrowIfNull(giverArmy);

        if (ReferenceEquals(this, giverArmy))
        {
            return;
        }

        // 1. First try to keep the stacks in the same slots.
        for (int slot = 0; slot < MaximumTroopCount; ++slot)
        {
            Troop taker = _slots[slot];
            Troop giver = giverArmy._slots[slot];

            if (!giver.IsValid)
            {
                continue;
            }

            if (!taker.IsValid)
            {
                // The slot is empty, so the stack simply moves over.
                taker.Set(giver);
                giver.Reset();
            }
            else if (taker.IsMonster(giver.Monster))
            {
                taker.Count += giver.Count;
                giver.Reset();
            }
        }

        // 2. There are still unmerged stacks left and there is empty room for them.
        for (int slot = 0; slot < MaximumTroopCount; ++slot)
        {
            Troop giver = giverArmy._slots[slot];

            if (giver.IsValid && JoinTroop(giver.Monster, giver.Count, false))
            {
                giver.Reset();
            }
        }

        // 3. More stacks than slots: keep the best five.
        if (giverArmy.OccupiedSlotCount > 0)
        {
            List<Troop> priority = giverArmy.GetOptimized();

            giverArmy.Clean();

            // Strongest at the end, so that the best is the last one popped.
            SortWeakestFirst(priority);

            // 3.1. Merge any remaining stack of ours to free some room.
            MergeSameMonsterTroops();

            // 3.2. Fill the empty slots with the best of theirs.
            int count = (int)OccupiedSlotCount;

            while (count < MaximumTroopCount && priority.Count > 0)
            {
                if (!JoinTroop(priority[^1]))
                {
                    // Something is wrong with the count of the free slots.
                    Debug.Assert(false, "A free slot was counted but refused the stack.");
                }

                priority.RemoveAt(priority.Count - 1);
                ++count;
            }

            // 3.3. Swap our weakest for their strongest until theirs is no better.
            while (priority.Count > 0)
            {
                Troop? weakest = GetWeakestTroop();

                if (weakest is null || weakest.GetStrength() > priority[^1].GetStrength())
                {
                    // Their stacks are no better than ours, so we are done.
                    break;
                }

                SwapTroops(weakest, priority[^1]);

                SortWeakestFirst(priority);
            }

            // 4. The rest goes back to the giver.
            while (priority.Count > 0)
            {
                if (!giverArmy.JoinTroop(priority[^1]))
                {
                    // We somehow cannot hand the same army its stack back.
                    Debug.Assert(false, "The giver refused its own stack.");
                }

                priority.RemoveAt(priority.Count - 1);
            }
        }

        if (!keepAtLeastOneSlotForGiver || giverArmy.IsValid)
        {
            // Either the giver does not need a stack of its own, or it already has one.
            return;
        }

        Troop? leftover = GetWeakestTroop();

        if (leftover is null)
        {
            // How is it possible that two merged armies hold no creatures at all?
            Debug.Assert(false, "A hero would be left with an empty army.");
            return;
        }

        // First check whether the weakest stack is worth keeping: it should not be more than a twentieth of the
        // army, and if it is, only a part of it is handed back.
        double weakestStrength = leftover.GetStrength();
        double strengthLimit = GetTroopsStrength() / 20;

        if (weakestStrength < strengthLimit)
        {
            if (!giverArmy.JoinTroop(leftover.Monster, leftover.Count, false))
            {
                Debug.Assert(false, "The empty giver refused a whole stack.");
            }

            leftover.Reset();

            return;
        }

        uint acceptableCount = (uint)(strengthLimit / weakestStrength * leftover.Count);

        if (acceptableCount > leftover.Count / 2)
        {
            // No more than half of the stack goes back.
            acceptableCount = leftover.Count / 2;
        }

        if (acceptableCount < 1)
        {
            acceptableCount = 1;
        }

        if (!giverArmy.JoinTroop(leftover.Monster, acceptableCount, false))
        {
            Debug.Assert(false, "The empty giver refused a part of a stack.");
        }

        leftover.Count -= acceptableCount;
    }

    /// <summary>
    /// Hands the giver's army to this one, keeping a stack for a hero, <c>Army::JoinStrongestFromArmy()</c>
    /// (<c>army.cpp:1426-1430</c>).
    /// </summary>
    /// <remarks>
    /// The single caller-facing entry point of <see cref="JoinStrongest"/>: the "hand over the army" button of
    /// the hero-meeting dialog and the AI's army consolidation both go through it, and the only thing it adds
    /// is the answer to "would the giver be left empty, and is that allowed".
    /// </remarks>
    /// <param name="giver">The army that is being emptied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="giver"/> is <see langword="null"/>.</exception>
    public void JoinStrongestFromArmy(Army giver)
    {
        ArgumentNullException.ThrowIfNull(giver);

        bool saveLast = giver.Commander?.IsHeroes == true;

        JoinStrongest(giver, saveLast);
    }

    /// <summary>
    /// Pulls as many stacks as will fit out of another army, <c>Army::MoveTroops()</c>
    /// (<c>army.cpp:1432-1526</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The "take everything" button of the two dialogs. Unlike <see cref="JoinStrongest"/> nothing is sorted or
    /// thrown away — what does not fit stays where it is — but two rules still apply: the receiver merges its
    /// own duplicates first, because a stack that could have been merged is a slot the incoming troops cannot
    /// use; and a hero's army keeps its last stack with a single creature in it.
    /// </para>
    /// <para>
    /// <paramref name="monsterIdToKeep"/> is the castle dialog's "leave the garrison its own creature" request.
    /// The reference concentrates that monster in the <i>source</i> before moving anything: if the receiver
    /// holds a stack of it and the source holds a stack of something else, the two are exchanged first, so that
    /// the monster to keep sits where the incoming stacks will not touch it.
    /// </para>
    /// <para>
    /// The moves happen in two passes, and the order matters: first everything except the monster to keep, then
    /// what is left of it. Otherwise the stacks that must stay could be pushed out by the ones that only might
    /// fit.
    /// </para>
    /// </remarks>
    /// <param name="from">The army to take from; it keeps whatever does not fit.</param>
    /// <param name="monsterIdToKeep">
    /// A monster that should stay in <paramref name="from"/>, <c>Monster::UNKNOWN</c> for none.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="from"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="from"/> is this army.</exception>
    public void MoveTroops(Army from, MonsterType monsterIdToKeep = MonsterType.Unknown)
    {
        ArgumentNullException.ThrowIfNull(from);

        if (ReferenceEquals(this, from))
        {
            throw new ArgumentException("An army cannot move troops from itself.", nameof(from));
        }

        // Combine the stacks of the same monster in one slot to leave more room for the incoming troops.
        MergeSameMonsterTroops();

        // Heroes need to have at least one occupied slot in their army, garrisons do not.
        bool fromHero = from.Commander?.IsHeroes == true;

        if (monsterIdToKeep != MonsterType.Unknown)
        {
            Troop? keep = null;
            Troop? exchange = null;

            foreach (Troop troop in _slots)
            {
                if (troop.IsValid && troop.IsMonster(monsterIdToKeep))
                {
                    keep = troop;
                    break;
                }
            }

            foreach (Troop troop in from._slots)
            {
                if (troop.IsValid && !troop.IsMonster(monsterIdToKeep))
                {
                    exchange = troop;
                    break;
                }
            }

            // Exchange the monster to keep for a stack of another monster, so that all the monsters to keep are
            // concentrated in the source army.
            if (keep is not null && exchange is not null)
            {
                uint count = keep.Count;

                keep.Reset();

                if (!JoinTroop(exchange.Monster, exchange.Count, false))
                {
                    Debug.Assert(false, "A slot was freed but refused the exchanged stack.");
                }

                exchange.Reset();

                if (!from.JoinTroop(monsterIdToKeep, count, false))
                {
                    Debug.Assert(false, "The source refused the monster it was supposed to keep.");
                }
            }
        }

        // First everything except the stacks of the monster that must stay...
        MoveTroopsInto(from, monsterIdToKeep, fromHero, true);

        // ...then as much of that monster as still fits, keeping the last creature of a hero's last stack.
        MoveTroopsInto(from, monsterIdToKeep, fromHero, false);
    }

    /// <summary>
    /// Swaps the two armies' stacks, <c>Army::SwapTroops( Army &amp; )</c> (<c>army.cpp:1528-1540</c>).
    /// </summary>
    /// <remarks>
    /// The stacks only: the race, the colour and the commander of each army stay where they are, because the
    /// reference's <c>Assign</c> copies the five stacks and nothing else. The reference asserts that both armies
    /// have at least one occupied slot — the caller (the hero-meeting dialog) guarantees it, since a swap that
    /// left a hero empty would end his game.
    /// </remarks>
    /// <param name="from">The army to swap with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="from"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="from"/> is this army.</exception>
    public void SwapTroops(Army from)
    {
        ArgumentNullException.ThrowIfNull(from);

        if (ReferenceEquals(this, from))
        {
            throw new ArgumentException("An army cannot swap troops with itself.", nameof(from));
        }

        Debug.Assert(IsValid && from.IsValid, "A swap must not leave an army empty.");

        Army temporary = new();

        temporary.AssignSlots(this);

        AssignSlots(from);
        from.AssignSlots(temporary);
    }

    /// <summary>
    /// Exchanges the contents of two stacks, <c>Army::SwapTroops( Troop &amp;, Troop &amp; )</c>
    /// (<c>army.cpp:1753-1756</c>), the reference's <c>std::swap</c> of two <c>Troop</c> objects.
    /// </summary>
    /// <param name="left">One stack.</param>
    /// <param name="right">The other stack.</param>
    /// <exception cref="ArgumentNullException">Either stack is <see langword="null"/>.</exception>
    public static void SwapTroops(Troop left, Troop right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (ReferenceEquals(left, right))
        {
            return;
        }

        (left.Monster, right.Monster) = (right.Monster, left.Monster);
        (left.Count, right.Count) = (right.Count, left.Count);
    }

    /// <summary>
    /// Copies the five stacks of another army over this one, <c>Troops::Assign( const Troops &amp; )</c>
    /// (<c>army.cpp:276-288</c>).
    /// </summary>
    /// <remarks>
    /// Slot by slot, and only the valid ones: an empty slot of the source leaves the target's slot empty, which
    /// is why the target is cleaned first. The commander is not touched — the reference's <c>Assign</c> is a
    /// <c>Troops</c> method, so the <c>Army</c>'s back-pointer survives the copy.
    /// </remarks>
    /// <param name="source">The army to copy from.</param>
    private void AssignSlots(Army source)
    {
        Clean();

        for (int slot = 0; slot < MaximumTroopCount; ++slot)
        {
            if (source._slots[slot].IsValid)
            {
                _slots[slot].Set(source._slots[slot]);
            }
        }
    }

    /// <summary>
    /// One pass of <see cref="MoveTroops"/>: walks the source and takes what fits.
    /// </summary>
    /// <remarks>
    /// The reference writes this as a lambda over <c>monsterIdToKeep</c>, <c>this</c> and <c>from</c>; the port
    /// spells the same thing as a method so that the two passes are one call each and the loop's bookkeeping is
    /// visible in one place.
    /// </remarks>
    /// <param name="from">The army to take from.</param>
    /// <param name="monsterIdToKeep">The monster the source should keep.</param>
    /// <param name="fromHero">Whether the source belongs to a hero.</param>
    /// <param name="ignoreMonstersToKeep">Whether the monster to keep is skipped.</param>
    private void MoveTroopsInto(Army from, MonsterType monsterIdToKeep, bool fromHero, bool ignoreMonstersToKeep)
    {
        uint stacksLeft = from.OccupiedSlotCount;

        foreach (Troop troop in from)
        {
            if (troop.IsEmpty)
            {
                continue;
            }

            if (ignoreMonstersToKeep && troop.IsMonster(monsterIdToKeep))
            {
                continue;
            }

            if (fromHero && stacksLeft == 1)
            {
                // The last valid stack of a hero: take all but one creature, then stop either way.
                if (JoinTroop(troop.Monster, troop.Count - 1, false))
                {
                    troop.Count = 1;
                }

                break;
            }

            if (JoinTroop(troop.Monster, troop.Count, false))
            {
                troop.Reset();

                --stacksLeft;
            }
        }
    }

    /// <summary>
    /// Orders stacks from the weakest to the strongest, <c>Army::WeakestTroop</c> as a sort predicate
    /// (<c>army.cpp:1733-1736</c>).
    /// </summary>
    /// <param name="troops">The stacks to order in place.</param>
    private static void SortWeakestFirst(List<Troop> troops) =>
        troops.Sort(static (left, right) => left.GetStrength().CompareTo(right.GetStrength()));
}
