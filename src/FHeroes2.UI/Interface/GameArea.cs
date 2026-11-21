using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

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
/// The scrolling map viewport, <c>Interface::GameArea</c> (<c>gui/interface_gamearea.h:144-320</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Status: signature-only.</b> The class is declared so that <see cref="BaseInterface"/> and the panels can be
/// written against it; the geometry of <c>gui/interface_gamearea.cpp</c> lands with G3. What is implemented here is
/// the part the reference defines inline and that needs nothing but the viewport rectangle: the ROI bookkeeping, the
/// pending scroll direction and the animation queue.
/// </para>
/// <para>
/// Several members still throw even though the reference spells them inline (<c>isDragScroll()</c>,
/// <c>getCurrentCenterInPixels()</c> and friends): they read state that only <c>generate()</c> and the scroll code
/// fill in, and a field that nothing writes yet is a compile error in this project. They land with G3.
/// </para>
/// </remarks>
public sealed class GameArea
{
    private readonly List<BaseObjectAnimationInfo> _animationInfo = [];

    private int _windowX;
    private int _windowY;
    private int _windowWidth;
    private int _windowHeight;
    private ScrollingType _scrollDirection;

    /// <summary>Wraps the interface the viewport belongs to, <c>GameArea( BaseInterface &amp; )</c>.</summary>
    /// <param name="interface">Owner of the viewport.</param>
    /// <exception cref="ArgumentNullException"><paramref name="interface"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The reference keeps the reference for the settings and the redraw calls of the viewport; the port uses it once
    /// <c>generate()</c> is ported, so nothing is stored yet.
    /// </remarks>
    public GameArea(BaseInterface @interface)
    {
        ArgumentNullException.ThrowIfNull(@interface);
    }

    /// <summary>The visible part of the map window, <c>GetROI()</c>.</summary>
    public Rect Roi => new(_windowX, _windowY, _windowWidth, _windowHeight);

