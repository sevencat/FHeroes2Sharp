using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The map editor, first milestone: it opens an MP2 map, draws it exactly like the adventure map and
/// overlays what makes an editor recognisable — the tile grid, the highlighted tile under the pointer
/// and a status bar that names the ground and the object there.
///
/// <para>
/// The reference has an editor as well (<c>src/fheroes2/editor/</c>, opened by the EDITOR button of
/// <c>Game::MainMenu()</c>) and it draws the map through the same <c>GameArea</c> as the adventure
/// map: <c>Editor::Interface</c> only repositions that area. This scene follows the same split - the
/// rendering is <see cref="AdventureMapView"/>, so whatever the adventure map learns about objects,
/// heroes and layers shows up here for free, while the frame, the grid, the highlighted tile and the
/// panel are the editor's own.
/// </para>
/// <para>
/// The layout is the one of <c>Interface::EditorInterface::reset()</c>: the map sits in a viewport
/// between a 16 pixel border and the radar column, the radar is in the top right corner, and the panel
/// fills the column below it. The frame is <c>ADVBORD.ICN</c>, the same image the reference cuts its
/// border patterns from, blitted whole because the logical frame is exactly 640x480.
/// </para>
/// <para>
/// The instruments are keys for now: <c>1</c> to <c>9</c> pick a ground and the terrain tool, <c>O</c>
/// cycles the objects of <see cref="Mp2ObjectPresets"/> and selects the object tool, <c>X</c> the eraser
/// and <c>T</c> the terrain tool again. <c>I</c> opens the information box of the tile under the
/// pointer, the popup the reference shows on a right click: what the object is, the parts it is drawn
/// from, its data word and what that word means for this object, and its coordinates, with <c>[</c>
/// and <c>]</c> stepping the data word.
/// <see cref="MapEditSession"/> owns what the instruments do; this scene only forwards input and
/// paints the panel.
/// </para>
///
/// <para>
/// The eraser deletes eleven kinds of object, the way the erase page of the reference's panel does: the
/// page lists them as check marks and a click on one turns that kind on or off, so an author can clear
/// the roads and keep the trees. Every kind starts checked, and a tile whose part belongs to no
/// catalogue group is left alone, which is what the reference does with objects it cannot name. See
/// <see cref="EditorEraseType"/> for the bits and <see cref="MapEditSession.EraseTypes"/> for the state.
/// </para>
///
/// <para>
/// The minimap in the top right corner is the radar of the reference and takes the pointer the same
/// way: a press on it centres the map on the tile under the pointer and keeps centring while the
/// button is held (one branch for the click and for the drag, as in
/// <c>Radar::QueueEventProcessing</c>), and it opens no stroke, so a click there cannot end up in the
/// undo history.
/// </para>
/// <para>
/// Not ported yet: the system buttons of <c>EditorPanel</c> (undo, redo, grid, view, menu) — this
/// release of the game ships thirty-five frames of <c>EDITBTNS.ICN</c> and those buttons need more —
/// and the property dialogs of the objects, which the information box only reports.
/// </para>
/// </summary>
public sealed class EditorScene : IScene
{
    /// <summary>Width of the interface border on every side, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int BorderWidth = MapFrame.BorderWidth;

    /// <summary>Distance between the minimap and the top right corner, the border of the frame.</summary>
    public const int RadarMargin = BorderWidth;

    /// <summary>File of the interface frame, the <c>ADVBORD</c> of the reference.</summary>
    public const string FrameFileName = MapFrame.AssetName;

    /// <summary>
    /// Distance in pixels a press must travel before it drags the map instead of clicking it,
    /// <c>minimalRequiredDraggingMovement</c> of <c>interface_gamearea.cpp:68</c>.
    /// </summary>
    /// <remarks>
    /// 参考的比较是严格的（<c>&gt; minimalRequiredDraggingMovement</c>），正好十个像素还是点击。参考把这个数放在
    /// <c>interface_gamearea.cpp</c> 的匿名命名空间里，全文件只此一份；端口只有编辑器拖地图，所以它也是本类的一份常量。
    /// </remarks>
    public const int DragThreshold = 10;

    /// <summary>
    /// Seconds between two edge scroll steps, <see cref="GameDelays.ScrollDelayMilliseconds"/>.
    /// </summary>
    /// <remarks>
    /// 参考每帧问 <c>Game::validateAnimationDelay( DelayType::SCROLL_DELAY )</c>，那个格子由
    /// <see cref="GameDelays.InitAnimateDelays"/> 装成二十毫秒。编辑器这一层没有阻塞循环，一帧一步，所以拿同一个数字自己
    /// 累计 <c>dt</c>：既避开全局节拍表的静态状态，也不会让 20 这个数在 App 层再出现一次。
    /// </remarks>
    public const double EdgeScrollInterval = GameDelays.ScrollDelayMilliseconds / 1000.0;

    /// <summary>Pixels an edge scroll step moves, <c>2 &lt;&lt; scrollSpeed</c> at the <c>NORMAL</c> speed.</summary>
    /// <remarks>
    /// 参考从 <c>Settings</c> 读速度档（<c>scrollSpeed</c>），端口的设置还没有这一项，所以照 <c>NORMAL</c> 档写死：
    /// 一步八个像素，与移植前的 <c>MapScrollController.EdgeScrollStep</c> 相同。
    /// </remarks>
    public const int EdgeScrollStep = 2 << 2;

    private const int Padding = 6;

    /// <summary>
    /// Most edge scroll steps applied in one frame. A stalled main loop can hand in a large
    /// <c>elapsedSeconds</c>; without a cap the view would jump across the whole map at once.
    /// </summary>
    private const int MaxEdgeStepsPerUpdate = 4;

    private const int ScrollLeft = 1;
    private const int ScrollRight = 2;
    private const int ScrollTop = 4;
    private const int ScrollBottom = 8;

    /// <summary>Height of the decorated header of the panel bay in <c>ADVBORD.ICN</c>.</summary>
    private const int PanelHeaderHeight = 16;

    /// <summary>How long the panel shows "saved" after a successful save, in seconds.</summary>
    private const double SavedFlashSeconds = 2.0;

    /// <summary>
    /// How long the object popup of the right button stays at most, in seconds.
    /// </summary>
    /// <remarks>
    /// 参考的弹窗是阻塞循环，按住右键就一直画，松手才收（<c>ui_map_interface.cpp:75-77</c>）。端口一帧一步，
    /// 没有「按住期间」这个状态可等：右键在本场景里还兼着平移与吸地形，弹窗是按下的那一帧造出来的，
    /// 所以给它一个上限——按住不放也不会在屏幕上留一辈子，丢了松手事件也不会。
    /// </remarks>
    private const double ObjectPopupSeconds = 2.0;

    private readonly ScreenBuffer _frame;
    private readonly GameFonts? _fonts;
    private readonly MapEditorView _editor;
    private readonly MapEditSession _edits;
    private readonly MapFrame _mapFrame;
    private readonly Rect _viewport;
    private readonly PixelBuffer _viewportPixels;
    private readonly Rect _radar;
    private readonly Rect _panel;
    private readonly EditorPanel? _panelView;

    /// <summary>Whether the archive can provide the message frame of the information box.</summary>
    private readonly bool _hasInfoFrame;

    /// <summary>The <c>QWIKINFO.ICN</c> frame of the right button popup, <see langword="null"/> without the archives.</summary>
    private readonly Sprite? _popupFrame;

    /// <summary>
    /// What the popup needs besides the tile: the per-object tables of the map.
    /// </summary>
    /// <remarks>
    /// MP2 的物件记录里没有参考那九本表（数量、归属、名字），这里是一份空表：<see cref="EditorObjectPopup"/>
    /// 缺表时只报名字，与参考在表里查不到时退回类型名的做法一致。
    /// </remarks>
    private readonly MapObjectMetadataStore _metadata = new();

    private readonly byte _dark;
    private readonly byte _bright;

    /// <summary>Baked colours of the map; rebuilt after a stroke because it does not read the tiles.</summary>
    private EditorRadar _minimap;

    /// <summary>The information box the <c>I</c> key opens, kept between frames and rebuilt when stale.</summary>
    private MessageBox? _info;

    /// <summary>Tile and data word the box was built for, i.e. what it shows.</summary>
    private (Point Tile, int Data)? _infoKey;

