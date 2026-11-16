using FHeroes2.Game.Kingdom;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>kingdom/speed.cpp</c> (the two spell adjustments and the two private step helpers) and the two
/// strings of <c>kingdom/experience.cpp</c>.
/// </summary>
public sealed class KingdomSpeedTests
{
    [Theory]
    [InlineData(Speed.VerySlow, Speed.Crawling)]
    [InlineData(Speed.Slow, Speed.VerySlow)]
    [InlineData(Speed.Average, Speed.VerySlow)]
    [InlineData(Speed.Fast, Speed.Slow)]
    [InlineData(Speed.VeryFast, Speed.Slow)]
    [InlineData(Speed.UltraFast, Speed.Average)]
    [InlineData(Speed.Blazing, Speed.Average)]
    [InlineData(Speed.Instant, Speed.Fast)]
    [InlineData(Speed.Standing, Speed.Standing)]
    public void ASlowerStepFollowsTheScale(uint speed, uint expected)
    {
        Assert.Equal(expected, Speed.GetSlowerSpeed(speed));
    }

    [Theory]
    [InlineData(Speed.Crawling, Speed.Slow)]
    [InlineData(Speed.VerySlow, Speed.Average)]
    [InlineData(Speed.Slow, Speed.Fast)]
    [InlineData(Speed.Average, Speed.VeryFast)]
    [InlineData(Speed.Fast, Speed.UltraFast)]
    [InlineData(Speed.VeryFast, Speed.Blazing)]
    [InlineData(Speed.Blazing, Speed.Instant)]
    [InlineData(Speed.Instant, Speed.Instant)]
    public void AFasterStepFollowsTheScale(uint speed, uint expected)
    {
        Assert.Equal(expected, Speed.GetFasterSpeed(speed));
    }

    [Fact]
    public void TheSlowSpellAndTheHasteSpellFollowTheirExtraValue()
    {
        // Slow and haste have no `extraValue` in the spell table (the column is for damage), so both fall
        // back to a single step — which is what the reference does with the same table.
        uint slowExtraValue = new FHeroes2.Game.Spells.Spell(FHeroes2.Game.Spells.SpellType.Slow).ExtraValue();
        uint hasteExtraValue = new FHeroes2.Game.Spells.Spell(FHeroes2.Game.Spells.SpellType.Haste).ExtraValue();

        Assert.Equal(
            slowExtraValue != 0 ? Speed.Average - slowExtraValue : Speed.GetSlowerSpeed(Speed.Average),
            Speed.GetSlowSpeedFromSpell(Speed.Average));
        Assert.Equal(
            hasteExtraValue != 0 ? Speed.Average + hasteExtraValue : Speed.GetFasterSpeed(Speed.Average),
            Speed.GetHasteSpeedFromSpell(Speed.Average));
    }

    [Fact]
    public void TheExperienceStringsAreTheReferenceOnes()
    {
        Assert.Equal("Experience", Experience.Name);
        Assert.Contains("primary and secondary skills", Experience.Description);
    }
}
