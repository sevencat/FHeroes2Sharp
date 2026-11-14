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
/// heroes and layers shows up here for free, while the grid, the highlighted tile, the toolbar and
/// the status bar are the editor's own.
/// </para>
/// <para>
/// The instruments are keys for now: <c>1</c> to <c>9</c> pick a ground and the terrain tool, <c>O</c>
/// cycles the objects of <see cref="Mp2ObjectPresets"/> and selects the object tool, <c>X</c> the eraser
/// and <c>T</c> the terrain tool again. <see cref="MapEditSession"/> owns what they do; this scene only
/// forwards input and paints the bars.
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
/// Not ported yet: the <c>ADVBORD</c> interface frame and the <c>EditorPanel</c> instruments. Until
/// those exist the map fills the logical frame and the two bars are painted over it, instead of the
/// reference's 448x448 viewport between the frame and the panel, and the objects are a fixed list of
/// presets instead of a panel of buttons.
/// </para>
/// </summary>
public sealed class EditorScene : IScene
{
    /// <summary>Height of the toolbar, one line of <c>SMALFONT.ICN</c> plus its frame.</summary>
    public const int ToolbarHeight = 13;

    /// <summary>Height of the status bar, one line of <c>SMALFONT.ICN</c>.</summary>
    public const int StatusBarHeight = 11;

    /// <summary>Distance between the minimap and the top right corner, <c>fheroes2::borderWidthPx</c>.</summary>
    public const int RadarMargin = 16;

    private const int Padding = 6;

    private const int Gap = 10;

    /// <summary>How long the status bar shows "saved" after a successful save, in seconds.</summary>
    private const double SavedFlashSeconds = 2.0;

    private readonly ScreenBuffer _frame;
    private readonly GameFonts? _fonts;
    private readonly MapEditorView _editor;
    private readonly MapEditSession _edits;
    private readonly MapScrollController _mouse;
    private readonly Rect _toolbar;
    private readonly Rect _statusBar;
    private readonly Rect _radar;
    private readonly byte _dark;
    private readonly byte _bright;

    /// <summary>Baked colours of the map; rebuilt after a stroke because it does not read the tiles.</summary>
    private Minimap _minimap;

    /// <summary>Whether the paint button is held, i.e. the pointer keeps painting as it moves.</summary>
    private bool _painting;

    /// <summary>Whether the left button is held anywhere in the window.</summary>
    private bool _leftHeld;

    /// <summary>Whether the press started on the minimap, which owns the pointer until it is released.</summary>
    private bool _radarGrabbed;

    /// <summary>Seconds left of the "saved" note in the status bar.</summary>
    private double _savedFlash;

    private EditorScene(
        ScreenBuffer frame,
        GameFonts? fonts,
        MapEditorView editor,
        MapEditSession edits,
        Minimap minimap,
        MapScrollController mouse)
    {
        _frame = frame;
        _fonts = fonts;
        _editor = editor;
        _edits = edits;
        _minimap = minimap;
        _mouse = mouse;
        _toolbar = new Rect(0, 0, frame.Size.Width, ToolbarHeight);
        _statusBar = new Rect(0, frame.Size.Height - StatusBarHeight, frame.Size.Width, StatusBarHeight);
        _radar = new Rect(
            frame.Size.Width - RadarMargin - Minimap.PanelSize,
            RadarMargin,
            Minimap.PanelSize,
            Minimap.PanelSize);
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

    /// <summary>The toolbar rectangle at the top of the logical frame.</summary>
    public Rect ToolbarArea => _toolbar;

    /// <summary>The status bar rectangle at the bottom of the logical frame.</summary>
    public Rect StatusBarArea => _statusBar;

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
        AdventureMapView view = new(
            map,
            tiles,
            frame.Size,
            ObjectSpriteSet.TryCreate(assets),
            HeroSpriteSet.TryCreate(assets));

        view.CenterOn(map.Width / 2, map.Height / 2);

        MapEditorView editor = new(view);
        MapEditSession edits = new(editor);

        return new EditorScene(
            frame, fonts, editor, edits, Minimap.Build(map), new MapScrollController(view));
    }

