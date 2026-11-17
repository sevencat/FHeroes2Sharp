using FHeroes2.Game.Army;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The unit information window, <c>Dialog::ArmyInfo()</c> (<c>dialog.h:127</c>, <c>dialog_armyinfo.cpp:515</c>).
/// </summary>
/// <remarks>
/// <para>
/// The <c>flags</c> of the reference carry two different things at once: <c>Dialog::BUTTONS</c>
/// (<c>YES|OK|NO|CANCEL</c>) says whether the window has buttons at all, and <c>Dialog::UPGRADE</c> /
/// <c>Dialog::DISMISS</c> say which of the two small buttons it shows. The port keeps both in one
/// <see cref="DialogButton"/> parameter, which is what the flags are anyway.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class ArmyInfoDialog
{
    private ArmyInfoDialog(Troop troop, DialogButton flags, bool isReflected)
    {
        Troop = troop;
        Flags = flags;
        IsReflected = isReflected;
    }

    /// <summary>
    /// Builds the window, <c>Dialog::ArmyInfo()</c> (<c>dialog_armyinfo.cpp:515</c>).
    /// </summary>
    /// <param name="troop">The unit to show.</param>
    /// <param name="flags">
    /// Which buttons the window has: <see cref="DialogButton.None"/> for a plain information window, or
    /// <see cref="DialogButton.Ok"/> combined with <see cref="DialogButton.Upgrade"/> and/or
    /// <see cref="DialogButton.Dismiss"/> for one that can act on the unit.
    /// </param>
    /// <param name="isReflected">Whether the unit sprite is drawn mirrored, <c>isReflected</c> of the reference.</param>
    /// <returns>The model.</returns>
    public static ArmyInfoDialog Create(Troop troop, DialogButton flags = DialogButton.None, bool isReflected = false) =>
        new(troop, flags, isReflected);

    /// <summary>The unit to show.</summary>
    public Troop Troop { get; }

    /// <summary>Which buttons the window has.</summary>
    public DialogButton Flags { get; }

    /// <summary>Whether the unit sprite is mirrored.</summary>
    public bool IsReflected { get; }

    /// <summary>Whether the window has buttons at all, <c>flags &amp; Dialog::BUTTONS</c> of the reference.</summary>
    public bool HasButtons =>
        (Flags & (DialogButton.Yes | DialogButton.Ok | DialogButton.No | DialogButton.Cancel)) != 0;

    /// <summary>
    /// The result of pressing a button, <c>result = Dialog::UPGRADE</c> and its neighbours
    /// (<c>dialog_armyinfo.cpp:636-670</c>).
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 upgrade, 1 dismiss, 2 exit.</param>
    /// <returns>
    /// <see cref="DialogButton.Upgrade"/>, <see cref="DialogButton.Dismiss"/> or <see cref="DialogButton.Cancel"/>.
    /// </returns>
    public static DialogButton FromButton(int index) => index switch
    {
        0 => DialogButton.Upgrade,
        1 => DialogButton.Dismiss,
        _ => DialogButton.Cancel,
    };
}
