using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The mouse of the adventure map: dragging the terrain, the scroll area along the window edges and
/// the wheel. A port of the three paths the reference has in <c>Interface::GameArea</c> plus the edge
/// test of <c>Interface::BaseInterface</c>.
/// </summary>
///
/// <remarks>
/// <para>
/// The reference keeps the drag state in <c>GameArea::QueueEventProcessing</c>
/// (<c>interface_gamearea.cpp</c>): a left press records the starting point, a movement of more than
/// ten pixels turns the gesture into a drag, and every later movement shifts the view by
/// <c>last - current</c>, so the terrain follows the pointer. A movement of ten pixels or less stays
/// a click and is left to the caller.
/// </para>
/// <para>
/// The edge scroll is driven by the frame clock, not by the pointer: the reference calls
/// <c>GameArea::Scroll()</c> once per <c>DelayType::SCROLL_DELAY</c> (twenty milliseconds) while the
/// pointer is inside the sixteen pixel border, by <c>2 &lt;&lt; scrollSpeed</c> pixels. With the
/// default speed <c>NORMAL</c> that is eight pixels per tick.
/// </para>
/// <para>
/// The wheel is the one path the reference of 1.1.18 does not have on the adventure map: there the
/// wheel only zooms the world view and scrolls lists. One notch moves one tile, which is what the
/// arrow keys do, so the wheel feels like a faster arrow key instead of a new kind of motion.
/// </para>
/// </remarks>
public sealed class MapScrollController
{
    /// <summary>Distance in pixels a left press must travel to become a drag.</summary>
    /// <remarks>
    /// <c>minimalRequiredDraggingMovement</c> of <c>interface_gamearea.cpp</c>; the comparison there
    /// is strict, so an exact ten pixel movement is still a click.
    /// </remarks>
    public const int DragThreshold = 10;

    /// <summary>Width of the scroll border along every edge, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int EdgeWidth = 16;

    /// <summary>Seconds between two edge scroll steps, <c>DelayType::SCROLL_DELAY</c>.</summary>
    public const double EdgeScrollInterval = 0.020;

    /// <summary>Speed level used for the edge scroll, the <c>NORMAL</c> default of the settings.</summary>
    public const int ScrollSpeedLevel = 2;

    /// <summary>Pixels an edge scroll step moves, <c>2 &lt;&lt; scrollSpeed</c>.</summary>
    public const int EdgeScrollStep = 2 << ScrollSpeedLevel;

    /// <summary>Most edge scroll steps applied in one frame.</summary>
    /// <remarks>
    /// A stalled main loop (a debugger break, a window drag) can hand in a large
    /// <c>elapsedSeconds</c>; without a cap the view would jump across the whole map at once.
    /// </remarks>
    private const int MaxEdgeStepsPerUpdate = 4;

    private const int ScrollLeft = 1;
    private const int ScrollRight = 2;
    private const int ScrollTop = 4;
    private const int ScrollBottom = 8;

    private readonly AdventureMapView _view;

    private Point? _cursor;
    private Point? _dragOrigin;
    private Point _dragAnchor;
    private bool _leftHeld;
    private bool _dragging;
    private double _edgeElapsed;

    /// <summary>Creates a controller that moves <paramref name="view"/>.</summary>
    /// <param name="view">The view whose <see cref="AdventureMapView.Scroll"/> is changed.</param>
    public MapScrollController(AdventureMapView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
    }

    /// <summary>Whether a press has already become a drag, i.e. the gesture is no longer a click.</summary>
    public bool IsDragging => _dragging;

    /// <summary>Last known pointer position in logical pixels, or <see langword="null"/> outside the window.</summary>
    public Point? Cursor => _cursor;

    /// <summary>
    /// Records the pointer. While the left button is held a movement beyond <see cref="DragThreshold"/>
    /// starts the drag and shifts the view by <c>last - current</c>, the direction of
    /// <c>SetCenterInPixels( center + last - current )</c>: the terrain moves with the pointer.
    /// </summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> when it left the window.</param>
    public void SetCursor(Point? cursor)
    {
        if (_cursor == cursor)
        {
            return;
        }

        _cursor = cursor;

        if (!_leftHeld || cursor is not { } point)
        {
            return;
        }

        if (!_dragging)
        {
            if (_dragOrigin is not { } origin
                || (Math.Abs(origin.X - point.X) <= DragThreshold && Math.Abs(origin.Y - point.Y) <= DragThreshold))
            {
                return;
            }

            // The reference starts the drag from the press position, so the movement that crossed the
            // threshold is part of the first shift instead of being swallowed.
            _dragging = true;
            _dragAnchor = origin;
        }

        _view.ScrollBy(_dragAnchor.X - point.X, _dragAnchor.Y - point.Y);
        _dragAnchor = point;
    }

