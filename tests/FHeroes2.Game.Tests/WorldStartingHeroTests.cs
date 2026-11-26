using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Kingdoms::ApplyPlayWithStartingHero()</c> (<c>kingdom.cpp:701-708</c>) and the two branches of
/// <c>Kingdom::ApplyPlayWithStartingHero()</c> (<c>kingdom.cpp:504-557</c>) that the map loader runs at the end of
/// <c>World::LoadMapMP2()</c> (<c>world_loadmap.cpp:1456</c>).
/// </summary>
/// <remarks>
/// The campaign maps of the original editor rely on the second branch: they set
/// <c>startWithHeroInFirstCastle</c> and place no hero on the map, so a scenario only has a hero because the loader
/// hires one. A map that sets the flag and one that does not are both checked, and so is a hero standing under a
/// town's gate, which is the first branch.
/// </remarks>
public sealed class WorldStartingHeroTests
{
    private const int MapSize = 36;

    private const int TownX = 18;

    private const int TownY = 18;

    private const int TownTileIndex = (TownY * MapSize) + TownX;

    /// <summary>The tile under the gate, where a map may place a hero that starts in the town.</summary>
    private const int GateTileIndex = ((TownY + 1) * MapSize) + TownX;

    [Fact]
    public void AMapThatAsksForAStartingHeroHiresOneInItsFirstCastle()
    {
        World world = new();

        world.LoadMap(MapWithBlueTown(startWithHeroInFirstCastle: true));

        Hero hero = Assert.Single(world.GetKingdom(PlayerColor.Blue).Heroes);

        // The hero stands on the town itself, is owned by the player and is a hero of the town's race
        // (kingdom.cpp:552-555 hires GetHeroForHire( first->GetRace() )).
        Assert.Equal(TownTileIndex, hero.MapIndex);
        Assert.Equal(PlayerColor.Blue, hero.Color);
        Assert.Equal(MapRace.Wizard, hero.Race);
        Assert.Same(hero, world.Tiles[TownTileIndex].GetHero(world));
    }

    [Fact]
    public void AMapWithoutTheFlagStartsWithoutAHero()
    {
        World world = new();

        world.LoadMap(MapWithBlueTown(startWithHeroInFirstCastle: false));

        Assert.Empty(world.GetKingdom(PlayerColor.Blue).Heroes);
        Assert.Null(world.Tiles[TownTileIndex].GetHero(world));
    }

    [Fact]
    public void AHeroUnderTheGateMovesIntoTheTownInstead()
    {
        Mp2Map map = MapWithBlueTown(startWithHeroInFirstCastle: false);

        // The first branch of the reference: the map placed a hero of the town's colour on the gate tile, so he is
        // moved onto the town and the map's flag is never consulted (kingdom.cpp:517-541).
        // The hero is the second block, so its tile carries metadata two.
        map.SetTileAt(GateTileIndex, MapHeroTile(blockId: 2));
        map = map with { InfoBlocks = [.. map.InfoBlocks, new Mp2InfoBlock(1, HeroBlock())] };

        World world = new();
        world.LoadMap(map);

        Hero hero = Assert.Single(world.GetKingdom(PlayerColor.Blue).Heroes);

        Assert.Equal(TownTileIndex, hero.MapIndex);
        Assert.Same(hero, world.Tiles[TownTileIndex].GetHero(world));
        Assert.Null(world.Tiles[GateTileIndex].GetHero(world));
    }

    /// <summary>
    /// A map whose player (<see cref="PlayerColor.Blue"/>) owns one fortified wizard town and that has no hero on it,
    /// the shape of a campaign map made by the original editor.
    /// </summary>
    /// <param name="startWithHeroInFirstCastle">The header flag <c>Mp2FileInfo.StartWithHeroInFirstCastle</c>.</param>
    /// <returns>The map.</returns>
    private static Mp2Map MapWithBlueTown(bool startWithHeroInFirstCastle)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        // A block's ObjectId is its zero based index plus one, and that id is the metadata of the tile it describes:
        // the town is the first block, so its tile carries metadata one.
        map.SetTileAt(TownTileIndex, TownTile(Mp2ObjectType.Castle, blockId: 1));

        return map with
        {
            Castles = [new Mp2CastlePosition(TownX, TownY, 0x80 | 4, MapRace.Wizard)],
            InfoBlocks = [new Mp2InfoBlock(0, TownBlock(owner: 0, faction: 4))],
            Info = map.Info with { StartWithHeroInFirstCastle = startWithHeroInFirstCastle },
        };
    }

    /// <summary>A town entrance tile of the map file: the block it refers to and its object type.</summary>
    /// <param name="type">The object type of the tile.</param>
    /// <param name="blockId">The information block the tile refers to.</param>
    /// <returns>The tile.</returns>
    private static Mp2TileInfo TownTile(Mp2ObjectType type, int blockId) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnTwrd << 2),
        BottomIcnImageIndex = 13,
        Quantity1 = (byte)((blockId << 3) | 1),
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = type,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>A hero tile of the map file, the <c>MINIHERO</c> sprite referring to a block.</summary>
    /// <param name="blockId">The information block the tile refers to.</param>
    /// <returns>The tile.</returns>
    /// <remarks>
    /// Frame four of <c>MINIHERO</c> is the blue wizard (see <see cref="Mp2MapHeroes.FromSpriteIndex"/>), which is the
    /// colour and race of the town this test fabricates.
    /// </remarks>
    private static Mp2TileInfo MapHeroTile(int blockId) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = 4,
        Quantity1 = (byte)((blockId << 3) | 1),
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Hero,
        NextAddonIndex = 0,
        Level1ObjectUid = 2,
        Level2ObjectUid = 0,
    };

    /// <summary>
    /// A 76 byte hero block, <c>world_loadmap.cpp:528-580</c>: the custom name flag, the name, the experience, the
    /// army and the artifacts are zero, and byte 41 is "no portrait", which the loader accepts.
    /// </summary>
    /// <returns>The block.</returns>
    private static byte[] HeroBlock() => new byte[Mp2HeroInfo.StructureSize];

    /// <summary>
    /// Builds a 70 byte town block, <see cref="CastleTown.Mp2BlockSize"/> bytes of <c>castle.cpp:102-395</c>.
    /// </summary>
    /// <param name="owner">The owner byte: 0 to 5 are blue to purple, anything else is "nobody".</param>
    /// <param name="faction">The race byte: 0 to 5 are the six races.</param>
    /// <returns>The block.</returns>
    private static byte[] TownBlock(int owner, int faction)
    {
        byte[] data = new byte[CastleTown.Mp2BlockSize];

        data[0] = (byte)owner;
        data[1] = 1; // Custom buildings, so the block says what it means.
        data[38] = (byte)faction;
        data[39] = 1; // The fort, so the town is the "first castle" the reference prefers.

        return data;
    }
}
