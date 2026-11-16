using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the opening scouting on a world the map loader built, which is what the adventure map scene runs
/// before it hands the fog over: <c>World::ClearFog( colors )</c> (<c>world.cpp:838</c>) right after
/// <c>World::LoadMapMP2()</c> (<c>game_startgame.cpp:769</c>).
/// </summary>
/// <remarks>
/// The tests here are the assembly check of ADR-131: the heroes are the ones the map file placed, not
/// hand-made <see cref="Hero"/> objects, so a color that the loader does not carry over would leave the map
/// black even though the scouting itself works.
/// </remarks>
public sealed class WorldFogOnLoadTests
{
    private const int MapSize = 36;

    /// <summary>Tile the hero of every test stands on; grass and far from the edges.</summary>
    private const int HeroTileIndex = 37;

    private const int HeroColumn = HeroTileIndex % MapSize;
    private const int HeroRow = HeroTileIndex / MapSize;

    /// <summary>Sprite of a blue knight; the <c>MINIHERO</c> frames run seven of them per color.</summary>
    private const int BlueKnightSprite = 0;

    /// <summary>Sprite of a red knight, two colors further down the same run.</summary>
    private const int RedKnightSprite = 14;

    [Fact]
    public void ALoadedMapStartsUnderTheFog()
    {
        World world = new();
        world.LoadMap(MapWithHero(BlueKnightSprite));

        Assert.True(world.Fog!.IsFogged(0, 0, PlayerColor.Blue));
        Assert.True(world.Fog.IsFogged(HeroColumn, HeroRow, PlayerColor.Blue));
    }

    [Fact]
    public void TheOpeningScoutRevealsTheHeroOfTheHumanColor()
    {
        World world = new();
        world.LoadMap(MapWithHero(BlueKnightSprite));

        // The loader carries the color of the sprite over, which is what the scouting filters on.
        Assert.Equal(PlayerColor.Blue, world.GetTile(HeroTileIndex).GetHero(world)!.Color);

        int revealed = world.ClearFog(PlayerColor.Blue);

        Assert.True(revealed > 0);
        Assert.False(world.Fog!.IsFogged(HeroColumn, HeroRow, PlayerColor.Blue));
        Assert.Equal((MapSize * MapSize) - revealed, world.Fog.CountFogged());

        // Scouting is per color: the very tiles the blue player now sees stay dark for the red one.
        Assert.True(world.Fog.IsFogged(HeroColumn, HeroRow, PlayerColor.Red));
    }

    [Fact]
    public void AHeroOfAnotherColorIsNotScouted()
    {
        World world = new();
        world.LoadMap(MapWithHero(RedKnightSprite));

        Assert.Equal(PlayerColor.Red, world.GetTile(HeroTileIndex).GetHero(world)!.Color);

        Assert.Equal(0, world.ClearFog(PlayerColor.Blue));
        Assert.True(world.Fog!.IsFogged(HeroColumn, HeroRow, PlayerColor.Blue));
    }

    [Fact]
    public void AMapWithoutAHeroOfThatColorStaysEntirelyUnderTheFog()
    {
        World world = new();
        world.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        Assert.Equal(0, world.ClearFog(PlayerColor.Blue));

        // Nothing was revealed, and the table still has to be drawable: the renderer walks the direction
        // masks, so a fresh table without them would show a map without fog at all.
        Assert.Equal(MapSize * MapSize, world.Fog!.CountFogged());
    }

    [Fact]
    public void LoadingAnotherMapDropsTheFogOfTheFirst()
    {
        World world = new();
        world.LoadMap(MapWithHero(BlueKnightSprite));

        Assert.True(world.ClearFog(PlayerColor.Blue) > 0);

        world.LoadMap(Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex));

        Assert.Equal(0, world.ClearFog(PlayerColor.Blue));
        Assert.True(world.Fog!.IsFogged(HeroColumn, HeroRow, PlayerColor.Blue));
        Assert.Equal(MapSize * MapSize, world.Fog.CountFogged());
    }

    /// <summary>
    /// A map with one hero tile and one hero information block, the smallest map that places a hero whose
    /// color comes from the sprite, <c>world_loadmap.cpp:528</c>.
    /// </summary>
    private static Mp2Map MapWithHero(int spriteIndex)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTileIndex, HeroTile(blockId: 1, spriteIndex));

        byte[] block = new byte[Mp2HeroInfo.StructureSize];
        block[17] = 1;

        return map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>
    /// A hero tile of the map file: the <c>MINIHERO</c> sprite of <paramref name="spriteIndex"/> refers to
    /// the information block <paramref name="blockId"/> through the 13 bit quantity word.
    /// </summary>
    private static Mp2TileInfo HeroTile(int blockId, int spriteIndex) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = (byte)spriteIndex,
        Quantity1 = (byte)((blockId << 3) | 1),
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Hero,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };
}