    /// <summary>Whether the paint button is held, i.e. the pointer keeps painting as it moves.</summary>
    private bool _painting;

    /// <summary>Whether the left button is held anywhere in the window.</summary>
    private bool _leftHeld;

    /// <summary>
    /// Whether the right button is held, kept so that <see cref="ProcessEvents"/> can tell the edge of a press
    /// from a button that is simply still down.
    /// </summary>
    private bool _rightHeld;

    /// <summary>Whether the press started on the minimap, which owns the pointer until it is released.</summary>
    private bool _radarGrabbed;

    /// <summary>The pointer in logical pixels, which the radar and the frame are laid out in.</summary>
    private Point? _screenCursor;

    /// <summary>The pointer in viewport pixels, the coordinates the drag and the edge scroll work in.</summary>
    private Point? _panCursor;

    /// <summary>Where the pan button went down, <see langword="null"/> while it is not held.</summary>
    private Point? _panOrigin;

    /// <summary>Pointer position of the previous drag step, the anchor of the next shift.</summary>
    private Point _panAnchor;

    /// <summary>Whether the pan button is held anywhere in the window.</summary>
    private bool _panHeld;

    /// <summary>Whether the press already travelled <see cref="DragThreshold"/> pixels, i.e. it pans.</summary>
    private bool _panning;

    /// <summary>Seconds accumulated towards the next edge scroll step.</summary>
    private double _edgeElapsed;

    /// <summary>Seconds left of the "saved" note in the status bar.</summary>
    private double _savedFlash;

    /// <summary>The tile the information box describes, <see langword="null"/> when it is closed.</summary>
    private Point? _infoTile;

    /// <summary>The popup the right button shows, <see langword="null"/> when it is closed.</summary>
    private StandardPopupWindow? _objectPopup;

    /// <summary>Seconds left of the object popup, i.e. what of <see cref="ObjectPopupSeconds"/> is unused.</summary>
    private double _objectPopupLeft;

    /// <summary>The tile the object popup was built for, the log names it.</summary>
    private Point? _objectPopupTile;

    private EditorScene(
        ScreenBuffer frame,
        GameFonts? fonts,
        MapEditorView editor,
        MapEditSession edits,
        EditorRadar minimap,
        MapFrame mapFrame,
        EditorPanel? panelView,
        bool hasInfoFrame,
        Sprite? popupFrame)
    {
        _frame = frame;
        _fonts = fonts;
        _editor = editor;
        _edits = edits;
        _minimap = minimap;
        _mapFrame = mapFrame;
        _panelView = panelView;
        _hasInfoFrame = hasInfoFrame;
        _popupFrame = popupFrame;

        // The layout of Interface::EditorInterface::reset(): the map gets the area between the border
        // and the radar column, the radar sits in the top right corner, and the editor panel fills the
        // column below it, down to the bottom of the frame.
        _viewport = mapFrame.Viewport;
        _viewportPixels = new PixelBuffer(_viewport.Width, _viewport.Height);

        _radar = mapFrame.Radar;
        _panel = mapFrame.Panel;

        _dark = frame.Palette.FindDarkestIndex();
        _bright = frame.Palette.FindBrightestIndex();
    }

    /// <summary>The map being edited.</summary>
    public Mp2Map Map => _editor.Map.Map;

    /// <summary>The hover and grid state of the editor.</summary>
    public MapEditorView Editor => _editor;

    /// <summary>The brush, the stroke in progress and the undo history.</summary>
    public MapEditSession Edits => _edits;

    /// <summary>The scrolling view shared with the adventure map.</summary>
    public AdventureMapView View => _editor.Map;

    /// <summary>Name of the map, as stored in its header.</summary>
    public string MapName => Map.Info.Name;

    /// <summary>The rectangle the map is drawn into, between the border and the radar column.</summary>
    public Rect ViewportArea => _viewport;

    /// <summary>The instrument panel below the radar, the <c>EditorPanel</c> of the reference.</summary>
    public Rect PanelArea => _panel;

    /// <summary>Size of the logical frame the editor draws into.</summary>
    public Size FrameSize => _frame.Size;

    /// <summary>The tool in use, as the panel names it.</summary>
    public string Tool => _edits.Tool.ToString();

    /// <summary>The ground of the terrain tool, as the panel names it.</summary>
    public string Brush => _edits.Brush.ToString();

    /// <summary>The preset of the object tool, or <c>none</c> when no object is selected.</summary>
    public string ObjectName => _edits.ObjectBrush?.Name ?? "none";

    /// <summary>The size of the ground and eraser brush, as the panel names it.</summary>
    public string BrushSizeName => _edits.BrushSize.ToString();

    /// <summary>Whether the information box of the object under the pointer is open.</summary>
    public bool IsObjectInfoOpen => _infoTile is not null;

    /// <summary>The tile the information box describes, <see langword="null"/> when it is closed.</summary>
    public Point? ObjectInfoTile => _infoTile;

    /// <summary>
    /// One line about the tile the information box describes, for the log of a key press: the name of
    /// what is on it, its coordinates and its data word.
    /// </summary>
    public string ObjectInfoSummary
    {
        get
        {
            if (_infoTile is not { } tile)
            {
                return "closed";
            }

            Mp2TileInfo info = Map.Tile(tile.X, tile.Y);

            return FormattableString.Invariant(
                $"{DescribeTile(info)} at ({tile.X}; {tile.Y}), data {_edits.ObjectData(tile)}");
        }
    }

    /// <summary>Opens or closes the information box of the hovered tile, the <c>I</c> key.</summary>
    /// <returns>Whether the box is open after the call.</returns>
    public bool ToggleObjectInfo()
    {
        if (_infoTile is not null)
        {
            HideObjectInfo();
            return false;
        }

        return _editor.HoverTile is { } tile && ShowObjectInfo(tile);
    }

    /// <summary>
    /// Opens the information box on one tile, the scripted entry point behind
    /// <c>--editor-info</c>.
    /// </summary>
    /// <param name="tile">Tile to describe, ignored when it is outside the map.</param>
    /// <returns>Whether the box opened.</returns>
    public bool ShowObjectInfo(Point tile)
    {
        if (tile.X < 0 || tile.X >= Map.Width || tile.Y < 0 || tile.Y >= Map.Height)
        {
            return false;
        }

        _infoTile = tile;
        _info = null;
        return true;
    }

    /// <summary>Closes the information box, a click on the map or the <c>I</c> key.</summary>
    public void HideObjectInfo()
    {
        _infoTile = null;
        _info = null;
    }

    /// <summary>Whether the popup of the right button is on screen.</summary>
    public bool IsObjectPopupOpen => _objectPopup is not null;

    /// <summary>Seconds left before the popup of the right button takes itself down.</summary>
    public double ObjectPopupRemaining => _objectPopupLeft;

    /// <summary>The tile the popup of the right button describes, <see langword="null"/> when it is closed.</summary>
    public Point? ObjectPopupTile => _objectPopupTile;

    /// <summary>
    /// Shows the popup of the right button on one tile, <c>EditorInterface::mouseCursorAreaPressRight()</c>
    /// (<c>src/fheroes2/editor/editor_interface.cpp:3080-3083</c>).
    /// </summary>
    /// <param name="tile">Tile to describe, in map tiles.</param>
    /// <returns>
    /// Whether the popup opened; <see langword="false"/> without the <c>QWIKINFO.ICN</c> frame, without the
    /// fonts, without a pointer position or with a tile outside the map.
    /// </returns>
    public bool ShowObjectPopup(Point tile)
    {
        HideObjectPopup();

        if (_popupFrame is null || _fonts is null || _screenCursor is not { } cursor)
        {
            return false;
        }

        // 参考用的是鼠标的绝对坐标与地图区（含边框）的宽高，端口照它传给 StandardPopupWindow。
        _objectPopup = EditorObjectPopupView.TryCreate(
            Map,
            tile,
            _metadata,
            _popupFrame,
            _fonts.SmallWhite,
            cursor,
            EditorObjectPopupView.InterfaceAreaOf(_viewport));

        if (_objectPopup is null)
        {
            return false;
        }

        _objectPopupTile = tile;
        _objectPopupLeft = ObjectPopupSeconds;
        return true;
    }

    /// <summary>Closes the popup of the right button, a release of the button or a drag across the map.</summary>
    public void HideObjectPopup()
    {
        _objectPopup = null;
        _objectPopupTile = null;
        _objectPopupLeft = 0.0;
    }

