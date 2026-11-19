using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The fog pass of <see cref="AdventureMapView"/>: the whole tiles that cover a fogged tile, the
/// objects the fog hides and the town parts it keeps, as <c>Maps::drawFog</c> and the two town calls
/// of <c>Interface::GameArea::Redraw</c> do.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class FogLayerTests
{
    private const int TileSize = 32;
    private const int MapSize = 5;
    private const int Center = MapSize / 2;

    /// <summary>One frame of <c>OBJNTOWN</c>, any will do: the test only follows the pixels it paints.</summary>
    private const int TownFrame = 189;

    [Fact]
    public void Draw_CoversEveryFoggedTileWithTheWholeTileOfItsCheckerboard()
    {
        // No archives: the fog source is a set of four flat tiles, one value each, so the pixel of a
        // tile tells both that it was covered and which variant was picked.
        MapFog fog = new(MapSize, MapSize);
        fog.UpdateDirections(PlayerColor.Blue);

        PixelBuffer target = Render(
            SyntheticMap.Create(MapSize, MapSize),
            fog,
            new FlatFogSprites(),
            heroSprites: null,
            objectSprites: null);

        for (int row = 0; row < MapSize; ++row)
        {
            for (int column = 0; column < MapSize; ++column)
            {
                Assert.Equal(
                    FlatFogSprites.ValueOf((column + row) % FogFrames.FullTileCount),
                    target[(column * TileSize) + (TileSize / 2), (row * TileSize) + (TileSize / 2)]);
            }
        }
    }

    [Fact]
    public void Draw_LeavesADiscoveredTileBare()
    {
        MapFog fog = new(MapSize, MapSize);
        fog.Reveal(Center, Center, 0, PlayerColor.Blue);
        fog.UpdateDirections(PlayerColor.Blue);

        PixelBuffer target = Render(
            SyntheticMap.Create(MapSize, MapSize),
            fog,
            new FlatFogSprites(),
            heroSprites: null,
            objectSprites: null);

        // The revealed tile keeps the single terrain image of the synthetic ground, which is filled with
        // one, while a tile far from it is still a whole fogged tile of its checkerboard.
        int x = (Center * TileSize) + (TileSize / 2);
        int y = (Center * TileSize) + (TileSize / 2);

        Assert.Equal(1, target[x, y]);
        Assert.Equal(FlatFogSprites.ValueOf(0), target[TileSize / 2, TileSize / 2]);
    }

    [Fact]
    public void Draw_HidesTheHeroesUnderTheFog()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        HeroSpriteSet? heroes = HeroSpriteSet.TryCreate(assets);
        Assert.NotNull(heroes);

        MapFog fog = new(MapSize, MapSize);
        fog.UpdateDirections(PlayerColor.Blue);

        Mp2TileInfo hero = HeroTile(3);
        PixelBuffer withHero = Render(
            SyntheticMap.Create(MapSize, MapSize, index => index == (Center * MapSize) + Center ? hero : SyntheticMap.BlankTile()),
            fog,
            new FlatFogSprites(),
            heroes,
            objectSprites: null);
        PixelBuffer withoutHero = Render(
            SyntheticMap.Create(MapSize, MapSize),
            fog,
            new FlatFogSprites(),
            heroes,
            objectSprites: null);

        // A hero under the fog leaves no trace at all, so the two renders are the same image.
        AssertSamePixels(withoutHero, withHero, "a hero was drawn under the fog");

        // ... while the same map without fog does draw it, so the comparison above is meaningful.
        PixelBuffer bareWithHero = Render(
            SyntheticMap.Create(MapSize, MapSize, index => index == (Center * MapSize) + Center ? hero : SyntheticMap.BlankTile()),
            fog: null,
            new FlatFogSprites(),
            heroes,
            objectSprites: null);

        Assert.True(
            Differs(bareWithHero, Render(SyntheticMap.Create(MapSize, MapSize), null, new FlatFogSprites(), heroes, null)),
            "the hero is not drawn at all without fog either");
    }

    [Fact]
    public void Draw_HidesTheTownUnderTheFogByDefault()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        ObjectSpriteSet? objects = ObjectSpriteSet.TryCreate(assets);
        Assert.NotNull(objects);

        MapFog fog = new(MapSize, MapSize);
        fog.UpdateDirections(PlayerColor.Blue);

        // The only difference between the two maps is the castle, and with the fog on and no
        // LEVEL_TOWNS both have to look the same: nothing of a town may show through the fog.
        PixelBuffer withTown = Render(TownMap(), fog, new FlatFogSprites(), heroSprites: null, objects);
        PixelBuffer withoutTown = Render(SyntheticMap.Create(MapSize, MapSize), fog, new FlatFogSprites(), heroSprites: null, objects);

        AssertSamePixels(withoutTown, withTown, "the town was drawn under the fog");
    }

    [Fact]
    public void Draw_KeepsTheTownPartsAboveTheFogWhenTownsAreAsked()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        ObjectSpriteSet? objects = ObjectSpriteSet.TryCreate(assets);
        Assert.NotNull(objects);

        Sprite town = Assert.IsType<Sprite>(objects.GetSprite(Mp2ObjectIcnType.ObjnTown, TownFrame));

        MapFog fog = new(MapSize, MapSize);
        fog.UpdateDirections(PlayerColor.Blue);

        Mp2Map map = TownMap();

        PixelBuffer fogged = Render(map, fog, new FlatFogSprites(), heroSprites: null, objects, drawTownsOverFog: true);
        PixelBuffer bare = Render(map, null, new FlatFogSprites(), heroSprites: null, objects);

        // The map only differs from the fog free one where the fog was painted, so the fog pass has to
        // paint the town frame again on top of it: the reference calls drawByObjectIcnType after the
        // fog of the tile (Maps::drawFog / Interface::GameArea::Redraw).
        int left = Center * TileSize;
        int top = Center * TileSize;
        byte[] transform = Assert.IsType<byte[]>(town.Transform);

        int painted = 0;
        int mismatches = 0;
        string first = string.Empty;

        for (int y = 0; y < town.Height; ++y)
        {
            for (int x = 0; x < town.Width; ++x)
            {
                // Transform 0 copies the sprite pixel, 1 skips it and anything above 1 runs the
                // destination pixel through the palette transform table, so only 0 can be compared
                // with the sprite colour.
                if (transform[(y * town.Width) + x] != 0)
                {
                    continue;
                }

                int targetX = left + town.OffsetX + x;
                int targetY = top + town.OffsetY + y;
                if (targetX < 0 || targetY < 0 || targetX >= fogged.Width || targetY >= fogged.Height)
                {
                    continue;
                }

                ++painted;

                if (town.Image[x, y] != fogged[targetX, targetY])
                {
                    if (mismatches == 0)
                    {
                        first = $"sprite ({x}; {y}) town={town.Image[x, y]} fogged={fogged[targetX, targetY]} bare={bare[targetX, targetY]}";
                    }

                    ++mismatches;
                }
            }
        }

        Assert.True(painted > 0, "the town frame is fully transparent");
        Assert.True(mismatches == 0, $"{mismatches} of {painted} town pixels missing from the fogged map: {first}");
        Assert.True(Differs(bare, fogged), "the fog did not change the map at all");
    }

    /// <summary>A map whose centre tile holds a town, the only object of the map.</summary>
    /// <returns>The map.</returns>
    private static Mp2Map TownMap() =>
        SyntheticMap.Create(
            MapSize,
            MapSize,
            index => index == (Center * MapSize) + Center
                ? SyntheticMap.BlankTile() with
                {
                    ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnTown << 2),
                    BottomIcnImageIndex = TownFrame,
                    ObjectType = Mp2ObjectType.Castle,
                }
                : SyntheticMap.BlankTile());

    private static Mp2TileInfo HeroTile(int spriteIndex) =>
        SyntheticMap.BlankTile() with
        {
            ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
            BottomIcnImageIndex = (byte)spriteIndex,
            ObjectType = Mp2ObjectType.Hero,
        };

    private static PixelBuffer Render(
        Mp2Map map,
        MapFog? fog,
        IFogSpriteSource? fogSprites,
        HeroSpriteSet? heroSprites,
        ObjectSpriteSet? objectSprites,
        bool drawTownsOverFog = false)
    {
        AdventureMapView view = new(
            map,
            SyntheticTerrain.Create(1, TileSize),
            new Size(MapSize * TileSize, MapSize * TileSize),
            objectSprites,
            heroSprites,
            monsters: null,
            boats: null,
            fog,
            fog is null ? null : fogSprites);

        PixelBuffer target = new(view.Viewport.Width, view.Viewport.Height);
        view.Draw(target, drawTownsOverFog);
        return target;
    }

    private static void AssertSamePixels(PixelBuffer expected, PixelBuffer actual, string because)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (int y = 0; y < expected.Height; ++y)
        {
            for (int x = 0; x < expected.Width; ++x)
            {
                Assert.True(expected[x, y] == actual[x, y], $"{because} at ( {x}; {y} )");
            }
        }
    }

    private static bool Differs(PixelBuffer first, PixelBuffer second)
    {
        for (int y = 0; y < first.Height; ++y)
        {
            for (int x = 0; x < first.Width; ++x)
            {
                if (first[x, y] != second[x, y])
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Four flat fog tiles, each filled with <c>100 + variant</c>; no boundary pieces.</summary>
    private sealed class FlatFogSprites : IFogSpriteSource
    {
        private readonly PixelBuffer[] _tiles =
        [
            Fill(ValueOf(0)),
            Fill(ValueOf(1)),
            Fill(ValueOf(2)),
            Fill(ValueOf(3)),
        ];

        public static byte ValueOf(int variant) => (byte)(100 + variant);

        public PixelBuffer? GetFullTile(int variant) => variant < _tiles.Length ? _tiles[variant] : null;

        public Sprite? GetBoundary(int frame, bool flipped) => null;

        private static PixelBuffer Fill(byte value)
        {
            PixelBuffer tile = new(TileSize, TileSize);

            for (int y = 0; y < tile.Height; ++y)
            {
                for (int x = 0; x < tile.Width; ++x)
                {
                    tile[x, y] = value;
                }
            }

            return tile;
        }
    }
}
