using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Io;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

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
    private readonly List<IEditStep> _undo = [];
    private readonly List<IEditStep> _redo = [];

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
    /// What the eraser deletes; every type until the author unchecks some on the erase page of the panel,
    /// <c>_eraseTypes{ ObjectErasureType::ERASE_ALL_OBJECTS }</c> of <c>editor_interface_panel.h:337</c>.
    /// </summary>
    private EditorEraseType _eraseTypes = EditorEraseType.AllObjects;

    /// <summary>Groups the checked types cover, rebuilt when the checks change: the erase loop asks per tile.</summary>
    private IReadOnlySet<ObjectGroup>? _eraseGroups;

    /// <summary>
    /// What the detail instrument does with a click, the
    /// <c>_selectedDetailBrushType{ DetailBrushType::EDITING }</c> of
    /// <c>editor_interface_panel.h:336</c>: the properties of an object until a button picks another mode.
    /// </summary>
    private EditorDetailMode _detailMode = EditorDetailMode.Editing;

    /// <summary>
    /// Tile an object was taken from by a press of the detail instrument in <see cref="EditorDetailMode.Moving"/>
    /// or <see cref="EditorDetailMode.Copying"/>: the release drops it. <c>null</c> while nothing is held.
    /// </summary>
    /// <remarks>
    /// 参考把这件事放在 <c>EditorInterface::_movableObjectInfo</c> 里，端口放进会话：按下与松开之间没有任何别的东西要读它，
    /// 而放进会话就能在没有窗口的测试里把一次拖放走完一整遍。
    /// </remarks>
    private Point? _detailSource;

    /// <summary>
    /// Per brush, the entry of <see cref="EditorObjectBrushes"/> kind the author picked, or <c>-1</c> while the page
    /// of that brush is still empty.
    /// </summary>
    /// <remarks>
    /// 参考给每一把刷子一个「上一页的选中项」：从地形物件跳到冒险物件再跳回来，原来的选择还在。数组的下标是
    /// <see cref="EditorTool"/> 的值，所以只有四个物件仪器占位置，别的仪器永远不会被读。
    /// </remarks>
    private readonly int[] _brushKind = Enumerable.Repeat(-1, (int)EditorTool.Erase + 1).ToArray();

    /// <summary>
    /// Per brush, the entry of the picked group's catalogue, or <c>-1</c> while nothing was picked.
    /// </summary>
    private readonly int[] _brushObject = Enumerable.Repeat(-1, (int)EditorTool.Erase + 1).ToArray();

    /// <summary>
    /// Whether the add-on table grew during this session. Tiles alone no longer describe the file
    /// then: a tile of a placed multi piece object points at an add-on the file it was read from does
    /// not have, so the save has to write every section instead of patching the tile area.
    /// </summary>
    private bool _addonsChanged;

    /// <summary>
    /// Whether <see cref="Mp2Map.Info"/> was replaced during this session, i.e. the map specifications
    /// window wrote the header back. The tile patch of a save leaves the header alone, so the save has to
    /// overwrite it too.
    /// </summary>
    private bool _headerChanged;

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

    /// <summary>What the eraser deletes, the eleven checkboxes of its page in the instrument panel.</summary>
    public EditorEraseType EraseTypes => _eraseTypes;

    /// <summary>What a click of the detail instrument does, the three buttons of its page.</summary>
    public EditorDetailMode DetailMode => _detailMode;

    /// <summary>
    /// Tile whose object a press of the detail instrument took, or <c>null</c> while nothing is held.
    /// The tile is written only by a release, so the map still shows the object where it was.
    /// </summary>
    public Point? DetailSource => _detailSource;

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
    /// look like a clean map. A header replaced by <see cref="ApplyMapInfo"/> is not in the history, so it
    /// has its own flag.
    /// </remarks>
    public bool IsDirty => _stroke is not null || _undo.Count != _savedDepth || _headerChanged;

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

        // 换了工具，手上拿着的东西就不算数了：参考在 `EditorInterface::setInstrument` 里同样把
        // `_movableObjectInfo` 清掉，否则松开时会用旧工具的对象去改新工具下的格子。
        _detailSource = null;
        return true;
    }

    /// <summary>Selects what a click of the detail instrument does, the three buttons of its page.</summary>
    /// <param name="mode">The mode to use.</param>
    /// <returns>Whether the mode changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a mode.</exception>
    public bool SetDetailMode(EditorDetailMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown detail mode.");
        }

        if (mode == _detailMode)
        {
            return false;
        }

        _detailMode = mode;

        // 切到别的模式时，手上那次按下属于旧模式，丢掉：参考每按一次按钮都 `_resetMovableObjectInfo()`。
        _detailSource = null;
        return true;
    }

    /// <summary>Replaces the set of types the eraser deletes, the eleven checkboxes of its page.</summary>
    /// <param name="types">The types to delete, any combination of <see cref="EditorEraseTypes.Buttons"/>.</param>
    /// <returns>Whether the set changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="types"/> has a bit that is not a type.</exception>
    public bool SetEraseTypes(EditorEraseType types)
    {
        if ((types & ~EditorEraseType.AllObjects) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(types), types, "Unknown erase type.");
        }

        if (types == _eraseTypes)
        {
            return false;
        }

        _eraseTypes = types;
        _eraseGroups = null;
        return true;
    }

    /// <summary>
    /// Turns one type of the eraser on or off, the left click on its button
    /// (<c>_eraseTypes ^= _eraseButtonObjectTypes[i]</c>, <c>editor_interface_panel.cpp:1383</c>).
    /// </summary>
    /// <param name="type">One of <see cref="EditorEraseTypes.Buttons"/>.</param>
    /// <returns>Whether the set changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a single type.</exception>
    public bool ToggleEraseType(EditorEraseType type)
    {
        if (!EditorEraseTypes.IsButton(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a single erase type.");
        }

        EditorEraseType next = _eraseTypes ^ type;

        if (next == _eraseTypes)
        {
            return false;
        }

        _eraseTypes = next;
        _eraseGroups = null;
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

        bool changed = !ReferenceEquals(preset, _object) || _tool != EditorTool.LandscapeObjects;

        _object = preset;
        _tool = EditorTool.LandscapeObjects;
        return changed;
    }

    /// <summary>The brushes the current instrument offers, empty for the instruments that place no object.</summary>
    public IReadOnlyList<ObjectGroup> BrushKinds => EditorObjectBrushes.For(_tool);

    /// <summary>The brush kind the current instrument has picked, or <c>-1</c> while its page is empty.</summary>
    public int BrushKind => _brushKind[(int)_tool];

    /// <summary>The object the current instrument has picked, or <c>-1</c> while its page is empty.</summary>
    public int BrushObjectIndex => _brushObject[(int)_tool];

    /// <summary>
    /// The objects of the current instrument's picked brush kind, in catalogue order.
    /// </summary>
    /// <remarks>
    /// 还没挑类别时返回空表：参考的物件页也是这样——先选「山、石头、树……」这一层，页面里才有东西。
    /// </remarks>
    public IReadOnlyList<Mp2ObjectPreset> BrushObjects
    {
        get
        {
            IReadOnlyList<ObjectGroup> kinds = BrushKinds;

            if (kinds.Count == 0)
            {
                return [];
            }

            int kind = _brushKind[(int)_tool];

            return kind < 0 || kind >= kinds.Count ? [] : MapObjectCatalogPresets.For(kinds[kind]);
        }
    }

    /// <summary>Selects a brush kind of the current instrument, which empties the object page below it.</summary>
    /// <param name="kind">Entry of <see cref="BrushKinds"/>.</param>
    /// <returns>Whether the selection changed.</returns>
    /// <remarks>
    /// 换类别要清掉下一层的选择：上一条目属于旧类别，留着就成了「类别显示宝物、落子却是棵树」。
    /// </remarks>
    public bool SelectBrushKind(int kind)
    {
        if (kind < 0 || kind >= BrushKinds.Count || _brushKind[(int)_tool] == kind)
        {
            return false;
        }

        _brushKind[(int)_tool] = kind;
        _brushObject[(int)_tool] = -1;
        return true;
    }

    /// <summary>Selects an object of the current brush kind's page, which makes the object tool the active one.</summary>
    /// <param name="index">Entry of <see cref="BrushObjects"/>.</param>
    /// <returns>Whether the selection changed.</returns>
    public bool SelectBrushObject(int index)
    {
        IReadOnlyList<Mp2ObjectPreset> objects = BrushObjects;

        if (index < 0 || index >= objects.Count)
        {
            return false;
        }

        bool changed = _brushObject[(int)_tool] != index;

        _brushObject[(int)_tool] = index;
        _object = objects[index];
        return changed;
    }

    /// <summary>The object the current brush would place, or <see langword="null"/> when nothing was picked yet.</summary>
    public Mp2ObjectPreset? SelectedBrushObject
    {
        get
        {
            IReadOnlyList<Mp2ObjectPreset> objects = BrushObjects;
            int index = _brushObject[(int)_tool];

            return index >= 0 && index < objects.Count ? objects[index] : null;
        }
    }

    /// <summary>
    /// Largest value the data word of a tile holds: <c>quantity1</c> and <c>quantity2</c> are thirteen
    /// bits once the three low bits of <c>quantity1</c> are taken out.
    /// </summary>
    public const int MaxObjectData = 0x1FFF;

    /// <summary>
    /// Reads the data word of the object on a tile, <c>MP2TileInfo.metadata</c>: one number per tile
    /// whose meaning is the object's — the amount of a resource pile, the content of a chest, the kind
    /// of a mine.
    /// </summary>
    /// <param name="tile">Tile to read, must be inside the map.</param>
    /// <returns>The thirteen bit value, 0 for a tile without an object.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tile"/> is outside the map.</exception>
    public int ObjectData(Point tile)
    {
        if (!WithinMap(tile))
        {
            throw new ArgumentOutOfRangeException(nameof(tile), tile, "The tile is outside the map.");
        }

        return (int)Map.Tile(tile.X, tile.Y).Metadata;
    }

    /// <summary>
    /// Writes the data word of the object on a tile, the property the information box of the editor
    /// shows and the <c>[</c> and <c>]</c> keys step.
    /// </summary>
    /// <remarks>
    /// The thirteen bits of the word are the top five bits of <c>quantity1</c> and all of
    /// <c>quantity2</c>; the three low bits of <c>quantity1</c> are outside the word — two of them are
    /// the ground layer the piece is drawn in — and stay as they are. A tile without an object has
    /// nothing to describe and is left alone: the data of an empty tile is part of the empty tile, not
    /// a property of an object.
    /// </remarks>
    /// <param name="tile">Tile to write, ignored when outside the map or <see langword="null"/>.</param>
    /// <param name="data">Value to write, 0 to <see cref="MaxObjectData"/>.</param>
    /// <returns>Whether the tile changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="data"/> does not fit thirteen bits.</exception>
    public bool SetObjectData(Point? tile, int data)
    {
        if (data is < 0 or > MaxObjectData)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data, "The data word holds thirteen bits.");
        }

        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        Mp2TileInfo before = Map.Tile(point.X, point.Y);

        if (!before.HasGroundObject && !before.HasTopObject)
        {
            return false;
        }

        Mp2TileInfo after = before with
        {
            Quantity1 = (byte)((before.Quantity1 & 0x07) | ((data & 0x1F) << 3)),
            Quantity2 = (byte)(data >> 5),
        };

        if (after == before)
        {
            return false;
        }

        bool ownStroke = _stroke is null;
        BeginStroke();

        int index = Map.TileIndex(point.X, point.Y);
        Map.SetTileAt(index, after);
        _stroke!.Record(index, before, after);

        if (ownStroke)
        {
            EndStroke();
        }

        return true;
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
        _tool = EditorTool.LandscapeObjects;
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

        _stroke = new MapStroke(Map);
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
    /// <remarks>
    /// 四个物件仪器走同一条落子路径：它们只差「页面里能选哪些条目」，落子本身都是「把选中的那一件放到格子上」。
    /// 没有选中任何物件时什么也不做——参考的落子也要求先有 <c>_selectedObjectType != -1</c>。
    /// </remarks>
    /// <remarks>
    /// 细节仪器（<see cref="EditorTool.Detail"/>）不在这条路上：它的一次操作要么是打开物件的信息（由界面做），
    /// 要么是一按一放（<see cref="GrabObject"/> 与 <see cref="ReleaseObject"/>），拖到一半不该改编地图。
    /// </remarks>
    /// <param name="tile">Tile under the pointer, ignored when outside the map.</param>
    /// <returns>Whether the tile changed.</returns>
    public bool Apply(Point? tile) => _tool switch
    {
        EditorTool.LandscapeObjects or EditorTool.AdventureObjects or EditorTool.KingdomObjects
            or EditorTool.Monsters => PlaceObject(tile),
        EditorTool.Erase => EraseObject(tile),
        EditorTool.Detail => false,
        EditorTool.Stream => LayStream(tile),
        EditorTool.Road => LayRoad(tile),
        _ => Paint(tile),
    };

    /// <summary>
    /// Lays a road on <paramref name="tile"/>, one step of the road brush
    /// (<see cref="EditorPaths.LayRoad"/>, the <c>setRoadOnTile</c> of the reference).
    /// </summary>
    /// <remarks>
    /// A road is not a brush stroke over one tile: laying it redraws the roads around it, and taking an
    /// old one apart touches the tiles it reached into. All of that shares the stroke of the drag, so a
    /// drag from one end of a road to the other is one undo.
    /// </remarks>
    /// <param name="tile">Tile under the pointer, ignored when outside the map.</param>
    /// <returns>Whether the map changed.</returns>
    public bool LayRoad(Point? tile) => LayPath(tile, EditorPaths.LayRoad);

    /// <summary>
    /// Draws a stream on <paramref name="tile"/>, one step of the stream brush
    /// (<see cref="EditorPaths.LayStream"/>, the <c>addStream</c> of the reference).
    /// </summary>
    /// <param name="tile">Tile under the pointer, ignored when outside the map.</param>
    /// <returns>Whether the map changed.</returns>
    public bool LayStream(Point? tile) => LayPath(tile, EditorPaths.LayStream);

    /// <summary>Runs one road or stream step inside the stroke of the drag (or a stroke of its own).</summary>
    /// <remarks>
    /// 路面把接头面片塞进相邻格的附加链里，所以这一步之后存档不能再走「只补地形」的近路。
    /// </remarks>
    /// <param name="tile">Tile under the pointer.</param>
    /// <param name="lay">The laying routine to run.</param>
    /// <returns>Whether the map changed.</returns>
    private bool LayPath(Point? tile, Func<Mp2Map, Point, MapStroke, bool> lay)
    {
        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        bool ownStroke = _stroke is null;
        BeginStroke();

        if (!lay(Map, point, _stroke!))
        {
            if (ownStroke)
            {
                _stroke = null;
            }

            return false;
        }

        _addonsChanged = true;

        if (ownStroke)
        {
            EndStroke();
        }

        return true;
    }

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
    /// Takes the object of <paramref name="tile"/> for a drag of the detail instrument, the press of
    /// <c>EditorInterface::_tryToMoveObject</c> that fills <c>_movableObjectInfo</c>
    /// (<c>editor_interface.cpp:2906-2935</c>).
    /// </summary>
    /// <remarks>
    /// 只记下格子，不写地图：参考在按下时也只是把物件的 UID 记进 <c>_movableObjectInfo</c>，真正的搬运发生在松开。
    /// 拖到地图外松开、或者按着的时候换了工具，移动就作废，地图一个字都不变。
    /// </remarks>
    /// <param name="tile">Tile under the press, ignored when outside the map or <see langword="null"/>.</param>
    /// <returns>Whether an object was taken, i.e. whether there is something to drop.</returns>
    public bool GrabObject(Point? tile)
    {
        _detailSource = null;

        if (tile is not { } point || !WithinMap(point))
        {
            return false;
        }

        Mp2TileInfo info = Map.Tile(point.X, point.Y);

        if (!info.HasGroundObject && !info.HasTopObject)
        {
            return false;
        }

        _detailSource = point;
        return true;
    }

    /// <summary>
    /// Drops the object taken by <see cref="GrabObject"/> on <paramref name="tile"/>, the release of a
    /// detail drag: <see cref="MoveObject"/> or <see cref="CopyObject"/> depending on
    /// <see cref="DetailMode"/>.
    /// </summary>
    /// <param name="tile">Tile under the release, ignored when outside the map or <see langword="null"/>.</param>
    /// <returns>Whether the map changed.</returns>
    public bool ReleaseObject(Point? tile)
    {
        if (_detailSource is not { } source)
        {
            return false;
        }

        _detailSource = null;

        if (tile is not { } destination)
        {
            return false;
        }

        return _detailMode == EditorDetailMode.Copying
            ? CopyObject(source, destination)
            : MoveObject(source, destination);
    }

    /// <summary>
    /// Moves the object of <paramref name="from"/> to <paramref name="to"/>, the edit
    /// <c>DetailBrushType::MOVING</c> makes on a release (<c>editor_interface.cpp:1748-1780</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 搬运只是在两张格子记录之间搬部件：源格的地面层与顶层、层号、数量、物件类型原样写进目标格，源格清成空件，
    /// 两张格子的地形图与地形位不动。宽度超过一格的物件在端口里本来就只占一张格子记录（其余格子的面片挂在附加链上，
    /// 见 <see cref="PlaceObject"/>），所以整件东西跟着走。
    /// </para>
    /// <para>
    /// 附加链不复制：搬走的物件仍用原来那条链，目标格直接指向它，链上的面片盖着的 UID 也没变；
    /// 源格只是把 <see cref="Mp2TileInfo.NextAddonIndex"/> 清掉，链就归目标格所有了。
    /// </para>
    /// <para>
    /// 目标格已经画着同样的东西时拒绝（参考的 "The same object exists on the tile."）：拖回原处、或者拖到另一棵同样的树上都不算编辑，
    /// 否则一次误操作就把物件搬没了。
    /// </para>
    /// </remarks>
    /// <param name="from">Tile holding the object, ignored when outside the map or <see langword="null"/>.</param>
    /// <param name="to">Tile to move it to, ignored when outside the map or <see langword="null"/>.</param>
    /// <returns>Whether the map changed.</returns>
    public bool MoveObject(Point? from, Point? to) => TransferObject(from, to, copy: false);

    /// <summary>
    /// Copies the object of <paramref name="from"/> onto <paramref name="to"/>, the edit
    /// <c>DetailBrushType::COPYING</c> makes (<c>editor_interface.cpp:1675-1745</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="MoveObject"/> 的差别只有两处：源格不动；目标格拿到新的 UID，附加链也整条复制一份（链上的面片各挂一个
    /// 新 UID），否则两张格子会共用一条链，改其中一个的旗子会连带改另一个。
    /// </remarks>
    /// <param name="from">Tile holding the object, ignored when outside the map or <see langword="null"/>.</param>
    /// <param name="to">Tile to copy it to, ignored when outside the map or <see langword="null"/>.</param>
    /// <returns>Whether the map changed.</returns>
    public bool CopyObject(Point? from, Point? to) => TransferObject(from, to, copy: true);

    /// <summary>The body of <see cref="MoveObject"/> and <see cref="CopyObject"/>.</summary>
    /// <param name="from">Tile holding the object.</param>
    /// <param name="to">Tile to write the object to.</param>
    /// <param name="copy">Whether the source stays as it is and the object gets new UIDs.</param>
    /// <returns>Whether the map changed.</returns>
    private bool TransferObject(Point? from, Point? to, bool copy)
    {
        if (from is not { } source || to is not { } destination)
        {
            return false;
        }

        if (!WithinMap(source) || !WithinMap(destination) || source == destination)
        {
            return false;
        }

        Mp2TileInfo carried = Map.Tile(source.X, source.Y);

        if (!carried.HasGroundObject && !carried.HasTopObject)
        {
            return false;
        }

        Mp2TileInfo target = Map.Tile(destination.X, destination.Y);

        if (SameObject(carried, target))
        {
            return false;
        }

        bool ownStroke = _stroke is null;
        BeginStroke();

        uint groundUid = carried.HasGroundObject ? (copy ? Map.TakeObjectUid() : carried.Level1ObjectUid) : 0;
        uint topUid = carried.HasTopObject ? (copy ? Map.TakeObjectUid() : carried.Level2ObjectUid) : 0;
        ushort chain = carried.NextAddonIndex;

        if (copy && chain != 0)
        {
            uint pieceUid = groundUid != 0 ? groundUid : topUid;

            // 链是先进后出的：照着原来的顺序反过来接，目标格的链首才是原来那条链的首。
            List<Mp2AddonInfo> pieces = Map.EnumerateAddonChain(carried)
                .Select(index => Map.Addons[index])
                .ToList();

            chain = 0;
            for (int index = pieces.Count - 1; index >= 0; --index)
            {
                chain = (ushort)Map.AppendAddon(pieces[index] with
                {
                    NextAddonIndex = chain,
                    Level1ObjectUid = pieceUid,
                    Level2ObjectUid = 0,
                });
            }

            _addonsChanged = true;
        }

        Mp2TileInfo placed = target with
        {
            ObjectName1 = carried.ObjectName1,
            BottomIcnImageIndex = carried.BottomIcnImageIndex,
            Quantity1 = carried.Quantity1,
            Quantity2 = carried.Quantity2,
            ObjectName2 = carried.ObjectName2,
            TopIcnImageIndex = carried.TopIcnImageIndex,
            ObjectType = carried.ObjectType,
            NextAddonIndex = chain,
            Level1ObjectUid = groundUid,
            Level2ObjectUid = topUid,
        };

        bool changed = WriteObject(destination, placed);

        if (!copy)
        {
            changed |= WriteObject(source, carried with
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
            });
        }

        if (ownStroke)
        {
            EndStroke();
        }

        return changed;
    }

    /// <summary>
    /// Whether two tiles show the same object, the check behind the "The same object exists on the tile."
    /// warning of a move or a copy.
    /// </summary>
    /// <remarks>
    /// 比较的是画出来的东西（两个部件的类型与图号、物件类型），不是数量或元数据：参考拿物件目录里的组与序号比
    /// （<c>_getSameObjectPresentOnTile</c>），数量属于同一个物件的状态，不构成「另一件」。
    /// </remarks>
    /// <param name="left">One tile.</param>
    /// <param name="right">The other tile.</param>
    /// <returns>Whether both tiles hold the same object and at least one of them holds something.</returns>
    private static bool SameObject(Mp2TileInfo left, Mp2TileInfo right)
        => (left.HasGroundObject || left.HasTopObject)
           && left.ObjectType == right.ObjectType
           && left.ObjectName1 == right.ObjectName1
           && left.BottomIcnImageIndex == right.BottomIcnImageIndex
           && left.ObjectName2 == right.ObjectName2
           && left.TopIcnImageIndex == right.TopIcnImageIndex;

    /// <summary>Writes one tile record into the map and the open stroke.</summary>
    /// <param name="point">Tile to write, must be inside the map.</param>
    /// <param name="after">The record to store.</param>
    /// <returns>Whether the tile changed.</returns>
    private bool WriteObject(Point point, Mp2TileInfo after)
    {
        Mp2TileInfo before = Map.Tile(point.X, point.Y);

        if (after == before)
        {
            return false;
        }

        int index = Map.TileIndex(point.X, point.Y);
        Map.SetTileAt(index, after);
        _stroke!.Record(index, before, after);
        return true;
    }

    /// <summary>
    /// Clears the objects under the brush and keeps their ground, the eraser of the editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result is the empty tile a shipped map stores: no ICN type, sprite index 255 on both parts, a zeroed
    /// layer and metadata and UIDs of 0, with the terrain image and the terrain flags untouched. Every tile of the
    /// brush area is cleared, not just the pointed one: the reference applies <c>getBrushArea()</c> of the eraser
    /// too, which is what the size buttons of the panel are for.
    /// </para>
    /// <para>
    /// 只删勾选的类别：参考把 <c>getEraseObjectGroups()</c> 一起交给 <c>_removeObjectsAsAction</c>，不在集合里的物件原样
    /// 留着（<c>editor_interface.cpp:1856</c>）。端口没有「每格一张物件表」，类别只能从画出这一格的那个面片反查
    /// （<see cref="MapObjectCatalog.GetGroundGroup"/>）；目录里查不到的部件按参考的做法跳过——参考同样只删它认得的物体。
    /// </para>
    /// <para>
    /// 一格可以同时有地面层与顶层两个部件，两者的类别未必相同：只清掉勾中的那一半，另一半连同它的 UID 留着。地面部件被清掉时
    /// 附加链的引用一并断开（链上的面片都盖着地面物件的 UID），物件类型按剩下的部件重算（<see cref="RemainingType"/>）。
    /// </para>
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

    /// <summary>The catalogue groups the checked types cover, built once per change of the checks.</summary>
    private IReadOnlySet<ObjectGroup> EraseGroups => _eraseGroups ??= EditorEraseTypes.Groups(_eraseTypes);

    /// <summary>Clears the parts of one tile whose group is checked, the body of <see cref="EraseObject"/>.</summary>
    /// <param name="point">Tile to clear, must be inside the map.</param>
    /// <returns>Whether the tile changed.</returns>
    private bool EraseTile(Point point)
    {
        Mp2TileInfo before = Map.Tile(point.X, point.Y);

        bool eraseGround = before.HasGroundObject && EraseGroups.Contains(MapObjectCatalog.GetGroundGroup(before));
        bool eraseTop = before.HasTopObject && EraseGroups.Contains(MapObjectCatalog.GetTopGroup(before));

        if (!eraseGround && !eraseTop)
        {
            return false;
        }

        Mp2TileInfo after = before;

        if (eraseGround)
        {
            after = after with
            {
                ObjectName1 = 0,
                BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
                Quantity1 = 0,
                Quantity2 = 0,
                NextAddonIndex = 0,
                Level1ObjectUid = 0,
            };
        }

        if (eraseTop)
        {
            after = after with
            {
                ObjectName2 = 0,
                TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
                Level2ObjectUid = 0,
            };
        }

        after = after with { ObjectType = RemainingType(after) };

        if (after == before)
        {
            return false;
        }

        int tileIndex = Map.TileIndex(point.X, point.Y);
        Map.SetTileAt(tileIndex, after);
        _stroke!.Record(tileIndex, before, after);
        return true;
    }

    /// <summary>
    /// The object type a tile reports once one of its parts was erased: the ground part decides while it is there,
    /// otherwise the top part does, and a tile with neither reports <see cref="Mp2ObjectType.None"/>.
    /// </summary>
    /// <param name="tile">The tile as the eraser left it.</param>
    /// <returns>The type the map file would store.</returns>
    private static Mp2ObjectType RemainingType(Mp2TileInfo tile)
    {
        if (tile.HasGroundObject)
        {
            return tile.ObjectType;
        }

        return tile.HasTopObject
            ? MapObjectCatalog.GetObjectTypeByIcn(tile.TopIcnType, tile.TopIcnImageIndex)
            : Mp2ObjectType.None;
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

        IEditStep step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        _ = step.Undo();
        _redo.Add(step);

        return true;
    }

    /// <summary>Applies the last undone step again.</summary>
    /// <returns>Whether there was anything to redo.</returns>
    public bool Redo()
    {
        EndStroke();

        if (_redo.Count == 0)
        {
            return false;
        }

        IEditStep step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);

        _ = step.Redo();
        _undo.Add(step);

        return true;
    }

    /// <summary>
    /// Puts a step that was not a tile stroke onto the same history, e.g. a change of one metadata table
    /// (<c>fheroes2::ActionCreator</c> committing into the single <c>HistoryManager</c> of the reference).
    /// </summary>
    /// <remarks>
    /// 步骤自己要先把「改之前」与「改之后」都准备好——<see cref="MetadataMapAction{T}"/> 的构造捕获前者、
    /// <see cref="MetadataMapAction{T}.Prepare"/> 捕获后者，正好是参考的构造加 <c>commit()</c>。压进来以后
    /// <kbd>Ctrl</kbd>+<kbd>Z</kbd> 会按时间顺序同时覆盖地块与元数据。
    /// </remarks>
    /// <param name="step">The step to record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="step"/> is <see langword="null"/>.</exception>
    public void RecordStep(IEditStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        _undo.Add(step);

        if (_undo.Count > MaxUndoDepth)
        {
            _undo.RemoveAt(0);
            _savedDepth = Math.Max(0, _savedDepth - 1);
        }

        _redo.Clear();
    }

    /// <summary>
    /// Replaces the header of the map with the one the map specifications window produced,
    /// <see cref="MapSpecsState.CreateInfo"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 头部不进撤销历史：规格窗没有「撤销」按钮，参考也是确定即生效。换过头部以后 <see cref="SaveAs"/>
    /// 会把头部一起写回文件（<see cref="Mp2MapWriter.Save"/> 的 <c>writeHeader</c>），否则补丁式保存仍然
    /// 留着旧名称。
    /// </para>
    /// <para>
    /// 只允许换同一个尺寸的头部：地形区的大小由头部的宽高决定，换了尺寸地图就散架了。
    /// </para>
    /// </remarks>
    /// <param name="info">The new header, normally <see cref="MapSpecsState.CreateInfo"/>'s result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The header has another map size.</exception>
    public void ApplyMapInfo(Mp2FileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (info.Width != Map.Width || info.Height != Map.Height)
        {
            throw new ArgumentException(
                $"The map is {Map.Width}x{Map.Height}, but the header is {info.Width}x{info.Height}.",
                nameof(info));
        }

        Map.Info = info;
        _headerChanged = true;
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
    /// the patch does not touch, so the file is written whole from then on. A header replaced by
    /// <see cref="ApplyMapInfo"/> is written over the original one in the same patch.
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
                Mp2MapWriter.Save(filePath, Map, _savePath, writeHeader: _headerChanged);
            }

            _savePath = filePath;
            _addonsChanged = false;
            _headerChanged = false;
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
internal sealed class MapStroke : IEditStep
{
    private readonly Dictionary<int, MapTileChange> _changes = [];

    private readonly Mp2Map _map;

    /// <summary>
    /// Creates a stroke over a map; the map is kept by reference, which is enough because
    /// <see cref="Mp2Map.SetTileAt"/> writes into the tile array the map already holds.
    /// </summary>
    /// <param name="map">The map the stroke edits.</param>
    public MapStroke(Mp2Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
    }

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

    /// <inheritdoc/>
    public bool Undo() => Apply(reverse: true);

    /// <inheritdoc/>
    public bool Redo() => Apply(reverse: false);

    /// <summary>Writes one side of the stroke back into the map.</summary>
    /// <param name="reverse">Whether to write the "before" or the "after" side.</param>
    /// <returns>Always <see langword="true"/>: writing tiles back cannot fail.</returns>
    private bool Apply(bool reverse)
    {
        foreach (MapTileChange change in _changes.Values)
        {
            _map.SetTileAt(change.Index, reverse ? change.Before : change.After);
        }

        return true;
    }
}
