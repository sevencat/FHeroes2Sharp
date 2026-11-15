using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the id numbering of <c>src/fheroes2/spell/spell.h:38-120</c>. The ids are stored in the monster ability
/// table and in spell scrolls, so an off-by-one here is silent and expensive.
/// </summary>
public class SpellTypeTests
{
    [Theory]
    [InlineData(SpellType.None, 0)]
    [InlineData(SpellType.Fireball, 1)]
    [InlineData(SpellType.Teleport, 5)]
    [InlineData(SpellType.Haste, 10)]
    [InlineData(SpellType.Blind, 14)]
    [InlineData(SpellType.Bless, 15)]
    [InlineData(SpellType.Curse, 19)]
    [InlineData(SpellType.AntiMagic, 23)]
    [InlineData(SpellType.Dispel, 24)]
    [InlineData(SpellType.Berserker, 27)]
    [InlineData(SpellType.Armageddon, 28)]
    [InlineData(SpellType.MeteorShower, 30)]
    [InlineData(SpellType.Paralyze, 31)]
    [InlineData(SpellType.ColdRay, 33)]
    [InlineData(SpellType.AnimateDead, 40)]
    [InlineData(SpellType.SummonWElement, 47)]
    [InlineData(SpellType.TownPortal, 59)]
    [InlineData(SpellType.SetWGuardian, 65)]
    [InlineData(SpellType.Random, 66)]
    [InlineData(SpellType.Random5, 71)]
    [InlineData(SpellType.Petrify, 72)]
    [InlineData(SpellType.Count, 73)]
    public void TheIdMatchesTheReference(SpellType spell, int expected)
    {
        Assert.Equal(expected, (int)spell);
    }

    [Fact]
    public void TheEnumHasOneMemberPerIdPlusTheCountSentinel()
    {
        Assert.Equal((int)SpellType.Count + 1, Enum.GetValues<SpellType>().Length);
    }

    [Fact]
    public void OnlyNoneIsOutsideTheValidRange()
    {
        Assert.False(SpellTypes.IsValid(SpellType.None));
        Assert.False(SpellTypes.IsValid(SpellType.Count));
        Assert.False(SpellTypes.IsValid((SpellType)(-1)));

        // The placeholders and the petrify gaze are valid ids: they are cast, they just never enter a spell
        // book.
        Assert.True(SpellTypes.IsValid(SpellType.Fireball));
        Assert.True(SpellTypes.IsValid(SpellType.Random));
        Assert.True(SpellTypes.IsValid(SpellType.Petrify));
    }
}
