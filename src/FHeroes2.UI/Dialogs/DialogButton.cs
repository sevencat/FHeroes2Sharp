namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Result of a dialog button, a port of the <c>Dialog</c> enum of
/// <c>src/fheroes2/dialog/dialog.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// The numbers are the bit flags of the reference, and for the same reason: the very same values describe which
/// buttons a dialog has (<c>Dialog::OK | Dialog::CANCEL</c>) and what a press on one of them returns. A
/// <see cref="Widgets.ButtonGroup"/> stores one value per button and hands it back when that button is clicked or
/// its hot key is pressed.
/// </para>
/// <para>
/// The four button flags and the nine layout and action flags cover the whole enum (<c>dialog.h:65-85</c>). The
/// reference also aliases <c>Dialog::MAX</c> as <c>UPGRADE_DISABLE</c>; the port uses <see cref="Max"/> for both
/// instead of adding a second name for the same bit. The layout flags are not magic numbers invented here, they are
/// what <c>Dialog::BUTTONS</c> and the dialogs of <c>dialog.h</c> are built from.
/// </para>
/// </remarks>
[Flags]
public enum DialogButton
{
    /// <summary>No button at all, <c>Dialog::ZERO</c>; also what an unfinished button group returns.</summary>
    None = 0x0000,

    /// <summary>Yes, <c>Dialog::YES</c>.</summary>
    Yes = 0x0001,

    /// <summary>OK, <c>Dialog::OK</c>.</summary>
    Ok = 0x0002,

    /// <summary>No, <c>Dialog::NO</c>.</summary>
    No = 0x0004,

    /// <summary>Cancel, <c>Dialog::CANCEL</c>.</summary>
    Cancel = 0x0008,

    /// <summary>Dismiss the troop, <c>Dialog::DISMISS</c>; a result of the army info dialog.</summary>
    Dismiss = 0x0010,

    /// <summary>Upgrade the troop, <c>Dialog::UPGRADE</c>; a result of the army info dialog.</summary>
    Upgrade = 0x0020,

    /// <summary>
    /// The "max" button, <c>Dialog::MAX</c>; also what the reference calls
    /// <c>Dialog::UPGRADE_DISABLE</c> when an upgrade is offered but impossible.
    /// </summary>
    Max = 0x0040,

    /// <summary>Previous, <c>Dialog::PREV</c>.</summary>
    Prev = 0x0080,

    /// <summary>Next, <c>Dialog::NEXT</c>.</summary>
    Next = 0x0100,

    /// <summary>Open the world map, <c>Dialog::WORLD</c>; a result of the adventure options dialog.</summary>
    World = 0x0200,

    /// <summary>Open the puzzle, <c>Dialog::PUZZLE</c>; a result of the adventure options dialog.</summary>
    Puzzle = 0x0400,

    /// <summary>Open the scenario information, <c>Dialog::INFO</c>; a result of the adventure options dialog.</summary>
    Info = 0x0800,

    /// <summary>Dig for the ultimate artifact, <c>Dialog::DIG</c>; a result of the adventure options dialog.</summary>
    Dig = 0x1000,
}
