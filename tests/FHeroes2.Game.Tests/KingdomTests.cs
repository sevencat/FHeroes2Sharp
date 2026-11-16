using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the kingdom skeleton: the treasury and the two rosters (<c>kingdom.cpp:103-374</c>), the seven kingdom
/// slots of a world (<c>kingdom.cpp:688-802</c>) and the step that fills them while a map loads
/// (<c>world_loadmap.cpp:1443-1445</c>).
/// </summary>
public sealed class KingdomTests
{
    private const int MapSize = 36;

    /// <summary>Column of the town of the tests that need one, far enough from the edges for a whole footprint.</summary>
    private const int TownX = 18;

    /// <summary>Row of the town of the tests that need one.</summary>
    private const int TownY = 18;

    /// <summary>The entrance tile of the town of the tests that need one.</summary>
    private const int TownTileIndex = (TownY * MapSize) + TownX;

    [Fact]
    public void AFreshKingdomOwnsNothing()
    {
        PlayerKingdom kingdom = new();

        Assert.Equal(PlayerColor.None, kingdom.Color);
        Assert.Equal(0, kingdom.Treasury.Gold);

        Assert.Empty(kingdom.Castles);
        Assert.Empty(kingdom.Heroes);

        // A kingdom without a colour and without towns is losing and lost: the two flags only look at the rosters.
        Assert.False(kingdom.HasHeroes);
        Assert.True(kingdom.IsLosingGame);
        Assert.True(kingdom.IsLoss);
        Assert.False(kingdom.CanRecruitHeroes);

        // The countdown starts past the limit, so a fresh kingdom is not "days without a town" (kingdom.cpp:124).
        Assert.Equal(PlayerKingdom.GameOverLostDays + 1, kingdom.LostTownDays);
    }

    [Fact]
    public void InitHandsOutTheResourcesOfTheDifficulty()
    {
        PlayerKingdom kingdom = new();

        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        Assert.Equal(PlayerColor.Blue, kingdom.Color);

        // kingdom.cpp:865, the player's row of NORMAL: { 7500, 20, 5, 20, 5, 5, 5 }.
        Assert.Equal(7500, kingdom.Treasury.Gold);
        Assert.Equal(20, kingdom.Treasury.Wood);
        Assert.Equal(20, kingdom.Treasury.Ore);
        Assert.Equal(5, kingdom.Treasury.Mercury);
        Assert.Equal(5, kingdom.Treasury.Sulfur);
        Assert.Equal(5, kingdom.Treasury.Crystal);
        Assert.Equal(5, kingdom.Treasury.Gems);
    }

    [Fact]
    public void TheComputerHasItsOwnResourceTable()
    {
        // Both tables are the reference's: the computer gets more than the player on the highest difficulty and
        // less than the player on the lowest one (kingdom.cpp:905-960).
        Assert.Equal(7500, PlayerKingdom.GetStartingResources(GameDifficulty.Easy, isAiControlled: true).Gold);
        Assert.Equal(10000, PlayerKingdom.GetStartingResources(GameDifficulty.Impossible, isAiControlled: true).Gold);

        // The player's side of the same two difficulties.
        Assert.Equal(10000, PlayerKingdom.GetStartingResources(GameDifficulty.Easy, isAiControlled: false).Gold);
        Assert.Equal(0, PlayerKingdom.GetStartingResources(GameDifficulty.Impossible, isAiControlled: false).Gold);

        // HARD and EXPERT belong to the second computer row, not to the easy one.
        Assert.Equal(10000, PlayerKingdom.GetStartingResources(GameDifficulty.Hard, isAiControlled: true).Gold);
        Assert.Equal(2500, PlayerKingdom.GetStartingResources(GameDifficulty.Expert, isAiControlled: false).Gold);
    }

    [Fact]
    public void AnUnknownColorGetsAnEmptyKingdom()
    {
        PlayerKingdom kingdom = new();

        kingdom.Init(PlayerColor.None, GameDifficulty.Easy, isAiControlled: false);

        // The reference logs and returns before touching the treasury (kingdom.cpp:110-114).
        Assert.Equal(PlayerColor.None, kingdom.Color);
        Assert.Equal(0, kingdom.Treasury.Gold);
        Assert.True(kingdom.IsLoss);
    }

