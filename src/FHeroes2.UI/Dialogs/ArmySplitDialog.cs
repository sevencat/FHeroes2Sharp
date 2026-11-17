namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the split dialog answered, <c>Dialog::ArmySplitTroop()</c> (<c>dialog_selectcount.cpp:462-651</c>).
/// </summary>
/// <param name="Button">The pressed button.</param>
/// <param name="RedistributeCount">
/// The number of units the split moves, the <c>redistributeCount</c> out parameter of the reference.
/// </param>
/// <param name="UseFastSplit">
/// Whether the player switched to the fast split mode, the <c>useFastSplit</c> out parameter of the reference.
/// </param>
public sealed record ArmySplitResult(DialogButton Button, int RedistributeCount, bool UseFastSplit);

/// <summary>
/// Splitting a stack between two slots, <c>Dialog::ArmySplitTroop()</c> (<c>dialog.h:130</c>,
/// <c>dialog_selectcount.cpp:462</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference returns the pressed button and writes the two values it computed into out parameters; the port
/// returns them together as an <see cref="ArmySplitResult"/>. The dialog itself is a number picker with a min button
/// and a max button, and the fast split mode - so the result's
/// <see cref="ArmySplitResult.RedistributeCount"/> is only meaningful together with
/// <see cref="ArmySplitResult.Button"/>.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class ArmySplitDialog
{
    private ArmySplitDialog(int freeSlots, int redistributeMaximum, string troopName)
    {
        FreeSlots = freeSlots;
        RedistributeMaximum = redistributeMaximum;
        TroopName = troopName;
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::ArmySplitTroop()</c> (<c>dialog_selectcount.cpp:462</c>).
    /// </summary>
    /// <param name="freeSlots">How many army slots are empty, <c>freeSlots</c> of the reference.</param>
    /// <param name="redistributeMaximum">
    /// Largest number of units that can be moved, <c>redistributeMax</c> of the reference.
    /// </param>
    /// <param name="troopName">Name of the unit, used in the message the dialog shows.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="freeSlots"/> or <paramref name="redistributeMaximum"/> is negative.
    /// </exception>
    public static ArmySplitDialog Create(int freeSlots, int redistributeMaximum, string troopName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(freeSlots);
        ArgumentOutOfRangeException.ThrowIfNegative(redistributeMaximum);

        return new ArmySplitDialog(freeSlots, redistributeMaximum, troopName);
    }

    /// <summary>How many army slots are empty.</summary>
    public int FreeSlots { get; }

    /// <summary>Largest number of units that can be moved.</summary>
    public int RedistributeMaximum { get; }

    /// <summary>Name of the unit.</summary>
    public string TroopName { get; }

    /// <summary>The buttons the dialog has.</summary>
    public DialogButton Buttons => DialogButton.Ok | DialogButton.Cancel;

    /// <summary>
    /// The result of pressing a button, <c>return result</c> at <c>dialog_selectcount.cpp:651</c>.
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 OK, 1 cancel.</param>
    /// <param name="redistributeCount">The number of units the split moves.</param>
    /// <param name="useFastSplit">Whether the fast split mode was chosen.</param>
    /// <returns>The result triple.</returns>
    public static ArmySplitResult FromButton(int index, int redistributeCount, bool useFastSplit) =>
        new(index == 0 ? DialogButton.Ok : DialogButton.Cancel, redistributeCount, useFastSplit);
}
