using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;
using KingdomState = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The shipyard: the three conditions of <c>AllowBuyBoat</c>, where the boat lands, and what stops a second one.
/// </summary>
public sealed class CastleBoatsTests
{
    /// <summary>The town's tile on the ten by ten maps below: (4, 4).</summary>
    private const int TownTile = 44;

    /// <summary>The middle tile of the harbour row, two rows below the town.</summary>
    private const int HarbourTile = 64;

    [Fact]
    public void ABoatNeedsAShipyard()
    {
        World world = SquareWorld();
        KingdomState kingdom = RichKingdom();
        CastleTown castle = Town();

        // No shipyard yet, so not even a rich owner can launch a boat.
        Assert.False(castle.AllowBuyBoat(world, kingdom));

        castle.AddBuildings(BuildingType.Shipyard);

        Assert.True(castle.AllowBuyBoat(world, kingdom));
    }

    [Fact]
    public void ABoatNeedsMoney()
    {
        World world = SquareWorld();
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Red, GameDifficulty.Normal, isAiControlled: false);
        kingdom.Treasury = new Funds(); // an empty purse

        CastleTown castle = Town();
        castle.AddBuildings(BuildingType.Shipyard);

        Assert.False(castle.AllowBuyBoat(world, kingdom));

        // The AI asks with the price left out: it has already decided how to raise the funds.
        Assert.True(castle.AllowBuyBoat(world, kingdom, checkPayment: false));
    }

    [Fact]
    public void ABoatNeedsWater()
    {
        World world = SquareWorld();
        KingdomState kingdom = RichKingdom();
        CastleTown castle = Town();
        castle.AddBuildings(BuildingType.Shipyard);

        // The harbour row is a meadow, so there is nowhere to float a boat.
        for (int index = HarbourTile - 1; index <= HarbourTile + 1; ++index)
        {
            world.GetTile(index).SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        Assert.Equal(-1, castle.GetTileIndexToPlaceBoat(world));
        Assert.False(castle.AllowBuyBoat(world, kingdom));
        Assert.False(castle.BuyBoat(world, kingdom));
        Assert.Equal(-1, castle.GetTileIndexToPlaceBoat(world));
    }

    [Fact]
    public void TheBoatLandsOnTheHarbourTileBelowTheTown()
    {
        World world = SquareWorld();
        CastleTown castle = Town();

        Assert.Equal(HarbourTile, castle.GetTileIndexToPlaceBoat(world));
    }

    [Fact]
    public void TheBoatIsPaidForOnceAndMarkedWithTheOwnersColour()
    {
        World world = SquareWorld();
        KingdomState kingdom = RichKingdom();
        CastleTown castle = Town();
        castle.AddBuildings(BuildingType.Shipyard);

        int goldBefore = kingdom.Treasury.Gold;
        int woodBefore = kingdom.Treasury.Wood;
        Funds price = PaymentConditions.BuyBoat();

        Assert.True(castle.BuyBoat(world, kingdom));

        Assert.Equal(goldBefore - price.Gold, kingdom.Treasury.Gold);
        Assert.Equal(woodBefore - price.Wood, kingdom.Treasury.Wood);

        Tile tile = world.GetTile(HarbourTile);

        Assert.Equal(Mp2ObjectType.ActionBoat, tile.MainObjectType);
        Assert.Equal(kingdom.Color, tile.BoatOwnerColor);

        // The harbour is taken now: no second boat on top of the first.
        Assert.True(castle.HasBoatNearby(world));
        Assert.False(castle.AllowBuyBoat(world, kingdom));
    }

    [Fact]
    public void AHeroSailingInTheHarbourCountsAsABoat()
    {
        World world = SquareWorld();
        CastleTown castle = Town();

        // A hero standing on water is in a boat; the reference treats the two alike.
        world.GetTile(HarbourTile).SetMainObjectType(Mp2ObjectType.ActionHero);

        Assert.True(castle.HasBoatNearby(world));
    }

    [Fact]
    public void AHarbourOffTheMapIsNoHarbour()
    {
        World world = SquareWorld();

        // The town two rows from the bottom edge has no harbour row below it at all.
        CastleTown castle = new(88, MapRace.Knight) { HasSeaAccess = true };

        Assert.False(castle.HasBoatNearby(world));
        Assert.Equal(-1, castle.GetTileIndexToPlaceBoat(world));
    }

    [Fact]
    public void EveryRaceHasItsOwnBoat()
    {
        IcnType knight = CastleTown.GetICNBoat(MapRace.Knight);

        Assert.NotEqual(IcnType.Unknown, knight);

        foreach (MapRace race in (MapRace[])[MapRace.Barbarian, MapRace.Necromancer, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard])
        {
            IcnType icn = CastleTown.GetICNBoat(race);

            Assert.NotEqual(IcnType.Unknown, icn);
            Assert.NotEqual(knight, icn);
        }

        Assert.Equal(IcnType.Unknown, CastleTown.GetICNBoat(MapRace.None));
    }

    private static World SquareWorld()
    {
        World world = new();
        world.GenerateUninitializedMap(10);

        return world;
    }

    private static KingdomState RichKingdom()
    {
        KingdomState kingdom = new();
        kingdom.Init(PlayerColor.Red, GameDifficulty.Normal, isAiControlled: false);
        kingdom.Treasury = new Funds { Gold = 100_000, Wood = 1000 };

        return kingdom;
    }

    private static CastleTown Town() => new(TownTile, MapRace.Knight) { HasSeaAccess = true };
}