    [Fact]
    public void InitForgetsWhatTheKingdomHad()
    {
        PlayerKingdom kingdom = new();

        kingdom.Init(PlayerColor.Red, GameDifficulty.Easy, isAiControlled: false);
        kingdom.AddCastle(Town(TownTileIndex, owner: PlayerColor.Red));
        kingdom.AddHero(new Hero(1, MapRace.Knight));

        kingdom.Init(PlayerColor.Green, GameDifficulty.Normal, isAiControlled: false);

        // clear() runs first (kingdom.cpp:105), so the rosters and the treasury both start over.
        Assert.Equal(PlayerColor.Green, kingdom.Color);
        Assert.Empty(kingdom.Castles);
        Assert.Empty(kingdom.Heroes);
        Assert.Equal(7500, kingdom.Treasury.Gold);
    }

    [Fact]
    public void AllowPaymentNeverChargesForNothing()
    {
        PlayerKingdom kingdom = new();

        kingdom.Init(PlayerColor.Blue, GameDifficulty.Impossible, isAiControlled: false);

        // The treasury is empty, and the reference's rule is "enough of it, or none of it asked": a price of zero
        // is always payable (kingdom.cpp:384-441).
        Assert.True(kingdom.AllowPayment(new Funds()));
        Assert.True(kingdom.AllowPayment(new Funds { Wood = 0, Gold = 0 }));

        Assert.False(kingdom.AllowPayment(new Funds { Gold = 1 }));
        Assert.False(kingdom.AllowPayment(new Funds { Gems = 1 }));
    }

    [Fact]
    public void PayingTooMuchLeavesTheTreasuryEmpty()
    {
        PlayerKingdom kingdom = new();

        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        kingdom.OddFundsResource(new Funds { Gold = 10000, Wood = 100 });

        Assert.Equal(0, kingdom.Treasury.Gold);
        Assert.Equal(0, kingdom.Treasury.Wood);

        // The other resources are untouched.
        Assert.Equal(5, kingdom.Treasury.Mercury);
    }

    [Fact]
    public void AddingNegativeIncomeCannotGoBelowZero()
    {
        PlayerKingdom kingdom = new();

        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        kingdom.AddFundsResource(new Funds { Gold = -100000, Ore = -50 });

        Assert.Equal(0, kingdom.Treasury.Gold);
        Assert.Equal(0, kingdom.Treasury.Ore);
        Assert.Equal(20, kingdom.Treasury.Wood);
    }

    [Fact]
    public void TheTreasuryIsALiveReference()
    {
        PlayerKingdom kingdom = new();

        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        // The castle commands take `ref Funds treasury` (ADR-107/108), so the property has to hand out the field
        // itself rather than a copy.
        ref Funds treasury = ref kingdom.Treasury;
        treasury.Gold = 10;

        Assert.Equal(10, kingdom.Treasury.Gold);

        kingdom.AddFundsResource(new Funds { Gold = 5 });

        Assert.Equal(15, kingdom.Treasury.Gold);
    }

    [Fact]
    public void ATownJoinsTheRosterOnlyOnce()
    {
        PlayerKingdom kingdom = new();

        CastleTown town = Town(TownTileIndex, owner: PlayerColor.Blue);

        kingdom.AddCastle(town);
        kingdom.AddCastle(town);

        Assert.Same(town, Assert.Single(kingdom.Castles));

        kingdom.RemoveCastle(town);
        kingdom.RemoveCastle(town);

        Assert.Empty(kingdom.Castles);
    }

    [Fact]
    public void AHeroJoinsTheRosterOnlyOnce()
    {
        PlayerKingdom kingdom = new();

        Hero hero = new(1, MapRace.Knight);

        kingdom.AddHero(hero);
        kingdom.AddHero(hero);

        Assert.Same(hero, Assert.Single(kingdom.Heroes));
        Assert.True(kingdom.HasHeroes);

        kingdom.RemoveHero(hero);

        Assert.Empty(kingdom.Heroes);
        Assert.False(kingdom.HasHeroes);
    }

    [Fact]
    public void AKingdomWithHeroesButNoTownIsLosingButNotLost()
    {
        PlayerKingdom kingdom = new();

        kingdom.AddHero(new Hero(1, MapRace.Knight));

        // isLosingGame() (kingdom.h:147) only asks for towns, isLoss() (kingdom.h:98) also asks for heroes.
        Assert.True(kingdom.IsLosingGame);
        Assert.False(kingdom.IsLoss);

        kingdom.RemoveHero(kingdom.Heroes[0]);

        Assert.True(kingdom.IsLoss);
    }

