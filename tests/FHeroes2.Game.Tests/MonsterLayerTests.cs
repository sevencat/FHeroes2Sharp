using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the monster layer of <see cref="AdventureMapView"/>: which rows of a monster are painted at
/// which moment of the frame, and which frame of <c>MINIMON</c> a tile asks for.
/// </summary>
/// <remarks>
/// The frame is 8x8 pixels with 2x2 tiles and the monster wanders over tile (1; 2), so its tile covers
/// rows 4 and 5. The anchor of a monster is <c>( 16, 30 )</c> in tile pixels, far outside a 2 pixel
/// tile, so the fake frames carry an offset that cancels it: a mock sprite at <c>( -16, -30 )</c> lands
/// on the corner of its tile, exactly where a real frame anchored at the anchor would be.
/// </remarks>
public sealed class MonsterLayerTests
{
    private const int TileSize = 2;

    /// <summary>Column the monster stands in, i.e. the first column of its sprite.</summary>
    private const int MonsterColumn = 2;

    /// <summary>Tile index of the monster, row major on the 4x4 synthetic map.</summary>
    private const int MonsterTileIndex = (2 * 4) + 1;

    /// <summary>Tile index of the tile below the monster, where its lower rows land.</summary>
    private const int TileBelowMonster = MonsterTileIndex + 4;

    [Fact]
    public void MonsterIsDrawnOnItsTile()
    {
        PixelBuffer target = Draw(CreateView());

        // The frame has one row per tile row it covers: 60 and 61 on the tile itself.
        Assert.Equal(60, target.Row(4)[MonsterColumn]);
        Assert.Equal(61, target.Row(5)[MonsterColumn]);
    }

    [Fact]
    public void MonsterRowsBelowItsTile_AreDrawnBeforeTheObjectLayer()
    {
        // The rows that fall into the tile below belong to the "below" part of the split: an object on
        // that tile covers them.
        AdventureMapView view = CreateView(index => index == TileBelowMonster
            ? GroundObject()
            : MonsterTile(index));

        PixelBuffer target = Draw(view);

        Assert.Equal(9, target.Row(6)[MonsterColumn]);
        Assert.Equal(9, target.Row(7)[MonsterColumn]);

        // The rows on the monster's own tile are painted later and stay.
        Assert.Equal(60, target.Row(4)[MonsterColumn]);
    }

    [Fact]
    public void MonsterRowsAboveItsTile_AreDrawnAfterTheTopLayer()
    {
        // A frame whose anchor reaches one row up covers part of the tile above; those rows are the
        // "above" part and are painted after the top layer, so a top object cannot hide them.
        AdventureMapView view = CreateView(
            index => index == MonsterTileIndex - 4
                ? TopObject()
                : MonsterTile(index),
            sprites: new TestMonsterSprites { ReachUp = true });

        PixelBuffer target = Draw(view);

        Assert.Equal(60, target.Row(3)[MonsterColumn]);

        // The rest of that tile is the top object, which was painted first.
        Assert.Equal(12, target.Row(2)[MonsterColumn]);
    }

    [Fact]
    public void MonsterIsDrawnUnderTheTopObjectOfItsTile()
    {
        // A monster belongs to the objects of its tile and is painted before the top layer, so a top
        // object on the same tile stands in front of it.
        AdventureMapView view = CreateView(
            index => index == MonsterTileIndex ? MonsterWithTopObject() : MonsterTile(index));

        PixelBuffer target = Draw(view);

        Assert.Equal(12, target.Row(4)[MonsterColumn]);
        Assert.Equal(12, target.Row(5)[MonsterColumn]);
    }

    [Fact]
    public void MonsterSprites_AreLookedUpByTheMonsterOfTheTile()
    {
        // Mons32 image 6 means creature 7: MINIMON frame ( 7 - 1 ) * 9 = 54.
        TestMonsterSprites sprites = new();
        Draw(CreateView(sprites: sprites));

        Assert.Equal(7, sprites.LastMonsterId);
    }

    [Fact]
    public void WithoutMonsterSprites_TheTileIsLeftAsItIs()
    {
        PixelBuffer target = Draw(CreateView(withMonsters: false));

        Assert.Equal(2, target.Row(4)[MonsterColumn]);
        Assert.Equal(2, target.Row(5)[MonsterColumn]);
    }

    [Fact]
    public void TheAnchorIsTheMonsterImageOffsetOfTheReference()
    {
        Assert.Equal(new Point(16, 30), AdventureMapView.MonsterAnchor);
    }

    private static PixelBuffer Draw(AdventureMapView view)
    {
        PixelBuffer target = new(8, 8);
        ImageOps.FillRect(target, new Rect(0, 0, 8, 8), 200);
        view.Draw(target);
        return target;
    }

    private static AdventureMapView CreateView(
        Func<int, Mp2TileInfo>? tileFactory = null,
        bool withMonsters = true,
        TestMonsterSprites? sprites = null)
    {
        Mp2Map map = SyntheticMap.Create(4, 4, tileFactory ?? MonsterTile);
        TerrainTileSet tiles = SyntheticTerrain.Create(4, TileSize);

        IMonsterSpriteSource? monsters = withMonsters ? sprites ?? new TestMonsterSprites() : null;
        return new AdventureMapView(map, tiles, new Size(8, 8), new TestObjectSprites(), null, monsters);
    }