    /// <summary>
    /// Steps the data word of the tile the box describes, the <c>[</c> and <c>]</c> keys.
    /// </summary>
    /// <param name="delta">Amount to add, clamped to the range the format has.</param>
    /// <returns>Whether the tile changed.</returns>
    public bool AdjustObjectData(int delta)
    {
        if (_infoTile is not { } tile)
        {
            return false;
        }

        int current = _edits.ObjectData(tile);
        int next = Math.Clamp(current + delta, 0, MapEditSession.MaxObjectData);

        return next != current && _edits.SetObjectData(tile, next);
    }

    /// <summary>Writes the data word of a tile, the scripted counterpart of the <c>[</c>/<c>]</c> keys.</summary>
    /// <param name="tile">Tile to write, ignored when it holds no object or is outside the map.</param>
    /// <param name="data">Value to write, 0 to <see cref="MapEditSession.MaxObjectData"/>.</param>
    /// <returns>Whether the tile changed.</returns>
    public bool SetObjectData(Point tile, int data) => _edits.SetObjectData(tile, data);

    /// <summary>
    /// The bay below the radar for a frame of <paramref name="size"/>, the rectangle
    /// <see cref="PanelArea"/> reports once the editor exists. The geometry is the one of
    /// <see cref="MapFrame"/>, so the host can hit test the panel before the editor is built.
    /// </summary>
    /// <param name="size">Size of the logical frame.</param>
    /// <returns>The rectangle of the panel bay.</returns>
    public static Rect PanelBayOf(Size size) => new MapFrame(size, null).Panel;

    /// <summary>
    /// Opens the editor on <paramref name="mapInfo"/>; <see langword="null"/> with an
    /// <paramref name="error"/> when the map or the terrain tiles cannot be read.
    /// </summary>
    /// <remarks>
    /// The assembly mirrors <see cref="AdventureMapScene.TryCreate"/>; it differs in the initial
    /// position, which is the middle of the map instead of the castle a game starts at: an editor
    /// has no player to centre on.
    /// </remarks>
    public static EditorScene? TryCreate(
        ScreenBuffer frame,
        AssetBundle? assets,
        GameFonts? fonts,
        Mp2FileInfo mapInfo,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(mapInfo);

        error = null;

        TerrainTileSet? tiles = TerrainTileSet.TryLoad(assets);
        if (tiles is null)
        {
            error = $"'{TerrainTileSet.GroundFileName}' is not available, so the map cannot be drawn.";
            return null;
        }

        try
        {
            Mp2Map map = Mp2Map.Read(mapInfo.FilePath);
            EditorScene scene = Assemble(frame, fonts, map, tiles, assets);

            GameLog.Info(
                $"Editor opened: '{map.Info.Name}' ({map.Width}x{map.Height}, " +
                $"{map.Castles.Count} castles, {map.Addons.Count} add-ons).");

            return scene;
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            GameLog.Error($"The map '{mapInfo.FilePath}' could not be opened", exception);
            return null;
        }
    }

    /// <summary>
    /// Opens the editor on a map that was built in memory instead of read from a file, the random map generator's
    /// output; <see langword="null"/> with an <paramref name="error"/> when the terrain tiles cannot be read.
    /// </summary>
    /// <remarks>
    /// 随机地图生成器产出的是 <see cref="Mp2Map"/>（`RandomMapBuilder`），它还没有文件；编辑器照常在里面涂改，
    /// 第一次保存时才需要给个文件名。
    /// </remarks>
    public static EditorScene? CreateFromMap(
        ScreenBuffer frame,
        AssetBundle? assets,
        GameFonts? fonts,
        Mp2Map map,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(map);

        error = null;

        TerrainTileSet? tiles = TerrainTileSet.TryLoad(assets);
        if (tiles is null)
        {
            error = $"'{TerrainTileSet.GroundFileName}' is not available, so the map cannot be drawn.";
            return null;
        }

        EditorScene scene = Assemble(frame, fonts, map, tiles, assets);

        GameLog.Info($"Editor opened on a map built in memory ({map.Width}x{map.Height}, no file behind it).");

        return scene;
    }

    /// <summary>
    /// Opens the editor on an empty map of <paramref name="size"/> tiles instead of a file;
    /// <see langword="null"/> with an <paramref name="error"/> when the size is not a map size or the
    /// terrain tiles cannot be read.
    /// </summary>
    /// <remarks>
    /// The map is grass with no object on it and has no file behind it, so the first save has to name
    /// one (<see cref="MapEditorView"/> has no "save as" dialog yet, the command line does).
    /// </remarks>
    public static EditorScene? CreateNew(
        ScreenBuffer frame,
        AssetBundle? assets,
        GameFonts? fonts,
        int size,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(frame);

        error = null;

        if (!Mp2Map.IsSupportedSize(size, size))
        {
            error = $"A map is 36, 72, 108 or 144 tiles wide and high, not {size}.";
            return null;
        }

        TerrainTileSet? tiles = TerrainTileSet.TryLoad(assets);
        if (tiles is null)
        {
            error = $"'{TerrainTileSet.GroundFileName}' is not available, so the map cannot be drawn.";
            return null;
        }

        // The plain images of grass, spread over the tiles by position: a map that started on the
        // first image of the range would be a transition to another ground on every tile.
        MapGround ground = MapGround.Grass;
        Mp2Map map = Mp2Map.CreateBlank(
            size,
            (x, y) => (ushort)(
                MapGrounds.StartImageIndex(ground)
                + MapGrounds.PlainImageOffset(ground)
                + MapGrounds.VariantOffset(x, y, 8)));

        EditorScene scene = Assemble(frame, fonts, map, tiles, assets);

        GameLog.Info($"Editor created a new {map.Width}x{map.Height} map; it has no file until it is saved.");

        return scene;
    }

    /// <summary>Builds the scene around an already loaded map, the part both entry points share.</summary>
    private static EditorScene Assemble(
        ScreenBuffer frame, GameFonts? fonts, Mp2Map map, TerrainTileSet tiles, AssetBundle? assets)
    {
        // The map only gets the area between the border and the radar column, as in the reference; the
        // scene blits it into the frame at ViewportArea.
        MapFrame mapFrame = new(frame.Size, MapFrame.Load(assets));

        AdventureMapView view = new(
            map,
            tiles,
            mapFrame.Viewport.Size,
            ObjectSpriteSet.TryCreate(assets),
            HeroSpriteSet.TryCreate(assets));

        view.CenterOn(map.Width / 2, map.Height / 2);

        MapEditorView editor = new(view);
        MapEditSession edits = new(editor);

        return new EditorScene(
            frame,
            fonts,
            editor,
            edits,
            EditorRadar.Build(map),
            mapFrame,
            EditorPanel.TryCreate(mapFrame.Panel, assets),
            HasInfoFrame(assets),
            QuickInfoArtwork.TryCreate(assets));
    }

    /// <summary>
    /// Whether the frame of the information box can be composed; the dialogs of the reference use
    /// <c>BUYBUILD.ICN</c>, which <see cref="FrameBox"/> reads itself.
    /// </summary>
    /// <param name="assets">The archive to read from, may be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the frame can be drawn.</returns>
    private static bool HasInfoFrame(AssetBundle? assets)
    {
        bool available = assets?.TryReadIcn(FrameBox.ArtworkFileName) is { } file &&
            file.Count >= FrameBox.RequiredSpriteCount;

        if (!available)
        {
            GameLog.Warn($"'{FrameBox.ArtworkFileName}' is not available, the editor draws no information box.");
        }

        return available;
    }

    /// <summary>Advances the edge scroll of the mouse; the terrain itself does not animate yet.</summary>
    /// <remarks>
    /// The object popup of the right button counts down here: the reference waits for the button to be
    /// released inside <c>displayStandardPopupWindow()</c>, and the port cannot wait, so the popup has
    /// <see cref="ObjectPopupSeconds"/> at most.
    /// </remarks>
    public void Update(double elapsedSeconds)
    {
        ProcessEvents(LocalEvent.Current);

        UpdateEdgeScroll(elapsedSeconds);
        _savedFlash = Math.Max(0.0, _savedFlash - elapsedSeconds);

        if (_objectPopup is not null)
        {
            _objectPopupLeft -= elapsedSeconds;

            if (_objectPopupLeft <= 0.0)
            {
                HideObjectPopup();
            }
        }
    }

