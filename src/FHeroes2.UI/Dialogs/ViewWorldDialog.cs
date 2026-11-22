using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The modal loop of the View World window, <c>ViewWorld::ViewWorldWindow()</c>
/// (<c>kingdom/view_world.cpp:700-795</c>), on top of the drawing and the interactions of
/// <see cref="ViewWorldWindow"/>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的循环体只有四件事：两个按钮的按下态、点击/雷达/拖拽/滚轮的分派、右键的说明、退出。窗口把这些动作
/// 都做成了返回 <c>changed</c> 的调用（<c>Press</c>/<c>DragTo</c>/<c>WheelUp</c>/<c>ClickZoom</c>/<c>ClickRadar</c>），
/// 所以循环本身只负责「事件 → 调用」和「重画」。
/// </para>
/// <para>
/// App 侧另有一份 <c>Scenes/ViewWorldOverlay</c>：主题是「主机收事件、场景收调用」，不能让出线程；这里则是参考那种
/// 阻塞循环，供 <c>IAdventureHost</c> 的同步实现与测试使用。两者共用 <see cref="ViewWorldWindow"/>，事件分派各一份。
/// </para>
/// <para>
/// <b>未搬</b>：fade in/out、<c>CursorRestorer</c>/<c>RadarRestorer</c>、<c>conf.SetViewWorldZoomLevel</c>（返回值交给
/// 调用方写回）、以及「退出后把界面设置复位」的那几行——这些都属于宿主的界面状态，不属于窗口。
/// </para>
/// </remarks>
public static class ViewWorldDialog
{
    /// <summary>Tooltip of the exit button, <c>_( "Exit" )</c>.</summary>
    public const string ExitTitle = "Exit";

    /// <summary>Tooltip body of the exit button, <c>_( "Exit this menu." )</c>.</summary>
    public const string ExitBody = "Exit this menu.";

    /// <summary>Tooltip of the zoom button, <c>_( "Zoom" )</c>.</summary>
    public const string ZoomTitle = "Zoom";

    /// <summary>Tooltip body of the zoom button, <c>_( "Click this button to adjust the level of zoom." )</c>.</summary>
    public const string ZoomBody = "Click this button to adjust the level of zoom.";

    /// <summary>
    /// Runs the window until the player closes it.
    /// </summary>
    /// <param name="display">The screen buffer the window paints into.</param>
    /// <param name="window">The opened window.</param>
    /// <param name="events">Event state to pull from; <see cref="LocalEvent.Current"/> when omitted.</param>
    /// <param name="showHint">
    /// Receives the two right-click tooltips as (title, body); the reference shows them with
    /// <c>fheroes2::showStandardTextMessage()</c>. When omitted the tooltips are silently dropped.
    /// </param>
    /// <param name="invalidate">Told after every redraw, so the host can present the buffer.</param>
    /// <returns>The zoom level the window was left at, <c>currentROI.getZoomLevel()</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ZoomLevel Show(
        PixelBuffer display,
        ViewWorldWindow window,
        LocalEvent? events = null,
        Action<string, string>? showHint = null,
        Action? invalidate = null)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(window);

        LocalEvent input = events ?? LocalEvent.Current;

        // 开窗的第一帧：地图、整块雷达、侧栏与两个按钮（参考 706-708 行）。
        Draw(display, window, zoomPressed: false, exitPressed: false, renderRadarObjects: true);
        invalidate?.Invoke();

        while (input.HandleEvents())
        {
            bool changed = false;

            if (input.MouseClickLeft(window.ExitButtonArea) || IsCloseHotKey(input))
            {
                break;
            }

            if (input.MouseClickLeft(window.ZoomButtonArea))
            {
                changed = window.ClickZoom();
            }
            else if (input.IsMouseCursorPosInArea(window.RadarArea))
            {
                changed = window.ClickRadar(input.CursorPosition);
            }
            else if (input.IsMouseLeftButtonPressedInArea(window.VisibleArea))
            {
                // 第一帧只记起点，之后每帧按位移推中心（参考 733-745 行）。
                changed = window.IsDragging ? window.DragTo(input.CursorPosition) : window.Press(input.CursorPosition);
            }
            else if (input.IsMouseWheelUp)
            {
                changed = window.WheelUp();
            }
            else if (input.IsMouseWheelDown)
            {
                changed = window.WheelDown();
            }
            else if (input.IsMouseRightButtonPressedInArea(window.ExitButtonArea))
            {
                showHint?.Invoke(ExitTitle, ExitBody);
            }
            else if (input.IsMouseRightButtonPressedInArea(window.ZoomButtonArea))
            {
                showHint?.Invoke(ZoomTitle, ZoomBody);
            }

            // 拖拽在「左键不再按在地图上」或「光标离开地图」时结束（参考 760-762 行）。
            if (!input.IsMouseLeftButtonPressedInArea(window.VisibleArea) ||
                !input.IsMouseCursorPosInArea(window.VisibleArea))
            {
                window.Release();
            }

            Draw(
                display,
                window,
                input.IsMouseLeftButtonPressedAndHeldInArea(window.ZoomButtonArea),
                input.IsMouseLeftButtonPressedAndHeldInArea(window.ExitButtonArea),
                renderRadarObjects: changed);
            invalidate?.Invoke();
        }

        return window.Close();
    }

    /// <summary>Whether <paramref name="events"/> carries the reference's <c>Game::HotKeyCloseWindow()</c>.</summary>
    /// <param name="events">Event state to read.</param>
    /// <returns><see langword="true"/> for Escape or Enter, the DEFAULT_CANCEL / DEFAULT_OKAY hot keys.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public static bool IsCloseHotKey(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events.IsKeyPressed(Key.Escape) || events.IsKeyPressed(Key.Enter);
    }

    private static void Draw(
        PixelBuffer display,
        ViewWorldWindow window,
        bool zoomPressed,
        bool exitPressed,
        bool renderRadarObjects)
    {
        window.DrawWorld(display);
        window.DrawRadar(display, renderRadarObjects);
        window.DrawChrome(display, zoomPressed, exitPressed);
    }
}
