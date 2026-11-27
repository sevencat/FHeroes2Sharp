using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The scrolling view of an MP2 map, the equivalent of <c>Interface::GameArea</c> plus
/// <c>Maps::redrawTile()</c> of the reference engine.
///
/// <para>
/// The map is drawn one layer at a time over the whole viewport, never tile by tile: the ground
/// image <c>GROUND32.TIL[ terrainImageIndex ]</c> mirrored by the two bit terrain shape first, then
/// the four ground object layers in the order of the reference, the monsters, the boats and the
/// heroes on top of them and finally the top object parts.
/// </para>
/// <para>
/// Fog and the seven other directions of a boat, which only a sailing hero sets, are later milestones.
/// Monsters and boats are not merely "not drawn yet": like heroes their sprites are split over several
/// tiles by the engine, so they need their own renderer instead of the generic object path.
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

    /// <summary>
    /// Where a monster stands on its tile, the <c>monsterImageOffset</c> of the reference: 16 pixels
    /// right and 30 pixels down, added before the offset stored in the frame itself.
    /// </summary>
    public static Point MonsterAnchor { get; } = new(16, 30);

    /// <summary>
    /// Where a boat floats on its tile, the <c>boatSpriteOffset</c> of the reference besides the
    /// horizontal flip: <c>tileWidthPx - 11</c> pixels down and no horizontal offset, added before
    /// the offset stored in the frame itself.
    /// </summary>
    public static Point BoatAnchor { get; } = new(0, 21);

    private readonly Mp2Map _map;
    private readonly TerrainTileSet _tiles;
    private readonly IObjectSpriteSource? _objects;
    private readonly IHeroSpriteSource? _heroes;
    private readonly IMonsterSpriteSource? _monsters;
    private readonly IBoatSpriteSource? _boats;
    private readonly MapFog? _fog;
    private readonly IFogSpriteSource? _fogSprites;
    private readonly World? _world;
    private readonly Size _viewport;

    /// <summary>
    /// The animation phase of the last <see cref="Draw"/> call. Rendering-only state, set at the entry of the
    /// draw pass so the private blitters do not have to carry it as a parameter.
    /// </summary>
    private uint _animationPhase;

    public AdventureMapView(
        Mp2Map map,
        TerrainTileSet tiles,
        Size viewport,
        IObjectSpriteSource? objects = null,
        IHeroSpriteSource? heroes = null,
        IMonsterSpriteSource? monsters = null,
        IBoatSpriteSource? boats = null,
        MapFog? fog = null,
        IFogSpriteSource? fogSprites = null,
        World? world = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tiles);

        _map = map;
        _tiles = tiles;
        _objects = objects;
        _heroes = heroes;
        _monsters = monsters;
        _boats = boats;
        _fog = fog;
        _fogSprites = fogSprites;
        _world = world;
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

    /// <summary>The monster sprites, <see langword="null"/> when the map monsters are left out.</summary>
    public IMonsterSpriteSource? Monsters => _monsters;

    /// <summary>The boat sprites, <see langword="null"/> when the empty boats are left out.</summary>
    public IBoatSpriteSource? Boats => _boats;

    /// <summary>
    /// The fog of war as the observer's colors see it, <see langword="null"/> when the map is drawn
    /// without fog — the editor mode of the reference is fog free as well.
    /// </summary>
    public MapFog? Fog => _fog;

    /// <summary>The fog sprites, <see langword="null"/> when the fog is left out.</summary>
    public IFogSpriteSource? FogSprites => _fogSprites;

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
    /// <param name="target">The image to paint on.</param>
    /// <param name="drawTownsOverFog">
    /// Whether castles are painted on top of the fog they stand in, the <c>LEVEL_TOWNS</c> bit of the reference.
    /// The adventure map leaves it off; only the View Towns and View All spells turn it on
    /// (<c>view_world.cpp:234-236</c>).
    /// </param>
    /// <param name="animationPhase">
    /// The adventure map animation index, <c>Game::getAdventureMapAnimationIndex()</c>. Animated object parts draw
    /// one frame of their cycle on top of the base image, chosen by this value; zero is a normal phase, not "off".
    /// </param>
    /// <param name="passability">
    /// The passability of the tiles, drawn instead of the fog when it is not <see langword="null"/> — the
    /// <c>LEVEL_PASSABILITIES</c> flag of <c>Interface::GameArea::Redraw()</c> (<c>interface_gamearea.cpp:768-776</c>).
    /// The map editor turns it on with the <c>EDITOR_PASSABILITY</c> option.
    /// </param>
    /// <remarks>
    /// <para>
    /// The passes are per layer over the whole viewport, not per tile, which is what the reference
    /// does (<c>Interface::GameArea::Redraw()</c>): a sprite that reaches over a tile border is not
    /// covered by the terrain of the next tile, and an object of one layer is drawn above an object
    /// of an earlier pass even when that object sits on the next tile.
    /// </para>
    /// <para>
    /// 参考在渲染里直接读全局的 <c>Game::getAdventureMapAnimationIndex()</c>；这里改成显式传进来，好让调用方
    /// 决定节拍，也让截图与用例的结果不依赖全局状态。
    /// </para>
    /// </remarks>
    public void Draw(
        PixelBuffer target,
        bool drawTownsOverFog = false,
        uint animationPhase = 0,
        MapTileSet? passability = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        _animationPhase = animationPhase;

        DrawTerrain(target);
        DrawSprites(target);

        if (passability is not null)
        {
            // 通行性叠层与战争迷雾是参考里互斥的两遍（interface_gamearea.cpp:768-793）：打开叠层时雾根本不画。
            DrawPassability(target, passability);

            return;
        }

        // The fog of war is the very last pass of the reference as well: it hides the terrain and
        // everything that stands on it, and only the castles are painted over it afterwards.
        DrawFog(target, drawTownsOverFog);
    }

    /// <summary>
    /// Draws the passability marker of every visible tile, the <c>LEVEL_PASSABILITIES</c> pass of
    /// <c>Interface::GameArea::Redraw()</c> (<c>interface_gamearea.cpp:768-776</c>).
    /// </summary>
    /// <param name="target">The image to paint on.</param>
    /// <param name="tiles">Passability of the tiles, one per tile of the map.</param>
    /// <exception cref="ArgumentException">The tile set belongs to a map of another size.</exception>
    private void DrawPassability(PixelBuffer target, MapTileSet tiles)
    {
        if (tiles.Geometry is not { } geometry || geometry.Width != _map.Width || geometry.Height != _map.Height)
        {
            throw new ArgumentException("The passability tile set does not cover this map.", nameof(tiles));
        }

        foreach ((int column, int row, int left, int top, Mp2TileInfo _) in VisibleTiles())
        {
            Tile tile = tiles.GetTile((row * _map.Width) + column);
            bool isActionObject = PassabilityOverlay.IsActionObject(tile.MainObjectType);

            if (isActionObject || tile.PassableDirections != Directions.All)
            {
                PassabilityOverlay.Draw(target, new Point(left, top), tile.PassableDirections, isActionObject);
            }
        }
    }

    /// <summary>
    /// Draws the object, hero, monster, boat and fog passes of the map, in the order of
    /// <c>Interface::GameArea::Redraw()</c>.
    /// </summary>
    private void DrawSprites(PixelBuffer target)
    {
        if (_objects is null && _heroes is null && _monsters is null && _boats is null)
        {
            return;
        }

        foreach (int layer in LayerPassOrder)
        {
            DrawGroundLayer(target, layer);

            if (layer == ShadowLayer)
            {
                // Shadows of the tile-unfit objects, then whatever a sprite draws below its own tile.
                DrawHeroShadows(target);
                DrawBoatShadows(target);
                DrawMonsters(target, TileBand.BelowTile);
                DrawBoats(target, TileBand.BelowTile);
                DrawHeroes(target, TileBand.BelowTile);
            }

            // A monster stands on its tile, a boat floats on it and a hero's body and flag stand in
            // front of both: the "middle" part of the reference, drawn after OBJECT_LAYER and before
            // the top layer.
            if (layer == ObjectLayer)
            {
                DrawMonsters(target, TileBand.OnTile);
                DrawBoats(target, TileBand.OnTile);
                DrawHeroes(target, TileBand.OnTile);
            }
        }

        DrawTopLayer(target);

        // The rows of a sprite that reach over the tile above are drawn last of all, so a castle tower
        // or the flag of a tall object never covers the hero's head.
        DrawMonsters(target, TileBand.AboveTile);
        DrawBoats(target, TileBand.AboveTile);
        DrawHeroes(target, TileBand.AboveTile);
    }

    /// <summary>Which row range of a sprite that is split over tiles is currently being drawn.</summary>
    private enum TileBand
    {
        /// <summary>Rows above the tile, the <c>topImages</c> of the reference.</summary>
        AboveTile,

        /// <summary>Rows inside the tile, the <c>bottomImages</c> of the reference.</summary>
        OnTile,

        /// <summary>Rows below the tile, the <c>heroBackgroundImages</c> of the reference.</summary>
        BelowTile,
    }

    /// <summary>
    /// Draws the part of every hero that falls into <paramref name="band"/>, the port of the
    /// <c>tileUnfit.*Images</c> passes of <c>Interface::GameArea::Redraw()</c>. The body comes from the
    /// tile set of the hero's race and the flag from the tile set of its kingdom, both anchored on the
    /// tile the hero occupies.
    /// </summary>
    private void DrawHeroes(PixelBuffer target, TileBand band)
    {
        if (_heroes is null)
        {
            return;
        }

        if (_world is { Heroes.Count: > 0 })
        {
            DrawWorldHeroes(target, band);

            return;
        }

        foreach (Mp2MapHero hero in VisibleHeroes())
        {
            // A hero under the fog is not drawn at all: the body of MINIHERO reaches over the tile
            // above, which the fog of its own tile would not cover (isTileUnderFog in the reference).
            if (IsUnderFog(hero.X, hero.Y))
            {
                continue;
            }

            int left = (hero.X * _tiles.TileWidth) - Scroll.X;
            int top = (hero.Y * _tiles.TileHeight) - Scroll.Y;

            BlitTileSprite(target, _heroes.GetBody(hero.Race), left, top, Point.Zero, band);
            BlitTileSprite(target, _heroes.GetFlag(hero.Color), left, top, Point.Zero, band);
        }
    }

    /// <summary>
    /// 画 <see cref="World"/> 里的英雄，也就是开局之后真正在走的那一批。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 地图里那一批是 <c>Mp2MapHero</c>（存档里的静态记录），<see cref="World"/> 里这一批是
    /// <see cref="Hero"/>（会走、会死、会换阵营）。两者在开局时一一对应，所以只要世界里已经有英雄就只画
    /// 世界这一批，否则会画成两个人。
    /// </para>
    /// <para>
    /// 帧与方向：<c>Heroes::GetHeroSpriteIndex()</c> 给出 <c>direction * 9 + frame</c>，端口的
    /// <see cref="Hero.SpriteIndex"/> 已经是这个值（站立时是 <c>RIGHT</c> 组的第 0 帧，18），走的动画由
    /// <see cref="Hero.MoveStep"/> 推进。偏移 <see cref="Hero.Offset"/> 是走到两格之间时的那半格。
    /// </para>
    /// <para>
    /// 雾下的英雄不画：参考的 <c>isTileUnderFog()</c> 也一样，理由同地图英雄——身体会伸到上一格，
    /// 那一格的雾盖不住它。
    /// </para>
    /// </remarks>
    private void DrawWorldHeroes(PixelBuffer target, TileBand band)
    {
        IHeroSpriteSource heroes = _heroes!;
        World world = _world!;

        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;
        int width = _map.Width;

        foreach (Hero hero in world.Heroes)
        {
            if (hero.MapIndex < 0)
            {
                continue;
            }

            int column = hero.MapIndex % width;
            int row = hero.MapIndex / width;

            if (IsUnderFog(column, row))
            {
                continue;
            }

            int left = (column * tileWidth) - Scroll.X + hero.Offset.X;
            int top = (row * tileHeight) - Scroll.Y + hero.Offset.Y;

            BlitTileSprite(target, heroes.GetBody(hero.Race, hero.SpriteIndex), left, top, Point.Zero, band);
            BlitTileSprite(target, heroes.GetFlag(hero.Color), left, top, Point.Zero, band);
        }
    }

    /// <summary>
    /// Draws the part of every map monster that falls into <paramref name="band"/>, the port of
    /// <c>drawMonsterAnimation( tile, false )</c> for a monster that is not being attacked.
    /// </summary>
    /// <remarks>
    /// The reference splits <c>MINIMON</c> into two images before drawing: the body (<c>transform</c>
    /// clamped to 1, the shadow pixels left out) and the shadow (<c>transform</c> above 1), because the
    /// shadow belongs under the objects of the tiles it covers. This port blits the frame of
    /// <c>MINIMON</c> as it is, so the baked shadow of a monster travels with its body: the pixels are
    /// the same as long as nothing else is drawn over the same tiles.
    /// </remarks>
    private void DrawMonsters(PixelBuffer target, TileBand band)
    {
        if (_monsters is null)
        {
            return;
        }

        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;

        foreach ((int column, int row) in VisibleSpriteTiles(1))
        {
            if (IsUnderFog(column, row))
            {
                continue;
            }

            int monsterId = Mp2ObjectData.MonsterOf(_map.Tile(column, row));
            if (monsterId <= 0)
            {
                continue;
            }

            Sprite? sprite = _monsters.GetMonster(monsterId);
            if (sprite is null)
            {
                continue;
            }

            int left = (column * tileWidth) - Scroll.X;
            int top = (row * tileHeight) - Scroll.Y;

            BlitTileSprite(target, sprite, left, top, MonsterAnchor, band);
        }
    }

    /// <summary>
    /// Draws the part of every empty boat that falls into <paramref name="band"/>, the port of
    /// <c>getBoatSpritesPerTile</c>. The hull comes from <c>BOAT32</c> instead of the tile, because
    /// <c>Maps::setInitialObjectInfo</c> replaces the editor frame of a boat with it.
    /// </summary>
    private void DrawBoats(PixelBuffer target, TileBand band)
    {
        if (_boats is null)
        {
            return;
        }

        Sprite? sprite = _boats.GetBoat();
        if (sprite is null)
        {
            return;
        }

        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;

        foreach ((int column, int row) in VisibleSpriteTiles(2))
        {
            if (!Mp2ObjectData.IsBoat(_map.Tile(column, row)) || IsUnderFogWithUpperTile(column, row))
            {
                continue;
            }

            int left = (column * tileWidth) - Scroll.X;
            int top = (row * tileHeight) - Scroll.Y;

            BlitTileSprite(target, sprite, left, top, BoatAnchor, band);
        }
    }

    /// <summary>
    /// Draws the shadows of the empty boats, the port of <c>tileUnfit.shadowImages</c> filled by
    /// <c>getBoatShadowSpritesPerTile</c>. A boat shadow lies on the water of the tile itself and the
    /// one below, so it is drawn with the shadow layer as one piece.
    /// </summary>
    private void DrawBoatShadows(PixelBuffer target)
    {
        if (_boats is null)
        {
            return;
        }

        Sprite? shadow = _boats.GetShadow();
        if (shadow is null)
        {
            return;
        }

        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;

        foreach ((int column, int row) in VisibleSpriteTiles(2))
        {
            if (!Mp2ObjectData.IsBoat(_map.Tile(column, row)) || IsUnderFogWithUpperTile(column, row))
            {
                continue;
            }

            int left = (column * tileWidth) - Scroll.X + BoatAnchor.X + shadow.OffsetX;
            int top = (row * tileHeight) - Scroll.Y + BoatAnchor.Y + shadow.OffsetY;

            ImageOps.BlitSprite(target, new Point(left, top), shadow);
        }
    }

    /// <summary>
    /// The tiles whose sprite can reach the viewport, the port of the <c>ScanAroundObject</c> range of
    /// the renderer: a monster is anchored 16 pixels right and 30 pixels down from the corner of its
    /// tile and a boat <c>tileWidthPx - 11</c> pixels down, so one row lower and one column either
    /// side can still show a pixel — a boat is tall enough to need two.
    /// </summary>
    /// <param name="margin">Number of tiles the test is grown by on every side.</param>
    private IEnumerable<(int Column, int Row)> VisibleSpriteTiles(int margin)
    {
        Rect area = VisibleTileArea;

        int firstColumn = Math.Max(area.X - margin, 0);
        int firstRow = Math.Max(area.Y - margin, 0);
        int lastColumn = Math.Min(area.Right + margin - 1, _map.Width - 1);
        int lastRow = Math.Min(area.Bottom + margin - 1, _map.Height - 1);

        for (int row = firstRow; row <= lastRow; ++row)
        {
            for (int column = firstColumn; column <= lastColumn; ++column)
            {
                yield return (column, row);
            }
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

        if (_world is { Heroes.Count: > 0 })
        {
            DrawWorldHeroShadows(target, shadow);

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
    /// 画世界里那批英雄的影子，与 <see cref="DrawWorldHeroes"/> 配套。
    /// </summary>
    /// <remarks>
    /// 影子跟着 <see cref="Hero.Offset"/> 走：参考里影子是 <c>tileUnfit.shadowImages</c> 的一层，
    /// 与身体用同一个偏移，否则走到两格中间时影子会落在原地。
    /// </remarks>
    private void DrawWorldHeroShadows(PixelBuffer target, Sprite shadow)
    {
        World world = _world!;
        int tileWidth = _tiles.TileWidth;
        int tileHeight = _tiles.TileHeight;
        int width = _map.Width;

        foreach (Hero hero in world.Heroes)
        {
            if (hero.MapIndex < 0)
            {
                continue;
            }

            int column = hero.MapIndex % width;
            int row = hero.MapIndex / width;

            int left = (column * tileWidth) - Scroll.X + hero.Offset.X + shadow.OffsetX;
            int top = (row * tileHeight) - Scroll.Y + hero.Offset.Y + shadow.OffsetY;

            ImageOps.BlitSprite(target, new Point(left, top), shadow);
        }
    }

    /// <summary>
    /// Draws the rows of <paramref name="sprite"/> that belong to <paramref name="band"/>. The split
    /// is the one of <c>DivideImageBySquares</c>: every row band of the sprite belongs to the tile its
    /// top edge falls into, and the three groups are painted at three different moments.
    /// <paramref name="anchor"/> is the offset of the sprite from the corner of its tile before the
    /// anchor stored in the sprite itself, <c>( 16, 30 )</c> for a monster and nothing for a hero.
    /// </summary>
    private void BlitTileSprite(PixelBuffer target, Sprite? sprite, int left, int top, Point anchor, TileBand band)
    {
        if (sprite is null)
        {
            return;
        }

        int spriteLeft = left + anchor.X + sprite.OffsetX;
        int spriteTop = top + anchor.Y + sprite.OffsetY;
        int tileTop = top;
        int tileBottom = top + _tiles.TileHeight;

        int firstRow = band switch
        {
            TileBand.AboveTile => spriteTop,
            TileBand.OnTile => Math.Max(spriteTop, tileTop),
            _ => Math.Max(spriteTop, tileBottom),
        };

        int lastRow = band switch
        {
            TileBand.AboveTile => Math.Min(spriteTop + sprite.Height, tileTop),
            TileBand.OnTile => Math.Min(spriteTop + sprite.Height, tileBottom),
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

            // The ground part of a boat is the frame the original editor shows, not the boat: the
            // reference throws it away and draws the hull out of BOAT32 instead (DrawBoats). A view
            // that has no boat sprites — the map editor of this port — keeps the editor frame.
            if (tile.GroundLayer == layer && !(_boats is not null && Mp2ObjectData.IsBoat(tile)))
            {
                BlitMainObject(target, tile, tile.GroundIcnType, tile.BottomIcnImageIndex, left, top);
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
                BlitMainObject(target, tile, tile.TopIcnType, tile.TopIcnImageIndex, left, top);
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
    /// Blits one object part with its animation frame, the port of <c>renderObjectPart()</c>
    /// (<c>maps_tiles_render.cpp:60-80</c>).
    /// </summary>
    /// <param name="target">The image to paint on.</param>
    /// <param name="icnType">Tile set of the part.</param>
    /// <param name="icnIndex">Frame of the part.</param>
    /// <param name="left">Left pixel of the tile.</param>
    /// <param name="top">Top pixel of the tile.</param>
    /// <remarks>
    /// 跳过的那些图集（<c>MINIHERO</c>、<c>MONS32</c> 等）走各自的渲染器，通过 <see cref="IHeroSpriteSource"/> 一类
    /// 的接口取图，不走这条路径。
    /// </remarks>
    private void BlitPart(PixelBuffer target, Mp2ObjectIcnType icnType, int icnIndex, int left, int top)
    {
        BlitAnimatedPart(target, icnType, icnIndex, left, top, isMainObject: false, metadata1: 0);
    }

    /// <summary>
    /// Blits the main object standing on a tile, the port of <c>renderMainObject()</c>
    /// (<c>maps_tiles_render.cpp:142-190</c>): the same two blits as a part plus one quirk of the original game.
    /// </summary>
    /// <param name="target">The image to paint on.</param>
    /// <param name="tile">The tile whose main object is drawn.</param>
    /// <param name="icnType">Tile set of the main part.</param>
    /// <param name="icnIndex">Frame of the main part.</param>
    /// <param name="left">Left pixel of the tile.</param>
    /// <param name="top">Top pixel of the tile.</param>
    private void BlitMainObject(PixelBuffer target, Mp2TileInfo tile, Mp2ObjectIcnType icnType, int icnIndex, int left, int top)
    {
        BlitAnimatedPart(target, icnType, icnIndex, left, top, isMainObject: true, metadata1: tile.Quantity2);
    }

    /// <summary>
    /// Blits an object part and, when the catalogue marks it animated, the one extra frame its cycle shows at
    /// <see cref="_animationPhase"/> (<c>maps_tiles_render.cpp:70-80</c>).
    /// </summary>
    /// <param name="target">The image to paint on.</param>
    /// <param name="icnType">Tile set of the part.</param>
    /// <param name="icnIndex">Base frame of the part.</param>
    /// <param name="left">Left pixel of the tile.</param>
    /// <param name="top">Top pixel of the tile.</param>
    /// <param name="isMainObject">Whether this is the main object of its tile.</param>
    /// <param name="metadata1">The second metadata byte of the tile, <c>quantity2</c>.</param>
    /// <remarks>
    /// 魔法花园（<c>maps_tiles_render.cpp:166-171</c>）被访问过（<c>metadata()[1] == 0</c>）时不是播动画，而是取整轮
    /// 之后那一帧。参考只对主物件做这个判断，addon 链上的部件照常走 <see cref="BlitPart"/>。
    /// </remarks>
    private void BlitAnimatedPart(
        PixelBuffer target,
        Mp2ObjectIcnType icnType,
        int icnIndex,
        int left,
        int top,
        bool isMainObject,
        byte metadata1)
    {
        if (!Mp2ObjectIcnTypes.IsDrawnAsObjectPart(icnType) || icnIndex is < 0 or Mp2TileInfo.EmptyImageIndex)
        {
            return;
        }

        BlitSprite(target, icnType, icnIndex, left, top);

        if (MapObjectCatalog.GetObjectPartByIcn(icnType, (uint)icnIndex) is not { AnimationFrames: > 0 } part)
        {
            return;
        }

        int frame = icnIndex + (int)(_animationPhase % part.AnimationFrames) + 1;

        if (isMainObject && metadata1 == 0
            && part.ObjectType is Mp2ObjectType.MagicGarden or Mp2ObjectType.ActionMagicGarden)
        {
            frame = icnIndex + part.AnimationFrames + 1;
        }

        BlitSprite(target, icnType, frame, left, top);
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

    /// <summary>
    /// Whether every observer color still has the tile undiscovered, which is what
    /// <c>Maps::Tile::isFog( colors )</c> means and the only case the fog hides.
    /// </summary>
    private bool IsUnderFog(int column, int row) =>
        _fog is not null
        && column >= 0 && column < _map.Width
        && row >= 0 && row < _map.Height
        && _fog.GetDirection(column, row) == Directions.All;

    /// <summary>
    /// Whether the tile and the tile above it are under the fog. A boat and a sailing hero reach over
    /// the tile above, so the reference hides them only when that row is fogged as well
    /// (<c>isTileUnderFog &amp;&amp; isUpperTileUnderFog</c>).
    /// </summary>
    private bool IsUnderFogWithUpperTile(int column, int row) =>
        IsUnderFog(column, row) && (row == 0 || IsUnderFog(column, row - 1));

    /// <summary>
    /// Draws the fog of war over the visible tiles, the port of <c>Maps::drawFog</c> and of the pass
    /// that re-draws the castles after it.
    /// </summary>
    /// <param name="target">The image to paint on.</param>
    /// <param name="drawTownsOverFog">
    /// Whether the castles of fogged tiles are painted on top of the fog as well, the <c>LEVEL_TOWNS</c>
    /// bit of the reference.
    /// </param>
    /// <remarks>
    /// <para>
    /// A tile is either covered by one of the four <c>CLOF32</c> whole tiles or receives one
    /// <c>CLOP32</c> piece, the boundary to the part of the tile that is already discovered.
    /// </para>
    /// <para>
    /// <b>城镇默认不透过迷雾画</b>：参考只在 <c>LEVEL_TOWNS</c> 打开时才把 <c>OBJNTWBA</c> 与
    /// <c>OBJNTOWN</c> 补在迷雾上，而那个位只有「看全图/看城镇」两个法术会用
    /// （<c>interface_gamearea.h:57</c>）；冒险地图上没侦察过的城镇本来就该被迷雾盖住。
    /// </para>
    /// </remarks>
    private void DrawFog(PixelBuffer target, bool drawTownsOverFog)
    {
        if (_fog is null || _fogSprites is null)
        {
            return;
        }

        foreach ((int column, int row, int left, int top, Mp2TileInfo tile) in VisibleTiles())
        {
            Direction direction = _fog.GetDirection(column, row);
            if (direction == Direction.Unknown)
            {
                continue;
            }

            DrawFogTile(target, direction, column, row, left, top);

            if (!drawTownsOverFog)
            {
                continue;
            }

            DrawObjectParts(target, tile, Mp2ObjectIcnType.ObjnTwba, left, top);
            if (IsCastle(tile.ObjectType))
            {
                DrawObjectParts(target, tile, Mp2ObjectIcnType.ObjnTown, left, top);
            }
        }
    }

    /// <summary>Draws the fog piece of one tile, the sprite choice of <c>Maps::drawFog</c>.</summary>
    private void DrawFogTile(PixelBuffer target, Direction direction, int column, int row, int left, int top)
    {
        FogFrame frame = FogFrames.Resolve(direction, column, row, _map.Width);

        if (frame.IsFullTile)
        {
            PixelBuffer? image = _fogSprites!.GetFullTile(frame.Index);
            if (image is not null)
            {
                ImageOps.Blit(target, new Point(left, top), image, image.Bounds);
            }

            return;
        }

        Sprite? sprite = _fogSprites!.GetBoundary(frame.Index, frame.Flipped);
        if (sprite is null)
        {
            return;
        }

        // BlitOnTile( ..., revert ) mirrors the sprite inside the tile: the x offset is measured from
        // the right edge and the pixels are flipped.
        int x = frame.Flipped ? _tiles.TileWidth - sprite.OffsetX - sprite.Width : sprite.OffsetX;
        ImageOps.BlitSprite(target, new Point(left + x, top + sprite.OffsetY), sprite);
    }

    /// <summary>
    /// Draws the ground parts of one tile set type of a tile, the <c>drawByObjectIcnType</c> the
    /// reference calls for the castles it paints over the fog.
    /// </summary>
    private void DrawObjectParts(PixelBuffer target, Mp2TileInfo tile, Mp2ObjectIcnType icnType, int left, int top)
    {
        foreach (int index in _map.EnumerateAddonChain(tile))
        {
            Mp2AddonInfo addon = _map.Addons[index];
            if (addon.GroundIcnType == icnType)
            {
                BlitPart(target, addon.GroundIcnType, addon.BottomIcnImageIndex, left, top);
            }
        }

        if (tile.GroundIcnType == icnType)
        {
            BlitPart(target, tile.GroundIcnType, tile.BottomIcnImageIndex, left, top);
        }
    }

    /// <summary>Whether a tile holds a town, the <c>MP2::OBJ_CASTLE</c> family of the reference.</summary>
    private static bool IsCastle(Mp2ObjectType type) =>
        type is Mp2ObjectType.Castle or Mp2ObjectType.ActionCastle
            or Mp2ObjectType.RandomCastle or Mp2ObjectType.ActionRandomCastle;
}
