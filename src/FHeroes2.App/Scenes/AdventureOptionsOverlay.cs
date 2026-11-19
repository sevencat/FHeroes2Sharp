using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The adventure options dialog while it is open: it paints the composed window over the adventure map
/// and hands the host's left button to the dialog.
/// </summary>
/// <remarks>
/// The reference runs a blocking loop (<c>dialog_adventure.cpp:64-115</c>) and paints the dialog straight
/// into the display, on top of the map that is already there. The port draws the map first and this
/// overlay second, so closing the dialog is simply no longer drawing it; the click rules stay in
/// <see cref="AdventureOptionsView.SetLeftButton"/> so they can be tested without a window.
/// </remarks>
public sealed class AdventureOptionsOverlay
{
    private readonly AdventureOptionsView _view;
    private readonly ScreenBuffer _frame;

    /// <summary>Wraps an opened dialog so it can be drawn and driven.</summary>
    /// <param name="view">The composed dialog.</param>
    /// <param name="frame">The screen the dialog is painted on.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public AdventureOptionsOverlay(AdventureOptionsView view, ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(frame);

        _view = view;
        _frame = frame;
    }

    /// <summary>The dialog this overlay drives.</summary>
    public AdventureOptionsView View => _view;

    /// <summary>Paints the dialog over whatever the scene has already drawn.</summary>
    public void Draw()
    {
        _frame.InvalidateAll();
        _view.Draw(_frame.Pixels);
    }

    /// <summary>
    /// A left button transition.
    /// </summary>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>
    /// The pressed button on release, or <see cref="DialogButton.None"/> when nothing was pressed.
    /// </returns>
    public DialogButton SetLeftButton(bool down, Point point) => _view.SetLeftButton(down, point);
}
