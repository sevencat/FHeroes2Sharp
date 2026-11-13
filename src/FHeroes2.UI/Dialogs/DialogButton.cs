namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Result of a dialog button, a port of the <c>Dialog</c> enum of
/// <c>src/fheroes2/dialog/dialog.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// The numbers are the bit flags of the reference, and for the same reason: the very same values
/// describe which buttons a dialog has (<c>Dialog::OK | Dialog::CANCEL</c>) and what a press on one
/// of them returns. A <see cref="Widgets.ButtonGroup"/> stores one value per button and hands it
/// back when that button is clicked or its hot key is pressed.
/// </para>
/// <para>
/// Only the four button flags are ported. The layout flags of the original (<c>Dismiss</c>,
/// <c>Upgrade</c>, <c>Prev</c>, <c>Next</c>, ...) are added together with the dialogs that place
/// those buttons, so no value is invented ahead of its use.
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
}
