using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Timing;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Interface;

/// <summary>
/// Direction of a scroll request of the map viewport, <c>Interface::ScrollingType</c>
/// (<c>gui/interface_gamearea.h:34-41</c>).
/// </summary>
[Flags]
public enum ScrollingType
{
    /// <summary>No scrolling, <c>SCROLL_NONE</c>.</summary>
    None = 0x00,

    /// <summary>Scroll towards the left edge of the map, <c>SCROLL_LEFT</c>.</summary>
    Left = 0x01,

    /// <summary>Scroll towards the right edge of the map, <c>SCROLL_RIGHT</c>.</summary>
    Right = 0x02,

    /// <summary>Scroll towards the top edge of the map, <c>SCROLL_TOP</c>.</summary>
    Top = 0x04,

    /// <summary>Scroll towards the bottom edge of the map, <c>SCROLL_BOTTOM</c>.</summary>
    Bottom = 0x08,
}

/// <summary>
/// Which kinds of map content a render pass has to draw, <c>Interface::RedrawLevelType</c>
/// (<c>gui/interface_gamearea.h:43-56</c>).
/// </summary>
/// <remarks>
/// <see cref="Objects"/> is <c>0</c> on purpose: it is the baseline every view draws, so it cannot be tested with a
/// flag. <see cref="Towns"/> only shows up in the View All / View Towns spells and <see cref="Passabilities"/> only
/// in the map editor.
/// </remarks>
[Flags]
public enum RedrawLevelType
{
    /// <summary>Terrain and objects, always drawn, <c>LEVEL_OBJECTS</c>.</summary>
    Objects = 0x00,

    /// <summary>Heroes and their flags, <c>LEVEL_HEROES</c>.</summary>
    Heroes = 0x01,

    /// <summary>Fog of war, <c>LEVEL_FOG</c>.</summary>
    Fog = 0x02,

    /// <summary>Hero paths, <c>LEVEL_ROUTES</c>.</summary>
    Routes = 0x04,

    /// <summary>Castles, <c>LEVEL_TOWNS</c>.</summary>
    Towns = 0x08,

    /// <summary>Passability overlay of the editor, <c>LEVEL_PASSABILITIES</c>.</summary>
    Passabilities = 0x10,

    /// <summary>Everything the game view draws, <c>LEVEL_ALL</c>.</summary>
    All = Objects | Heroes | Fog | Routes,
}

/// <summary>
/// One entry of the map animation queue, <c>Interface::BaseObjectAnimationInfo</c>
/// (<c>gui/interface_gamearea.h:58-84</c>).
/// </summary>
/// <remarks>
/// The reference deletes the copy constructor and hands the entries around as <c>std::shared_ptr</c>; the port keeps
/// them as class instances owned by the queue, so the identity stays unique as well.
/// </remarks>
public abstract class BaseObjectAnimationInfo
{
    /// <summary>Builds an entry without an object, the default constructor of the reference.</summary>
    protected BaseObjectAnimationInfo()
    {
    }

    /// <summary>Builds an entry for an object, <c>BaseObjectAnimationInfo( uid, tileId, type )</c>.</summary>
    /// <param name="uid">Unique id of the map object.</param>
    /// <param name="tileId">Tile the object stands on.</param>
    /// <param name="type">Kind of the object.</param>
    protected BaseObjectAnimationInfo(uint uid, int tileId, Mp2ObjectType type)
    {
        Uid = uid;
        TileId = tileId;
        Type = type;
    }

    /// <summary>Unique id of the map object.</summary>
    public uint Uid { get; set; }

    /// <summary>Tile the object stands on, <c>-1</c> when there is none.</summary>
    public int TileId { get; set; } = -1;

    /// <summary>Kind of the map object.</summary>
    public Mp2ObjectType Type { get; set; }

    /// <summary>Opacity the object is drawn with, <c>255</c> when fully visible.</summary>
    public byte AlphaValue { get; set; } = 255;

    /// <summary>Advances one frame, <c>update()</c>.</summary>
    /// <returns><see langword="true"/> when the animation has finished.</returns>
    public abstract bool Update();

    /// <summary>Whether the animation has finished, <c>isAnimationCompleted()</c>.</summary>
    public abstract bool IsAnimationCompleted { get; }
}

/// <summary>
/// An object that fades away, <c>Interface::ObjectFadingOutInfo</c> (<c>gui/interface_gamearea.h:86-107</c>).
/// </summary>
public sealed class ObjectFadingOutInfo : BaseObjectAnimationInfo
{
    /// <summary>Amount of opacity removed per frame, <c>alphaStep</c>.</summary>
    public const byte AlphaStep = 20;

