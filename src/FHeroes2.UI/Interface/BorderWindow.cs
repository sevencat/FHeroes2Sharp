using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Interface;

/// <summary>
/// A panel that can float over the map when the interface is hidden, a port of
/// <c>Interface::BorderWindow</c> (<c>src/fheroes2/gui/interface_border.h:35-78</c>,
/// <c>interface_border.cpp:420-524</c>).
/// </summary>
/// <remarks>
/// <para>
/// In the normal layout a panel is just a rectangle inside the frame: <see cref="SetPosition(int, int)"/> only
/// moves <see cref="Area"/>. With the «no interface» setting on, every panel becomes a floating window with its
/// own stone border, clamped to the display and draggable by that border. Which of the two
/// <see cref="GetRect"/> returns is exactly the <c>isHideInterfaceEnabled() &amp;&amp; border.isValid()</c> test
/// of the reference.
/// </para>
/// <para>
/// <b>Dragging is host driven.</b> The reference's <c>QueueEventProcessing()</c> blocks: it draws a dotted
/// yellow outline around the panel through a <see cref="MovableSprite"/> and spins on
/// <c>le.HandleEvents()</c> until the button is released. The port returns a <see cref="BorderDrag"/> instead
/// and the scene feeds it the cursor position each frame - the same split the port uses for the virtual
/// keyboard and the text caret. The outline itself, its 6 pixel skip pattern and its colour
/// (<c>GetColorId( 0xD0, 0xC0, 0x48 )</c>) are kept.
/// </para>
/// </remarks>
public abstract class BorderWindow : IDisposable
{
    /// <summary>Distance between the dots of the drag outline, the last argument of <c>DrawBorder()</c>.</summary>
    public const uint DragOutlineSkip = 6;

    private readonly PixelBuffer _target;
    private readonly Sprite? _artwork;
    private readonly FrameBorder _border;
    private int _areaX;
    private int _areaY;
    private int _areaWidth;
    private int _areaHeight;
    private bool _isMouseCaptured;
    private bool _isDisposed;

    /// <summary>
    /// Wraps an area of the panel, <c>BorderWindow( const Rect &amp; )</c>.
    /// </summary>
    /// <param name="target">Frame buffer the floating border is drawn into and restored from.</param>
    /// <param name="area">Rectangle the panel occupies in the normal layout.</param>
    /// <param name="artwork">Frame 0 of the stone frame, used only when the interface is hidden.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    protected BorderWindow(PixelBuffer target, Rect area, Sprite? artwork)
    {
        ArgumentNullException.ThrowIfNull(target);

        _target = target;
        _artwork = artwork;
        _border = new FrameBorder(target);

        _areaX = area.X;
        _areaY = area.Y;
        _areaWidth = area.Width;
        _areaHeight = area.Height;
    }

    /// <summary>Whether panels float, <c>Settings::Get().isHideInterfaceEnabled()</c>.</summary>
    /// <remarks>
    /// The reference reads the setting from the singleton whenever it needs it; the port lets the scene own it,
    /// because a settings change is applied by rebuilding the panels anyway.
    /// </remarks>
    public bool HideInterfaceEnabled { get; set; }

    /// <summary>Rectangle of the panel contents, <c>GetArea()</c>.</summary>
    public Rect Area => new(_areaX, _areaY, _areaWidth, _areaHeight);

    /// <summary>
    /// What the panel occupies on screen, <c>GetRect()</c>: the floating border when it has one, the plain
    /// area otherwise.
    /// </summary>
    public Rect GetRect => HideInterfaceEnabled && _border.IsValid ? _border.Rect : Area;

    /// <summary>Moves the panel, <c>SetPos()</c> of a derived panel.</summary>
    /// <param name="x">Left edge of the area (or of the floating window, when the interface is hidden).</param>
    /// <param name="y">Top edge, same convention.</param>
    public abstract void SetPos(int x, int y);

    /// <summary>Saves the new position, <c>SavePosition()</c> of a derived panel.</summary>
    public abstract void SavePosition();

