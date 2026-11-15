using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the hero half of the map loader: the <c>MP2::OBJ_HERO</c> and <c>MP2::OBJ_JAIL</c> information
/// blocks (<c>world_loadmap.cpp:528</c>, <c>:583</c>) and the placement of the hero on the tile
/// (<c>world_loadmap.cpp:1583</c>).
/// </summary>
public sealed class WorldHeroLoadTests
{
    private const int MapSize = 36;

    /// <summary>Tile the hero of every test stands on; 1 is grass, far from the edges.</summary>
    private const int HeroTileIndex = 37;

    /// <summary>
    /// A hero tile of the map file: the <c>MINIHERO</c> sprite of <paramref name="spriteIndex"/> refers to
    /// the information block <paramref name="blockId"/>.
    /// </summary>
    private static Mp2TileInfo HeroTile(int blockId, int spriteIndex = 0) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.MiniHero << 2),
        BottomIcnImageIndex = (byte)spriteIndex,

        // Object layer in the low two bits, and the block id in the 13 bit quantity word.
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

    /// <summary>
    /// A map with one hero tile and the given information block, the smallest map that has a hero.
    /// </summary>
    private static Mp2Map MapWithHeroBlock(byte[]? block, int spriteIndex = 0)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTileIndex, HeroTile(blockId: 1, spriteIndex));

        return block is null ? map : map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>
    /// A jail tile of the map file: sprite 9 of <c>XLOC2</c> on the object layer is the prison itself, and
    /// its quantity word refers to the information block <paramref name="blockId"/>.
    /// </summary>
    private static Mp2TileInfo JailTile(int blockId) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.XLoc2 << 2),
        BottomIcnImageIndex = 9,
        Quantity1 = (byte)((blockId << 3) | 1),
        Quantity2 = 0,
        ObjectName2 = 0,
        TopIcnImageIndex = Mp2TileInfo.EmptyImageIndex,
        TerrainFlags = 0,
        ObjectType = Mp2ObjectType.Jail,
        NextAddonIndex = 0,
        Level1ObjectUid = 1,
        Level2ObjectUid = 0,
    };

    /// <summary>
    /// A map with one jail tile and the given information block.
    /// </summary>
    private static Mp2Map MapWithJailBlock(byte[] block)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTileIndex, JailTile(blockId: 1));

        return map with { InfoBlocks = [new Mp2InfoBlock(0, block)] };
    }

    /// <summary>
    /// Builds a hero block; the portrait byte counts from 0, the engine id is <c>portraitId + 1</c>.
    /// </summary>
    private static byte[] HeroBlock(int portraitId = 0, bool customPortrait = true)
    {
        byte[] data = new byte[Mp2HeroInfo.StructureSize];

        data[17] = (byte)(customPortrait ? 1 : 0);
        data[18] = (byte)portraitId;

        return data;
    }

    /// <summary>
    /// Builds a jail block: the payload of a hero, but byte 60 is the race of the prisoner instead of the
    /// patrol distance (<c>world_loadmap.cpp:536</c>).
    /// </summary>
    private static byte[] JailBlock(int raceIndex, int portraitId = 0, bool customPortrait = true)
    {
        byte[] data = HeroBlock(portraitId, customPortrait);

        data[60] = (byte)raceIndex;

        return data;
    }

    [Fact]
    public void AheroBlockPutsTheHeroOnHisTile()
    {
        World world = new();
        world.LoadMap(MapWithHeroBlock(HeroBlock()));

        // The portrait byte names Lord Kilburn, and the sprite of the tile says blue and knight, which is
        // what he is. A custom portrait marks the hero as custom on its own (heroes.cpp:486-488).
        Hero hero = world.GetHero(Hero.LordKilburn)!;

        Assert.Equal(HeroTileIndex, hero.MapIndex);
        Assert.Equal(PlayerColor.Blue, hero.Color);
        Assert.Equal(MapRace.Knight, hero.Race);
        Assert.True(hero.Modes.HasFlag(HeroModes.Custom));
        Assert.False(hero.IsAvailableForHire);

        Tile tile = world.GetTile(HeroTileIndex);

        Assert.Equal(Mp2ObjectType.ActionHero, tile.MainObjectType);
        Assert.Equal(Hero.LordKilburn, tile.OccupantHeroId);
        Assert.Same(hero, tile.GetHero(world));
    }

    [Fact]
    public void TheHeroSpriteOfTheEditorIsRemoved()
    {
        World world = new();
        world.LoadMap(MapWithHeroBlock(HeroBlock()));

        Tile tile = world.GetTile(HeroTileIndex);

        // The map file draws a MINIHERO object, the game draws the hero himself (world_loadmap.cpp:1586).
        Assert.Equal(Mp2ObjectIcnType.Unknown, tile.MainObjectPart.IcnType);
        Assert.DoesNotContain(tile.GroundObjectParts, part => part.IcnType == Mp2ObjectIcnType.MiniHero);
    }

    [Fact]
    public void ThePortraitOfTheBlockNamesTheHero()
    {
        World world = new();

        // Portrait 10 is hero 11, a barbarian, while the tile says knight: the hero is reclassified and
        // marked as custom, which is what the reference does (heroes.cpp:432).
        world.LoadMap(MapWithHeroBlock(HeroBlock(portraitId: 10)));

        Hero hero = world.GetHero(11)!;

        Assert.Equal(11, hero.PortraitId);
        Assert.Equal(MapRace.Knight, hero.Race);
        Assert.True(hero.Modes.HasFlag(HeroModes.Custom));
        Assert.Equal(PlayerColor.Blue, hero.Color);
        Assert.Equal(HeroTileIndex, hero.MapIndex);
        Assert.Equal(11, world.GetTile(HeroTileIndex).OccupantHeroId);
    }

    [Fact]
    public void WithoutAPortraitTheHeroComesFromTheRecruitmentPool()
    {
        World world = new();
        world.LoadMap(MapWithHeroBlock(HeroBlock(customPortrait: false)));

        // The sprite says blue and knight, so a knight that no kingdom has hired is claimed by the map.
        Tile tile = world.GetTile(HeroTileIndex);
        Hero hero = tile.GetHero(world)!;

        Assert.InRange(hero.Id, Hero.LordKilburn, 9);
        Assert.Equal(MapRace.Knight, hero.Race);
        Assert.Equal(PlayerColor.Blue, hero.Color);
        Assert.Equal(HeroTileIndex, hero.MapIndex);
        Assert.Equal(hero.Id, tile.OccupantHeroId);
    }

    [Fact]
    public void AHeroTileWithoutABlockStaysEmpty()
    {
        World world = new();
        world.LoadMap(MapWithHeroBlock(block: null));

        Tile tile = world.GetTile(HeroTileIndex);

        // A hero object without a hero is a broken map; the reference asserts, the port leaves the tile as
        // the file describes it instead of dropping the object.
        Assert.Null(tile.GetHero(world));
        Assert.Equal(Hero.UnknownId, tile.OccupantHeroId);
        Assert.Equal(Mp2ObjectType.Hero, tile.MainObjectType);
    }

    [Fact]
    public void ABrokenBlockIsSkipped()
    {
        World world = new();
        world.LoadMap(MapWithHeroBlock(new byte[Mp2HeroInfo.StructureSize - 1]));

        Assert.Null(world.GetTile(HeroTileIndex).GetHero(world));
        Assert.Equal(Mp2ObjectType.Hero, world.GetTile(HeroTileIndex).MainObjectType);
    }

    [Fact]
    public void APoLHeroOnANonExpansionMapIsReplacedFromThePool()
    {
        World world = new();

        // Portrait 70 is Jarkonas, a The Price of Loyalty hero. A map of the original game has no slot for
        // him (AllHeroes::Init only creates him for expansion maps). The reference finds an UNKNOWN
        // placeholder there instead, which is no hero for hire, so the tile gets a knight from the pool
        // (world_loadmap.cpp:604-609).
        world.LoadMap(MapWithHeroBlock(HeroBlock(portraitId: 70)));

        Assert.Null(world.GetHero(Hero.Jarkonas));

        Hero hero = world.GetTile(HeroTileIndex).GetHero(world)!;

        Assert.InRange(hero.Id, Hero.LordKilburn, 9);
        Assert.Equal(MapRace.Knight, hero.Race);
        Assert.Equal(PlayerColor.Blue, hero.Color);
    }

    [Fact]
    public void ACustomNameAndAPatrolAreReadFromTheBlock()
    {
        byte[] block = HeroBlock();
        block[45] = 1;
        "Zed"u8.CopyTo(block.AsSpan(46));
        block[59] = 1;
        block[60] = 7;

        World world = new();
        world.LoadMap(MapWithHeroBlock(block));

        Hero hero = world.GetHero(Hero.LordKilburn)!;

        Assert.Equal("Zed", hero.Name);
        Assert.True(hero.Modes.HasFlag(HeroModes.Custom));
        Assert.True(hero.Modes.HasFlag(HeroModes.Patrol));
        Assert.Equal(7, hero.PatrolDistance);
        Assert.Equal(HeroTileIndex, hero.PatrolCenterIndex);
    }

    [Fact]
    public void AHeroTheMapNamesTwiceIsOnlyPlacedOnce()
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(HeroTileIndex, HeroTile(blockId: 1));
        map.SetTileAt(HeroTileIndex + 1, HeroTile(blockId: 2));
        map = map with
        {
            InfoBlocks =
            [
                new Mp2InfoBlock(0, HeroBlock()),
                new Mp2InfoBlock(1, HeroBlock()),
            ],
        };

        World world = new();
        world.LoadMap(map);

        // Lord Kilburn already stands on the first tile, so he is no longer available for hire and the
        // second block draws another knight from the pool (world_loadmap.cpp:608).
        Assert.Equal(Hero.LordKilburn, world.GetTile(HeroTileIndex).OccupantHeroId);

        int second = world.GetTile(HeroTileIndex + 1).OccupantHeroId;

        Assert.NotEqual(Hero.UnknownId, second);
        Assert.NotEqual(Hero.LordKilburn, second);
        Assert.Equal(HeroTileIndex + 1, world.GetHero(second)!.MapIndex);
    }

    [Fact]
    public void TheHeroTableOfTheMapIsCreatedBeforeTheHeroesAreRead()
    {
        World world = new();
        world.LoadMap(MapWithHeroBlock(HeroBlock()));

        // AllHeroes::Init() runs as part of the defaults of the map: 54 heroes of the six races plus the six
        // campaign heroes, and no The Price of Loyalty hero.
        Assert.Equal(60, world.Heroes.Count);
        Assert.Null(world.GetHero(Hero.Solmyr));
    }

    [Fact]
    public void AJailBlockPutsTheHeroInACellOfNoKingdom()
    {
        World world = new();
        world.LoadMap(MapWithJailBlock(JailBlock(raceIndex: 0)));

        // The prisoner is no one's hero and cannot be hired, but he occupies the index of the jail so that
        // visiting the prison can free him (world_loadmap.cpp:576).
        Hero hero = world.GetHero(Hero.LordKilburn)!;

        Assert.True(hero.Modes.HasFlag(HeroModes.Jail));
        Assert.Equal(PlayerColor.None, hero.Color);
        Assert.Equal(MapRace.Knight, hero.Race);
        Assert.Equal(HeroTileIndex, hero.MapIndex);
        Assert.False(hero.IsAvailableForHire);

        // The jail itself stays on the tile: the prison object is what the player visits, not the hero. The
        // tile keeps the non-action value of the file, and the action type is what a visit asks for.
        Tile tile = world.GetTile(HeroTileIndex);

        Assert.Equal(Mp2ObjectType.Jail, tile.MainObjectType);
        Assert.Equal(Mp2ObjectType.ActionJail, Mp2ObjectTypes.BaseActionType(tile.MainObjectType));
        Assert.Equal(Hero.UnknownId, tile.OccupantHeroId);
        Assert.Null(tile.GetHero(world));
    }

    [Fact]
    public void TheRaceByteOfAJailChangesTheRaceOfThePrisoner()
    {
        World world = new();

        // Byte 60 is the race of the prisoner, and 5 is the necromancer, which is not the race of the hero
        // the portrait names, so the hero is marked as custom (world_loadmap.cpp:535-560).
        world.LoadMap(MapWithJailBlock(JailBlock(raceIndex: 5)));

        Hero hero = world.GetHero(Hero.LordKilburn)!;

        Assert.Equal(MapRace.Necromancer, hero.Race);
        Assert.True(hero.Modes.HasFlag(HeroModes.Custom));
        Assert.True(hero.Modes.HasFlag(HeroModes.Jail));
    }

    [Fact]
    public void AnUnknownJailRaceImprisonsAKnight()
    {
        World world = new();

        // 7 is not a playable race; the reference warns and keeps the knight it started with.
        world.LoadMap(MapWithJailBlock(JailBlock(raceIndex: 7)));

        Hero hero = world.GetHero(Hero.LordKilburn)!;

        Assert.Equal(MapRace.Knight, hero.Race);
        Assert.True(hero.Modes.HasFlag(HeroModes.Jail));
    }

    [Fact]
    public void AJailHeroWithoutAPortraitIsDrawnFromThePool()
    {
        World world = new();
        world.LoadMap(MapWithJailBlock(JailBlock(raceIndex: 1, customPortrait: false)));

        Hero? jailed = null;

        for (int heroId = Hero.LordKilburn; heroId <= Hero.Jarkonas; ++heroId)
        {
            if (world.GetHero(heroId) is { } hero && hero.Modes.HasFlag(HeroModes.Jail))
            {
                jailed = hero;

                break;
            }
        }

        // Byte 60 is 1, the barbarian, so the pool only hands out barbarians (heroes.cpp:19-25).
        (int min, int max) = HeroRaces.GetHeroIdRange(MapRace.Barbarian);

        Assert.NotNull(jailed);
        Assert.InRange(jailed.Id, min, max);
        Assert.Equal(MapRace.Barbarian, jailed.Race);
        Assert.Equal(PlayerColor.None, jailed.Color);
        Assert.Equal(HeroTileIndex, jailed.MapIndex);

        // Nobody else was claimed, so the pool still has the knights the map did not use.
        Assert.Equal(Hero.NoPosition, world.GetHero(Hero.LordKilburn)!.MapIndex);
    }
}
