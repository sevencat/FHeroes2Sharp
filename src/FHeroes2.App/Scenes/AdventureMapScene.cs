using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Interface;

// `AdventureMap` 这个名字同时是 FHeroes2.Game 下的命名空间与 Interface::AdventureMap 这个类，因此用别名钉住后者。
using AdventureInterface = FHeroes2.UI.Interface.AdventureMap;

using CastleTown = FHeroes2.Game.Castle.Castle;

using ZoomLevel = FHeroes2.Game.Kingdom.ZoomLevel;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The adventure map of a started scenario, milestone M4, and now a thin shell over
/// <see cref="AdventureInterface"/> (<c>Interface::AdventureMap</c>).
///
/// <para>
/// What the scene still owns is what the ported screen cannot reach: it builds the world and the session
/// once, it feeds the frame to the turn loop (<see cref="Update"/>: panels, hot keys, animation, scrolling and
/// the end of turn), it answers the two requests the screen hands back (the castle screen of a left click and
/// the exit of the loop, <see cref="ExitEvent"/>), and it draws the two windows that are still scene owned
/// (View World and the adventure options).
/// </para>
/// <para>
/// 第 4 步之前的自建件（雷达、按钮面板、状态石、视口卷屏）已经全部让给屏幕，见 <c>bd 归档（PROGRESS.md，FHeroes2Sharp-jwk）</c> §6.1b。
/// 地图的绘制与卷屏都归 <see cref="GameArea"/>，瓦片换算走 <c>GetValidTileIdFromPoint()</c>，场景不再自己
/// 记偏移；<see cref="AdventureMapView"/> 只剩给 View World 与编辑器用的几何。
/// </para>
/// <para>
/// The fog is <b>not</b> the scene's own: the scene loads the map into <see cref="World.Current"/> and
/// shows <see cref="World.Fog"/>, so the table the renderer walks is the one the game rules change
/// (ADR-131).
/// </para>
/// </summary>
public sealed class AdventureMapScene : IScene
{
    private readonly ScreenBuffer _frame;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly World _world;
    private readonly AdventureSession? _session;
    private readonly AdventureMapView _view;

    /// <summary>A panel drag the screen handed over, <see langword="null"/> while no panel is grabbed.</summary>
    private BorderDrag? _drag;

    /// <summary>The ported screen itself, <c>Interface::AdventureMap</c>: it owns the panels and the frame.</summary>
    private readonly AdventureInterface _interface;
    private readonly Rect _viewport;

    private CastleTown? _castleRequest;

    private readonly IViewWorldSpriteSource? _viewWorldSprites;
    private readonly PlayerColor _friends;

    private ViewWorldOverlay? _viewWorld;
    private ZoomLevel _viewWorldZoom = ZoomLevel.ZoomLevel2;

    private readonly AdventureOptionsView? _optionsArtwork;
    private AdventureOptionsOverlay? _options;

    /// <summary>The quick info box while the right button is held, <c>Dialog::QuickInfo()</c>.</summary>
    private IQuickInfoPanel? _quickInfo;

    /// <summary>
    /// What a human hero's action asks of the interface, <c>IHeroActionHost</c>'s human branch; <see langword="null"/>
    /// on a map without a human player.
    /// </summary>
    /// <remarks>
    /// 没有它的话人类英雄走不动（<see cref="AdventureSession"/> 会装上只揭雾的处理器），所以它和会话一起建。
    /// 战斗那一半由 <see cref="BattlePresenter"/> 在这个宿主里提供；问句与城堡屏还没接（<c>0lm.12</c>）。
    /// </remarks>
    private AdventureHeroActionHost? _heroActionHost;

    /// <summary>
    /// Whether the hourglass of a computer turn has been put up, so the next frame runs the turn itself.
    /// </summary>
    /// <remarks>
    /// 参考在 <c>CONTROL_AI</c> 分支里先重绘一帧再调 <c>KingdomTurn()</c>（<c>game_startgame.cpp:916-938</c>），
    /// 那块画面就留在屏幕上等 AI 跑完。端口的 <see cref="Update"/> 不画，画在
    /// <see cref="DrawFrame"/>，所以亮招牌与跑回合必须分成两帧。
    /// </remarks>
    private bool _aiTurnAnnounced;

    /// <summary>Whether the end of the game has already been reported to the host.</summary>
    private bool _gameOverReported;

    private AdventureMapScene(
        ScreenBuffer frame,
        AssetBundle? assets,
        GameFonts? fonts,
        World world,
        AdventureSession? session,
        AdventureMapView view,
        MapFrame mapFrame,
        AdventureInterface screen,
        AdventureOptionsView? optionsArtwork,
        IViewWorldSpriteSource? viewWorldSprites,
        PlayerColor friends)
    {
        _frame = frame;
        _assets = assets;
        _fonts = fonts;
        _world = world;
        _session = session;
        _view = view;

        _interface = screen;
        _optionsArtwork = optionsArtwork;
        _viewWorldSprites = viewWorldSprites;
        _friends = friends;
        _viewport = mapFrame.Viewport;
    }

    /// <summary>The map the scene draws.</summary>
    public Mp2Map Map => _view.Map;