    /// <summary>Builds an entry for an object.</summary>
    /// <param name="uid">Unique id of the map object.</param>
    /// <param name="tileId">Tile the object stands on.</param>
    /// <param name="type">Kind of the object.</param>
    public ObjectFadingOutInfo(uint uid, int tileId, Mp2ObjectType type)
        : base(uid, tileId, type)
    {
    }

    /// <inheritdoc />
    public override bool Update()
    {
        AlphaValue = AlphaValue >= AlphaStep ? (byte)(AlphaValue - AlphaStep) : (byte)0;

        return AlphaValue == 0;
    }

    /// <inheritdoc />
    public override bool IsAnimationCompleted => AlphaValue == 0;

    /// <summary>
    /// Takes the faded object off its tile, the body of <c>~ObjectFadingOutInfo()</c>
    /// (<c>gui/interface_gamearea.cpp:1278-1285</c>).
    /// </summary>
    /// <remarks>
    /// The reference relies on the destructor, which runs when the animation queue drops its <c>shared_ptr</c>. The
    /// port has no deterministic finaliser and the queue keeps plain instances, so
    /// <see cref="UpdateObjectAnimationInfo"/> calls this method at the point the reference destroys the entry.
    /// </remarks>
    public void RemoveObjectFromTile()
    {
        if (TileId < 0)
        {
            // The reference always builds a fading entry for a real tile; the port's default is -1.
            return;
        }

        Tile tile = World.Current.GetTile(TileId);

        if (tile.MainObjectType == Type)
        {
            tile.RemoveMainObject(World.Current);
        }
    }
}

/// <summary>
/// An object that fades in, <c>Interface::ObjectFadingInInfo</c> (<c>gui/interface_gamearea.h:109-142</c>).
/// </summary>
public sealed class ObjectFadingInInfo : BaseObjectAnimationInfo
{
    /// <summary>Amount of opacity added per frame, <c>alphaStep</c>.</summary>
    public const byte AlphaStep = 20;

    /// <summary>Builds an entry for an object that starts invisible.</summary>
    /// <param name="uid">Unique id of the map object.</param>
    /// <param name="tileId">Tile the object stands on.</param>
    /// <param name="type">Kind of the object.</param>
    public ObjectFadingInInfo(uint uid, int tileId, Mp2ObjectType type)
        : base(uid, tileId, type)
    {
        AlphaValue = 0;
    }

    /// <inheritdoc />
    public override bool Update()
    {
        AlphaValue = 255 - AlphaValue >= AlphaStep ? (byte)(AlphaValue + AlphaStep) : (byte)255;

        return AlphaValue == 255;
    }

    /// <inheritdoc />
    public override bool IsAnimationCompleted => AlphaValue == 255;
}

/// <summary>
/// The scrolling map viewport, <c>Interface::GameArea</c> (<c>gui/interface_gamearea.h:152-320</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Status: complete.</b> This file holds everything that turns a frame position into a tile and moves the
/// viewport: the placement and clamping of <c>SetAreaPosition</c>, the tile arithmetic of
/// <c>GetValidTileIdFromPoint</c> / <c>GetRelativeTilePosition</c>, the edge scroll of <c>Scroll</c> /
/// <c>SetScroll</c>, the drag and inertia handling of <c>QueueEventProcessing</c> / <c>updateInertia</c>, the
/// object animation queue, and the fog recomputation of <c>updateMapFogDirections</c>. The render passes of
/// <c>gui/interface_gamearea.cpp</c> — <see cref="Redraw"/>, <see cref="RedrawOnlyFog"/>,
/// <see cref="RenderTileAreaSelect"/>, <see cref="GenerateUltimateArtifactAreaSurface"/>,
/// <see cref="RunSingleObjectAnimation"/> and their <c>TileUnfitRenderObjectInfo</c> helpers — live in
/// <c>GameArea.Render.cs</c>.
/// </para>
/// <para>
/// <b>Deviations.</b> <c>mouseIndicatesFastScroll</c> reads <c>Display::instance()</c> in the reference for the
/// frame edges; the port has no display singleton, so <see cref="Generate"/> remembers the frame size instead — the
/// same choice <see cref="BaseInterface"/> made for its scroll helpers. The control panel guard of
/// <c>QueueEventProcessing</c> reads <c>AdventureMap::Get().getControlPanel().GetArea()</c>, which has no owner in
/// the port yet, so it asks the interface through <see cref="BaseInterface.ControlPanelAreaForInput"/>.
/// </para>
/// </remarks>
public sealed partial class GameArea
{
    /// <summary>How far the mouse travels before a press becomes a drag, <c>minimalRequiredDraggingMovement</c>.</summary>
    private const int MinimalRequiredDraggingMovement = 10;

