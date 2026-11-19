using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the world half of the hero layer of <see cref="AdventureMapView"/>: once a scenario has started the
/// adventurers come from <see cref="World.Heroes"/>, so they are drawn with the frame the walk animation and the
/// sub-tile offset the movement left on them, instead of the static hero table of the MP2 map.
/// </summary>
/// <remarks>
/// The frame is the one of <see cref="HeroLayerTests"/> — 8x8 pixels with 2x2 tiles, the hero on tile
/// (1; 2) — so the rows of the body and of the flag land on the same pixels as the tests of the map heroes:
/// <code>
///   row 0,1   flag, reaches over the tile above
///   row 2,3   hero body above its tile
///   row 4,5   hero body on its own tile
///   row 6,7   hero body below its tile
/// </code>
/// </remarks>
public sealed class WorldHeroLayerTests
{
    private const int TileSize = 2;

    /// <summary>Side of the map: the format only knows 36, 72, 108 and 144 tiles.</summary>
    private const int MapSize = 36;

    /// <summary>Column of the hero's tile, both on the map and in the frame (1 x 2 pixels).</summary>
    private const int HeroColumn = 1;

    /// <summary>Row of the hero's tile.</summary>
    private const int HeroRow = 2;

    /// <summary>Tile index of the hero: column 1 of row 2.</summary>
    private const int HeroTileIndex = (HeroRow * MapSize) + HeroColumn;

    /// <summary>A frame of the walking block, well away from the standing frame of 18.</summary>
    private const int WalkingFrame = 21;

    [Fact]
    public void AWorldHeroIsDrawnAtItsTileWithItsOwnFrame()
    {
        (World world, Mp2Map map) = LoadMap();

        // The hero table of a world holds every hero of the game; only the ones put on the map have a tile.
        Hero hero = Assert.IsAssignableFrom<Hero>(world.Heroes.GetAtMapIndex(HeroTileIndex));

        // The two things the renderer has to take from the hero: the frame of the walk animation and the half
        // tile step inside the tile. The hero table of the map has neither.
        hero.SpriteIndex = WalkingFrame;
        hero.Offset = new Point(1, 0);

        TestHeroSprites sprites = new();
        PixelBuffer target = Draw(CreateView(map, world, sprites));

        Assert.Equal(MapRace.Knight, sprites.LastBodyRace);
        Assert.Equal(WalkingFrame, sprites.LastBodyFrame);

        // The body is one pixel wide, moved one pixel right by the offset: the column right of the tile's
        // first pixel, on both rows the tile covers.
        int column = (HeroColumn * TileSize) + 1;
        Assert.Equal(WalkingFrame, target.Row(4)[column]);
        Assert.Equal(WalkingFrame, target.Row(5)[column]);
    }

    [Fact]
    public void AWorldHeroUnderTheFogIsNotDrawn()
    {
        (World world, Mp2Map map) = LoadMap();
        Hero hero = Assert.IsAssignableFrom<Hero>(world.Heroes.GetAtMapIndex(HeroTileIndex));
        hero.SpriteIndex = WalkingFrame;

        // A fog that has never been lifted: every tile is undiscovered, so the hero is under it.
        MapFog fog = new(MapSize, MapSize);
        fog.UpdateDirections(PlayerColor.Blue);

        TestHeroSprites sprites = new();
        PixelBuffer target = Draw(CreateView(map, world, sprites, fog));

        // The body was not even asked for, and the pixels of the tile are the ones of the terrain.
        Assert.Null(sprites.LastBodyFrame);
        Assert.NotEqual(WalkingFrame, target.Row(4)[HeroColumn * TileSize]);
        Assert.NotEqual(WalkingFrame, target.Row(5)[HeroColumn * TileSize]);
    }

    /// <summary>
    /// Loads a one hero map into a world, the way the scenario selection does it: a blue knight on tile
    /// (1; 2) and a header that puts only blue in play.
    /// </summary>
    /// <returns>The world and the map it was loaded from.</returns>
    private static (World World, Mp2Map Map) LoadMap()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);
        map.SetTileAt(HeroTileIndex, HeroTile());

        map = map with
        {
            InfoBlocks = [new Mp2InfoBlock(0, HeroBlock())],
            Info = map.Info with
            {
                KingdomColors = PlayerColor.Blue,
                HumanPlayers = PlayerColor.Blue,
                ComputerPlayers = PlayerColor.Blue,
            },
        };

        World world = new();
        world.LoadMap(map);

        return (world, map);
    }

    private static PixelBuffer Draw(AdventureMapView view)
    {
        PixelBuffer target = new(8, 8);
        ImageOps.FillRect(target, new Rect(0, 0, 8, 8), 200);
        view.Draw(target);

        return target;
    }

    private static AdventureMapView CreateView(
        Mp2Map map,
        World world,
        IHeroSpriteSource sprites,
        MapFog? fog = null) =>
        new(
            map,
            SyntheticTerrain.Create(1, TileSize),
            new Size(8, 8),
            objects: null,
            heroes: sprites,
            monsters: null,
            boats: null,
            fog: fog,
            fogSprites: null,
            world: world);

    /// <summary>
    /// A grass tile holding the <c>MINIHERO</c> sprite of the blue knight (sprite 0 is the knight of the first
    /// colour of <see cref="PlayerColors.Ordered"/>), referring to information block 1.
    /// </summary>
    private static Mp2TileInfo HeroTile() => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = 0,
        Quantity1 = (1 << 3) | 1,
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Hero,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>A hero information block without a custom portrait, so the pool hands out the race of the tile.</summary>
    private static byte[] HeroBlock()
    {
        byte[] data = new byte[Mp2HeroInfo.StructureSize];

        data[17] = 1;

        return data;
    }

    /// <summary>
    /// Body 1x6 at anchor (0; -2) filled with the frame index, so every pixel of the result says which
    /// frame the renderer asked for; flag 2x2 of 30 at anchor (0; -4) and shadow one pixel of 40.
    /// </summary>
    private sealed class TestHeroSprites : IHeroSpriteSource
    {
        private static readonly Sprite Flag = Create(2, 2, 0, -4, 30);

        private static readonly Sprite Shadow = Create(1, 1, -1, 1, 40);

        public MapRace? LastBodyRace { get; private set; }

        public int? LastBodyFrame { get; private set; }

        public Sprite? GetBody(MapRace race)
        {
            LastBodyRace = race;

            return Create(1, 6, 0, -2, 0);
        }

        public Sprite? GetBody(MapRace race, int frame)
        {
            LastBodyRace = race;
            LastBodyFrame = frame;

            return Create(1, 6, 0, -2, (byte)frame);
        }

        public Sprite? GetFlag(PlayerColor color) => Flag;

        public Sprite? GetShadow() => Shadow;

        private static Sprite Create(int width, int height, int offsetX, int offsetY, byte value)
        {
            Sprite sprite = new(width, height, offsetX, offsetY);
            sprite.Image.Fill(value);

            return sprite;
        }
    }
}
