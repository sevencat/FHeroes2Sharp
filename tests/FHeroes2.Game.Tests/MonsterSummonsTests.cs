using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Monster( const Spell &amp; )</c> (<c>monster.cpp:77-108</c>): the eight elemental summon and
/// guardian spells, and the one spell that calls a ghost.
/// </summary>
public class MonsterSummonsTests
{
    [Theory]
    [InlineData(SpellType.SummonEElement, MonsterType.EarthElement)]
    [InlineData(SpellType.SetEGuardian, MonsterType.EarthElement)]
    [InlineData(SpellType.SummonAElement, MonsterType.AirElement)]
    [InlineData(SpellType.SetAGuardian, MonsterType.AirElement)]
    [InlineData(SpellType.SummonFElement, MonsterType.FireElement)]
    [InlineData(SpellType.SetFGuardian, MonsterType.FireElement)]
    [InlineData(SpellType.SummonWElement, MonsterType.WaterElement)]
    [InlineData(SpellType.SetWGuardian, MonsterType.WaterElement)]
    public void TheElementalSpellsCallTheirOwnElement(SpellType spell, MonsterType expected)
    {
        Assert.Equal(expected, MonsterSummons.FromSpell(spell));
    }

    [Fact]
    public void HauntCallsAGhost()
    {
        Assert.Equal(MonsterType.Ghost, MonsterSummons.FromSpell(SpellType.Haunt));
    }

    [Theory]
    [InlineData(SpellType.Fireball)]
    [InlineData(SpellType.Haste)]
    [InlineData(SpellType.Teleport)]
    [InlineData(SpellType.ViewTowns)]
    public void EveryOtherSpellCallsNothing(SpellType spell)
    {
        Assert.Equal(MonsterType.Unknown, MonsterSummons.FromSpell(spell));
    }
}