    /// <summary>Restores the pixels the floating border covered, <c>~BorderWindow()</c>.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _border.Dispose();
    }

    /// <summary>
    /// Whether the panel still owns the mouse after a drag started on it, <c>isMouseCaptured()</c>.
    /// </summary>
    /// <param name="events">Event state of the frame.</param>
    /// <returns>
    /// <see langword="false"/> when no drag was ever started on the panel; otherwise <see langword="true"/> even
    /// on the frame the button went up, so that the release belongs to the panel and not to a widget underneath.
    /// </returns>
    public bool IsMouseCaptured(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!_isMouseCaptured)
        {
            return false;
        }

        _isMouseCaptured = events.IsMouseLeftButtonPressed;

        return true;
    }

    /// <summary>
    /// Takes the mouse if the press landed on the panel, <c>captureMouse()</c>.
    /// </summary>
    /// <param name="events">Event state of the frame.</param>
    public void CaptureMouse(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.IsMouseLeftButtonPressedInArea(GetRect))
        {
            _isMouseCaptured = true;
        }
        else
        {
            _isMouseCaptured = _isMouseCaptured && events.IsMouseLeftButtonPressed;
        }
    }

    /// <summary>
    /// Starts dragging the panel by its top border, the first half of <c>QueueEventProcessing()</c>.
    /// </summary>
    /// <param name="events">Event state of the frame.</param>
    /// <returns>The drag to feed with <see cref="BorderDrag.MoveTo"/>, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Only a hidden-interface panel has a border to grab, which is the
    /// <c>!isHideInterfaceEnabled() || !isMouseLeftButtonPressedInArea( border.GetTop() )</c> guard of the
    /// reference. The caller has to reset the cursor to a plain pointer itself: the reference does it here
    /// because it owns the global cursor, the port's cursor layer sits above this assembly.
    /// </remarks>
    public BorderDrag? QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (!HideInterfaceEnabled || !events.IsMouseLeftButtonPressedInArea(_border.Top))
        {
            return null;
        }

        Rect panel = GetRect;
        Point cursor = events.CursorPosition;

        var drag = new BorderDrag(panel, new Point(cursor.X - panel.X, cursor.Y - panel.Y));

        // The reference also hands the drag state to the cursor layer by clearing the capture here.
        _isMouseCaptured = false;

        return drag;
    }

    /// <summary>
    /// Paints the floating border of the panel, <c>Redraw()</c>.
    /// </summary>
    /// <remarks>
    /// The reference always draws the themed stone frame, which it reads from the assets at this point. The port
    /// takes the artwork in the constructor and does nothing when it was not given any - a panel in the normal
    /// layout has no border to paint.
    /// </remarks>
    protected void Redraw()
    {
        if (_artwork is null)
        {
            return;
        }

        FrameBorder.RenderRegular(_target, _artwork, _border.Rect);
    }

    /// <summary>
    /// Sets the panel size and position, <c>SetPosition( x, y, width, height )</c>.
    /// </summary>
    protected void SetPosition(int x, int y, int width, int height)
    {
        _areaWidth = width;
        _areaHeight = height;

        SetPosition(x, y);
    }

    /// <summary>
    /// Sets the panel position, <c>SetPosition( x, y )</c>.
    /// </summary>
    /// <remarks>
    /// With the interface hidden the panel is a window, so the position is clamped to the display and shifted to
    /// the contents' origin, the border is re-placed (which restores what it covered before) and the new position
    /// is saved. Otherwise the position is the area's own origin.
    /// </remarks>
    protected void SetPosition(int x, int y)
    {
        if (HideInterfaceEnabled)
        {
            x = Math.Max(0, Math.Min(x, _target.Width - (_areaWidth + (_border.BorderWidth * 2))));
            y = Math.Max(0, Math.Min(y, _target.Height - (_areaHeight + (_border.BorderHeight * 2))));

            _areaX = x + _border.BorderWidth;
            _areaY = y + _border.BorderHeight;

            _border.SetPosition(x, y, _areaWidth, _areaHeight);
            SavePosition();
        }
        else
        {
            _areaX = x;
            _areaY = y;
        }
    }
}

