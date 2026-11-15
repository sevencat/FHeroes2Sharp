using FHeroes2.Assets.Maps;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the spell book, the spell points and the strategic value of a hero
/// (<c>heroes_base.cpp:172-190</c>, <c>heroes_base.cpp:358-377</c>, <c>heroes.cpp:967-970</c>) and the point
/// where they reach the army's strength (<c>army.cpp:1291-1333</c>).
/// </summary>
public class HeroSpellsTests
{
    private static Hero NewHero() => new(1, MapRace.Knight);

    private static Hero NewCaster(params SpellType[] spells)
    {
        Hero hero = NewHero();
        hero.BagArtifacts.Push(ArtifactType.MagicBook);

        foreach (SpellType spell in spells)
        {
            hero.MagicBook.Append(new Spell(spell));
        }

        return hero;
    }

    [Fact]
    public void ANewHeroHasNoBookAndNoSpellPoints()
    {
        Hero hero = NewHero();

        Assert.False(hero.HasSpellBook);
        Assert.Equal(0u, hero.SpellPoints);
        Assert.Equal(0, hero.GetAllSpells().Count);
        Assert.Equal(0.0, hero.GetMagicStrategicValue(1000));
    }

    [Fact]
    public void TheBookIsAnArtifact()
    {
        Hero hero = NewHero();

        hero.BagArtifacts.Push(ArtifactType.MagicBook);

        Assert.True(hero.HasSpellBook);
    }

    [Fact]
    public void TheSpellsOfTheBookAreUnreachableWithoutTheBook()
    {
        // The reference returns an empty storage instead of the scrolls: knowing a spell is not casting it.
        Hero hero = NewHero();
        hero.MagicBook.Append(new Spell(SpellType.Fireball));

        Assert.Equal(0, hero.GetAllSpells().Count);
        Assert.False(hero.HasSpell(new Spell(SpellType.Fireball)));

        hero.BagArtifacts.Push(ArtifactType.MagicBook);

        Assert.Equal(1, hero.GetAllSpells().Count);
        Assert.True(hero.HasSpell(new Spell(SpellType.Fireball)));
    }

    [Fact]
    public void TheScrollsJoinTheSpellsOfTheBook()
    {
        // The battle garb carries Town Portal, the swordsman no spell: the book spell comes first, the
        // artifact one after it.
        Hero hero = NewCaster(SpellType.Haste);
        hero.BagArtifacts.Push(ArtifactType.BattleGarb);
        hero.BagArtifacts.Push(ArtifactType.UltimateSword);

        SpellStorage all = hero.GetAllSpells();

        Assert.Equal(2, all.Count);
        Assert.Equal(SpellType.Haste, all[0].Type);
        Assert.Equal(SpellType.TownPortal, all[1].Type);

        Assert.True(hero.HasSpell(new Spell(SpellType.TownPortal)));

        // The spell book screen asks without the scrolls.
        Assert.False(hero.HasSpell(new Spell(SpellType.TownPortal), skipBag: true));
        Assert.True(hero.HasSpell(new Spell(SpellType.Haste), skipBag: true));
    }

    [Fact]
    public void TheMaximumSpellPointsAreTenPerKnowledge()
    {
        Hero hero = NewHero();

        // The effective knowledge is clamped to at least one, so a hero always has ten points of mana.
        Assert.Equal(10u, hero.GetMaxSpellPoints());

        hero.SetPrimarySkill(PrimarySkill.Knowledge, 4);
        Assert.Equal(40u, hero.GetMaxSpellPoints());

        // The artifacts count, because it is the effective knowledge that is read.
        hero.BagArtifacts.Push(ArtifactType.UltimateBook);
        Assert.Equal(160u, hero.GetMaxSpellPoints());
    }

    [Fact]
    public void TheSpellPointsAreNotClampedByTheSetter()
    {
        // The reference does not clamp: the callers are the MP2 loader and the two mana sources, and each
        // bounds the value it writes.
        Hero hero = NewHero();

        hero.SetSpellPoints(7);
        Assert.Equal(7u, hero.SpellPoints);

        hero.SetSpellPoints(9999);
        Assert.Equal(9999u, hero.SpellPoints);
    }

    [Fact]
    public void NoSpellPointsMeansNoMagicValue()
    {
        Hero hero = NewCaster(SpellType.Fireball);
        hero.SetPrimarySkill(PrimarySkill.Power, 3);

        Assert.Equal(0.0, hero.GetMagicStrategicValue(1000));

        hero.SetSpellPoints(9);
        Assert.Equal(30.0, hero.GetMagicStrategicValue(1000), 9);
    }

    [Fact]
    public void TheBestSpellDecidesTheValueNotTheSum()
    {
        // Fireball with nine points of mana is one cast of thirty; Shield is three casts of four percent of
        // the army, twenty for two hundred peasants. The hero casts one spell per turn, so the strength grows
        // by the best of them, not by the two together.
        Hero hero = NewCaster(SpellType.Fireball, SpellType.Shield);
        hero.SetPrimarySkill(PrimarySkill.Power, 3);
        hero.SetSpellPoints(9);

        Assert.Equal(30.0, hero.GetMagicStrategicValue(200), 9);
    }

    [Fact]
    public void TheAdventureSpellsAreNotCounted()
    {
        // Town Portal is worth a lot on the map and nothing in a battle, and this value is about battles.
        Hero hero = NewCaster(SpellType.TownPortal);
        hero.SetPrimarySkill(PrimarySkill.Power, 1);
        hero.SetSpellPoints(100);

        Assert.Equal(0.0, hero.GetMagicStrategicValue(1000));
    }

    [Fact]
    public void TheArmyStrengthGrowsWithTheBestSpell()
    {
        Hero hero = NewCaster(SpellType.Fireball);
        hero.SetPrimarySkill(PrimarySkill.Power, 3);
        hero.Army.JoinTroop(MonsterType.Peasant, 10);

        double withoutMagic = hero.Army.GetStrength();

        hero.SetSpellPoints(9);

        double magic = hero.GetMagicStrategicValue(withoutMagic);

        Assert.Equal(30.0, magic, 9);
        Assert.Equal(withoutMagic + magic, hero.Army.GetStrength(), 9);
    }
}
