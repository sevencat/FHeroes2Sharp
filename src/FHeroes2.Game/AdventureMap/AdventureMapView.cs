using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The scrolling view of an MP2 map's terrain layer, the equivalent of <c>Interface::GameArea</c>
/// plus <c>Maps::getTileSurface()</c> of the reference engine.
///
/// <para>
/// M4 draws the ground layer only: every tile is replaced by
/// <c>GROUND32.TIL[ terrainImageIndex ]</c> mirrored by the two bit terrain shape, which is exactly
/// what <c>Maps::getTileSurface()</c> returns. Objects, fog, the heroes and the interface panels are
/// later milestones; the scroll position, the clamping and the tile loop are already the final ones.
/// </para>
/// </summary>
public sealed class AdventureMapView
{
    private readonly Mp2Map _map;
    private readonly TerrainTileSet _tiles;
    private readonly Size _viewport;

    public AdventureMapView(Mp2Map map, TerrainTileSet tiles, Size viewport)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tiles);

        _map = map;
        _tiles = tiles;
        _viewport = viewport;
        Scroll = new Point(0, 0);
    }

    /// <summary>The map being drawn.</summary>
    public Mp2Map Map => _map;

    /// <summary>The terrain tiles the ground layer is built from.</summary>
    public TerrainTileSet Tiles => _tiles;

    /// <summary>Size of the visible area, which is the logical frame for now.</summary>
    public Size Viewport => _viewport;

    /// <summary>Pixel size of the whole map.</summary>
    public Size MapPixelSize => new(_map.Width * _tiles.TileWidth, _map.Height * _tiles.TileHeight);

    /// <summary>Top left corner of the visible part of the map, in map pixels.</summary>
    public Point Scroll { get; private set; }

    /// <summary>Largest valid scroll offset, <c>max( 0, map - viewport )</c> per axis.</summary>
    public Point MaximumScroll => new(
        Math.Max(MapPixelSize.Width - _viewport.Width, 0),
        Math.Max(MapPixelSize.Height - _viewport.Height, 0));

    /// <summary>Whether the whole map fits into the viewport, i.e. scrolling does nothing.</summary>
    public bool IsScrollingPossible => MaximumScroll.X > 0 || MaximumScroll.Y > 0;

    /// <summary>Scrolls by <paramref name="dx"/>, <paramref name="dy"/> map pixels, clamped to the map.</summary>
    public void ScrollBy(int dx, int dy) => ScrollTo(Scroll.X + dx, Scroll.Y + dy);

    /// <summary>Scrolls to an absolute offset, clamped to <see cref="MaximumScroll"/>.</summary>
    public void ScrollTo(int x, int y)
    {
        Point maximum = MaximumScroll;
        Scroll = new Point(Math.Clamp(x, 0, maximum.X), Math.Clamp(y, 0, maximum.Y));
    }

    /// <summary>Centres the view on a tile, clamped to the map.</summary>
    public void CenterOn(int tileX, int tileY)
    {
        ScrollTo(
            (tileX * _tiles.TileWidth) - (_viewport.Width / 2),
            (tileY * _tiles.TileHeight) - (_viewport.Height / 2));
    }

    /// <summary>The tile whose top left pixel contains <paramref name="point"/> of the viewport.</summary>
    public Point TileAt(Point point) => new(
        (Scroll.X + point.X) / _tiles.TileWidth,
        (Scroll.Y + point.Y) / _tiles.TileHeight);

    /// <summary>The tile rectangle of the viewport: first and last column and row, both inclusive.</summary>
    public Rect VisibleTileArea
    {
        get
        {
            int firstColumn = Scroll.X / _tiles.TileWidth;
            int firstRow = Scroll.Y / _tiles.TileHeight;
            int lastColumn = Math.Min((Scroll.X + _viewport.Width - 1) / _tiles.TileWidth, _map.Width - 1);
            int lastRow = Math.Min((Scroll.Y + _viewport.Height - 1) / _tiles.TileHeight, _map.Height - 1);

            return new Rect(firstColumn, firstRow, lastColumn - firstColumn + 1, lastRow - firstRow + 1);
        }
    }

    /// <summary>
    /// Draws the visible terrain into <paramref name="target"/>; the caller is expected to have
    /// cleared or otherwise prepared the pixels the map does not cover.
    /// </summary>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;
        Rect area = VisibleTileArea;

        for (int row = area.Y; row < area.Bottom; ++row)
        {
            int top = (row * tileHeight) - Scroll.Y;

            for (int column = area.X; column < area.Right; ++column)
            {
                Mp2TileInfo tile = _map.Tile(column, row);
                PixelBuffer? image = _tiles.GetTile(tile.TerrainImageIndex, tile.TerrainShape);
                if (image is null)
                {
                    continue;
                }

                ImageOps.Blit(target, new Point((column * tileWidth) - Scroll.X, top), image, image.Bounds);
            }
        }
    }
}
