using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the weakness table of <c>monster_info.cpp:531-561</c>. Only the four elementals have one, in pairs
/// that mirror their immunities: the air elemental burns from the lightning spells the earth elemental
/// shrugs off, and the fire and water elementals double each other's damage.
/// </summary>
public class MonsterWeaknessTests
{
    private static readonly MonsterType[] Elementals =
    [
        MonsterType.AirElement,
        MonsterType.EarthElement,
        MonsterType.FireElement,
        MonsterType.WaterElement,
    ];

    [Fact]
    public void OnlyTheFourElementalsHaveWeaknesses()
    {
        for (int id = (int)MonsterType.Unknown; id < (int)MonsterType.MonsterCount; ++id)
        {
            var monster = (MonsterType)id;
            bool expected = Elementals.Contains(monster);

            Assert.Equal(expected, MonsterWeaknesses.GetInstances(monster).Count > 0);
        }
    }

    [Fact]
    public void TheAirElementalIsHurtByEveryLightningSpell()
    {
        MonsterWeaknessInstance[] weaknesses = [.. MonsterWeaknesses.GetInstances(MonsterType.AirElement)];

        Assert.Equal(
            [
                MonsterWeaknessType.ExtraDamageFromCertainSpell,
                MonsterWeaknessType.ExtraDamageFromCertainSpell,
                MonsterWeaknessType.ExtraDamageFromCertainSpell,
                MonsterWeaknessType.DoubleDamageFromEarthCreatures,
            ],
            weaknesses.Select(weakness => weakness.Type));

        Assert.Equal(
            [SpellType.ChainLightning, SpellType.ElementalStorm, SpellType.LightningBolt],
            weaknesses.Take(3).Select(weakness => weakness.Spell));

        Assert.All(weaknesses.Take(3), weakness => Assert.Equal(100, weakness.Percentage));
    }

    [Fact]
    public void TheEarthElementalMirrorsTheAirOne()
    {
        MonsterWeaknessInstance[] weaknesses = [.. MonsterWeaknesses.GetInstances(MonsterType.EarthElement)];

        Assert.Equal(
            [MonsterWeaknessType.ExtraDamageFromCertainSpell, MonsterWeaknessType.DoubleDamageFromAirCreatures],
            weaknesses.Select(weakness => weakness.Type));

        Assert.Equal(SpellType.MeteorShower, weaknesses[0].Spell);
        Assert.Equal(100, weaknesses[0].Percentage);
    }

    [Fact]
    public void TheFireAndWaterElementalsDoubleEachOthersDamage()
    {
        Assert.Equal(
            [MonsterWeaknessType.DoubleDamageFromColdSpells, MonsterWeaknessType.DoubleDamageFromWaterCreatures],
            MonsterWeaknesses.GetInstances(MonsterType.FireElement).Select(weakness => weakness.Type));

        Assert.Equal(
            [MonsterWeaknessType.DoubleDamageFromFireSpells, MonsterWeaknessType.DoubleDamageFromFireCreatures],
            MonsterWeaknesses.GetInstances(MonsterType.WaterElement).Select(weakness => weakness.Type));
    }

    [Fact]
    public void TheBasicWeaknessesAreTheFourCreatureOnes()
    {
        Assert.True(MonsterWeaknesses.IsBasic(MonsterWeaknessType.DoubleDamageFromEarthCreatures));
        Assert.True(MonsterWeaknesses.IsBasic(MonsterWeaknessType.DoubleDamageFromAirCreatures));
        Assert.True(MonsterWeaknesses.IsBasic(MonsterWeaknessType.DoubleDamageFromFireCreatures));
        Assert.True(MonsterWeaknesses.IsBasic(MonsterWeaknessType.DoubleDamageFromWaterCreatures));

        Assert.False(MonsterWeaknesses.IsBasic(MonsterWeaknessType.DoubleDamageFromFireSpells));
        Assert.False(MonsterWeaknesses.IsBasic(MonsterWeaknessType.DoubleDamageFromColdSpells));
        Assert.False(MonsterWeaknesses.IsBasic(MonsterWeaknessType.ExtraDamageFromCertainSpell));
        Assert.False(MonsterWeaknesses.IsBasic(MonsterWeaknessType.None));
    }

    [Fact]
    public void TheEntryCarriesThePercentageAndTheSpell()
    {
        Assert.True(
            MonsterWeaknesses.TryGetInstance(
                MonsterType.AirElement,
                MonsterWeaknessType.ExtraDamageFromCertainSpell,
                out MonsterWeaknessInstance instance));

        Assert.Equal(100, instance.Percentage);
        Assert.Equal(SpellType.ChainLightning, instance.Spell);

        Assert.False(
            MonsterWeaknesses.TryGetInstance(
                MonsterType.AirElement,
                MonsterWeaknessType.DoubleDamageFromColdSpells,
                out _));
    }

    [Fact]
    public void PresenceAndLookupAgree()
    {
        foreach (MonsterType monster in Elementals)
        {
            foreach (MonsterWeaknessInstance instance in MonsterWeaknesses.GetInstances(monster))
            {
                Assert.True(MonsterWeaknesses.IsPresent(monster, instance.Type));
            }
        }

        Assert.False(MonsterWeaknesses.IsPresent(MonsterType.Peasant, MonsterWeaknessType.DoubleDamageFromColdSpells));
    }
}
