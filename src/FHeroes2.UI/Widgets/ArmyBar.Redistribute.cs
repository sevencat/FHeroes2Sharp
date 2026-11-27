using FHeroes2.Game.Army;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Moving creatures between two slots, the anonymous namespace of <c>army_bar.cpp:55-186</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every one of the bar's three gestures — the split hotkey, the even split hotkey and the join hotkey — ends up
/// here, and so does a drag or a middle click onto another slot. The rules are worth reading closely because they
/// are the game's, not the widget's: two creatures in a slot dragged to an empty one leave one behind rather than
/// move as a pair, one creature dragged onto another slot of its own kind merges instead of swapping, and a hero
/// never gives away the last stack that holds his army together.
/// </para>
/// <para>
/// <b>The port passes the army explicitly.</b> The reference asks a stack for its army
/// (<c>troopFrom.GetArmy()</c>) because <c>ArmyTroop</c> keeps a back pointer. Slots in this port are plain
/// <see cref="Troop"/> values with no such pointer — the army owns its slots, not the other way round — so the two
/// armies travel as parameters. That is also why <c>ArmyBar</c> has to remember which army it shows.
/// </para>
/// <para>
/// <b>The split dialog is a request.</b> <c>Dialog::ArmySplitTroop()</c> blocks until the player picks a number of
/// slots and whether the stack is split evenly; the port's dialogs are models driven from outside, so
/// <see cref="RedistributeArmy"/> returns an <see cref="ArmyBarSplit"/> describing the question and
/// <see cref="ApplySplit"/> finishes the job once the answer is known. Everything the reference does before the
/// dialog has already happened by then; everything after it happens in <see cref="ApplySplit"/> and in the same
/// order.
/// </para>
/// </remarks>
public sealed partial class ArmyBar
{
    /// <summary>
    /// Moves creatures between the two slots of an army, <c>RedistributeArmy()</c> (<c>army_bar.cpp:55</c>).
    /// </summary>
    /// <remarks>
    /// Four cases before a dialog is ever needed: the whole of a one creature stack, one of two creatures into an
    /// empty slot, one onto one of the same kind, and everything else — which is where the split question is asked
    /// and <see cref="ApplySplit"/> takes over.
    /// </remarks>
    /// <param name="troopFrom">The slot the creatures come from.</param>
    /// <param name="armyFrom">The army that slot belongs to.</param>
    /// <param name="troopTarget">The slot they go to.</param>
    /// <param name="armyTarget">The army that slot belongs to, which may be the same one.</param>
    /// <returns>
    /// The split question to put to the player, or <see langword="null"/> when the move was already done — or when
    /// the rules refuse it.
    /// </returns>
    private static ArmyBarSplit? RedistributeArmy(Troop troopFrom, Army armyFrom, Troop troopTarget, Army armyTarget)
    {
        bool saveLastTroop = armyFrom.SaveLastTroop() && !ReferenceEquals(armyFrom, armyTarget);
        bool sameTroopType = troopTarget.IsValid && troopFrom.Monster == troopTarget.Monster;
        uint overallCount = sameTroopType ? troopFrom.Count + troopTarget.Count : troopFrom.Count;

        if (overallCount == 0)
        {
            return null;
        }

        if (overallCount == 1)
        {
            // The last stack of a hero's army stays where it is; otherwise a single creature simply changes slot.
            if (saveLastTroop)
            {
                return null;
            }

            Army.SwapTroops(troopFrom, troopTarget);

            return null;
        }

        if (!troopTarget.IsValid && troopFrom.Count == 2)
        {
            troopFrom.Count = 1;
            troopTarget.Set(troopFrom.Monster, 1);

            return null;
        }

        if (sameTroopType && troopFrom.Count == 1 && troopTarget.Count == 1)
        {
            if (saveLastTroop)
            {
                return null;
            }

            troopFrom.Reset();
            troopTarget.Count = 2;

            return null;
        }

        int freeSlots = 1 + Army.MaximumTroopCount - (int)armyTarget.OccupiedSlotCount;

        if (sameTroopType)
        {
            ++freeSlots;
        }

        int maxCount = (int)(saveLastTroop ? troopFrom.Count - 1 : troopFrom.Count);
        int suggestedCount = sameTroopType ? Math.Min(1, maxCount) : (int)troopFrom.Count / 2;
        bool useFastSplit = !sameTroopType;

        return new ArmyBarSplit(
            troopFrom,
            armyFrom,
            troopTarget,
            armyTarget,
            sameTroopType,
            (uint)overallCount,
            Math.Min(freeSlots, (int)overallCount),
            maxCount,
            suggestedCount,
            useFastSplit);
    }

