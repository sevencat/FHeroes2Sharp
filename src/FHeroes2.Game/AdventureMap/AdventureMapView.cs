using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The scrolling view of an MP2 map, the equivalent of <c>Interface::GameArea</c> plus
/// <c>Maps::redrawTile()</c> of the reference engine.
///
/// <para>
/// The map is drawn one layer at a time over the whole viewport, never tile by tile: the ground
/// image <c>GROUND32.TIL[ terrainImageIndex ]</c> mirrored by the two bit terrain shape first, then
/// the four ground object layers in the order of the reference, the heroes on top of them and
/// finally the top object parts.
/// </para>
/// <para>
/// Fog, the monsters, the boats and the interface panels are later milestones. Monsters and boats
/// are not merely "not drawn yet": like heroes their sprites are split over several tiles by the
/// engine, so they need their own renderer instead of the generic object path.
/// </para>
/// </summary>
public sealed class AdventureMapView
{
    /// <summary>Number of ground object layers, the range of <c>quantity1 &amp; 3</c>.</summary>
    public const int GroundLayerCount = 4;

    /// <summary>
    /// Order in which the ground layers are painted, the port of the four
    /// <c>redrawBottomLayerObjects()</c> calls of <c>Interface::GameArea::Redraw()</c>:
    /// <c>TERRAIN_LAYER</c> (roads, water flaws, cracks) first, then <c>BACKGROUND_LAYER</c>
    /// (lakes, bushes), <c>SHADOW_LAYER</c> and finally <c>OBJECT_LAYER</c> (mines, forests,
    /// mountains, castles). Ascending order would put the roads on top of the objects.
    /// </summary>
    public static IReadOnlyList<int> LayerPassOrder { get; } = [TerrainLayer, BackgroundLayer, ShadowLayer, ObjectLayer];

    /// <summary>Roads, water flaws and cracks: the layer drawn first.</summary>
    public const int TerrainLayer = 3;

    /// <summary>Lakes, bushes and other passable background objects.</summary>
    public const int BackgroundLayer = 1;

    /// <summary>Shadows and road tiles of a castle.</summary>
    public const int ShadowLayer = 2;

    /// <summary>Mines, forests, mountains and castles: the layer drawn last among the ground parts.</summary>
    public const int ObjectLayer = 0;

    private readonly Mp2Map _map;
    private readonly TerrainTileSet _tiles;
    private readonly IObjectSpriteSource? _objects;
    private readonly IHeroSpriteSource? _heroes;
    private readonly Size _viewport;

    public AdventureMapView(
        Mp2Map map,
        TerrainTileSet tiles,
        Size viewport,
        IObjectSpriteSource? objects = null,
        IHeroSpriteSource? heroes = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tiles);

