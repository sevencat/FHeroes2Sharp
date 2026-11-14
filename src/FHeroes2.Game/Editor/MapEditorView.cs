using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The state of the map editor, first milestone: what is under the pointer and where the tile grid
/// goes. The scene owns the pixels, this class owns the answers, so the hover and the grid can be
/// tested without a frame.
///
/// <para>
/// The reference does have an editor (<c>src/fheroes2/editor/</c>, reached through the EDITOR button
/// of <c>Game::MainMenu()</c>), and it draws the map through the very same <c>GameArea</c> the
/// adventure map uses - <c>Editor::Interface</c> only moves that area with
/// <c>SetAreaPosition( borderWidthPx, borderWidthPx, ... )</c>. That is why the rendering here is
/// <see cref="AdventureMapView"/> as well: the frame, the tile grid and the hover highlight are ours
/// (the reference keeps them in <c>Editor::Interface</c> and <c>EditorPanel</c>), the map is not.
/// The ground type of a tile comes back from the terrain image index through
/// <c>Maps::Ground::getGroundByImageIndex</c>, exactly as the minimap does.
/// </para>
/// </summary>
public sealed class MapEditorView
{
    private readonly AdventureMapView _map;

    private Point? _cursor;

    /// <summary>Wraps the adventure map view that draws the terrain.</summary>
    /// <param name="map">The view of the map being edited.</param>
    public MapEditorView(AdventureMapView map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
    }

    /// <summary>The map view the editor shares with the adventure map.</summary>
    public AdventureMapView Map => _map;

    /// <summary>Whether the tile grid is painted, on by default as in every map editor.</summary>
    public bool GridEnabled { get; private set; } = true;

    /// <summary>The pointer in logical pixels, <see langword="null"/> outside the window.</summary>
    public Point? Cursor => _cursor;

    /// <summary>Turns the tile grid on or off and returns the new state, the <c>G</c> key.</summary>
    public bool ToggleGrid()
    {
        GridEnabled = !GridEnabled;
        return GridEnabled;
    }

    /// <summary>Records the pointer; nothing else is cached, so scrolling moves the hover with it.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor) => _cursor = cursor;

    /// <summary>
    /// The map tile under the pointer, or <see langword="null"/> when the pointer is outside the
    /// viewport or over the empty border a map smaller than the screen leaves.
    /// </summary>
    /// <remarks>
    /// Computed on demand rather than when the pointer moves: a scroll changes the tile under a
    /// still pointer, so a cached value would show the wrong tile until the mouse is moved.
    /// </remarks>
    public Point? HoverTile
    {
        get
        {
            if (_cursor is not { } point || !InsideViewport(point))
            {
                return null;
            }

            Point tile = _map.TileAt(point);

            return WithinMap(tile) ? tile : null;
        }
    }

    /// <summary>The tile structure under the pointer, <see langword="null"/> when there is none.</summary>
    public Mp2TileInfo? Hover => HoverTile is { } tile
        ? _map.Map.Tiles[_map.Map.TileIndex(tile.X, tile.Y)]
        : null;

    /// <summary>The ground under the pointer, <see cref="MapGround.Unknown"/> when there is none.</summary>
    public MapGround HoverGround => Hover is { } hover
        ? MapGrounds.FromTerrainImageIndex(hover.TerrainImageIndex)
        : MapGround.Unknown;

    /// <summary>
    /// X positions of the visible tile borders in viewport pixels, left to right; empty when the grid
    /// is off. The right edge of the last visible column is included when it falls inside the frame,
    /// which is what closes the grid on a map smaller than the screen.
    /// </summary>
    /// <returns>One entry per visible border, ascending.</returns>
    public IReadOnlyList<int> GridColumns() =>
        GridEnabled
            ? Borders(_map.Tiles.TileWidth, _map.Scroll.X, _map.Map.Width, _map.Viewport.Width)
            : [];

    /// <summary>Y positions of the visible tile borders in viewport pixels, top to bottom.</summary>
    /// <returns>One entry per visible border, ascending.</returns>
    public IReadOnlyList<int> GridRows() =>
        GridEnabled
            ? Borders(_map.Tiles.TileHeight, _map.Scroll.Y, _map.Map.Height, _map.Viewport.Height)
            : [];

    /// <summary>
    /// The viewport rectangle of the tile under the pointer, or <see langword="null"/> without a
    /// hover; the scene outlines it, which is how a mouse click says "this tile".
    /// </summary>
    public Rect? HoverArea
    {
        get
        {
            if (HoverTile is not { } tile)
            {
                return null;
            }

            return new Rect(
                (tile.X * _map.Tiles.TileWidth) - _map.Scroll.X,
                (tile.Y * _map.Tiles.TileHeight) - _map.Scroll.Y,
                _map.Tiles.TileWidth,
                _map.Tiles.TileHeight);
        }
    }

    private bool InsideViewport(Point point) =>
        point.X >= 0 && point.Y >= 0
        && point.X < _map.Viewport.Width && point.Y < _map.Viewport.Height;

    private bool WithinMap(Point tile) =>
        tile.X >= 0 && tile.Y >= 0
        && tile.X < _map.Map.Width && tile.Y < _map.Map.Height;

    /// <summary>
    /// Every tile border of one axis that falls into the viewport, <c>i * size - scroll</c> for the
    /// <c>i</c> from 0 to the number of tiles.
    /// </summary>
    private static IReadOnlyList<int> Borders(int size, int scroll, int count, int viewportExtent)
    {
        List<int> positions = [];

        for (int i = 0; i <= count; ++i)
        {
            int position = (i * size) - scroll;

            if (position >= 0 && position < viewportExtent)
            {
                positions.Add(position);
            }
        }

        return positions;
    }
}
