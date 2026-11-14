using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The editing half of the map editor: a ground brush, an object brush, the eraser, the stroke that a
/// press-drag-release forms and the undo history. It owns no pixels and no input, so a stroke can be
/// replayed in a test without a window, the same split <see cref="MapEditorView"/> uses for the hover.
/// </summary>
///
/// <remarks>
/// <para>
/// The reference paints through <c>Editor::Interface::mouseCursorAreaClickLeft</c>, which routes a
/// click to the active instrument and lets it rewrite the tiles under the cursor
/// (<c>editor_interface_panel.cpp</c> holds the instrument table). <see cref="EditorTool"/> is that
/// table cut down to three instruments and <see cref="Apply"/> is the routing. Its terrain tool also
/// re-blends the painted tile with its neighbours; this port only swaps the terrain image, so a painted
/// tile is the plain image of its ground and the edges stay hard. The blending is the next step, not a
/// hidden behaviour.
/// </para>
/// <para>
/// Placing an object rewrites the object half of a tile and appends the add-ons the preset needs; the
/// terrain half is left alone, which is what makes "a tree on snow" a tree on snow. Add-ons are never
/// reclaimed: the format has no free list, so replacing an object leaves its pieces unreferenced
/// (ADR-045). A save after such a placement writes the whole file, because the add-on table grew and a
/// tile patch cannot express that.
/// </para>
/// <para>
/// A stroke, not a tile, is the unit of undo: a drag across twenty tiles is one <c>Ctrl+Z</c> in every
/// editor, and merging the repeats of a tile the pointer crossed twice is what keeps the history of a
/// scribble small.
/// </para>
/// </remarks>
public sealed class MapEditSession
{
    /// <summary>Number of strokes kept in the undo history, oldest dropped first.</summary>
    public const int MaxUndoDepth = 200;

    private readonly MapEditorView _editor;
    private readonly List<MapStroke> _undo = [];
    private readonly List<MapStroke> _redo = [];

    private MapStroke? _stroke;
    private MapGround _brush = MapGround.Grass;

    /// <summary>
    /// Size of the ground and eraser brush. The reference opens with its medium brush, this port with
    /// the 1x1 one: the port painted one tile per step before sizes existed, and the keys of the editor
    /// pick grounds, not sizes.
    /// </summary>
    private BrushSize _brushSize = BrushSize.Small;

    /// <summary>Tile the current <see cref="BrushSize.Area"/> rectangle starts from, i.e. the first one painted.</summary>
    private Point? _strokeAnchor;

    /// <summary>Instrument the left button uses; the ground brush until a key picks another one.</summary>
    private EditorTool _tool = EditorTool.Terrain;

    /// <summary>Object the object tool places; the first preset until a key picks another one.</summary>
    private Mp2ObjectPreset _object = Mp2ObjectPresets.All[0];

    /// <summary>
    /// Whether the add-on table grew during this session. Tiles alone no longer describe the file
    /// then: a tile of a placed multi piece object points at an add-on the file it was read from does
    /// not have, so the save has to write every section instead of patching the tile area.
    /// </summary>
    private bool _addonsChanged;

    /// <summary>Undo depth at the last save; the map is dirty while the depth differs from it.</summary>
    private int _savedDepth;

    /// <summary>File the map is saved to; an edit is written over the file it came from.</summary>
    private string _savePath = string.Empty;

    /// <summary>Wraps the editor state whose map is being changed.</summary>
    /// <param name="editor">The editor that owns the map and the pointer.</param>
    public MapEditSession(MapEditorView editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _editor = editor;
        _savePath = editor.Map.Map.Info.FilePath;
    }

    /// <summary>The map being edited, changed in place by <see cref="Paint"/>.</summary>
    public Mp2Map Map => _editor.Map.Map;

    /// <summary>The ground the brush paints, grass until a tool picks another one.</summary>
    public MapGround Brush => _brush;

    /// <summary>What the left button does: paint ground, place the object or erase it.</summary>
    public EditorTool Tool => _tool;

    /// <summary>How many tiles the ground and eraser brushes cover, the brush buttons of the panel.</summary>
    public BrushSize BrushSize => _brushSize;