        _map = map;
        _tiles = tiles;
        _objects = objects;
        _heroes = heroes;
        _viewport = viewport;
        Scroll = new Point(0, 0);
    }

    /// <summary>The map being drawn.</summary>
    public Mp2Map Map => _map;

    /// <summary>The terrain tiles the ground layer is built from.</summary>
    public TerrainTileSet Tiles => _tiles;

    /// <summary>The object sprites, <see langword="null"/> when only the ground layer is drawn.</summary>
    public IObjectSpriteSource? Objects => _objects;

    /// <summary>The hero sprites, <see langword="null"/> when heroes are left out.</summary>
    public IHeroSpriteSource? Heroes => _heroes;

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
    /// Draws the visible part of the map into <paramref name="target"/>; the caller is expected to
    /// have cleared or otherwise prepared the pixels the map does not cover.
    /// </summary>
    /// <remarks>
    /// The passes are per layer over the whole viewport, not per tile, which is what the reference
    /// does (<c>Interface::GameArea::Redraw()</c>): a sprite that reaches over a tile border is not
    /// covered by the terrain of the next tile, and an object of one layer is drawn above an object
    /// of an earlier pass even when that object sits on the next tile.
    /// </remarks>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        DrawTerrain(target);

        if (_objects is null && _heroes is null)
        {
            return;
        }

        foreach (int layer in LayerPassOrder)
        {
            DrawGroundLayer(target, layer);

            if (layer == ShadowLayer)
            {
                // Shadows of the tile-unfit objects, then whatever a hero draws below its own tile.
                DrawHeroShadows(target);
                DrawHeroes(target, HeroBand.BelowTile);
            }

            // A hero's body and flag stand on its own tile: the "middle" part of the reference, drawn
            // after OBJECT_LAYER and before the top layer.
            if (layer == ObjectLayer)
            {
                DrawHeroes(target, HeroBand.OnTile);
            }
        }

        DrawTopLayer(target);

        // The rows of a hero sprite that reach over the tile above are drawn last of all, so a castle
        // tower or the flag of a tall object never covers the hero's head.
        DrawHeroes(target, HeroBand.AboveTile);
    }

    /// <summary>Which row range of a hero sprite is currently being drawn.</summary>
    private enum HeroBand
    {
        /// <summary>Rows above the hero's tile, the <c>topImages</c> of the reference.</summary>
        AboveTile,

        /// <summary>Rows inside the hero's tile, the <c>bottomImages</c> of the reference.</summary>
        OnTile,

        /// <summary>Rows below the hero's tile, the <c>heroBackgroundImages</c> of the reference.</summary>
        BelowTile,
    }

    /// <summary>
    /// Draws the part of every hero that falls into <paramref name="band"/>, the port of the
    /// <c>tileUnfit.*Images</c> passes of <c>Interface::GameArea::Redraw()</c>. The body comes from the
    /// tile set of the hero's race and the flag from the tile set of its kingdom, both anchored on the
    /// tile the hero occupies.
    /// </summary>
    private void DrawHeroes(PixelBuffer target, HeroBand band)
    {
        if (_heroes is null)
        {
            return;
        }

        foreach (Mp2MapHero hero in VisibleHeroes())
        {
            int left = (hero.X * _tiles.TileWidth) - Scroll.X;
            int top = (hero.Y * _tiles.TileHeight) - Scroll.Y;

            BlitHeroSprite(target, _heroes.GetBody(hero.Race), left, top, band);
            BlitHeroSprite(target, _heroes.GetFlag(hero.Color), left, top, band);
        }
    }

    /// <summary>
    /// Draws the shadows of the heroes, the port of <c>tileUnfit.shadowImages</c>. A shadow is a flat
    /// ellipse lying on the tile and one tile to its left, so it is drawn with the shadow layer and
    /// never needs splitting.
    /// </summary>
    private void DrawHeroShadows(PixelBuffer target)
    {
        if (_heroes is null)
        {
            return;
        }

        Sprite? shadow = _heroes.GetShadow();
        if (shadow is null)
        {
            return;
        }

        foreach (Mp2MapHero hero in VisibleHeroes())
        {
            int left = (hero.X * _tiles.TileWidth) - Scroll.X + shadow.OffsetX;
            int top = (hero.Y * _tiles.TileHeight) - Scroll.Y + shadow.OffsetY;

            ImageOps.BlitSprite(target, new Point(left, top), shadow);
        }
    }

    /// <summary>
    /// Draws the rows of <paramref name="sprite"/> that belong to <paramref name="band"/>. The split
    /// is the one of <c>DivideImageBySquares</c>: every 32 pixel row of the sprite belongs to the tile
    /// its top edge falls into, and the three groups are painted at three different moments.
    /// </summary>
    private void BlitHeroSprite(PixelBuffer target, Sprite? sprite, int left, int top, HeroBand band)
    {
        if (sprite is null)
        {
            return;
        }

        int spriteLeft = left + sprite.OffsetX;
        int spriteTop = top + sprite.OffsetY;
        int tileTop = top;
        int tileBottom = top + _tiles.TileHeight;

        int firstRow = band switch
        {
            HeroBand.AboveTile => spriteTop,
            HeroBand.OnTile => Math.Max(spriteTop, tileTop),
            _ => Math.Max(spriteTop, tileBottom),
        };

        int lastRow = band switch
        {
            HeroBand.AboveTile => Math.Min(spriteTop + sprite.Height, tileTop),
            HeroBand.OnTile => Math.Min(spriteTop + sprite.Height, tileBottom),
            _ => spriteTop + sprite.Height,
        };

        if (lastRow <= firstRow)
        {
            return;
        }

        ImageOps.BlitSprite(
            target,
            new Point(spriteLeft, spriteTop),
            sprite,
            new Rect(0, firstRow - spriteTop, sprite.Width, lastRow - firstRow));
    }

    /// <summary>
    /// The heroes that can reach the viewport. A hero sprite is about one and a half tiles tall and its
    /// shadow reaches one tile to the left, so the visible area is grown by two tiles on every side
    /// before the test; the blit clips the rest.
    /// </summary>
    private IEnumerable<Mp2MapHero> VisibleHeroes()
    {
        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;

        int firstColumn = (Scroll.X / tileWidth) - 2;
        int firstRow = (Scroll.Y / tileHeight) - 2;
        int lastColumn = ((Scroll.X + _viewport.Width - 1) / tileWidth) + 2;
        int lastRow = ((Scroll.Y + _viewport.Height - 1) / tileHeight) + 2;

        foreach (Mp2MapHero hero in _map.Heroes)
        {
            if (hero.X >= firstColumn && hero.X <= lastColumn
                && hero.Y >= firstRow && hero.Y <= lastRow)
            {
                yield return hero;
            }
        }
    }

    /// <summary>Draws the ground image of every visible tile.</summary>
    private void DrawTerrain(PixelBuffer target)
    {
        foreach ((int column, int row, int left, int top, Mp2TileInfo tile) in VisibleTiles())
        {
            PixelBuffer? image = _tiles.GetTile(tile.TerrainImageIndex, tile.TerrainShape);
            if (image is not null)
            {
                ImageOps.Blit(target, new Point(left, top), image, image.Bounds);
            }
        }
    }

    /// <summary>
    /// Draws every ground part whose layer is <paramref name="layer"/>, the port of
    /// <c>redrawBottomLayerObjects(tile, ..., level)</c>. The piece stored on the tile itself is the
    /// main object of the tile; the add-on chain holds the remaining pieces of the same object.
    /// </summary>
    private void DrawGroundLayer(PixelBuffer target, int layer)
    {
        foreach ((int column, int row, int left, int top, Mp2TileInfo tile) in VisibleTiles())
        {
            foreach (int index in _map.EnumerateAddonChain(tile))
            {
                Mp2AddonInfo addon = _map.Addons[index];
                if (addon.GroundIcnType != Mp2ObjectIcnType.Unknown && addon.GroundLayer == layer)
                {
                    BlitPart(target, addon.GroundIcnType, addon.BottomIcnImageIndex, left, top);
                }
            }

            if (tile.GroundLayer == layer)
            {
                BlitPart(target, tile.GroundIcnType, tile.BottomIcnImageIndex, left, top);
            }
        }
    }

    /// <summary>
    /// Draws every top layer part, the port of <c>redrawTopLayerObject()</c>. Tall top layer objects
    /// (flags, ghosts, some ruins) reach over the tile above in the reference and are drawn last;
    /// that pass is not ported yet, so their extra rows are drawn in tile order.
    /// </summary>
    private void DrawTopLayer(PixelBuffer target)
    {
        foreach ((int column, int row, int left, int top, Mp2TileInfo tile) in VisibleTiles())
        {
            if (tile.HasTopObject)
            {
                BlitPart(target, tile.TopIcnType, tile.TopIcnImageIndex, left, top);
            }

            foreach (int index in _map.EnumerateAddonChain(tile))
            {
                Mp2AddonInfo addon = _map.Addons[index];
                if (addon.TopIcnType != Mp2ObjectIcnType.Unknown)
                {
                    BlitPart(target, addon.TopIcnType, addon.TopIcnImageIndex, left, top);
                }
            }
        }
    }

    /// <summary>Enumerates the visible tiles with their viewport position and data, row major.</summary>
    private IEnumerable<(int Column, int Row, int Left, int Top, Mp2TileInfo Tile)> VisibleTiles()
    {
        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;
        Rect area = VisibleTileArea;

        for (int row = area.Y; row < area.Bottom; ++row)
        {
            int top = (row * tileHeight) - Scroll.Y;

            for (int column = area.X; column < area.Right; ++column)
            {
                yield return (column, row, (column * tileWidth) - Scroll.X, top, _map.Tile(column, row));
            }
        }
    }

    /// <summary>
    /// Blits one object part, skipping the tile sets the object path must not draw
    /// (<c>MINIHERO</c>, <c>MONS32</c>, …). Those objects have their own renderers, which ask for
    /// their sprites through <see cref="IHeroSpriteSource"/> instead.
    /// </summary>
    private void BlitPart(PixelBuffer target, Mp2ObjectIcnType icnType, int icnIndex, int left, int top)
    {
        if (!Mp2ObjectIcnTypes.IsDrawnAsObjectPart(icnType))
        {
            return;
        }

        BlitSprite(target, icnType, icnIndex, left, top);
    }

    /// <summary>
    /// Blits one sprite at the tile position plus the anchor stored in the sprite, which is what
    /// <c>Interface::GameArea::BlitOnTile( ..., sprite, sprite.x(), sprite.y(), ... )</c> does.
    /// </summary>
    private void BlitSprite(PixelBuffer target, Mp2ObjectIcnType icnType, int icnIndex, int left, int top)
    {
        // 255 is the file's "no sprite" marker, not a frame index.
        if (icnIndex is < 0 or Mp2TileInfo.EmptyImageIndex)
        {
            return;
        }

        Sprite? sprite = _objects?.GetSprite(icnType, icnIndex);
        if (sprite is null)
        {
            return;
        }

        ImageOps.BlitSprite(target, new Point(left + sprite.OffsetX, top + sprite.OffsetY), sprite);
    }
}
