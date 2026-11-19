using FHeroes2.Game.Army;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The question <c>Dialog::ArmySplitTroop()</c> asks when a stack is split, as a value instead of a blocking call
/// (<c>army_bar.cpp:96-102</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference builds five arguments for the dialog and then reads two of them back — the number of slots and the
/// state of the "even split" checkbox. The port's dialogs are models driven from outside, so those five arguments
/// are this record and the two answers are the parameters of <see cref="ArmyBar.ApplySplit"/>. A screen that shows
/// the split model therefore owns the answer: it reads <see cref="Slots"/>, <see cref="MaxCount"/> and
/// <see cref="SuggestedCount"/> to lay the question out, and passes the player's answer back to the bar.
/// </para>
/// <para>
/// <see cref="From"/> and <see cref="Target"/> are the very slot objects of the two armies, not copies: the slots are
/// mutated in place when the answer arrives, which is how the reference's pointers behave.
/// </para>
/// </remarks>
public sealed class ArmyBarSplit
{
    /// <summary>
    /// Describes a split question, <c>RedistributeArmy()</c> (<c>army_bar.cpp:96</c>).
    /// </summary>
    /// <param name="from">The slot the creatures come from.</param>
    /// <param name="fromArmy">The army that slot belongs to.</param>
    /// <param name="target">The slot they go to.</param>
    /// <param name="targetArmy">The army that slot belongs to.</param>
    /// <param name="sameTroopType">Whether both slots hold the same creature.</param>
    /// <param name="overallCount">The creatures the split is about, both stacks when they are of the same kind.</param>
    /// <param name="slots">How many slots the creatures could be spread over.</param>
    /// <param name="maxCount">The largest number of slots the dialog offers.</param>
    /// <param name="suggestedCount">The number the dialog starts with.</param>
    /// <param name="useFastSplit">Whether the dialog starts with "even split" checked.</param>
    public ArmyBarSplit(Troop from, Army fromArmy, Troop target, Army targetArmy, bool sameTroopType,
        uint overallCount, int slots, int maxCount, int suggestedCount, bool useFastSplit)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(fromArmy);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetArmy);

        From = from;
        FromArmy = fromArmy;
        Target = target;
        TargetArmy = targetArmy;
        SameTroopType = sameTroopType;
        OverallCount = overallCount;
        Slots = slots;
        MaxCount = maxCount;
        SuggestedCount = suggestedCount;
        UseFastSplit = useFastSplit;
    }

    /// <summary>The slot the creatures come from.</summary>
    public Troop From { get; }

    /// <summary>The army <see cref="From"/> belongs to.</summary>
    public Army FromArmy { get; }

    /// <summary>The slot they go to.</summary>
    public Troop Target { get; }

    /// <summary>The army <see cref="Target"/> belongs to, which may be <see cref="FromArmy"/>.</summary>
    public Army TargetArmy { get; }

    /// <summary>Whether both slots hold the same creature, in which case they are merged rather than swapped.</summary>
    public bool SameTroopType { get; }

    /// <summary>The creatures the split is about.</summary>
    public uint OverallCount { get; }

    /// <summary>How many slots the army can spread the creatures over, <c>freeSlots</c>.</summary>
    public int Slots { get; }

    /// <summary>The largest number of slots the dialog offers, <c>maxCount</c>.</summary>
    public int MaxCount { get; }

    /// <summary>The number of slots the dialog suggests, <c>suggestedCount</c>.</summary>
    public int SuggestedCount { get; }

    /// <summary>
    /// Whether the "even split" checkbox is checked, <c>useFastSplit</c>.
    /// </summary>
    /// <remarks>
    /// The reference passes this by reference and the dialog may flip it; the same holds here, so a screen may write
    /// the player's choice before handing the answer to <see cref="ArmyBar.ApplySplit"/>.
    /// </remarks>
    public bool UseFastSplit { get; set; }
}
