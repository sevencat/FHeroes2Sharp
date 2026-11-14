using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the boat layer of <see cref="AdventureMapView"/>: the hull replaces the editor frame of a
/// boat tile, its rows are painted at the three moments of the frame and the shadow is laid down with
/// the shadow layer.
/// </summary>
/// <remarks>
/// The frame is 8x8 pixels with 2x2 tiles and the boat floats on tile (1; 2), so its tile covers rows
/// 4 and 5. The anchor of a boat is <c>( 0, 21 )</c> in tile pixels, far outside a 2 pixel tile, so
/// the fake frames carry an offset that cancels it: a mock sprite at <c>( 0, -21 )</c> lands on the
/// corner of its tile, exactly where a real frame anchored at the anchor would be.
/// </remarks>
public sealed class BoatLayerTests
{
    private const int TileSize = 2;

    /// <summary>Column the boat floats in, i.e. the first column of its sprite.</summary>
    private const int BoatColumn = 2;

    /// <summary>Tile index of the boat, row major on the 4x4 synthetic map.</summary>
    private const int BoatTileIndex = (2 * 4) + 1;

    /// <summary>Tile index of the tile below the boat, where its lower rows land.</summary>
    private const int TileBelowBoat = BoatTileIndex + 4;

    [Fact]
    public void BoatIsDrawnOnItsTile()
    {
        PixelBuffer target = Draw(CreateView());

        Assert.Equal(70, target.Row(4)[BoatColumn]);
        Assert.Equal(71, target.Row(5)[BoatColumn]);
    }

    [Fact]
    public void TheEditorFrameOfTheBoatIsReplacedByTheHull()
    {
        // An MP2 file keeps the OBJNWAT2 frame the original editor shows in the ground part of a boat
        // tile; the reference overwrites it with BOAT32 on load, so that frame must not be painted.
        PixelBuffer target = Draw(CreateView());

        Assert.NotEqual(9, target.Row(4)[BoatColumn]);
        Assert.Equal(70, target.Row(4)[BoatColumn]);
    }

    [Fact]
    public void BoatRowsBelowItsTile_AreDrawnBeforeTheObjectLayer()
    {
        // The rows that fall into the tile below belong to the "below" part of the split: an object on
        // that tile covers them.
        AdventureMapView view = CreateView(index => index == TileBelowBoat
            ? GroundObject()
            : BoatTile(index));

        PixelBuffer target = Draw(view);

        Assert.Equal(9, target.Row(6)[BoatColumn]);
        Assert.Equal(9, target.Row(7)[BoatColumn]);

        // The rows on the boat's own tile are painted later and stay.
        Assert.Equal(70, target.Row(4)[BoatColumn]);
    }

    [Fact]
    public void BoatRowsAboveItsTile_AreDrawnAfterTheTopLayer()
    {
        // A hull is two tiles high in the reference; the rows that reach over the tile above are the
        // "above" part and are painted after the top layer, so a top object cannot hide them.
        AdventureMapView view = CreateView(
            index => index == BoatTileIndex - 4
                ? TopObject()
                : BoatTile(index),
            sprites: new TestBoatSprites { ReachUp = true });

        PixelBuffer target = Draw(view);

        Assert.Equal(70, target.Row(3)[BoatColumn]);

        // The rest of that tile is the top object, which was painted first.
        Assert.Equal(12, target.Row(2)[BoatColumn]);
    }

    [Fact]
    public void TheShadowOfABoat_IsDrawnWithTheShadowLayer()
    {
        // BOATSHAD is a separate archive entry and goes down with the shadows: it covers the water the
        // boat floats on, and the hull is painted over it afterwards.
        PixelBuffer target = Draw(CreateView());

        Assert.Equal(40, target.Row(4)[BoatColumn + 1]);
        Assert.Equal(70, target.Row(4)[BoatColumn]);
    }

