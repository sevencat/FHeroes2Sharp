using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The file menu of the game as it is drawn: a stone window, the six generated buttons of
/// <c>BUTTONS_FILE_DIALOG</c> in two columns and the cancel button under them,
/// <c>selectFileOption()</c> (<c>dialog_file.cpp:42-152</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference generates the six plates from their labels at asset load time
/// (<c>makeSymmetricBackgroundSprites</c>, <c>game_assets.cpp:1563-1578</c>) and lays them out with
/// <c>StandardWindow</c>. The port has no generated-asset cache, so this class takes the twelve frames
/// the caller produced with <see cref="ButtonFactory.MakeSymmetricBackgroundSprites"/> and only owns the
/// layout, exactly like <see cref="AdventureOptionsView"/> takes the frames of <c>APANEL.ICN</c>.
/// </para>
/// <para>
/// The restart button is disabled, <c>restartGameButton.disable()</c> of
/// <c>dialog_file.cpp:62-63</c>: the reference keeps it in the layout but never acts on it.
/// </para>
/// </remarks>
public sealed class FileOptionsView
{
    /// <summary>
    /// Smallest width the generated plates are stretched to, the <c>80</c> of
    /// <c>game_assets.cpp:1576</c>.
    /// </summary>
    public const int MinimumButtonWidth = 80;

    /// <summary>Frames the six buttons take, released and pressed each.</summary>
    public const int ButtonsFrameCount = 12;

    /// <summary>Archive entry that holds the cancel button, the <c>ICN::SYSTEM</c> of the reference.</summary>
    public const string CancelAssetName = "SYSTEM.ICN";

    /// <summary>Frame of the released cancel button in <see cref="CancelAssetName"/>.</summary>
    /// <remarks>
    /// Frames 3 and 4 of the archive hold CANCEL, the pair <see cref="AdventureOptionsView"/> uses.
    /// </remarks>
    public const int CancelReleasedFrame = 3;

    /// <summary>Frame of the pressed cancel button in <see cref="CancelAssetName"/>.</summary>
    public const int CancelPressedFrame = 4;

    /// <summary>
    /// Distance of the cancel button from the bottom centre of the window, the <c>{ 0, 11 }</c> of
    /// <c>dialog_file.cpp:69</c>.
    /// </summary>
    public static Point CancelOffset => new(0, 11);

    /// <summary>Labels of the six buttons, in the order of <c>BUTTONS_FILE_DIALOG</c>.</summary>
    /// <remarks>
    /// The reference passes <c>gettext_noop()</c> strings; the port has no translation layer yet, so
    /// these are the English originals of <c>game_assets.cpp:1570-1575</c>.
    /// </remarks>
    public static IReadOnlyList<string> ButtonLabels => Labels;

    private static readonly string[] Labels =
        ["NEW\nGAME", "LOAD\nGAME", "RESTART\nGAME", "SAVE\nGAME", "QUICK\nSAVE", "QUIT"];

    /// <summary>Identity of the six buttons, in the order of <see cref="ButtonLabels"/>.</summary>
    public static IReadOnlyList<DialogButton> ButtonValues => Values;

    private static readonly DialogButton[] Values =
    [
        DialogButton.NewGame, DialogButton.LoadGame, DialogButton.Restart,
        DialogButton.SaveGame, DialogButton.QuickSave, DialogButton.Quit,
    ];

    /// <summary>Index of the restart button in <see cref="ButtonValues"/>.</summary>
    public const int RestartButtonIndex = 2;

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly ButtonSprite _cancel;
    private bool _cancelPressed;

    private FileOptionsView(StandardWindow window, ButtonGroup buttons, ButtonSprite cancel)
    {
        _window = window;
        _buttons = buttons;
        _cancel = cancel;
    }

    /// <summary>The six option buttons, already placed in the window.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The cancel button, already placed in the window.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>Area the window content goes into.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>
    /// Composes the dialog from the generated frames and the cancel button of the archive.
    /// </summary>
    /// <param name="buttonFrames">
    /// Frames of the six generated plates: released then pressed per button, twelve of them.
    /// </param>
    /// <param name="systemFrames">Frames of <see cref="CancelAssetName"/>, five at least.</param>
    /// <param name="isRestartEnabled">
    /// Whether the restart button can be pressed; the reference always disables it
    /// (<c>dialog_file.cpp:63</c>).
    /// </param>
    /// <returns><see langword="null"/> when an input is missing or too short.</returns>
    public static FileOptionsView? TryCreate(
        IReadOnlyList<Sprite>? buttonFrames, IReadOnlyList<Sprite>? systemFrames, bool isRestartEnabled = false)
    {
        if (buttonFrames is null || systemFrames is null)
        {
            return null;
        }

        if (buttonFrames.Count < ButtonsFrameCount || systemFrames.Count <= CancelPressedFrame)
        {
            return null;
        }

        ButtonGroup buttons = ButtonGroup.CreateFromFrames(
            [.. buttonFrames.Take(ButtonsFrameCount)], Values);

        if (!isRestartEnabled)
        {
            buttons[RestartButtonIndex].Disable();
        }

        StandardWindow window = StandardWindow.CreateForSymmetricButtons(buttons);
        window.PlaceSymmetricButtons(buttons);

        ButtonSprite cancel = new(systemFrames[CancelReleasedFrame], systemFrames[CancelPressedFrame]);
        Point cancelPosition = window.RenderPosition(
            CancelOffset, new Size(cancel.Area.Width, cancel.Area.Height), WindowPadding.BottomCenter);
        cancel.SetPosition(cancelPosition.X, cancelPosition.Y);

        return new FileOptionsView(window, buttons, cancel);
    }

    /// <summary>Moves the cursor over the dialog, <c>drawOnState()</c> of the reference loop.</summary>
    /// <param name="point">Cursor in logical pixels.</param>
    public void SetCursor(Point? point) => _buttons.SetCursor(point);

    /// <summary>
    /// A left button transition over the dialog.
    /// </summary>
    /// <remarks>
    /// A click has to press and release inside the same button; the cancel button repeats the rule
    /// because it is a plain <see cref="ButtonSprite"/> and not a member of the group.
    /// </remarks>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>
    /// The pressed button on release, or <see cref="DialogButton.None"/> when nothing was pressed.
    /// </returns>
    public DialogButton SetLeftButton(bool down, Point point)
    {
        if (down)
        {
            _buttons.SetCursor(point);
            _buttons.SetLeftButton(true);
            _cancelPressed = _cancel.Area.Contains(point);

            if (_cancelPressed)
            {
                _cancel.Press();
            }

            return DialogButton.None;
        }

        // The release has to reach the group as well, so a press that stayed inside a button shows its
        // released frame afterwards.
        _buttons.SetCursor(point);
        DialogButton answer = _buttons.SetLeftButton(false);

        if (_cancelPressed && _cancel.Area.Contains(point))
        {
            answer = DialogButton.Cancel;
        }

        _cancelPressed = false;
        _cancel.Release();

        return answer;
    }

    /// <summary>
    /// Draws the window, then the six buttons and the cancel button with their shadows, in the order the
    /// reference renders them.
    /// </summary>
    /// <param name="target">Frame to draw into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);
        _buttons.DrawShadows(target);
        _buttons.Draw(target);
        _cancel.DrawShadow(target);
        _cancel.Draw(target);
    }
}
