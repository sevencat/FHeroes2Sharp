using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of <see cref="HeroNames"/> and of the default-name fallback of <see cref="Hero.Name"/>
/// (<c>defaultHeroNames</c>, <c>heroes.cpp:84-121</c>；回落见 <c>heroes.cpp:2100-2116</c>).
/// </summary>
public sealed class HeroNamesTests
{
    [Fact]
    public void TheTableHoldsOneNamePerHeroId()
    {
        Assert.Equal(Hero.IdCount, HeroNames.Count);
        Assert.Equal("Unknown", HeroNames.Get(0));
        Assert.Equal("Lord Kilburn", HeroNames.Get(Hero.LordKilburn));
        Assert.Equal("Debug Hero", HeroNames.Get(Hero.DebugHero));
    }

    [Theory]
    [InlineData(9, "Dimitry")]
    [InlineData(10, "Thundax")]
    [InlineData(18, "Atlas")]
    [InlineData(19, "Astra")]
    [InlineData(54, "Celia")]
    [InlineData(55, "Roland")]
    [InlineData(60, "Brother Brax")]
    [InlineData(61, "Solmyr")]
    [InlineData(71, "Jarkonas")]
    public void TheRacesAreLinedUpAsInTheReference(int id, string expected) =>
        Assert.Equal(expected, HeroNames.Get(id));

    [Fact]
    public void AnIdOutsideTheTableHasNoName()
    {
        Assert.Equal(string.Empty, HeroNames.Get(-1));
        Assert.Equal(string.Empty, HeroNames.Get(Hero.IdCount));
    }

    [Fact]
    public void AHeroWithoutACustomNameWearsTheDefaultOne()
    {
        Hero hero = new(Hero.LordKilburn, MapRace.Knight);

        Assert.Equal("Lord Kilburn", hero.Name);
    }
}