    /// <summary>
    /// One frame of input, read out of the frame's <see cref="LocalEvent"/> the way the reference reads it.
    /// </summary>
    /// <param name="events">The events the host pumped during this frame.</param>
    /// <returns>Whether anything the run log reports happened: a stroke was closed or a brush was taken.</returns>
    /// <remarks>
    /// <para>
    /// 参考的编辑界面（<c>editor_interface.cpp</c>）没有「按下/松开」回调，它每帧在 <c>processEvents()</c> 里问
    /// <c>LocalEvent</c>：指针在哪、左键按着没有、滚轮滚了多远。端口原来由宿主把 SDL 事件翻成
    /// <see cref="SetCursor"/> / <see cref="SetLeftButton"/> / <see cref="SetRightButton"/> / <see cref="ScrollByWheel"/>
    /// 逐个喂进来，一个动作要在宿主里拆成两处代码，加一个手势就要改两个文件。这里照冒险地图
    /// （<c>AdventureMapScene.cs:570</c>）的写法，把整帧事件交给场景自己分。
    /// </para>
    /// <para>
    /// 那四个方法留着，是因为 <c>--editor-paint</c> 之类的脚本动作和 <c>--cursor</c> 合成的指针都要走它们；
    /// 真实的鼠标不再经过宿主，一个动作只剩一条路径。
    /// </para>
    /// <para>
    /// 键盘仍在宿主（<c>GameHost.HandleEditorKey</c>）：Esc 要开编辑器菜单、Ctrl+S 要存盘，它们和菜单、模态窗
    /// 的优先级绑在一起，搬过来要把优先级一起搬；键位重复（<c>key.Repeat</c>）与修饰键在 <see cref="LocalEvent"/>
    /// 里也还只能拿到「这一帧按下过」，撑不住按住连发。另见 <c>a7l.6.1</c> 的说明。
    /// </para>
    /// </remarks>
    public bool ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        bool leftHeld = events.IsMouseLeftButtonPressed;
        bool rightHeld = events.IsMouseRightButtonPressed;

        // 点击状态会边读边被消耗，所以先读出来再用。
        bool leftClicked = events.MouseClickLeft();
        bool rightClicked = events.MouseClickRight();

        // 指针：LocalEvent 一直记着最后一次位置，但这里只有真的动了、或者这一帧有按钮动作时才同步给
        // SetCursor。无条件每帧取一次有两个后果——指针离开窗口后宿主置的 null 会被又填回旧位置，边缘滚动
        // 会在鼠标没动过的时候一直生效（滚到地图角上停不下来）。
        if (events.HasMouseMoved || leftHeld != _leftHeld || rightHeld != _rightHeld || leftClicked ||
            rightClicked)
        {
            SetCursor(events.CursorPosition);
        }

        bool changed = false;

        // 左键：跨帧按住时按前后状态比出按下与松开这两根边；按下与松开落在同一帧（宿主把一对鼠标事件合进
        // 一个首尾相接的 LocalEvent）时状态没变化，只能靠 MouseClickLeft 补一次按下+松开——参考的
        // processEvents 每帧看的就是 MouseClickLeft，一笔快击正好画一格。
        if (leftHeld != _leftHeld)
        {
            changed |= SetLeftButton(leftHeld);
        }
        else if (leftClicked && !_leftHeld)
        {
            SetLeftButton(true);
            changed |= SetLeftButton(false);
        }

        // 右键同理：按下弹物件框、松开取笔刷，快击也要走完这两步。
        if (rightHeld != _rightHeld)
        {
            changed |= SetRightButton(rightHeld);
        }
        else if (rightClicked && !_rightHeld)
        {
            SetRightButton(true);
            changed |= SetRightButton(false);
        }

        // 滚轮：宿主原来把 SDL 的整格数取负交给 ScrollByWheel，这里原样搬过来，高分辨率滚轮的半格仍会被
        // 折成 0（ScrollByWheel 只认整格）。
        int rows = -events.MouseWheelMovement.Y;

        if (rows != 0)
        {
            ScrollByWheel(0, rows);
        }

