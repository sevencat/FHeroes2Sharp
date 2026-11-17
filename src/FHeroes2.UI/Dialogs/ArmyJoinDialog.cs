using FHeroes2.Game.Army;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// "Do you accept?", <c>Dialog::ArmyJoinFree()</c> and <c>Dialog::ArmyJoinWithCost()</c>
/// (<c>dialog.h:128-129</c>, <c>dialog_armyinfo.cpp:726-795</c>).
/// </summary>
/// <remarks>
/// <para>
/// The two functions differ only in whether the offer costs gold: the free one is a plain yes/no message box
/// (<c>fheroes2::showStandardTextMessage</c>), the paid one draws the same message plus a gold pile and a sum. The
/// port models them as one class whose <see cref="Gold"/> is <c>0</c> for the free variant.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet, and no translated message text either - the
/// reference builds three different sentences from <see cref="JoinCount"/> and <see cref="Troop"/>, which belongs to
/// the implementation.
/// </para>
/// </remarks>
public sealed class ArmyJoinDialog
{
    private ArmyJoinDialog(Troop troop, uint joinCount, uint gold)
    {
        Troop = troop;
        JoinCount = joinCount;
        Gold = gold;
    }

    /// <summary>
    /// Builds the free offer, <c>Dialog::ArmyJoinFree()</c> (<c>dialog_armyinfo.cpp:726</c>).
    /// </summary>
    /// <param name="troop">The army that wants to join.</param>
    /// <returns>The model.</returns>
    public static ArmyJoinDialog CreateFree(Troop troop) => new(troop, troop.Count, 0);

    /// <summary>
    /// Builds the paid offer, <c>Dialog::ArmyJoinWithCost()</c> (<c>dialog_armyinfo.cpp:734</c>).
    /// </summary>
    /// <param name="troop">The army that is being hired.</param>
    /// <param name="joinCount">How many units join, <c>join</c> of the reference; must be greater than zero.</param>
    /// <param name="gold">The price, <c>gold</c> of the reference.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="joinCount"/> is zero.</exception>
    public static ArmyJoinDialog CreateWithCost(Troop troop, uint joinCount, uint gold)
    {
        ArgumentOutOfRangeException.ThrowIfZero(joinCount);

        return new ArmyJoinDialog(troop, joinCount, gold);
    }

    /// <summary>The army that is offered.</summary>
    public Troop Troop { get; }

    /// <summary>How many units join.</summary>
    public uint JoinCount { get; }

    /// <summary>The price in gold, <c>0</c> for the free variant.</summary>
    public uint Gold { get; }

    /// <summary>Whether the offer costs gold.</summary>
    public bool IsPaid => Gold > 0;

    /// <summary>The buttons the dialog has, <c>Dialog::YES | Dialog::NO</c> of both variants.</summary>
    public DialogButton Buttons => DialogButton.Yes | DialogButton.No;

    /// <summary>
    /// The result of pressing a button, the <c>btnGroup.processEvents()</c> of the reference.
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 yes, 1 no.</param>
    /// <returns><see cref="DialogButton.Yes"/> or <see cref="DialogButton.No"/>.</returns>
    public static DialogButton FromButton(int index) =>
        index == 0 ? DialogButton.Yes : DialogButton.No;
}
