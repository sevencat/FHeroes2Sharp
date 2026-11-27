using System.Diagnostics.CodeAnalysis;
using FHeroes2.App.Fonts;
using FHeroes2.App.Localization;
using FHeroes2.App.Modals;
using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Audio;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Editor;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;
using ZoomLevel = FHeroes2.Game.Kingdom.ZoomLevel;

namespace FHeroes2.App.Scenes;

/// <summary>
/// What the editor scene asks of the host: the frame pump its windows run on, and the windows themselves
/// built out of the archives.
/// </summary>
/// <remarks>
/// <para>
/// 参考的编辑器直接伸手去拿界面：<c>showStandardTextMessage</c>、<c>openMapSpecificationsDialog</c>、
/// <c>eventFileDialog</c> 都是阻塞调用（<c>editor_interface_panel.cpp:1461-1479</c>、<c>editor_interface.cpp:2068</c>）。
/// 端口一条帧循环，所以这些窗口借宿主的 <see cref="RunModal"/> 进内层循环，关掉之后外层接着跑——与
/// <see cref="AdventureHeroActionHost"/> 同一条缝。
/// </para>
/// <para>
/// <b>从哪来</b>：<see cref="RunModal"/> 由 <c>GameHost</c> 在造好编辑器场景之后接上（和
/// <c>AdventureMapScene.ExitEvent</c> 一样的位置）。没接上时提示只在日志里留一行，不弹窗。
/// </para>
/// <para>
/// 造窗口的方法留在这里而不是场景里：它们要的字体、按钮与页面美术都是这里攒下的两样（<see cref="AssetBundle"/>
/// 与 <see cref="GameFonts"/>）。文件菜单与系统菜单随后面几个子任务加进同样的位置。
/// </para>
/// </remarks>
internal sealed class EditorSceneHost
{
    /// <summary>Title of the box the host shows when a window reports a failure.</summary>
    public const string ErrorTitle = "Error";

    /// <summary>
    /// First frame of <c>REQUESTS</c> that the split window uses as a slot icon, <c>spriteIconIdx = 21</c>
    /// (<c>dialog_selectcount.cpp:512</c>).
    /// </summary>
    public const int FirstSplitIconFrame = 21;

    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;

    private SystemButtonArtwork? _buttons;
    private EditorListArtwork? _listArtwork;
    private EditorDailyEventSpecArtwork? _dailyEventArtwork;
    private ItemSelectionWindowArtwork? _itemSelectionArtwork;
    private IcnViewWorldSpriteSource? _sprites;
    private VirtualKeyboardArtwork? _keyboardArtwork;
    private IReadOnlyList<Sprite>? _spellIcons;
    private Mons32Sprites? _monsters;
    private ArtifactsBarArtwork? _artifactIcons;
    private Sprite? _monsterPlate;
    private bool _monsterPlateLoaded;

    /// <summary>Collects what a window of the editor needs from the archives.</summary>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="fonts">The fonts of the windows; <see langword="null"/> when the data is missing.</param>
    public EditorSceneHost(AssetBundle? assets, GameFonts? fonts)
    {
        _assets = assets;
        _fonts = fonts;
        SelectResolution = RunResolutionPicker;
    }

    /// <summary>
    /// Runs a modal window to its end, <c>GameHost.RunModal</c>; <see langword="null"/> while the host has not
    /// wired it up.
    /// </summary>
    public Action<IModalScreen>? RunModal { get; set; }

    /// <summary>Puts an OK-only box to the player and waits for it, <c>fheroes2::showStandardTextMessage( …, Dialog::OK )</c>.</summary>
    /// <param name="header">Title of the box; empty for none.</param>
    /// <param name="body">The message.</param>
    /// <returns>Whether the box was run; <see langword="false"/> when the fonts, the buttons or the frame pump are missing.</returns>
    public bool ShowMessage(string header, string body)
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn($"The box \"{body}\" has no frame pump (RunModal); it is skipped.");