    /// <summary>
    /// Finishes a redistribution once the player has answered the split question, <c>army_bar.cpp:103-142</c>.
    /// </summary>
    /// <remarks>
    /// The number of slots is the dialog's answer and <paramref name="useFastSplit"/> is the answer to its checkbox,
    /// which the player may have changed — the reference passes it by reference for exactly that reason. An even
    /// split fills the slots as evenly as the count allows, with the source taking the larger share, and hands what
    /// is left to the army, which knows where its free slots are.
    /// </remarks>
    /// <param name="split">The question that was answered.</param>
    /// <param name="slots">How many slots to split the creatures into; 2 to 6 to be honoured.</param>
    /// <param name="useFastSplit">Whether the player asked for an even split rather than the suggested count.</param>
    /// <param name="redistributeCount">
    /// How many creatures the player chose to move, or <see langword="null"/> for <see cref="ArmyBarSplit.SuggestedCount"/>.
    /// </param>
    /// <returns><see langword="true"/> when the slots changed.</returns>
    public static bool ApplySplit(ArmyBarSplit split, int slots, bool useFastSplit, int? redistributeCount = null)
    {
        ArgumentNullException.ThrowIfNull(split);

        if (slots < 2 || slots > 6)
        {
            return false;
        }

        Troop troopFrom = split.From;
        Troop troopTarget = split.Target;
        int chosenCount = redistributeCount ?? split.SuggestedCount;
        uint totalSplitTroopCount = troopFrom.Count;

        if (!useFastSplit && slots == 2)
        {
            // The count the player picked, which defaults to the suggested one.
            if (split.SameTroopType)
            {
                troopTarget.Count += (uint)chosenCount;
            }
            else
            {
                troopTarget.Set(troopFrom.Monster, (uint)chosenCount);
            }

            troopFrom.Count = totalSplitTroopCount - (uint)chosenCount;

            return true;
        }

        if (split.SameTroopType)
        {
            totalSplitTroopCount += troopTarget.Count;
        }

        // Round up on both ends: the source takes the larger share, the target the smaller one, and the rest is
        // divided by the army so that no creature is lost between the slots.
        uint fromSplitCount = (totalSplitTroopCount + (uint)slots - 1) / (uint)slots;
        troopFrom.Count = fromSplitCount;

        uint targetSplitCount = (totalSplitTroopCount + (uint)slots - 2) / (uint)slots;

        if (!split.SameTroopType)
        {
            troopTarget.Monster = troopFrom.Monster;
        }

        troopTarget.Count = targetSplitCount;

        totalSplitTroopCount -= fromSplitCount;
        totalSplitTroopCount -= targetSplitCount;

        if (slots > 2)
        {
            Troop rest = new();
            rest.Set(troopFrom.Monster, totalSplitTroopCount);

            split.TargetArmy.SplitTroopIntoFreeSlots(rest, troopTarget, (uint)(slots - 2));
        }

        return true;
    }

    /// <summary>
    /// Splits part of a stack into the first free slot of an army, <c>RedistributeTroopToFirstFreeSlot()</c>
    /// (<c>army_bar.cpp:146</c>).
    /// </summary>
    /// <param name="troopFrom">The slot to take creatures from.</param>
    /// <param name="armyTarget">The army whose free slot receives them.</param>
    /// <param name="count">How many creatures to move.</param>
    private static void RedistributeTroopToFirstFreeSlot(Troop troopFrom, Army armyTarget, uint count)
    {
        // A single creature is not a stack that can be split, and an empty one has nothing to give.
        if (troopFrom.Count <= 1 || count == 0)
        {
            return;
        }

        int freeSlots = Army.MaximumTroopCount - (int)armyTarget.OccupiedSlotCount;

        if (freeSlots == 0)
        {
            return;
        }

        armyTarget.AssignToFirstFreeSlot(troopFrom, count);
        troopFrom.Count -= count;
    }

    /// <summary>Moves one creature into the first free slot, <c>RedistributeTroopByOne()</c> (<c>army_bar.cpp:160</c>).</summary>
    /// <param name="troopFrom">The slot to take the creature from.</param>
    /// <param name="armyTarget">The army whose free slot receives it.</param>
    private static void RedistributeTroopByOne(Troop troopFrom, Army armyTarget) =>
        RedistributeTroopToFirstFreeSlot(troopFrom, armyTarget, 1);

    /// <summary>Moves half a stack into the first free slot, <c>RedistributeTroopEvenly()</c> (<c>army_bar.cpp:165</c>).</summary>
    /// <param name="troopFrom">The slot to take the creatures from.</param>
    /// <param name="armyTarget">The army whose free slot receives them.</param>
    private static void RedistributeTroopEvenly(Troop troopFrom, Army armyTarget) =>
        RedistributeTroopToFirstFreeSlot(troopFrom, armyTarget, troopFrom.Count / 2);

    /// <summary>
    /// Acts on whichever redistribution hotkey is held, <c>IsSplitHotkeyUsed()</c> (<c>army_bar.cpp:170</c>).
    /// </summary>
    /// <remarks>
    /// The order matters: split by one, then join, then split by half. The reference asks the three hotkey states in
    /// that order and stops at the first that answers, so a player who holds two of them gets the first.
    /// </remarks>
    /// <param name="troopFrom">The slot under the cursor.</param>
    /// <param name="armyTarget">The army the cursor is over.</param>
    /// <param name="hotKey">The hotkey state of this frame.</param>
    /// <returns><see langword="true"/> when a hotkey acted.</returns>
    private static bool IsSplitHotkeyUsed(Troop troopFrom, Army armyTarget, ArmyBarHotKey hotKey)
    {
        switch (hotKey)
        {
        case ArmyBarHotKey.SplitStackByOne:
            RedistributeTroopByOne(troopFrom, armyTarget);

            return true;

        case ArmyBarHotKey.JoinStacks:
            armyTarget.JoinAllTroopsOfType(troopFrom);

            return true;

        case ArmyBarHotKey.SplitStackByHalf:
            RedistributeTroopEvenly(troopFrom, armyTarget);

            return true;

        default:
            return false;
        }
    }
}
