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
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Interface;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The adventure map of a started scenario, milestone M4. It shows the ground and object layers of
/// the selected MP2 map, scrolls with the arrow keys and reports the map and the viewport in a
/// status bar.
///
/// <para>
/// The scene is deliberately thin: <see cref="AdventureMapView"/> knows how a tile becomes pixels,
/// <see cref="MapFrame"/> owns the interface frame (<c>ADVBORD.ICN</c>, the 448x448 viewport, the radar
/// bay and the panel bay), and the scene only puts them together. <see cref="IconBar"/> fills the top
/// of the panel bay with the castle icons of the map and <see cref="ButtonsPanel"/> puts the eight
/// interface buttons below them. The fog of war closes the map passes (<see cref="MapFog"/> over
/// <c>CLOF32.TIL</c> / <c>CLOP32.ICN</c>), the resource bar, paths and the fog free reveal are later
/// milestones.
/// The reference composition (<c>Maps::redrawBottomLayerObjects</c> followed by the top layer, then the
/// interface) is what those will plug into.
/// </para>
/// <para>
/// The fog is <b>not</b> the scene's own: the scene loads the map into <see cref="World.Current"/> and
/// shows <see cref="World.Fog"/>, so the table the renderer walks is the one the game rules change
/// (ADR-131).
/// </para>
/// </summary>
public sealed class AdventureMapScene : IScene
{
    /// <summary>Height of the status bar, one line of <c>SMALFONT.ICN</c>.</summary>
    public const int StatusBarHeight = 11;

    /// <summary>Distance between the minimap and the top right corner, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int RadarMargin = MapFrame.BorderWidth;

    private const int StatusBarPadding = 8;

    private const int StatusBarGap = 10;

    private readonly ScreenBuffer _frame;
    private readonly GameFonts? _fonts;
    private readonly World _world;
    private readonly AdventureSession? _session;
    private readonly AdventureMapView _view;
    private readonly Minimap _minimap;
    private readonly MapScrollController _mouse;
    private readonly MapFrame _mapFrame;
    private readonly IconBar _iconBar;
    private readonly IconSpriteSet? _iconSprites;
    private readonly ButtonsPanel _buttons;
    private readonly ButtonSpriteSet? _buttonSprites;
    private readonly StatusPanel? _statusPanel;
    private readonly Rect _statusArea;
    private readonly Rect _viewport;
    private readonly PixelBuffer _viewportPixels;
    private readonly Rect _radar;
    private readonly Rect _statusBar;
    private readonly byte _dark;

    private bool _leftHeld;
    private bool _radarGrabbed;

    private StatusPanelState _statusState = StatusPanelState.Day;

    private readonly IViewWorldSpriteSource? _viewWorldSprites;
    private readonly PlayerColor _friends;

    private ViewWorldOverlay? _viewWorld;
    private ZoomLevel _viewWorldZoom = ZoomLevel.ZoomLevel2;

    private readonly AdventureOptionsView? _optionsArtwork;
    private AdventureOptionsOverlay? _options;