    /// <summary>Records the left button; a release ends a drag and turns the next gesture back into a click.</summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down) => SetDragButton(down);

    /// <summary>
    /// Records the button that drags the map when the left one has another job: the editor paints with
    /// the left button and pans with the right one, so it drives the same state machine from there.
    /// </summary>
    /// <param name="down">Whether the pan button is now held.</param>
    public void SetPanButton(bool down) => SetDragButton(down);

    private void SetDragButton(bool down)
    {
        if (down)
        {
            if (_leftHeld)
            {
                return;
            }

            _leftHeld = true;
            _dragOrigin = _cursor;
            _dragging = false;
            _edgeElapsed = 0;
            return;
        }

        _leftHeld = false;
        _dragging = false;
        _dragOrigin = null;
    }

    /// <summary>
    /// Advances the edge scroll by <paramref name="elapsedSeconds"/>. The pointer must be inside the
    /// sixteen pixel border of the viewport and no drag may be running, which is the
    /// <c>assert( !isDragScroll() )</c> of <c>GameArea::SetScroll</c>.
    /// </summary>
    /// <param name="elapsedSeconds">Time since the last frame.</param>
    /// <returns>Whether the view moved.</returns>
    public bool Update(double elapsedSeconds)
    {
        if (_dragging || _cursor is not { } cursor)
        {
            _edgeElapsed = 0;
            return false;
        }

        int direction = DirectionAt(cursor);

        if (direction == 0 || !_view.IsScrollingPossible)
        {
            _edgeElapsed = 0;
            return false;
        }

        _edgeElapsed += elapsedSeconds;

        bool moved = false;
        int steps = 0;

        while (_edgeElapsed >= EdgeScrollInterval && steps < MaxEdgeStepsPerUpdate)
        {
            _edgeElapsed -= EdgeScrollInterval;
            moved |= Scroll(direction);
            ++steps;
        }

        // Drop the backlog of a long frame instead of turning it into a burst on the next ones.
        _edgeElapsed = Math.Min(_edgeElapsed, EdgeScrollInterval);

        return moved;
    }

    /// <summary>
    /// Rolls the map with the wheel by whole tiles, one tile per notch. Not part of the reference on
    /// this screen (see the remarks of the class); the step matches the arrow keys.
    /// </summary>
    /// <param name="columns">Tiles to scroll right; negative scrolls left.</param>
    /// <param name="rows">Tiles to scroll down; negative scrolls up.</param>
    /// <returns>Whether the view moved.</returns>
    public bool ScrollByWheel(int columns, int rows)
    {
        if (columns == 0 && rows == 0)
        {
            return false;
        }

        Point before = _view.Scroll;

        _view.ScrollBy(
            columns * _view.Tiles.TileWidth,
            rows * _view.Tiles.TileHeight);

        return _view.Scroll != before;
    }

    /// <summary>Bits of the directions whose scroll region contains <paramref name="cursor"/>.</summary>
    private int DirectionAt(Point cursor)
    {
        Size viewport = _view.Viewport;
        int direction = 0;

        if (cursor.X < EdgeWidth)
        {
            direction |= ScrollLeft;
        }

        if (cursor.X >= viewport.Width - EdgeWidth)
        {
            direction |= ScrollRight;
        }

        if (cursor.Y < EdgeWidth)
        {
            direction |= ScrollTop;
        }

        if (cursor.Y >= viewport.Height - EdgeWidth)
        {
            direction |= ScrollBottom;
        }

        return direction;
    }

    /// <summary>Applies one edge scroll step of <see cref="EdgeScrollStep"/> pixels.</summary>
    private bool Scroll(int direction)
    {
        Point before = _view.Scroll;

        _view.ScrollBy(
            (direction & ScrollLeft) != 0 ? -EdgeScrollStep : (direction & ScrollRight) != 0 ? EdgeScrollStep : 0,
            (direction & ScrollTop) != 0 ? -EdgeScrollStep : (direction & ScrollBottom) != 0 ? EdgeScrollStep : 0);

        return _view.Scroll != before;
    }
}