    /// <summary>A terrain tile, or the monster tile when <paramref name="index"/> is the monster's.</summary>
    private static Mp2TileInfo MonsterTile(int index) =>
        index == MonsterTileIndex
            ? BlankTile() with
            {
                ObjectName1 = (byte)((int)Mp2ObjectIcnType.Mons32 << 2),
                BottomIcnImageIndex = 6,
                ObjectType = Mp2ObjectType.ActionMonster,
            }
            : BlankTile();

    /// <summary>The monster tile with a snow object on top of it.</summary>
    private static Mp2TileInfo MonsterWithTopObject() =>
        MonsterTile(MonsterTileIndex) with
        {
            ObjectName2 = (byte)((int)Mp2ObjectIcnType.ObjnSnow << 2),
            TopIcnImageIndex = 0,
        };

    /// <summary>A plain tile with a ground object of <see cref="Mp2ObjectIcnType.ObjnGras"/>.</summary>
    private static Mp2TileInfo GroundObject() =>
        BlankTile() with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnGras << 2),
            BottomIcnImageIndex = 0,
        };

    /// <summary>A plain tile with a top object of <see cref="Mp2ObjectIcnType.ObjnSnow"/>.</summary>
    private static Mp2TileInfo TopObject() =>
        BlankTile() with
        {
            ObjectName2 = (byte)((int)Mp2ObjectIcnType.ObjnSnow << 2),
            TopIcnImageIndex = 0,
        };

    private static Mp2TileInfo BlankTile() =>
        new()
        {
            TerrainImageIndex = 1,
            ObjectName1 = 0,
            BottomIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            Quantity1 = 0,
            Quantity2 = 0,
            ObjectName2 = 0,
            TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
            TerrainFlags = 0,
            ObjectType = Mp2ObjectType.None,
            NextAddonIndex = 0,
            Level1ObjectUid = 0,
            Level2ObjectUid = 0,
        };

    /// <summary>Ground objects are 9, top objects are 12, both flat over their whole tile.</summary>
    private sealed class TestObjectSprites : IObjectSpriteSource
    {
        public Sprite? GetSprite(Mp2ObjectIcnType type, int index)
        {
            Sprite sprite = new(TileSize, TileSize);
            sprite.Image.Fill(type == Mp2ObjectIcnType.ObjnSnow ? (byte)12 : (byte)9);
            return sprite;
        }
    }

    /// <summary>
    /// A 1x2 frame of 60 and 61 anchored on the corner of the tile, or one row higher with
    /// <see cref="ReachUp"/>.
    /// </summary>
    private sealed class TestMonsterSprites : IMonsterSpriteSource
    {
        /// <summary>Moves the frame one row up, so its upper row belongs to the tile above.</summary>
        public bool ReachUp { get; init; }

        public int? LastMonsterId { get; private set; }

        public Sprite? GetMonster(int monsterId)
        {
            LastMonsterId = monsterId;

            // The anchor of a monster is ( 16, 30 ): cancelling it puts the frame on the tile corner,
            // and one pixel less lifts it over the row above.
            Sprite sprite = new(
                1,
                2,
                -AdventureMapView.MonsterAnchor.X,
                -AdventureMapView.MonsterAnchor.Y - (ReachUp ? 1 : 0));

            sprite.Image[0, 0] = 60;
            sprite.Image[0, 1] = 61;

            return sprite;
        }
    }

}

/// <summary>
/// Covers <see cref="MonsterSpriteSet"/> against the installed archives: the nine frames per monster of
/// <c>MINIMON.ICN</c> and the frame a creature asks for.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MonsterSpriteSetTests
{
    [Fact]
    public void EachMonsterHasNineFrames()
    {
        // Monster::UNKNOWN is zero and has no frames, so the peasant starts at frame 0.
        Assert.Equal(0, MonsterSpriteSet.FrameOf(1));
        Assert.Equal(9, MonsterSpriteSet.FrameOf(2));
        Assert.Equal(54, MonsterSpriteSet.FrameOf(7));
        Assert.Equal(585, MonsterSpriteSet.FrameOf(66));
    }

    [Fact]
    public void EveryMonsterOfTheTableHasAFrame()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        MonsterSpriteSet? set = MonsterSpriteSet.TryCreate(assets);

        Assert.NotNull(set);
        Assert.Empty(set!.Missing);

        Sprite? peasant = set.GetMonster(1);

        Assert.NotNull(peasant);
        Assert.Equal(31, peasant!.Width);
        Assert.Equal(28, peasant.Height);

        // The last creature of the table still has its standing frame; one past it the file ends.
        Assert.NotNull(set.GetMonster(66));
        Assert.Null(set.GetMonster(67));
        Assert.Null(set.GetMonster(Mp2MonsterName.Unknown));
    }
}
