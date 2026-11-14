using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the hero layer of <see cref="AdventureMapView"/>: which rows of a hero are painted at which
/// moment of the frame. The sprites come from a dictionary, so every pixel of the result can be traced
/// back to the sprite it came from.
/// </summary>
/// <remarks>
/// The frame is 8x8 pixels with 2x2 tiles and the hero stands on tile (1; 2), so its tile covers rows
/// 4 and 5:
/// <code>
///   row 0,1   flag, reaches over the tile above
///   row 2,3   hero body above its tile
///   row 4,5   hero body on its own tile
///   row 6,7   hero body below its tile
/// </code>
/// </remarks>
public sealed class HeroLayerTests
{
    private const int TileSize = 2;

    /// <summary>Column the hero stands in, i.e. the first column of its sprite.</summary>
    private const int HeroColumn = 2;

    /// <summary>Tile index of the hero, row major on the 4x4 synthetic map.</summary>
    private const int HeroTileIndex = (2 * 4) + 1;

    [Fact]
    public void HeroIsDrawnInThreeBandsAroundItsTile()
    {
        PixelBuffer target = Draw(CreateView());

        // Flag (30) over the tile above, body (20..25) split around the tile the hero stands on.
        Assert.Equal(30, target.Row(0)[HeroColumn]);
        Assert.Equal(30, target.Row(1)[HeroColumn]);
        Assert.Equal(20, target.Row(2)[HeroColumn]);
        Assert.Equal(21, target.Row(3)[HeroColumn]);
        Assert.Equal(22, target.Row(4)[HeroColumn]);
        Assert.Equal(23, target.Row(5)[HeroColumn]);
        Assert.Equal(24, target.Row(6)[HeroColumn]);
        Assert.Equal(25, target.Row(7)[HeroColumn]);
    }

    [Fact]
    public void HeroShadow_IsDrawnWhenTheShadowLayerIsPainted()
    {
        // The shadow is a single pixel left of the hero, one row below its tile top.
        PixelBuffer target = Draw(CreateView());

        Assert.Equal(40, target.Row(5)[HeroColumn - 1]);
    }

    [Fact]
    public void Hero_IsDrawnOverTheObjectLayerOfItsOwnTile()
    {
        // A ground object of the add-on chain covers the hero's tile; the body is painted after
        // OBJECT_LAYER, so it wins. The hero has to stay in the main slot, that is where its MINIHERO
        // frame lives.
        AdventureMapView view = CreateView(
            index => index == HeroTileIndex ? HeroTile(index) with { NextAddonIndex = 1 } : HeroTile(index),
            addons: [GroundAddon()]);

        PixelBuffer target = Draw(view);

        Assert.Equal(9, target.Row(4)[HeroColumn + 1]);
        Assert.Equal(22, target.Row(4)[HeroColumn]);
        Assert.Equal(23, target.Row(5)[HeroColumn]);
    }

    [Fact]
    public void HeroRowsBelowItsTile_AreDrawnBeforeTheObjectLayer()
    {
        // The rows of the body that fall into the tile below the hero are part of
        // heroBackgroundImages: an object on that tile covers them.
        AdventureMapView view = CreateView(index => index == HeroTileIndex + 4
            ? GroundObject()
            : HeroTile(index));

        PixelBuffer target = Draw(view);

        Assert.Equal(9, target.Row(6)[HeroColumn]);
        Assert.Equal(9, target.Row(7)[HeroColumn]);

        // The rows on the hero's own tile are unaffected, they are painted later.
        Assert.Equal(22, target.Row(4)[HeroColumn]);
    }

    [Fact]
    public void HeroRowsAboveItsTile_AreDrawnAfterTheTopLayer()
    {
        // A top layer object on the tile above the hero would cover the body rows that reach into it,
        // unless those rows are painted last — which is what the reference does with topImages.
        AdventureMapView view = CreateView(index => index == HeroTileIndex - 4
            ? TopObject()
            : HeroTile(index));

        PixelBuffer target = Draw(view);

        Assert.Equal(20, target.Row(2)[HeroColumn]);
        Assert.Equal(21, target.Row(3)[HeroColumn]);

        // The rest of that tile is the top object, which was painted before the hero's upper rows.
        Assert.Equal(12, target.Row(2)[HeroColumn + 1]);
    }