    /// <summary>Middle of the viewport in pixels, <c>_middlePoint()</c>.</summary>
    public Point MiddlePoint => new(_windowWidth / 2, _windowHeight / 2);

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
        _windowX = x;
        _windowY = y;
        _windowWidth = width;
        _windowHeight = height;
    }

    /// <summary>The tiles the viewport shows, <c>GetVisibleTileROI()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public Rect VisibleTileRoi => throw new NotImplementedException("GameArea::GetVisibleTileROI lands with G3.");

    /// <summary>Builds the viewport for the given frame size, <c>generate()</c>.</summary>
    /// <param name="screenSize">Size of the frame.</param>
    /// <param name="withoutBorders">Whether the border of the adventure screen is hidden.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void Generate(Size screenSize, bool withoutBorders) => throw new NotImplementedException("GameArea::generate lands with G3.");

    /// <summary>Whether the map is being dragged with the mouse, <c>isDragScroll()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public bool IsDragScroll() => throw new NotImplementedException("GameArea::isDragScroll lands with G3.");

    /// <summary>Whether a drag left a frame to repaint, <c>needDragScrollRedraw()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public bool NeedDragScrollRedraw() => throw new NotImplementedException("GameArea::needDragScrollRedraw lands with G3.");

    /// <summary>Whether the cursor is far enough out to scroll fast, <c>isFastScrollEnabled()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public bool IsFastScrollEnabled() => throw new NotImplementedException("GameArea::isFastScrollEnabled lands with G3.");

    /// <summary>Center of the viewport in pixels, <c>getCurrentCenterInPixels()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public Point GetCurrentCenterInPixels() => throw new NotImplementedException("GameArea::getCurrentCenterInPixels lands with G3.");

    /// <summary>Places a point of the frame on the map, <c>getInternalPosition()</c>.</summary>
    /// <param name="position">Position relative to the viewport.</param>
    /// <returns>Position in map pixels.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public Point GetInternalPosition(Point position) => throw new NotImplementedException("GameArea::getInternalPosition lands with G3.");

    /// <summary>Shifts the center of the viewport in pixels, <c>ShiftCenter()</c>.</summary>
    /// <param name="offset">Offset in pixels.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void ShiftCenter(Point offset) => throw new NotImplementedException("GameArea::ShiftCenter lands with G3.");

    /// <summary>Asks for a cursor update on the next frame, <c>SetUpdateCursor()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void SetUpdateCursor() => throw new NotImplementedException("GameArea::SetUpdateCursor lands with G3.");

    /// <summary>Asks for a repaint of the viewport, <c>SetRedraw()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void SetRedraw() => throw new NotImplementedException("GameArea::SetRedraw lands with G3.");

    /// <summary>Scrolls one step in the requested direction, <c>Scroll()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void Scroll() => throw new NotImplementedException("GameArea::Scroll lands with G3.");

    /// <summary>Requests a scroll, <c>SetScroll( direction )</c>.</summary>
    /// <param name="direction">Direction to scroll towards.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void SetScroll(ScrollingType direction) => throw new NotImplementedException("GameArea::SetScroll lands with G3.");

    /// <summary>Cursor of the scroll request, <c>GetScrollCursor()</c>.</summary>
    /// <returns>Cursor theme id the reference hands to its cursor layer.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public int GetScrollCursor() => throw new NotImplementedException("GameArea::GetScrollCursor lands with G3.");

    /// <summary>Centers the viewport on a pixel position, <c>SetCenterInPixels()</c>.</summary>
    /// <param name="point">Position in map pixels.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void SetCenterInPixels(Point point) => throw new NotImplementedException("GameArea::SetCenterInPixels lands with G3.");

    /// <summary>Repaints the viewport, <c>Redraw()</c>.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="flag">Parts of the map to draw, <see cref="RedrawLevelType"/>.</param>
    /// <param name="isPuzzleDraw">Whether the puzzle map is being drawn.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void Redraw(PixelBuffer dst, RedrawLevelType flag, bool isPuzzleDraw = false)
        => throw new NotImplementedException("GameArea::Redraw lands with G3.");

    /// <summary>Repaints only the fog of war, <c>redrawOnlyFog()</c>.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void RedrawOnlyFog(PixelBuffer dst) => throw new NotImplementedException("GameArea::redrawOnlyFog lands with G3.");

    /// <summary>Draws the selection of a tile area, <c>renderTileAreaSelect()</c>.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="startTile">First tile of the area.</param>
    /// <param name="endTile">Last tile of the area.</param>
    /// <param name="isActionObject">Whether the area belongs to an action object.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void RenderTileAreaSelect(PixelBuffer dst, int startTile, int endTile, bool isActionObject)
        => throw new NotImplementedException("GameArea::renderTileAreaSelect lands with G3.");

    /// <summary>Blits an image on a tile, <c>BlitOnTile()</c>.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="src">Image to blit.</param>
    /// <param name="ox">Horizontal offset inside the tile.</param>
    /// <param name="oy">Vertical offset inside the tile.</param>
    /// <param name="mp">Tile position.</param>
    /// <param name="flip">Whether the image is mirrored.</param>
    /// <param name="alpha">Opacity of the blit.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void BlitOnTile(PixelBuffer dst, Sprite src, int ox, int oy, Point mp, bool flip, byte alpha)
        => throw new NotImplementedException("GameArea::BlitOnTile lands with G3.");

    /// <summary>Blits a part of an image on a tile, <c>BlitOnTile()</c> with a source rectangle.</summary>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="src">Image to blit.</param>
    /// <param name="srcRoi">Part of the image to blit.</param>
    /// <param name="ox">Horizontal offset inside the tile.</param>
    /// <param name="oy">Vertical offset inside the tile.</param>
    /// <param name="mp">Tile position.</param>
    /// <param name="flip">Whether the image is mirrored.</param>
    /// <param name="alpha">Opacity of the blit.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void BlitOnTile(PixelBuffer dst, Sprite src, Rect srcRoi, int ox, int oy, Point mp, bool flip, byte alpha)
        => throw new NotImplementedException("GameArea::BlitOnTile lands with G3.");

    /// <summary>Draws a terrain tile, <c>DrawTile()</c>.</summary>
    /// <param name="src">Terrain image.</param>
    /// <param name="dst">Frame buffer.</param>
    /// <param name="mp">Tile position.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void DrawTile(PixelBuffer src, PixelBuffer dst, Point mp)
        => throw new NotImplementedException("GameArea::DrawTile lands with G3.");

    /// <summary>Recomputes the fog directions of the whole map, <c>updateMapFogDirections()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public static void UpdateMapFogDirections() => throw new NotImplementedException("GameArea::updateMapFogDirections lands with G3.");

    /// <summary>Processes the events of the viewport, <c>QueueEventProcessing()</c>.</summary>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void QueueEventProcessing() => throw new NotImplementedException("GameArea::QueueEventProcessing lands with G3.");

    /// <summary>Renders the area around an ultimate artifact, <c>GenerateUltimateArtifactAreaSurface()</c>.</summary>
    /// <param name="index">Tile index of the artifact.</param>
    /// <param name="offset">Offset of the area to render.</param>
    /// <returns>The rendered map fragment.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public static PixelBuffer GenerateUltimateArtifactAreaSurface(int index, Point offset)
        => throw new NotImplementedException("GameArea::GenerateUltimateArtifactAreaSurface lands with G3.");

    /// <summary>Tile under a point of the frame, <c>GetValidTileIdFromPoint()</c>.</summary>
    /// <param name="point">Position in the frame.</param>
    /// <returns>The tile id, or <c>-1</c> outside the map.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public int GetValidTileIdFromPoint(Point point) => throw new NotImplementedException("GameArea::GetValidTileIdFromPoint lands with G3.");

    /// <summary>Position of a tile in the frame, <c>GetRelativeTilePosition()</c>.</summary>
    /// <param name="tileId">Tile id as a point.</param>
    /// <returns>Top-left corner of the tile relative to the frame.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public Point GetRelativeTilePosition(Point tileId) => throw new NotImplementedException("GameArea::GetRelativeTilePosition lands with G3.");

    /// <summary>Opacity an object is currently drawn with, <c>getObjectAlphaValue()</c>.</summary>
    /// <param name="tileId">Tile the object stands on.</param>
    /// <param name="type">Kind of the object.</param>
    /// <returns>Opacity, <c>255</c> when the object is not animating.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public byte GetObjectAlphaValue(int tileId, Mp2ObjectType type)
        => throw new NotImplementedException("GameArea::getObjectAlphaValue lands with G3.");

    /// <summary>Opacity an object is currently drawn with, <c>getObjectAlphaValue()</c> by uid.</summary>
    /// <param name="uid">Unique id of the map object.</param>
    /// <returns>Opacity, <c>255</c> when the object is not animating.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public byte GetObjectAlphaValue(uint uid) => throw new NotImplementedException("GameArea::getObjectAlphaValue lands with G3.");

    /// <summary>Runs one animation to its end, <c>runSingleObjectAnimation()</c>.</summary>
    /// <param name="info">Animation to run.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void RunSingleObjectAnimation(BaseObjectAnimationInfo info)
        => throw new NotImplementedException("GameArea::runSingleObjectAnimation lands with G3.");

    /// <summary>Advances the inertia scroll or drag scroll by one frame, <c>updateInertia()</c>.</summary>
    /// <returns><see langword="true"/> when the frame has to be repainted.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3, needs <c>UIScrollInertia</c>).</exception>
    public bool UpdateInertia() => throw new NotImplementedException("GameArea::updateInertia lands with G3, together with UIScrollInertia.");

    /// <summary>Turns the fast scroll mode on or off, <c>setFastScrollStatus()</c>.</summary>
    /// <param name="enable">Whether the cursor is far enough out to scroll fast.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public void SetFastScrollStatus(bool enable) => throw new NotImplementedException("GameArea::setFastScrollStatus lands with G3.");

    /// <summary>Whether the cursor asks for a fast scroll, <c>mouseIndicatesFastScroll()</c>.</summary>
    /// <param name="mousePosition">Cursor position in the frame.</param>
    /// <returns><see langword="true"/> when the cursor is in the fast scroll margin.</returns>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    public bool MouseIndicatesFastScroll(Point mousePosition)
        => throw new NotImplementedException("GameArea::mouseIndicatesFastScroll lands with G3.");

    /// <summary>Centers the viewport on the middle of a tile, <c>_setCenterToTile()</c>.</summary>
    /// <param name="tile">Tile id as a point of tile coordinates.</param>
    /// <exception cref="NotImplementedException">Not ported yet (G3).</exception>
    private void SetCenterToTile(Point tile) => throw new NotImplementedException("GameArea::_setCenterToTile lands with G3.");
}