    [Fact]
    public void OnlyAFortOrTheRoomForOneRecruitsHeroes()
    {
        PlayerKingdom kingdom = new();

        CastleTown fortified = Town(TownTileIndex, owner: PlayerColor.Blue, hasCastle: true);
        kingdom.AddCastle(fortified);

        Assert.True(kingdom.CanRecruitHeroes);

        kingdom.RemoveCastle(fortified);

        // A town without its fort still recruits, because the fort can be built (kingdom.h:108).
        CastleTown town = Town(TownTileIndex, owner: PlayerColor.Blue, hasCastle: false);
        kingdom.AddCastle(town);

        Assert.True(kingdom.CanRecruitHeroes);

        // Unless the map forbids it, in which case there is nowhere to hire.
        CastleTown blocked = Town(TownTileIndex, owner: PlayerColor.Blue, hasCastle: false, disableCastle: true);
        kingdom.RemoveCastle(town);
        kingdom.AddCastle(blocked);

        Assert.False(kingdom.CanRecruitHeroes);
    }

    [Fact]
    public void CountsFollowTheBuildings()
    {
        PlayerKingdom kingdom = new();

        kingdom.AddCastle(Town(5, owner: PlayerColor.Blue, hasCastle: true, common: 0x0002 | 0x0400));
        kingdom.AddCastle(Town(TownTileIndex, owner: PlayerColor.Blue, hasCastle: false));

        Assert.Equal(1, kingdom.CountCastles());
        Assert.Equal(1, kingdom.CountTowns());

        // 0x0002 is the thieves' guild and 0x0400 the marketplace (castle.cpp:150-160).
        Assert.Equal(1, kingdom.CountThievesGuilds());
        Assert.Equal(1, kingdom.CountMarketplaces());
        Assert.Equal(1, kingdom.CountBuildings(BuildingType.Marketplace));

        // A combination mask only counts towns that own every bit of it (castle.h:174), so the second town — with
        // no marketplace and no guild — does not answer for the pair either.
        Assert.Equal(1, kingdom.CountBuildings(BuildingType.Marketplace | BuildingType.ThievesGuild));
        Assert.Equal(0, kingdom.CountBuildings(BuildingType.Marketplace | BuildingType.Tent));

        // No necromancer among the two, hence no shrine.
        Assert.Equal(0, kingdom.CountNecromancyShrines());
    }

    [Fact]
    public void KingdomsOfUnknownColorsShareTheLastSlot()
    {
        Kingdoms kingdoms = new();

        Assert.Same(kingdoms.GetKingdom(PlayerColor.Blue), kingdoms.GetKingdom(PlayerColor.Blue));
        Assert.NotSame(kingdoms.GetKingdom(PlayerColor.Blue), kingdoms.GetKingdom(PlayerColor.Red));

        // The reference's seventh slot holds every colour it cannot place (kingdom.cpp:710-730).
        Assert.Same(kingdoms.GetKingdom(PlayerColor.None), kingdoms.GetKingdom(PlayerColor.Blue | PlayerColor.Red));

        Assert.NotSame(kingdoms.GetKingdom(PlayerColor.None), kingdoms.GetKingdom(PlayerColor.Purple));
    }

    [Fact]
    public void InitOnlyStartsTheColorsInPlay()
    {
        Kingdoms kingdoms = new();

        // Expert splits the two tables widest: the player gets 2500 gold, the computer 10000.
        kingdoms.Init(PlayerColor.Blue | PlayerColor.Red, GameDifficulty.Expert, PlayerColor.Red);

        Assert.Equal(PlayerColor.Blue, kingdoms.GetKingdom(PlayerColor.Blue).Color);
        Assert.Equal(2500, kingdoms.GetKingdom(PlayerColor.Blue).Treasury.Gold);

        Assert.Equal(PlayerColor.Red, kingdoms.GetKingdom(PlayerColor.Red).Color);
        Assert.Equal(10000, kingdoms.GetKingdom(PlayerColor.Red).Treasury.Gold);

        // Green is not in play, so it stays cleared (kingdom.cpp:688-695).
        Assert.Equal(PlayerColor.None, kingdoms.GetKingdom(PlayerColor.Green).Color);
        Assert.Equal(0, kingdoms.GetKingdom(PlayerColor.Green).Treasury.Gold);
    }

