using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers the helpers of <c>race.cpp</c> that live on <see cref="MapRaces"/>: the six playable races,
/// <c>Race::Rand()</c>, <c>isEvilRace()</c>, <c>isMagicalRace()</c> and <c>Race::String()</c>.
/// </summary>
public sealed class MapRaceTests
{
    [Fact]
    public void TheSixPlayableRacesAreTheOnesTheRandomRollDraws()
    {
        Assert.Equal(6, MapRaces.Basic.Count);
        Assert.Equal(MapRace.Knight, MapRaces.Basic[0]);
        Assert.Equal(MapRace.Necromancer, MapRaces.Basic[5]);

        // Two casts and a multiple are never drawn: Race::Rand() rolls Rand::Get(1, 6).
        Assert.DoesNotContain(MapRace.None, MapRaces.Basic);
        Assert.DoesNotContain(MapRace.Random, MapRaces.Basic);
        Assert.DoesNotContain(MapRace.Multiple, MapRaces.Basic);

        for (int draw = 0; draw < 200; ++draw)
        {
            Assert.Contains(MapRaces.Random(), MapRaces.Basic);
        }
    }

    [Fact]
    public void OnlyTheThreeDarkRacesAreEvil()
    {
        Assert.True(MapRaces.IsEvil(MapRace.Barbarian));
        Assert.True(MapRaces.IsEvil(MapRace.Warlock));
        Assert.True(MapRaces.IsEvil(MapRace.Necromancer));

        Assert.False(MapRaces.IsEvil(MapRace.Knight));
        Assert.False(MapRaces.IsEvil(MapRace.Sorceress));
        Assert.False(MapRaces.IsEvil(MapRace.Wizard));

        // The reference asserts on anything else and answers false.
        Assert.False(MapRaces.IsEvil(MapRace.None));
        Assert.False(MapRaces.IsEvil(MapRace.Random));
    }

    [Fact]
    public void TheFourCastingRacesAreMagical()
    {
        Assert.True(MapRaces.IsMagical(MapRace.Sorceress));
        Assert.True(MapRaces.IsMagical(MapRace.Warlock));
        Assert.True(MapRaces.IsMagical(MapRace.Wizard));
        Assert.True(MapRaces.IsMagical(MapRace.Necromancer));

        Assert.False(MapRaces.IsMagical(MapRace.Knight));
        Assert.False(MapRaces.IsMagical(MapRace.Barbarian));
        Assert.False(MapRaces.IsMagical(MapRace.None));
    }

    [Fact]
    public void TheNamesAreTheEnglishOnesOfTheReference()
    {
        Assert.Equal("Knight", MapRaces.Name(MapRace.Knight));
        Assert.Equal("Barbarian", MapRaces.Name(MapRace.Barbarian));
        Assert.Equal("Sorceress", MapRaces.Name(MapRace.Sorceress));
        Assert.Equal("Warlock", MapRaces.Name(MapRace.Warlock));
        Assert.Equal("Wizard", MapRaces.Name(MapRace.Wizard));
        Assert.Equal("Necromancer", MapRaces.Name(MapRace.Necromancer));
        Assert.Equal("Neutral", MapRaces.Name(MapRace.None));
        Assert.Equal("Random", MapRaces.Name(MapRace.Random));
    }
}
