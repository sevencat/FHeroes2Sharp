using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Assets.Maps;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The colour picker, <c>Dialog::selectPlayerColor()</c> (<c>dialog_selectitems.cpp:2109-2185</c>), as a modal.
/// </summary>
/// <remarks>
/// 与 <see cref="SelectCountModal"/> 同一种接法：窗口只认光标与左键，答案写在
/// <see cref="PlayerColorSelectionWindow.Answer"/> 上，关窗时取 <see cref="Answer"/>。
/// </remarks>
internal sealed class PlayerColorSelectionModal : IModalScreen
{
    private readonly PlayerColorSelectionWindow _window;

    private Point? _cursor;

    /// <summary>Wraps the window so the frame pump can run it.</summary>
    /// <param name="window">The window to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public PlayerColorSelectionModal(PlayerColorSelectionWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
    }

    /// <summary>The window being run.</summary>
    public PlayerColorSelectionWindow Window => _window;

    /// <summary>
    /// The colour the author chose, or <see langword="null"/> while the window is still open; CANCEL answers the colour
    /// the object already had, and the neutral plate answers <see cref="PlayerColor.None"/>.
    /// </summary>
    public PlayerColor? Answer => _window.Answer;

    /// <inheritdoc />
    public bool IsFinished => _window.IsClosed;

    /// <inheritdoc />
    public void SetCursor(Point? position)
    {
        _cursor = position;
        _window.Buttons.SetCursor(position);
    }

    /// <inheritdoc />
    public void SetLeftButton(bool down)
    {
        if (_cursor is not { } point)
        {
            return;
        }

        _ = _window.SetLeftButton(down, point);
    }

    /// <summary>Ignored: the reference's right clicks only show help boxes, and this window has none of its own.</summary>
    /// <param name="down">Whether the button is now held.</param>
    public void SetRightButton(bool down)
    {
    }

    /// <inheritdoc />
    public void HotKey(ButtonHotKey hotKey) => _ = _window.ProcessHotKey(hotKey);

    /// <inheritdoc />
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _window.Draw(frame.Pixels);
    }

    /// <summary>Nothing animates: the window is a title, a row of plates and two buttons.</summary>
    /// <param name="frame">Unused.</param>
    /// <param name="elapsedSeconds">Unused.</param>
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);
    }
}