    /// <summary>The object the object tool places.</summary>
    public Mp2ObjectPreset ObjectBrush => _object;

    /// <summary>Whether a stroke is in progress, i.e. the button is still held.</summary>
    public bool IsPainting => _stroke is not null;

    /// <summary>Whether the map differs from the last state written to disk.</summary>
    /// <remarks>
    /// "Differs" is read from the history, not from a comparison with a copy of the map: a stroke that
    /// is undoable is a change, and undoing back to the point of the last save makes the flag go away
    /// again. Re-painting a tile with the ground it already has records nothing, so it does not mark
    /// the map either. The depth is compared with the depth at the save, so undoing past the save
    /// counts as a change even though the history only shrinks. An unfinished stroke counts as well:
    /// the tiles under the held button are already changed, and a save in the middle of a drag must not
    /// look like a clean map.
    /// </remarks>
    public bool IsDirty => _stroke is not null || _undo.Count != _savedDepth;

    /// <summary>File <see cref="Save"/> writes to, the file the map was opened from by default.</summary>
    public string SavePath => _savePath;

    /// <summary>Number of undoable strokes.</summary>
    public int UndoDepth => _undo.Count;

    /// <summary>Number of redoable strokes, cleared by the next stroke.</summary>
    public int RedoDepth => _redo.Count;

    /// <summary>Selects the ground the brush paints, which also makes the terrain tool the active one.</summary>
    /// <param name="ground">Ground to paint, not <see cref="MapGround.Unknown"/>.</param>
    /// <returns>Whether the brush or the tool changed, so that a caller can redraw only when it did.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ground"/> has no terrain images.</exception>
    public bool SetBrush(MapGround ground)
    {
        _ = MapGrounds.StartImageIndex(ground);

        bool changed = ground != _brush || _tool != EditorTool.Terrain;

        _brush = ground;
        _tool = EditorTool.Terrain;
        return changed;
    }

