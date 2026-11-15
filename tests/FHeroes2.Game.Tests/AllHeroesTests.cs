using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="AllHeroes"/> (<c>heroes.cpp:2265</c>), the id → race table of
/// <see cref="HeroRaces"/> (<c>getHeroIdRangeForRace</c>, <c>heroes.cpp:38</c>) and the recruitment pool
/// <c>AllHeroes::GetHeroForHire()</c> (<c>heroes.cpp:2322</c>).
/// </summary>
public sealed class AllHeroesTests
{
    [Fact]
    public void InitOfTheSuccessionWarsFillsTheSixtyOriginalHeroes()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        // 54 heroes of the six races plus the 6 campaign heroes.
        Assert.Equal(60, heroes.Count);
        Assert.NotNull(heroes.Get(Hero.LordKilburn));
        Assert.NotNull(heroes.Get(Hero.Celia));
        Assert.NotNull(heroes.Get(Hero.Roland));
        Assert.NotNull(heroes.Get(Hero.Brax));

        // The Price of Loyalty heroes exist only for expansion maps, the debug hero only in dev builds
        // (heroes.cpp:2291-2314): the reference puts UNKNOWN placeholders there, the port leaves the slot
        // empty.
        Assert.Null(heroes.Get(Hero.Solmyr));
        Assert.Null(heroes.Get(Hero.Jarkonas));
        Assert.Null(heroes.Get(Hero.DebugHero));
        Assert.Null(heroes.Get(Hero.UnknownId));
        Assert.Null(heroes.Get(Hero.IdCount));
    }

    [Fact]
    public void InitOfTheExpansionAddsThePriceOfLoyaltyHeroes()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.PriceOfLoyalty);

        Assert.Equal(71, heroes.Count);
        Assert.Equal(MapRace.Wizard, heroes.Get(Hero.Solmyr)!.Race);
        Assert.Equal(MapRace.Barbarian, heroes.Get(Hero.Jarkonas)!.Race);
        Assert.Null(heroes.Get(Hero.DebugHero));

        AllHeroes resurrection = new();
        resurrection.Init(MapVersion.Resurrection);
        Assert.Equal(71, resurrection.Count);
    }

    [Fact]
    public void TheRaceRangesMatchTheReference()
    {
        // getHeroIdRangeForRace() (heroes.cpp:38): nine heroes per race, and Race::NONE spans them all.
        Assert.Equal((1, 9), HeroRaces.GetHeroIdRange(MapRace.Knight));
        Assert.Equal((10, 18), HeroRaces.GetHeroIdRange(MapRace.Barbarian));
        Assert.Equal((19, 27), HeroRaces.GetHeroIdRange(MapRace.Sorceress));
        Assert.Equal((28, 36), HeroRaces.GetHeroIdRange(MapRace.Warlock));
        Assert.Equal((37, 45), HeroRaces.GetHeroIdRange(MapRace.Wizard));
        Assert.Equal((46, 54), HeroRaces.GetHeroIdRange(MapRace.Necromancer));
        Assert.Equal((1, 54), HeroRaces.GetHeroIdRange(MapRace.None));
        Assert.Equal((1, 54), HeroRaces.GetHeroIdRange(MapRace.Random));
    }

    [Fact]
    public void TheRacesMatchTheReference()
    {
        Assert.Equal(MapRace.Knight, HeroRaces.GetRace(Hero.LordKilburn));
        Assert.Equal(MapRace.Knight, HeroRaces.GetRace(9));
        Assert.Equal(MapRace.Barbarian, HeroRaces.GetRace(10));
        Assert.Equal(MapRace.Necromancer, HeroRaces.GetRace(Hero.Celia));

        // The per-hero races of AllHeroes::Init(), which the ranges do not cover.
        Assert.Equal(MapRace.Wizard, HeroRaces.GetRace(Hero.Roland));
        Assert.Equal(MapRace.Knight, HeroRaces.GetRace(Hero.Corlagon));
        Assert.Equal(MapRace.Necromancer, HeroRaces.GetRace(Hero.Brax));
        Assert.Equal(MapRace.Sorceress, HeroRaces.GetRace(Hero.Martine));
        Assert.Equal(MapRace.Warlock, HeroRaces.GetRace(Hero.DebugHero));

        Assert.Equal(MapRace.None, HeroRaces.GetRace(Hero.UnknownId));
        Assert.Equal(MapRace.None, HeroRaces.GetRace(Hero.IdCount));
    }

    [Fact]
    public void EveryHeroCarriesTheRaceOfTheTable()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.PriceOfLoyalty);

        foreach (Hero hero in heroes)
        {
            Assert.Equal(HeroRaces.GetRace(hero.Id), hero.Race);
            Assert.Equal(hero.Id, hero.PortraitId);
            Assert.Equal(PlayerColor.None, hero.Color);
            Assert.True(hero.IsAvailableForHire);
        }
    }

    [Fact]
    public void HeroesAreIteratedInIdOrder()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        int previous = Hero.UnknownId;

        foreach (Hero hero in heroes)
        {
            Assert.True(hero.Id > previous, $"{hero.Id} came after {previous}");
            previous = hero.Id;
        }

        Assert.Equal(Hero.Brax, previous);
    }

    [Fact]
    public void ClearEmptiesTheTable()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        heroes.Clear();

        Assert.Empty(heroes);
        Assert.Null(heroes.Get(Hero.LordKilburn));
        Assert.Null(heroes.GetHeroForHire(MapRace.None));
    }

    [Fact]
    public void GetHeroForHireOnlyOffersRegularHeroesOfTheRequestedRace()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.PriceOfLoyalty);

        for (int i = 0; i < 200; ++i)
        {
            Hero knight = heroes.GetHeroForHire(MapRace.Knight)!;

            Assert.Equal(MapRace.Knight, knight.Race);
            Assert.InRange(knight.Id, 1, 9);
        }

        for (int i = 0; i < 200; ++i)
        {
            // Any race still stays inside the regular heroes: campaign and The Price of Loyalty heroes are
            // never offered (heroes.cpp:2349).
            Hero anyone = heroes.GetHeroForHire(MapRace.None)!;

            Assert.InRange(anyone.Id, Hero.LordKilburn, HeroRaces.MaxRegularHeroId);
        }
    }

    [Fact]
    public void GetHeroForHireFallsBackWhenTheRaceHasNobodyLeft()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        for (int heroId = 1; heroId <= 9; ++heroId)
        {
            heroes.Get(heroId)!.SetColor(PlayerColor.Blue);
        }

        // Every knight is hired, so the second pass takes any available hero (heroes.cpp:2374).
        Hero fallback = heroes.GetHeroForHire(MapRace.Knight)!;

        Assert.NotEqual(MapRace.Knight, fallback.Race);
        Assert.True(fallback.IsAvailableForHire);
    }

    [Fact]
    public void GetHeroForHireLeavesOutHiredAndImprisonedHeroes()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        for (int heroId = 1; heroId <= 9; ++heroId)
        {
            heroes.Get(heroId)!.SetModes(HeroModes.Jail);
        }

        Assert.Equal(MapRace.Barbarian, heroes.GetHeroForHire(MapRace.Barbarian)!.Race);

        // A jailed hero is not available for hire, so a knight request falls through to another race.
        Hero other = heroes.GetHeroForHire(MapRace.Knight)!;
        Assert.False(other.Modes.HasFlag(HeroModes.Jail));
        Assert.NotEqual(MapRace.Knight, other.Race);

        foreach (Hero hero in heroes)
        {
            hero.SetColor(PlayerColor.Red);
        }

        Assert.Null(heroes.GetHeroForHire(MapRace.None));
    }

    [Fact]
    public void GetHeroForHireHonoursTheIgnoredHero()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        for (int heroId = 1; heroId <= 9; ++heroId)
        {
            if (heroId != 3 && heroId != 4)
            {
                heroes.Get(heroId)!.SetColor(PlayerColor.Green);
            }
        }

        // The tavern that already offers 3 must get 4 and nobody else.
        for (int i = 0; i < 50; ++i)
        {
            Assert.Equal(4, heroes.GetHeroForHire(MapRace.Knight, heroIdToIgnore: 3)!.Id);
        }
    }

    [Fact]
    public void GetHeroForHirePrefersHeroesNobodyOffersYet()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);

        // Every knight but the last one is already advertised by some kingdom.
        for (int heroId = 1; heroId <= 8; ++heroId)
        {
            heroes.Get(heroId)!.SetModes(HeroModes.Recruit);
        }

        for (int i = 0; i < 50; ++i)
        {
            Assert.Equal(9, heroes.GetHeroForHire(MapRace.Knight)!.Id);
        }

        // With every candidate reserved the reference rather shares a hero than returns nothing.
        heroes.Get(9)!.SetModes(HeroModes.Recruit);
        Assert.NotNull(heroes.GetHeroForHire(MapRace.Knight));
    }

    [Fact]
    public void IsAvailableForHireFollowsTheColorAndTheJail()
    {
        Hero hero = new(Hero.LordKilburn, MapRace.Knight);

        Assert.True(hero.IsAvailableForHire);

        hero.SetColor(PlayerColor.Blue);
        Assert.False(hero.IsAvailableForHire);

        hero.SetColor(PlayerColor.None);
        Assert.True(hero.IsAvailableForHire);

        hero.SetModes(HeroModes.Jail);
        Assert.False(hero.IsAvailableForHire);

        // A jailed hero without a color is not offered; clearing the bit brings him back.
        hero.ResetModes(HeroModes.Jail);
        Assert.True(hero.IsAvailableForHire);
    }
}