        return changed;
    }

    /// <summary>Paints the terrain, the grid, the highlighted tile, the minimap and both bars.</summary>
    /// <remarks>
    /// Every frame is a full repaint, so the whole buffer is marked dirty: the terrain is written
    /// straight into <see cref="ScreenBuffer.Pixels"/>, which does not report changes, and a frame
    /// that is never marked dirty is never uploaded - the window would keep showing the screen of
    /// the previous scene.
    /// </remarks>
    public void DrawFrame(in DiagnosticsState state)
    {
        _frame.InvalidateAll();
        _frame.Pixels.Clear();
        DrawFrameChrome();

        _editor.Map.Draw(_viewportPixels);
        ImageOps.Blit(_frame.Pixels, _viewport.Position, _viewportPixels, _viewportPixels.Bounds);

        DrawGrid();
        DrawHover();
        DrawMinimap();
        DrawPanel();
        DrawObjectInfo();
        DrawObjectPopup();
    }

    /// <summary>
    /// Draws the popup of the right button last, the way the reference draws it over everything else
    /// (<c>displayStandardPopupWindow()</c> blits it straight onto the display).
    /// </summary>
    private void DrawObjectPopup() => _objectPopup?.Draw(_frame.Pixels);

    /// <summary>
    /// Draws the information box of the tile the <c>I</c> key named, over the panel like the reference
    /// draws its popup over everything else. The box is only rebuilt when the tile or its data word
    /// moved, so holding a key does not relayout the text on every frame.
    /// </summary>
    private void DrawObjectInfo()
    {
        if (_infoTile is not { } tile || _fonts is null || !_hasInfoFrame)
        {
            return;
        }

        int data = _edits.ObjectData(tile);

        if (_info is null || _infoKey != (tile, data))
        {
            _info = ComposeObjectInfo(tile, data, _fonts.Small);
            _infoKey = (tile, data);
        }

        _info.Draw(_frame.Pixels);
    }

    /// <summary>
    /// Builds the text of the information box: what the object is, the parts it is drawn from, its
    /// data word with the sentence <see cref="Mp2ObjectData.Describe"/> gives it, and the coordinates
    /// of its tile, which is the content of <c>Editor::getObjectInfoText</c> in the reference.
    /// </summary>
    /// <param name="tile">Tile to describe, must be inside the map.</param>
    /// <param name="data">Data word of the tile, read again by the caller to keep the box current.</param>
    /// <param name="font">Font of the text.</param>
    /// <returns>The box, centred on the frame like every dialog of the reference.</returns>
    private MessageBox ComposeObjectInfo(Point tile, int data, IGlyphSource font)
    {
        Mp2TileInfo info = Map.Tile(tile.X, tile.Y);
        List<string> lines = [];

        if (info.HasGroundObject)
        {
            lines.Add(FormattableString.Invariant(
                $"ground {info.GroundIcnType}:{info.BottomIcnImageIndex} layer {info.GroundLayer}"));
        }

        if (info.HasTopObject)
        {
            lines.Add(FormattableString.Invariant($"top {info.TopIcnType}:{info.TopIcnImageIndex}"));
        }

        if (info.HasGroundObject || info.HasTopObject)
        {
            lines.Add(FormattableString.Invariant($"data {data}"));
            lines.Add(Mp2ObjectData.Describe(info));
            lines.Add("[ ] change the data");
        }
        else
        {
            lines.Add("no object");
        }

        lines.Add(FormattableString.Invariant($"[{tile.X}; {tile.Y}]"));

        return MessageBox.Create(DescribeTile(info), string.Join('\n', lines), font, font);
    }

    /// <summary>
    /// Names what a tile holds: the preset of the editor when the tile shows one of its objects, the
    /// name <see cref="Mp2ObjectName.Of"/> gives the object type when it does not, and the ground when
    /// there is no object at all.
    /// </summary>
    /// <param name="info">Tile to name.</param>
    /// <returns>The name shown as the header of the information box.</returns>
    private static string DescribeTile(Mp2TileInfo info)
    {
        if (Mp2ObjectPresets.Match(info) is { } preset)
        {
            return preset.Name;
        }

        return info.ObjectType != Mp2ObjectType.None
            ? Mp2ObjectName.Of(info.ObjectType)
            : MapGrounds.FromTerrainImageIndex(info.TerrainImageIndex).ToString();
    }

    /// <summary>The editor draws all of its text with the game fonts.</summary>
    public void DrawOverlay(in DiagnosticsState state)
    {
    }

    /// <summary>Scrolls the view by map pixels, clamped to the map.</summary>
    public void ScrollBy(int dx, int dy) => _editor.Map.ScrollBy(dx, dy);

    /// <summary>Scrolls the view by whole tiles, the arrow keys.</summary>
    public void ScrollByTiles(int columns, int rows) =>
        _editor.Map.ScrollBy(columns * _editor.Map.Tiles.TileWidth, rows * _editor.Map.Tiles.TileHeight);

    /// <summary>Scrolls the view back to the top left corner of the map.</summary>
    public void ScrollToStart() => _editor.Map.ScrollTo(0, 0);

    /// <summary>Turns the tile grid on or off and returns the new state, the <c>G</c> key.</summary>
    public bool ToggleGrid() => _editor.ToggleGrid();

    /// <summary>
    /// Records the pointer: it moves the pan, the edge scroll and the highlighted tile, and paints the
    /// tile it enters while the left button is held.
    /// </summary>
    /// <remarks>
    /// A press that started on the minimap keeps centring the map while it is held, which is the
    /// <c>MouseClickLeft || isMouseLeftButtonPressedInArea</c> branch of
    /// <c>Radar::QueueEventProcessing</c>: one branch for the click and for the drag across the panel.
    /// </remarks>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor)
    {
        _screenCursor = cursor;

        // The map view and its scroll work in viewport pixels: a pointer over the border, the radar or
        // the panel is outside the map, which is what Interface::EditorInterface reports as no tile.
        _editor.SetCursor(cursor is { } screen ? ViewportCursor(screen) : null);

        // 拖动在指针定完格子之后才挪视图：参考的 GameArea 也是先认格子再拖（interface_gamearea.cpp:360 一带），
        // 先拖会让这一次的格子按新位置算，跟手差的是一整格。
        DragTo(cursor is { } inside ? ViewportCursor(inside) : null);

        // A pan that started takes the popup down: the reference pans with the left button and only ever
        // holds the popup still with the right one, while this scene drags the map with the right button,
        // so a popup left behind would name a tile the pointer has left.
        if (_panning)
        {
            HideObjectPopup();
        }

        if (_radarGrabbed && _leftHeld)
        {
            if (cursor is { } point)
            {
                CentreOnRadar(point);
            }

            return;
        }

        if (_painting)
        {
            Paint();
        }
    }

    /// <summary>
    /// Converts a pointer position to viewport pixels, the coordinates the map view and the editor
    /// session work in.
    /// </summary>
    /// <param name="screen">Pointer in logical pixels.</param>
    /// <returns>Point inside the viewport, or <see langword="null"/> when the pointer is not over it.</returns>
    private Point? ViewportCursor(Point screen) => _viewport.Contains(screen)
        ? new Point(screen.X - _viewport.X, screen.Y - _viewport.Y)
        : null;

    /// <summary>
    /// Records the left button, which paints in the editor: a press opens a stroke, a release closes
    /// it and rebuilds the minimap, because the minimap holds baked colours of the old tiles.
    /// </summary>
    /// <remarks>
    /// A press on the minimap is swallowed by it instead: it centres the map and opens no stroke, so
    /// the release cannot leave an empty step in the history. The reference does the same by letting
    /// the radar capture the mouse before the game area sees the event.
    /// </remarks>
    /// <param name="down">Whether the left button is now held.</param>
    /// <returns>Whether a stroke with at least one changed tile was closed, so a caller can log it.</returns>
    public bool SetLeftButton(bool down)
    {
        _leftHeld = down;

        // The information box is modal the way a message of the reference is: a click anywhere takes it
        // away and does nothing else, so a press meant to dismiss it cannot paint a tile as a side effect.
        if (down && _infoTile is not null)
        {
            HideObjectInfo();
            return false;
        }

        if (down && _screenCursor is { } cursor && _radar.Contains(cursor))
        {
            _radarGrabbed = true;
            CentreOnRadar(cursor);
            return false;
        }

        // The panel takes the press as well, and it is a click on a control, not a stroke: the bay is
        // not map, and an empty stroke behind it would land in the undo history.
        if (down && _screenCursor is { } panelPoint && _panel.Contains(panelPoint))
        {
            ClickPanel(panelPoint);
            return false;
        }

        if (!down && _radarGrabbed)
        {
            _radarGrabbed = false;
            return false;
        }

        // 细节仪器不走笔画：它的按下与松开要么开一个物件窗口，要么搬一件东西，中间不改编地图。
        if (_edits.Tool == EditorTool.Detail)
        {
            return PressDetail(down);
        }

        if (down)
        {
            _painting = true;
            _edits.BeginStroke();
            Paint();
            return false;
        }

        _painting = false;

        if (!_edits.EndStroke())
        {
            return false;
        }

        RebuildMinimap();

        // 日志在这一层打：真实鼠标（ProcessEvents）与脚本动作（--editor-paint）都从这个出口过，宿主再打一遍
        // 就会重复，漏一处又会少一行。
        GameLog.Info($"Painted with '{_edits.Brush}'; {_edits.UndoDepth} undoable strokes.");

        return true;
    }

    /// <summary>
    /// Runs the left button of the detail instrument, which neither paints a ground nor keeps a stroke
    /// open (<c>editor_interface.cpp:1675-1780</c> and <c>2906-2935</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 三个模式就是参考的三条路：编辑模式左键点在有物件的格子上打开窗；移动与复制都是一按一放，按下用
    /// <see cref="MapEditSession.GrabObject"/> 记下格子，松开用 <see cref="MapEditSession.ReleaseObject"/> 落子。
    /// 一格的地面层与顶层可以分属两个物件，参考也是按「这一格里有没有东西」判定，端口照此。
    /// </para>
    /// <para>
    /// 端口的物件属性窗还没移植（城堡、事件等窗口在后面的 issue 里），编辑模式打开的是物件信息框：入口与时机相同，
    /// 只是内容少一层，等属性窗落地后再接进来。
    /// </para>
    /// </remarks>
    /// <param name="down">Whether the left button is now held.</param>
    /// <returns>Whether the map changed.</returns>
    private bool PressDetail(bool down)
    {
        if (_edits.DetailMode == EditorDetailMode.Editing)
        {
            if (!down || _editor.HoverTile is not { } tile)
            {
                return false;
            }

            Mp2TileInfo info = Map.Tile(tile.X, tile.Y);

            return (info.HasGroundObject || info.HasTopObject) && ShowObjectInfo(tile);
        }

        if (down)
        {
            _edits.GrabObject(_editor.HoverTile);
            return false;
        }

        if (!_edits.ReleaseObject(_editor.HoverTile))
        {
            return false;
        }

        RebuildMinimap();

        // 细节仪器的移动与复制同样是一笔画，日志与颜料笔共用一行。
        GameLog.Info($"Painted with '{_edits.Brush}'; {_edits.UndoDepth} undoable strokes.");

        return true;
    }

    /// <summary>
    /// Records the right button: a press describes the tile under the pointer in the object popup,
    /// dragging it pans the map, and a click without a drag samples the ground under the pointer as
    /// the brush.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的右键只有一个用途：<c>GameArea::QueueEventProcessing()</c> 在右键按下时调
    /// <c>mouseCursorAreaPressRight()</c>，编辑器把它接到物件弹窗（<c>interface_gamearea.cpp:1145-1147</c>）；
    /// 平移在左键上。端口的左键要画画，平移只能让给右键，于是右键两头都要顾：按下这一帧就弹窗（与参考同刻），
    /// 指针拖过阈值后 <see cref="SetCursor"/> 把弹窗收掉、交给平移，松手时再按老规矩吸地形。
    /// </para>
    /// <para>
    /// The minimap takes none of them: the reference shows its "World Map" hint there, and an eyedropper or
    /// a pan that started on the panel would describe a tile the user did not point at.
    /// </para>
    /// </remarks>
    /// <param name="down">Whether the right button is now held.</param>
    /// <returns>Whether the release took the brush from the map.</returns>
    public bool SetRightButton(bool down)
    {
        _rightHeld = down;

        bool onRadar = _screenCursor is { } cursor && _radar.Contains(cursor);

        if (down)
        {
            if (_infoTile is not null)
            {
                HideObjectInfo();
                return false;
            }

            if (!onRadar)
            {
                if (_editor.HoverTile is { } tile)
                {
                    ShowObjectPopup(tile);
                }

                SetPanButton(true);
            }

            return false;
        }

        HideObjectPopup();

        // A release after a drag is the end of a pan, not an eyedropper click.
        if (_panning)
        {
            SetPanButton(false);
            return false;
        }

        SetPanButton(false);

        if (onRadar || !_edits.SampleBrush(_editor.HoverTile))
        {
            return false;
        }

        GameLog.Info($"Picked the '{_edits.Brush}' brush from the map.");

        return true;
    }

    /// <summary>
    /// Presses the minimap and releases it, the scripted version of a click that
    /// <c>--editor-radar-click</c> uses. With <paramref name="dragTo"/> the pointer also moves before
    /// the release, which is the drag branch of <see cref="SetCursor"/>: the reference keeps centring
    /// the map for as long as the button is held, not only on the press.
    /// </summary>
    /// <param name="panelPoint">Point inside the 144x144 panel, its top left corner at 0,0.</param>
    /// <param name="dragTo">Point to drag to before the release, <see langword="null"/> for a click.</param>
    /// <returns>Whether the view moved.</returns>
    public bool ClickRadar(Point panelPoint, Point? dragTo = null)
    {
        Point before = _editor.Map.Scroll;

        SetCursor(new Point(_radar.X + panelPoint.X, _radar.Y + panelPoint.Y));
        SetLeftButton(true);

        if (dragTo is { } end)
        {
            SetCursor(new Point(_radar.X + end.X, _radar.Y + end.Y));
        }

        SetLeftButton(false);

        return _editor.Map.Scroll != before;
    }

    /// <summary>
    /// Drags the map with the right button, the scripted version of a pan that a mouse would do across
    /// several frames: the press lands on <paramref name="from"/>, the pointer jumps to <paramref name="to"/>
    /// and the button is released.
    /// </summary>
    /// <param name="from">Where the press lands, in viewport pixels.</param>
    /// <param name="to">Where the pointer moves to while the button is held.</param>
    /// <returns>Whether the view moved.</returns>
    /// <remarks>
    /// 两个点都在视口坐标里，与 <see cref="AdventureMapView"/> 的滚动同一套。按下会先弹物件窗（右键的老规矩），
    /// 指针越过阈值后 <see cref="DragTo"/> 把它收掉，所以一次真正的拖动不会留下那个 2 秒窗。
    /// </remarks>
    public bool DragMap(Point from, Point to)
    {
        Point before = _editor.Map.Scroll;

        SetCursor(new Point(_viewport.X + from.X, _viewport.Y + from.Y));
        SetRightButton(true);
        SetCursor(new Point(_viewport.X + to.X, _viewport.Y + to.Y));
        SetRightButton(false);

        return _editor.Map.Scroll != before;
    }

    /// <summary>
    /// Clicks a control of the panel: an instrument button, a terrain button, an object preset or a
    /// brush size button. The reference routes the same click through <c>EditorPanel::_queueEvent</c>,
    /// which reads what the control under the pointer selects.
    /// </summary>
    /// <param name="screen">Pointer in logical pixels.</param>
    /// <returns>What the point hit, <see cref="EditorPanelHit.None"/> when nothing; the change is applied.</returns>
    internal EditorPanelHit ClickPanel(Point screen)
    {
        if (_panelView is null || !_panel.Contains(screen))
        {
            return EditorPanelHit.None;
        }

        EditorPanelHit hit = _panelView.HitTest(screen, PanelState);

        switch (hit.Kind)
        {
            case EditorPanelHitKind.Tool:
                _edits.SetTool(EditorPanel.ToolAt(hit.Index));

                break;

            case EditorPanelHitKind.Ground:
                _edits.SetBrush(EditorPanel.Grounds[hit.Index]);
                break;

            case EditorPanelHitKind.BrushKind:
                _edits.SelectBrushKind(hit.Index);
                break;

            case EditorPanelHitKind.Object:
                _edits.SelectBrushObject(hit.Index);
                break;

            case EditorPanelHitKind.BrushSize:
                _edits.SetBrushSize((BrushSize)hit.Index);
                break;

            case EditorPanelHitKind.EraseType:
                _edits.ToggleEraseType(EditorEraseTypes.Buttons[hit.Index]);
                break;

            case EditorPanelHitKind.DetailMode:
                _edits.SetDetailMode(EditorDetailModes.All[hit.Index]);
                break;

            case EditorPanelHitKind.System:
                ClickSystemButton(EditorPanel.SystemAt(hit.Index));
                break;

            case EditorPanelHitKind.None:
            default:
                break;
        }

        return hit;
    }

    /// <summary>What the instrument panel draws and accepts a click on, read off the edit session.</summary>
    private EditorPanelState PanelState => new(
        _edits.Tool, _edits.Brush, _edits.BrushSize, _edits.BrushKinds, _edits.BrushKind, _edits.BrushObjects,
        _edits.BrushObjectIndex, _edits.EraseTypes, _edits.DetailMode);

    /// <summary>
    /// Runs one of the six system buttons of the panel. Undo and redo are the two that the port can
    /// answer; the magnifier needs a zoom, and the specifications, the file and the menu dialogs are
    /// later milestones, so their buttons do nothing yet.
    /// </summary>
    /// <param name="button">The button whose cell was clicked.</param>
    private void ClickSystemButton(EditorSystemButton button)
    {
        switch (button)
        {
            case EditorSystemButton.Undo:
                Undo();
                break;

            case EditorSystemButton.Redo:
                Redo();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Centres the map on the tile under a point of the minimap, <c>GameArea::SetCenter( tile )</c> of
    /// <c>Radar::QueueEventProcessing</c>. A point outside the panel is ignored, which is what the
    /// <c>rect &amp; pt</c> test of the reference does while the radar holds the mouse.
    /// </summary>
    private void CentreOnRadar(Point cursor)
    {
        if (!_radar.Contains(cursor))
        {
            return;
        }

        Point tile = _minimap.TileAt(new Point(cursor.X - _radar.X, cursor.Y - _radar.Y));
        _editor.Map.CenterOn(tile.X, tile.Y);
    }

    /// <summary>
    /// Runs the active tool on the tile under the pointer, one step of the current stroke: it paints
    /// the ground, places the object or clears the tile.
    /// </summary>
    /// <remarks>
    /// Nothing is painted while the pointer is outside the viewport: the editor session only knows
    /// viewport pixels, and the frame, the radar and the panel are not map. The reference gets the same
    /// result from its <c>isMouseCursorPosInArea</c> test, and a stroke that runs from the map across
    /// the panel simply stops painting at the edge.
    /// </remarks>
    /// <returns>Whether the tile changed.</returns>
    public bool Paint() => _edits.Apply(_editor.HoverTile);

    /// <summary>
    /// Paints a rectangle of tiles as one stroke, the scripted version of a drag that
    /// <c>--editor-paint</c> uses. Tiles outside the map are skipped by the session.
    /// </summary>
    /// <param name="tile">Top left tile of the rectangle.</param>
    /// <param name="width">Width in tiles.</param>
    /// <param name="height">Height in tiles.</param>
    /// <param name="ground">Ground to paint, left alone when <see langword="null"/>.</param>
    /// <returns>Whether anything changed.</returns>
    public bool PaintArea(Point tile, int width, int height, MapGround? ground)
    {
        if (ground is { } brush)
        {
            _edits.SetBrush(brush);
        }

        _edits.BeginStroke();

        for (int y = tile.Y; y < tile.Y + height; ++y)
        {
            for (int x = tile.X; x < tile.X + width; ++x)
            {
                _edits.Paint(new Point(x, y));
            }
        }

        if (!_edits.EndStroke())
        {
            return false;
        }

        RebuildMinimap();
        return true;
    }

    /// <summary>Reverts the last stroke and rebuilds the minimap.</summary>
    /// <returns>Whether there was anything to undo.</returns>
    public bool Undo()
    {
        _painting = false;
        _edits.EndStroke();

        if (!_edits.Undo())
        {
            return false;
        }

        RebuildMinimap();
        return true;
    }

    /// <summary>Applies the last undone stroke again and rebuilds the minimap.</summary>
    /// <returns>Whether there was anything to redo.</returns>
    public bool Redo()
    {
        _painting = false;
        _edits.EndStroke();

        if (!_edits.Redo())
        {
            return false;
        }

        RebuildMinimap();
        return true;
    }

    /// <summary>Saves the edited map over the file it was opened from, <c>Ctrl+S</c>.</summary>
    /// <param name="error">Why the save failed, <see langword="null"/> when it worked.</param>
    /// <returns>Whether the map was written.</returns>
    public bool Save(out string? error) => SaveAs(_edits.SavePath, out error);

    /// <summary>Saves the edited map to <paramref name="filePath"/> and keeps saving there.</summary>
    /// <param name="filePath">File to write, created or replaced.</param>
    /// <param name="error">Why the save failed, <see langword="null"/> when it worked.</param>
    /// <returns>Whether the map was written.</returns>
    public bool SaveAs(string filePath, out string? error)
    {
        if (!_edits.SaveAs(filePath, out error))
        {
            return false;
        }

        // The status bar says "saved" for a moment: a save is the one action whose result is otherwise
        // invisible, and a player should not have to open the file to believe it.
        _savedFlash = SavedFlashSeconds;
        return true;
    }

    /// <summary>Selects the ground the brush paints, the number keys and the eyedropper.</summary>
    /// <param name="ground">Ground to paint.</param>
    /// <returns>Whether the brush changed.</returns>
    public bool SetBrush(MapGround ground) => _edits.SetBrush(ground);

    /// <summary>Selects the ground by its number, 1 water to 9 beach, the way the number keys do.</summary>
    /// <param name="number">Ground number, 1 to 9; other values are ignored.</param>
    /// <returns>Whether the brush changed.</returns>
    public bool SetBrushByNumber(int number) =>
        number >= 1 && number <= (int)MapGround.Beach && _edits.SetBrush((MapGround)number);

    /// <summary>Selects what the left button does, <c>T</c> for the ground and <c>X</c> for the eraser.</summary>
    /// <param name="tool">The instrument to use.</param>
    /// <returns>Whether the tool changed.</returns>
    public bool SetTool(EditorTool tool) => _edits.SetTool(tool);

    /// <summary>Steps through the object presets and selects the object tool, the <c>O</c> key.</summary>
    /// <param name="delta">Presets to step; wraps around the list.</param>
    /// <returns>The object after the step.</returns>
    public Mp2ObjectPreset CycleObject(int delta) => _edits.CycleObject(delta);

    /// <summary>
    /// Selects the object the object tool places by name or number, <c>--editor-object</c>.
    /// </summary>
    /// <param name="value">Name such as <c>Tree</c>, or a number from 1 to the number of presets.</param>
    /// <returns>Whether the object was found and selected.</returns>
    public bool SelectObject(string value) =>
        Mp2ObjectPresets.Find(value) is { } preset && _edits.SelectObject(preset);

    /// <summary>
    /// Places one object as its own stroke, the scripted version of a click that
    /// <c>--editor-place</c> uses; the tool the user selected stays untouched.
    /// </summary>
    /// <param name="tile">Tile to place on, outside the map is ignored.</param>
    /// <returns>Whether the tile changed.</returns>
    public bool PlaceObject(Point tile)
    {
        _edits.BeginStroke();
        bool changed = _edits.PlaceObject(tile);

        if (!_edits.EndStroke() || !changed)
        {
            return false;
        }

        RebuildMinimap();
        return true;
    }

    /// <summary>Steps the brush through the nine grounds, the <c>+</c> and <c>-</c> keys.</summary>
    /// <param name="delta">Grounds to step; wraps around the nine grounds.</param>
    /// <returns>The brush after the step.</returns>
    public MapGround CycleBrush(int delta)
    {
        const int count = (int)MapGround.Beach;

        int next = ((((int)_edits.Brush - 1) + delta) % count) + 1;

        if (next < 1)
        {
            next += count;
        }

        _edits.SetBrush((MapGround)next);
        return _edits.Brush;
    }

    /// <summary>Rolls the map with the wheel by whole tiles, one tile per notch.</summary>
    /// <param name="columns">Tiles to scroll right; negative scrolls left.</param>
    /// <param name="rows">Tiles to scroll down; negative scrolls up.</param>
    /// <remarks>
    /// The wheel is the only scroll the reference moves by whole tiles (<c>GameArea::ProcessMouseWheel</c>), so
    /// the step is a tile side and the clamp of <see cref="AdventureMapView.ScrollBy"/> does the rest.
    /// </remarks>
    public void ScrollByWheel(int columns, int rows) =>
        _editor.Map.ScrollBy(
            columns * _editor.Map.Tiles.TileWidth, rows * _editor.Map.Tiles.TileHeight);

    /// <summary>
    /// Starts or ends a press of the button that pans the map, the <c>mouseCursorAreaPressRight()</c> path of
    /// <c>GameArea::QueueEventProcessing</c>. The editor pans with the right button because the left one paints.
    /// </summary>
    /// <param name="down">Whether the button is now held.</param>
    private void SetPanButton(bool down)
    {
        if (down)
        {
            if (_panHeld)
            {
                return;
            }

            // 按下的那一点既是拖动的起点，也是「这是点击还是拖动」的基准；按下时清零累计，免得边滚的余量带进来。
            _panHeld = true;
            _panOrigin = _panCursor;
            _panning = false;
            _edgeElapsed = 0;
            return;
        }

        _panHeld = false;
        _panning = false;
        _panOrigin = null;
    }

    /// <summary>
    /// Moves the map with the pointer while the pan button is held; the shift is <c>last - current</c>, the
    /// direction of <c>SetCenterInPixels( center + last - current )</c>, so the terrain follows the pointer.
    /// </summary>
    /// <param name="cursor">Pointer in viewport pixels, or <see langword="null"/> outside the viewport.</param>
    private void DragTo(Point? cursor)
    {
        if (_panCursor == cursor)
        {
            return;
        }

        _panCursor = cursor;

        if (!_panHeld || cursor is not { } point)
        {
            return;
        }

        if (!_panning)
        {
            if (_panOrigin is not { } origin
                || (Math.Abs(origin.X - point.X) <= DragThreshold && Math.Abs(origin.Y - point.Y) <= DragThreshold))
            {
                return;
            }

            // The reference starts the drag from the press position, so the movement that crossed the
            // threshold is part of the first shift instead of being swallowed.
            _panning = true;
            _panAnchor = origin;
        }

        _editor.Map.ScrollBy(_panAnchor.X - point.X, _panAnchor.Y - point.Y);
        _panAnchor = point;
    }

    /// <summary>
    /// Rolls the map while the pointer sits in the <see cref="BorderWidth"/> pixels along an edge of the
    /// viewport, one <see cref="EdgeScrollStep"/> every <see cref="EdgeScrollInterval"/> seconds.
    /// </summary>
    /// <param name="elapsedSeconds">Time since the last frame.</param>
    /// <remarks>
    /// 参考的边滚在 <c>GameArea::SetScroll</c> 里，条件是指针落在边框内、且没有在拖（<c>assert( !isDragScroll() )</c>）；
    /// 节拍来自全局限时表（<c>validateAnimationDelay( SCROLL_DELAY )</c>），这里按帧的 <c>dt</c> 自己累计，
    /// 用的是同一个 20 ms，见 <see cref="EdgeScrollInterval"/>。
    /// </remarks>
    private void UpdateEdgeScroll(double elapsedSeconds)
    {
        if (_panning || _panCursor is not { } cursor)
        {
            _edgeElapsed = 0;
            return;
        }

        int direction = EdgeDirectionAt(cursor);

        // 已经贴到地图边上的方向不消耗节拍：参考的 GameArea 在 SetScroll 里被钳住，也不累计。
        if (direction == 0 || !_editor.Map.IsScrollingPossible)
        {
            _edgeElapsed = 0;
            return;
        }

        _edgeElapsed += elapsedSeconds;

        for (int steps = 0; steps < MaxEdgeStepsPerUpdate && _edgeElapsed >= EdgeScrollInterval; ++steps)
        {
            _edgeElapsed -= EdgeScrollInterval;
            ScrollOneStep(direction);
        }

        // Drop the backlog of a long frame instead of turning it into a burst on the next ones.
        _edgeElapsed = Math.Min(_edgeElapsed, EdgeScrollInterval);
    }

    /// <summary>Bits of the directions whose scroll region contains <paramref name="cursor"/>.</summary>
    /// <param name="cursor">Pointer in viewport pixels.</param>
    /// <returns>Any of <see cref="ScrollLeft"/>, <see cref="ScrollRight"/>, <see cref="ScrollTop"/>, <see cref="ScrollBottom"/>.</returns>
    private int EdgeDirectionAt(Point cursor)
    {
        Size viewport = _editor.Map.Viewport;
        int direction = 0;

        if (cursor.X < BorderWidth)
        {
            direction |= ScrollLeft;
        }

        if (cursor.X >= viewport.Width - BorderWidth)
        {
            direction |= ScrollRight;
        }

        if (cursor.Y < BorderWidth)
        {
            direction |= ScrollTop;
        }

        if (cursor.Y >= viewport.Height - BorderWidth)
        {
            direction |= ScrollBottom;
        }

        return direction;
    }

    /// <summary>Applies one edge scroll step of <see cref="EdgeScrollStep"/> pixels.</summary>
    /// <param name="direction">Directions to move in, bits from <see cref="EdgeDirectionAt"/>.</param>
    private void ScrollOneStep(int direction) =>
        _editor.Map.ScrollBy(
            (direction & ScrollLeft) != 0 ? -EdgeScrollStep : (direction & ScrollRight) != 0 ? EdgeScrollStep : 0,
            (direction & ScrollTop) != 0 ? -EdgeScrollStep : (direction & ScrollBottom) != 0 ? EdgeScrollStep : 0);

    /// <summary>
    /// Paints the interface frame: the <c>ADVBORD</c> image when the archive has it, flat borders
    /// otherwise, so that the layout is the same with and without the artwork.
    /// </summary>
    private void DrawFrameChrome() => _mapFrame.Draw(_frame.Pixels, _dark);

    /// <summary>
    /// Draws the tile borders of the visible area. The grid stops at the viewport, which the frame
    /// already cuts out: a line drawn past it would paint over the border.
    /// </summary>
    private void DrawGrid()
    {
        if (_viewport.Width <= 0 || _viewport.Height <= 0)
        {
            return;
        }

        foreach (int column in _editor.GridColumns())
        {
            ImageOps.VLine(_frame.Pixels, _viewport.X + column, _viewport.Y, _viewport.Height, _dark);
        }

        foreach (int row in _editor.GridRows())
        {
            ImageOps.HLine(_frame.Pixels, _viewport.X, _viewport.Y + row, _viewport.Width, _dark);
        }
    }

    /// <summary>Outlines the tile under the pointer, bright outside and dark inside, so that the tile
    /// reads on both the light and the dark grounds of a map.</summary>
    /// <remarks>
    /// The outline is clipped to the viewport: the tile under a pointer that sits on the last visible
    /// row still reaches into the border, and the frame must not be painted over.
    /// </remarks>
    private void DrawHover()
    {
        if (_editor.HoverArea is not { } area)
        {
            return;
        }

        Rect clipped = new Rect(
            _viewport.X + area.X,
            _viewport.Y + area.Y,
            area.Width,
            area.Height).Intersect(_viewport);

        if (clipped.IsEmpty)
        {
            return;
        }

        ImageOps.DrawRect(_frame.Pixels, clipped, _bright);

        if (clipped.Width > 2 && clipped.Height > 2)
        {
            ImageOps.DrawRect(
                _frame.Pixels,
                new Rect(clipped.X + 1, clipped.Y + 1, clipped.Width - 2, clipped.Height - 2),
                _dark);
        }
    }

    /// <summary>
    /// Draws the radar map into its bay. The bay itself is part of the frame, so the minimap does not
    /// outline it any more.
    /// </summary>
    private void DrawMinimap()
    {
        _minimap.Draw(_frame.Pixels, _radar.Position, _editor.Map.VisibleTileArea);
    }

    /// <summary>
    /// Re-bakes the minimap after the tiles changed. It is the one cache in the editor: the map view
    /// reads the tiles on every frame, the minimap keeps one colour per tile from <c>Build</c>.
    /// </summary>
    private void RebuildMinimap() => _minimap = EditorRadar.Build(Map);

    /// <summary>
    /// Draws the bay below the radar: the instrument panel of the reference when its artwork is in the
    /// archive, a text card carrying the same facts otherwise.
    /// </summary>
    private void DrawPanel()
    {
        if (_fonts is null)
        {
            return;
        }

        if (_panelView is not null)
        {
            // The bay carries the patterns of ADVBORD, and its last rows are the bottom border of the
            // frame: the panel of the reference paints its own background over both — the stone
            // texture of STONEBAK, or a flat bay when the archive has none.
            if (!_panelView.HasBackground)
            {
                ImageOps.FillRect(_frame.Pixels, _panel, _dark);
            }

            _panelView.Draw(_frame.Pixels, _fonts.Small, PanelState, _bright);

            return;
        }

        DrawPanelText();
    }

    /// <summary>
    /// The text card the editor falls back on when <c>EDITBTNS</c> or <c>EDITPANL</c> is missing: the
    /// same facts and keys, laid out like the picker of the reference.
    /// </summary>
    private void DrawPanelText()
    {
        if (_fonts is null)
        {
            return;
        }

        // The bay of the frame carries a decorated header and recessed slots for the buttons of the
        // reference. The header stays, and the slots are cleared to a dark card that the text can be
        // read on: a text line over the slot borders reads as noise.
        Rect card = new Rect(
            _panel.X + 4,
            _panel.Y + PanelHeaderHeight,
            Math.Max(_panel.Width - 8, 0),
            Math.Max(_panel.Height - PanelHeaderHeight - 4, 0));
        ImageOps.FillRect(_frame.Pixels, card, _dark);

        Rect text = card.Inflate(-4, -4);

        if (text.IsEmpty)
        {
            return;
        }

        IGlyphSource font = _fonts.Small;
        int y = text.Y;

        void Line(string value)
        {
            if (y + font.LineHeight > text.Bottom)
            {
                return;
            }

            GlyphText.DrawLine(
                font,
                _frame.Pixels,
                new Point(text.X, y),
                value,
                new Rect(text.X, y, text.Width, font.LineHeight));

            y += font.LineHeight + 2;
        }

        Line("MAP EDITOR");
        Line(FormattableString.Invariant($"{MapName} {Map.Width}x{Map.Height}"));

        Line(_edits.Tool switch
        {
            EditorTool.Erase => "erase",
            EditorTool.Detail => FormattableString.Invariant($"detail {EditorDetailModes.Name(_edits.DetailMode)}"),
            EditorTool.Stream => "stream",
            EditorTool.Road => "road",
            EditorTool.LandscapeObjects or EditorTool.AdventureObjects or EditorTool.KingdomObjects
                or EditorTool.Monsters => FormattableString.Invariant(
                    $"object {_edits.SelectedBrushObject?.Name ?? "none"}"),
            _ => FormattableString.Invariant($"ground {_edits.Brush}"),
        });

        if (_edits.IsDirty)
        {
            Line("edited");
        }
        else if (_savedFlash > 0.0)
        {
            Line("saved");
        }
        else
        {
            Line("clean");
        }

        Line(_editor.HoverTile is { } tile
            ? FormattableString.Invariant($"{tile.X},{tile.Y} {_editor.HoverGround}")
            : "no tile");

        if (_editor.Hover is { } info && info.ObjectType != Mp2ObjectType.None)
        {
            Line(Mp2ObjectName.Of(info.ObjectType));
        }

        if (_editor.GridEnabled)
        {
            Line("grid on");
        }

        Line(FormattableString.Invariant($"undo {_edits.UndoDepth}"));
        Line(FormattableString.Invariant($"scroll {_editor.Map.Scroll.X},{_editor.Map.Scroll.Y}"));

        // The hints sit at the bottom of the bay, where the reference keeps its pickers: the state
        // above them grows with one line (an object name), the hints do not move.
        const int Hints = 6;
        int step = font.LineHeight + 2;
        int hintsTop = text.Bottom - (Hints * step) + (step - font.LineHeight) - 2;

        y = Math.Max(hintsTop - step + 1, y + step);
        ImageOps.HLine(_frame.Pixels, text.X, y, text.Width, _dark);
        y = hintsTop;

        Line("1-9 ground  T tool");
        Line("O object  X erase");
        Line("I info  G grid");
        Line("^Z undo  ^Y redo");
        Line("arrows/wheel scroll");
        Line("Home start  Esc menu");
    }
}
