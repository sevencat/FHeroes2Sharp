using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

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

    /// <summary>
    /// Runs the dialog until a button closes it, the event loop of <c>Dialog::AdventureOptions()</c>
    /// (<c>dialog_adventure.cpp:55-110</c>).
    /// </summary>
    /// <remarks>
    /// The reference builds the window, polls the events in a <c>while ( le.HandleEvents() )</c> loop and
    /// asks <c>le.MouseClickLeft( area )</c> for every button; the port keeps the inputs in
    /// <see cref="AdventureOptionsView"/> and only adds the frame. The dig button is skipped when
    /// <paramref name="enableDig"/> is <see langword="false"/>, the disabled button of the reference.
    /// </remarks>
    /// <param name="target">Frame to draw into and to submit; it must already contain the scene behind.</param>
    /// <param name="view">The window and the buttons, built by <see cref="AdventureOptionsView.TryCreate"/>.</param>
    /// <param name="enableDig">Whether the dig button answers, <see cref="IsDigEnabled"/>.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// The pressed button, or <see cref="DialogButton.None"/> when the client was asked to exit before
    /// the player answered.
    /// </returns>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public static DialogButton Show(PixelBuffer target, AdventureOptionsView view, bool enableDig = true,
        LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(view);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "AdventureOptionsDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        DialogButton answer = DialogButton.None;
        bool leftDown = false;

        while (answer == DialogButton.None)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return DialogButton.None;
            }

            if (eventState.HasMouseMoved)
            {
                view.SetCursor(eventState.CursorPosition);
            }

            bool down = eventState.IsMouseLeftButtonPressed;

            if (down != leftDown)
            {
                leftDown = down;
                answer = view.SetLeftButton(down, eventState.CursorPosition);
            }

            if (answer == DialogButton.Dig && !enableDig)
            {
                // The reference leaves the dig button disabled; a click on it does nothing.
                answer = DialogButton.None;
            }

            if (answer == DialogButton.None && eventState.IsKeyPressed(Key.Escape))
            {
                answer = DialogButton.Cancel;
            }

            view.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return answer;
    }
}
