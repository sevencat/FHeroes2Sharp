using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The adventure map of a started scenario, milestone M4. It shows the ground and object layers of
/// the selected MP2 map, scrolls with the arrow keys and reports the map and the viewport in a
/// status bar.
///
/// <para>
/// The scene is deliberately thin: <see cref="AdventureMapView"/> knows how a tile becomes pixels,
/// the scene only owns the frame, the status bar and the keyboard. Fog, heroes and the interface
/// panels are later milestones; the reference composition (<c>Maps::redrawBottomLayerObjects</c>
/// followed by the top layer, then the interface) is what those will plug into.
/// </para>
/// </summary>
public sealed class AdventureMapScene : IScene
{
    /// <summary>Height of the status bar, one line of <c>SMALFONT.ICN</c>.</summary>
    public const int StatusBarHeight = 11;

    /// <summary>Distance between the minimap and the top right corner, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int RadarMargin = 16;

    private const int StatusBarPadding = 8;

    private const int StatusBarGap = 10;

    private readonly ScreenBuffer _frame;
    private readonly GameFonts? _fonts;
    private readonly AdventureMapView _view;
    private readonly Minimap _minimap;
    private readonly MapScrollController _mouse;
    private readonly Rect _radar;
    private readonly Rect _statusBar;
    private readonly byte _statusBarColor;

    private bool _leftHeld;
    private bool _radarGrabbed;

    private AdventureMapScene(ScreenBuffer frame, GameFonts? fonts, AdventureMapView view, Minimap minimap)
    {
        _frame = frame;
        _fonts = fonts;
        _view = view;
        _minimap = minimap;
        _mouse = new MapScrollController(view);
        _radar = new Rect(
            frame.Size.Width - RadarMargin - Minimap.PanelSize,
            RadarMargin,
            Minimap.PanelSize,
            Minimap.PanelSize);
        _statusBar = new Rect(0, frame.Size.Height - StatusBarHeight, frame.Size.Width, StatusBarHeight);
        _statusBarColor = frame.Palette.FindDarkestIndex();
    }

    /// <summary>The map the scene draws.</summary>
    public Mp2Map Map => _view.Map;

    /// <summary>Scrolling view of the terrain.</summary>
    public AdventureMapView View => _view;

    /// <summary>The minimap panel in the top right corner.</summary>
    public Minimap Minimap => _minimap;

    /// <summary>Where the minimap is drawn, the radar area of the reference layout.</summary>
    public Rect RadarArea => _radar;

    /// <summary>Name of the map, as stored in its header.</summary>
    public string MapName => _view.Map.Info.Name;

    /// <summary>The status bar rectangle at the bottom of the logical frame.</summary>
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

            // Both sets share one decoder cache, so keeping them apart is only about what they map.
            AdventureMapView view = new(map, tiles, frame.Size, objects, HeroSpriteSet.TryCreate(assets));
            Minimap minimap = Minimap.Build(map);

            // Without heroes there is no player position to centre on yet; the castle closest to the
            // middle of the map takes that role, which is what makes the first screenshot of a map
            // show its heart instead of an empty corner.
            if (map.Castles.Count > 0)
            {
                int middleX = map.Width / 2;
                int middleY = map.Height / 2;

                Mp2CastlePosition nearest = map.Castles
                    .OrderBy(castle => ((castle.X - middleX) * (castle.X - middleX))
                                       + ((castle.Y - middleY) * (castle.Y - middleY)))
                    .First();

                view.CenterOn(nearest.X, nearest.Y);
            }

            AdventureMapScene scene = new(frame, fonts, view, minimap);

            GameLog.Info(
                $"Adventure map opened: '{map.Info.Name}' ({map.Width}x{map.Height}, " +
                $"{scene.View.MapPixelSize.Width}x{scene.View.MapPixelSize.Height} pixels, " +
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

    /// <summary>Advances the edge scroll of the mouse; the terrain itself does not animate yet.</summary>
    public void Update(double elapsedSeconds) => _mouse.Update(elapsedSeconds);

    /// <summary>Paints the visible terrain, the minimap and the status bar.</summary>
    public void DrawFrame(in DiagnosticsState state)
    {
        // Tiles the map does not cover (only possible at the edges) stay black, which is the colour
        // of index 0 of KB.PAL and matches the empty border of the reference.
        // The map is written straight into the indexed pixels, which cannot report the change, so
        // the whole buffer has to be marked dirty or the presenter would skip the upload.
        _frame.InvalidateAll();
        _frame.Pixels.Clear();
        _view.Draw(_frame.Pixels);
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
        // The panel art (ADVBORD.ICN) and the buttons around the radar belong to the interface shell,
        // which is still missing; a one pixel frame keeps the panel readable on top of the map.
        _minimap.Draw(_frame.Pixels, _radar.Position, _view.VisibleTileArea);
        ImageOps.DrawRect(_frame.Pixels, _radar, Minimap.BlackColor);
    }

    private void DrawStatusBar(in DiagnosticsState state)
    {
        ImageOps.FillRect(_frame.Pixels, _statusBar, _statusBarColor);

        if (_fonts is null)
        {
            return;
        }

        IGlyphSource font = _fonts.Small;
        int top = _statusBar.Y;

        string right = FormattableString.Invariant(
            $"viewport {_view.Scroll.X},{_view.Scroll.Y}  |  v{BuildInfo.Version}");
        int rightX = Math.Max(
            _statusBar.Right - StatusBarPadding - GlyphText.Measure(font, right), StatusBarPadding);
        GlyphText.DrawLine(font, _frame.Pixels, new Point(rightX, top), right);

        string left = $"{MapName}  {_view.Map.Width}x{_view.Map.Height}";
        int width = Math.Max(rightX - StatusBarGap - StatusBarPadding, 0);
        if (width <= 0)
        {
            return;
        }

        Rect line = new(StatusBarPadding, top, width, _statusBar.Height);
        GlyphText.DrawLine(font, _frame.Pixels, new Point(line.X, top), left, line);
    }
}