    /// <summary>Advances the edge scroll of the mouse; the terrain itself does not animate yet.</summary>
    public void Update(double elapsedSeconds)
    {
        _mouse.Update(elapsedSeconds);
        _savedFlash = Math.Max(0.0, _savedFlash - elapsedSeconds);
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
        _editor.Map.Draw(_frame.Pixels);
        DrawGrid();
        DrawHover();
        DrawMinimap();
        DrawToolbar();
        DrawStatusBar(state);
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
        _editor.SetCursor(cursor);
        _mouse.SetCursor(cursor);

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

        if (down && _mouse.Cursor is { } cursor && _radar.Contains(cursor))
        {
            _radarGrabbed = true;
            CentreOnRadar(cursor);
            return false;
        }

        if (!down && _radarGrabbed)
        {
            _radarGrabbed = false;
            return false;
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
        return true;
    }

    /// <summary>
    /// Records the right button: dragging it pans the map, a click without a drag samples the ground
    /// under the pointer as the brush.
    /// </summary>
    /// <remarks>
    /// The minimap takes neither: the reference shows its "World Map" hint there, and an eyedropper or
    /// a pan that started on the panel would describe a tile the user did not point at.
    /// </remarks>
    /// <param name="down">Whether the right button is now held.</param>
    /// <returns>Whether the release took the brush from the map.</returns>
    public bool SetRightButton(bool down)
    {
        bool onRadar = _mouse.Cursor is { } cursor && _radar.Contains(cursor);

        if (down)
        {
            if (!onRadar)
            {
                _mouse.SetPanButton(true);
            }

            return false;
        }

        // A release after a drag is the end of a pan, not an eyedropper click.
        if (_mouse.IsDragging)
        {
            _mouse.SetPanButton(false);
            return false;
        }

        _mouse.SetPanButton(false);

        return !onRadar && _edits.SampleBrush(_editor.HoverTile);
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
    /// A click on the toolbar, the status bar or the minimap does nothing: the panels cover the map, so
    /// the tile behind them is highlighted and hovered, but it is not what the user pointed at. The
    /// reference does not have this problem because its panels are outside the 448x448 map area, and a
    /// stroke that starts on the map and is dragged across the minimap keeps painting there.
    /// </remarks>
    /// <returns>Whether the tile changed.</returns>
    public bool Paint()
    {
        if (_editor.Cursor is { } cursor
            && (_toolbar.Contains(cursor) || _statusBar.Contains(cursor) || _radar.Contains(cursor)))
        {
            return false;
        }

        return _edits.Apply(_editor.HoverTile);
    }

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
    public void ScrollByWheel(int columns, int rows) => _mouse.ScrollByWheel(columns, rows);

    /// <summary>
    /// Draws the tile borders of the visible area. The grid stays between the two bars so that it
    /// never paints over their text.
    /// </summary>
    private void DrawGrid()
    {
        int top = _toolbar.Y + _toolbar.Height;
        int height = _statusBar.Y - top;

        if (height <= 0)
        {
            return;
        }

        foreach (int column in _editor.GridColumns())
        {
            ImageOps.VLine(_frame.Pixels, column, top, height, _dark);
        }

        foreach (int row in _editor.GridRows())
        {
            if (row >= top && row < _statusBar.Y)
            {
                ImageOps.HLine(_frame.Pixels, 0, row, _frame.Size.Width, _dark);
            }
        }
    }

    /// <summary>Outlines the tile under the pointer, bright outside and dark inside, so that the tile
    /// reads on both the light and the dark grounds of a map.</summary>
    private void DrawHover()
    {
        if (_editor.HoverArea is not { } area)
        {
            return;
        }

        ImageOps.DrawRect(_frame.Pixels, area, _bright);

        if (area.Width > 2 && area.Height > 2)
        {
            ImageOps.DrawRect(_frame.Pixels, new Rect(area.X + 1, area.Y + 1, area.Width - 2, area.Height - 2), _dark);
        }
    }

    private void DrawMinimap()
    {
        _minimap.Draw(_frame.Pixels, _radar.Position, _editor.Map.VisibleTileArea);
        ImageOps.DrawRect(_frame.Pixels, _radar, _dark);
    }

    /// <summary>
    /// Re-bakes the minimap after the tiles changed. It is the one cache in the editor: the map view
    /// reads the tiles on every frame, the minimap keeps one colour per tile from <c>Build</c>.
    /// </summary>
    private void RebuildMinimap() => _minimap = Minimap.Build(Map);

    private void DrawToolbar()
    {
        ImageOps.FillRect(_frame.Pixels, _toolbar, _dark);
        ImageOps.HLine(
            _frame.Pixels, 0, _toolbar.Y + _toolbar.Height - 1, _frame.Size.Width, _bright);

        if (_fonts is null)
        {
            return;
        }

        IGlyphSource font = _fonts.Small;

        // The tool and the object it places do not fit next to the hints at the narrowest logical
        // width, so the bar keeps its name and the status bar reports the instrument.
        GlyphText.DrawLine(font, _frame.Pixels, new Point(Padding, _toolbar.Y), "MAP EDITOR");

        string right = "1-9 ground  O obj  X del  ^Z/^Y  G grid  Esc";

        int rightX = Math.Max(
            _toolbar.Right - Padding - GlyphText.Measure(font, right), Padding);

        GlyphText.DrawLine(font, _frame.Pixels, new Point(rightX, _toolbar.Y), right);
    }

    private void DrawStatusBar(in DiagnosticsState state)
    {
        ImageOps.FillRect(_frame.Pixels, _statusBar, _dark);

        if (_fonts is null)
        {
            return;
        }

        IGlyphSource font = _fonts.Small;
        int top = _statusBar.Y;

        string right = FormattableString.Invariant(
            $"undo {_edits.UndoDepth}  scroll {_editor.Map.Scroll.X},{_editor.Map.Scroll.Y}  |  v{BuildInfo.Version}");
        int rightX = Math.Max(
            _statusBar.Right - Padding - GlyphText.Measure(font, right), Padding);
        GlyphText.DrawLine(font, _frame.Pixels, new Point(rightX, top), right);

        string left = FormattableString.Invariant($"{MapName}  {Map.Width}x{Map.Height}");
        int leftEdge = Padding + GlyphText.Measure(font, left);

        if (leftEdge + Gap >= rightX)
        {
            GlyphText.DrawLine(
                font, _frame.Pixels, new Point(Padding, top), left,
                new Rect(Padding, top, Math.Max(rightX - Padding - Gap, 0), _statusBar.Height));
            return;
        }

        GlyphText.DrawLine(font, _frame.Pixels, new Point(Padding, top), left);

        string tool = _edits.Tool switch
        {
            EditorTool.Object => FormattableString.Invariant($"object {_edits.ObjectBrush.Name}"),
            EditorTool.Erase => "erase",
            _ => FormattableString.Invariant($"brush {_edits.Brush}"),
        };

        string hover = _editor.HoverTile is { } tile
            ? FormattableString.Invariant($"{tool}  {tile.X},{tile.Y}  {_editor.HoverGround}")
            : FormattableString.Invariant($"{tool}  no tile");

        if (_edits.IsDirty)
        {
            hover += "  edited";
        }
        else if (_savedFlash > 0.0)
        {
            hover += "  saved";
        }

        if (_editor.GridEnabled)
        {
            hover += "  grid";
        }

        if (_editor.Hover is { } info && info.ObjectType != Mp2ObjectType.None)
        {
            hover = FormattableString.Invariant($"{hover}  {info.ObjectType}");
        }

        Rect middle = new(leftEdge + Gap, top, Math.Max(rightX - leftEdge - Gap, 0), _statusBar.Height);
        GlyphText.DrawLine(font, _frame.Pixels, new Point(middle.X, top), hover, middle);
    }
}