    private AdventureMapScene(
        ScreenBuffer frame,
        GameFonts? fonts,
        World world,
        AdventureSession? session,
        AdventureMapView view,
        Minimap minimap,
        MapFrame mapFrame,
        IconSpriteSet? iconSprites,
        ButtonSpriteSet? buttonSprites,
        StatusPanel? statusPanel,
        AdventureOptionsView? optionsArtwork,
        IViewWorldSpriteSource? viewWorldSprites,
        PlayerColor friends)
    {
        _frame = frame;
        _fonts = fonts;
        _world = world;
        _session = session;
        _view = view;
        _minimap = minimap;
        _mouse = new MapScrollController(view);
        _mapFrame = mapFrame;
        _iconBar = new IconBar(mapFrame.Panel);
        _iconSprites = iconSprites;
        _buttons = new ButtonsPanel(mapFrame.Panel);
        _buttonSprites = buttonSprites;
        _statusPanel = statusPanel;
        _optionsArtwork = optionsArtwork;
        _viewWorldSprites = viewWorldSprites;
        _friends = friends;
        _viewport = mapFrame.Viewport;
        _viewportPixels = new PixelBuffer(_viewport.Width, _viewport.Height);
        _radar = mapFrame.Radar;

        // The stone panel hangs under the buttons down to the bottom border, the _statusPanel.SetPosition
        // of the reference (game_interface.cpp), which leaves 480 - 392 - 16 = 72 pixels, one frame of
        // STONBACK.ICN.
        _statusArea = StatusPanel.AreaFor(mapFrame.Panel, _buttons.Area, frame.Size.Height, MapFrame.BorderWidth);

        // The status line goes on the bottom border, where the frame art leaves the strip empty; the
        // panel bay above it belongs to the interface buttons of later milestones.
        _statusBar = new Rect(
            mapFrame.BottomBorder.X,
            mapFrame.BottomBorder.Y + 2,
            mapFrame.BottomBorder.Width,
            StatusBarHeight);
        _dark = frame.Palette.FindDarkestIndex();
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

    /// <summary>The minimap panel in the top right corner.</summary>
    public Minimap Minimap => _minimap;

    /// <summary>Where the minimap is drawn, the radar area of the reference layout.</summary>
    public Rect RadarArea => _radar;

    /// <summary>Name of the map, as stored in its header.</summary>
    public string MapName => _view.Map.Info.Name;

    /// <summary>
    /// The status bar rectangle: a strip of the bottom border of the frame, which the artwork leaves
    /// empty.
    /// </summary>
    public Rect StatusBarArea => _statusBar;

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
            World world = World.Current;
            world.LoadMap(map);

            // The fog is drawn for the colors the map declares as human, like Players::FriendColors() in the
            // reference: a tile is fogged only when every one of them has not discovered it yet. A scenario
            // with no human player at all (a map only meant for the editor) stays fog free.
            PlayerColor observers = map.Info.HumanPlayers;
            int scouted = ScoutStart(world, observers, focus);
            MapFog? fog = observers == PlayerColor.None ? null : world.Fog;
            FogSpriteSet? fogSprites = FogSpriteSet.TryLoad(assets);

            // The turn loop of the reference's StartGame(): day one, kingdoms' income, the focus hero. A map
            // without a human player returns null and stays a viewer, which is what the scene was until the
            // session existed.
            AdventureSession? session = AdventureSession.Start(world, map.Info);

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

            Minimap minimap = Minimap.Build(map);

            if (focus is not null)
            {
                view.CenterOn(focus.X, focus.Y);
            }

            AdventureMapScene scene = new(
                frame,
                fonts,
                world,
                session,
                view,
                minimap,
                mapFrame,
                IconSpriteSet.TryCreate(assets),
                ButtonSpriteSet.TryCreate(assets),
                StatusPanel.TryCreate(assets),
                AdventureOptionsArtwork.TryCreate(assets),
                assets is null ? null : new IcnViewWorldSpriteSource(assets),
                observers);

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

    /// <summary>Advances the edge scroll of the mouse and the walk animation of the heroes.</summary>
    public void Update(double elapsedSeconds)
    {
        _mouse.Update(elapsedSeconds);

        if (_session is null)
        {
            return;
        }

        // The reference's main loop calls Heroes::Move() once per frame, only for the heroes whose movement
        // is enabled; Move() advances the sprite by one frame and, when the sprite reaches the last frame of
        // the step, lands the hero on the next tile. The step-by-step position inside a tile is not a field
        // of the hero: it is a function of the sprite frame, which is why the drawing reads SpriteIndex and
        // Offset from the hero and never keeps its own copy.
        foreach (Hero hero in _world.Heroes)
        {
            if (!hero.IsMoveEnabled)
            {
                continue;
            }

            bool stepped = hero.Move(false, _world);
            hero.Offset = hero.GetCurrentPixelOffset();

            // The view follows the acting hero, GameArea::SetCenter( hero->GetCenter() ) after each step.
            if (stepped && ReferenceEquals(hero, _session.FocusHero))
            {
                CentreOnHero(hero);
            }
        }
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
    public void DrawFrame(in DiagnosticsState state)
    {
        // The View World window owns the whole display, the way the reference draws it straight into
        // the screen buffer and covers the adventure map with its side panel.
        if (_viewWorld is { } viewWorld)
        {
            viewWorld.Draw();

            return;
        }

        // The frame is painted first and the map on top of it: ADVBORD already holds the border, the
        // radar bay and the panel bay, so the only work left is to put the map between them.
        // The map is written straight into the indexed pixels, which cannot report the change, so
        // the whole buffer has to be marked dirty or the presenter would skip the upload.
        _frame.InvalidateAll();
        _mapFrame.Draw(_frame.Pixels, _dark);
        _mapFrame.DrawPanel(_frame.Pixels);

        // The icons of the panel go on top of the brick: the bar starts at 176, one border below the
        // radar, which is where the brick starts as well.
        _iconBar.Draw(_frame.Pixels, _iconSprites, _view.Map.Castles);

        // The three status buttons follow the focus hero, the rest of ButtonsPanel::_setButtonStatus.
        _buttons.Draw(_frame.Pixels, _buttonSprites, CurrentButtonStatus());

        // The stone panel closes the column under the buttons, Interface::StatusPanel::_redraw().
        DrawStatusPanel();

        // Tiles the map does not cover (only possible at the edges) stay black, which is the colour
        // of index 0 of KB.PAL and matches the empty border of the reference.
        _viewportPixels.Clear();
        _view.Draw(_viewportPixels);
        ImageOps.Blit(_frame.Pixels, _viewport.Position, _viewportPixels, _viewportPixels.Bounds);

        DrawMinimap();
        DrawStatusBar(state);

        // The options dialog is painted over the map that is already there, the way the reference
        // draws its blocking dialog straight into the display.
        if (_options is { } options)
        {
            options.Draw();
        }
    }

    /// <summary>The adventure map draws all of its text with the game fonts.</summary>
    public void DrawOverlay(in DiagnosticsState state)
    {
    }

    /// <summary>Scrolls the view by map pixels, clamped to the map.</summary>
    public void ScrollBy(int dx, int dy) => _view.ScrollBy(dx, dy);

    /// <summary>Scrolls the view by whole tiles, the arrow keys of the reference.</summary>
    public void ScrollByTiles(int columns, int rows) =>
        _view.ScrollBy(columns * _view.Tiles.TileWidth, rows * _view.Tiles.TileHeight);

    /// <summary>scrolls the view back to the top left corner, which is where a scenario starts without heroes.</summary>
    public void ScrollToStart() => _view.ScrollTo(0, 0);

    /// <summary>The mouse of the terrain: drag, edge scroll and wheel.</summary>
    public MapScrollController Mouse => _mouse;

    /// <summary>
    /// Records the pointer. A press that started on the minimap keeps centring the map while it is
    /// held, which is the <c>MouseClickLeft || isMouseLeftButtonPressedInArea</c> branch of
    /// <c>Radar::QueueEventProcessing</c>: one branch for the click and for the drag across the panel.
    /// </summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor)
    {
        if (_radarGrabbed && _leftHeld && cursor is { } point && _radar.Contains(point))
        {
            CentreOnRadar(point);
        }

        _mouse.SetCursor(cursor);
    }

    /// <summary>
    /// Records the left button. A press on the minimap is consumed by the radar, a press on the button
    /// panel by the buttons and a press on the terrain lays the route of the focus hero; everything else
    /// is the drag of the terrain, and a release ends both.
    /// </summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down)
    {
        _leftHeld = down;

        if (down && _mouse.Cursor is { } cursor)
        {
            if (_radar.Contains(cursor))
            {
                _radarGrabbed = true;
                CentreOnRadar(cursor);
                return;
            }

            if (ClickPanel(cursor))
            {
                return;
            }

            // A press on the stone cycles the state, Interface::StatusPanel::QueueEventProcessing(): the date,
            // then the kingdom, then back again. The army state is not drawn yet, so it is not in the cycle.
            if (_statusPanel is not null && _statusArea.Contains(cursor))
            {
                _statusState = _statusState == StatusPanelState.Day ? StatusPanelState.Funds : StatusPanelState.Day;

                return;
            }

            // The reference's MouseLeftClick on the terrain: the focus hero gets the route of world.getPath()
            // and starts walking. Without a session (a map with no human player) the press stays a drag.
            if (_session?.FocusHero is { } hero
                && _viewport.Contains(cursor)
                && TrySetHeroPath(hero, cursor))
            {
                return;
            }
        }

        if (!down)
        {
            _radarGrabbed = false;
        }

        _mouse.SetLeftButton(down);
    }

    /// <summary>
    /// Ends the turn of the current player, the END TURN button and the <c>E</c> key of the reference.
    /// </summary>
    /// <remarks>
    /// The session changes the player or the day; the scene only follows up on the map (recentring on the
    /// hero of the player who is now acting) and on the log, because the next redraw asks the session for
    /// the new buttons and the new status bar.
    /// </remarks>
    public void EndTurn()
    {
        if (_session is null)
        {
            return;
        }

        _session.EndTurn();
        CentreOnFocusHero();

        GameLog.Info($"End turn: day {_session.DayCount}, {_session.CurrentColor} plays.");
    }

    /// <summary>
    /// Moves the focus to the next hero of the acting kingdom, the next hero button and the <c>TAB</c> key.
    /// </summary>
    public void NextHero()
    {
        if (_session is null)
        {
            return;
        }

        if (_session.NextHero() is null)
        {
            GameLog.Info("The acting kingdom has no hero to focus.");

            return;
        }

        CentreOnFocusHero();
    }

    /// <summary>
    /// Reacts to a left press on the button panel: the two buttons the port can already honour do their
    /// job, the rest report that they are not wired up yet.
    /// </summary>
    /// <param name="cursor">Pointer in logical pixels.</param>
    /// <returns><see langword="true"/> when the press landed on a button.</returns>
    public bool ClickPanel(Point cursor)
    {
        if (!_buttons.Area.Contains(cursor))
        {
            return false;
        }

        foreach (AdventureButton button in Enum.GetValues<AdventureButton>())
        {
            if (!_buttons.AreaOf(button).Contains(cursor))
            {
                continue;
            }

            switch (button)
            {
                case AdventureButton.NextHero:
                    NextHero();
                    break;

                case AdventureButton.EndTurn:
                    EndTurn();
                    break;

                default:
                    GameLog.Info($"The '{button}' button is not wired up yet.");
                    break;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// What <see cref="ButtonsPanel"/> should show, <c>Interface::ButtonsPanel::_setButtonStatus</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference asks the focus hero four things: whether the route is valid (movement glyph), whether
    /// he may still walk it (<c>MayStillMove</c>), whether the object under him may be visited again (the
    /// action glyph) and whether he carries a spell book and has spell points (the spell button). The port
    /// keeps the same four answers and drops only the "hero is asleep" case, which needs a mode the port
    /// has not modelled.
    /// </para>
    /// <para>
    /// The next hero button is enabled when some hero of the kingdom can still move, which is what the
    /// reference's <c>_getNextHeroIndex( currentHero )</c> does by ending the search at that hero.
    /// </para>
    /// </remarks>
    /// <returns>The state of the three conditional buttons.</returns>
    public AdventureButtonStatus CurrentButtonStatus()
    {
        if (_session is not { } session || session.FocusHero is not { } hero)
        {
            return AdventureButtonStatus.WithoutHero;
        }

        bool hasRoute = hero.Path.IsValidForMovement(_world.Size);
        bool canWalk = hasRoute && hero.IsActive && hero.Path.HasAllowedSteps(hero.MovePoints);
        bool canVisit = !hasRoute
            && hero.IsActive
            && Mp2ObjectTypes.IsRevisitAllowedForObject(_world.GetTile(hero.MapIndex).MainObjectType, false);

        HeroMovementIcon movement = hasRoute
            ? (canWalk ? HeroMovementIcon.Walk : HeroMovementIcon.Reset)
            : (canVisit ? HeroMovementIcon.Action : HeroMovementIcon.Reset);

        bool hasMobileHero = false;
        foreach (Hero kingdomHero in session.Kingdom.Heroes)
        {
            if (kingdomHero.IsActive && kingdomHero.MovePoints > 0)
            {
                hasMobileHero = true;
                break;
            }
        }

        return new AdventureButtonStatus(
            movement,
            IsMovementEnabled: canWalk || canVisit,
            IsSpellEnabled: hero.IsActive && hero.HasSpellBook && hero.SpellPoints > 0,
            IsNextHeroEnabled: hasMobileHero);
    }

    /// <summary>Rolls the map with the wheel by whole tiles, one tile per notch.</summary>
    /// <param name="columns">Tiles to scroll right; negative scrolls left.</param>
    /// <param name="rows">Tiles to scroll down; negative scrolls up.</param>
    public void ScrollByWheel(int columns, int rows) => _mouse.ScrollByWheel(columns, rows);

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
        _view.CenterOn(hero.MapIndex % width, hero.MapIndex / width);
    }

    /// <summary>
    /// Lays the route of <paramref name="hero"/> to the tile under <paramref name="cursor"/>.
    /// </summary>
    /// <param name="hero">The focus hero.</param>
    /// <param name="cursor">Pointer in logical pixels.</param>
    /// <returns><see langword="true"/> when the press landed on a tile of the map.</returns>
    private bool TrySetHeroPath(Hero hero, Point cursor)
    {
        Point tile = _view.TileAt(new Point(cursor.X - _viewport.X, cursor.Y - _viewport.Y));

        if (tile.X < 0 || tile.Y < 0 || tile.X >= _view.Map.Width || tile.Y >= _view.Map.Height)
        {
            return false;
        }

        return _session!.SetPath(hero, (tile.Y * _view.Map.Width) + tile.X);
    }

    /// <summary>
    /// Centres the map on the tile under a point of the minimap, <c>GameArea::SetCenter( tile )</c>
    /// of the reference; the clamp of the view keeps points outside the panel harmless.
    /// </summary>
    private void CentreOnRadar(Point cursor)
    {
        Point tile = _minimap.TileAt(new Point(cursor.X - _radar.X, cursor.Y - _radar.Y));
        _view.CenterOn(tile.X, tile.Y);
    }

    /// <summary>
    /// Paints the stone panel: the date, or the kingdom's counts, <c>Interface::StatusPanel::_redraw()</c>.
    /// </summary>
    /// <remarks>
    /// The panel shows nothing but the stone before the first day of the game, which is the reference's
    /// <c>world.CountDay() == 0</c> early return. The kingdom counts need a started session; a viewer scene
    /// (a map without a human player) still has the calendar, so only the funds are left at zero.
    /// </remarks>
    private void DrawStatusPanel()
    {
        if (_statusPanel is null || _fonts is null)
        {
            return;
        }

        StatusPanelState state = _world.DayCount == 0 ? StatusPanelState.Unknown : _statusState;
        StatusPanelDate date = new((int)_world.Month, (int)_world.Week, (int)_world.Day);

        StatusPanelKingdom kingdom = _session is { } session
            ? new StatusPanelKingdom(
                session.Kingdom.CountCastles(), session.Kingdom.CountTowns(), session.Kingdom.Treasury)
            : default;

        _statusPanel.Draw(_frame.Pixels, _statusArea, state, date, kingdom, _fonts.Small, _fonts.Normal);
    }

    private void DrawMinimap()
    {
        // The radar bay of the frame is exactly the size of the minimap, so the picture covers it; the
        // one pixel rectangle is what the reference draws around it as well.
        _minimap.Draw(_frame.Pixels, _radar.Position, _view.VisibleTileArea);
        ImageOps.DrawRect(_frame.Pixels, _radar, Minimap.BlackColor);
    }

    private void DrawStatusBar(in DiagnosticsState state)
    {
        // The line sits on the bottom border of the frame, over a dark strip of its own: the border art
        // is too light for SMALFONT on its own, and this strip is the only row the frame leaves free.
        ImageOps.FillRect(_frame.Pixels, _statusBar, _dark);

        if (_fonts is null)
        {
            return;
        }

        IGlyphSource font = _fonts.Small;
        int top = _statusBar.Y;
        int leftEdge = _statusBar.X + StatusBarPadding;

        string right = FormattableString.Invariant(
            $"viewport {_view.Scroll.X},{_view.Scroll.Y}  |  v{BuildInfo.Version}");
        int rightX = Math.Max(
            _statusBar.Right - StatusBarPadding - GlyphText.Measure(font, right), leftEdge);
        GlyphText.DrawLine(font, _frame.Pixels, new Point(rightX, top), right);

        // With a started game the status line carries the two things the player needs at a glance in the
        // reference: the day (StatusPanel's date) and whose turn it is (the flag next to it). A viewer map
        // keeps the size instead, because it has neither.
        string left = _session is { } session
            ? $"{MapName}  day {session.DayCount}  {session.CurrentColor}"
            : $"{MapName}  {_view.Map.Width}x{_view.Map.Height}";
        int width = Math.Max(rightX - StatusBarGap - leftEdge, 0);
        if (width <= 0)
        {
            return;
        }

        Rect line = new(leftEdge, top, width, _statusBar.Height);
        GlyphText.DrawLine(font, _frame.Pixels, new Point(line.X, top), left, line);
    }
}