/// <summary>
/// One drag of a <see cref="BorderWindow"/>, the blocking loop of <c>BorderWindow::QueueEventProcessing()</c>
/// (<c>interface_border.cpp:494-523</c>) split into a state object.
/// </summary>
/// <remarks>
/// The reference walks the panel-shaped outline with the cursor: a sprite the size of the panel, cleared, framed
/// with a dotted yellow border and moved to <c>cursor - grabOffset</c> on every mouse move. <see cref="MoveTo"/>
/// does the same, and <see cref="Draw"/> blits the outline so a scene can paint it after the map.
/// </remarks>
public sealed class BorderDrag
{
    /// <summary>
    /// Colour of the outline, <c>GetColorId( 0xD0, 0xC0, 0x48 )</c>.
    /// </summary>
    /// <remarks>
    /// Resolved on every access instead of once in a static field, so that loading the type does not need the
    /// palette; the reference resolves it inside the loop.
    /// </remarks>
    public static byte OutlineColor => EmptyButtons.GetColorId(0xD0, 0xC0, 0x48);

    private readonly MovableSprite _indicator;

    /// <summary>Starts a drag of a panel that begins at <paramref name="panel"/>.</summary>
    /// <param name="panel">Rectangle of the panel when the drag began, <c>GetRect()</c>.</param>
    /// <param name="grabOffset">Offset of the cursor inside the panel, <c>ox/oy</c>.</param>
    public BorderDrag(Rect panel, Point grabOffset)
    {
        Panel = panel;
        GrabOffset = grabOffset;

        // MovableSprite( pos.width, pos.height, pos.x, pos.y ) then reset() + DrawBorder().
        _indicator = new MovableSprite(panel.Width, panel.Height, panel.X, panel.Y);

        if (_indicator.Sprite is { } sprite)
        {
            // `reset()` makes the fresh sprite see-through; without it the outline would be a black slab.
            sprite.Reset();
            ImageOps.DrawBorder(sprite.Image, OutlineColor, BorderWindow.DragOutlineSkip);
            MakePaintedPixelsVisible(sprite);
        }
    }

    /// <summary>Rectangle of the panel when the drag began.</summary>
    public Rect Panel { get; }

    /// <summary>Offset of the cursor inside the panel.</summary>
    public Point GrabOffset { get; }

    /// <summary>Where the outline currently is.</summary>
    public Point Position => new(_indicator.X, _indicator.Y);

    /// <summary>The outline, for callers that want to place it themselves.</summary>
    public MovableSprite Indicator => _indicator;

    /// <summary>
    /// Moves the outline to follow the cursor, the body of the reference's event loop.
    /// </summary>
    /// <param name="cursor">Current cursor position.</param>
    public void MoveTo(Point cursor) => _indicator.SetPosition(cursor.X - GrabOffset.X, cursor.Y - GrabOffset.Y);

    /// <summary>Paints the outline at its current position.</summary>
    /// <param name="target">Frame buffer to paint into.</param>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_indicator.Sprite is { } sprite && !_indicator.IsHidden)
        {
            ImageOps.BlitSprite(target, new Point(_indicator.X, _indicator.Y), sprite);
        }
    }

    /// <summary>
    /// Clears the transform of the pixels <see cref="ImageOps.DrawBorder"/> painted.
    /// </summary>
    /// <remarks>
    /// The reference's <c>DrawBorder()</c> clears the transform layer of every pixel it touches
    /// (<c>image.cpp:1831-2001</c>), which is what turns the frame on a freshly reset image into visible pixels.
    /// The port's <see cref="ImageOps.DrawBorder"/> takes a single layer <see cref="PixelBuffer"/>, so the clear
    /// has to happen here, after the drawing.
    /// </remarks>
    /// <summary>
    /// Makes the pixels a caller has just painted into a reset sprite show up, keeping the untouched ones transparent.
    /// </summary>
    /// <param name="sprite">A sprite whose transform layer has been reset and whose image holds the painted pixels.</param>
    /// <remarks>
    /// Shared with <see cref="Radar"/>, which builds its cursor frame the same way.
    /// </remarks>
    internal static void MakePaintedPixelsVisible(Sprite sprite)
    {
        if (sprite.Transform is not { } transform)
        {
            return;
        }

        byte[] pixels = sprite.Image.Pixels;

        for (int i = 0; i < pixels.Length; ++i)
        {
            if (pixels[i] != 0)
            {
                transform[i] = 0;
            }
        }
    }
}
