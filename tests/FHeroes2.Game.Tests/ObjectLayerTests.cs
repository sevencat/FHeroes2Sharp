using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the object layer of <see cref="AdventureMapView"/>: which parts are blitted, in which
/// order and where. The springs come from a dictionary, so the expectations stay readable and no
/// game data is needed.
/// </summary>
public sealed class ObjectLayerTests
{
    private const int TileSize = 2;

    [Fact]
    public void GroundPart_IsDrawnOverTheTerrain()
    {
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                GroundImageIndex = 0,
                GroundLayer = 0,
            }));

        PixelBuffer target = Draw(view);

        // The terrain of tile 0 is 1 everywhere; the 2x2 object covers it with 9.
        Assert.Equal(9, target.Row(0)[0]);
        Assert.Equal(9, target.Row(0)[1]);
        Assert.Equal(9, target.Row(1)[0]);
        Assert.Equal(9, target.Row(1)[1]);
        Assert.Equal(2, target.Row(0)[2]);
    }

    [Fact]
    public void TopPart_IsDrawnOverTheGroundPart()
    {
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                GroundImageIndex = 0,
                GroundLayer = 0,
                TopIcnType = Mp2ObjectIcnType.ObjnSnow,
                TopImageIndex = 1,
            }));

        PixelBuffer target = Draw(view);

        Assert.Equal(10, target.Row(0)[0]);
    }

    [Fact]
    public void GroundLayers_AreDrawnInTheReferenceOrder()
    {
        // The reference paints TERRAIN_LAYER (3), BACKGROUND_LAYER (1), SHADOW_LAYER (2) and only then
        // OBJECT_LAYER (0), so ascending order would put the roads of a tile over its own object.
        Assert.Equal([3, 1, 2, 0], AdventureMapView.LayerPassOrder);
    }

    [Fact]
    public void ObjectLayer_IsDrawnOverTheTerrainLayer()
    {
        // The same tile holds a road (layer 3) and an object (layer 0) through its add-on chain; the
        // object wins because the road was painted in the earlier pass.
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                GroundImageIndex = 0,
                GroundLayer = 3,
                NextAddonIndex = 1,
            }),
            addons:
            [
                new Mp2AddonInfo
                {
                    NextAddonIndex = 0,
                    ObjectNameN1 = ObjectName(Mp2ObjectIcnType.ObjnSnow),
                    BottomIcnImageIndex = 1,
                    QuantityN = 0,
                    ObjectNameN2 = 0,
                    TopIcnImageIndex = 0,
                    Level1ObjectUid = 1,
                    Level2ObjectUid = 0,
                },
            ]);

        PixelBuffer target = Draw(view);

        Assert.Equal(10, target.Row(0)[0]);
    }

    [Fact]
    public void SpriteAnchor_IsAddedToTheTilePosition()
    {
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                GroundImageIndex = 2,
                GroundLayer = 0,
            }),
            anchor: new Point(1, 0));

        PixelBuffer target = Draw(view);

        // Frame 2 is a single pixel at anchor (1; 0), so it lands on the second column.
        Assert.Equal(2, target.Row(0)[0]);
        Assert.Equal(11, target.Row(0)[1]);
    }

    [Fact]
    public void MissingSprite_LeavesTheTerrainVisible()
    {
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                // The source has no frame 200.
                GroundImageIndex = 200,
                GroundLayer = 0,
            }));

        PixelBuffer target = Draw(view);

        Assert.Equal(2, target.Row(0)[0]);
    }

    [Fact]
    public void EmptyImageIndex_IsNotDrawn()
    {
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                GroundImageIndex = Mp2TileInfo.EmptyImageIndex,
                GroundLayer = 0,
            }));

        PixelBuffer target = Draw(view);

        Assert.Equal(2, target.Row(0)[0]);
    }

    [Fact]
    public void AddonTopPart_IsDrawnAfterTheTileTopPart()
    {
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                TopIcnType = Mp2ObjectIcnType.ObjnGras,
                TopImageIndex = 0,
                NextAddonIndex = 1,
            }),
            addons:
            [
                new Mp2AddonInfo
                {
                    NextAddonIndex = 0,
                    ObjectNameN1 = 0,
                    BottomIcnImageIndex = 0,
                    QuantityN = 0,
                    ObjectNameN2 = ObjectName(Mp2ObjectIcnType.ObjnSnow),
                    TopIcnImageIndex = 1,
                    Level1ObjectUid = 0,
                    Level2ObjectUid = 1,
                },
            ]);

        PixelBuffer target = Draw(view);

        Assert.Equal(10, target.Row(0)[0]);
    }

    [Fact]
    public void LayerPasses_CoverTheTerrainOfTheNextTile()
    {
        // The object of tile 0 is 4x4 pixels, so it reaches over the tile grid. The reference draws
        // a whole layer before the next one, which is why the sprite is not clipped by the terrain
        // of the neighbouring tiles.
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                GroundImageIndex = 5,
                GroundLayer = 3,
            }));

        PixelBuffer target = Draw(view);

        Assert.Equal(9, target.Row(0)[0]);
        Assert.Equal(9, target.Row(0)[3]);
        Assert.Equal(9, target.Row(3)[3]);
    }

    [Fact]
    public void WithoutObjects_OnlyTheTerrainIsDrawn()
    {
        AdventureMapView view = CreateView(
            index => Tile(index, new TileFields
            {
                TerrainImageIndex = 1,
                GroundIcnType = Mp2ObjectIcnType.ObjnGras,
                GroundImageIndex = 0,
                GroundLayer = 0,
            }),
            objects: false);

        PixelBuffer target = Draw(view);

        Assert.Equal(2, target.Row(0)[0]);
    }

    private static PixelBuffer Draw(AdventureMapView view)
    {
        PixelBuffer target = new(4, 4);
        ImageOps.FillRect(target, new Rect(0, 0, 4, 4), 200);
        view.Draw(target);
        return target;
    }

    private static AdventureMapView CreateView(
        Func<int, Mp2TileInfo> tileFactory,
        Mp2AddonInfo[]? addons = null,
        Point anchor = default,
        bool objects = true)
    {
        // Only the first tile carries an object, so the expectations can talk about one tile.
        Mp2Map map = SyntheticMap.Create(
            2,
            2,
            index => index == 0 ? tileFactory(index) : Tile(index, new TileFields { TerrainImageIndex = 1 }),
            addons);

        TerrainTileSet tiles = SyntheticTerrain.Create(4, TileSize);

        IObjectSpriteSource? source = objects
            ? new DictionarySpriteSource(anchor)
            : null;

        return new AdventureMapView(map, tiles, new Size(4, 4), source);
    }

    private static byte ObjectName(Mp2ObjectIcnType type) => (byte)((int)type << 2);

    /// <summary>Builds a tile with the given explicit fields.</summary>
    private static Mp2TileInfo Tile(int index, TileFields fields) =>
        new()
        {
            TerrainImageIndex = fields.TerrainImageIndex,

            // The ICN type lives in the top two bits of the name bytes; the sprite index is a
            // separate byte, which is why the tests set both.
            ObjectName1 = ObjectName(fields.GroundIcnType),
            BottomIcnImageIndex = fields.GroundImageIndex,
            Quantity1 = (byte)fields.GroundLayer,
            Quantity2 = 0,
            ObjectName2 = ObjectName(fields.TopIcnType),
            TopIcnImageIndex = fields.TopImageIndex,
            TerrainFlags = 0,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = fields.NextAddonIndex,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

    /// <summary>Fields of <see cref="Tile"/> with defaults that mean "nothing".</summary>
    private readonly record struct TileFields
    {
        public ushort TerrainImageIndex { get; init; }

        public Mp2ObjectIcnType GroundIcnType { get; init; }

        public byte GroundImageIndex { get; init; }

        public int GroundLayer { get; init; }

        public Mp2ObjectIcnType TopIcnType { get; init; }

        public byte TopImageIndex { get; init; }

        public ushort NextAddonIndex { get; init; }
    }

    /// <summary>
    /// Sprites of (<c>ObjnGras</c>, 0) = flat 9, (<c>ObjnSnow</c>, 1) = flat 10 and
    /// (<c>ObjnGras</c>, 2) = one pixel of 11 at <c>anchor</c>.
    /// </summary>
    private sealed class DictionarySpriteSource : IObjectSpriteSource
    {
        private readonly Dictionary<(Mp2ObjectIcnType, int), Sprite> _sprites = [];

        public DictionarySpriteSource(Point anchor)
        {
            _sprites[(Mp2ObjectIcnType.ObjnGras, 0)] = Flat(9);
            _sprites[(Mp2ObjectIcnType.ObjnSnow, 1)] = Flat(10);
            _sprites[(Mp2ObjectIcnType.ObjnSnow, 0)] = Flat(12);
            _sprites[(Mp2ObjectIcnType.ObjnGras, 5)] = Flat(9, size: 4);

            Sprite dot = new(1, 1, anchor.X, anchor.Y);
            dot.Image.Fill(11);
            _sprites[(Mp2ObjectIcnType.ObjnGras, 2)] = dot;
        }

        public Sprite? GetSprite(Mp2ObjectIcnType type, int index) =>
            _sprites.TryGetValue((type, index), out Sprite? sprite) ? sprite : null;

        private static Sprite Flat(byte value, int size = TileSize)
        {
            Sprite sprite = new(size, size);
            sprite.Image.Fill(value);
            return sprite;
        }
    }
}
