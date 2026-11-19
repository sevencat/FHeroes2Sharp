using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The interactive part of the View World window: it owns a <see cref="ViewWorldWindow"/>, draws it over
/// the whole screen and turns the host's mouse events into the window calls of <c>view_world.cpp:700-795</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的 `ViewWorldWindow()` 是一个阻塞的事件循环；端口的 App 是「主机收事件、场景收调用」，所以这里只保留
/// 循环里的两件事：把事件翻译成窗口的调用、把窗口画到屏幕上。打开与关闭（建缓存、fade）留给
/// <see cref="AdventureMapScene"/>。
/// </para>
/// <para>
/// <b>点击的语义照抄 `MouseClickLeft`</b>：按下与松开都落在同一个按钮里才算一次点击，所以按下时记下位置、
/// 松开时再比一次；地图窗口的拖拽则从按下就开始（`Press`），移动由 <see cref="DragTo"/> 推中心，松开或离开
/// 窗口结束。
/// </para>
/// </remarks>
public sealed class ViewWorldOverlay
{
    private readonly ViewWorldWindow _window;
    private readonly ScreenBuffer _frame;
    private Point? _pressPoint;
    private bool _zoomPressed;
    private bool _exitPressed;
    private bool _radarNeedsObjects = true;

    /// <summary>Wraps an opened window so it can be drawn and driven.</summary>
    /// <param name="window">The window, already built for its mode.</param>
    /// <param name="frame">The screen the window is painted on.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ViewWorldOverlay(ViewWorldWindow window, ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(frame);

        _window = window;
        _frame = frame;
    }

    /// <summary>The window this overlay drives.</summary>
    public ViewWorldWindow Window => _window;

    /// <summary>Paints the map, the radar and the side panel over the whole frame.</summary>
    /// <remarks>
    /// 顺序照参考的重绘：先地图，再雷达（开窗时整块重新着色，之后只画光标框），最后侧栏与两个按钮。
    /// 画面是直接写进索引像素的，所以每帧都要整屏标脏。
    /// </remarks>
    public void Draw()
    {
        _frame.InvalidateAll();

        PixelBuffer display = _frame.Pixels;

        _window.DrawWorld(display);
        _window.DrawRadar(display, _radarNeedsObjects);
        _radarNeedsObjects = false;
        _window.DrawChrome(display, _zoomPressed, _exitPressed);
    }

    /// <summary>Moves the map while the button is held, <c>view_world.cpp:734-745</c>.</summary>
    /// <param name="point">The cursor, or <see langword="null"/> when it left the window.</param>
    public void DragTo(Point? point)
    {
        if (_pressPoint is null)
        {
            return;
        }

        if (point is null)
        {
            _window.Release();
            _pressPoint = null;
            return;
        }

        if (!_window.DragTo(point.Value))
        {
            // Either the cursor left the map window - which ends the drag like the reference does - or
            // the centre is clamped against an edge. Both leave the view as it is.
            _pressPoint = _window.IsDragging ? _pressPoint : null;
        }
    }

    /// <summary>
    /// A left button transition: presses start a drag or arm a button, releases run the click.
    /// </summary>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns><see langword="true"/> when the exit button was clicked and the window has to close.</returns>
    public bool SetLeftButton(bool down, Point point)
    {
        if (down)
        {
            _pressPoint = point;
            _zoomPressed = _window.IsInZoomButton(point);
            _exitPressed = _window.IsInExitButton(point);
            _window.Press(point);

            return false;
        }

        Point pressed = _pressPoint ?? point;
        _pressPoint = null;
        _zoomPressed = false;
        _exitPressed = false;
        _window.Release();

        if (_window.IsInExitButton(pressed) && _window.IsInExitButton(point))
        {
            return true;
        }

        if (_window.IsInZoomButton(pressed) && _window.IsInZoomButton(point))
        {
            _window.ClickZoom();
        }
        else if (_window.IsInRadar(pressed) && _window.IsInRadar(point))
        {
            _window.ClickRadar(point);
        }

        return false;
    }

    /// <summary>One wheel notch: up zooms in, down zooms out, <c>view_world.cpp:713-716</c>.</summary>
    /// <param name="notches">Positive when the wheel turns away from the user.</param>
    public void Wheel(int notches)
    {
        if (notches > 0)
        {
            _window.WheelUp();
        }
        else if (notches < 0)
        {
            _window.WheelDown();
        }
    }

    /// <summary>Closes the window and answers the level to remember.</summary>
    /// <returns>The zoom level the window was left at.</returns>
    public ZoomLevel Close()
    {
        _pressPoint = null;
        _zoomPressed = false;
        _exitPressed = false;

        return _window.Close();
    }
}
