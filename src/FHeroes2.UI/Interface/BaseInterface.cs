using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

using FHeroes2.Game.System;

using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Interface;

/// <summary>
/// Common base of the adventure map interface and of the editor, <c>Interface::BaseInterface</c>
/// (<c>gui/interface_base.h:60-186</c>).
/// </summary>
/// <remarks>
/// <para>
/// The class owns the two pieces every screen of the series needs - the scrolling <see cref="GameArea"/> and the
/// <see cref="Radar"/> - plus the redraw mask the frame loop drains. The reference stores both panels by value and
/// builds them with <c>*this</c>; the port keeps the same ownership and hands the radar the frame buffer it has to
/// paint its stone border into (there is no <c>Display</c> singleton here).
/// </para>
/// <para>
/// The scroll margins of <see cref="IsScrollLeft"/> and friends are <c>borderWidthPx</c> (16), the width of the
/// adventure screen border; the reference reads the display size behind them, the port takes it from the caller.
/// </para>
/// </remarks>
public abstract class BaseInterface
{
    private readonly bool _isEditor;
    private readonly PixelBuffer _display;
    private Redraw _redraw;
    private DisplayFade? _pendingFadeIn;

    /// <summary>Builds the interface, <c>BaseInterface( bool isEditor )</c>.</summary>
    /// <param name="isEditor">Whether this is the map editor interface.</param>
    /// <param name="display">Frame buffer the radar paints its border into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="display"/> is <see langword="null"/>.</exception>
    protected BaseInterface(bool isEditor, PixelBuffer display)
    {
        ArgumentNullException.ThrowIfNull(display);

        _isEditor = isEditor;
        _display = display;
        GameArea = new GameArea(this);
        Radar = new Radar(this, display);
    }

    /// <summary>The scrolling map viewport, <c>_gameArea</c>.</summary>
    protected GameArea GameArea { get; }

    /// <summary>The radar of the interface, <c>_radar</c>.</summary>
    protected Radar Radar { get; }

    /// <summary>Whether this is the map editor interface, <c>isEditor()</c>.</summary>
    public bool IsEditor => _isEditor;

    /// <summary>
    /// Area of the control panel that swallows map clicks while the interface is hidden,
    /// <c>AdventureMap::Get().getControlPanel().GetArea()</c>.
    /// </summary>
    /// <remarks>
    /// The reference reaches the panel through the adventure-map singleton, which the port does not have yet
    /// (<c>AdventureMap</c> is G5). <see cref="GameArea.QueueEventProcessing"/> only needs to know whether the cursor is
    /// over the panel, so the interface answers for itself and the default — no panel — stays correct until the
    /// adventure map overrides it.
    /// </remarks>
    public virtual Rect? ControlPanelAreaForInput => null;

    /// <summary>Whether something is queued for a repaint, <c>needRedraw()</c>.</summary>
    public bool NeedRedraw => _redraw != Redraw.None;

    /// <summary>The queued parts, <c>getRedrawMask()</c>.</summary>
    /// <returns>The current mask, which the frame loop drains through <see cref="RedrawInterface"/>.</returns>
    public Redraw GetRedrawMask() => _redraw;

    /// <summary>Queues parts of the interface for a repaint, <c>setRedraw()</c>.</summary>
    /// <param name="parts">Parts to repaint.</param>
    public void SetRedraw(Redraw parts) => _redraw |= parts;

    /// <summary>Drops the queued parts, the <c>_redraw = 0</c> at the end of <c>redraw()</c>.</summary>
    protected void ClearRedraw() => _redraw = Redraw.None;

    /// <summary>Whether the cursor is in the left scroll margin, <c>isScrollLeft()</c>.</summary>
    /// <param name="cursorPos">Cursor position in the frame.</param>
    /// <returns><see langword="true"/> inside the border strip.</returns>
    public static bool IsScrollLeft(Point cursorPos) => cursorPos.X < GameBorder.BorderWidth;

    /// <summary>Whether the cursor is in the right scroll margin, <c>isScrollRight()</c>.</summary>
    /// <param name="cursorPos">Cursor position in the frame.</param>
    /// <param name="screenSize">Size of the frame.</param>
    /// <returns><see langword="true"/> inside the border strip.</returns>
    public static bool IsScrollRight(Point cursorPos, Size screenSize) => cursorPos.X >= screenSize.Width - GameBorder.BorderWidth;

    /// <summary>Whether the cursor is in the top scroll margin, <c>isScrollTop()</c>.</summary>
    /// <param name="cursorPos">Cursor position in the frame.</param>
    /// <returns><see langword="true"/> inside the border strip.</returns>
    public static bool IsScrollTop(Point cursorPos) => cursorPos.Y < GameBorder.BorderWidth;

    /// <summary>Whether the cursor is in the bottom scroll margin, <c>isScrollBottom()</c>.</summary>
    /// <param name="cursorPos">Cursor position in the frame.</param>
    /// <param name="screenSize">Size of the frame.</param>
    /// <returns><see langword="true"/> inside the border strip.</returns>
    public static bool IsScrollBottom(Point cursorPos, Size screenSize) => cursorPos.Y >= screenSize.Height - GameBorder.BorderWidth;

