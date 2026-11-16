using FHeroes2.Assets.Maps;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the tavern pair, <see cref="Recruits"/> (<c>heroes_recruits.cpp</c>), and the kingdom that owns one
/// (<c>Kingdom::clear()</c>, <c>kingdom.cpp:132</c>).
/// </summary>
public sealed class RecruitsTests
{
    private static AllHeroes Heroes()
    {
        AllHeroes heroes = new();
        heroes.Init(MapVersion.SuccessionWars);
        return heroes;
    }

    [Fact]
    public void AFreshPairOffersNobody()
    {
        Recruits recruits = new();

        Assert.Equal(Hero.UnknownId, recruits.Id1);
        Assert.Equal(Hero.UnknownId, recruits.Id2);
        Assert.False(recruits.First.IsValid);
        Assert.False(recruits.Second.IsValid);
        Assert.Equal(0u, recruits.GetSurrenderDayOfHero1());
        Assert.Equal(0u, recruits.GetSurrenderDayOfHero2());

        AllHeroes heroes = Heroes();
        Assert.Null(recruits.GetHero1(heroes));
        Assert.Null(recruits.GetHero2(heroes));
    }

    [Fact]
    public void OfferingAHeroFillsTheSlotAndMarksTheHero()
    {
        AllHeroes heroes = Heroes();
        Hero hero = heroes.Get(Hero.LordKilburn)!;
        Recruits recruits = new();

        recruits.SetHero1(hero);

        Assert.Equal(hero.Id, recruits.Id1);
        Assert.Same(hero, recruits.GetHero1(heroes));
        Assert.True(hero.Modes.HasFlag(HeroModes.Recruit));

        // An offered hero has never surrendered, so the day stays 0.
        Assert.Equal(0u, recruits.GetSurrenderDayOfHero1());
    }

    [Fact]
    public void EmptyingASlotLeavesTheHeroesRecruitModeAlone()
    {
        // The reference's SetHero1( nullptr ) only clears the slot; the mode bit belongs to whoever drops the hero
        // from the offer (usually the hiring, which happens elsewhere).
        AllHeroes heroes = Heroes();
        Hero hero = heroes.Get(Hero.LordKilburn)!;
        Recruits recruits = new();
        recruits.SetHero1(hero);

        recruits.SetHero1(null);

        Assert.Equal(Hero.UnknownId, recruits.Id1);
        Assert.Null(recruits.GetHero1(heroes));
        Assert.True(hero.Modes.HasFlag(HeroModes.Recruit));
    }

    [Fact]
    public void ASurrenderedHeroReplacesTheOlderSlot()
    {
        // heroes_recruits.cpp:94-104: the newcomer goes into the slot whose surrender day is older, and when the two
        // days are equal (both fresh) the first slot is replaced — that is the reference's `>` on a tie being false.
        AllHeroes heroes = Heroes();
        Hero first = heroes.Get(Hero.LordKilburn)!;
        Hero second = heroes.Get(Hero.Celia)!;
        Hero third = heroes.Get(Hero.Roland)!;
        Recruits recruits = new();

        recruits.AppendSurrenderedHero(first, 5);
        Assert.Equal(first.Id, recruits.Id1);
        Assert.Equal(5u, recruits.GetSurrenderDayOfHero1());

        recruits.AppendSurrenderedHero(second, 7);
        Assert.Equal(first.Id, recruits.Id1);
        Assert.Equal(second.Id, recruits.Id2);
        Assert.Equal(5u, recruits.GetSurrenderDayOfHero1());
        Assert.Equal(7u, recruits.GetSurrenderDayOfHero2());

        recruits.AppendSurrenderedHero(third, 9);
        Assert.Equal(third.Id, recruits.Id1);
        Assert.Equal(second.Id, recruits.Id2);
        Assert.Equal(9u, recruits.GetSurrenderDayOfHero1());

        // Being pushed out of the pair does not unmark the hero: only the hiring does that.
        Assert.True(first.Modes.HasFlag(HeroModes.Recruit));
        Assert.True(second.Modes.HasFlag(HeroModes.Recruit));
        Assert.True(third.Modes.HasFlag(HeroModes.Recruit));
    }

    [Fact]
    public void ASurrenderedHeroMustHaveARealDay()
    {
        AllHeroes heroes = Heroes();
        Recruits recruits = new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => recruits.AppendSurrenderedHero(heroes.Get(Hero.LordKilburn)!, 0));
    }

    [Fact]
    public void ResetEmptiesBothSlots()
    {
        AllHeroes heroes = Heroes();
        Recruits recruits = new();
        recruits.SetHero1(heroes.Get(Hero.LordKilburn)!);
        recruits.SetHero2(heroes.Get(Hero.Celia)!);

        recruits.Reset();

        Assert.Equal(Hero.UnknownId, recruits.Id1);
        Assert.Equal(Hero.UnknownId, recruits.Id2);
        Assert.False(recruits.First.IsValid);
        Assert.False(recruits.Second.IsValid);
    }

    [Fact]
    public void AKingdomStartsWithNoRecruitsAndForgetsThemOnClear()
    {
        AllHeroes heroes = Heroes();
        PlayerKingdom kingdom = new();
        kingdom.Init(PlayerColor.Blue, GameDifficulty.Normal, isAiControlled: false);

        Assert.Equal(Hero.UnknownId, kingdom.GetRecruits().Id1);
        Assert.Equal(Hero.UnknownId, kingdom.GetRecruits().Id2);

        kingdom.GetRecruits().SetHero1(heroes.Get(Hero.LordKilburn)!);
        Assert.NotEqual(Hero.UnknownId, kingdom.GetRecruits().Id1);

        // Kingdom::clear() (kingdom.cpp:132) resets the pair along with the castle and hero lists.
        kingdom.Clear();
        Assert.Equal(Hero.UnknownId, kingdom.GetRecruits().Id1);
        Assert.Equal(Hero.UnknownId, kingdom.GetRecruits().Id2);
    }
}
