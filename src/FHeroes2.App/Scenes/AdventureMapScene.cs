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
    private readonly AdventureMapView _view;
    private readonly Minimap _minimap;
    private readonly MapScrollController _mouse;
    private readonly MapFrame _mapFrame;
    private readonly IconBar _iconBar;
    private readonly IconSpriteSet? _iconSprites;
    private readonly ButtonsPanel _buttons;
    private readonly ButtonSpriteSet? _buttonSprites;
    private readonly Rect _viewport;
    private readonly PixelBuffer _viewportPixels;
    private readonly Rect _radar;
    private readonly Rect _statusBar;
    private readonly byte _dark;

    private bool _leftHeld;
    private bool _radarGrabbed;

    private AdventureMapScene(
        ScreenBuffer frame,
        GameFonts? fonts,
        World world,
        AdventureMapView view,
        Minimap minimap,
        MapFrame mapFrame,
        IconSpriteSet? iconSprites,
        ButtonSpriteSet? buttonSprites)
    {
        _frame = frame;
        _fonts = fonts;
        _world = world;
        _view = view;
        _minimap = minimap;
        _mouse = new MapScrollController(view);
        _mapFrame = mapFrame;
        _iconBar = new IconBar(mapFrame.Panel);
        _iconSprites = iconSprites;
        _buttons = new ButtonsPanel(mapFrame.Panel);
        _buttonSprites = buttonSprites;
        _viewport = mapFrame.Viewport;
        _viewportPixels = new PixelBuffer(_viewport.Width, _viewport.Height);
        _radar = mapFrame.Radar;

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

            // Both sets share one decoder cache, so keeping them apart is only about what they map.
            AdventureMapView view = new(
                map,
                tiles,
                mapFrame.Viewport.Size,
                objects,
                HeroSpriteSet.TryCreate(assets),
                MonsterSpriteSet.TryCreate(assets),
                BoatSpriteSet.TryCreate(assets),
                fog,
                fogSprites);

            Minimap minimap = Minimap.Build(map);

            if (focus is not null)
            {
                view.CenterOn(focus.X, focus.Y);
            }

            AdventureMapScene scene = new(
                frame,
                fonts,
                world,
                view,
                minimap,
                mapFrame,
                IconSpriteSet.TryCreate(assets),
                ButtonSpriteSet.TryCreate(assets));

            GameLog.Info(
                $"Adventure map opened: '{map.Info.Name}' ({map.Width}x{map.Height}, " +
                $"{scene.View.MapPixelSize.Width}x{scene.View.MapPixelSize.Height} pixels, " +
                $"{map.Castles.Count} castles, {map.Addons.Count} add-ons, " +
                $"{scouted} tiles scouted, {scene.View.Fog?.CountFogged() ?? 0} under the fog).");

            return scene;
        }
        catch (Exception exception) when (exception is BinaryFormatException or IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            GameLog.Error($"The map '{mapInfo.FilePath}' could not be opened", exception);
            return null;
        }
    }

    /// <summary>Advances the edge scroll of the mouse; the terrain itself does not animate yet.</summary>
    public void Update(double elapsedSeconds) => _mouse.Update(elapsedSeconds);

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

        // A scenario without a kingdom has no focused hero, which is the branch of _setButtonStatus
        // that greys out the movement, the spell and the next hero button.
        _buttons.Draw(_frame.Pixels, _buttonSprites, AdventureButtonStatus.WithoutHero);

        // Tiles the map does not cover (only possible at the edges) stay black, which is the colour
        // of index 0 of KB.PAL and matches the empty border of the reference.
        _viewportPixels.Clear();
        _view.Draw(_viewportPixels);
        ImageOps.Blit(_frame.Pixels, _viewport.Position, _viewportPixels, _viewportPixels.Bounds);

        DrawMinimap();
        DrawStatusBar(state);
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
    /// Records the left button. A press on the minimap is consumed by the radar, everything else is
    /// the drag of the terrain, and a release ends both.
    /// </summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down)
    {
        _leftHeld = down;

        if (down && _mouse.Cursor is { } cursor && _radar.Contains(cursor))
        {
            _radarGrabbed = true;
            CentreOnRadar(cursor);
            return;
        }

        if (!down)
        {
            _radarGrabbed = false;
        }

        _mouse.SetLeftButton(down);
    }

    /// <summary>Rolls the map with the wheel by whole tiles, one tile per notch.</summary>
    /// <param name="columns">Tiles to scroll right; negative scrolls left.</param>
    /// <param name="rows">Tiles to scroll down; negative scrolls up.</param>
    public void ScrollByWheel(int columns, int rows) => _mouse.ScrollByWheel(columns, rows);

    /// <summary>
    /// Centres the map on the tile under a point of the minimap, <c>GameArea::SetCenter( tile )</c>
    /// of the reference; the clamp of the view keeps points outside the panel harmless.
    /// </summary>
    private void CentreOnRadar(Point cursor)
    {
        Point tile = _minimap.TileAt(new Point(cursor.X - _radar.X, cursor.Y - _radar.Y));
        _view.CenterOn(tile.X, tile.Y);
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

        string left = $"{MapName}  {_view.Map.Width}x{_view.Map.Height}";
        int width = Math.Max(rightX - StatusBarGap - leftEdge, 0);
        if (width <= 0)
        {
            return;
        }

        Rect line = new(leftEdge, top, width, _statusBar.Height);
        GlyphText.DrawLine(font, _frame.Pixels, new Point(line.X, top), left, line);
    }
}
