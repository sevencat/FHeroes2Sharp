using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The editing half of the map editor: a ground brush, the stroke that a press-drag-release forms and
/// the undo history. It owns no pixels and no input, so a stroke can be replayed in a test without a
/// window, the same split <see cref="MapEditorView"/> uses for the hover.
/// </summary>
///
/// <remarks>
/// <para>
/// The reference paints through <c>Editor::Interface::mouseCursorAreaClickLeft</c>, which routes a
/// click to the active instrument and lets it rewrite the tiles under the cursor
/// (<c>editor_interface_panel.cpp</c> holds the instrument table). Its terrain tool also re-blends the
/// painted tile with its neighbours; this port only swaps the terrain image, so a painted tile is the
/// plain image of its ground and the edges stay hard. The blending is the next step, not a hidden
/// behaviour.
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

    /// <summary>Bits of <see cref="Mp2TileInfo.TerrainFlags"/> that hold the terrain shape.</summary>
    private const byte TerrainShapeMask = 0x03;

    private readonly MapEditorView _editor;
    private readonly List<MapStroke> _undo = [];
    private readonly List<MapStroke> _redo = [];

    private MapStroke? _stroke;
    private MapGround _brush = MapGround.Grass;

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

    /// <summary>Selects the ground the brush paints.</summary>
    /// <param name="ground">Ground to paint, not <see cref="MapGround.Unknown"/>.</param>
    /// <returns>Whether the brush changed, so that a caller can redraw only when it did.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ground"/> has no terrain images.</exception>
    public bool SetBrush(MapGround ground)
    {
        _ = MapGrounds.StartImageIndex(ground);

        if (ground == _brush)
        {
            return false;
        }

        _brush = ground;
        return true;
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
        return true;
    }

    /// <summary>Closes the stroke and pushes it onto the undo history when it changed anything.</summary>
    /// <returns>Whether a stroke with at least one changed tile was recorded.</returns>
    public bool EndStroke()
    {
        MapStroke? stroke = _stroke;
        _stroke = null;

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
    /// The terrain shape stays as it was: it tells the renderer how to flip the tile, and a painted
    /// tile should keep the orientation of the one it replaces. The beach bits (bit 2 to 4) are
    /// dropped, because they describe how water touches the ground that is no longer there.
    /// </remarks>
    /// <param name="tile">Tile to paint, ignored when it is outside the map or <see langword="null"/>.</param>
    /// <returns>Whether the tile changed.</returns>
    public bool Paint(Point? tile)
    {
        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        bool ownStroke = _stroke is null;
        BeginStroke();

        Mp2TileInfo before = Map.Tile(point.X, point.Y);
        Mp2TileInfo after = before with
        {
            TerrainImageIndex = MapGrounds.StartImageIndex(_brush),
            TerrainFlags = (byte)(before.TerrainFlags & TerrainShapeMask),
        };

        if (after == before)
        {
            if (ownStroke)
            {
                _stroke = null;
            }

            return false;
        }

        int index = Map.TileIndex(point.X, point.Y);
        Map.SetTileAt(index, after);
        _stroke!.Record(index, before, after);

        if (ownStroke)
        {
            EndStroke();
        }

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
            if (string.IsNullOrWhiteSpace(_savePath) || !File.Exists(_savePath))
            {
                Mp2MapWriter.WriteAll(filePath, Map);
            }
            else
            {
                Mp2MapWriter.Save(filePath, Map, _savePath);
            }

            _savePath = filePath;
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
