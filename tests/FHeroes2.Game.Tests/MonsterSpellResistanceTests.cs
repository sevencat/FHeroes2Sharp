using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins <c>getSpellResistance()</c> (<c>monster_info.cpp:880-940</c>): the order of the checks matters
/// because a hundred percent short-circuits, and the twenty-five of a dwarf only covers damage spells and
/// spells aimed at enemies.
/// </summary>
public class MonsterSpellResistanceTests
{
    [Theory]
    [InlineData(SpellType.Fireball)]
    [InlineData(SpellType.Blind)]
    [InlineData(SpellType.Curse)]
    public void TheDragonsShrugOffEverySpell(SpellType spell)
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.GreenDragon, spell));
    }

    [Fact]
    public void TheUndeadAreDeafToMindSpells()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.Skeleton, SpellType.Blind));
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.Peasant, SpellType.Blind));
    }

    [Fact]
    public void TheGiantsAreDeafToMindSpellsToo()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.Giant, SpellType.Blind));
    }

    [Fact]
    public void OnlyTheUndeadCanBeHurtByUndeadSpells()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.Peasant, SpellType.HolyWord));
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.Skeleton, SpellType.HolyWord));
    }

    [Fact]
    public void TheDwarfResistsDamageSpellsOnly()
    {
        Assert.Equal(25u, MonsterSpellResistance.GetSpellResistance(MonsterType.BattleDwarf, SpellType.Fireball));

        // Haste is neither a damage spell nor aimed at enemies, so the dwarf's resistance does not apply.
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.BattleDwarf, SpellType.Haste));
    }

    [Fact]
    public void TheFireElementalIgnoresFireAndTakesTheColdTwice()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.FireElement, SpellType.Fireball));

        // Cold is not resisted; the weakness table doubles its damage instead.
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.FireElement, SpellType.ColdRay));
    }

    [Fact]
    public void TheWaterElementalIgnoresColdAndTakesTheFireTwice()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.WaterElement, SpellType.ColdRay));
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.WaterElement, SpellType.Fireball));
    }

    [Fact]
    public void TheNamedSpellImmunityIsAbsolute()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.AirElement, SpellType.MeteorShower));
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.EarthElement, SpellType.ChainLightning));
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.EarthElement, SpellType.LightningBolt));
    }

    [Fact]
    public void TheNamedSpellImmunityDoesNotCoverItsSiblings()
    {
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.AirElement, SpellType.LightningBolt));
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.EarthElement, SpellType.MeteorShower));
    }

    [Fact]
    public void ThePhoenixIgnoresElementalSpells()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.Phoenix, SpellType.Fireball));
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.Phoenix, SpellType.ColdRay));
    }

    [Fact]
    public void TheElementalsAreDeafToMindSpells()
    {
        Assert.Equal(100u, MonsterSpellResistance.GetSpellResistance(MonsterType.AirElement, SpellType.Blind));
    }

    [Fact]
    public void APlainCreatureResistsNothing()
    {
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.Peasant, SpellType.Fireball));
        Assert.Equal(0u, MonsterSpellResistance.GetSpellResistance(MonsterType.Unknown, SpellType.Fireball));
    }
}
