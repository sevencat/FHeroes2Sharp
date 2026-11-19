using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The adventure options dialog as it is drawn: a stone window, the four big buttons of
/// <c>APANEL.ICN</c> and the cancel button under them, <c>Dialog::AdventureOptions()</c>
/// (<c>dialog_adventure.cpp:38-121</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference builds this in place and runs its own event loop. The port splits the two: this class
/// owns the artwork and the layout, and hands out <see cref="Buttons"/> and <see cref="Cancel"/> so the
/// caller can feed them events and decide when the dialog is done. Rendering is stateless, like every
/// other scene of the port, so <see cref="Draw"/> can be called for every frame.
/// </para>
/// <para>
/// The reference takes the four buttons from <c>BUTTONS_ADVENTURE_OPTIONS_DIALOG</c>, a table the
/// engine generates from text, but its original-resource branch copies the buttons of
/// <c>APANEL.ICN</c> as they are (<c>game_assets.cpp:1585-1592</c>); the port follows that branch,
/// because the port only reads the archives. Frame pairs are released, then pressed.
/// </para>
/// </remarks>
public sealed class AdventureOptionsView
{
    /// <summary>Archive entry that holds the four big buttons.</summary>
    public const string ButtonsAssetName = "APANEL.ICN";

    /// <summary>
    /// Frames the four big buttons take: world, puzzle, information and dig, released and pressed each.
    /// </summary>
    public const int ButtonsFrameCount = 8;

    /// <summary>Archive entry that holds the cancel button, the <c>ICN::SYSTEM</c> of the reference.</summary>
    public const string CancelAssetName = "SYSTEM.ICN";

    /// <summary>Frame of the released cancel button in <see cref="CancelAssetName"/>.</summary>
    /// <remarks>
    /// The reference asks for frames 0 and 1 of <c>ICN::SYSTEM</c> (<c>dialog_adventure.cpp:109</c>), but
    /// its <c>SYSTEM.ICN</c> is generated from text and holds the buttons in a different order than the
    /// archive. The archive keeps the checkpoint frames first: frames 1 and 2 are OKAY, frames 3 and 4
    /// CANCEL, the same pair the settings dialog of the port uses.
    /// </remarks>
    public const int CancelReleasedFrame = 3;

    /// <summary>Frame of the pressed cancel button in <see cref="CancelAssetName"/>.</summary>
    public const int CancelPressedFrame = 4;

    /// <summary>
    /// Distance of the cancel button from the bottom centre of the window, the
    /// <c>{ 0, 11 }</c> of <c>dialog_adventure.cpp:109</c>. The vertical part lifts it off the border.
    /// </summary>
    public static Point CancelOffset => new(0, 11);

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly ButtonSprite _cancel;
    private bool _cancelPressed;

    private AdventureOptionsView(StandardWindow window, ButtonGroup buttons, ButtonSprite cancel)
    {
        _window = window;
        _buttons = buttons;
        _cancel = cancel;
    }

    /// <summary>Values of the four big buttons, in the order of <c>APANEL.ICN</c>.</summary>
    public static IReadOnlyList<DialogButton> ButtonValues => Values;

    private static readonly DialogButton[] Values =
        [DialogButton.World, DialogButton.Puzzle, DialogButton.Info, DialogButton.Dig];

    /// <summary>The four big buttons, already placed in the window.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The cancel button, already placed in the window.</summary>
    public ButtonSprite Cancel => _cancel;

    /// <summary>Area the window content goes into.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>
    /// Composes the dialog from the archive frames.
    /// </summary>
    /// <param name="chrome">Border and background artwork of the stone window.</param>
    /// <param name="buttonFrames">Frames of <see cref="ButtonsAssetName"/>, eight of them.</param>
    /// <param name="systemFrames">Frames of <see cref="CancelAssetName"/>, two at least.</param>
    /// <returns><see langword="null"/> when an input is missing or too short.</returns>
    public static AdventureOptionsView? TryCreate(StandardWindowChrome? chrome,
        IReadOnlyList<Sprite>? buttonFrames, IReadOnlyList<Sprite>? systemFrames)
    {
        if (chrome is null || buttonFrames is null || systemFrames is null)
        {
            return null;
        }

        if (buttonFrames.Count < ButtonsFrameCount || systemFrames.Count <= CancelPressedFrame)
        {
            return null;
        }

        ButtonGroup buttons = ButtonGroup.CreateFromFrames(
            [.. buttonFrames.Take(ButtonsFrameCount)], ButtonValues);

        StandardWindow window = StandardWindow.CreateForSymmetricButtons(chrome, buttons);
        window.PlaceSymmetricButtons(buttons);

        ButtonSprite cancel = new(systemFrames[CancelReleasedFrame], systemFrames[CancelPressedFrame]);
        Point cancelPosition = window.RenderPosition(
            CancelOffset, new Size(cancel.Area.Width, cancel.Area.Height), WindowPadding.BottomCenter);
        cancel.SetPosition(cancelPosition.X, cancelPosition.Y);

        return new AdventureOptionsView(window, buttons, cancel);
    }

    /// <summary>
    /// A left button transition over the dialog.
    /// </summary>
    /// <remarks>
    /// A click has to press and release inside the same button, the <c>MouseClickLeft</c> rule the four
    /// big buttons get from <see cref="ButtonGroup"/>; the cancel button repeats it here because it is a
    /// plain <see cref="ButtonSprite"/> and not a member of the group.
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
    /// Draws the window, then the four buttons and the cancel button with their shadows, in the order
    /// the reference renders them.
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