    [Fact]
    public void WithoutBoatSprites_TheEditorFrameStays()
    {
        // A view without boat sprites is the map editor of this port: it keeps the frame of the water
        // object set, which is what the editor of the original game shows.
        PixelBuffer target = Draw(CreateView(withBoats: false));

        Assert.Equal(9, target.Row(4)[BoatColumn]);
    }

    [Fact]
    public void TheAnchorIsTheBoatOffsetOfTheReference()
    {
        Assert.Equal(new Point(0, 21), AdventureMapView.BoatAnchor);
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
        bool withBoats = true,
        TestBoatSprites? sprites = null)
    {
        Mp2Map map = SyntheticMap.Create(4, 4, tileFactory ?? BoatTile);
        TerrainTileSet tiles = SyntheticTerrain.Create(4, TileSize);

        IBoatSpriteSource? boats = withBoats ? sprites ?? new TestBoatSprites() : null;
        return new AdventureMapView(map, tiles, new Size(8, 8), new TestObjectSprites(), null, null, boats);
    }

    /// <summary>A terrain tile, or the boat tile when <paramref name="index"/> is the boat's.</summary>
    private static Mp2TileInfo BoatTile(int index) =>
        index == BoatTileIndex
            ? BlankTile() with
            {
                ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnWat2 << 2),
                BottomIcnImageIndex = 23,
                ObjectType = Mp2ObjectType.Boat,
            }
            : BlankTile();

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

    private static Mp2TileInfo BlankTile() => SyntheticMap.BlankTile();

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
    /// A 1x4 hull of 70 and 71 over two tile rows and a 1x1 shadow of 40 on the right of its tile.
    /// </summary>
    private sealed class TestBoatSprites : IBoatSpriteSource
    {
        /// <summary>Moves the hull one row up, so its upper row belongs to the tile above.</summary>
        public bool ReachUp { get; init; }

        public Sprite? GetBoat()
        {
            // The anchor of a boat is ( 0, 21 ): cancelling it puts the frame on the tile corner, and
            // one pixel less lifts it over the row above.
            Sprite sprite = new(
                1,
                4,
                0,
                -AdventureMapView.BoatAnchor.Y - (ReachUp ? 1 : 0));

            sprite.Image[0, 0] = 70;
            sprite.Image[0, 1] = 71;
            sprite.Image[0, 2] = 72;
            sprite.Image[0, 3] = 73;

            return sprite;
        }

        public Sprite? GetShadow()
        {
            Sprite shadow = new(1, 1, 1, -AdventureMapView.BoatAnchor.Y);
            shadow.Image[0, 0] = 40;
            return shadow;
        }
    }
}

/// <summary>
/// Covers <see cref="BoatSpriteSet"/> against the installed archives: the hull and the shadow of the
/// boat frame a map uses.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class BoatSpriteSetTests
{
    [Fact]
    public void TheHullAndTheShadowOfAnEmptyBoatExist()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        BoatSpriteSet? set = BoatSpriteSet.TryCreate(assets);

        Assert.NotNull(set);
        Assert.Empty(set!.Missing);

        Sprite? hull = set.GetBoat();
        Sprite? shadow = set.GetShadow();

        Assert.NotNull(hull);
        Assert.NotNull(shadow);

        // Measure of the RIGHT facing hull the reference forces on every boat of an MP2 file: the hull
        // is 33 pixels higher than the corner of its tile and reaches two tiles up.
        Assert.Equal(62, hull!.Width);
        Assert.Equal(56, hull.Height);
        Assert.Equal(-8, hull.OffsetX);
        Assert.Equal(-54, hull.OffsetY);

        Assert.Equal(74, shadow!.Width);
        Assert.Equal(28, shadow.Height);
        Assert.Equal(-38, shadow.OffsetX);
        Assert.Equal(-28, shadow.OffsetY);
    }

    [Fact]
    public void WithoutDataThereIsNoSet()
    {
        Assert.Null(BoatSpriteSet.TryCreate(null));
    }
}
