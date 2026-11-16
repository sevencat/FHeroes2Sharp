using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the fog of war as world state (<c>Maps::Tile::_fogColors</c>, <c>Maps::ClearFog</c>) and the two
/// objects that were waiting for it: the observation tower (<c>heroes_action.cpp:2521</c>) and Magellan's maps
/// (<c>heroes_action.cpp:2759</c>).
/// </summary>
public sealed class HeroFogTests
{
    private const int MapSize = 41;
    private const int CenterColumn = 20;
    private const int CenterRow = 20;
    private const int CenterTile = (CenterRow * MapSize) + CenterColumn;

    private const int CaptainTile = 0;
    private const int SeaColumn = 1;
    private const int SeaRow = 0;

    private static World FogWorld(int size = MapSize)
    {
        World world = new();
        world.GenerateUninitializedMap(size);
        return world;
    }

    private static HeroActions FogActions(out PlayerKingdom kingdom, out World world, out Hero hero)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
        hero = new Hero(1, MapRace.Knight);
        kingdom.AddHero(hero);

        world = FogWorld();
        Tile tile = world.GetTile(CenterTile);
        tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        tile.SetMainObjectType(Mp2ObjectType.ObservationTower);

        return new HeroActions(hero, kingdoms);
    }

    private static HeroActions BuyActions(
        out PlayerKingdom kingdom,
        out World world,
        int gold,
        out int startingGold)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
        Hero hero = new(1, MapRace.Knight);
        kingdom.AddHero(hero);

        kingdom.Treasury = new Funds(0, 0, 0, 0, 0, 0, gold);
        startingGold = kingdom.Treasury.Gold;

        world = FogWorld(4);
        FillGrass(world);
        world.GetTile((SeaRow * 4) + SeaColumn).SetTerrain(MapGrounds.WaterStartImageIndex, 0);
        world.GetTile(CaptainTile).SetMainObjectType(Mp2ObjectType.MagellansMaps);

        return new HeroActions(hero, kingdoms);
    }

    private static void FillGrass(World world)
    {
        foreach (Tile tile in world.Tiles)
        {
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }
    }

    [Fact]
    public void TheFogAppearsWithTheMapAndLeavesWithIt()
    {
        World world = new();

        Assert.Null(world.Fog);

        world.GenerateUninitializedMap(2);

        Assert.NotNull(world.Fog);
        Assert.True(world.Fog!.IsFogged(0, 0, PlayerColor.Blue));

        world.Reset();

        Assert.Null(world.Fog);
    }

    [Fact]
    public void TheClearFogDiscHasTheRadiusOfTheReference()
    {
        World world = FogWorld();

        int expected = 0;
        for (int row = 0; row < MapSize; ++row)
        {
            for (int column = 0; column < MapSize; ++column)
            {
                int dx = column - CenterColumn;
                int dy = row - CenterRow;

                // Nineteen tiles of radius mean a plain square as the limit, see MapFog.SquaredRevealLimit.
                if ((dx * dx) + (dy * dy) < 400)
                {
                    ++expected;
                }
            }
        }

        Assert.Equal(expected, world.ClearFog(CenterTile, MapFog.ObservationTowerRevealDistance, PlayerColor.Blue));
    }

    [Fact]
    public void ClearFogUncoversOnlyTheColorsItWasGiven()
    {
        World world = FogWorld();

        world.ClearFog(CenterTile, MapFog.HeroRevealDistance, PlayerColor.Blue);

        MapFog fog = world.Fog!;

        Assert.False(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Blue));
        Assert.True(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Red));

        // The corner is far outside the disc, so it stays fogged for everyone.
        Assert.True(fog.IsFogged(0, 0, PlayerColor.Blue));
    }

    [Fact]
    public void ClearFogReportsNothingTheSecondTime()
    {
        World world = FogWorld();

        Assert.True(world.ClearFog(CenterTile, MapFog.HeroRevealDistance, PlayerColor.Blue) > 0);
        Assert.Equal(0, world.ClearFog(CenterTile, MapFog.HeroRevealDistance, PlayerColor.Blue));
    }

    [Fact]
    public void ClearFogIgnoresAnEmptyRadiusAndAnImpossibleTile()
    {
        World world = FogWorld();

        Assert.Equal(0, world.ClearFog(CenterTile, 0, PlayerColor.Blue));
        Assert.Equal(0, world.ClearFog(CenterTile, -3, PlayerColor.Blue));
        Assert.Equal(0, world.ClearFog(-1, MapFog.HeroRevealDistance, PlayerColor.Blue));
        Assert.Equal(0, world.ClearFog(MapSize * MapSize, MapFog.HeroRevealDistance, PlayerColor.Blue));

        // Nothing was touched: the center is still undiscovered.
        Assert.True(world.Fog!.IsFogged(CenterColumn, CenterRow, PlayerColor.Blue));
    }

    [Fact]
    public void TheObservationTowerRevealsAroundItselfForTheHerosColor()
    {
        HeroActions actions = FogActions(out _, out World world, out Hero hero);

        int revealed = actions.ActionToObservationTower(CenterTile, world);

        Assert.True(revealed > 0);
        Assert.False(world.Fog!.IsFogged(CenterColumn, CenterRow, hero.Color));
        Assert.True(world.Fog!.IsFogged(CenterColumn, CenterRow, PlayerColor.Red));
        Assert.True(world.Fog!.IsFogged(0, 0, hero.Color));
    }

    [Fact]
    public void TheTowerCanBeClimbedTwice()
    {
        HeroActions actions = FogActions(out _, out World world, out _);

        Assert.True(actions.ActionToObservationTower(CenterTile, world) > 0);
        Assert.Equal(0, actions.ActionToObservationTower(CenterTile, world));
    }

    [Fact]
    public void MagellansMapsRevealTheSeaAndOnlyTheSea()
    {
        const int firstSeaTile = 1;
        const int secondSeaTile = (3 * MapSize) + 2;

        World world = FogWorld();
        FillGrass(world);
        world.GetTile(firstSeaTile).SetTerrain(MapGrounds.WaterStartImageIndex, 0);
        world.GetTile(secondSeaTile).SetTerrain(MapGrounds.WaterStartImageIndex, 0);

        Assert.Equal(2, world.ActionForMagellanMaps(PlayerColor.Blue));

        MapFog fog = world.Fog!;

        Assert.False(fog.IsFogged(1, 0, PlayerColor.Blue));
        Assert.False(fog.IsFogged(2, 3, PlayerColor.Blue));
        Assert.True(fog.IsFogged(CenterColumn, CenterRow, PlayerColor.Blue));
        Assert.True(fog.IsFogged(1, 0, PlayerColor.Red));
    }

    [Fact]
    public void TheCaptainAsksBeforeChargingAnything()
    {
        HeroActions actions = BuyActions(out PlayerKingdom kingdom, out World world, 5000, out int before);

        Assert.Equal(MagellansMapsOutcome.NeedsConfirmation, actions.ActionToMagellanMaps(CaptainTile, world));
        Assert.Equal(before, kingdom.Treasury.Gold);
        Assert.True(world.Fog!.IsFogged(SeaColumn, SeaRow, PlayerColor.Blue));
    }

    [Fact]
    public void BuyingTheMapsCostsAThousandGold()
    {
        HeroActions actions = BuyActions(out PlayerKingdom kingdom, out World world, 5000, out int before);

        Assert.Equal(MagellansMapsOutcome.Bought, actions.ActionToMagellanMaps(CaptainTile, world, confirmed: true));
        Assert.Equal(before - 1000, kingdom.Treasury.Gold);
        Assert.False(world.Fog!.IsFogged(SeaColumn, SeaRow, PlayerColor.Blue));
        Assert.True(kingdom.IsVisited(Mp2ObjectType.MagellansMaps));
    }

    [Fact]
    public void AKingdomThatBoughtTheMapsIsNotChargedAgain()
    {
        HeroActions actions = BuyActions(out PlayerKingdom kingdom, out World world, 5000, out _);

        actions.ActionToMagellanMaps(CaptainTile, world, confirmed: true);
        int afterFirst = kingdom.Treasury.Gold;

        Assert.Equal(
            MagellansMapsOutcome.AlreadyBought,
            actions.ActionToMagellanMaps(CaptainTile, world, confirmed: true));
        Assert.Equal(afterFirst, kingdom.Treasury.Gold);
    }

    [Fact]
    public void APoorKingdomCannotBuyTheMaps()
    {
        HeroActions actions = BuyActions(out PlayerKingdom kingdom, out World world, 999, out int before);

        Assert.Equal(
            MagellansMapsOutcome.NotEnoughGold,
            actions.ActionToMagellanMaps(CaptainTile, world, confirmed: true));
        Assert.Equal(before, kingdom.Treasury.Gold);
        Assert.True(world.Fog!.IsFogged(SeaColumn, SeaRow, PlayerColor.Blue));
    }

    [Fact]
    public void HavingTheMapsBeatsHavingNoMoney()
    {
        HeroActions actions = BuyActions(out PlayerKingdom kingdom, out World world, 1000, out _);

        Assert.Equal(MagellansMapsOutcome.Bought, actions.ActionToMagellanMaps(CaptainTile, world, confirmed: true));

        // The visit is asked about first: a kingdom that owns the maps is told so even when it is broke.
        Assert.Equal(0, kingdom.Treasury.Gold);
        Assert.Equal(MagellansMapsOutcome.AlreadyBought, actions.ActionToMagellanMaps(CaptainTile, world));
    }
}
