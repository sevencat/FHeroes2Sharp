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

    private const int StatusBarPadding = 8;

    private const int StatusBarGap = 10;

    private readonly ScreenBuffer _frame;
    private readonly GameFonts? _fonts;
    private readonly AdventureMapView _view;
    private readonly Rect _statusBar;
    private readonly byte _statusBarColor;

    private AdventureMapScene(ScreenBuffer frame, GameFonts? fonts, AdventureMapView view)
    {
        _frame = frame;
        _fonts = fonts;
        _view = view;
        _statusBar = new Rect(0, frame.Size.Height - StatusBarHeight, frame.Size.Width, StatusBarHeight);
        _statusBarColor = frame.Palette.FindDarkestIndex();
    }

    /// <summary>The map the scene draws.</summary>
    public Mp2Map Map => _view.Map;

    /// <summary>Scrolling view of the terrain.</summary>
    public AdventureMapView View => _view;

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

            AdventureMapScene scene = new(frame, fonts, view);

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

    /// <summary>Nothing animates on the terrain layer yet.</summary>
    public void Update(double elapsedSeconds)
    {
    }

    /// <summary>Paints the visible terrain and the status bar.</summary>
    public void DrawFrame(in DiagnosticsState state)
    {
        // Tiles the map does not cover (only possible at the edges) stay black, which is the colour
        // of index 0 of KB.PAL and matches the empty border of the reference.
        _frame.Pixels.Clear();
        _view.Draw(_frame.Pixels);
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