    [Fact]
    public void RostersSkipNeutralTowns()
    {
        Kingdoms kingdoms = new();

        kingdoms.Init(PlayerColor.Blue | PlayerColor.Red, GameDifficulty.Normal, PlayerColor.None);

        AllCastles castles = new(MapSize);

        CastleTown blue = Town(TownTileIndex, owner: PlayerColor.Blue);
        CastleTown neutral = Town(5, owner: PlayerColor.None);

        castles.Add(blue);
        castles.Add(neutral);

        kingdoms.AddCastles(castles);

        Assert.Same(blue, Assert.Single(kingdoms.GetKingdom(PlayerColor.Blue).Castles));
        Assert.Empty(kingdoms.GetKingdom(PlayerColor.Red).Castles);

        // The neutral town has no colour, and the seventh slot is not a kingdom anyone owns (kingdom.cpp:802).
        Assert.Empty(kingdoms.GetKingdom(PlayerColor.None).Castles);
    }

    [Fact]
    public void RostersSkipNeutralHeroes()
    {
        Kingdoms kingdoms = new();

        kingdoms.Init(PlayerColor.Blue | PlayerColor.Red, GameDifficulty.Normal, PlayerColor.None);

        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        Hero? hero = heroes.Get(1);

        Assert.NotNull(hero);

        hero.SetColor(PlayerColor.Red);

        kingdoms.AddHeroes(heroes);

        Assert.Same(hero, Assert.Single(kingdoms.GetKingdom(PlayerColor.Red).Heroes));
        Assert.Empty(kingdoms.GetKingdom(PlayerColor.Blue).Heroes);
        Assert.Empty(kingdoms.GetKingdom(PlayerColor.None).Heroes);
    }

    [Fact]
    public void GetNotLossColorsLeavesOutTheLost()
    {
        Kingdoms kingdoms = new();

        kingdoms.Init(PlayerColor.Blue | PlayerColor.Red | PlayerColor.Green, GameDifficulty.Normal, PlayerColor.None);

        kingdoms.GetKingdom(PlayerColor.Blue).AddHero(new Hero(1, MapRace.Knight));
        kingdoms.GetKingdom(PlayerColor.Red).AddCastle(Town(TownTileIndex, owner: PlayerColor.Red));

        // Green has nothing at all, so it is out; the seventh slot never reports.
        Assert.Equal(PlayerColor.Blue | PlayerColor.Red, kingdoms.GetNotLossColors());
    }

    [Fact]
    public void ClearForgetsEveryKingdom()
    {
        Kingdoms kingdoms = new();

        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Easy, PlayerColor.None);
        kingdoms.GetKingdom(PlayerColor.Blue).AddHero(new Hero(1, MapRace.Knight));

        kingdoms.Clear();

