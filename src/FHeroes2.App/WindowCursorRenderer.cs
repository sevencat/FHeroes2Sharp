using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Cursor;
using FHeroes2.UI.Input;

namespace FHeroes2.App;

/// <summary>
/// The pointer of the desktop client: the window system draws it, so the game only tracks where it is
/// (<c>fheroes2::cursor()</c> backed by the OS pointer, <c>src/engine/screen.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的软件光标把 ICN 帧画进显示缓冲；端口的指针是窗口系统画的那个箭头，所以这里只保留
/// <see cref="CursorState"/> 真正需要的东西：位置、显示与否，以及「不夹在屏幕内」。
/// </para>
/// <para>
/// 这一层虽然不画东西，却是**输入的一半**：冒险地图的点击先按光标主题分类（<c>CursorState.Current.Theme</c>，
/// 见 <c>Interface::AdventureMap::mouseCursorAreaClickLeft()</c>），主题为空时点地图什么都不做。没有
/// <see cref="CursorState"/> 实例时主题恒为 <see cref="CursorTheme.None"/>，英雄就走不动，所以组合根必须装一个。
/// </para>
/// </remarks>
internal sealed class WindowCursorRenderer : ICursorRenderer
{
    private bool _visible = true;

    /// <inheritdoc />
    public bool IsVisible => _visible;

    /// <inheritdoc />
    /// <remarks>窗口系统画指针，所以永远不是软件模拟。</remarks>
    public bool IsSoftwareEmulation => false;

    /// <inheritdoc />
    /// <remarks>指针位置就是输入层记下的那个位置，<c>LocalEvent::Get().getMouseCursorPos()</c>。</remarks>
    public Point Position => LocalEvent.Current.CursorPosition;

    /// <inheritdoc />
    /// <remarks>窗口系统没有「藏起来」以外的需求：隐藏时由宿主把指针移出画面。</remarks>
    public void Show(bool enable) => _visible = enable;

    /// <inheritdoc />
    /// <remarks>参考在这里换掉指针的图；端口的指针图是窗口系统给的形状，不需要这一帧。</remarks>
    public void Update(Sprite image, int offsetX, int offsetY)
    {
        _ = image;
        _ = offsetX;
        _ = offsetY;
    }

    /// <inheritdoc />
    /// <remarks>参考在这里把指针挪到指定位置；端口的指针由玩家和冒烟的脚本点击挪动。</remarks>
    public void SetPosition(int x, int y)
    {
        _ = x;
        _ = y;
    }

    /// <inheritdoc />
    /// <remarks>夹边由窗口系统负责，没有可留的状态。</remarks>
    public void KeepInScreenArea(bool keep) => _ = keep;
}