    /// <summary>Travel that re-arms fast scrolling while the cursor sits on a side border.</summary>
    private const int FastScrollDeadZone = 3;

    private readonly BaseInterface _interface;
    private readonly List<BaseObjectAnimationInfo> _animationInfo = [];
    private readonly ScrollInertia _inertiaHandler = new();
    private readonly Time _scrollTime = new();

    private Rect _windowRoi;
    private Point _topLeftTileOffset;
    private Size _screenSize;

    private int _minLeftOffset;
    private int _maxLeftOffset;
    private int _minTopOffset;
    private int _maxTopOffset;

    private Size _visibleTileCount;

    private int _prevIndexPos;
    private ScrollingType _scrollDirection;
    private bool _updateCursor;

    private Point _lastMouseDragPosition;
    private Point _mousePositionForFastScroll;
    private bool _mouseDraggingInitiated;
    private bool _mouseDraggingMovement;
    private bool _needRedrawByMouseDragging;
    private bool _isFastScrollEnabled;
    private bool _resetMousePositionForFastScroll;

    /// <summary>Wraps the interface the viewport belongs to, <c>GameArea( BaseInterface &amp; )</c>.</summary>
    /// <param name="interface">Owner of the viewport, used for the redraw and cursor callbacks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="interface"/> is <see langword="null"/>.</exception>
    public GameArea(BaseInterface @interface)
    {
        ArgumentNullException.ThrowIfNull(@interface);

        _interface = @interface;
    }

    /// <summary>The visible part of the map window, <c>GetROI()</c>.</summary>
    public Rect Roi => _windowRoi;

    /// <summary>Middle of the viewport in pixels, <c>_middlePoint()</c>.</summary>
    public Point MiddlePoint => new(_windowRoi.Width / 2, _windowRoi.Height / 2);

    /// <summary>Center of the viewport in pixels, <c>getCurrentCenterInPixels()</c>.</summary>
    public Point CurrentCenterInPixels => _topLeftTileOffset + MiddlePoint;

    /// <summary>Whether a scroll request is pending, <c>NeedScroll()</c>.</summary>
    public bool NeedScroll => _scrollDirection != ScrollingType.None;