    /// <summary>
    /// Selects the size of the ground and eraser brushes, the four buttons under the picker of the
    /// panel. The object tool ignores it: an object is as big as its own artwork, not as the brush.
    /// </summary>
    /// <param name="size">The size to use.</param>
    /// <returns>Whether the size changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not a brush size.</exception>
    public bool SetBrushSize(BrushSize size)
    {
        if (!Enum.IsDefined(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown brush size.");
        }

        if (size == _brushSize)
        {
            return false;
        }

        _brushSize = size;
        return true;
    }

    /// <summary>Selects what the left button does.</summary>
    /// <param name="tool">The instrument to use.</param>
    /// <returns>Whether the tool changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tool"/> is not a tool.</exception>
    public bool SetTool(EditorTool tool)
    {
        if (!Enum.IsDefined(tool))
        {
            throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unknown editor tool.");
        }

        if (tool == _tool)
        {
            return false;
        }

        _tool = tool;
        return true;
    }

    /// <summary>
    /// Selects the object the object tool places and switches to that tool, the way picking a ground
    /// switches to the terrain tool: a click on the button means a click with the instrument.
    /// </summary>
    /// <param name="preset">Object to place.</param>
    /// <returns>Whether the object or the tool changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="preset"/> is <see langword="null"/>.</exception>
    public bool SelectObject(Mp2ObjectPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        bool changed = !ReferenceEquals(preset, _object) || _tool != EditorTool.Object;

        _object = preset;
        _tool = EditorTool.Object;
        return changed;
    }

    /// <summary>Steps through the object presets, the <c>O</c> key; also selects the object tool.</summary>
    /// <param name="delta">Presets to step; wraps around the list.</param>
    /// <returns>The object after the step.</returns>
    public Mp2ObjectPreset CycleObject(int delta)
    {
        int count = Mp2ObjectPresets.Count;
        int current = Mp2ObjectPresets.IndexOf(_object);
        int next = ((current + delta) % count + count) % count;

        _object = Mp2ObjectPresets.All[next];
        _tool = EditorTool.Object;
        return _object;
    }

    /// <summary>
    /// Takes the ground under <paramref name="tile"/> as the brush, the eyedropper of every editor.
    /// </summary>
    /// <param name="tile">Tile to read, outside the map when there is nothing to sample.</param>
    /// <returns>Whether the brush changed.</returns>
    public bool SampleBrush(Point? tile)
    {
        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        MapGround ground = MapGrounds.FromTerrainImageIndex(Map.Tile(point.X, point.Y).TerrainImageIndex);

        return ground != MapGround.Unknown && SetBrush(ground);
    }

    /// <summary>
    /// Starts a stroke. Called when the paint button goes down; a paint without an explicit stroke
    /// opens and closes one itself, which is what a single click without a window does.
    /// </summary>
    /// <returns>Whether a new stroke was started, <see langword="false"/> when one was already open.</returns>
    public bool BeginStroke()
    {
        if (_stroke is not null)
        {
            return false;
        }

        _stroke = new MapStroke();
        _strokeAnchor = null;
        return true;
    }

    /// <summary>Closes the stroke and pushes it onto the undo history when it changed anything.</summary>
    /// <returns>Whether a stroke with at least one changed tile was recorded.</returns>
    public bool EndStroke()
    {
        MapStroke? stroke = _stroke;
        _stroke = null;
        _strokeAnchor = null;

        if (stroke is null || stroke.Count == 0)
        {
            return false;
        }

        _undo.Add(stroke);

        if (_undo.Count > MaxUndoDepth)
        {
            // The oldest stroke leaves the history and can no longer be undone, so the depth of the
            // last save moves with it; otherwise a long session would look clean at the wrong moment.
            _undo.RemoveAt(0);
            _savedDepth = Math.Max(0, _savedDepth - 1);
        }

        _redo.Clear();
        return true;
    }

    /// <summary>
    /// Paints the ground of the brush onto <paramref name="tile"/>, which is one step of a stroke.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every tile of the brush area gets the plain image of the brush, then the blend is recomputed for
    /// the area and its one tile border: the images of those neighbours say how a tile ends against
    /// every other ground, so painting grass next to water has to change the water edge too. The whole
    /// area belongs to the same stroke, so one <c>Ctrl+Z</c> takes the edit back. See
    /// <see cref="TerrainTransitions"/> for the table itself.
    /// </para>
    /// <para>
    /// The terrain shape and the beach bits of a painted tile are replaced by what the blend says: both
    /// describe the ground that is no longer there.
    /// </para>
    /// <para>
    /// A <see cref="BrushSize.Area"/> brush paints the rectangle between the tile the stroke started on
    /// and <paramref name="tile"/>, growing it as the pointer moves: the reference draws a rubber band
    /// while dragging and fills the same rectangle when the button comes up, this port fills as it goes.
    /// </para>
    /// </remarks>
    /// <param name="tile">Tile to paint, ignored when it is outside the map or <see langword="null"/>.</param>
    /// <returns>Whether anything changed.</returns>
    public bool Paint(Point? tile)
    {
        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        bool ownStroke = _stroke is null;
        BeginStroke();

        Rect area = BrushArea(point);
        bool changed = false;

        foreach (Point painted in Tiles(area, 0))
        {
            changed |= WriteGround(painted, TerrainTransitions.PlainImageIndex(_brush, painted), 0);
        }

        foreach (Point blended in Tiles(area, 1))
        {
            changed |= Blend(blended);
        }

        if (!changed)
        {
            if (ownStroke)
            {
                _stroke = null;
            }

            return false;
        }

        if (ownStroke)
        {
            EndStroke();
        }

        return true;
    }

    /// <summary>
    /// The tiles the current brush covers around <paramref name="tile"/>, clipped to the map.
    /// </summary>
    /// <param name="tile">Tile under the pointer, must be inside the map.</param>
    /// <returns>The area in tile coordinates, never empty for <paramref name="tile"/> inside the map.</returns>
    private Rect BrushArea(Point tile)
    {
        if (_brushSize != BrushSize.Area)
        {
            Rect offsets = _brushSize.Area();
            return new Rect(tile.X + offsets.X, tile.Y + offsets.Y, offsets.Width, offsets.Height);
        }

        Point anchor = _strokeAnchor ??= tile;
        int left = Math.Min(anchor.X, tile.X);
        int top = Math.Min(anchor.Y, tile.Y);

        return new Rect(left, top, Math.Max(anchor.X, tile.X) - left + 1, Math.Max(anchor.Y, tile.Y) - top + 1);
    }

    /// <summary>
    /// Iterates the tiles of <paramref name="area"/> grown by <paramref name="border"/>, clipped to the map.
    /// </summary>
    /// <param name="area">Area in tile coordinates.</param>
    /// <param name="border">How many tiles of margin to include, the blend needs one.</param>
    /// <returns>The tiles to visit, in row-major order.</returns>
    private IEnumerable<Point> Tiles(Rect area, int border)
    {
        Rect grown = new Rect(area.X - border, area.Y - border, area.Width + (2 * border), area.Height + (2 * border));
        int left = Math.Max(0, grown.Left);
        int top = Math.Max(0, grown.Top);
        int right = Math.Min(Map.Width, grown.Right);
        int bottom = Math.Min(Map.Height, grown.Bottom);

        for (int y = top; y < bottom; ++y)
        {
            for (int x = left; x < right; ++x)
            {
                yield return new Point(x, y);
            }
        }
    }

    /// <summary>
    /// Writes the terrain image and flags of one tile into the map and the open stroke.
    /// </summary>
    /// <param name="point">Tile to write, must be inside the map.</param>
    /// <param name="imageIndex">Terrain image index to set.</param>
    /// <param name="flags">Terrain flags to set.</param>
    /// <returns>Whether the tile changed.</returns>
    private bool WriteGround(Point point, ushort imageIndex, byte flags)
    {
        Mp2TileInfo before = Map.Tile(point.X, point.Y);
        Mp2TileInfo after = before with { TerrainImageIndex = imageIndex, TerrainFlags = flags };

        if (after == before)
        {
            return false;
        }

        int index = Map.TileIndex(point.X, point.Y);
        Map.SetTileAt(index, after);
        _stroke!.Record(index, before, after);
        return true;
    }

    /// <summary>Recomputes the blend of one tile and writes it back when it changed.</summary>
    /// <param name="point">Tile to blend, must be inside the map.</param>
    /// <returns>Whether the tile changed.</returns>
    private bool Blend(Point point)
        => TerrainTransitions.TryGetBlend(Map, point, out ushort imageIndex, out byte shape)
           && WriteGround(point, imageIndex, shape);

    /// <summary>
    /// Runs the active tool on <paramref name="tile"/>, the one entry point a click and a drag share.
    /// </summary>
    /// <param name="tile">Tile under the pointer, ignored when outside the map.</param>
    /// <returns>Whether the tile changed.</returns>
    public bool Apply(Point? tile) => _tool switch
    {
        EditorTool.Object => PlaceObject(tile),
        EditorTool.Erase => EraseObject(tile),
        _ => Paint(tile),
    };

    /// <summary>
    /// Places <see cref="ObjectBrush"/> on <paramref name="tile"/>, one step of a stroke.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tile keeps its terrain image and its terrain flags: the object is drawn over the ground, it
    /// does not replace it. The rest comes from the preset, except the add-on chain and the UIDs, which
    /// are built here so that two placements are two objects to the game.
    /// </para>
    /// <para>
    /// A tile that already shows the same object is left alone, so dragging across it does not spend a
    /// UID per mouse move. An object of another kind is replaced, and the add-ons of the old one stay in
    /// the table without a reference: the format has no free list to give them back to, and an orphan
    /// costs 15 bytes in the file and nothing in the frame, while deleting it would renumber every
    /// chain behind it.
    /// </para>
    /// </remarks>
    /// <param name="tile">Tile to place on, ignored when outside the map or <see langword="null"/>.</param>
    /// <returns>Whether the tile changed.</returns>
    public bool PlaceObject(Point? tile)
    {
        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        Mp2TileInfo before = Map.Tile(point.X, point.Y);
        Mp2TileInfo template = _object.Template;

        if (before.ObjectType == template.ObjectType
            && before.ObjectName1 == template.ObjectName1
            && before.BottomIcnImageIndex == template.BottomIcnImageIndex
            && before.Quantity1 == template.Quantity1
            && before.Quantity2 == template.Quantity2
            && before.ObjectName2 == template.ObjectName2
            && before.TopIcnImageIndex == template.TopIcnImageIndex)
        {
            return false;
        }

        bool ownStroke = _stroke is null;
        BeginStroke();

        uint groundUid = template.HasGroundObject ? Map.TakeObjectUid() : 0;
        uint topUid = template.HasTopObject ? Map.TakeObjectUid() : 0;
        uint pieceUid = groundUid != 0 ? groundUid : topUid;

        ushort chain = 0;
        for (int index = _object.Addons.Count - 1; index >= 0; --index)
        {
            chain = (ushort)Map.AppendAddon(_object.Addons[index] with
            {
                NextAddonIndex = chain,
                Level1ObjectUid = pieceUid,
                Level2ObjectUid = 0,
            });
        }

        if (chain != 0)
        {
            _addonsChanged = true;
        }

        Mp2TileInfo after = before with
        {
            ObjectName1 = template.ObjectName1,
            BottomIcnImageIndex = template.BottomIcnImageIndex,
            Quantity1 = template.Quantity1,
            Quantity2 = template.Quantity2,
            ObjectName2 = template.ObjectName2,
            TopIcnImageIndex = template.TopIcnImageIndex,
            ObjectType = template.ObjectType,
            NextAddonIndex = chain,
            Level1ObjectUid = groundUid,
            Level2ObjectUid = topUid,
        };

        int tileIndex = Map.TileIndex(point.X, point.Y);
        Map.SetTileAt(tileIndex, after);
        _stroke!.Record(tileIndex, before, after);

        if (ownStroke)
        {
            EndStroke();
        }

        return true;
    }

    /// <summary>
    /// Clears the objects under the brush and keeps their ground, the eraser of the editor.
    /// </summary>
    /// <remarks>
    /// The result is the empty tile a shipped map stores: no ICN type, sprite index 255 on both
    /// parts, a zeroed layer and metadata and UIDs of 0, with the terrain image and the terrain flags
    /// untouched. Every tile of the brush area is cleared, not just the pointed one: the reference
    /// applies <c>getBrushArea()</c> of the eraser too, which is what the size buttons of the panel
    /// are for.
    /// </remarks>
    /// <param name="tile">Tile under the pointer, ignored when outside the map or <see langword="null"/>.</param>
    /// <returns>Whether anything changed.</returns>
    public bool EraseObject(Point? tile)
    {
        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        bool ownStroke = _stroke is null;
        BeginStroke();

        bool changed = false;

        foreach (Point cleared in Tiles(BrushArea(point), 0))
        {
            changed |= EraseTile(cleared);
        }

        if (!changed)
        {
            if (ownStroke)
            {
                _stroke = null;
            }

            return false;
        }

        if (ownStroke)
        {
            EndStroke();
        }

        return true;
    }

    /// <summary>Clears the object of one tile, the body of <see cref="EraseObject"/>.</summary>
    /// <param name="point">Tile to clear, must be inside the map.</param>
    /// <returns>Whether the tile changed.</returns>
    private bool EraseTile(Point point)
    {
        Mp2TileInfo before = Map.Tile(point.X, point.Y);

        if (!before.HasGroundObject
            && !before.HasTopObject
            && before.ObjectType == Mp2ObjectType.None
            && before.NextAddonIndex == 0)
        {
            return false;
        }

        Mp2TileInfo after = before with
        {
            ObjectName1 = 0,
            BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            Quantity1 = 0,
            Quantity2 = 0,
            ObjectName2 = 0,
            TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

        int tileIndex = Map.TileIndex(point.X, point.Y);
        Map.SetTileAt(tileIndex, after);
        _stroke!.Record(tileIndex, before, after);
        return true;
    }

    /// <summary>Reverts the last stroke, or the stroke in progress first.</summary>
    /// <returns>Whether there was anything to undo.</returns>
    public bool Undo()
    {
        if (_stroke is not null)
        {
            MapStroke open = _stroke;
            _stroke = null;

            if (open.Count > 0)
            {
                _undo.Add(open);
            }
        }

        if (_undo.Count == 0)
        {
            return false;
        }

        MapStroke stroke = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        Apply(stroke, reverse: true);
        _redo.Add(stroke);
        return true;
    }

    /// <summary>Applies the last undone stroke again.</summary>
    /// <returns>Whether there was anything to redo.</returns>
    public bool Redo()
    {
        EndStroke();

        if (_redo.Count == 0)
        {
            return false;
        }

        MapStroke stroke = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);

        Apply(stroke, reverse: false);
        _undo.Add(stroke);
        return true;
    }

    /// <summary>Writes the map to <see cref="SavePath"/>.</summary>
    /// <param name="error">Why the save failed, <see langword="null"/> when it worked.</param>
    /// <returns>Whether the map was written.</returns>
    public bool Save(out string? error)
    {
        if (string.IsNullOrWhiteSpace(_savePath))
        {
            error = "The map has no file yet, so there is nowhere to save it.";
            return false;
        }

        return SaveAs(_savePath, out error);
    }

    /// <summary>
    /// Writes the map to <paramref name="filePath"/> and makes that file the save target.
    /// </summary>
    /// <remarks>
    /// A map that came from a file is patched: only the tiles are rewritten and every other section is
    /// kept as it was read. A map that never had a file — the ones <see cref="Mp2Map.CreateBlank"/>
    /// makes — has nothing to patch, so it is written whole instead (see <see cref="Mp2MapWriter"/>).
    /// The same holds once an object with add-ons was placed: those add-ons are new rows of a section
    /// the patch does not touch, so the file is written whole from then on.
    /// </remarks>
    /// <param name="filePath">File to write, created or replaced.</param>
    /// <param name="error">Why the save failed, <see langword="null"/> when it worked.</param>
    /// <returns>Whether the map was written.</returns>
    public bool SaveAs(string filePath, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        error = null;

        // A save in the middle of a drag closes the stroke first: the tiles under the pointer are part
        // of the map already, so saving them and then recording the stroke as unsaved would lie.
        EndStroke();

        try
        {
            bool canPatch = !string.IsNullOrWhiteSpace(_savePath) && File.Exists(_savePath);

            if (!canPatch || _addonsChanged)
            {
                Mp2MapWriter.WriteAll(filePath, Map);
            }
            else
            {
                Mp2MapWriter.Save(filePath, Map, _savePath);
            }

            _savePath = filePath;
            _addonsChanged = false;
            _savedDepth = _undo.Count;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or BinaryFormatException or NotSupportedException)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>Writes one side of a stroke back into the map.</summary>
    private void Apply(MapStroke stroke, bool reverse)
    {
        foreach (MapTileChange change in stroke.Changes)
        {
            Map.SetTileAt(change.Index, reverse ? change.Before : change.After);
        }
    }

    private bool WithinMap(Point tile) =>
        tile.X >= 0 && tile.Y >= 0 && tile.X < Map.Width && tile.Y < Map.Height;
}

/// <summary>One tile of a stroke: what it was before and what it is after, by tile index.</summary>
/// <param name="Index">Index of the tile, <c>x + y * width</c>.</param>
/// <param name="Before">The tile as it was when the stroke first touched it.</param>
/// <param name="After">The tile as the stroke left it.</param>
public readonly record struct MapTileChange(int Index, Mp2TileInfo Before, Mp2TileInfo After);

/// <summary>
/// The tiles one press-drag-release changed, one entry per tile: a tile the pointer crossed twice
/// keeps its first "before" and its last "after", so the whole gesture undoes in one step.
/// </summary>
internal sealed class MapStroke
{
    private readonly Dictionary<int, MapTileChange> _changes = [];

    /// <summary>Number of distinct tiles the stroke changed.</summary>
    public int Count => _changes.Count;

    /// <summary>The changes, in the order the tiles were first touched.</summary>
    public IReadOnlyCollection<MapTileChange> Changes => _changes.Values;

    /// <summary>Records that the stroke changed a tile; a repeat updates the "after" side only.</summary>
    /// <param name="index">Index of the tile.</param>
    /// <param name="before">Value before the first change of the stroke.</param>
    /// <param name="after">Value after this change.</param>
    public void Record(int index, Mp2TileInfo before, Mp2TileInfo after)
    {
        _changes[index] = _changes.TryGetValue(index, out MapTileChange change)
            ? change with { After = after }
            : new MapTileChange(index, before, after);
    }
}
