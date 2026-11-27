using System.Globalization;
using FHeroes2.App.Fonts;
using FHeroes2.App.Localization;
using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

using ZoomLevel = FHeroes2.Game.Kingdom.ZoomLevel;

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
/// The magnifier of the system row opens the View World window over the map being edited, the
/// <c>eventViewWorld()</c> of <c>editor_interface.cpp:2183</c>: a modal that builds a throwaway world out
/// of the map, caches the whole map at every zoom level and comes back with the level it was left at.
/// Not ported yet: the property dialogs of the objects, which the information box only reports.
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

    /// <summary>
    /// Seconds between two steps of the map object animation, <see cref="GameDelays.MapsDelayMilliseconds"/>.
    /// </summary>
    /// <remarks>
    /// 参考在 <c>editor_interface.cpp:2017-2024</c> 里问 <c>Game::validateAnimationDelay( MAPS_DELAY )</c>，格子由
    /// <see cref="GameDelays.InitAnimateDelays"/> 装成二百五十毫秒。与卷屏同样按帧的 <c>dt</c> 自己累计：全局节拍表是
    /// 静态状态，用例里推动不了，画面也会随宿主帧率抖动。
    /// </remarks>
    public const double AnimationInterval = GameDelays.MapsDelayMilliseconds / 1000.0;

    private const int Padding = 6;

    /// <summary>
    /// Most edge scroll steps applied in one frame. A stalled main loop can hand in a large
    /// <c>elapsedSeconds</c>; without a cap the view would jump across the whole map at once.
    /// </summary>
    private const int MaxEdgeStepsPerUpdate = 4;

    /// <summary>
    /// Most animation index steps taken in one frame, the same protection as <see cref="MaxEdgeStepsPerUpdate"/>:
    /// a long frame must not make the objects jump through their cycle.
    /// </summary>
    private const int MaxAnimationStepsPerUpdate = 4;

    private const int ScrollLeft = 1;
    private const int ScrollRight = 2;
    private const int ScrollTop = 4;
    private const int ScrollBottom = 8;

    /// <summary>Header of the note the Language option shows when only English is installed.</summary>
    private const string AttentionTitle = "Attention";

    /// <summary>Body of that note, verbatim from <c>editor_options.cpp:293-295</c>.</summary>
    private const string OnlyEnglishMessage =
        "Your version of Heroes of Might and Magic II does not support any other languages than English.";

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

    /// <summary>The archive the chrome is read from, kept so that a theme change can read it again.</summary>
    private readonly AssetBundle? _assets;

    /// <summary>
    /// The frame of the interface, rebuilt when the theme changes: the chrome is the one thing of the layout that
    /// differs between <c>ADVBORD</c> and <c>ADVBORDE</c>, the bays themselves stay where they are.
    /// </summary>
    private MapFrame _mapFrame;

    private Rect _viewport;
    private PixelBuffer _viewportPixels;
    private Rect _radar;
    private Rect _panel;
    private EditorPanel? _panelView;

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

    /// <summary>
    /// The adventure map animation index the tiles are drawn with, <c>Game::getAdventureMapAnimationIndex()</c>.
    /// </summary>
    private uint _animationPhase;

    /// <summary>Seconds accumulated since the last animation step, the <c>MAPS_DELAY</c> of the reference.</summary>
    private double _animationElapsed;

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

    /// <summary>What the scene asks of the host: the frame pump of its windows, <see langword="null"/> until wired.</summary>
    private readonly EditorSceneHost _host;

    /// <summary>
    /// Passability of every tile, <see langword="null"/> until the overlay is switched on once. Kept across frames and
    /// patched tile by tile, because the editor replaces the whole map on every edit.
    /// </summary>
    private MapTileSet? _passability;

    /// <summary>The map version <see cref="_passability"/> was built from; the reference edits its world in place.</summary>
    private Mp2Map? _passabilitySource;

    /// <summary>
    /// The zoom level the View World window was left at, the <c>conf.ViewWorldZoomLevel()</c> the reference keeps
    /// (<c>view_world.cpp:771-772</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="AdventureMapScene"/> 一样只记在场景里，不进 <see cref="Settings"/>：端口的设置还没有这一项，
    /// 两个场景各记各的，退出游戏就丢——与参考在 <c>--no-config</c> 下的行为一致。
    /// </remarks>
    private ZoomLevel _viewWorldZoom = ZoomLevel.ZoomLevel2;

    private EditorScene(
        ScreenBuffer frame,
        GameFonts? fonts,
        MapEditorView editor,
        MapEditSession edits,
        EditorRadar minimap,
        MapFrame mapFrame,
        EditorPanel? panelView,
        bool hasInfoFrame,
        Sprite? popupFrame,
        EditorSceneHost host,
        AssetBundle? assets)
    {
        _frame = frame;
        _fonts = fonts;
        _editor = editor;
        _edits = edits;
        _minimap = minimap;
        _assets = assets;
        _mapFrame = mapFrame;
        _panelView = panelView;
        _hasInfoFrame = hasInfoFrame;
        _popupFrame = popupFrame;
        _host = host;

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

    /// <summary>
    /// The animation index the last frame was drawn with, <c>Game::getAdventureMapAnimationIndex()</c>.
    /// </summary>
    /// <remarks>
    /// 参考的这个值是全局的，端口的动画只服务于编辑器这一层（冒险地图的界面用的是 UI 那套 World 渲染），所以放在场景里，
    /// 也不再改 <c>GameAnimation</c> 的静态计数器——那里推进一次会连带影响别的场景。
    /// </remarks>
    public uint AnimationPhase => _animationPhase;

    /// <summary>Name of the map, as stored in its header.</summary>
    public string MapName => Map.Info.Name;

    /// <summary>The rectangle the map is drawn into, between the border and the radar column.</summary>
    public Rect ViewportArea => _viewport;

    /// <summary>The instrument panel below the radar, the <c>EditorPanel</c> of the reference.</summary>
    public Rect PanelArea => _panel;

    /// <summary>Size of the logical frame the editor draws into.</summary>
    public Size FrameSize => _frame.Size;

    /// <summary>
    /// Runs a modal window of the editor to its end, the frame pump <c>GameHost</c> wires up right after the
    /// scene is built; <see langword="null"/> while it is not, and then the windows the panel asks for log
    /// their failure instead of opening.
    /// </summary>
    /// <remarks>
    /// 参考的编辑器窗口都是阻塞的（<c>showStandardTextMessage</c> 之类），端口只有一条帧循环，所以窗口进宿主
    /// 的内层循环，关掉才回到编辑器——与 <see cref="AdventureMapScene"/> 借 <c>AdventureHeroActionHost</c>
    /// 弹窗是同一条缝。
    /// </remarks>
    public Action<IModalScreen>? RunModal
    {
        get => _host.RunModal;
        set => _host.RunModal = value;
    }

    /// <summary>
    /// Writes the preferences to the configuration file, the <c>conf.Save( configFileName )</c> of
    /// <c>openEditorSettings()</c>; <see langword="null"/> when the host runs without a configuration file.
    /// </summary>
    /// <remarks>
    /// 参考在选项循环里改完设置就 <c>conf.Save</c>，端口把「怎么落盘」留给 <c>GameHost</c>——配置文件是它的东西，
    /// 编辑器不该知道路径。没接上时设置只留在内存里，和 <c>--no-config</c> 时的行为一样。
    /// </remarks>
    public Action? SavePreferences { get; set; }

    /// <summary>
    /// Asks the host to leave the editor, the <c>fheroes2::GameMode</c> the reference's <c>eventFileDialog()</c> and
    /// <c>processEditorExitEvent()</c> return to the main loop (<c>editor_interface.cpp:1393-1401</c>, <c>:2121-2153</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的编辑器自己就是主循环的一帧，退出与回主菜单都是「返回一个 GameMode」。端口把这一步交回
    /// <c>GameHost</c>——它才持有场景表与客户端生命周期，与 <see cref="SavePreferences"/> 同一条缝。
    /// </para>
    /// <para>
    /// 只在确认框答了 YES 之后才调用；没接上时什么都不发生，编辑器留下（<c>--scene editor</c> 那种没有菜单的
    /// 启动方式就是这样）。
    /// </para>
    /// </remarks>
    public Action<EditorLeaveRequest>? LeaveEditor { get; set; }

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

    /// <summary>
    /// The directory the save window lists and writes into; empty to use the writable data directory's
    /// <c>maps</c> folder.
    /// </summary>
    /// <remarks>
    /// 参考只认 <c>System::GetDataDirectory( "fheroes2" )</c>；端口留这个缝是给用例换成临时目录，免得往用户的
    /// <c>%APPDATA%</c> 里写字。
    /// </remarks>
    public string SaveMapDirectory { get; set; } = string.Empty;

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

    /// <summary>
    /// The per-object metadata tables of the map, i.e. what the castle details window edits
    /// (<c>_mapFormat.castleMetadata</c> of the reference).
    /// </summary>
    /// <remarks>
    /// MP2 里没有这套表，于是它从空开始，城堡详情窗确认后才写入 <see cref="MapObjectMetadataStore.Castles"/>。
    /// 历史栈还没接：参考的 <c>ActionCreator( CASTLE_METADATA )</c> 因此缺一条撤销，见本方法下方的日志。
    /// </remarks>
    public MapObjectMetadataStore Metadata => _metadata;

    /// <summary>
    /// The tile whose castle details window is open, or <see langword="null"/> when none is.
    /// </summary>
    /// <remarks>
    /// 窗口是模态的（<see cref="EditorSceneHost.RunCastleDetails"/> 里再起一层帧循环），所以这个字段只在
    /// 「跑完回来后」有意义：用例与日志靠它确认点下去确实开了窗。
    /// </remarks>
    public Point? LastCastleDetailsTile { get; private set; }

    /// <summary>
    /// Opens the castle details window of the town on a tile, <c>Editor::castleDetailsDialog()</c> of
    /// <c>editor_interface.cpp:2232-2246</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与参考同一个顺序：从城堡块读出元数据与归属色（MP2 的等价物，见 <see cref="Mp2CastleMetadata"/>），把守军
    /// 填进 <see cref="Army"/>，跑窗，只有 <see cref="CastleDetailsOutcome.Accepted"/> 才写回元数据表。
    /// </para>
    /// <para>
    /// 表里已经有这一格（<see cref="Mp2TileInfo.Level1ObjectUid"/>）的条目时以表里的为准——作者上一次确认过的改动
    /// 比地图文件里的原样新。取消时表里原样的东西一个字都不动，与参考「<c>newCastleMetadata</c> 只是个副本」一致。
    /// </para>
    /// <para>
    /// <b>已知缺口</b>：参考在写回前用 <c>ActionCreator( CASTLE_METADATA )</c> 把改动记进撤销栈，端口的
    /// <see cref="MapEditSession"/> 只记地块，元数据表还没有历史，所以这里只写日志。
    /// </para>
    /// </remarks>
    /// <param name="tile">Tile of the town's gate, in map tiles.</param>
    /// <returns>Whether the tile held a town and the window ran.</returns>
    public bool OpenCastleDetails(Point tile)
    {
        if (tile.X < 0 || tile.X >= Map.Width || tile.Y < 0 || tile.Y >= Map.Height)
        {
            return false;
        }

        Mp2TileInfo info = Map.Tile(tile.X, tile.Y);

        if (!Mp2CastleMetadata.IsTownTile(info.ObjectType))
        {
            return false;
        }

        _ = Mp2CastleMetadata.TryRead(Map, tile, out CastleMetadata fromBlock, out PlayerColor owner);

        // 城堡表里那条决定种族：物件自身不带种族，参考也是问物件元数据里的那一位。
        MapRace race = MapRace.Knight;

        foreach (Mp2CastlePosition position in Map.Castles)
        {
            if (position.X == tile.X && position.Y == tile.Y)
            {
                race = position.Race;

                break;
            }
        }

        uint uid = info.Level1ObjectUid;
        CastleMetadata metadata = _metadata.Castles.TryGetValue(uid, out CastleMetadata? recorded)
            ? recorded.Clone()
            : fromBlock;

        var army = new Army();

        _ = MapFormatArmy.LoadCastleArmy(army, metadata);

        var state = new CastleDetailsState(metadata, race, owner, army);

        CastleDetailsOutcome outcome = _host.RunCastleDetails(state, _frame.Palette);

        LastCastleDetailsTile = tile;

        if (outcome != CastleDetailsOutcome.Accepted)
        {
            return true;
        }

        MetadataMapAction<CastleMetadata> action = new(_metadata.Castles, metadata => metadata.Clone());
        _metadata.Castles[uid] = state.Metadata;
        CommitMetadataChange(action);

        GameLog.Info($"The castle at ({tile.X}; {tile.Y}) was accepted.");

        return true;
    }

    /// <summary>
    /// Puts a metadata change on the editor's history, the <c>commit()</c> of the reference's
    /// <c>fheroes2::ActionCreator</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用法与参考完全一致：改表**之前**先用 <c>new MetadataMapAction&lt;T&gt;( table )</c> 捕获前值（构造即快照，
    /// 与 <c>ActionCreator</c> 的构造函数同语义），改完再调本方法把后值也捕获下来并入栈。
    /// </para>
    /// <para>
    /// 入栈的是 <see cref="MapEditSession.RecordStep"/>，所以 Ctrl+Z / Ctrl+Y 与地块笔画共用一条时间线：
    /// 先改名再画一格，两次撤销正好按相反顺序退回。
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The per-object metadata type of the table.</typeparam>
    /// <param name="action">The action whose table has just been changed.</param>
    private void CommitMetadataChange<T>(MetadataMapAction<T> action)
    {
        if (!action.Prepare(Map.LastObjectUid))
        {
            return;
        }

        _edits.RecordStep(action);
    }

    /// <summary>
    /// Opens the spell chooser of a shrine or a pyramid, the two <c>Editor::openSpellSelectionWindow()</c> calls of
    /// <c>editor_interface.cpp:2484-2504</c> and <c>:2520-2540</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考给这三个神殿各一级（1/2/3 环），金字塔是 5 级；标题是物件名加冒号。选中结果写进
    /// <c>selectionObjectMetadata[uid].selectedItems</c>，只有真的变了才提交历史（历史栈还没接，见
    /// <c>bpn.3.3</c>）。
    /// </para>
    /// <para>
    /// 空列表在参考里表示「按默认」：神殿默认给本环的随机法术，所以取消与「全部启用」都不写表——端口照抄，
    /// 只在接受且列表非空时建条目。
    /// </para>
    /// </remarks>
    /// <param name="tile">Tile the author clicked.</param>
    /// <returns>Whether the tile was a shrine or a pyramid and the window ran.</returns>
    public bool OpenSpellChooser(Point tile)
    {
        if (tile.X < 0 || tile.X >= Map.Width || tile.Y < 0 || tile.Y >= Map.Height)
        {
            return false;
        }

        Mp2TileInfo info = Map.Tile(tile.X, tile.Y);

        int spellLevel = SpellLevelOf(info.ObjectType);

        if (spellLevel == 0)
        {
            return false;
        }

        uint uid = info.Level1ObjectUid;
        SelectionObjectMetadata metadata = _metadata.Selections.TryGetValue(uid, out SelectionObjectMetadata? recorded)
            ? new SelectionObjectMetadata { SelectedItems = [.. recorded.SelectedItems] }
            : new SelectionObjectMetadata();

        string title = Mp2ObjectName.Of(info.ObjectType) + ":";
        List<SpellType> current = [.. metadata.SelectedItems.Select(id => (SpellType)id)];

        EditorSpellSelectionDialogResult result = _host.RunSpellSelection(title, spellLevel, current);

        LastSpellChooserTile = tile;

        if (!result.Accepted || result.Spells.Count == 0)
        {
            return true;
        }

        var updated = new List<int>(result.Spells.Count);

        foreach (SpellType spell in result.Spells)
        {
            updated.Add((int)spell);
        }

        if (recorded is not null && recorded.SelectedItems.SequenceEqual(updated))
        {
            return true;
        }

        MetadataMapAction<SelectionObjectMetadata> action = new(_metadata.Selections, metadata => metadata.Clone());
        _metadata.Selections[uid] = new SelectionObjectMetadata { SelectedItems = updated };
        CommitMetadataChange(action);

        GameLog.Info(
            $"The {Mp2ObjectName.Of(info.ObjectType)} at ({tile.X}; {tile.Y}) keeps {updated.Count} spell(s).");

        return true;
    }

    /// <summary>
    /// The spell level a shrine or a pyramid offers, or zero when the object is neither
    /// (<c>editor_interface.cpp:2489-2500</c>, <c>:2530</c>).
    /// </summary>
    /// <param name="type">The object type of the clicked tile.</param>
    /// <returns>1 to 3 for the three shrines, 5 for a pyramid, zero otherwise.</returns>
    private static int SpellLevelOf(Mp2ObjectType type) => type switch
    {
        Mp2ObjectType.ShrineFirstCircle or Mp2ObjectType.ActionShrineFirstCircle => 1,
        Mp2ObjectType.ShrineSecondCircle or Mp2ObjectType.ActionShrineSecondCircle => 2,
        Mp2ObjectType.ShrineThirdCircle or Mp2ObjectType.ActionShrineThirdCircle => 3,
        Mp2ObjectType.Pyramid or Mp2ObjectType.ActionPyramid => 5,
        _ => 0,
    };

    /// <summary>
    /// The tile whose spell chooser ran last, or <see langword="null"/> when none did; the tests and the log read it.
    /// </summary>
    public Point? LastSpellChooserTile { get; private set; }

    /// <summary>
    /// Opens the text box of a sign or a bottle, the <c>Dialog::inputString</c> of
    /// <c>editor_interface.cpp:2266-2281</c>.
    /// </summary>
    /// <remarks>
    /// 标题是 <c>Input %{object} text:</c>（物件名填进去），正文为空，多行，上限
    /// <see cref="SignMessageCharLimit"/>；接受且文本真的变了才写 <c>signMetadata[uid].Message</c>。
    /// </remarks>
    /// <param name="tile">Tile the author clicked.</param>
    /// <returns>Whether the tile was a sign or a bottle and the box ran.</returns>
    public bool EditSign(Point tile)
    {
        if (!TryReadEditableTile(tile, out Mp2TileInfo info) ||
            info.ObjectType is not (Mp2ObjectType.Sign or Mp2ObjectType.ActionSign or
                Mp2ObjectType.Bottle or Mp2ObjectType.ActionBottle))
        {
            return false;
        }

        uint uid = info.Level1ObjectUid;
        string current = _metadata.Signs.TryGetValue(uid, out SignMetadata? recorded) ? recorded.Message : string.Empty;

        string header = string.Format(CultureInfo.InvariantCulture, InputObjectTextHeader, Mp2ObjectName.Of(info.ObjectType));
        InputStringDialog dialog = InputStringDialog.Create(string.Empty, header, SignMessageCharLimit,
            isMultiLine: true, initialText: current);

        if (_host.TryCreateInput(dialog) is not { } window || RunModal is not { } runModal)
        {
            GameLog.Warn("The sign text needs the input window, and it cannot be built.");

            return true;
        }

        runModal(window);

        LastObjectEditorTile = tile;

        if (window.Window.Result is not { } text || text == current)
        {
            return true;
        }

        MetadataMapAction<SignMetadata> action = new(_metadata.Signs, metadata => metadata.Clone());
        _metadata.Signs[uid] = new SignMetadata { Message = text };
        CommitMetadataChange(action);

        GameLog.Info(
            $"The {Mp2ObjectName.Of(info.ObjectType)} at ({tile.X}; {tile.Y}) now reads {text.Length} character(s).");

        return true;
    }

    /// <summary>
    /// Opens the count window of a monster stack, the <c>Dialog::SelectCount()</c> of
    /// <c>editor_interface.cpp:2294-2331</c>.
    /// </summary>
    /// <remarks>
    /// 参考还带一个「这一堆可能是哪些怪」的多选 UI（<c>ObjectMultiSelectionUI</c>），端口先只做数量：数量本身才是
    /// 地图要存的东西，多选那块要等 <c>a7l.10.4</c> 的多选骨架。
    /// </remarks>
    /// <param name="tile">Tile the author clicked.</param>
    /// <returns>Whether the tile held a monster stack and the window ran.</returns>
    public bool EditMonsterCount(Point tile)
    {
        if (!TryReadEditableTile(tile, out Mp2TileInfo info) || !Mp2ObjectData.CarriesTroopCount(info.ObjectType))
        {
            return false;
        }

        int monsterId = Mp2ObjectData.MonsterOf(info);

        if (monsterId == Mp2MonsterName.Unknown)
        {
            return false;
        }

        uint uid = info.Level1ObjectUid;
        MonsterMetadata metadata = _metadata.Monsters.TryGetValue(uid, out MonsterMetadata? recorded)
            ? recorded
            : new MonsterMetadata();
        int current = metadata.Count > 0 ? metadata.Count : (int)info.Metadata;

        string header = string.Format(CultureInfo.InvariantCulture, SetMonsterCountHeader,
            MonsterNames.GetSingular((MonsterType)monsterId));
        SelectCountDialog dialog = SelectCountDialog.Create(header, 0, MaximumMonsterCount, step: 1);

        if (_host.TryCreateSelectCount(dialog, current) is not { } window || RunModal is not { } runModal)
        {
            GameLog.Warn($"The stack of {monsterId} needs the count window, and it cannot be built.");

            return true;
        }

        runModal(window);

        LastObjectEditorTile = tile;

        if (window.Result is not { } count || count == current)
        {
            return true;
        }

        // metadata 是表里那条的同一份对象（没有条目时是新建的），所以先入栈再改它的 Count：
        // 前值是构造时拷下的那份，后值是 Prepare 时看到的这份。
        MetadataMapAction<MonsterMetadata> action = new(_metadata.Monsters, metadata => metadata.Clone());
        metadata.Count = count;
        _metadata.Monsters[uid] = metadata;
        CommitMetadataChange(action);

        GameLog.Info($"The stack at ({tile.X}; {tile.Y}) holds {count} creature(s) now.");

        return true;
    }

    /// <summary>
    /// Opens the count window of a resource pile, the <c>Dialog::SelectCount()</c> of
    /// <c>editor_interface.cpp:2333-2362</c>.
    /// </summary>
    /// <param name="tile">Tile the author clicked.</param>
    /// <returns>Whether the tile held a resource pile and the window ran.</returns>
    public bool EditResourceCount(Point tile)
    {
        if (!TryReadEditableTile(tile, out Mp2TileInfo info) ||
            info.ObjectType is not (Mp2ObjectType.Resource or Mp2ObjectType.ActionResource))
        {
            return false;
        }

        Mp2ResourceType resource = Mp2ObjectData.ResourceOf(info);

        if (resource == Mp2ResourceType.None)
        {
            return false;
        }

        uint uid = info.Level1ObjectUid;
        int current = _metadata.Resources.TryGetValue(uid, out ResourceMetadata? recorded) ? recorded.Count : 0;

        string header = string.Format(CultureInfo.InvariantCulture, SetResourceCountHeader,
            Mp2ObjectData.Name(resource));
        SelectCountDialog dialog = SelectCountDialog.Create(header, 0, MaximumResourceCount, step: 1);

        if (_host.TryCreateSelectCount(dialog, current) is not { } window || RunModal is not { } runModal)
        {
            GameLog.Warn($"The {resource} pile needs the count window, and it cannot be built.");

            return true;
        }

        runModal(window);

        LastObjectEditorTile = tile;

        if (window.Result is not { } count || count == current)
        {
            return true;
        }

        MetadataMapAction<ResourceMetadata> action = new(_metadata.Resources, metadata => metadata.Clone());
        _metadata.Resources[uid] = new ResourceMetadata { Count = count };
        CommitMetadataChange(action);

        GameLog.Info(
            $"The {resource} pile at ({tile.X}; {tile.Y}) holds {count} now; " +
            "the count is not in the undo history yet.");

        return true;
    }

    /// <summary>
    /// The artifact editors of a tile, the four <c>ADVENTURE_ARTIFACTS</c> branches of
    /// <c>editor_interface.cpp:2346-2437</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 四支的分派与参考一致：终极神器问「半径 + 那八件里允许哪些」，法术卷轴问「哪条法术」，四种随机神器问「按等级
    /// 允许哪些」，其余合法神器只弹说明框。随机那三支都是多选，参考里是 <c>Dialog::multiSelectArtifact()</c>。
    /// </para>
    /// <para>
    /// 只有接受且列表真的变了才写 <c>artifactMetadata[uid]</c>（并沿用 <see cref="EditorScene"/> 的同一条撤销时间线）；
    /// 取消什么都不写，与参考的 <c>if ( temp != selected )</c> 同口径。
    /// </para>
    /// </remarks>
    /// <param name="tile">Tile the author clicked.</param>
    /// <returns>Whether the tile held an artifact and something ran.</returns>
    public bool EditArtifact(Point tile)
    {
        if (!TryReadEditableTile(tile, out Mp2TileInfo info))
        {
            return false;
        }

        // 只有单件神器那一支要问「这是哪一件」：随机神器的帧是固定标记，而 FromMapIndex 在那些下标上会真的抽一次
        // （ArtifactRandom.Next），编辑器不该消耗随机序列。
        ArtifactType artifact = info.ObjectType is Mp2ObjectType.Artifact or Mp2ObjectType.ActionArtifact
            ? ArtifactOf(info)
            : ArtifactType.Unknown;

        uint uid = info.Level1ObjectUid;
        ArtifactMetadata metadata = _metadata.Artifacts.TryGetValue(uid, out ArtifactMetadata? recorded)
            ? recorded
            : new ArtifactMetadata();

        switch (info.ObjectType)
        {
            case Mp2ObjectType.RandomUltimateArtifact:
            case Mp2ObjectType.ActionRandomUltimateArtifact:
                return EditUltimateArtifact(tile, uid, metadata);

            case Mp2ObjectType.Artifact:
            case Mp2ObjectType.ActionArtifact when artifact == ArtifactType.SpellScroll:
                return EditSpellScroll(tile, uid, metadata);

            case Mp2ObjectType.RandomArtifact:
            case Mp2ObjectType.ActionRandomArtifact:
            case Mp2ObjectType.RandomArtifactTreasure:
            case Mp2ObjectType.ActionRandomArtifactTreasure:
            case Mp2ObjectType.RandomArtifactMinor:
            case Mp2ObjectType.ActionRandomArtifactMinor:
            case Mp2ObjectType.RandomArtifactMajor:
            case Mp2ObjectType.ActionRandomArtifactMajor:
                return EditRandomArtifact(tile, uid, metadata, info.ObjectType);

            default:
                break;
        }

        if (!ArtifactTypes.IsValid(artifact))
        {
            return false;
        }

        // 单件神器：只有说明框，没有可改的东西（参考的 ArtifactDialogElement::showPopup）。
        if (RunModal is not { } runModal || _host.TryCreateArtifactInfo(artifact) is not { } box)
        {
            return false;
        }

        runModal(box);

        LastArtifactEditorTile = tile;

        return true;
    }

    /// <summary>
    /// The radius and the candidates of the ultimate artifact, <c>editor_interface.cpp:2349-2382</c>.
    /// </summary>
    /// <param name="tile">Tile the author clicked.</param>
    /// <param name="uid">Object UID, the key of the metadata table.</param>
    /// <param name="metadata">Its recorded data, or a fresh one.</param>
    /// <returns>Whether a window ran.</returns>
    private bool EditUltimateArtifact(Point tile, uint uid, ArtifactMetadata metadata)
    {
        int radius = metadata.Radius;
        SelectCountDialog dialog = SelectCountDialog.Create(SetUltimateRadiusHeader, 0, MaximumUltimateRadius, 1);

        if (_host.TryCreateSelectCount(dialog, radius) is not { } count || RunModal is not { } runModal)
        {
            GameLog.Warn("The ultimate artifact needs the count window, and it cannot be built.");

            return true;
        }

        runModal(count);

        LastArtifactEditorTile = tile;

        if (count.Result is not { } chosen || chosen == radius)
        {
            return true;
        }

        MetadataMapAction<ArtifactMetadata> action = new(_metadata.Artifacts, entry => entry.Clone());
        metadata.Radius = chosen;
        _metadata.Artifacts[uid] = metadata;
        CommitMetadataChange(action);

        GameLog.Info($"The ultimate artifact at ({tile.X}; {tile.Y}) is buried within {chosen} tile(s).");

        return true;
    }

    /// <summary>
    /// The spell of a spell scroll, <c>editor_interface.cpp:2383-2400</c>.
    /// </summary>
    /// <param name="tile">Tile the author clicked.</param>
    /// <param name="uid">Object UID.</param>
    /// <param name="metadata">Its recorded data.</param>
    /// <returns>Whether a window ran.</returns>
    private bool EditSpellScroll(Point tile, uint uid, ArtifactMetadata metadata)
    {
        SpellType current = metadata.Selected.Count > 0 ? (SpellType)metadata.Selected[0] : SpellType.None;

        if (_host.TryCreateSpellSelect(spellLevel: null, current, []) is not { } window ||
            RunModal is not { } runModal)
        {
            GameLog.Warn("The spell scroll needs the spell picker, and it cannot be built.");

            return true;
        }

        runModal(window);

        LastArtifactEditorTile = tile;

        SpellType chosen = window.SelectedSpell;

        // 参考：没选法术、或者选了同一个，就什么都不做。
        if (chosen == SpellType.None || chosen == current)
        {
            return true;
        }

        MetadataMapAction<ArtifactMetadata> action = new(_metadata.Artifacts, entry => entry.Clone());
        metadata.Selected.Clear();
        metadata.Selected.Add((int)chosen);
        _metadata.Artifacts[uid] = metadata;
        CommitMetadataChange(action);

        GameLog.Info($"The spell scroll at ({tile.X}; {tile.Y}) now carries {new Spell(chosen).GetName()}.");

        return true;
    }

    /// <summary>
    /// The candidates of a random artifact, <c>editor_interface.cpp:2401-2427</c>.
    /// </summary>
    /// <param name="tile">Tile the author clicked.</param>
    /// <param name="uid">Object UID.</param>
    /// <param name="metadata">Its recorded data.</param>
    /// <param name="type">The object type, which decides the tier.</param>
    /// <returns>Whether a window ran.</returns>
    private bool EditRandomArtifact(Point tile, uint uid, ArtifactMetadata metadata, Mp2ObjectType type)
    {
        ArtifactLevel level = type switch
        {
            Mp2ObjectType.RandomArtifact or Mp2ObjectType.ActionRandomArtifact => ArtifactLevel.AllNormal,
            Mp2ObjectType.RandomArtifactTreasure or Mp2ObjectType.ActionRandomArtifactTreasure => ArtifactLevel.Treasure,
            Mp2ObjectType.RandomArtifactMinor or Mp2ObjectType.ActionRandomArtifactMinor => ArtifactLevel.Minor,
            _ => ArtifactLevel.Major,
        };

        List<int> allowed = ArtifactSelectDialog.BuildArtifactList(Map.Info.Version, level: level);

        if (_host.TryCreateMultiArtifactSelect(allowed, metadata.Selected) is not { } picker ||
            RunModal is not { } runModal)
        {
            GameLog.Warn($"The random artifact at ({tile.X}; {tile.Y}) needs the artifact picker, and it cannot be built.");

            return true;
        }

        runModal(picker);

        LastArtifactEditorTile = tile;

        if (!picker.Accepted)
        {
            return true;
        }

        List<int> chosen = [.. picker.SelectedArtifacts];

        if (chosen.SequenceEqual(metadata.Selected))
        {
            return true;
        }

        MetadataMapAction<ArtifactMetadata> action = new(_metadata.Artifacts, entry => entry.Clone());
        metadata.Selected.Clear();
        metadata.Selected.AddRange(chosen);
        _metadata.Artifacts[uid] = metadata;
        CommitMetadataChange(action);

        GameLog.Info($"The random artifact at ({tile.X}; {tile.Y}) may now be {chosen.Count} kind(s).");

        return true;
    }

    /// <summary>
    /// The artifact an <c>OBJNARTI</c> tile stands for, read back from its sprite frame.
    /// </summary>
    /// <param name="info">The tile.</param>
    /// <returns>The artifact, or <see cref="ArtifactType.Unknown"/> when the sprite names none.</returns>
    private ArtifactType ArtifactOf(Mp2TileInfo info)
    {
        // 精灵帧是 2 * id - 1（OBJNARTI 每件神器占两帧，前面那帧是影子），所以反算见 ArtifactMapSprites。
        if (!info.HasGroundObject || info.GroundIcnType != Mp2ObjectIcnType.ObjnArti)
        {
            return ArtifactType.Unknown;
        }

        return ArtifactMapSprites.FromMapIndex(info.BottomIcnImageIndex, Map.Info.Version,
            Map.Info.Version != MapVersion.SuccessionWars);
    }

    /// <summary>The tile whose artifact editor ran last, or <see langword="null"/> when none did.</summary>
    public Point? LastArtifactEditorTile { get; private set; }

    /// <summary>
    /// Reads the tile an object editor is about to look at, refusing everything outside the map.
    /// </summary>
    /// <param name="tile">Tile the author clicked.</param>
    /// <param name="info">The tile's content.</param>
    /// <returns><see langword="false"/> for a point outside the map.</returns>
    private bool TryReadEditableTile(Point tile, out Mp2TileInfo info)
    {
        info = default;

        if (tile.X < 0 || tile.X >= Map.Width || tile.Y < 0 || tile.Y >= Map.Height)
        {
            return false;
        }

        info = Map.Tile(tile.X, tile.Y);

        return true;
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
        // scene blits it into the frame at ViewportArea. The chrome follows the interface set of the settings.
        bool isEvilInterface = Settings.Get().IsEvilInterfaceEnabled;
        MapFrame mapFrame = new(frame.Size, MapFrame.Load(assets, isEvilInterface));

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
            EditorPanel.TryCreate(mapFrame.Panel, assets, isEvilInterface),
            HasInfoFrame(assets),
            QuickInfoArtwork.TryCreate(assets),
            new EditorSceneHost(assets, fonts),
            assets);
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

    /// <summary>Advances the edge scroll of the mouse and the object animation of the map.</summary>
    /// <remarks>
    /// The object popup of the right button counts down here: the reference waits for the button to be
    /// released inside <c>displayStandardPopupWindow()</c>, and the port cannot wait, so the popup has
    /// <see cref="ObjectPopupSeconds"/> at most.
    /// </remarks>
    public void Update(double elapsedSeconds)
    {
        ProcessEvents(LocalEvent.Current);

        UpdateEdgeScroll(elapsedSeconds);
        UpdateAnimation(elapsedSeconds);
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

        _editor.Map.Draw(_viewportPixels, animationPhase: _animationPhase, passability: PassabilityTileSet());
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

            // 参考在编辑模式下先看这一格是不是城堡入口（editor_interface.cpp:2232-2246），不是才描述物件。
            if (OpenCastleDetails(tile))
            {
                return false;
            }

            // 神殿与金字塔是法术选择窗（editor_interface.cpp:2484-2540），比说明框先判。
            if (OpenSpellChooser(tile))
            {
                return false;
            }

            // 标牌、怪物、资源各开一扇现成的窗（:2266-2362）。
            if (EditSign(tile) || EditMonsterCount(tile) || EditResourceCount(tile))
            {
                return false;
            }

            // 神器的四支（:2346-2437）：终极神器的半径与候选、法术卷轴的法术、随机神器的候选、单件的说明。
            if (EditArtifact(tile))
            {
                return false;
            }

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

            // :1461-1479 —— 面板上的右键只弹提示：不平移，也不弹物件窗。
            if (_screenCursor is { } panelPoint && _panel.Contains(panelPoint) && _panelView is not null &&
                ShowSystemHint(_panelView.HitTest(panelPoint, PanelState)))
            {
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
    /// Shows the hint of a system button on the right button, the <c>showStandardTextMessage</c> calls of
    /// <c>editor_interface_panel.cpp:1461-1479</c>.
    /// </summary>
    /// <param name="hit">What the point hits.</param>
    /// <returns>
    /// Whether the click was a system button, i.e. whether it was consumed; the other controls of the panel
    /// keep their old behaviour, which is the pan the right button owns in the port.
    /// </returns>
    private bool ShowSystemHint(EditorPanelHit hit)
    {
        if (hit.Kind != EditorPanelHitKind.System)
        {
            return false;
        }

        (string Title, string Body) hint = EditorPanel.SystemHint(EditorPanel.SystemAt(hit.Index));

        _host.ShowMessage(hint.Title, hint.Body);

        return true;
    }

    /// <summary>
    /// Runs one of the six system buttons of the panel. Undo and redo are the two the port answers itself;
    /// the magnifier opens the View World window, the file menu its own loop. The specifications button opens the
    /// page the port already has, the system button the options page.
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

            case EditorSystemButton.Magnify:
                OpenViewWorld();
                break;

            case EditorSystemButton.Specs:
                _host.RunMapSpecs(_edits, _frame.Palette);
                break;

            case EditorSystemButton.System:
                OpenEditorOptions();
                break;

            case EditorSystemButton.File:
                OpenFileMenu();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Opens the View World window over the map being edited, <c>eventViewWorld()</c>
    /// (<c>editor_interface.cpp:2183-2186</c>).
    /// </summary>
    /// <remarks>
    /// 与冒险地图的放大镜不同，参考的编辑器调的是整图预览（<c>ViewWorldMode::ViewAll</c>、<c>PlayerColor::NONE</c>），
    /// 不切换编辑区的缩放。窗口是模态，回来之后把缩放档记住；<see cref="EditorSceneHost.RunViewWorld"/> 在数据不全时
    /// 只留日志，编辑器不受影响。
    /// </remarks>
    private void OpenViewWorld()
    {
        if (_host.RunViewWorld(_editor, _viewport, _frame, _viewWorldZoom) is { } zoom)
        {
            _viewWorldZoom = zoom;
        }
    }

    /// <summary>
    /// Re-skins the editor for the interface set the settings now hold, the <c>reset()</c> behind the reference's
    /// <c>rebuildEditor( true )</c> (<c>editor_interface.cpp:1413-1470</c>, called from <c>editor_options.cpp:281-285</c>).
    /// </summary>
    /// <remarks>
    /// 参考的边框、雷达与控制面板都是每帧现取界面图，换主题只要重算布局；端口把边框
    /// （<see cref="MapFrame"/>）与面板（<see cref="EditorPanel"/>）的图缓存在场景里，所以要按新主题重读并换掉这两个
    /// 对象。布局本身不随主题变——两块边框都是 640x480、边宽都是 16——所以地图视图、滚动位置与雷达底图都不用动；
    /// <see cref="MapFrame.Viewport"/> 万一真的变了才换视口缓冲。
    /// </remarks>
    public void RebuildInterface()
    {
        bool isEvilInterface = Settings.Get().IsEvilInterfaceEnabled;
        MapFrame mapFrame = new(_frame.Size, MapFrame.Load(_assets, isEvilInterface));

        _mapFrame = mapFrame;
        _viewport = mapFrame.Viewport;
        _radar = mapFrame.Radar;
        _panel = mapFrame.Panel;

        if (_viewportPixels.Size != _viewport.Size)
        {
            _viewportPixels = new PixelBuffer(_viewport.Width, _viewport.Height);
        }

        _panelView = EditorPanel.TryCreate(mapFrame.Panel, _assets, isEvilInterface);

        GameLog.Info($"The editor interface was rebuilt for the {(isEvilInterface ? "evil" : "good")} graphics.");
    }

    /// <summary>
    /// The graphics page of the system options, <c>openGraphicsSettingsDialog( rebuildEditor )</c> of
    /// <c>editor_options.cpp:307-311</c>: run one turn of the page, apply the leaf, reopen until the author is done.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的循环（<c>dialog_graphics_settings.cpp:238-295</c>）每选完一项就回到 <c>Configuration</c> 重开同一页，关窗才是
    /// <c>Exit</c>；端口照抄：一轮一个模态，<see cref="GraphicsSettingsDialog.Apply"/> 落设置，再开一轮。
    /// </para>
    /// <para>
    /// <c>updateUI</c> 就是 <see cref="RebuildInterface"/>。参考只在改分辨率后调它（缩放变化只对主菜单重画，
    /// <c>:274-277</c>），所以模式、缩放、垂直同步、系统信息四项都不重画整套界面。
    /// </para>
    /// </remarks>
    /// <returns>Whether at least one setting changed, the reference's <c>saveConfiguration</c>.</returns>
    public bool OpenGraphicsSettings()
    {
        bool changed = false;

        while (_host.TryCreateGraphicsSettingsDialog() is { } page)
        {
            GraphicsSettingsOption option = _host.RunGraphicsSettingsDialog(page);

            if (option == GraphicsSettingsOption.Exit)
            {
                break;
            }

            if (GraphicsSettingsDialog.Apply(page, option, RebuildInterface, _host.SelectResolution))
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// The hot keys page of the system options, <c>fheroes2::openHotkeysDialog()</c> of
    /// <c>editor_options.cpp:317-321</c>.
    /// </summary>
    /// <remarks>
    /// 参考这一页既没有返回值也没有 <c>saveConfiguration</c>——改键当场写进热键表，关窗就回选项页。端口照抄。
    /// </remarks>
    public void OpenHotKeys()
    {
        if (_host.TryCreateHotKeysDialog() is { } page)
        {
            _host.RunHotKeysDialog(page);
        }
    }

    /// <summary>
    /// The audio page of the system options, <c>Dialog::openAudioSettingsDialog( false )</c> of
    /// <c>editor_options.cpp:313-316</c>.
    /// </summary>
    /// <remarks>
    /// 参考这一页是整窗阻塞的循环（<c>dialog_audio.cpp:149-259</c>），端口一轮一个模态；关窗时把页面自己的
    /// <c>IsDirty</c> 当 <c>saveConfiguration</c> 交回来，音量、音乐类型与 3D 音效都在页内当场写进设置，不用再 apply。
    /// </remarks>
    /// <returns>Whether at least one setting changed, the reference's <c>saveConfiguration</c>.</returns>
    public bool OpenAudioSettings()
    {
        if (_host.TryCreateAudioSettingsDialog() is not { } page)
        {
            return false;
        }

        return _host.RunAudioSettingsDialog(page);
    }

    /// <summary>
    /// The options page behind the system button, <c>openEditorSettings()</c> of
    /// <c>editor_options.cpp:235-378</c>: run one turn of the page, apply the action, reopen until the author is done.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的循环里 <c>action = DialogAction::Configuration</c> 表示「还要再开一层」，关窗才是
    /// <c>Close</c>；端口把「重开」直接写成循环。<c>saveConfiguration</c> 只在真改过设置时才置位，最后
    /// 交给宿主落盘（<see cref="SavePreferences"/> 由 <c>GameHost</c> 接到它自己的配置文件上）。
    /// </para>
    /// <para>
    /// 参考在这里还要 <c>redrawEditor</c> / <c>rebuildEditor</c>：通行性的叠层和对象动画都要重画地图，换界面风格
    /// 更是要把边框、雷达、控制面板整套换皮。界面风格的换皮已经在 <see cref="RebuildInterface"/> 里接上；动画开关
    /// 由 <see cref="UpdateAnimation"/> 每帧读设置，通行性叠层由 <see cref="PassabilityTileSet"/> 每帧现取，场景本来
    /// 每帧整幅重画，所以两个开关一翻就生效，不需要额外的重画位。
    /// </para>
    /// <para>
    /// 四个子页语言（<see cref="SwitchInterfaceLanguage"/>）、图形（<see cref="OpenGraphicsSettings"/>）、音频
    /// （<see cref="OpenAudioSettings"/>）与热键（<see cref="OpenHotKeys"/>）都已接上。
    /// </para>
    /// </remarks>
    public void OpenEditorOptions()
    {
        Settings settings = Settings.Get();
        bool saveConfiguration = false;

        while (true)
        {
            EditorOptionsAction action = _host.RunEditorOptionsDialog();

            switch (action)
            {
                case EditorOptionsAction.Animation:
                    settings.SetEditorAnimation(!settings.IsEditorAnimationEnabled);
                    saveConfiguration = true;
                    break;

                case EditorOptionsAction.Passability:
                    settings.SetEditorPassability(!settings.IsEditorPassabilityEnabled);
                    saveConfiguration = true;
                    break;

                case EditorOptionsAction.InterfaceType:
                    settings.SwitchToNextInterfaceType();
                    RebuildInterface();
                    saveConfiguration = true;
                    break;

                case EditorOptionsAction.CursorType:
                    settings.SetMonochromeCursor(!settings.IsMonochromeCursor);
                    saveConfiguration = true;
                    break;

                case EditorOptionsAction.ScrollSpeed:
                    // 参考这颗是循环换档：越界回最低档（editor_options.cpp:292-296）。
                    settings.SetScrollSpeed(((int)settings.ScrollSpeed + 1) %
                                            ((int)AdventureMapScrollSpeed.VeryFast + 1));
                    saveConfiguration = true;
                    break;

                case EditorOptionsAction.IncreaseScrollSpeed:
                    settings.SetScrollSpeed((int)settings.ScrollSpeed + 1);
                    saveConfiguration = true;
                    break;

                case EditorOptionsAction.DecreaseScrollSpeed:
                    settings.SetScrollSpeed((int)settings.ScrollSpeed - 1);
                    saveConfiguration = true;
                    break;

                case EditorOptionsAction.Language:
                    saveConfiguration |= SwitchInterfaceLanguage();
                    break;

                case EditorOptionsAction.Graphics:
                    saveConfiguration |= OpenGraphicsSettings();
                    break;

                case EditorOptionsAction.AudioSettings:
                    saveConfiguration |= OpenAudioSettings();
                    break;

                case EditorOptionsAction.HotKeys:
                    OpenHotKeys();
                    break;

                case EditorOptionsAction.Exit:
                case EditorOptionsAction.None:
                default:
                    if (saveConfiguration)
                    {
                        SavePreferences?.Invoke();
                    }

                    return;
            }
        }
    }

    /// <summary>
    /// The Language option of the page: pick the interface language, <c>editor_options.cpp:283-296</c>.
    /// </summary>
    /// <returns>Whether the language changed, i.e. whether the preferences have to be saved.</returns>
    /// <remarks>
    /// <para>
    /// 参考在有多个语言时开 <c>selectLanguage( supported, 当前, true )</c>，换了语言就 <c>clearWarningMessage</c> +
    /// <c>redrawEditor</c> + <c>saveConfiguration = true</c>；只剩英文时把语言钉成英文并弹一句提示。
    /// </para>
    /// <para>
    /// 端口没有 <c>clearWarningMessage</c>（编辑器还没有警告条），<c>redrawEditor</c> 也不需要——场景每帧整幅重画，
    /// 字形表按 <c>GameLanguage.Current</c> 现查（<see cref="FHeroes2.App.Fonts.FontGlyphTable"/>），所以换完语言下一帧
    /// 就是新译文，不用重载字体。编辑器的界面文案目前还是英文字面量，因此这一页改的是整个游戏的语言设置，编辑器自己的
    /// 字样要等本地化那一轮才跟着变。
    /// </para>
    /// </remarks>
    public bool SwitchInterfaceLanguage()
    {
        if (_assets is not { } assets || _fonts is not { } fonts)
        {
            GameLog.Warn("The language picker needs the game data and the fonts; it stays closed.");

            return false;
        }

        Settings settings = Settings.Get();
        IReadOnlyList<GameText> languages = LanguageCatalog.Select(GameText.LoadAll(assets.DataDirectory), fonts);
        int current = LanguageCatalog.IndexOfCulture(languages, settings.GameLanguage);

        if (languages.Count <= 1)
        {
            // 参考断言过这一支只剩英文（editor_options.cpp:287-293）：钉成英文，再解释一句为什么。
            bool changed = !string.Equals(settings.GameLanguage, languages[0].Culture, StringComparison.OrdinalIgnoreCase);

            settings.SetGameLanguage(languages[0].Culture);
            _host.ShowMessage(AttentionTitle, OnlyEnglishMessage);

            return changed;
        }

        int picked = _host.RunLanguagePicker(
            [.. languages.Select(language => language.NativeName)],
            current,
            languages[current].Get(MainMenuScene.LanguagePickerTitleId));

        if (picked < 0 || picked == current)
        {
            return false;
        }

        settings.SetGameLanguage(languages[picked].Culture);
        GameLog.Info($"The interface language is now '{languages[picked].NativeName}', {languages[picked].Culture}.");

        return true;
    }

    /// <summary>
    /// The file menu behind the file button, <c>EditorInterface::eventFileDialog()</c> of
    /// <c>editor_interface.cpp:2068-2181</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在循环里直接「存图 / 起新图 / 载图 / 开始地图 / 回主菜单 / 退出 / 自动测试」各开各的窗；端口这一轮只把
    /// 菜单本身跑通（画、点、取消、右键说明），七个去向都没接：
    /// </para>
    /// <list type="bullet">
    /// <item>存图要 <c>saveMapToFile()</c>（保存窗、文件名输入、覆盖确认、落盘）——<c>a7l.7.3.2</c>。</item>
    /// <item>新图/载图/主菜单/退出/开始地图/自动测试要各自的确认框与游戏模式切换，其中开始地图与自动测试还要
    /// <c>World</c>——<c>a7l.7.3.3</c>。</item>
    /// </list>
    /// <para>
    /// 现在这些按钮按下去只留一行日志：不静默，也不假装做成了。取消（含 ESC/回车）本来就没有后续动作，所以
    /// 那里的日志是多余的，直接返回。
    /// </para>
    /// </remarks>
    public void OpenFileMenu() => HandleFileMenuResult(_host.RunFileDialog());

    /// <summary>
    /// Acts on the button the file menu was left with, the switch inside the reference's <c>eventFileDialog()</c>
    /// loop (<c>editor_interface.cpp:2103-2153</c>).
    /// </summary>
    /// <param name="result">The button that was pressed.</param>
    public void HandleFileMenuResult(EditorFileResult result)
    {
        switch (result)
        {
            case EditorFileResult.Cancel:
            case EditorFileResult.None:
                return;

            case EditorFileResult.SaveMap:
                _ = SaveMapToFile();

                return;

            case EditorFileResult.NewMap:
                // editor_interface.cpp:2059-2067 的 eventNewMap()：问一句「真的要新建吗」，YES 才回主菜单。
                if (ConfirmLeave(string.Empty, NewMapQuestion))
                {
                    LeaveEditor?.Invoke(EditorLeaveRequest.NewMap);
                }

                return;

            case EditorFileResult.LoadMap:
                // editor_interface.cpp:2050-2057 的 eventLoadMap()。
                if (ConfirmLeave(string.Empty, LoadMapQuestion))
                {
                    LeaveEditor?.Invoke(EditorLeaveRequest.LoadMap);
                }

                return;

            case EditorFileResult.MainMenu:
                // editor_interface.cpp:2127-2136：问一句「回主菜单吗，未保存的改动会丢」，YES 才交出请求。
                if (ConfirmLeave(MainMenuTitle, MainMenuQuestion))
                {
                    LeaveEditor?.Invoke(EditorLeaveRequest.MainMenu);
                }

                return;

            case EditorFileResult.Quit:
                // editor_interface.cpp:1393-1401 的 processEditorExitEvent()。
                if (ConfirmLeave(QuitTitle, QuitQuestion))
                {
                    LeaveEditor?.Invoke(EditorLeaveRequest.Quit);
                }

                return;

            default:
                GameLog.Warn($"The editor file menu asked for {result}, and no action answers it yet.");
                break;
        }
    }

    /// <summary>Title of the quit confirmation, <c>_( "Quit" )</c> (<c>editor_interface.cpp:1396</c>).</summary>
    public const string QuitTitle = "Quit";

    /// <summary>
    /// Header of the sign text box, <c>_( "Input %{object} text:" )</c> (<c>editor_interface.cpp:2267</c>).
    /// </summary>
    public const string InputObjectTextHeader = "Input {0} text:";

    /// <summary>Longest sign text, <c>Maps::Map_Format::messageCharLimit</c>.</summary>
    public const int SignMessageCharLimit = 999;

    /// <summary>Header of a stack's count window, <c>_( "Set %{monster} Count:" )</c> (<c>:2308</c>).</summary>
    public const string SetMonsterCountHeader = "Set {0} Count:";
    /// <summary>Largest stack the editor lets an author set, the <c>500000</c> of <c>:2320</c>.</summary>
    public const int MaximumMonsterCount = 500_000;

    /// <summary>Header of a pile's count window, <c>_( "Set %{resource-type} Count:" )</c> (<c>:2352</c>).</summary>
    public const string SetResourceCountHeader = "Set {0} Count:";

    /// <summary>Largest pile the editor lets an author set, the <c>999999</c> of <c>:2357</c>.</summary>
    public const int MaximumResourceCount = 999_999;

    /// <summary>
    /// Header of the ultimate artifact's radius window, <c>_( "Set Random Ultimate Artifact Radius:" )</c>
    /// (<c>:2368</c>).
    /// </summary>
    public const string SetUltimateRadiusHeader = "Set Random Ultimate Artifact Radius:";

    /// <summary>Largest radius of the ultimate artifact, the <c>100</c> of <c>:2368</c>.</summary>
    public const int MaximumUltimateRadius = 100;

    /// <summary>
    /// The tile whose sign text, monster count or resource count window ran last, or <see langword="null"/> when
    /// none did; the tests and the log read it.
    /// </summary>
    public Point? LastObjectEditorTile { get; private set; }

    /// <summary>Body of the quit confirmation, <c>_( "Are you sure you want to quit? …" )</c>.</summary>
    public const string QuitQuestion = "Are you sure you want to quit? (Any unsaved changes to the current map will be lost.)";

    /// <summary>Title of the main menu confirmation, <c>_( "Main Menu" )</c> (<c>:2129</c>).</summary>
    public const string MainMenuTitle = "Main Menu";

    /// <summary>Body of the main menu confirmation, <c>_( "Do you wish to return to the game's Main Menu? …" )</c>.</summary>
    public const string MainMenuQuestion =
        "Do you wish to return to the game's Main Menu? (Any unsaved changes to the current map will be lost.)";

    /// <summary>Body of the new map confirmation, <c>editor_interface.cpp:2062</c>; the reference shows no title.</summary>
    public const string NewMapQuestion =
        "Are you sure you want to create a new map? (Any unsaved changes to the current map will be lost.)";

    /// <summary>Body of the load map confirmation, <c>editor_interface.cpp:2053</c>; the reference shows no title.</summary>
    public const string LoadMapQuestion =
        "Are you sure you want to load a new map? (Any unsaved changes to the current map will be lost.)";

    /// <summary>
    /// Asks a YES/NO question about leaving the editor, the <c>showStandardTextMessage( …, Dialog::YES | Dialog::NO )</c>
    /// pair of <c>:1393-1401</c> and <c>:2127-2136</c>.
    /// </summary>
    /// <remarks>
    /// 没有帧泵时答 NO：参考在没有显示设备时也开不出这个框，宁可让作者留在编辑器里，也不要悄悄丢掉改动。
    /// </remarks>
    /// <param name="title">Title of the box.</param>
    /// <param name="body">The question.</param>
    /// <returns>Whether the author said YES.</returns>
    private bool ConfirmLeave(string title, string body)
    {
        if (RunModal is not { } runModal ||
            _host.TryCreateMessageBox(title, body, DialogButton.Yes | DialogButton.No) is not { } box)
        {
            GameLog.Warn($"The editor cannot ask \"{body}\", so it stays open.");

            return false;
        }

        runModal(box);

        return box.Answer == DialogButton.Yes;
    }

    /// <summary>
    /// Saves the map through the save window, <c>EditorInterface::saveMapToFile()</c>
    /// (<c>editor_interface.cpp:3204-3266</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的顺序：确认数据目录与 <c>maps</c> 子目录可写 → 开保存窗（取消就返回）→ 拼出 <c>目录/文件名.mp2</c> →
    /// 文件已经存在就先问一句「真的要覆盖吗」，答 NO 就回去重开窗 → 写盘 → 记一条「Map saved to: …」。
    /// 端口照抄这个循环，只是写盘交给 <see cref="MapEditSession.SaveAs"/>，它顺手把「已保存」的基线也更新了。
    /// </para>
    /// <para>
    /// 地图名改了就按参考写回头部（<c>_mapFormat.name = mapName</c>）：<see cref="MapEditSession.ApplyMapInfo"/>
    /// 会置上「头部变了」，保存时才会把名字一起写回文件。
    /// </para>
    /// <para>
    /// 保存目录默认是 <see cref="GameDataDirectory"/> 下的 <c>maps</c>，用例可以先用
    /// <see cref="SaveMapDirectory"/> 换成临时目录。
    /// </para>
    /// </remarks>
    /// <returns>Whether a file was written.</returns>
    public bool SaveMapToFile()
    {
        string directory = ResolveSaveMapDirectory();

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowError(EditorSaveMapWindow.DirectoryErrorMessage);
            GameLog.Warn($"The map directory '{directory}' could not be created: {exception.Message}");

            return false;
        }

        string fileName = Path.GetFileNameWithoutExtension(_edits.SavePath ?? string.Empty);
        string mapName = Map.Info.Name;

        while (true)
        {
            if (_host.RunSaveMapWindow(directory, fileName, mapName) is not { } modal ||
                modal.Outcome != EditorSaveMapOutcome.Accepted)
            {
                return false;
            }

            fileName = modal.Window.FileName;
            mapName = modal.Window.MapName;

            if (EditorSaveMapWindow.SanitizeFileName(fileName).Length == 0)
            {
                continue;
            }

            string path = EditorSaveMapWindow.ComposeFilePath(directory, fileName);

            if (File.Exists(path) && !ConfirmOverwrite())
            {
                continue;
            }

            if (mapName != Map.Info.Name)
            {
                _edits.ApplyMapInfo(Map.Info with { Name = mapName });
            }

            // 参考的 Maps::Map_Format::saveMap() 写的就是这条路径；端口从同一个目录直接写 mp2。
            if (!_edits.SaveAs(path, out string? error))
            {
                ShowError(EditorSaveMapWindow.SaveFailedMessage);
                GameLog.Warn($"The map could not be saved to '{path}': {error}");

                return false;
            }

            GameLog.Info($"Map saved to: {path}");

            return true;
        }
    }

    /// <summary>
    /// The directory the save window lists and writes into: <see cref="SaveMapDirectory"/> when set, otherwise the
    /// <c>maps</c> folder of the writable data directory (<c>System::GetDataDirectory( "fheroes2" ) + "maps"</c>).
    /// </summary>
    private string ResolveSaveMapDirectory() =>
        string.IsNullOrWhiteSpace(SaveMapDirectory)
            ? Path.Combine(GameDataDirectory.Get("fheroes2"), "maps")
            : SaveMapDirectory;

    /// <summary>Asks whether an existing map may be overwritten, <c>editor_interface.cpp:3247-3250</c>.</summary>
    /// <returns>Whether the author said YES.</returns>
    private bool ConfirmOverwrite()
    {
        if (RunModal is not { } runModal || _host.TryCreateMessageBox(string.Empty,
                EditorSaveMapWindow.OverwriteMessage, DialogButton.Yes | DialogButton.No) is not { } box)
        {
            // 没有帧泵就当成「不覆盖」，与参考在没有窗口时只有 OK 一样保守：宁可回去改名字。
            return false;
        }

        runModal(box);

        return box.Answer == DialogButton.Yes;
    }

    /// <summary>Shows a one-button error box, the <c>showStandardTextMessage( _( "Error" ), … )</c> of the reference.</summary>
    /// <param name="body">The message.</param>
    private void ShowError(string body)
    {
        if (RunModal is not { } runModal || _host.TryCreateMessageBox(EditorSceneHost.ErrorTitle, body) is not { } box)
        {
            return;
        }

        runModal(box);
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

    /// <summary>
    /// Advances the object animation of the map, the <c>MAPS_DELAY</c> block of
    /// <c>editor_interface.cpp:2017-2024</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考每过一次 <c>validateAnimationDelay( MAPS_DELAY )</c> 才推进一步，并且只在
    /// <c>Settings::isEditorAnimationEnabled()</c> 为真时推进；标记重画的 <c>_redraw |= REDRAW_GAMEAREA</c> 在端口不需要，
    /// 场景每帧整幅重画。关掉动画时索引停在原处而不是归零，与参考一致。
    /// </para>
    /// <para>
    /// 参考这一支还有 <c>startColorCycling</c> / <c>stopColorCycling</c>：水面与城堡火焰靠调色板轮转动起来。端口没有轮转，
    /// 合成表把轮转色并进了静态槽（<c>PaletteId.CyclingToStatic</c>），所以这里只剩帧索引这一半。
    /// </para>
    /// </remarks>
    private void UpdateAnimation(double elapsedSeconds)
    {
        if (!Settings.Get().IsEditorAnimationEnabled)
        {
            return;
        }

        _animationElapsed += elapsedSeconds;

        if (_animationElapsed < AnimationInterval)
        {
            return;
        }

        // One step per delay passed, capped like the edge scroll so a stalled frame cannot skip a whole cycle.
        int steps = (int)Math.Min(_animationElapsed / AnimationInterval, MaxAnimationStepsPerUpdate);

        _animationElapsed -= steps * AnimationInterval;
        _animationPhase += (uint)steps;
    }

    /// <summary>
    /// The passability of the tiles when <c>Settings::isEditorPassabilityEnabled()</c> asks for the overlay,
    /// <see langword="null"/> otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的编辑器直接读自己那个 <c>World</c> 的地块方向；端口的地块集是从 <see cref="Mp2Map"/> 现算的，所以第一次打开
    /// 叠层时整张算一遍，之后每次编辑只补变过的格子（<see cref="MapTileSet.Refresh"/>）。
    /// </para>
    /// <para>
    /// 地图换了一张新对象就是编辑过：<see cref="Mp2Map"/> 是 record，<see cref="MapEditSession"/> 每落一笔都换出一张新的。
    /// 逐格比一遍比整张重建便宜得多，也比给每条编辑路径都挂回调稳妥——撤销、重做同样只是换了张地图。
    /// </para>
    /// </remarks>
    /// <returns>The tile set to hand to the view, or <see langword="null"/> to draw the plain map.</returns>
    private MapTileSet? PassabilityTileSet()
    {
        if (!Settings.Get().IsEditorPassabilityEnabled)
        {
            return null;
        }

        Mp2Map map = Map;

        if (_passability is { } set && _passabilitySource is { } source && ReferenceEquals(source, map))
        {
            return set;
        }

        if (_passability is not { } tileSet || tileSet.Geometry is not { } geometry
                                               || geometry.Width != map.Width || geometry.Height != map.Height)
        {
            _passability = MapTileSet.Create(map);
            _passabilitySource = map;

            return _passability;
        }

        for (int index = 0; index < map.Tiles.Count; ++index)
        {
            if (!map.Tiles[index].Equals(_passabilitySource!.Tiles[index]))
            {
                tileSet.Refresh(map, index);
            }
        }

        _passabilitySource = map;

        return tileSet;
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
