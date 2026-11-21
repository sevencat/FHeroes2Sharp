using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

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
    private Redraw _redraw;

    /// <summary>Builds the interface, <c>BaseInterface( bool isEditor )</c>.</summary>
    /// <param name="isEditor">Whether this is the map editor interface.</param>
    /// <param name="display">Frame buffer the radar paints its border into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="display"/> is <see langword="null"/>.</exception>
    protected BaseInterface(bool isEditor, PixelBuffer display)
    {
        ArgumentNullException.ThrowIfNull(display);

        _isEditor = isEditor;
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
    /// <exception cref="NotImplementedException">
    /// Not ported yet: the reference asks <c>Game::validateDisplayFadeIn()</c>, and the global game state is not
    /// ported (S14). The port drives fade-ins through <c>DisplayFade</c> objects owned by the host instead.
    /// </exception>
    public void RenderWithFadeInOrPlanRender(Redraw redrawMask)
        => throw new NotImplementedException("BaseInterface::renderWithFadeInOrPlanRender needs the Game fade-in state (S14).");

    /// <summary>
    /// Clears a pending fade-in and paints the whole frame, <c>validateFadeInAndRender()</c>.
    /// </summary>
    /// <exception cref="NotImplementedException">Not ported yet, same reason as <see cref="RenderWithFadeInOrPlanRender"/>.</exception>
    protected void ValidateFadeInAndRender()
        => throw new NotImplementedException("BaseInterface::validateFadeInAndRender needs the Game fade-in state (S14).");
}