    /// <summary>The map viewport, <c>getGameArea()</c>.</summary>
    public GameArea GetGameArea() => GameArea;

    /// <summary>The radar, <c>getRadar()</c>.</summary>
    public Radar GetRadar() => Radar;

    /// <summary>Whether dragging the map with the mouse is allowed, <c>useMouseDragMovement()</c>.</summary>
    public virtual bool UseMouseDragMovement => true;

    /// <summary>Repaints the queued parts, <c>redraw()</c>.</summary>
    /// <param name="force">Parts to repaint, merged into the queued mask.</param>
    /// <remarks>
    /// The reference spells this <c>redraw()</c>; the port cannot call it <c>Redraw</c> because that is the name of
    /// the mask type the method takes, so the verb gained a suffix.
    /// </remarks>
    public abstract void RedrawInterface(Redraw force);

    /// <summary>Handles a left click on a tile, <c>mouseCursorAreaClickLeft()</c>.</summary>
    /// <param name="tileIndex">Tile that was clicked.</param>
    public abstract void MouseCursorAreaClickLeft(int tileIndex);

    /// <summary>Handles a right press on a tile, <c>mouseCursorAreaPressRight()</c>.</summary>
    /// <param name="tileIndex">Tile under the cursor.</param>
    public abstract void MouseCursorAreaPressRight(int tileIndex);

    /// <summary>Handles a long left press on a tile, <c>mouseCursorAreaLongPressLeft()</c>.</summary>
    /// <param name="tileIndex">Tile under the cursor.</param>
    public abstract void MouseCursorAreaLongPressLeft(int tileIndex);

    /// <summary>Rebuilds the viewport and the panels, <c>reset()</c>.</summary>
    public abstract void Reset();

    /// <summary>Updates the cursor for a tile, <c>updateCursor()</c>.</summary>
    /// <param name="tileIndex">Tile under the cursor.</param>
    public abstract void UpdateCursor(int tileIndex);

    /// <summary>
    /// Renders the given parts, fading the frame in when a fade-in is pending, <c>renderWithFadeInOrPlanRender()</c>.
    /// </summary>
    /// <param name="redrawMask">Parts to repaint and to include in the fade-in frame.</param>
    /// <remarks>
    /// 参考（<c>gui/interface_base.cpp:28-38</c>）：有待播的淡入就立刻重画这一帧、再把整屏淡入；否则只把这几块排进队列，
    /// 交给帧循环。<c>redraw()</c> 那一半端口照做，阻塞的 <c>fheroes2::fadeInDisplay()</c> 换成发布
    /// <see cref="TakePendingFadeIn"/>，由宿主逐帧播放。
    /// </remarks>
    public void RenderWithFadeInOrPlanRender(Redraw redrawMask)
    {
        if (GameDisplayFadeIn.ValidateDisplayFadeIn())
        {
            RedrawInterface(redrawMask);
            _pendingFadeIn = DisplayFade.In(_display);

            return;
        }

        SetRedraw(redrawMask);
    }

    /// <summary>
    /// Clears a pending fade-in and paints the whole frame, <c>validateFadeInAndRender()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考（<c>gui/interface_base.cpp:40-50</c>）：有待播的淡入就整屏淡入、再排一次地图区的重画；否则只
    /// <c>Display::render()</c> 把当前帧推上屏。端口的呈现归宿主，后一支无事可做；前一支同样是把淡入交给
    /// <see cref="TakePendingFadeIn"/>。
    /// </para>
    /// <para>
    /// 参考在 <c>HumanTurn()</c> 每帧 <c>redraw(0)</c> 之后紧接着调它（<c>game_startgame.cpp:1580</c>），
    /// 端口的地图场景因此也能从外面调：它自己没有 <c>HumanTurn</c> 那道循环，画完这一帧就在这里取淡入。
    /// </para>
    /// </remarks>
    public void ValidateFadeInAndRender()
    {
        if (!GameDisplayFadeIn.ValidateDisplayFadeIn())
        {
            return;
        }

        _pendingFadeIn = DisplayFade.In(_display);
        SetRedraw(Redraw.GameArea);
    }

    /// <summary>The full-screen fade-in the frame loop still has to play, <c>fheroes2::fadeInDisplay()</c>.</summary>
    /// <returns>The fade, or <see langword="null"/> when no fade-in is pending.</returns>
    /// <remarks>
    /// 参考的 <c>fadeInDisplay()</c> 自己阻塞跑完六帧、每帧 <c>Display::render()</c>；端口把逐帧时机交给宿主，
    /// 所以淡入以对象的形式在这里取走，由宿主对着 <c>BeginFrame</c> 的节奏调 <see cref="DisplayFade.TryAdvance"/>。
    /// 取一次就清空：一次请求只属于一帧。
    /// </remarks>
    public DisplayFade? TakePendingFadeIn()
    {
        DisplayFade? fade = _pendingFadeIn;
        _pendingFadeIn = null;

        return fade;
    }
}