    [Fact]
    public void HeroSprites_AreLookedUpByTheColorAndRaceOfTheTile()
    {
        // MINIHERO frame 22 is 3 * 7 + 1: yellow, barbarian.
        TestHeroSprites sprites = new();
        Draw(CreateView(sprites: sprites));

        Assert.Equal(MapRace.Barbarian, sprites.LastBodyRace);
        Assert.Equal(PlayerColor.Yellow, sprites.LastFlagColor);
    }

    [Fact]
    public void WithoutHeroSprites_TheTileIsLeftAsItIs()
    {
        PixelBuffer target = Draw(CreateView(withHeroes: false));

        // Row 4 of the hero tile holds terrain image 1, which is the flat value 2.
        Assert.Equal(2, target.Row(4)[HeroColumn]);
        Assert.Equal(2, target.Row(2)[HeroColumn]);
    }

    [Fact]
    public void HeroWithoutAFlagTileSet_StillDrawsItsBody()
    {
        TestHeroSprites sprites = new() { FlagIsMissing = true };
        PixelBuffer target = Draw(CreateView(sprites: sprites));

        Assert.Equal(2, target.Row(0)[HeroColumn]);
        Assert.Equal(22, target.Row(4)[HeroColumn]);
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
        Mp2AddonInfo[]? addons = null,
        bool withHeroes = true,
        TestHeroSprites? sprites = null)
    {
        Mp2Map map = SyntheticMap.Create(4, 4, tileFactory ?? HeroTile, addons);
        TerrainTileSet tiles = SyntheticTerrain.Create(4, TileSize);

        IHeroSpriteSource? heroes = withHeroes ? sprites ?? new TestHeroSprites() : null;
        return new AdventureMapView(map, tiles, new Size(8, 8), new TestObjectSprites(), heroes);
    }

    /// <summary>A terrain tile, or the hero tile when <paramref name="index"/> is the hero's.</summary>
    private static Mp2TileInfo HeroTile(int index) =>
        index == HeroTileIndex
            ? BlankTile() with
            {
                ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
                BottomIcnImageIndex = 22,
                ObjectType = Mp2ObjectType.Hero,
            }
            : BlankTile();

    /// <summary>The hero's tile with a ground object of <see cref="Mp2ObjectIcnType.ObjnGras"/>.</summary>
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

    /// <summary>An add-on on layer 0 holding a ground object of <see cref="Mp2ObjectIcnType.ObjnGras"/>.</summary>
    private static Mp2AddonInfo GroundAddon() =>
        new()
        {
            NextAddonIndex = 0,
            ObjectNameN1 = (byte)((int)Mp2ObjectIcnType.ObjnGras << 2),
            BottomIcnImageIndex = 0,
            QuantityN = 0,
            ObjectNameN2 = 0,
            TopIcnImageIndex = 0,
            Level1ObjectUid = 1,
            Level2ObjectUid = 0,
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
    /// Body 1x6 at anchor (0; -2) with the value 20..25 per row, flag 2x2 of 30 at anchor (0; -4) and
    /// shadow one pixel of 40 at anchor (-1; 1). The body is one pixel wide so the pixel next to it
    /// stays available for the object layers.
    /// </summary>
    private sealed class TestHeroSprites : IHeroSpriteSource
    {
        private static readonly Sprite Body = CreateBody();

        private static readonly Sprite Flag = CreateFlag();

        private static readonly Sprite Shadow = CreateShadow();

        public MapRace? LastBodyRace { get; private set; }

        public PlayerColor? LastFlagColor { get; private set; }

        /// <summary>Simulates data without a flag tile set for the hero's colour.</summary>
        public bool FlagIsMissing { get; init; }

        public Sprite? GetBody(MapRace race)
        {
            LastBodyRace = race;
            return Body;
        }

        public Sprite? GetFlag(PlayerColor color)
        {
            LastFlagColor = color;
            return FlagIsMissing ? null : Flag;
        }

        public Sprite? GetShadow() => Shadow;

        private static Sprite CreateBody()
        {
            Sprite sprite = new(1, 6, 0, -2);
            for (int row = 0; row < 6; ++row)
            {
                sprite.Image.Row(row).Fill((byte)(20 + row));
            }

            return sprite;
        }

        private static Sprite CreateFlag()
        {
            Sprite sprite = new(2, 2, 0, -4);
            sprite.Image.Fill(30);
            return sprite;
        }

        private static Sprite CreateShadow()
        {
            Sprite sprite = new(1, 1, -1, 1);
            sprite.Image.Fill(40);
            return sprite;
        }
    }
}