            return false;
        }

        if (TryCreateMessageBox(header, body) is not { } box)
        {
            return false;
        }

        runModal(box);

        return true;
    }

    /// <summary>
    /// Runs the map specifications window to its end and writes the result back,
    /// <c>openMapSpecificationsDialog()</c> of the reference.
    /// </summary>
    /// <param name="session">The edit session the accepted changes are committed to.</param>
    /// <param name="gamePalette">Palette the condition panels tint their town and hero rows with.</param>
    /// <remarks>
    /// 关窗后的两步照参考：确定就把 <c>MapSpecsState</c> 写回地图（<c>commitMapChange</c>），失败弹错误；取消就什么
    /// 都不写。规格窗自己有内层窗（消息框、文本输入、后面的传闻/事件/语言），它们由 <see cref="EditorMapSpecsModal"/>
    /// 自己摞起来——不能在这里递归 <see cref="RunModal"/>，那会在已经开着窗口的时候再压一层。
    /// </remarks>
    public void RunMapSpecs(MapEditSession session, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The map specifications window has no frame pump (RunModal); it is skipped.");

            return;
        }

        bool evil = Settings.Get().IsEvilInterfaceEnabled;

        if (MapSpecsPageArtwork.TryCreate(evil) is not { } artwork)
        {
            GameLog.Warn("The map specifications window needs the page artwork, and it is missing.");

            return;
        }

        var window = EditorMapSpecsWindow.Create(artwork, new MapSpecsState(session.Map), _fonts?.White, gamePalette);

        var modal = new EditorMapSpecsModal(window, new EditorMapSpecsInput(window), session, this);

        runModal(modal);

        if (modal.Error is { } error)
        {
            ShowMessage(ErrorTitle, error);
        }
    }

    /// <summary>
    /// Runs the View World window over the map being edited, <c>eventViewWorld()</c> of
    /// <c>editor_interface.cpp:2183-2186</c>.
    /// </summary>
    /// <param name="view">The map view of the editor; only its map, tile set and sprite sources are read.</param>
    /// <param name="viewport">The rectangle the map is drawn into, the window's <c>GameArea::GetROI()</c>.</param>
    /// <param name="frame">The screen the window is painted on.</param>
    /// <param name="zoomLevel">The zoom level the previous visit ended on.</param>
    /// <returns>
    /// The zoom level the window was left at, to remember for the next visit; <see langword="null"/> when the window
    /// could not be opened.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 参考在编辑器里调 <c>ViewWorld::ViewWorldWindow( PlayerColor::NONE, ViewWorldMode::ViewAll, *this )</c>：编辑器不按
    /// 玩家裁雾，看的是整张图。这里照做，只在前面多一步——把编辑中的地图装成世界
    /// （<see cref="World.CreatePreview"/>），窗口、雷达与图标层都要它。
    /// </para>
    /// <para>
    /// 世界与缓存都只活到窗口关掉：它们不写 <see cref="World.Current"/>，也不回写地图，所以关掉之后编辑器手上的
    /// <c>Mp2Map</c> 一格都没变。
    /// </para>
    /// <para>
    /// 预览开不出来时只记一行日志：编辑中的地图可能还没配上完整的物件表，这时没有理由把编辑器一起打断。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="view"/> or <paramref name="frame"/> is <see langword="null"/>.</exception>
    public ZoomLevel? RunViewWorld(MapEditorView view, Rect viewport, ScreenBuffer frame, ZoomLevel zoomLevel)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(frame);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The View World window has no frame pump (RunModal); it is skipped.");

            return null;
        }

        if (_assets is not { } assets)
        {
            GameLog.Warn("The View World window needs the game data, and it is missing.");

            return null;
        }

        AdventureMapView map = view.Map;
        World world;

        try
        {
            world = World.CreatePreview(map.Map);
        }
        catch (ArgumentException error)
        {
            GameLog.Warn($"The View World window cannot be built: {error.Message}");

            return null;
        }

        _sprites ??= new IcnViewWorldSpriteSource(assets);

        ViewWorldMapCache cache = new ViewWorldMapBuilder(
            map.Map,
            map.Tiles,
            world,
            map.Objects,
            map.Monsters,
            map.Boats,
            map.FogSprites).Build(ViewWorldMode.ViewAll);

        ViewWorldRenderer.DrawObjectsIcons(PlayerColor.None, ViewWorldMode.ViewAll, cache, world, _sprites);

        var window = new ViewWorldWindow(
            world,
            ViewWorldMode.ViewAll,
            PlayerColor.None,
            zoomLevel,
            viewport,
            map.VisibleTileArea,
            cache,
            frame.Size.Width,
            frame.Size.Height,
            _sprites,
            Settings.Get().IsEvilInterfaceEnabled);

        var modal = new ViewWorldModal(new ViewWorldOverlay(window, frame));

        runModal(modal);

        return modal.Result;
    }

    /// <summary>
    /// Builds the castle settings window of the editor, <c>openCastleDialog()</c> of
    /// <c>editor_castle_details_window.cpp:790</c>.
    /// </summary>
    /// <param name="state">The castle the window shows and edits.</param>
    /// <param name="gamePalette">Palette in video values, needed to blur the buildings of a non default castle.</param>
    /// <returns>
    /// The state machine over the loaded window, or <see langword="null"/> when the archives, the fonts or the artwork
    /// are missing.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CastleDetailsInput? TryCreateCastleDetails(CastleDetailsState state, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (_assets is not { } assets)
        {
            GameLog.Warn("The castle settings window needs the game data, and it is missing.");

            return null;
        }

        if (_fonts is null)
        {
            GameLog.Warn("The castle settings window needs the game fonts, and they are missing.");

            return null;
        }

        if (CastleDetailsView.TryCreate(assets, state, gamePalette, Settings.Get().IsEvilInterfaceEnabled) is not
            { } view)
        {
            GameLog.Warn("The castle settings window needs its artwork, and it is missing.");

            return null;
        }

        return new CastleDetailsInput(view);
    }

    /// <summary>
    /// Runs the castle settings window to its end, <c>openCastleDialog()</c>.
    /// </summary>
    /// <param name="state">The castle the window shows and edits.</param>
    /// <param name="gamePalette">Palette in video values, needed to blur the buildings of a non default castle.</param>
    /// <returns>
    /// How the window was left; <see cref="CastleDetailsOutcome.Accepted"/> means the caller writes
    /// <paramref name="state"/> back into the map, <see cref="CastleDetailsOutcome.None"/> means it never opened.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public CastleDetailsOutcome RunCastleDetails(CastleDetailsState state, Palette gamePalette)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gamePalette);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The castle settings window has no frame pump (RunModal); it is skipped.");

            return CastleDetailsOutcome.None;
        }

        if (TryCreateCastleDetails(state, gamePalette) is not { } input)
        {
            return CastleDetailsOutcome.None;
        }

        var modal = new EditorCastleDetailsModal(input, this);

        runModal(modal);

        return modal.Outcome;
    }

    /// <summary>
    /// Runs one turn of the editor's options page, <c>openEditorOptionsDialog()</c> of the reference.
    /// </summary>
    /// <returns>
    /// The action the page was left with; <see cref="EditorOptionsAction.Exit"/> when the author closed it or when the
    /// page could not be built at all (nothing else can be done with a missing window).
    /// </returns>
    /// <remarks>
    /// 端口只跑一轮：参考的 <c>openEditorOptionsDialog()</c> 是个阻塞循环，动作不等于 <c>Configuration</c> 就
    /// <c>return action</c>；「按动作改设置、再重开一层」在 <c>openEditorSettings()</c> 里
    /// （<c>editor_options.cpp:235-378</c>），那是场景的活——通行性叠层与界面换皮都要重画地图。
    /// </remarks>
    public EditorOptionsAction RunEditorOptionsDialog()
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The editor options page has no frame pump (RunModal); it is skipped.");

            return EditorOptionsAction.Exit;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The editor options page needs the OK button, and it is missing.");

            return EditorOptionsAction.Exit;
        }

        if (EditorOptionsArtwork.TryCreate(buttons) is not { } artwork)
        {
            GameLog.Warn("The editor options page needs its artwork, and it is missing.");

            return EditorOptionsAction.Exit;
        }

        if (EditorOptionsDialog.TryCreate(ScreenBuffer.DefaultSize, artwork) is not { } dialog)
        {
            GameLog.Warn("The editor options page is incomplete, and it is skipped.");

            return EditorOptionsAction.Exit;
        }

        var modal = new EditorOptionsModal(dialog, this);

        runModal(modal);

        // 模态没拿到动作就结束只可能是被外部收掉了，当关窗处理。
        return modal.Action;
    }

    /// <summary>
    /// Runs the interface language picker to its end, <c>fheroes2::selectLanguage( ..., isGameLanguage = true )</c>.
    /// </summary>
    /// <param name="names">The languages as the rows show them, English first.</param>
    /// <param name="selectedIndex">Index of the row to highlight, the language in use.</param>
    /// <param name="title">The line above the list, already in the language in use.</param>
    /// <returns>The index the author picked, or -1 when the window was cancelled or could not be built.</returns>
    /// <remarks>
    /// 与主菜单的语言选择器同一个 <see cref="LanguageSelectionDialog"/>，只是拦在一层模态里：参考的
    /// <c>selectLanguage</c> 也是阻塞的，返回的是选中的语言，取消时原样返回传进去的那个。
    /// </remarks>
    public int RunLanguagePicker(IReadOnlyList<string> names, int selectedIndex, string title)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(title);

        if (names.Count == 0)
        {
            GameLog.Warn("The language picker needs at least one installed language, and there is none.");

            return -1;
        }

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The language picker has no frame pump (RunModal); it is skipped.");

            return -1;
        }

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The language picker needs the game fonts, and they are missing.");

            return -1;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The language picker needs the standard buttons, and they are missing.");

            return -1;
        }

        int contentHeight = LanguageSelectionDialog.GetRequiredHeight(fonts.Normal.LineHeight, names.Count);
        StandardWindow window = StandardWindow.CreateCentered(
            new Size(LanguageSelectionDialog.ContentWidth, contentHeight), true, ScreenBuffer.DefaultSize);

        // SYSTEM.ICN 里 OKAY 与 CANCEL 都在，不像主菜单的启动图框还要看素材齐不齐。
        ButtonGroup group = ButtonGroup.CreateStandard(
            window.ActiveArea, DialogButton.Ok | DialogButton.Cancel, buttons);

        var dialog = new LanguageSelectionDialog(window, group, fonts.Yellow, fonts.Normal, fonts.Yellow, title,
            [.. names], Math.Clamp(selectedIndex, 0, names.Count - 1));

        var modal = new GameLanguagePickerModal(dialog);

        runModal(modal);

        return modal.Accepted ? modal.SelectedIndex : -1;
    }

    /// <summary>
    /// Opens the resolution chooser, <c>Dialog::SelectResolution()</c> of the graphics page; returns whether a new
    /// resolution was chosen.
    /// </summary>
    /// <remarks>
    /// 默认接到 <see cref="RunResolutionPicker"/>：参考这一行点下去就是阻塞的 <c>Dialog::SelectResolution()</c>，关窗后
    /// 图形页拿返回值决定要不要 <c>updateUI</c>。仍是可替换的缝，置 <see langword="null"/> 就退回「点了不变」
    /// （<see cref="GraphicsSettingsDialog.Apply"/> 收到 <see langword="null"/> 就返回不变）。
    /// </remarks>
    public Func<bool>? SelectResolution { get; set; }

    /// <summary>
    /// Builds the resolution chooser, <c>Dialog::SelectResolution()</c> of <c>dialog_graphics_settings.cpp:255-269</c>.
    /// </summary>
    /// <remarks>
    /// 美术与主菜单首运流程同一套：滚动条 <c>SCROLL</c> 与窗底 <c>ADVBORD</c> 的第 0 帧。行只留游戏尺寸等于帧缓冲的那些
    /// （<see cref="IDisplayControl.GetAvailableResolutions"/>，ADR-205），其余的 <see cref="IDisplayControl.SetResolution"/>
    /// 会拒绝；没有显示设备或一条可用行都没有时返回 <see langword="null"/>。
    /// </remarks>
    /// <returns>The chooser, or <see langword="null"/> when the artwork, the fonts or the display are missing.</returns>
    public ResolutionDialog? TryCreateResolutionDialog()
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The resolution chooser needs the game data, and it is missing.");

            return null;
        }

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The resolution chooser needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The resolution chooser needs the standard buttons, and they are missing.");

            return null;
        }

        if (LocalEvent.Current.Engine?.Display is not { } display)
        {
            GameLog.Warn("The resolution chooser needs a display to read the resolutions from, and there is none.");

            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Scroll)) is not { } scroll ||
            assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Advbord)) is not { } board)
        {
            GameLog.Warn("The resolution chooser needs SCROLL and ADVBORD, and one of them is missing.");

            return null;
        }

        Size frameSize = ScreenBuffer.DefaultSize;
        List<ResolutionInfo> available = [];

        foreach (ResolutionInfo resolution in display.GetAvailableResolutions())
        {
            if (resolution.GameSize == frameSize)
            {
                available.Add(resolution);
            }
        }

        if (available.Count == 0)
        {
            GameLog.Warn(
                $"The display offers no window for a {frameSize.Width}x{frameSize.Height} frame buffer; the " +
                "resolution chooser stays closed.");

            return null;
        }

        ResolutionArtwork artwork = new()
        {
            ScrollBarFrames = scroll.Sprites,
            ScrollBarBackground = board.Sprites[0],
            Buttons = buttons,
        };

        ResolutionInfo current = new(frameSize.Width, frameSize.Height, display.ScreenSize.Width,
            display.ScreenSize.Height);

        return ResolutionDialog.TryCreate(frameSize, artwork, fonts.Yellow, fonts.Normal, fonts.Yellow, available,
            current);
    }

    /// <summary>
    /// Runs the resolution chooser to its end and reports whether the window was switched,
    /// <c>Dialog::SelectResolution()</c>.
    /// </summary>
    /// <returns>Whether a new resolution was chosen and applied.</returns>
    public bool RunResolutionPicker()
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The resolution chooser has no frame pump (RunModal); it is skipped.");

            return false;
        }

        if (TryCreateResolutionDialog() is not { } page)
        {
            return false;
        }

        var modal = new ResolutionPickerModal(page, this, LocalEvent.Current.Engine?.Display);

        runModal(modal);

        return modal.IsFinished && modal.Accepted;
    }

    /// <summary>
    /// Builds the graphics page of the system options, <c>showConfigurationWindow()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// 美术来自四个 ICN：<c>SPANEL</c>（模式与垂直同步的图标）、<c>RESOLUTION_ICON</c>、<c>GAME_OPTION_ICON</c>（屏幕缩放）
    /// 与 <c>EMPTY_OPTION_ICON_BACKGROUND</c>（系统信息那行要往上写字）。窗口控制取自
    /// <see cref="LocalEvent.Current"/> 的引擎——没窗口时页面仍画得出来，只是全屏与分辨率不会真的生效，与
    /// <see cref="GraphicsSettingsDialog"/> 的文档一致。
    /// </remarks>
    /// <returns>The page, or <see langword="null"/> when the assets, the fonts or the frames are missing.</returns>
    public GraphicsSettingsDialog? TryCreateGraphicsSettingsDialog()
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The graphics settings page needs the game data, and it is missing.");

            return null;
        }

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The graphics settings page needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The graphics settings page needs the standard buttons, and they are missing.");

            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Spanel)) is not { } spanel)
        {
            GameLog.Warn("The graphics settings page needs SPANEL, and it is missing.");

            return null;
        }

        // RESOLUTION_ICON、GAME_OPTION_ICON 与 EMPTY_OPTION_ICON_BACKGROUND 都是新版资源，这套数据里没有；缺哪张就退回
        // 一块同尺寸的空图标（同 BattleSettingsWindow.ComposeOptionBackground），页面的字与状态照样画得出来。
        GraphicsSettingsArtwork artwork = new()
        {
            SpanelFrames = spanel.Sprites,
            ResolutionFrames = OptionFrames(IcnType.ResolutionIcon, 1),
            GameOptionFrames = OptionFrames(IcnType.GameOptionIcon, 4),
            EmptyOptionIconFrames = OptionFrames(IcnType.EmptyOptionIconBackground, 1),
            Buttons = buttons,
        };

        IDisplayControl? display = LocalEvent.Current.Engine?.Display;

        return GraphicsSettingsDialog.TryCreate(ScreenBuffer.DefaultSize, ScreenBuffer.DefaultSize, artwork,
            fonts.Yellow, fonts.Normal, display);
    }

    /// <summary>
    /// Reads an option icon, or its blank stand-in when the archives of this data set do not carry it.
    /// </summary>
    /// <param name="type">The icon file.</param>
    /// <param name="count">How many frames the caller indexes into.</param>
    /// <returns>The frames of the icon, or <paramref name="count"/> blank icons of the standard size.</returns>
    private IReadOnlyList<Sprite> OptionFrames(IcnType type, int count)
    {
        if (_assets?.TryReadIcn(IcnCatalog.GetFileName(type)) is { } file)
        {
            return file.Sprites;
        }

        GameLog.Warn($"'{IcnCatalog.GetFileName(type)}' is missing from the archives; the row gets a blank icon.");

        return [.. Enumerable.Range(0, count).Select(_ => new Sprite(OptionItem.OptionIconSize, OptionItem.OptionIconSize))];
    }

    /// <summary>
    /// Runs the graphics page to its end and reports the leaf the author chose, <c>showConfigurationWindow()</c>.
    /// </summary>
    /// <param name="dialog">The page to run, from <see cref="TryCreateGraphicsSettingsDialog"/>.</param>
    /// <returns>The leaf, or <see cref="GraphicsSettingsOption.Exit"/> when the page could not run at all.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> is <see langword="null"/>.</exception>
    public GraphicsSettingsOption RunGraphicsSettingsDialog(GraphicsSettingsDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The graphics settings page has no frame pump (RunModal); it is skipped.");

            return GraphicsSettingsOption.Exit;
        }

        var modal = new GraphicsSettingsModal(dialog, this);

        runModal(modal);

        return modal.IsFinished ? modal.Option : GraphicsSettingsOption.Exit;
    }

    /// <summary>
    /// The audio device the pages push their volumes to, <c>Audio::isValid()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// App 侧还没有混音器（只有 <c>AudioPlayer</c> 放音乐），所以默认是 <see langword="null"/>：音频页照常打开、音乐类型与
    /// 3D 音效照常翻，但两处音量点不动——与参考在没有声卡时的表现一致。接上混音器后由宿主赋值即可生效。
    /// </remarks>
    public AudioManager? Audio { get; set; }

    /// <summary>
    /// Builds the audio page of the system options, <c>Dialog::openAudioSettingsDialog( false )</c>.
    /// </summary>
    /// <remarks>
    /// 美术只有 <c>SPANEL</c> 一张（音量、音效、音乐类型与 3D 四处的图标）与共用按钮；字体给右键说明的标题与正文。
    /// 与图形页不同，这里没有窗口控制可接：音量交给 <see cref="Audio"/>，引擎模式与音频设备无关。
    /// </remarks>
    /// <returns>The page, or <see langword="null"/> when the assets, the fonts or the frames are missing.</returns>
    public AudioSettingsDialog? TryCreateAudioSettingsDialog()
    {
        if (_assets is null)
        {
            GameLog.Warn("The audio settings page needs the game data, and it is missing.");

            return null;
        }

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The audio settings page needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The audio settings page needs the standard buttons, and they are missing.");

            return null;
        }

        if (_assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Spanel)) is not { } spanel)
        {
            GameLog.Warn("The audio settings page needs SPANEL, and it is missing.");

            return null;
        }

        AudioSettingsArtwork artwork = new()
        {
            SpanelFrames = spanel.Sprites,
            Buttons = buttons,
        };

        AudioSettingsDialog? page = AudioSettingsDialog.TryCreate(ScreenBuffer.DefaultSize, artwork, fonts.Yellow,
            fonts.Normal, Audio);

        if (page is null)
        {
            GameLog.Warn("The audio settings artwork has too few frames; the page stays closed.");
        }

        return page;
    }

    /// <summary>
    /// Runs the audio page to its end and reports whether a setting changed, the <c>saveConfig</c> of
    /// <c>Dialog::openAudioSettingsDialog()</c>.
    /// </summary>
    /// <param name="dialog">The page to run, from <see cref="TryCreateAudioSettingsDialog"/>.</param>
    /// <returns>Whether a setting changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> is <see langword="null"/>.</exception>
    public bool RunAudioSettingsDialog(AudioSettingsDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The audio settings page has no frame pump (RunModal); it is skipped.");

            return false;
        }

        var modal = new AudioSettingsModal(dialog, this);

        runModal(modal);

        return modal.IsFinished && modal.IsDirty;
    }

    /// <summary>
    /// Builds the hot keys page of the system options, <c>fheroes2::openHotkeysDialog()</c>.
    /// </summary>
    /// <remarks>
    /// 美术是滚动条的 <c>SCROLL</c>、窗底 <c>ADVBORD</c> 的第 0 帧与共用按钮（同分辨率窗，<c>dialog_hotkeys.cpp:307-330</c>
    /// 读的也是这两份）；界面风格固定用好的一套，理由同 <c>MainMenuScene.CreateResolutionArtwork</c>。
    /// </remarks>
    /// <returns>The page, or <see langword="null"/> when the assets, the fonts or the artwork are missing.</returns>
    public HotKeysDialog? TryCreateHotKeysDialog()
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The hot keys page needs the game data, and it is missing.");

            return null;
        }

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The hot keys page needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The hot keys page needs the standard buttons, and they are missing.");

            return null;
        }

        if (assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Scroll)) is not { } scroll ||
            assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Advbord)) is not { } board)
        {
            GameLog.Warn("The hot keys page needs SCROLL and ADVBORD, and one of them is missing.");

            return null;
        }

        HotKeysArtwork artwork = new()
        {
            ScrollBarFrames = scroll.Sprites,
            ScrollBarBackground = board.Sprites[0],
            Buttons = buttons,
        };

        HotKeysDialog? page = HotKeysDialog.TryCreate(ScreenBuffer.DefaultSize, artwork, fonts.Yellow, fonts.Normal,
            fonts.Yellow);

        if (page is null)
        {
            GameLog.Warn("The hot keys artwork is incomplete or has no rows; the page stays closed.");
        }

        return page;
    }

    /// <summary>
    /// Runs the hot keys page to its end; the reference has no result to report.
    /// </summary>
    /// <param name="dialog">The page to run, from <see cref="TryCreateHotKeysDialog"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> is <see langword="null"/>.</exception>
    public void RunHotKeysDialog(HotKeysDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The hot keys page has no frame pump (RunModal); it is skipped.");

            return;
        }

        runModal(new HotKeysModal(dialog, this));
    }

    /// <summary>
    /// Runs the save map window to its end, <c>Editor::mapSaveSelectFile()</c>
    /// (<c>editor_save_map_window.cpp:181-487</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 列表内容由 <see cref="MapCatalog.ReadAll"/> 现扫目录得到（参考也是每次开窗重新扫），所以传进来的目录必须已经
    /// 存在；调用方负责先建目录并给出错误框。
    /// </para>
    /// <para>
    /// 窗口的四个请求（地图名、删除、虚拟键盘、右键说明）都在 <see cref="Modals.EditorSaveMapModal"/> 里分派，
    /// 这里只造窗与跑一轮。取消与「造不出来」都返回 <see langword="null"/>。
    /// </para>
    /// </remarks>
    /// <param name="directory">The <c>maps</c> directory to list and save into.</param>
    /// <param name="fileName">The file stem the field starts with; empty for the default one.</param>
    /// <param name="mapName">The map name shown above the list.</param>
    /// <returns>The window after it closed, or <see langword="null"/> with the reason in the log.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public EditorSaveMapModal? RunSaveMapWindow(string directory, string fileName, string mapName)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(mapName);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The save map window has no frame pump (RunModal); it is skipped.");

            return null;
        }

        if (_fonts?.Yellow is not { } headerFont || _fonts.White is not { } bodyFont)
        {
            GameLog.Warn("The save map window needs the game fonts, and they are missing.");

            return null;
        }

        EditorSaveMapArtwork? artwork = EditorSaveMapArtwork.TryCreate(Settings.Get().IsEvilInterfaceEnabled);

        if (artwork is null)
        {
            GameLog.Warn("The save map window needs the SCROLL, ADVBORD and SYSTEM frames, and they are missing.");

            return null;
        }

        IReadOnlyList<Mp2FileInfo> maps = MapCatalog.ReadAll([directory]);

        EditorSaveMapWindow window = EditorSaveMapWindow.Create(ScreenBuffer.DefaultSize, maps, fileName, mapName,
            artwork, headerFont, bodyFont);

        var modal = new EditorSaveMapModal(window, this);

        runModal(modal);

        return modal;
    }

    /// <summary>
    /// Runs the editor's file menu to its end, <c>eventFileDialog()</c> of the reference.
    /// </summary>
    /// <returns>
    /// The button the menu was left with; <see cref="EditorFileResult.Cancel"/> when it could not be built at all.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 七块牌子不是归档里的帧：参考在载入时用 <c>makeSymmetricBackgroundSprites</c> 把 NEW MAP 这些字样压进
    /// 底图（<c>game_assets.cpp:1579-1593</c>），端口没有生成物缓存，所以这里现做——这一步要字体，字体没加载就
    /// 干脆别开窗，否则 <c>Text</c> 会在测量时炸。
    /// </para>
    /// <para>
    /// 取消键照参考取 <c>BUTTON_SMALL_CANCEL</c> 的 0/1 帧（<c>editor_interface.cpp:2090</c>），好坏两套主题各一张，
    /// 由 <see cref="Settings.IsEvilInterfaceEnabled"/> 决定。
    /// </para>
    /// </remarks>
    public EditorFileResult RunFileDialog()
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The editor file menu has no frame pump (RunModal); it is skipped.");

            return EditorFileResult.Cancel;
        }

        if (_fonts is null)
        {
            GameLog.Warn("The editor file menu needs the fonts, and they are missing.");

            return EditorFileResult.Cancel;
        }

        bool evil = Settings.Get().IsEvilInterfaceEnabled;

        string cancelName = GameAssets.GetFileName(
            evil ? IcnType.ButtonSmallCancelEvil : IcnType.ButtonSmallCancelGood);

        if (GameAssets.GetImage(cancelName, 0) is not { } cancelReleased ||
            GameAssets.GetImage(cancelName, 1) is not { } cancelPressed)
        {
            GameLog.Warn($"The editor file menu needs \"{cancelName}\", and it is missing.");

            return EditorFileResult.Cancel;
        }

        List<Sprite> plates = ButtonFactory.MakeSymmetricBackgroundSprites(
            EditorFileDialog.ButtonLabels, evil, EditorFileDialog.MinimumButtonWidth);

        if (EditorFileDialog.TryCreate(ScreenBuffer.DefaultSize, plates, [cancelReleased, cancelPressed]) is not { } dialog)
        {
            GameLog.Warn("The editor file menu is incomplete, and it is skipped.");

            return EditorFileResult.Cancel;
        }

        var modal = new EditorFileModal(dialog, this);

        runModal(modal);

        return modal.Result == EditorFileResult.None ? EditorFileResult.Cancel : modal.Result;
    }

    /// <summary>
    /// Runs the spell selection window to its end, <c>openSpellSelectionWindow()</c> of the reference, and reopens
    /// it when the author asked for another level.
    /// </summary>
    /// <param name="title">The line above the scrolls; the callers pass the object or building name with a colon.</param>
    /// <param name="spellLevel">The level to start on, 1 to 5.</param>
    /// <param name="selectedSpells">The spells the caller holds; an empty list means every spell.</param>
    /// <param name="isMultiLevelSelectionEnabled">Whether the five level buttons are shown.</param>
    /// <param name="minimumEnabledSpells">How many spells have to stay enabled for OKAY to work.</param>
    /// <param name="pickDisabledSpells">Whether the caller wants the disabled spells instead of the enabled ones.</param>
    /// <returns>How the window was left and, when accepted, the spells of the last level shown.</returns>
    /// <remarks>
    /// 换等级的重开循环在参考里分两层：窗口自己 <c>break</c>（<c>editor_spell_selection.cpp:324-326</c>），调用方的
    /// <c>while</c> 拿着改过的 <c>spellLevel</c> 再来一次（<c>editor_castle_details_window.cpp:515</c>）。端口把两层
    /// 合成这个方法里的一个循环——调用方只看结果，不用自己拼 <c>while</c>。换等级时列表不算数，重开用的是调用方
    /// 原来那份（参考在 <c>break</c> 之后跳过了写回）。
    /// </remarks>
    public EditorSpellSelectionDialogResult RunSpellSelection(string title, int spellLevel,
        IReadOnlyList<SpellType> selectedSpells, bool isMultiLevelSelectionEnabled = false,
        int minimumEnabledSpells = 1, bool pickDisabledSpells = false)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(selectedSpells);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The spell selection window has no frame pump (RunModal); it is skipped.");

            return new EditorSpellSelectionDialogResult(false, spellLevel, []);
        }

        int level = spellLevel;

        while (true)
        {
            if (TryCreateSpellSelection(title, level, selectedSpells, isMultiLevelSelectionEnabled,
                    minimumEnabledSpells, pickDisabledSpells) is not { } modal)
            {
                return new EditorSpellSelectionDialogResult(false, level, []);
            }

            runModal(modal);

            // 换了等级就按新的等级重开一层；等级没变却说要换，说明状态机与窗口对不上，当取消收场。
            if (modal.Outcome == EditorSpellSelectionOutcome.LevelChanged && modal.Level != level)
            {
                level = modal.Level;

                continue;
            }

            return new EditorSpellSelectionDialogResult(modal.Outcome == EditorSpellSelectionOutcome.Accepted, level,
                modal.Spells);
        }
    }

    /// <summary>
    /// Builds one level of the spell selection window, the half of <c>Editor::openSpellSelectionWindow()</c> that only
    /// builds it (<c>editor_spell_selection.cpp:172-208</c>).
    /// </summary>
    /// <remarks>
    /// 已经在模态里跑的调用方（城堡详情的魔法行会子窗）不能再用 <see cref="RunSpellSelection"/>：那会把帧泵套成两层。
    /// 它们拿这个工厂自己压栈、自己判断 <see cref="EditorSpellSelectionOutcome.LevelChanged"/> 再开下一层。
    /// </remarks>
    /// <param name="title">Heading of the window, with the trailing colon the reference prints.</param>
    /// <param name="spellLevel">Spell level, 1 to 5.</param>
    /// <param name="selectedSpells">The list the window opens with: picked spells, or banned ones.</param>
    /// <param name="isMultiLevelSelectionEnabled">Whether the level buttons below the list are active.</param>
    /// <param name="minimumEnabledSpells">How many spells have to stay enabled, <c>getMaxSpellsCount()</c>.</param>
    /// <param name="pickDisabledSpells">Whether the window edits the banned list instead of the chosen one.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    public EditorSpellSelectionModal? TryCreateSpellSelection(string title, int spellLevel,
        IReadOnlyList<SpellType> selectedSpells, bool isMultiLevelSelectionEnabled = false,
        int minimumEnabledSpells = 1, bool pickDisabledSpells = false)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(selectedSpells);

        if (spellLevel is < 1 or > 5)
        {
            // 参考在这里 assert，调用方给错了等级就没有窗口可开。
            GameLog.Warn($"The spell selection window needs a level between 1 and 5, not {spellLevel}.");

            return null;
        }

        if (_fonts?.Yellow is not { } titleFont || _fonts.White is not { } spellFont)
        {
            GameLog.Warn("The spell selection window needs the game fonts, and they are missing.");

            return null;
        }

        if (EditorSpellSelectionArtwork.TryCreate(Settings.Get().IsEvilInterfaceEnabled) is not { } artwork)
        {
            GameLog.Warn("The spell selection window needs its artwork, and it is missing.");

            return null;
        }

        var window = EditorSpellSelectionWindow.Create(artwork, title, spellLevel, selectedSpells,
            isMultiLevelSelectionEnabled, minimumEnabledSpells, pickDisabledSpells, titleFont, spellFont);

        return new EditorSpellSelectionModal(window, new EditorSpellSelectionInput(window), this);
    }

    /// <summary>
    /// Builds the single-spell picker, <c>Dialog::selectSpell()</c> (<c>dialog_selectitems.cpp:1117-1141</c>).
    /// </summary>
    /// <param name="spellLevel">Spell level, 1 to 5, or <see langword="null"/> for every level.</param>
    /// <param name="currentSpell">The spell the row already holds, pre-selected in the list.</param>
    /// <param name="excludedSpells">Spells of the same level the picker must not offer.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    public SpellSelectModal? TryCreateSpellSelect(int? spellLevel, SpellType currentSpell,
        IReadOnlyList<int> excludedSpells)
    {
        ArgumentNullException.ThrowIfNull(excludedSpells);

        if (_fonts?.Yellow is not { } titleFont || _fonts.White is not { } spellFont)
        {
            GameLog.Warn("The spell picker needs the game fonts, and they are missing.");

            return null;
        }

        if (TryCreateItemSelectionArtwork() is not { } artwork)
        {
            GameLog.Warn("The spell picker needs the list artwork, and it is missing.");

            return null;
        }

        IReadOnlyList<Sprite> icons = SpellIcons();

        if (icons.Count == 0)
        {
            GameLog.Warn("The spell picker needs the spell icons, and they are missing.");

            return null;
        }

        SpellSelectionWindow? window = SpellSelectDialog.TryCreate((int)currentSpell, true,
            new HashSet<int>(excludedSpells), spellLevel ?? -1, icons, artwork, titleFont, spellFont);

        return window is null ? null : new SpellSelectModal(window);
    }

    /// <summary>
    /// Builds the mage guild spells sub-window of the castle settings,
    /// <c>mageGuildSpellsDialog()</c> (<c>editor_castle_details_window.cpp:383-605</c>).
    /// </summary>
    /// <param name="state">The castle being edited; the window writes back into its metadata.</param>
    /// <param name="dialogArea">The <c>dialogRoi</c> of the castle window.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    public MageGuildSpellsModal? TryCreateMageGuildSpells(CastleDetailsState state, Rect dialogArea)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_assets is not { } assets)
        {
            GameLog.Warn("The mage guild spells window needs the game data, and it is missing.");

            return null;
        }

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The mage guild spells window needs the game fonts, and they are missing.");

            return null;
        }

        if (MageGuildSpellsArtwork.TryCreate(assets, state.Race, Settings.Get().IsEvilInterfaceEnabled) is not
            { } artwork)
        {
            GameLog.Warn("The mage guild spells window needs its artwork, and it is missing.");

            return null;
        }

        var table = new MageGuildSpellTable(state.Race);
        table.Load(state.Metadata);

        var view = new MageGuildSpellsView(table, artwork, dialogArea, fonts.White);
        var input = new MageGuildSpellsInput(view, state.Metadata);

        return new MageGuildSpellsModal(view, input, this);
    }

    /// <summary>
    /// Builds the unit information window the army bar asks for, <c>Dialog::ArmyInfo()</c>
    /// (<c>dialog_armyinfo.cpp:518-712</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 军队条把三个按钮的标志（升级、解散、退出）带在窗口模型里，这里按标志去读对应的两帧按钮图；窗口的位置由
    /// <see cref="ArmyInfoLayout.Create"/> 按屏幕居中算，偏移量取 0——参考只有在军队条自带偏移时才不为 0。
    /// </para>
    /// <para>
    /// 参考画的是兵种战斗动画的空闲帧，端口的怪兽动画还没转，所以窗口没有单位精灵；等动画转完再把精灵传进来。
    /// </para>
    /// </remarks>
    /// <param name="dialog">The window's model, built by the army bar.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    public ArmyInfoModal? TryCreateArmyInfo(ArmyInfoDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The unit information window needs the game fonts, and they are missing.");

            return null;
        }

        bool isEvil = Settings.Get().IsEvilInterfaceEnabled;

        IReadOnlyList<Sprite>? sprites = TryReadSprites(isEvil ? IcnType.Viewarme : IcnType.Viewarmy);

        if (sprites is null)
        {
            GameLog.Warn("The unit information window needs its artwork, and it is missing.");

            return null;
        }

        DialogButtonArtwork? exitButton = TryReadButtonPair(isEvil
            ? IcnType.ButtonSmallExitEvil
            : IcnType.ButtonSmallExitGood);
        DialogButtonArtwork? upgradeButton = dialog.CanUpgrade
            ? TryReadButtonPair(isEvil ? IcnType.ButtonSmallUpgradeEvil : IcnType.ButtonSmallUpgradeGood)
            : null;
        DialogButtonArtwork? dismissButton = dialog.CanDismiss
            ? TryReadButtonPair(isEvil ? IcnType.ButtonSmallDismissEvil : IcnType.ButtonSmallDismissGood)
            : null;

        if (ArmyInfoArtwork.TryCreate(sprites, isEvil, exitButton, upgradeButton, dismissButton) is not { } artwork ||
            exitButton is not { } exit)
        {
            GameLog.Warn("The unit information window needs its buttons, and they are missing.");

            return null;
        }

        ArmyInfoLayout layout = ArmyInfoLayout.Create(
            new Rect(new Point(0, 0), ScreenBuffer.DefaultSize),
            new Size(artwork.Background.Width, artwork.Background.Height),
            new Size(artwork.Shadow.Width, artwork.Shadow.Height),
            artwork.GetShadowShift(),
            new Size(exit.Released.Width, exit.Released.Height),
            upgradeButton is { } upgrade ? new Size(upgrade.Released.Width, upgrade.Released.Height) : null,
            dismissButton is { } dismiss ? new Size(dismiss.Released.Width, dismiss.Released.Height) : null,
            isEvil,
            windowOffsetY: 0,
            fonts.White.LineHeight,
            fonts.SmallWhite.LineHeight);

        return new ArmyInfoModal(dialog, artwork, layout, fonts, this);
    }

    /// <summary>
    /// Builds the creature picker the army bar asks for, <c>Dialog::selectMonster()</c>
    /// (<c>dialog_selectitems.cpp:1204-1229</c>).
    /// </summary>
    /// <remarks>
    /// 行底那块 43x43 的板子是世界窗 <c>SWAPWIN</c> 第 0 帧的 (36, 267) 一块，与参考
    /// <c>renderMonsterOnBackground()</c> 同源；缺它就只画 <c>MONS32</c> 的怪兽（列表仍然能用）。
    /// 列表骨架、滚动条与两颗小按钮复用的是 <see cref="TryCreateItemSelectionArtwork"/> 那一套。
    /// </remarks>
    /// <param name="suggested">The creature the list opens on, or <see cref="MonsterType.Unknown"/> for the first row.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    public MonsterSelectModal? TryCreateMonsterSelect(MonsterType suggested)
    {
        if (_assets is not { } assets)
        {
            GameLog.Warn("The creature picker needs the game data, and it is missing.");

            return null;
        }

        if (_fonts?.Yellow is not { } titleFont || _fonts.White is not { } font)
        {
            GameLog.Warn("The creature picker needs the game fonts, and they are missing.");

            return null;
        }

        if (TryCreateItemSelectionArtwork() is not { } artwork)
        {
            GameLog.Warn("The creature picker needs the list artwork, and it is missing.");

            return null;
        }

        _monsters ??= Mons32Sprites.TryCreate(assets);

        if (_monsters is null)
        {
            GameLog.Warn("The creature picker needs MONS32, and it is missing.");

            return null;
        }

        MonsterSelectionWindow? window = MonsterSelectDialog.TryCreate(suggested, _monsters, MonsterPlate(), artwork,
            titleFont, font);

        return window is null ? null : new MonsterSelectModal(window, this);
    }

    /// <summary>
    /// Builds the "set the amount" window, <c>Dialog::SelectCount()</c>
    /// (<c>dialog_selectcount.cpp:79-225</c>).
    /// </summary>
    /// <remarks>
    /// 数字框与上下箭头取自 <c>TOWNWIND</c> 的第 4..8 帧（<see cref="ValueSelectionElement.RequiredFrameCount"/>），
    /// 按钮是 <c>SYSTEM</c> 的 OKAY 与 CANCEL。参考在这个窗里还能放两块调用方的部件（怪兽、资源图标），端口的
    /// <see cref="SelectCountView"/> 还没转，所以只有标题与数字。
    /// </remarks>
    /// <param name="dialog">The header, the range and the step, plus where the answer lands.</param>
    /// <param name="initialValue">Number shown first, clamped into the range.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> is <see langword="null"/>.</exception>
    public SelectCountModal? TryCreateSelectCount(SelectCountDialog dialog, int initialValue = 0)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (_fonts?.White is not { } font)
        {
            GameLog.Warn("The count window needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The count window needs the standard buttons, and they are missing.");

            return null;
        }

        if (_assets?.TryReadIcn(IcnCatalog.GetFileName(IcnType.Townwind)) is not { } townwind ||
            townwind.Count < ValueSelectionElement.RequiredFrameCount)
        {
            GameLog.Warn("The count window needs the number frames of TOWNWIND, and they are missing.");

            return null;
        }

        SelectCountView? view = SelectCountView.TryCreate(dialog, buttons, font, townwind.Sprites, initialValue);

        if (view is null)
        {
            GameLog.Warn("The count window needs the message frame, and it is missing.");

            return null;
        }

        return new SelectCountModal(view);
    }

    /// <summary>
    /// Builds the split question the army bar asks, <c>Dialog::ArmySplitTroop()</c>
    /// (<c>dialog_selectcount.cpp:462-651</c>).
    /// </summary>
    /// <remarks>
    /// 槽位那排的图标是 <c>REQUESTS</c> 第 21 帧起的 <c>freeSlots - 1</c> 张（<c>:509-523</c>），数字框与按钮与数量窗
    /// 同一套。参考的标题是 <c>troopFrom.GetName()</c>，即单数名字。
    /// </remarks>
    /// <param name="split">The question, holding the two armies and the start values.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="split"/> is <see langword="null"/>.</exception>
    public ArmySplitModal? TryCreateArmySplit(ArmyBarSplit split)
    {
        ArgumentNullException.ThrowIfNull(split);

        if (_fonts?.Yellow is not { } headerFont || _fonts.White is not { } textFont)
        {
            GameLog.Warn("The split window needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is not { } buttons)
        {
            GameLog.Warn("The split window needs the standard buttons, and they are missing.");

            return null;
        }

        if (_assets?.TryReadIcn(IcnCatalog.GetFileName(IcnType.Townwind)) is not { } townwind ||
            townwind.Count < ValueSelectionElement.RequiredFrameCount)
        {
            GameLog.Warn("The split window needs the number frames of TOWNWIND, and they are missing.");

            return null;
        }

        if (TryReadRequestsFrames(FirstSplitIconFrame, split.Slots - 1) is not { } icons)
        {
            GameLog.Warn("The split window needs the slot icons of REQUESTS, and they are missing.");

            return null;
        }

        ArmySplitDialog dialog = ArmySplitDialog.Create(split.Slots, split.MaxCount,
            MonsterNames.GetSingular(split.From.Monster), split.SuggestedCount, split.UseFastSplit);

        ArmySplitView? view = ArmySplitView.TryCreate(dialog, buttons, headerFont, textFont, townwind.Sprites, icons);

        if (view is null)
        {
            GameLog.Warn("The split window needs the message frame, and it is missing.");

            return null;
        }

        return new ArmySplitModal(view);
    }

    /// <summary>
    /// The 43x43 plate the creature picker puts under a portrait, cut from <c>SWAPWIN</c> frame 0; loaded once.
    /// </summary>
    /// <returns>The plate, or <see langword="null"/> when the archive is missing it.</returns>
    private Sprite? MonsterPlate()
    {
        if (_monsterPlateLoaded)
        {
            return _monsterPlate;
        }

        _monsterPlateLoaded = true;

        if (GameAssets.GetImage(GameAssets.GetFileName(IcnType.Swapwin), 0) is not { } swapWindow)
        {
            GameLog.Warn("The creature picker needs SWAPWIN for the row plates; it draws the portraits without them.");

            return null;
        }

        return _monsterPlate = MonsterSelectionWindow.CutPlate(swapWindow);
    }

    /// <summary>Reads every frame of an archive, the <c>getImage( icn, 0..7 )</c> the reference spells out.</summary>
    /// <param name="type">The archive to read.</param>
    /// <returns>The frames, or <see langword="null"/> when the archive is missing.</returns>
    private static IReadOnlyList<Sprite>? TryReadSprites(IcnType type)
    {
        string name = GameAssets.GetFileName(type);
        List<Sprite> frames = [];

        for (int frame = 0; frame < ArmyInfoArtwork.RequiredSpriteCount; ++frame)
        {
            if (GameAssets.GetImage(name, frame) is not { } sprite)
            {
                return null;
            }

            frames.Add(sprite);
        }

        return frames;
    }

    /// <summary>
    /// Reads a run of frames of <c>REQUESTS</c>, the slot icons of the split window (<c>dialog_selectcount.cpp:509-523</c>).
    /// </summary>
    /// <param name="first">Frame of the first icon, 21 in the reference.</param>
    /// <param name="count">How many icons the row holds.</param>
    /// <returns>The frames, or <see langword="null"/> when one of them is missing.</returns>
    private static IReadOnlyList<Sprite>? TryReadRequestsFrames(int first, int count)
    {
        if (count <= 0)
        {
            return null;
        }

        string name = IcnCatalog.GetFileName(IcnType.Requests);
        List<Sprite> frames = new(count);

        for (int index = 0; index < count; ++index)
        {
            if (GameAssets.GetImage(name, first + index) is not { } sprite)
            {
                return null;
            }

            frames.Add(sprite);
        }

        return frames;
    }

    /// <summary>Reads the two frames of a button, released and pressed.</summary>
    /// <param name="type">The archive to read.</param>
    /// <returns>The two frames, or <see langword="null"/> when the archive is missing.</returns>
    private static DialogButtonArtwork? TryReadButtonPair(IcnType type)    {
        string name = GameAssets.GetFileName(type);

        if (GameAssets.GetImage(name, 0) is not { } released || GameAssets.GetImage(name, 1) is not { } pressed)
        {
            return null;
        }

        return new DialogButtonArtwork(released, pressed);
    }

    /// <summary>Every frame of <c>ICN::SPELLS</c>, the icons the picker lists; loaded once.</summary>
    /// <returns>The icons, or an empty list when the archive is missing.</returns>
    private IReadOnlyList<Sprite> SpellIcons()
    {
        if (_spellIcons is not null)
        {
            return _spellIcons;
        }

        if (_assets is null || _assets.TryReadIcn(IcnCatalog.GetFileName(IcnType.Spells)) is not { } file)
        {
            GameLog.Warn("The spell picker needs ICN::SPELLS, and it is missing.");

            return _spellIcons = [];
        }

        return _spellIcons = file.Sprites;
    }

    /// <summary>Builds a message box ready to be run; <see langword="null"/> when the fonts or the buttons are missing.</summary>
    /// <param name="header">Title of the box; empty for none.</param>
    /// <param name="body">The message.</param>
    /// <param name="buttons">Buttons on the bottom edge; <see cref="DialogButton.Ok"/> for the plain explanation.</param>
    /// <returns>The box, or <see langword="null"/> with the reason in the log.</returns>
    public MessageBoxModal? TryCreateMessageBox(string header, string body, DialogButton buttons = DialogButton.Ok)
    {
        if (_fonts is null)
        {
            GameLog.Warn($"The box \"{body}\" needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is null)
        {
            GameLog.Warn($"The box \"{body}\" needs the standard buttons, and they are missing.");

            return null;
        }

        return MessageBoxModal.Create(header, body, _fonts.Yellow, _fonts.White, buttons, _buttons);
    }

    /// <summary>Builds the input window a request of the specifications page asks for, <c>Dialog::inputString</c>.</summary>
    /// <param name="dialog">The dialog the page state machine filled in.</param>
    /// <returns>The window, or <see langword="null"/> when the fonts or the input frame are missing.</returns>
    public InputStringModal? TryCreateInput(InputStringDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (_fonts is null)
        {
            GameLog.Warn("The input window needs the game fonts, and they are missing.");

            return null;
        }

        if (InputStringWindow.TryCreate(dialog, _fonts.Yellow, _fonts.White, ScreenBuffer.DefaultSize) is not { } window)
        {
            GameLog.Warn("The input window needs the input frame, and it is missing.");

            return null;
        }

        return new InputStringModal(window);
    }

    /// <summary>
    /// Builds the number pad <c>openVirtualNumpad()</c> puts on the screen (<c>ui_keyboard.cpp:1295-1320</c>).
    /// </summary>
    /// <param name="value">The number the field shows now.</param>
    /// <param name="minimum">Smallest number the caller accepts; a negative one makes the pad signed.</param>
    /// <param name="maximum">Largest number the caller accepts.</param>
    /// <returns>The pad, or <see langword="null"/> with the reason in the log when the artwork is missing.</returns>
    /// <remarks>范围检查不在这里：参考是关窗时才 <c>std::stoi</c> 再比，越界要回到键盘上重打，端口照这个顺序。</remarks>
    public VirtualKeyboardView? TryCreateNumpad(int value, int minimum, int maximum)
    {
        bool isEvil = Settings.Get().IsEvilInterfaceEnabled;

        if (_keyboardArtwork is null)
        {
            string okayName = isEvil
                ? VirtualKeyboardArtwork.EvilOkayButtonAssetName
                : VirtualKeyboardArtwork.OkayButtonAssetName;
            Sprite? background = GameAssets.GetImage(isEvil
                ? VirtualKeyboardArtwork.EvilButtonBackgroundAssetName
                : VirtualKeyboardArtwork.ButtonBackgroundAssetName, 0);
            Sprite? request = GameAssets.GetImage(VirtualKeyboardArtwork.RequestBackgroundAssetName, 0);
            Sprite? okayReleased = GameAssets.GetImage(okayName, 0);
            Sprite? okayPressed = GameAssets.GetImage(okayName, 1);

            _keyboardArtwork = background is null || request is null || okayReleased is null || okayPressed is null
                ? null
                : VirtualKeyboardArtwork.TryCreate(background, request, [okayReleased, okayPressed]);
        }

        if (_keyboardArtwork is null)
        {
            GameLog.Warn("The number pad needs the keyboard artwork, and it is missing.");

            return null;
        }

        VirtualKeyboardView? pad = VirtualKeyboardView.TryCreateNumpad(_keyboardArtwork, ScreenBuffer.DefaultSize, value,
            minimum, maximum, isEvil);

        if (pad is null)
        {
            GameLog.Warn($"The number pad for {value} needs the game fonts, and they are missing.");
        }

        return pad;
    }

    /// <summary>
    /// Builds the box that describes one spell, <c>SpellDialogElement::showPopup()</c> (<c>ui_dialog.cpp:543-546</c>).
    /// </summary>
    /// <param name="spell">The spell the author right-clicked.</param>
    /// <returns>The box, or <see langword="null"/> with the reason in the log when the data is missing.</returns>
    public MessageBoxModal? TryCreateSpellInfo(SpellType spell)
    {
        if (_fonts is null)
        {
            GameLog.Warn("The spell description needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is null)
        {
            GameLog.Warn("The spell description needs the standard buttons, and they are missing.");

            return null;
        }

        // 图标是 ICN::SPELLS 的一帧，按 Spell::IndexSprite() 取（editor_spell_selection.cpp:117 同一个下标）。
        var described = new Spell(spell);
        Sprite? icon = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Spells), (int)described.IndexSprite());

        if (icon is null)
        {
            GameLog.Warn($"The spell description needs the icon of {spell}, and the archive lacks it.");

            return null;
        }

        return new MessageBoxModal(SpellDescriptionDialog.Create(described, icon, _fonts.Yellow, _fonts.White,
            _fonts.SmallWhite, _buttons));
    }

    /// <summary>The list artwork the rumors, the events and the language windows are drawn with.</summary>
    /// <returns>The artwork, or <see langword="null"/> with the reason in the log when the archives lack it.</returns>
    public EditorListArtwork? TryCreateListArtwork()
    {
        _listArtwork ??= EditorListArtwork.TryCreate(Settings.Get().IsEvilInterfaceEnabled);

        if (_listArtwork is null)
        {
            GameLog.Warn("The editor list windows need their artwork, and it is missing.");
        }

        return _listArtwork;
    }

    /// <summary>The artwork of the daily event spec window, <c>editDailyEvent()</c>'s frames.</summary>
    /// <returns>The artwork, or <see langword="null"/> with the reason in the log when the archives lack it.</returns>
    public EditorDailyEventSpecArtwork? TryCreateDailyEventArtwork()
    {
        _dailyEventArtwork ??= EditorDailyEventSpecArtwork.TryCreate(Settings.Get().IsEvilInterfaceEnabled);

        if (_dailyEventArtwork is null)
        {
            GameLog.Warn("The daily event window needs its artwork, and it is missing.");
        }

        return _dailyEventArtwork;
    }

    /// <summary>
    /// Builds the single-artifact picker, <c>Dialog::selectArtifact()</c> (<c>dialog_selectitems.cpp:1162-1194</c>).
    /// </summary>
    /// <param name="artifact">The artifact the list starts on, or <see cref="ArtifactType.Unknown"/> for the first row.</param>
    /// <param name="level">When set, only the artifacts of that tier are offered.</param>
    /// <param name="version">
    /// Version of the map, which decides whether the expansion artifacts are offered
    /// (<c>Settings::getCurrentMapInfo().version</c> of the reference; the port has no settings layer for it).
    /// </param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    public ArtifactSelectModal? TryCreateArtifactSelect(ArtifactType artifact, ArtifactLevel? level = null,
        MapVersion version = MapVersion.PriceOfLoyalty)
    {
        if (TryCreateItemSelectionArtwork() is not { } artwork ||
            _fonts?.Yellow is not { } titleFont || _fonts.White is not { } font)
        {
            GameLog.Warn("The artifact picker needs the list artwork and the fonts, and they are missing.");

            return null;
        }

        if (TryCreateArtifactIcons() is not { } icons)
        {
            return null;
        }

        ArtifactSelectionWindow? window = ArtifactSelectDialog.TryCreate(artifact, icons, artwork, titleFont, font,
            version, level: level);

        if (window is null)
        {
            GameLog.Warn($"The artifact picker has nothing to offer for {level}.");

            return null;
        }

        return new ArtifactSelectModal(window, this);
    }

    /// <summary>
    /// Builds the multi-artifact picker, <c>Dialog::multiSelectArtifact()</c> (<c>dialog_selectitems.cpp:1196-1202</c>).
    /// </summary>
    /// <param name="allowed">The artifacts the list offers.</param>
    /// <param name="selected">The artifacts already picked; empty means every one of them is picked.</param>
    /// <returns>The window, or <see langword="null"/> with the reason in the log.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="allowed"/> or <paramref name="selected"/> is <see langword="null"/>.</exception>
    public MultiArtifactSelectModal? TryCreateMultiArtifactSelect(IReadOnlyList<int> allowed,
        IReadOnlyList<int> selected)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        ArgumentNullException.ThrowIfNull(selected);

        if (allowed.Count == 0)
        {
            GameLog.Warn("The artifact picker has no candidates.");

            return null;
        }

        if (TryCreateItemSelectionArtwork() is not { } artwork ||
            _fonts?.Yellow is not { } titleFont || _fonts.White is not { } font)
        {
            GameLog.Warn("The artifact picker needs the list artwork and the fonts, and they are missing.");

            return null;
        }

        if (TryCreateArtifactIcons() is not { } icons)
        {
            return null;
        }

        var window = new MultiArtifactSelectionWindow(allowed, selected, icons, ArtifactSelectDialog.DialogSize(),
            artwork, titleFont, font);

        return new MultiArtifactSelectModal(window, this);
    }

    /// <summary>
    /// Builds the box that describes one artifact, <c>ArtifactDialogElement::showPopup()</c>
    /// (<c>ui_dialog.cpp:666-676</c>).
    /// </summary>
    /// <param name="artifact">The artifact the author clicked.</param>
    /// <param name="spell">The spell of a spell scroll, which its description names; <see cref="SpellType.None"/> otherwise.</param>
    /// <returns>The box, or <see langword="null"/> with the reason in the log when the data is missing.</returns>
    public MessageBoxModal? TryCreateArtifactInfo(ArtifactType artifact, SpellType spell = SpellType.None)
    {
        if (_fonts is null)
        {
            GameLog.Warn("The artifact description needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is null)
        {
            GameLog.Warn("The artifact description needs the standard buttons, and they are missing.");

            return null;
        }

        Sprite? frame = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Resource),
            ArtifactDescriptionDialog.FrameIndex);
        Sprite? icon = GameAssets.GetImage(GameAssets.GetFileName(IcnType.Artifact),
            (int)ArtifactTypes.FullSpriteIndex(artifact));

        if (frame is null || icon is null)
        {
            GameLog.Warn($"The artifact description needs the frames of {artifact}, and the archive lacks them.");

            return null;
        }

        return new MessageBoxModal(ArtifactDescriptionDialog.Create(artifact, frame, icon, _fonts.Yellow,
            _fonts.White, _buttons, (int)spell));
    }

    /// <summary>
    /// The <c>ARTFX</c> icons the artifact pickers draw, which the artifact bag already loads.
    /// </summary>
    /// <returns>The icons, or <see langword="null"/> with the reason in the log when the archives are missing.</returns>
    private IArtifactIcons? TryCreateArtifactIcons()
    {
        if (_assets is null)
        {
            GameLog.Warn("The artifact picker needs the archives, and they are missing.");

            return null;
        }

        return _artifactIcons ??= new ArtifactsBarArtwork();
    }

    /// <summary>
    /// The artwork of the candidate list windows, <c>ItemSelectionWindow::ItemSelectionWindow()</c>
    /// (<c>dialog_selectitems.cpp:909-957</c>): the scroll bar, the groove and the two small buttons.
    /// </summary>
    /// <returns>The artwork, or <see langword="null"/> with the reason in the log when the archives lack it.</returns>
    public ItemSelectionWindowArtwork? TryCreateItemSelectionArtwork()
    {
        if (_assets is null)
        {
            GameLog.Warn("The candidate list windows need their artwork, and the archives are missing.");

            return null;
        }

        _sprites ??= new IcnViewWorldSpriteSource(_assets);
        _itemSelectionArtwork ??= ItemSelectionWindowArtwork.TryCreate(_sprites, Settings.Get().IsEvilInterfaceEnabled);

        if (_itemSelectionArtwork is null)
        {
            GameLog.Warn("The candidate list windows need their artwork, and it is missing.");
        }

        return _itemSelectionArtwork;
    }

    /// <summary>Hands the fonts of the editor windows out; the list windows take a yellow header and a white body.</summary>
    /// <param name="fonts">The fonts, when the data is installed.</param>
    /// <returns>Whether the fonts are there.</returns>
    public bool TryGetFonts([NotNullWhen(true)] out GameFonts? fonts)
    {
        fonts = _fonts;

        return _fonts is not null;
    }
}

/// <summary>
/// How the spell selection window was left, <c>openSpellSelectionWindow()</c>'s <c>bool</c> plus the level it
/// ended on.
/// </summary>
/// <param name="Accepted">Whether the author accepted the selection, <c>true</c> of the reference's return value.</param>
/// <param name="SpellLevel">The level of the last window shown; 1 to 5, or the level the caller passed in.</param>
/// <param name="Spells">
/// The spells to keep; empty both when the window was cancelled and when every spell of the level was enabled (the
/// game reads an empty list as "the default", <c>editor_spell_selection.cpp:426-429</c>).
/// </param>
public readonly record struct EditorSpellSelectionDialogResult(bool Accepted, int SpellLevel,
    IReadOnlyList<SpellType> Spells);
