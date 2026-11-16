using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers breaking into a jail, <c>ActionToJail</c> (<c>heroes_action.cpp:3402</c>), the recruiting it ends in
/// (<c>heroes.cpp:1105</c>) and the seat test it starts with (<c>kingdom.cpp:499</c>).
/// </summary>
public sealed class HeroJailTests
{
    private const int JailTile = 0;

    private static HeroActions JailActions(
        out PlayerKingdom kingdom,
        out World world,
        out Hero prisoner,
        bool withPrisoner = true)
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        kingdom = kingdoms.GetKingdom(PlayerColor.Blue);

        Hero hero = new Hero(1, MapRace.Knight);
        kingdom.AddHero(hero);

        world = new World();
        world.GenerateUninitializedMap(2);
        world.Heroes.Init(MapVersion.PriceOfLoyalty);

        Tile tile = world.GetTile(JailTile);
        tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        tile.SetMainObjectType(Mp2ObjectType.Jail);

        prisoner = world.Heroes[Hero.LordKilburn]!;

        if (withPrisoner)
        {
            prisoner.SetModes(HeroModes.Jail);
            prisoner.SetMapIndex(JailTile);
        }

        return new HeroActions(hero, kingdoms);
    }

    private static void FillSeats(PlayerKingdom kingdom, Hero first, int extra)
    {
        for (int index = 0; index < extra; ++index)
        {
            // The ids must differ from the first hero's; AllHeroes allocates one hero per id.
            kingdom.AddHero(new Hero(2 + index, MapRace.Wizard));
        }

        Assert.Equal(PlayerKingdom.MaxHeroes, kingdom.Heroes.Count);
        Assert.Contains(first, kingdom.Heroes);
    }

    [Fact]
    public void AJailWithRoomReleasesThePrisoner()
    {
        HeroActions actions = JailActions(out PlayerKingdom kingdom, out World world, out Hero prisoner);

        JailOutcome outcome = actions.ActionToJail(JailTile, world);

        Assert.Equal(JailOutcome.Released, outcome);
        Assert.Equal(PlayerColor.Blue, prisoner.Color);
        Assert.False(prisoner.Modes.HasFlag(HeroModes.Jail));
        Assert.Contains(prisoner, kingdom.Heroes);
    }

    [Fact]
    public void TheFreedPrisonerStandsOnTheJailTile()
    {
        HeroActions actions = JailActions(out _, out World world, out Hero prisoner);

        actions.ActionToJail(JailTile, world);

        Assert.Equal(JailTile, prisoner.MapIndex);
        Assert.Equal((byte)prisoner.Id, world.GetTile(JailTile).OccupantHeroId);
        Assert.Same(prisoner, world.GetTile(JailTile).GetHero(world));
    }

    [Fact]
    public void TheFreedPrisonerIsNoLongerFoundByTheJailLookup()
    {
        HeroActions actions = JailActions(out _, out World world, out _);

        Assert.NotNull(world.FromJailHeroes(JailTile));

        actions.ActionToJail(JailTile, world);

        Assert.Null(world.FromJailHeroes(JailTile));
        Assert.Equal(JailOutcome.NoPrisoner, actions.ActionToJail(JailTile, world));
    }

    [Fact]
    public void AFreedHeroIsActive()
    {
        HeroActions actions = JailActions(out _, out World world, out Hero prisoner);

        Assert.False(prisoner.IsActive);

        actions.ActionToJail(JailTile, world);

        Assert.True(prisoner.IsActive);
    }

    [Fact]
    public void AFreedHeroTurnsToTheRight()
    {
        HeroActions actions = JailActions(out _, out World world, out Hero prisoner);

        actions.ActionToJail(JailTile, world);

        Assert.Equal(Direction.Right, prisoner.Direction);
    }

    [Fact]
    public void AFullKingdomLeavesThePrisonerInHisCell()
    {
        HeroActions actions = JailActions(out PlayerKingdom kingdom, out World world, out Hero prisoner, false);
        FillSeats(kingdom, kingdom.Heroes[0], PlayerKingdom.MaxHeroes - 1);

        prisoner.SetModes(HeroModes.Jail);
        prisoner.SetMapIndex(JailTile);

        JailOutcome outcome = actions.ActionToJail(JailTile, world);

        Assert.Equal(JailOutcome.NoSeat, outcome);
        Assert.True(prisoner.Modes.HasFlag(HeroModes.Jail));
        Assert.Equal(PlayerColor.None, prisoner.Color);
    }

    [Fact]
    public void AnEmptyJailHasNobodyToFree()
    {
        HeroActions actions = JailActions(out _, out World world, out _, false);

        Assert.Equal(JailOutcome.NoPrisoner, actions.ActionToJail(JailTile, world));
    }

    [Fact]
    public void AFullKingdomIsReportedBeforeAnEmptyCell()
    {
        HeroActions actions = JailActions(out PlayerKingdom kingdom, out World world, out _, false);
        FillSeats(kingdom, kingdom.Heroes[0], PlayerKingdom.MaxHeroes - 1);

        // The reference asks for a free seat before it even looks for a prisoner.
        Assert.Equal(JailOutcome.NoSeat, actions.ActionToJail(JailTile, world));
    }

    [Fact]
    public void AllowRecruitHeroCountsTheSeats()
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        PlayerKingdom kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
        Hero first = new Hero(1, MapRace.Knight);
        kingdom.AddHero(first);

        Assert.True(kingdom.AllowRecruitHero(false));

        FillSeats(kingdom, first, PlayerKingdom.MaxHeroes - 1);

        Assert.False(kingdom.AllowRecruitHero(false));
    }

    [Fact]
    public void AllowRecruitHeroChecksTheTavernPriceOnlyWhenAsked()
    {
        Kingdoms kingdoms = new();
        kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        PlayerKingdom kingdom = kingdoms.GetKingdom(PlayerColor.Blue);
        kingdom.AddHero(new Hero(1, MapRace.Knight));

        kingdom.Treasury.Gold = 0;

        // The jail path never asks for money; the tavern path does.
        Assert.True(kingdom.AllowRecruitHero(false));
        Assert.False(kingdom.AllowRecruitHero(true));

        kingdom.Treasury.Gold = PaymentConditions.RecruitHero().Gold;

        Assert.True(kingdom.AllowRecruitHero(true));
    }
}