    /// <summary>Queues an object animation, <c>addObjectAnimationInfo()</c>.</summary>
    /// <param name="info">Animation to run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    public void AddObjectAnimationInfo(BaseObjectAnimationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        _animationInfo.Add(info);
    }

    /// <summary>Centers the viewport on a tile, <c>SetCenter()</c>.</summary>
    /// <param name="tile">Tile id to center on.</param>
    public void SetCenter(Point tile)
    {
        SetCenterToTile(tile);
        _scrollDirection = ScrollingType.None;
    }

    /// <summary>Places the viewport, <c>SetAreaPosition()</c>.</summary>
    /// <param name="x">Left edge of the viewport in the frame.</param>
    /// <param name="y">Top edge of the viewport in the frame.</param>
    /// <param name="width">Width of the viewport.</param>
    /// <param name="height">Height of the viewport.</param>
    public void SetAreaPosition(int x, int y, int width, int height)
    {
        _windowRoi = new Rect(x, y, width, height);

        int worldWidth = World.Current.Width * GameBorder.TileWidth;
        int worldHeight = World.Current.Height * GameBorder.TileWidth;

        if (worldWidth > width)
        {
            _minLeftOffset = -(width / 2) - (GameBorder.TileWidth / 2);
            _maxLeftOffset = worldWidth - (width / 2);
        }
        else
        {
            _minLeftOffset = -(width - worldWidth) / 2;
            _maxLeftOffset = _minLeftOffset;
        }

        if (worldHeight > height)
        {
            _minTopOffset = -(height / 2) - (GameBorder.TileWidth / 2);
            _maxTopOffset = worldHeight - (height / 2);
        }
        else
        {
            _minTopOffset = -(height - worldHeight) / 2;
            _maxTopOffset = _minTopOffset;
        }

        // One extra tile on both axes, so tiles cut by the window edge are drawn as well.
        _visibleTileCount = new Size(
            ((width + GameBorder.TileWidth - 1) / GameBorder.TileWidth) + 1,
            ((height + GameBorder.TileWidth - 1) / GameBorder.TileWidth) + 1);

        SetCenterToTile(new Point(World.Current.Width / 2, World.Current.Height / 2));
    }

    /// <summary>The tiles the viewport shows, <c>GetVisibleTileROI()</c>.</summary>
    /// <remarks>The reference warns not to call this in heavy loops: the start tile is recomputed every time.</remarks>
    public Rect VisibleTileRoi => new(GetStartTile(), _visibleTileCount);

    /// <summary>Builds the viewport for the given frame size, <c>generate()</c>.</summary>
    /// <param name="screenSize">Size of the frame.</param>
    /// <param name="withoutBorders">Whether the border of the adventure screen is hidden.</param>
    public void Generate(Size screenSize, bool withoutBorders)
    {
        _screenSize = screenSize;

        if (withoutBorders)
        {
            SetAreaPosition(0, 0, screenSize.Width, screenSize.Height);
        }
        else
        {
            SetAreaPosition(
                GameBorder.BorderWidth,
                GameBorder.BorderWidth,
                screenSize.Width - GameBorder.RadarWidth - (3 * GameBorder.BorderWidth),
                screenSize.Height - (2 * GameBorder.BorderWidth));
        }
    }

    /// <summary>Whether the map is being dragged with the mouse, <c>isDragScroll()</c>.</summary>
    public bool IsDragScroll() => _mouseDraggingMovement;

    /// <summary>Whether a drag left a frame to repaint, <c>needDragScrollRedraw()</c>.</summary>
    public bool NeedDragScrollRedraw() => _needRedrawByMouseDragging;

    /// <summary>Whether the cursor is far enough out to scroll fast, <c>isFastScrollEnabled()</c>.</summary>
    public bool IsFastScrollEnabled() => _isFastScrollEnabled;

    /// <summary>Places a point of the frame on the map, <c>getInternalPosition()</c>.</summary>
    /// <param name="position">Position relative to the frame.</param>
    /// <returns>Position in map pixels.</returns>
    public Point GetInternalPosition(Point position) => _topLeftTileOffset + position - new Point(_windowRoi.X, _windowRoi.Y);

    /// <summary>Shifts the center of the viewport in pixels, <c>ShiftCenter()</c>.</summary>
    /// <param name="offset">Offset in pixels.</param>
    public void ShiftCenter(Point offset) => SetCenterInPixels(_topLeftTileOffset + MiddlePoint + offset);

    /// <summary>Asks for a cursor update on the next frame, <c>SetUpdateCursor()</c>.</summary>
    public void SetUpdateCursor() => _updateCursor = true;

    /// <summary>Asks for a repaint of the viewport, <c>SetRedraw()</c>.</summary>
    public void SetRedraw() => _interface.SetRedraw(FHeroes2.UI.Interface.Redraw.GameArea);

    /// <summary>Scrolls one step in the requested direction, <c>Scroll()</c>.</summary>
    public void Scroll()
    {
        AdventureMapScrollSpeed scrollSpeed = Settings.Get().ScrollSpeed;

        if (scrollSpeed == AdventureMapScrollSpeed.None)
        {
            // No scrolling.
            _scrollDirection = ScrollingType.None;

            return;
        }

        int shift = 2 << (int)scrollSpeed;

        int offsetX = 0;
        int offsetY = 0;

        if ((_scrollDirection & ScrollingType.Left) == ScrollingType.Left)
        {
            offsetX = -shift;
        }
        else if ((_scrollDirection & ScrollingType.Right) == ScrollingType.Right)
        {
            offsetX = shift;
        }

        if ((_scrollDirection & ScrollingType.Top) == ScrollingType.Top)
        {
            offsetY = -shift;
        }
        else if ((_scrollDirection & ScrollingType.Bottom) == ScrollingType.Bottom)
        {
            offsetY = shift;
        }

        ShiftCenter(new Point(offsetX, offsetY));

        _scrollDirection = ScrollingType.None;
    }

    /// <summary>Requests a scroll, <c>SetScroll( direction )</c>.</summary>
    /// <param name="direction">Direction to scroll towards.</param>
    public void SetScroll(ScrollingType direction)
    {
        // The reference asserts !isDragScroll() here: a drag scroll and an edge scroll never overlap.

        // Edge scrolling is not allowed while inertia is running.
        if (_inertiaHandler.IsActive)
        {
            return;
        }

        if ((direction & ScrollingType.Left) == ScrollingType.Left)
        {
            if (_topLeftTileOffset.X > _minLeftOffset)
            {
                _scrollDirection |= direction;
                _updateCursor = true;
            }
        }
        else if ((direction & ScrollingType.Right) == ScrollingType.Right)
        {
            if (_topLeftTileOffset.X < _maxLeftOffset)
            {
                _scrollDirection |= direction;
                _updateCursor = true;
            }
        }

        if ((direction & ScrollingType.Top) == ScrollingType.Top)
        {
            if (_topLeftTileOffset.Y > _minTopOffset)
            {
                _scrollDirection |= direction;
                _updateCursor = true;
            }
        }
        else if ((direction & ScrollingType.Bottom) == ScrollingType.Bottom)
        {
            if (_topLeftTileOffset.Y < _maxTopOffset)
            {
                _scrollDirection |= direction;
                _updateCursor = true;
            }
        }

        _scrollTime.Reset();
    }

    /// <summary>Cursor of the scroll request, <c>GetScrollCursor()</c>.</summary>
    /// <returns>Cursor theme id the reference hands to its cursor layer.</returns>
    public int GetScrollCursor()
    {
        switch (_scrollDirection)
        {
            case ScrollingType.Left | ScrollingType.Top:
                return (int)CursorTheme.ScrollTopLeft;
            case ScrollingType.Left | ScrollingType.Bottom:
                return (int)CursorTheme.ScrollBottomLeft;
            case ScrollingType.Right | ScrollingType.Top:
                return (int)CursorTheme.ScrollTopRight;
            case ScrollingType.Right | ScrollingType.Bottom:
                return (int)CursorTheme.ScrollBottomRight;
            case ScrollingType.Top:
                return (int)CursorTheme.ScrollTop;
            case ScrollingType.Bottom:
                return (int)CursorTheme.ScrollBottom;
            case ScrollingType.Right:
                return (int)CursorTheme.ScrollRight;
            case ScrollingType.Left:
                return (int)CursorTheme.ScrollLeft;
            default:
                break;
        }

        return (int)CursorTheme.None;
    }

    /// <summary>Centers the viewport on a pixel position, <c>SetCenterInPixels()</c>.</summary>
    /// <param name="point">Position in map pixels.</param>
    public void SetCenterInPixels(Point point)
    {
        Point middlePos = MiddlePoint;

        int offsetX = Math.Clamp(point.X - middlePos.X, _minLeftOffset, _maxLeftOffset);
        int offsetY = Math.Clamp(point.Y - middlePos.Y, _minTopOffset, _maxTopOffset);

        _topLeftTileOffset = new Point(offsetX, offsetY);
    }

    /// <summary>Blits an image on a tile, <c>BlitOnTile()</c>.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="src">Image to blit.</param>
    /// <param name="ox">Horizontal offset inside the tile.</param>
    /// <param name="oy">Vertical offset inside the tile.</param>
    /// <param name="mp">Tile position.</param>
    /// <param name="flip">Whether the image is mirrored.</param>
    /// <param name="alpha">Opacity of the blit.</param>
    public void BlitOnTile(PixelBuffer dst, Sprite src, int ox, int oy, Point mp, bool flip, byte alpha)
    {
        ArgumentNullException.ThrowIfNull(src);

        BlitOnTile(dst, src, src.Image.Bounds, ox, oy, mp, flip, alpha);
    }

    /// <summary>Blits a part of an image on a tile, <c>BlitOnTile()</c> with a source rectangle.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="src">Image to blit.</param>
    /// <param name="srcRoi">Part of the image to blit.</param>
    /// <param name="ox">Horizontal offset inside the tile.</param>
    /// <param name="oy">Vertical offset inside the tile.</param>
    /// <param name="mp">Tile position.</param>
    /// <param name="flip">Whether the image is mirrored.</param>
    /// <param name="alpha">Opacity of the blit.</param>
    /// <remarks>
    /// The offsets move the whole image, they do not cut it: the reference clips
    /// <c>{ tileOffset + ( ox, oy ), srcRoi.size }</c> against the viewport and blits the corresponding part of
    /// <paramref name="srcRoi"/> there, so the sprite keeps the anchor the ICN header gives it - a multi-tile
    /// object whose part hangs over the tiles above and to the left lands there, and the part is not truncated.
    /// </remarks>
    public void BlitOnTile(PixelBuffer dst, Sprite src, Rect srcRoi, int ox, int oy, Point mp, bool flip, byte alpha)
    {
        ArgumentNullException.ThrowIfNull(dst);

        Point dstPos = GetRelativeTilePosition(mp) + new Point(ox, oy);

        if (alpha < 255 && MapSprites.GamePalette is not { } gamePalette)
        {
            // The reference blends through the global palette of the display; without one the blend would have to be
            // guessed, and a guessed colour is worse than a loud failure.
            throw new InvalidOperationException("MapSprites.Initialize must be given the game palette before alpha blits.");
        }

        ImageOps.AlphaBlitSpriteClipped(
            dst,
            dstPos,
            src,
            srcRoi,
            _windowRoi,
            alpha,
            flip,
            MapSprites.GamePalette!);
    }

    /// <summary>Draws a terrain tile, <c>DrawTile()</c>.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="src">Terrain image.</param>
    /// <param name="mp">Tile position.</param>
    public void DrawTile(PixelBuffer dst, PixelBuffer src, Point mp)
    {
        ArgumentNullException.ThrowIfNull(dst);
        ArgumentNullException.ThrowIfNull(src);

        Point dstPos = GetRelativeTilePosition(mp);

        ImageOps.Blit(dst, dstPos, src, src.Bounds);
    }

    /// <summary>Recomputes the fog directions of the whole map, <c>updateMapFogDirections()</c>.</summary>
    public static void UpdateMapFogDirections()
    {
        PlayerColor friendColors = Players.FriendColors();

        TileFog.UpdateFogDirectionsInArea(
            new Point(),
            new Point(World.Current.Width - 1, World.Current.Height - 1),
            friendColors);
    }

    /// <summary>Processes the events of the viewport, <c>QueueEventProcessing()</c>.</summary>
    /// <remarks>
    /// The reference reads <c>LocalEvent::Get()</c>; the port takes the frame's state as a parameter, and this overload
    /// is the one that still answers with the process wide state, kept for the callers that have no frame of their own
    /// (<c>GameHost</c>).
    /// </remarks>
    public void QueueEventProcessing() => QueueEventProcessing(LocalEvent.Current);

    /// <summary>Processes the events of the viewport, <c>QueueEventProcessing()</c>.</summary>
    /// <param name="events">The event state of the frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public void QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        LocalEvent localEvent = events;
        Point mousePosition = localEvent.CursorPosition;
        Settings settings = Settings.Get();

        if (!localEvent.IsMouseLeftButtonPressed)
        {
            if (_mouseDraggingMovement && settings.IsMapSmoothScrollingEnabled)
            {
                _inertiaHandler.CommitRelease();
            }

            _mouseDraggingInitiated = false;
            _mouseDraggingMovement = false;
            _needRedrawByMouseDragging = false;
        }
        else if (_interface.UseMouseDragMovement)
        {
            if (!_mouseDraggingInitiated)
            {
                _mouseDraggingInitiated = true;
                _lastMouseDragPosition = mousePosition;
                _inertiaHandler.Reset();
            }
            else if (Math.Abs(_lastMouseDragPosition.X - mousePosition.X) > MinimalRequiredDraggingMovement
                     || Math.Abs(_lastMouseDragPosition.Y - mousePosition.Y) > MinimalRequiredDraggingMovement)
            {
                _mouseDraggingMovement = true;
            }
        }

        if (_mouseDraggingMovement && localEvent.IsMouseLeftButtonPressedInArea(_windowRoi))
        {
            if (_lastMouseDragPosition == mousePosition)
            {
                _needRedrawByMouseDragging = false;
            }
            else
            {
                Point delta = _lastMouseDragPosition - mousePosition;

                if (settings.IsMapSmoothScrollingEnabled)
                {
                    _inertiaHandler.AddMovementStep(delta);
                }

                // Move the center first: the reference only redraws when the mouse actually moved.
                _needRedrawByMouseDragging = true;
                SetCenterInPixels(CurrentCenterInPixels + delta);
                _lastMouseDragPosition = mousePosition;
            }

            return;
        }

        int index = GetValidTileIdFromPoint(mousePosition);

        if (!IsValidAbsIndex(index))
        {
            // Change the cursor image when it leaves the map or when a cursor update was requested.
            if (_updateCursor || index != _prevIndexPos)
            {
                _interface.UpdateCursor(index);

                _prevIndexPos = index;
                _updateCursor = false;
            }

            return;
        }

        if (settings.IsHideInterface && settings.ShowControlPanel && _interface.ControlPanelAreaForInput is { } panelArea
            && localEvent.IsMouseCursorPosInArea(panelArea))
        {
            return;
        }

        Point tileOffset = GetInternalPosition(mousePosition);

        // The reference uses _windowROI.x for both components here. Both edges of the frame are equal in every layout
        // the port has, so the value stays correct and the quirk is kept on purpose.
        Point tilePos = new(
            ((tileOffset.X / GameBorder.TileWidth) * GameBorder.TileWidth) - _topLeftTileOffset.X + _windowRoi.X,
            ((tileOffset.Y / GameBorder.TileWidth) * GameBorder.TileWidth) - _topLeftTileOffset.Y + _windowRoi.X);

        Rect tileRoi = new(tilePos.X, tilePos.Y, GameBorder.TileWidth, GameBorder.TileWidth);

        if (localEvent.MouseClickLeft(tileRoi))
        {
            _interface.MouseCursorAreaClickLeft(index);
        }
        else if (localEvent.IsMouseRightButtonPressedInArea(tileRoi))
        {
            _interface.MouseCursorAreaPressRight(index);
        }
        else if (localEvent.MouseLongPressLeft(tileRoi))
        {
            _interface.MouseCursorAreaLongPressLeft(index);
        }

        // The cursor may have moved while the click was handled.
        index = GetValidTileIdFromPoint(localEvent.CursorPosition);

        if (_updateCursor || index != _prevIndexPos)
        {
            _interface.UpdateCursor(index);

            _prevIndexPos = index;
            _updateCursor = false;
        }
    }

    /// <summary>Tile under a point of the frame, <c>GetValidTileIdFromPoint()</c>.</summary>
    /// <param name="point">Position in the frame.</param>
    /// <returns>The tile id, or <c>-1</c> outside the map.</returns>
    public int GetValidTileIdFromPoint(Point point)
    {
        Point offset = GetInternalPosition(point);

        if (offset.X < 0 || offset.Y < 0)
        {
            return -1;
        }

        int x = offset.X / GameBorder.TileWidth;
        int y = offset.Y / GameBorder.TileWidth;

        if (x >= World.Current.Width || y >= World.Current.Height)
        {
            return -1;
        }

        return (y * World.Current.Width) + x;
    }

    /// <summary>Position of a tile in the frame, <c>GetRelativeTilePosition()</c>.</summary>
    /// <param name="tileId">Tile id as a point.</param>
    /// <returns>Top-left corner of the tile relative to the frame.</returns>
    public Point GetRelativeTilePosition(Point tileId) =>
        new(
            (tileId.X * GameBorder.TileWidth) - _topLeftTileOffset.X + _windowRoi.X,
            (tileId.Y * GameBorder.TileWidth) - _topLeftTileOffset.Y + _windowRoi.Y);

    /// <summary>Opacity an object is currently drawn with, <c>getObjectAlphaValue()</c>.</summary>
    /// <param name="tileId">Tile the object stands on.</param>
    /// <param name="type">Kind of the object.</param>
    /// <returns>Opacity, <c>255</c> when the object is not animating.</returns>
    public byte GetObjectAlphaValue(int tileId, Mp2ObjectType type)
    {
        foreach (BaseObjectAnimationInfo info in _animationInfo)
        {
            if (info.TileId == tileId && info.Type == type)
            {
                return info.AlphaValue;
            }
        }

        return 255;
    }

    /// <summary>Opacity an object is currently drawn with, <c>getObjectAlphaValue()</c> by uid.</summary>
    /// <param name="uid">Unique id of the map object.</param>
    /// <returns>Opacity, <c>255</c> when the object is not animating.</returns>
    public byte GetObjectAlphaValue(uint uid)
    {
        foreach (BaseObjectAnimationInfo info in _animationInfo)
        {
            if (info.Uid == uid)
            {
                return info.AlphaValue;
            }
        }

        return 255;
    }

    /// <summary>Advances the inertia scroll or drag scroll by one frame, <c>updateInertia()</c>.</summary>
    /// <returns><see langword="true"/> when the frame has to be repainted.</returns>
    public bool UpdateInertia() => _inertiaHandler.Update(() => CurrentCenterInPixels, SetCenterInPixels);

    /// <summary>Turns the fast scroll mode on or off, <c>setFastScrollStatus()</c>.</summary>
    /// <param name="enable">Whether the cursor is far enough out to scroll fast.</param>
    public void SetFastScrollStatus(bool enable)
    {
        _isFastScrollEnabled = enable;
        _resetMousePositionForFastScroll = true;
    }

    /// <summary>Whether the cursor asks for a fast scroll, <c>mouseIndicatesFastScroll()</c>.</summary>
    /// <param name="mousePosition">Cursor position in the frame.</param>
    /// <returns><see langword="true"/> when the cursor is in the fast scroll margin.</returns>
    public bool MouseIndicatesFastScroll(Point mousePosition)
    {
        // Remember the reference point the current status was decided from.
        if (_resetMousePositionForFastScroll)
        {
            _mousePositionForFastScroll = mousePosition;
            _resetMousePositionForFastScroll = false;
        }

        if (BaseInterface.IsScrollLeft(_mousePositionForFastScroll))
        {
            if (mousePosition.X > _mousePositionForFastScroll.X)
            {
                // Moving away from the border: the reference point follows the cursor.
                _mousePositionForFastScroll = mousePosition;
            }
            else if (mousePosition.X < _mousePositionForFastScroll.X
                     || (Math.Abs(mousePosition.Y - _mousePositionForFastScroll.Y) > FastScrollDeadZone
                         && mousePosition.X <= 0))
            {
                // Towards the border, or along it past the dead zone: fast scrolling can be re-enabled.
                return true;
            }
        }
        else if (BaseInterface.IsScrollRight(_mousePositionForFastScroll, _screenSize))
        {
            if (mousePosition.X < _mousePositionForFastScroll.X)
            {
                _mousePositionForFastScroll = mousePosition;
            }
            else if (mousePosition.X > _mousePositionForFastScroll.X
                     || (Math.Abs(mousePosition.Y - _mousePositionForFastScroll.Y) > FastScrollDeadZone
                         && mousePosition.X >= _screenSize.Width - 1))
            {
                return true;
            }
        }
        else if (BaseInterface.IsScrollTop(_mousePositionForFastScroll))
        {
            if (mousePosition.Y > _mousePositionForFastScroll.Y)
            {
                _mousePositionForFastScroll = mousePosition;
            }
            else if (mousePosition.Y < _mousePositionForFastScroll.Y
                     || (Math.Abs(mousePosition.X - _mousePositionForFastScroll.X) > FastScrollDeadZone
                         && mousePosition.Y <= 0))
            {
                return true;
            }
        }
        else if (BaseInterface.IsScrollBottom(_mousePositionForFastScroll, _screenSize))
        {
            if (mousePosition.Y < _mousePositionForFastScroll.Y)
            {
                _mousePositionForFastScroll = mousePosition;
            }
            else if (mousePosition.Y > _mousePositionForFastScroll.Y
                     || (Math.Abs(mousePosition.X - _mousePositionForFastScroll.X) > FastScrollDeadZone
                         && mousePosition.Y >= _screenSize.Height - 1))
            {
                return true;
            }
        }
        else
        {
            // Outside every scroll border: fast scrolling is definitely allowed again.
            return true;
        }

        // Still on a border, and the movement does not ask for a fast scroll yet.
        return false;
    }

    /// <summary>Centers the viewport on the middle of a tile, <c>_setCenterToTile()</c>.</summary>
    /// <param name="tile">Tile id as a point of tile coordinates.</param>
    private void SetCenterToTile(Point tile) =>
        SetCenterInPixels(new Point(
            (tile.X * GameBorder.TileWidth) + (GameBorder.TileWidth / 2),
            (tile.Y * GameBorder.TileWidth) + (GameBorder.TileWidth / 2)));

    /// <summary>Whether a tile id addresses a tile of the map, <c>Maps::isValidAbsIndex()</c>.</summary>
    /// <param name="index">Tile id to check.</param>
    /// <returns><see langword="true"/> when the id is inside the map.</returns>
    private static bool IsValidAbsIndex(int index) => index >= 0 && index < World.Current.Width * World.Current.Height;

    /// <summary>First drawn tile, <c>_getStartTileId()</c>.</summary>
    /// <returns>The tile of the top-left corner, negative on the left or top edge of the map.</returns>
    private Point GetStartTile()
    {
        // Both languages truncate towards zero, but the reference wants floor for negative offsets, hence the
        // one-tile adjustment.
        int x = _topLeftTileOffset.X < 0
            ? (_topLeftTileOffset.X - GameBorder.TileWidth - 1) / GameBorder.TileWidth
            : _topLeftTileOffset.X / GameBorder.TileWidth;

        int y = _topLeftTileOffset.Y < 0
            ? (_topLeftTileOffset.Y - GameBorder.TileWidth - 1) / GameBorder.TileWidth
            : _topLeftTileOffset.Y / GameBorder.TileWidth;

        return new Point(x, y);
    }
}
