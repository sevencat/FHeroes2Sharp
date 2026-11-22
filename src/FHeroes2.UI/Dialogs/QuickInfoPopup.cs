using System;

using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Shows a quick info box until the right button is released, the tail of every <c>Dialog::QuickInfo()</c>
/// overload (<c>dialog_quickinfo.cpp:604-690</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference saves the covered area with <c>fheroes2::ImageRestorer</c>, blits the box, renders once and
/// then pumps events with an empty body while <c>le.isMouseRightButtonPressed()</c> holds; on the way out the
/// restorer is dropped and the area is rendered again. The cursor is hidden for the same span
/// (<c>Cursor::Get().Hide()</c>), which the port leaves to the caller's frame loop.
/// </para>
/// <para>
/// 参考在三个重载里各写一遍这十几行；端口把它收成一处，三个 <c>ShowQuickInfo()</c> 只负责造出面板。
/// </para>
/// </remarks>
public static class QuickInfoPopup
{
    /// <summary>
    /// Paints <paramref name="panel"/> over <paramref name="display"/> and waits for the right button to be
    /// released, restoring the covered area before returning.
    /// </summary>
    /// <param name="display">The frame that already holds the map.</param>
    /// <param name="panel">The box to show.</param>
    /// <param name="events">Event state to pump; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="display"/> or <paramref name="panel"/> is <see langword="null"/>.
    /// </exception>
    public static void Show(PixelBuffer display, IQuickInfoPanel panel, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(panel);

        LocalEvent input = events ?? LocalEvent.Current;
        Rect area = panel.Area;

        using ImageRestorer restorer = new(display, area.X, area.Y, area.Width, area.Height);

        panel.Draw(display);
        Present(input, area);

        // 参考 680-682：按住右键期间循环体是空的，松开就退。
        while (input.HandleEvents() && input.IsMouseRightButtonPressed)
        {
        }

        restorer.Restore();
        Present(input, area);
    }

    private static void Present(LocalEvent input, Rect area)
    {
        if (input.Engine is { } engine)
        {
            engine.Render(area);
        }
    }
}