        Assert.Equal(PlayerColor.None, kingdoms.GetKingdom(PlayerColor.Blue).Color);
        Assert.Empty(kingdoms.GetKingdom(PlayerColor.Blue).Heroes);
        Assert.Equal(0, kingdoms.GetKingdom(PlayerColor.Blue).Treasury.Gold);
    }

    [Fact]
    public void LoadingAMapFillsTheKingdomOfItsColors()
    {
        Mp2Map map = MapWithTown(TownBlock(owner: 0));

        World world = new();

        world.LoadMap(map);

        CastleTown town = Assert.Single(world.Castles);
        PlayerKingdom blue = world.Kingdoms.GetKingdom(PlayerColor.Blue);

        // world_loadmap.cpp:1443-1445: the town of the map joins the kingdom of its colour.
        Assert.Same(town, Assert.Single(blue.Castles));
        Assert.Equal(PlayerColor.Blue, blue.Color);

        // The header says NORMAL and does not mark blue as the computer, so it is the player's row.
        Assert.Equal(7500, blue.Treasury.Gold);

        // The other colours are not in play and own nothing.
        Assert.Empty(world.Kingdoms.GetKingdom(PlayerColor.Red).Castles);
    }

    [Fact]
    public void AMapWithoutColorsStartsNoKingdom()
    {
        Mp2Map map = MapWithTown(TownBlock(owner: 0));

        Mp2Map anonymous = map with { Info = map.Info with { KingdomColors = PlayerColor.None } };

        World world = new();

        world.LoadMap(anonymous);

        PlayerKingdom blue = world.Kingdoms.GetKingdom(PlayerColor.Blue);

        // No colour is in play, so Init starts nothing and blue keeps the cleared state: no colour, no treasury.
        Assert.Equal(PlayerColor.None, blue.Color);
        Assert.Equal(0, blue.Treasury.Gold);

        // The roster still receives the town, because AddCastles does not ask whether the kingdom was started —
        // the reference keys the roster by the town's colour and nothing else (kingdom.cpp:802).
        Assert.Same(Assert.Single(world.Castles), Assert.Single(blue.Castles));
    }

    /// <summary>A town of the tests, built from a 70 byte block.</summary>
    /// <param name="tileIndex">Tile index of its entrance.</param>
    /// <param name="owner">The colour of its owner.</param>
    /// <param name="hasCastle">Whether it has its fort.</param>
    /// <param name="common">The common building bits.</param>
    /// <param name="disableCastle">Whether the map forbids building the fort.</param>
    /// <returns>The town.</returns>
    private static CastleTown Town(
        int tileIndex,
        PlayerColor owner,
        bool hasCastle = true,
        ushort common = 0,
        bool disableCastle = false)
    {
        byte[] block = TownBlock(
            owner: OwnerByte(owner),
            hasCastle: hasCastle,
            common: common);

        if (disableCastle)
        {
            block[40] = 1;
        }

        CastleTown town = new(tileIndex, MapRace.Knight);

        town.LoadFromMP2(block, GameDifficulty.Normal);

        return town;
    }

    /// <summary>The owner byte of a town block, <c>MP2::ColorToIndex()</c>: blue is zero, purple five.</summary>
    /// <param name="color">The colour of the owner.</param>
    /// <returns>The byte, or 255 for a town nobody owns.</returns>
    private static int OwnerByte(PlayerColor color) => color switch
    {
        PlayerColor.Blue => 0,
        PlayerColor.Green => 1,
        PlayerColor.Red => 2,
        PlayerColor.Yellow => 3,
        PlayerColor.Orange => 4,
        PlayerColor.Purple => 5,
        _ => 255,
    };

    /// <summary>A 36 by 36 grass map with one town on <see cref="TownTileIndex"/>.</summary>
    /// <param name="block">The town's information block.</param>
    /// <returns>The map.</returns>
    private static Mp2Map MapWithTown(byte[] block)
    {
        Mp2Map map = Mp2Map.CreateBlank(MapSize, MapGrounds.GrassStartImageIndex);

        map.SetTileAt(TownTileIndex, TownTile(Mp2ObjectType.Castle, blockId: 1));

        return map with
        {
            Info = map.Info with { KingdomColors = PlayerColor.Blue, ComputerPlayers = PlayerColor.None },
            Castles = [new Mp2CastlePosition(TownX, TownY, 0, MapRace.Knight)],
            InfoBlocks = [new Mp2InfoBlock(0, block)],
        };
    }

    /// <summary>A town tile of the map file: the object type of the entrance and the block it refers to.</summary>
    /// <param name="type">The object type of the tile.</param>
    /// <param name="blockId">The information block the tile refers to.</param>
    /// <param name="imageIndex">Sprite index inside <c>OBJNTWRD</c>.</param>
    /// <returns>The tile.</returns>
    private static Mp2TileInfo TownTile(Mp2ObjectType type, int blockId = 1, int imageIndex = 0) => new()
    {
        TerrainImageIndex = MapGrounds.GrassStartImageIndex,
        ObjectName1 = (byte)((int)Mp2ObjectIcnType.ObjnTwrd << 2),
        BottomIcnImageIndex = (byte)imageIndex,
        // Object layer in the low two bits, and the block id in the 13 bit quantity word.
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

    /// <summary>Builds a 70 byte town block, <see cref="CastleTown.Mp2BlockSize"/> bytes of <c>castle.cpp:102-395</c>.</summary>
    /// <param name="owner">The owner byte: 0 to 5 are blue to purple, anything else is "nobody".</param>
    /// <param name="faction">The race byte.</param>
    /// <param name="hasCastle">Whether the town has its fort.</param>
    /// <param name="common">The common building bits.</param>
    /// <returns>The block.</returns>
    private static byte[] TownBlock(int owner = 255, int faction = 0, bool hasCastle = true, ushort common = 0)
    {
        byte[] data = new byte[CastleTown.Mp2BlockSize];

        data[0] = (byte)owner;
        data[1] = 1; // Custom buildings, so the block says what it means.

        data[2] = (byte)common;
        data[3] = (byte)(common >> 8);

        data[38] = (byte)faction;
        data[39] = (byte)(hasCastle ? 1 : 0);

        return data;
    }
}
