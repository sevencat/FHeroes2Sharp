using FHeroes2.App.Scenes;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.App.Modals;

/// <summary>
/// The View World window of the editor run on the host's frame pump, the event loop of
/// <c>ViewWorld::ViewWorldWindow()</c> (<c>view_world.cpp:700-795</c>).
/// </summary>
/// <remarks>
/// <para>
/// 冒险地图把这一层压在场景里：<see cref="AdventureMapScene"/> 打开时整帧让开，<c>GameHost</c> 再特判它的三处
/// 输入。编辑器没有这条特判，所以预览改借模态——<see cref="EditorSceneHost.RunViewWorld"/> 先建好世界、缓存与窗口，
/// 把 <see cref="ViewWorldOverlay"/> 包进这里，宿主的内层循环接管输入与整屏重绘，关掉才回到编辑器。
/// </para>
/// <para>
/// 与参考的两处差异：滚轮不在 <see cref="IModalScreen"/> 的输入面上（模态只收光标与左右键），所以
/// <see cref="Update"/> 每帧直接读 <see cref="LocalEvent"/>，与参考在循环里读的是同一份状态；右键在参考里只对退出
/// 按钮弹说明（<c>view_world.cpp:753-758</c>），端口没有这条说明窗，吞掉。
/// </para>
/// </remarks>
internal sealed class ViewWorldModal : IModalScreen
{
    private readonly ViewWorldOverlay _overlay;
    private readonly LocalEvent _events;
    private Point? _cursor;

    /// <summary>Wraps an opened window so the host can run it.</summary>
    /// <param name="overlay">The window to drive, already built for its mode.</param>
    /// <param name="events">Where the wheel is read from; <see langword="null"/> for <see cref="LocalEvent.Current"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="overlay"/> is <see langword="null"/>.</exception>
    public ViewWorldModal(ViewWorldOverlay overlay, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        _overlay = overlay;
        _events = events ?? LocalEvent.Current;
    }

    /// <inheritdoc />
    public bool IsFinished { get; private set; }

    /// <summary>
    /// The zoom level the window was left at, <c>conf.SetViewWorldZoomLevel( ... )</c>; <see langword="null"/> until
    /// it closes.
    /// </summary>
    public ZoomLevel? Result { get; private set; }

    /// <summary>The window this modal draws and drives.</summary>
    public ViewWorldOverlay Overlay => _overlay;

    /// <inheritdoc />
    public void SetCursor(Point? position)
    {
        _cursor = position;
        _overlay.DragTo(position);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="IModalScreen.SetLeftButton"/> 不带坐标，点击的落点按最近一次 <see cref="SetCursor"/> 算；
    /// 光标还没进来过就退回 <see cref="LocalEvent.CursorPosition"/>（脚本点击与真实点击都先推光标，这条只是兜底）。
    /// </remarks>
    public void SetLeftButton(bool down)
    {
        Point point = _cursor ?? _events.CursorPosition;

        if (_overlay.SetLeftButton(down, point))
        {
            Close();
        }
    }

    /// <inheritdoc />
    /// <remarks>View World 没有右键动作，见类注释。</remarks>
    public void SetRightButton(bool down) => _ = down;

    /// <inheritdoc />
    /// <remarks>ESC 与点退出按钮同一条路，<c>view_world.cpp:723</c> 的 <c>Game::HotKeyCloseWindow()</c>。</remarks>
    public void HotKey(ButtonHotKey hotKey)
    {
        if (hotKey == ButtonHotKey.Cancel)
        {
            Close();
        }
    }

    /// <inheritdoc />
    /// <remarks>窗口是整屏的：<see cref="ViewWorldOverlay.Draw"/> 自己把整帧标脏并重画。</remarks>
    public void Draw(ScreenBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _overlay.Draw();
    }

    /// <inheritdoc />
    public void Update(ScreenBuffer frame, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _ = elapsedSeconds;

        if (IsFinished)
        {
            return;
        }

        // 一个滚轮档位一次缩放，与参考的 isMouseWheelUp / isMouseWheelDown 一致；LocalEvent 的状态每帧重置，
        // 所以这里不会被同一档连发。
        if (_events.IsMouseWheelUp)
        {
            _overlay.Wheel(1);
        }
        else if (_events.IsMouseWheelDown)
        {
            _overlay.Wheel(-1);
        }
    }

    private void Close()
    {
        Result = _overlay.Close();
        IsFinished = true;
    }
}