    /// <summary>Scrolling view of the terrain.</summary>
    public AdventureMapView View => _view;

    /// <summary>The world the map was loaded into, the fog of war included.</summary>
    public World World => _world;

    /// <summary>
    /// The turn loop of the scenario, or <see langword="null"/> when the map has no human player and the
    /// scene is only a viewer.
    /// </summary>
    public AdventureSession? Session => _session;

    /// <summary>The hero the movement and the next-hero button act on, <c>Interface::AdventureMap::GetFocus()</c>.</summary>
    public Hero? FocusHero => _session?.FocusHero;

    /// <summary>
    /// What a human hero's action asks of the interface, or <see langword="null"/> on a map without a human player.
    /// </summary>
    /// <remarks>
    /// <c>GameHost</c> 在造好场景之后把帧循环接到它身上（<see cref="AdventureHeroActionHost.RunModal"/>），
    /// 就像接 <see cref="ExitEvent"/> 一样；战斗界面的人类回合与动画都要借那段循环。
    /// </remarks>
    internal AdventureHeroActionHost? HeroActionHost => _heroActionHost;

    /// <summary>Name of the map, as stored in its header.</summary>
    public string MapName => _view.Map.Info.Name;

    /// <summary>
    /// Opens the adventure map of <paramref name="mapInfo"/>; <see langword="null"/> with an
    /// <paramref name="error"/> when the map or the terrain tiles cannot be read.
    /// </summary>
    public static AdventureMapScene? TryCreate(
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
            ObjectSpriteSet? objects = ObjectSpriteSet.TryCreate(assets);

            // The map is drawn into the viewport of the frame and not over the whole display, so the
            // border, the radar and the panel bay never have to be repainted over it.
            MapFrame mapFrame = new(frame.Size, MapFrame.Load(assets));

            // Without a kingdom there is no focus hero to start on, so the castle closest to the middle
            // of the map stands in for the player's position; that is what makes the first screenshot of
            // a map show its heart instead of an empty corner.
            int middleX = map.Width / 2;
            int middleY = map.Height / 2;
            Mp2CastlePosition? focus = map.Castles
                .OrderBy(castle => ((castle.X - middleX) * (castle.X - middleX))
                                   + ((castle.Y - middleY) * (castle.Y - middleY)))
                .FirstOrDefault();

            // The world owns the map state, the fog of war included (ADR-131): the scene loads the map into
            // World.Current the way a started scenario does, and every rule that opens the fog writes into
            // the same table the renderer walks.
            // 起始资源按玩家挑的难度发（参考 Kingdom::Init 取 Game::getDifficulty()），地图头的那一档只用来
            // 显示与评分；--difficulty 就是没法开窗时替玩家做这个选择的口子（ADR-203）。
            World world = World.Current;
            world.LoadMap(map, Settings.Get().GameDifficulty);

            // The fog is drawn for the colors the map declares as human, like Players::FriendColors() in the
            // reference: a tile is fogged only when every one of them has not discovered it yet. A scenario
            // with no human player at all (a map only meant for the editor) stays fog free.
            PlayerColor observers = map.Info.HumanPlayers;
            int scouted = ScoutStart(world, observers, focus);
            MapFog? fog = observers == PlayerColor.None ? null : world.Fog;
            FogSpriteSet? fogSprites = FogSpriteSet.TryLoad(assets);

            // The hero actions of a human player go through the interface layer: the battle screen has to be opened
            // from inside the action (the reference blocks there, heroes_action.cpp), and the questions of the
            // wandering monsters cannot be answered by the rules.
            AdventureHeroActionHost actionHost = new(frame, assets, fonts, world);

            // The turn loop of the reference's StartGame(): day one, kingdoms' income, the focus hero. A map
            // without a human player returns null and stays a viewer, which is what the scene was until the
            // session existed.
            AdventureSession? session = AdventureSession.Start(world, map.Info, actionHost);

            // Both sets share one decoder cache, so keeping them apart is only about what they map. The world
            // goes in last: from now on the adventurers are drawn from the world's heroes (position, facing,
            // step of the walk animation) and not from the static MP2 hero table.
            AdventureMapView view = new(
                map,
                tiles,
                mapFrame.Viewport.Size,
                objects,
                HeroSpriteSet.TryCreate(assets),
                MonsterSpriteSet.TryCreate(assets),
                BoatSpriteSet.TryCreate(assets),
                fog,
                fogSprites,
                world);

            if (focus is not null)
            {
                view.CenterOn(focus.X, focus.Y);
            }

            // 屏幕自己建面板、自己生成 GameArea：参考在 StartGame 里 reset() 之后才 SetFocus，
            // 端口照这个次序来——先 Reset() 摆好边框、雷达、图标条、按钮与状态面板，再按焦点英雄居中。
            AdventureInterface? screen = AdventureInterface.TryCreate(frame.Pixels, assets, null);
            if (screen is null)
            {
                error = "The monster portraits are not available, so the interface cannot be built.";

                return null;
            }

            // StartGame() 在 reset() 之后还要把雷达按世界画出来、把状态面板摆回初始态（game_startgame.cpp:745-750），
            // 这两步在参考里由宿主做，端口同样留给场景。
            // 开局前把「轮到谁」摆成人类颜色，参考在 reset() 之前就是这么做的（game_startgame.cpp:742），
            // 迷雾方向与雷达都按这个颜色取 Friends 来算。
            FHeroes2.Game.System.Settings.Get().Players.SetCurrentColor(FHeroes2.Game.System.Players.HumanColors());

            screen.Reset();
            screen.GetRadar().Build(world);
            screen.GetStatusPanel().Reset();

            // 迷雾的方向缓存按刚侦察过的世界重算一次，否则地图层按「全雾」画（game_startgame.cpp:781）。
            FHeroes2.UI.Interface.GameArea.UpdateMapFogDirections();

            if (session?.FocusHero is { } actingHero)
            {
                screen.SetFocus(actingHero, false);
            }
            else if (focus is not null)
            {
                screen.GetGameArea().SetCenter(new Point(focus.X, focus.Y));
            }

            // View World 由屏幕自己开窗，它要读视口与可见区域；两者都来自场景的地图视图。
            screen.AttachView(view, mapFrame.Viewport);

            AdventureMapScene scene = new(
                frame,
                assets,
                fonts,
                world,
                session,
                view,
                mapFrame,
                screen,
                AdventureOptionsArtwork.TryCreate(assets),
                assets is null ? null : new IcnViewWorldSpriteSource(assets),
                observers)
            {
                // 只有有人类玩家时才有英雄动作可做：会话为 null 的观光地图留着空宿主。
                _heroActionHost = session is null ? null : actionHost,
            };

            // 城堡屏由宿主换场景（参考里是阻塞的 Game::OpenCastleDialog），屏幕点开城堡时先问场景。
            screen.OpenCastleScreen = scene.RequestCastleScreen;

            // 速览在参考里也是阻塞循环（按住右键），渲染循环自己管帧的场景不能被卡住：面板交回场景画，
            // 松手时场景自己收掉（UpdateQuickInfo）。
            screen.Dialogs.PresentQuickInfo = panel => scene._quickInfo = panel;

            // With a started game the map opens on the acting hero instead of the middle castle.
            scene.CentreOnFocusHero();

            GameLog.Info(
                $"Adventure map opened: '{map.Info.Name}' ({map.Width}x{map.Height}, " +
                $"{scene.View.MapPixelSize.Width}x{scene.View.MapPixelSize.Height} pixels, " +
                $"{map.Castles.Count} castles, {map.Addons.Count} add-ons, " +
                $"{scouted} tiles scouted, {scene.View.Fog?.CountFogged() ?? 0} under the fog).");

            if (session is not null)
            {
                int heroes = session.Kingdom.Heroes.Count;
                GameLog.Info(
                    $"The game has started: day {session.DayCount}, {session.CurrentColor} plays, " +
                    $"{heroes} {(heroes == 1 ? "hero" : "heroes")} in the kingdom.");
            }

            return scene;
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            GameLog.Error($"The map '{mapInfo.FilePath}' could not be opened", exception);
            return null;
        }
    }

    /// <summary>The open View World window, <see langword="null"/> while the adventure map is on screen.</summary>
    public ViewWorldOverlay? ViewWorld => _viewWorld;

    /// <summary>Whether the View World window owns the screen and the input.</summary>
    public bool IsViewWorldOpen => _viewWorld is not null;

    /// <summary>The open adventure options dialog, <see langword="null"/> while it is closed.</summary>
    public AdventureOptionsOverlay? AdventureOptions => _options;

    /// <summary>Whether the adventure options dialog is on screen and owns the input.</summary>
    public bool IsAdventureOptionsOpen => _options is not null;

    /// <summary>
    /// Opens the adventure options dialog over the map, <c>Dialog::AdventureOptions()</c>
    /// (<c>dialog_adventure.cpp:38-121</c>).
    /// </summary>
    /// <returns><see langword="false"/> when the dialog is already open or its artwork is missing.</returns>
    public bool OpenAdventureOptions()
    {
        if (_options is not null || _optionsArtwork is null)
        {
            return false;
        }

        _options = new AdventureOptionsOverlay(_optionsArtwork, _frame);

        return true;
    }

    /// <summary>Closes the adventure options dialog without a result.</summary>
    public void CloseAdventureOptions() => _options = null;

    /// <summary>
    /// Opens the View World window on <paramref name="mode"/>, the body of <c>ViewWorld::ViewWorldWindow()</c>
    /// (<c>view_world.cpp:589-700</c>) up to its event loop.
    /// </summary>
    /// <param name="mode">Which objects to reveal.</param>
    /// <returns><see langword="false"/> when the window is already open or its artwork is missing.</returns>
    /// <remarks>
    /// <para>
    /// 参考在淡出之后才建缓存（`CacheForMapWithResources`），本方法照这个顺序做三件事：用
    /// <see cref="ViewWorldMapBuilder"/> 把整张图渲染成每档一张、把图标盖上去（`DrawObjectsIcons`）、再把
    /// 结果交给 <see cref="ViewWorldWindow"/>。
    /// </para>
    /// <para>
    /// 缓存只对这个模式有效（图标与迷雾都按模式烘进去），所以模式一变就要重开，跟参考一致。
    /// </para>
    /// </remarks>
    public bool OpenViewWorld(ViewWorldMode mode)
    {
        if (_viewWorld is not null || _viewWorldSprites is null)
        {
            return false;
        }

        ViewWorldMapCache cache = new ViewWorldMapBuilder(
            _view.Map,
            _view.Tiles,
            _world,
            _view.Objects,
            _view.Monsters,
            _view.Boats,
            _view.FogSprites).Build(mode);

        ViewWorldRenderer.DrawObjectsIcons(_friends, mode, cache, _world, _viewWorldSprites);

        var window = new ViewWorldWindow(
            _world,
            mode,
            _friends,
            _viewWorldZoom,
            _viewport,
            _view.VisibleTileArea,
            cache,
            _frame.Size.Width,
            _frame.Size.Height,
            _viewWorldSprites);

        _viewWorld = new ViewWorldOverlay(window, _frame);

        return true;
    }

    /// <summary>
    /// Closes the View World window and remembers the zoom level it was left at, the
    /// <c>conf.SetViewWorldZoomLevel( ... )</c> of <c>view_world.cpp:771-772</c>.
    /// </summary>
    public ZoomLevel? CloseViewWorld()
    {
        if (_viewWorld is null)
        {
            return null;
        }

        _viewWorldZoom = _viewWorld.Close();
        _viewWorld = null;

        return _viewWorldZoom;
    }

    /// <summary>
    /// One frame of the turn loop, the body of <c>Interface::AdventureMap::HumanTurn()</c>
    /// (<c>game_startgame.cpp:1127-1609</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的循环体做七件事：收键、派发面板（含边缘卷屏与拖拽卷屏）、传英雄的走动动画、按面板的结论收回合、
    /// 判断王国的胜负。前四件都在屏幕里（<see cref="AdventureInterface.ProcessTurnFrame"/> 与
    /// <see cref="AdventureInterface.FinishHumanTurn"/>），场景只剩屏幕管不了的三件：接管面板拖动、把界面焦点
    /// 同步回会话、把「这一帧要求做什么」落到会话与宿主上。
    /// </para>
    /// <para>
    /// 两个旧浮层（View World 与冒险选项）打开时整帧都不跑：参考里这两个窗口有自己的循环，地图的循环挂在它们下面。
    /// 端口的这两层窗口仍是场景自己压帧，所以只在这里让开。
    /// </para>
    /// <para>
    /// <paramref name="elapsedSeconds"/> 不参与计算：卷屏的节流与惯性都走 <c>GameDelays</c> 的全局表、
    /// 动画走 <see cref="GameArea"/> 自己的帧计数，参考同样不把帧时递进这两处。
    /// </para>
    /// </remarks>
    /// <param name="elapsedSeconds">Seconds since the previous frame; not used.</param>
    public void Update(double elapsedSeconds)
    {
        _ = elapsedSeconds;

        if (_viewWorld is not null || _options is not null)
        {
            return;
        }

        // 轮到电脑：参考在 StartGame() 的循环里把这一回合整个跑完（game_startgame.cpp:893-974），且期间
        // 光标是沙漏、玩家动不了地图。端口同样在这一帧把人类那一套让开，见 RunComputerTurn()。
        if (_session is { IsAiTurn: true } computerTurn)
        {
            if (computerTurn.IsGameOver)
            {
                // 人类的回合结束处有胜负检查（FinishHumanTurn 的 !isPlay 分支），电脑这一侧没有；
                // 人类全灭之后 EndTurn() 不再换人，若不在这里收手，电脑回合会一帧一帧地跑下去。
                ReportGameOver(computerTurn);

                return;
            }

            RunComputerTurn(computerTurn);

            return;
        }

        LocalEvent events = LocalEvent.Current;

        AdventurePanelResult panel = _interface.ProcessTurnFrame(events, RequestExit);

        PumpDrag(panel.Drag, events);

        // The kingdom that has neither a hero nor a town cannot act, so the reference turns its "nothing to do"
        // into an end of turn, the `!myKingdom.isPlay()` branch of FinishHumanTurn().
        AdventureEventResult result = _interface.FinishHumanTurn(panel.Mode, IsKingdomVanquished());

        FollowInterfaceFocus();
        UpdateQuickInfo(events);

        if (result != AdventureEventResult.Cancel)
        {
            HandleResult(result);
        }
    }

    /// <summary>
    /// Plays one computer player's turn, the <c>CONTROL_AI</c> branch of <c>StartGame()</c>
    /// (<c>game_startgame.cpp:893-974</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的顺序是：换沙漏光标（<c>:897</c>）、把状态栏切成「电脑回合」（<c>:914</c>）、重绘一帧
    /// （<c>:916-918</c>）、跑 <c>AI::Planner::KingdomTurn()</c>（<c>:938</c>）。重绘那一帧在端口是
    /// <see cref="DrawFrame"/> 的事，而 <see cref="Update"/> 不画，所以这里分两帧：第一帧只把招牌挂上（画出去
    /// 就是沙漏在转），第二帧跑回合并交给下一位 —— 又轮到电脑就下一帧继续，轮到人类就还给他。
    /// </para>
    /// <para>
    /// <b>偏差</b>：参考的电脑走子是逐格动画（动画在 <c>AI::HeroesMove()</c> 自己的事件循环里），端口的
    /// <c>AiHeroAction.HeroesMove()</c> 是纯规则、一口气走完，所以电脑英雄在画面上是瞬移的，状态栏的进度条
    /// 也只有沙漏在转。见 <c>bd 归档（PROGRESS.md，FHeroes2Sharp-jwk）</c> §7.3。
    /// </para>
    /// </remarks>
    /// <param name="session">The session whose current color is the computer.</param>
    private void RunComputerTurn(AdventureSession session)
    {
        if (!_aiTurnAnnounced)
        {
            CursorState.Current?.SetThemes(CursorTheme.Wait);

            StatusPanel status = _interface.GetStatusPanel();

            status.Reset();
            status.SetState(StatusType.AiTurn);
            status.ResetAiTurnProgress();

            _aiTurnAnnounced = true;

            return;
        }

        _aiTurnAnnounced = false;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        AiTurnOutcome outcome = session.RunAiTurn();
        watch.Stop();

        var report = new System.Text.StringBuilder();

        foreach (Hero aiHero in session.Kingdom.Heroes)
        {
            report.Append($"[{aiHero.MapIndex} role={aiHero.AiRole} mp={aiHero.MovePoints} path={aiHero.Path.Count} army={aiHero.Army.GetCount()}] ");
        }

        // 电脑回合没有画面可看，日志是唯一能判断它真的走过棋的地方：每个英雄的编号、角色、剩余移动力、
        // 走完留下的路径长度和兵力。冒烟（--skip-turns）就靠这一行验收。
        GameLog.Info(
            $"Computer turn {session.CurrentColor}: {watch.ElapsedMilliseconds} ms, {session.Kingdom.Heroes.Count} heroes, " +
            $"{session.Kingdom.Castles.Count} castles, {report}");

        if (outcome != AiTurnOutcome.EndTurn)
        {
            // 参考把非 END_TURN 的结果交回 StartGame() 的调用方，由它离开这张图（game_startgame.cpp:990-1005）。
            GameLog.Info($"The computer player {session.CurrentColor} returned '{outcome}', so the map is left.");
            RequestExit();

            return;
        }

        EndTurn();

        if (!session.IsAiTurn)
        {
            // 交回人类：撤掉沙漏。参考在 CONTROL_HUMAN 分支里换回指针、状态栏 Reset（:851-884）。
            CursorState.Current?.SetThemes(CursorTheme.Pointer);
            _interface.GetStatusPanel().Reset();
        }
    }

    /// <summary>
    /// Tells the host that every human kingdom is out, the <c>checkGameOver()</c> of <c>StartGame()</c>
    /// (<c>game_startgame.cpp:970-976</c>).
    /// </summary>
    /// <remarks>
    /// 结局画面还没有（<c>bd 归档（PROGRESS.md，FHeroes2Sharp-jwk）</c> §7.4），所以只记一笔再请宿主离开这张图，且只做一次：
    /// 宿主不换场景时，这个分支每帧都会被走到。
    /// </remarks>
    /// <param name="session">The session that is over.</param>
    private void ReportGameOver(AdventureSession session)
    {
        if (_gameOverReported)
        {
            return;
        }

        _gameOverReported = true;
        _aiTurnAnnounced = false;
        GameLog.Info($"The game is over on day {session.DayCount}: every human kingdom is out.");
        RequestExit();
    }

    /// <summary>
    /// What the scene does when the screen asks to leave the map, the <c>Game::processExitEvent()</c> the reference
    /// hands to <c>HumanTurn()</c>.
    /// </summary>
    /// <remarks>
    /// 问「确定退出吗」与换场景都是宿主（<c>GameHost</c>）的事：它拿得到场景管理器，场景拿不到。宿主没接这一手时
    /// 按「取消」处理，也就是留在图上，和参考里玩家答「否」一样。
    /// </remarks>
    /// <returns>The decision to give back to the turn loop.</returns>
    private AdventureEventResult RequestExit() => ExitEvent?.Invoke() ?? AdventureEventResult.Cancel;

    /// <summary>
    /// What the host wants done when the player asks to leave the map, wired up by <c>GameHost</c>.
    /// </summary>
    /// <remarks>
    /// 宿主在这里换场景（回主菜单）并把「取消」还给循环：场景换不了自己，所以它只负责转达。
    /// </remarks>
    public Func<AdventureEventResult>? ExitEvent { get; set; }

    /// <summary>The acting kingdom has no hero and no town, the <c>!myKingdom.isPlay()</c> of the turn loop.</summary>
    /// <returns><see langword="true"/> when the current player cannot act any more.</returns>
    private bool IsKingdomVanquished() => _session is not null && _session.Kingdom.IsLoss;

    /// <summary>
    /// Drops the quick info box when the right button is no longer held, the release half of
    /// <c>MouseRightClick</c> (<c>interface_events.cpp</c>).
    /// </summary>
    /// <remarks>
    /// 参考的速览在 <c>Dialog::QuickInfo()</c> 里阻塞到松手；端口的场景自己管帧，面板从
    /// <see cref="AdventureMapDialogs.PresentQuickInfo"/> 交回来，所以松手这一帧由场景收掉。
    /// </remarks>
    /// <param name="events">Event state of this frame.</param>
    private void UpdateQuickInfo(LocalEvent events)
    {
        if (_quickInfo is not null && !events.IsMouseRightButtonPressed)
        {
            _quickInfo = null;
        }
    }

    /// <summary>
    /// Shows the quick info of the tile under <paramref name="point"/>, the <c>--quick-info</c> of the command line.
    /// </summary>
    /// <param name="point">Pointer in logical pixels.</param>
    /// <returns><see langword="false"/> when the pointer is not over the map or the box cannot be built.</returns>
    public bool ShowQuickInfoAt(Point point)
    {
        GameArea gameArea = _interface.GetGameArea();

        if (!gameArea.Roi.Contains(point))
        {
            return false;
        }

        int index = gameArea.GetValidTileIdFromPoint(point);

        if (index < 0 || index >= _world.Size)
        {
            return false;
        }

        _interface.MouseCursorAreaPressRight(index);

        return _quickInfo is not null;
    }

    /// <summary>
    /// Carries the interface focus back into the session.
    /// </summary>
    /// <remarks>
    /// 热键与按钮改的是界面的焦点（<c>Settings.Players.Current.Focus</c>，<c>EventNextHero()</c> 与点击英雄都写它），
    /// 而会话自己那份焦点是给存档与状态栏用的。两份不能各走各的，所以每帧把界面那份搬回去；界面没有焦点英雄
    /// （例如只点了一座城）时不动会话，免得把焦点清空。
    /// </remarks>
    private void FollowInterfaceFocus()
    {
        if (_session is { } session && InterfaceFocus.GetFocusHeroes() is { } hero)
        {
            session.SetFocus(hero);
        }
    }

    /// <summary>
    /// Acts on what the turn frame asked the loop to do, the <c>return res</c> of <c>HumanTurn()</c>.
    /// </summary>
    /// <param name="result">The decision of the frame.</param>
    private void HandleResult(AdventureEventResult result)
    {
        switch (result)
        {
            case AdventureEventResult.EndTurn:
                EndTurn();

                break;

            case AdventureEventResult.NewGame:
            case AdventureEventResult.LoadGame:
            case AdventureEventResult.QuitGame:
                // 开新局、读档、退出都要换场景，那是宿主的活；参考在这三个值上离开这张图，端口先回主菜单。
                GameLog.Info($"The adventure map asked for '{result}', so the map is left.");
                RequestExit();

                break;

            default:
                // 胜负、高分与战役的入口都还没有接：先把值记下来，免得它被悄悄吞掉。
                GameLog.Info($"The adventure map returned '{result}', which has no handler yet.");

                break;
        }
    }

    /// <summary>
    /// Carries a panel drag across frames, the blocking loop of <c>BorderWindow::QueueEventProcessing()</c>
    /// (<c>interface_border.cpp:494-523</c>).
    /// </summary>
    /// <param name="drag">The drag the frame started, or <see langword="null"/>.</param>
    /// <param name="events">Input state of the frame.</param>
    /// <remarks>
    /// 参考在松手时把面板摆到轮廓的位置（<c>BorderWindow::SetPosition</c>）。轮廓自己带着被拖的
    /// <see cref="BorderDrag.Owner"/>，所以这里松手调 <see cref="BorderDrag.Drop"/> 就够；面板的
    /// <c>SetPos</c> 会夹进屏幕并落进 <c>Settings</c> 的四个位置，退出时随配置写出。
    /// 只有隐藏界面模式有可抓的面板，正常布局下 <paramref name="drag"/> 一直是 <see langword="null"/>。
    /// </remarks>
    private void PumpDrag(BorderDrag? drag, LocalEvent events)
    {
        if (drag is not null)
        {
            _drag = drag;
        }

        if (_drag is null)
        {
            return;
        }

        if (!events.IsMouseLeftButtonPressed)
        {
            _drag.Drop(events.CursorPosition);
            _drag = null;

            return;
        }

        _drag.MoveTo(events.CursorPosition);
    }

    /// <summary>
    /// The scouting a scenario runs once its map is read, <c>World::ClearFog( colors )</c>
    /// (<c>world.cpp:838</c>) followed by the stand-in for the town the view starts on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference scouting is <c>AllHeroes::Scout</c> plus <c>AllCastles::Scout</c> of the human
    /// colors, both inside <c>World::ClearFog( colors )</c>; that is exactly the call below, on the world
    /// the scene just loaded.
    /// </para>
    /// <para>
    /// An MP2 castle carries no owner — the castle table of the format is only <c>x</c>, <c>y</c> and the
    /// castle type, and <c>world_loadmap.cpp</c> creates every one of them with <c>PlayerColor::NONE</c> —
    /// so the castle half of <c>ClearFog</c> finds nothing to do and <paramref name="focus"/> stands in
    /// for the kingdom the port does not model yet: the tile the view starts on is scouted with the castle
    /// radius, exactly what owning that castle would give. Once the map carries owners this extra reveal is
    /// what to delete.
    /// </para>
    /// </remarks>
    /// <param name="world">The world the map was just loaded into.</param>
    /// <param name="observers">The colors the fog is drawn for.</param>
    /// <param name="focus">The town the view is centred on, if the map has one.</param>
    /// <returns>Number of tiles that lost the fog.</returns>
    private static int ScoutStart(World world, PlayerColor observers, Mp2CastlePosition? focus)
    {
        if (observers == PlayerColor.None)
        {
            return 0;
        }

        int revealed = world.ClearFog(observers);

        if (focus is not null)
        {
            revealed += world.ClearFog(
                (focus.Y * world.Width) + focus.X, MapFog.CastleRevealDistance, observers);
        }

        return revealed;
    }

    /// <summary>
    /// Paints the frame, the visible terrain, the icons of the panel, the minimap and the status bar.
    /// </summary>
    /// <remarks>
    /// 整块屏幕现在由 <c>Interface::AdventureMap::redraw</c>（端口的 <see cref="AdventureInterface.RedrawInterface"/>）画：
    /// 它按参考的次序铺边框、画地图区、雷达、图标条、按钮与石头状态面板。场景只做两件它不管的事——把索引缓冲标脏
    /// （像素直写报告不了自己）、再叠上两个浮层。
    /// </remarks>
    public void DrawFrame(in DiagnosticsState state)
    {
        _ = state;

        // The View World window owns the whole display, the way the reference draws it straight into
        // the screen buffer and covers the adventure map with its side panel.
        if (_viewWorld is { } viewWorld)
        {
            viewWorld.Draw();

            return;
        }

        // The map is written straight into the indexed pixels, which cannot report the change, so
        // the whole buffer has to be marked dirty or the presenter would skip the upload.
        _frame.InvalidateAll();
        _interface.RedrawInterface(Redraw.All);

        // The outline of a grabbed panel goes over the map and under the dialogs, the order the reference
        // draws it in inside its drag loop.
        _drag?.Draw(_frame.Pixels);

        // The options dialog is painted over the map that is already there, the way the reference
        // draws its blocking dialog straight into the display.
        if (_options is { } options)
        {
            options.Draw();
        }

        // The quick info goes last: it is a tooltip over everything, including the terrain it names.
        _quickInfo?.Draw(_frame.Pixels);
    }

    /// <summary>The adventure map draws all of its text with the game fonts.</summary>
    public void DrawOverlay(in DiagnosticsState state)
    {
    }

    /// <summary>Scrolls the map area by map pixels, clamped to the map, <c>GameArea::ShiftCenter()</c>.</summary>
    public void ScrollBy(int dx, int dy) => _interface.GetGameArea().ShiftCenter(new Point(dx, dy));

    /// <summary>
    /// Asks the host for the town screen of <paramref name="castle"/>, the <c>Game::OpenCastleDialog()</c> the
    /// reference opens when the left button lands on a town of the acting kingdom.
    /// </summary>
    /// <remarks>
    /// 参考里 <c>MouseCursorAreaClickLeft</c> 直接调阻塞的 <c>Game::OpenCastleDialog</c>；端口的城堡屏是宿主的场景，
    /// 所以屏幕把这个调用转成一次请求（<see cref="AdventureInterface.OpenCastleScreen"/>），场景记下来交给宿主
    /// （<see cref="TakeCastleRequest"/>）。点地形铺路、面板与雷达的点击都在屏幕里，不经过这里。
    /// </remarks>
    /// <param name="castle">The town to open.</param>
    /// <returns>Always <see langword="true"/>: the request is queued for the host.</returns>
    public bool RequestCastleScreen(CastleTown castle)
    {
        _castleRequest = castle;

        return true;
    }

    /// <summary>
    /// Ends the turn of the current player, the END TURN button and the <c>E</c> key of the reference.
    /// </summary>
    /// <remarks>
    /// 会话换人或换天之后，屏幕有三样东西要跟着换：当前色（界面焦点、状态石与雷达都按它取数）、焦点英雄与视野中心。
    /// 参考在 <c>HumanTurn()</c> 的收尾里靠 <c>Interface::Basic::SetFocus()</c> 与状态栏自己的 <c>Reset()</c>
    /// 做到，端口没有换幕的黑板，所以在这里一次推完。
    /// </remarks>
    public void EndTurn()
    {
        if (_session is null)
        {
            return;
        }

        _session.EndTurn();

        FHeroes2.Game.System.Settings.Get().Players.SetCurrentColor(_session.CurrentColor);

        if (_session.FocusHero is { } hero)
        {
            _interface.SetFocus(hero, false);
        }
        else
        {
            CentreOnFocusHero();
        }

        GameLog.Info($"End turn: day {_session.DayCount}, {_session.CurrentColor} plays.");
    }

    /// <summary>
    /// Takes the town a click asked to open, if any; the request is consumed.
    /// </summary>
    /// <remarks>
    /// 场景不自己开城：城堡屏是另一个场景，由宿主换过去再换回来，所以这里只交出一座城的引用。
    /// </remarks>
    /// <returns>The town to open, or <see langword="null"/> when nothing was clicked.</returns>
    public CastleTown? TakeCastleRequest()
    {
        CastleTown? castle = _castleRequest;
        _castleRequest = null;

        return castle;
    }

    /// <summary>
    /// Builds the town screen of a castle, <c>Castle::OpenDialog()</c>.
    /// </summary>
    /// <param name="castle">The town to show.</param>
    /// <returns>The screen, or <see langword="null"/> when the fonts or a frame of the screen are missing.</returns>
    public CastleScene? TryOpenCastle(CastleTown castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        if (_fonts is null)
        {
            GameLog.Warn("The town screen needs the game fonts, and they are missing.");

            return null;
        }

        return CastleScene.TryCreate(_frame, _world, castle, _session?.Kingdom, _assets, _fonts);
    }

    /// <summary>
    /// Starts the battle of the focus hero against the first wandering monster of the map, the
    /// <c>--battle</c> of the command line.
    /// </summary>
    /// <param name="freezeAnimation">Whether the fight stands still, for a screenshot.</param>
    /// <param name="siege">Whether to attack a town instead of a wandering monster.</param>
    /// <param name="autoPlay">Whether both sides are handed to the computer, <c>--battle-auto</c>.</param>
    /// <returns>The battle screen, or <see langword="null"/> when the map has no hero or no monster.</returns>
    /// <remarks>
    /// 参考的仗是「英雄走到怪物格上」触发的（<c>heroes_action.cpp</c> 的 <c>ActionToMonster</c> 里调
    /// <c>Battle::Loader</c>）；端口的人类的动作链还没接上界面宿主（<c>bd 归档（PROGRESS.md，FHeroes2Sharp-jwk）</c> §7.4），
    /// 所以这一步先让命令行挑一场真打，把战斗画面本身做出来。
    /// </remarks>
    public BattleScene? TryStartBattle(bool freezeAnimation, bool siege, bool autoPlay)
    {
        if (_session?.FocusHero is not { } hero)
        {
            GameLog.Warn("--battle: the map has no acting hero to fight with.");

            return null;
        }

        BattleScene? scene = BattleScene.TryCreate(
            _frame, _assets, _world, hero, freezeAnimation, siege, autoPlay, _fonts?.SmallWhite, _fonts?.White,
            _fonts?.Small, _fonts, out string? error);

        if (scene is null)
        {
            GameLog.Warn($"--battle: the battle cannot be started: {error}.");
        }

        return scene;
    }

    /// <summary>
    /// Builds the town screen of a town of the acting kingdom, the <c>--enter-town</c> of the command line.
    /// </summary>
    /// <param name="index">Which town of the kingdom to show, in the order of <c>Kingdom.Castles</c>.</param>
    /// <returns>The screen, or <see langword="null"/> when it cannot be built.</returns>
    public CastleScene? TryEnterTown(int index = 0)
    {
        if (_session is not { } session)
        {
            GameLog.Warn("--enter-town: the map has no acting kingdom (no human player).");

            return null;
        }

        if (session.Kingdom.Castles.Count == 0)
        {
            GameLog.Warn($"--enter-town: the acting kingdom ({session.CurrentColor}) owns no town.");

            return null;
        }

        if (index < 0 || index >= session.Kingdom.Castles.Count)
        {
            GameLog.Warn($"--enter-town: the acting kingdom owns {session.Kingdom.Castles.Count} towns, there is no #{index}.");

            return null;
        }

        CastleTown castle = session.Kingdom.Castles[index];

        if (TryOpenCastle(castle) is { } scene)
        {
            return scene;
        }

        GameLog.Warn($"--enter-town: the screen of '{castle.Name}' ({castle.Color}) cannot be built.");

        return null;
    }

    /// <summary>Rolls the map with the wheel by whole tiles, one tile per notch.</summary>
    /// <param name="columns">Tiles to scroll right; negative scrolls left.</param>
    /// <param name="rows">Tiles to scroll down; negative scrolls up.</param>
    public void ScrollByWheel(int columns, int rows)
    {
        GameArea gameArea = _interface.GetGameArea();

        gameArea.SetCenterInPixels(
            gameArea.CurrentCenterInPixels
            + new Point(columns * _view.Tiles.TileWidth, rows * _view.Tiles.TileHeight));
    }

    /// <summary>Centres the map on the focus hero, the <c>SetCenter()</c> of a new turn.</summary>
    public void CentreOnFocusHero()
    {
        if (_session?.FocusHero is { } hero)
        {
            CentreOnHero(hero);
        }
    }

    /// <summary>Centres the map on the tile <paramref name="hero"/> stands on.</summary>
    /// <param name="hero">The hero to follow.</param>
    private void CentreOnHero(Hero hero)
    {
        if (hero.MapIndex < 0)
        {
            return;
        }

        int width = _view.Map.Width;
        _interface.GetGameArea().SetCenter(new Point(hero.MapIndex % width, hero.MapIndex / width));
    }
}
