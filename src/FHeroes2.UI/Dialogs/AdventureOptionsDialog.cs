namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The four adventure options and cancel, <c>Dialog::AdventureOptions()</c> (<c>dialog.h:87</c>,
/// <c>dialog_adventure.cpp:38-121</c>).
/// </summary>
/// <remarks>
/// <para>
/// The dialog is four big buttons (<c>BUTTONS_ADVENTURE_OPTIONS_DIALOG</c>) plus a cancel button in the bottom
/// centre, so the port models it as a button set and a mapping from the button index to the result - the reference
/// spells the same thing out as four <c>if</c> branches in its event loop.
/// </para>
/// <para>
/// The model stays free of artwork so that the rule layer can decide what the four buttons do without the UI
/// layer; <see cref="AdventureOptionsView"/> composes the window and the buttons out of archive frames and owns
/// the layout.
/// </para>
/// </remarks>
public sealed class AdventureOptionsDialog
{
    private AdventureOptionsDialog(bool isDigEnabled)
    {
        IsDigEnabled = isDigEnabled;
    }

    /// <summary>
    /// Builds the dialog, <c>Dialog::AdventureOptions( const bool enableDig )</c> (<c>dialog_adventure.cpp:117</c>).
    /// </summary>
    /// <param name="enableDig">
    /// Whether the dig button is usable, <c>enableDig</c> of the reference: a hero has to stand on diggable ground and
    /// no one has dug there yet.
    /// </param>
    /// <returns>The model.</returns>
    public static AdventureOptionsDialog Create(bool enableDig) => new(enableDig);

    /// <summary>Whether the dig button can be pressed.</summary>
    public bool IsDigEnabled { get; }

    /// <summary>The buttons the dialog has, in the order of <c>BUTTONS_ADVENTURE_OPTIONS_DIALOG</c>.</summary>
    public DialogButton Buttons =>
        IsDigEnabled
            ? DialogButton.World | DialogButton.Puzzle | DialogButton.Info | DialogButton.Dig | DialogButton.Cancel
            : DialogButton.World | DialogButton.Puzzle | DialogButton.Info | DialogButton.Cancel;

    /// <summary>
    /// The result of pressing a button, the four <c>result = ...</c> assignments of <c>dialog_adventure.cpp:80-98</c>.
    /// </summary>
    /// <param name="index">Index of the pressed button: 0 world, 1 puzzle, 2 information, 3 dig, 4 cancel.</param>
    /// <returns>
    /// One of <see cref="DialogButton.World"/>, <see cref="DialogButton.Puzzle"/>, <see cref="DialogButton.Info"/>,
    /// <see cref="DialogButton.Dig"/> or <see cref="DialogButton.Cancel"/>.
    /// </returns>
    public static DialogButton FromButton(int index) => index switch
    {
        0 => DialogButton.World,
        1 => DialogButton.Puzzle,
        2 => DialogButton.Info,
        3 => DialogButton.Dig,
        _ => DialogButton.Cancel,
    };
}
