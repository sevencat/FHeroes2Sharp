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
}
