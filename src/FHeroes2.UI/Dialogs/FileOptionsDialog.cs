using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What the file options dialog decided, the <c>fheroes2::GameMode</c> values <c>Dialog::FileOptions()</c> can return
/// (<c>dialog_file.cpp:74-151</c>).
/// </summary>
public enum FileOptionsResult
{
    /// <summary>The dialog was closed, <c>fheroes2::GameMode::CANCEL</c>.</summary>
    Cancel = 0,

    /// <summary>A new game should start, <c>fheroes2::GameMode::NEW_GAME</c>.</summary>
    NewGame = 1,

    /// <summary>A saved game should be loaded, <c>fheroes2::GameMode::LOAD_GAME</c>.</summary>
    LoadGame = 2,

    /// <summary>The game asks for a file name and saves, <c>EventSaveGame()</c>.</summary>
    SaveGame = 3,

    /// <summary>The game saves without asking for a name, <c>Game::QuickSave()</c>.</summary>
    QuickSave = 4,

    /// <summary>The application should quit, <c>fheroes2::GameMode::QUIT_GAME</c>.</summary>
    Quit = 5,
}

/// <summary>
/// The six options of the file menu, <c>Dialog::FileOptions()</c> (<c>dialog.h:88</c>,
/// <c>dialog_file.cpp:42-159</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference returns a whole <c>fheroes2::GameMode</c> because the caller is the game loop; the port has scenes
/// instead of a game mode <c>switch</c>, so the five outcomes that this dialog can produce are spelled out as
/// <see cref="FileOptionsResult"/>. <c>MAIN_MENU</c>, <c>LOAD_STANDARD</c> and the campaign values of
/// <c>GameMode</c> are never produced here and are therefore not part of the result.
/// </para>
/// <para>
/// <b>Contract only</b> (ADR-163): inputs and answer, no layout yet.
/// </para>
/// </remarks>
public sealed class FileOptionsDialog
{
    private FileOptionsDialog(bool isRestartEnabled)
    {
        IsRestartEnabled = isRestartEnabled;
    }

    /// <summary>
    /// Builds the dialog, <c>selectFileOption()</c> (<c>dialog_file.cpp:42</c>).
    /// </summary>
    /// <param name="isRestartEnabled">
    /// Whether the restart button works. The reference disables it unconditionally
    /// (<c>restartGameButton.disable()</c>, <c>dialog_file.cpp:63</c>) because restarting a game is not implemented,
    /// so the port keeps the flag but defaults it to <see langword="false"/>.
    /// </param>
    /// <returns>The model.</returns>
    public static FileOptionsDialog Create(bool isRestartEnabled = false) => new(isRestartEnabled);

    /// <summary>Whether the restart button can be pressed.</summary>
    public bool IsRestartEnabled { get; }

    /// <summary>The buttons the dialog has, in the order of <c>BUTTONS_FILE_DIALOG</c>.</summary>
    public DialogButton Buttons => DialogButton.Cancel;

    /// <summary>
    /// The result of pressing a button, <c>dialog_file.cpp:83-126</c>.
    /// </summary>
    /// <param name="index">
    /// Index of the pressed button: 0 new game, 1 load game, 2 restart, 3 save game, 4 quick save, 5 quit,
    /// 6 cancel. Indices 2 and 4 fall back to <see cref="FileOptionsResult.Cancel"/> when the option they stand for
    /// has nothing to do - the reference does the same after showing its "under construction" message.
    /// </param>
    /// <returns>The outcome.</returns>
    public static FileOptionsResult FromButton(int index) => index switch
    {
        0 => FileOptionsResult.NewGame,
        1 => FileOptionsResult.LoadGame,
        3 => FileOptionsResult.SaveGame,
        4 => FileOptionsResult.QuickSave,
        5 => FileOptionsResult.Quit,
        _ => FileOptionsResult.Cancel,
    };

    /// <summary>
    /// The result of pressing a button of <see cref="FileOptionsView"/>, <see cref="FromButton"/> keyed by
    /// the identity the port gives the buttons instead of their index.
    /// </summary>
    /// <param name="button">Button that was released.</param>
    /// <returns>
    /// The outcome; <see cref="FileOptionsResult.Cancel"/> for cancel, the disabled restart button and
    /// anything else.
    /// </returns>
    public static FileOptionsResult FromDialogButton(DialogButton button) => button switch
    {
        DialogButton.NewGame => FileOptionsResult.NewGame,
        DialogButton.LoadGame => FileOptionsResult.LoadGame,
        DialogButton.SaveGame => FileOptionsResult.SaveGame,
        DialogButton.QuickSave => FileOptionsResult.QuickSave,
        DialogButton.Quit => FileOptionsResult.Quit,
        _ => FileOptionsResult.Cancel,
    };

    /// <summary>
    /// Runs the dialog until a button closes it, the event loop of <c>selectFileOption()</c>
    /// (<c>dialog_file.cpp:78-149</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference does more than close on a click: new game and load game go through
    /// <c>EventNewGame()</c> / <c>EventLoadGame()</c> first, and their own windows can keep the file menu
    /// open. The port has no game loop to call yet, so <see cref="Show"/> answers as soon as a button is
    /// released and leaves those windows to the caller, which is also what
    /// <see cref="AdventureOptionsDialog.Show"/> does.
    /// </para>
    /// <para>
    /// Save game hides the window before it returns in the reference; there is nothing to hide here,
    /// the caller is expected to draw its own frame over the answer.
    /// </para>
    /// </remarks>
    /// <param name="target">Frame to draw into and to submit; it must already contain the scene behind.</param>
    /// <param name="view">The window and the buttons, built by <see cref="FileOptionsView.TryCreate"/>.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <returns>
    /// The outcome of the pressed button, or <see cref="FileOptionsResult.Quit"/> when the client was asked
    /// to exit before the player answered - the reference keeps <c>QUIT_GAME</c> in that case.
    /// </returns>
    /// <exception cref="InvalidOperationException">No <see cref="IEventEngine"/> is attached.</exception>
    public static FileOptionsResult Show(PixelBuffer target, FileOptionsView view, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(view);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine ?? throw new InvalidOperationException(
            "FileOptionsDialog.Show() needs an event engine: attach LocalEvent.Engine first.");

        DialogButton answer = DialogButton.None;
        bool leftDown = false;

        while (answer == DialogButton.None)
        {
            if (!eventState.HandleEvents(sleepAfterEventProcessing: false))
            {
                return FileOptionsResult.Quit;
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

            if (answer == DialogButton.None && eventState.IsKeyPressed(Key.Escape))
            {
                answer = DialogButton.Cancel;
            }

            view.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return FromDialogButton(answer);
    }
}
