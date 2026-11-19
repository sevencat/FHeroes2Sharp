using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="MonsterDescriptions"/>: the three description functions of <c>monster_info.cpp</c> — the words of the
/// creature info screen (ADR-173).
/// </summary>
public sealed class MonsterDescriptionsTests
{
    [Theory]
    [InlineData(MonsterAbility.None, 0, 0, "None")]
    [InlineData(MonsterAbility.DoubleShooting, 0, 0, "Double shot")]
    [InlineData(MonsterAbility.DoubleMeleeAttack, 0, 0, "Double strike")]
    [InlineData(MonsterAbility.DoubleDamageToUndead, 0, 0, "Double damage to Undead")]
    [InlineData(MonsterAbility.MagicResistance, 25, 0, "25% magic resistance")]
    [InlineData(MonsterAbility.MindSpellImmunity, 0, 0, "Immune to Mind spells")]
    [InlineData(MonsterAbility.FireSpellImmunity, 0, 0, "Immune to Fire spells")]
    [InlineData(MonsterAbility.HpRegeneration, 0, 0, "HP regeneration")]
    [InlineData(MonsterAbility.TwoCellMeleeAttack, 0, 0, "Two hexes attack")]
    [InlineData(MonsterAbility.UnlimitedRetaliation, 0, 0, "Unlimited retaliation")]
    [InlineData(MonsterAbility.AllAdjacentCellMeleeAttack, 0, 0, "Attacks all adjacent enemies")]
    [InlineData(MonsterAbility.NoMeleePenalty, 0, 0, "No melee penalty")]
    [InlineData(MonsterAbility.Undead, 0, 0, "Undead")]
    [InlineData(MonsterAbility.NoEnemyRetaliation, 0, 0, "No enemy retaliation")]
    [InlineData(MonsterAbility.HpDrain, 0, 0, "HP drain")]
    [InlineData(MonsterAbility.AreaShot, 0, 0, "Cloud attack")]
    [InlineData(MonsterAbility.MoralDecrement, 100, 1, "Decreases enemy's morale by 1")]
    [InlineData(MonsterAbility.EnemyHalving, 10, 0, "10% chance to halve enemy")]
    [InlineData(MonsterAbility.SoulEater, 0, 0, "Soul Eater")]
    [InlineData(MonsterAbility.ElementalSpellDamageReduction, 50, 0, "50% damage from Elemental spells")]
    [InlineData(MonsterAbility.Elemental, 0, 0, "Elemental")]
    public void AnAbilityIsNamedWithTheReferencesWord(MonsterAbility type, int percentage, int value, string description)
    {
        Assert.Equal(description, MonsterDescriptions.GetAbilityDescription(new MonsterAbilityInstance(type, percentage, value)));
    }

    [Theory]
    [InlineData(MonsterAbility.None, "None")]
    [InlineData(MonsterAbility.DoubleHexSize, "2-hex monster")]
    [InlineData(MonsterAbility.Flying, "Flyer")]
    [InlineData(MonsterAbility.Dragon, "Dragon")]
    [InlineData(MonsterAbility.EarthCreature, "Earth creature")]
    [InlineData(MonsterAbility.AirCreature, "Air creature")]
    [InlineData(MonsterAbility.FireCreature, "Fire creature")]
    [InlineData(MonsterAbility.WaterCreature, "Water creature")]
    public void ABasicAbilityIsHiddenFromTheInfoScreen(MonsterAbility type, string description)
    {
        MonsterAbilityInstance ability = new(type);

        Assert.Equal(description, MonsterDescriptions.GetAbilityDescription(ability, ignoreBasicAbilities: false));
        Assert.Empty(MonsterDescriptions.GetAbilityDescription(ability, ignoreBasicAbilities: true));
    }

    [Fact]
    public void AnElementalSaysNoMoraleInsteadOfGoingSilent()
    {
        // The one entry the flag rewrites rather than hides: the reference answers "No Morale" here.
        MonsterAbilityInstance ability = new(MonsterAbility.Elemental);

        Assert.Equal("Elemental", MonsterDescriptions.GetAbilityDescription(ability, ignoreBasicAbilities: false));
        Assert.Equal("No Morale", MonsterDescriptions.GetAbilityDescription(ability, ignoreBasicAbilities: true));
    }

    [Theory]
    // The five spells the reference gives a sentence of their own.
    [InlineData(SpellType.Dispel, 20, "20% chance to Dispel beneficial spells")]
    [InlineData(SpellType.Paralyze, 20, "20% chance to Paralyze")]
    [InlineData(SpellType.Petrify, 10, "10% chance to Petrify")]
    [InlineData(SpellType.Blind, 20, "20% chance to Blind")]
    [InlineData(SpellType.Curse, 30, "30% chance to Curse")]
    // Everything else, the general form.
    [InlineData(SpellType.Fireball, 30, "30% chance to cast Fireball spell")]
    public void ASpellCasterNamesItsSpellThroughTheSpellTable(SpellType spell, int percentage, string description)
    {
        MonsterAbilityInstance ability = new(MonsterAbility.SpellCaster, percentage, (int)spell);

        Assert.Equal(description, MonsterDescriptions.GetAbilityDescription(ability));
    }

    [Fact]
    public void ANamedSpellIsReadFromTheSpellTable()
    {
        // 100 percent is immunity, anything less keeps the number in front.
        Assert.Equal("Immune to Fireball", MonsterDescriptions.GetAbilityDescription(
            new MonsterAbilityInstance(MonsterAbility.ImmuneToCertainSpell, 100, (int)SpellType.Fireball)));
        Assert.Equal("50% immunity to Fireball spell", MonsterDescriptions.GetAbilityDescription(
            new MonsterAbilityInstance(MonsterAbility.ImmuneToCertainSpell, 50, (int)SpellType.Fireball)));
        Assert.Equal("50% damage from Armageddon spell", MonsterDescriptions.GetAbilityDescription(
            new MonsterAbilityInstance(MonsterAbility.CertainSpellDamageReduction, 50, (int)SpellType.Armageddon)));
    }

    [Theory]
    [InlineData(MonsterWeaknessType.None, 0, 0, "None")]
    [InlineData(MonsterWeaknessType.DoubleDamageFromFireSpells, 0, 0, "Double damage from Fire spells")]
    [InlineData(MonsterWeaknessType.DoubleDamageFromColdSpells, 0, 0, "Double damage from Cold spells")]
    [InlineData(MonsterWeaknessType.DoubleDamageFromEarthCreatures, 0, 0, "Double damage from Earth creatures")]
    [InlineData(MonsterWeaknessType.DoubleDamageFromWaterCreatures, 0, 0, "Double damage from Water creatures")]
    // The stored number is the extra over 100, the sentence says the total.
    [InlineData(MonsterWeaknessType.ExtraDamageFromCertainSpell, 100, 4, "200% damage from Chain Lightning spell")]
    public void AWeaknessIsNamedWithTheReferencesWord(MonsterWeaknessType type, int percentage, int value, string description)
    {
        Assert.Equal(description, MonsterDescriptions.GetWeaknessDescription(new MonsterWeaknessInstance(type, percentage, value)));
    }

    [Fact]
    public void TheFourWeaknessesOfACreaturesElementAreHidden()
    {
        MonsterWeaknessInstance basic = new(MonsterWeaknessType.DoubleDamageFromFireCreatures);

        Assert.Equal("Double damage from Fire creatures", MonsterDescriptions.GetWeaknessDescription(basic));
        Assert.Empty(MonsterDescriptions.GetWeaknessDescription(basic, ignoreBasicWeaknesses: true));

        // NONE is hidden too, which IsBasic() does not cover: the reference checks it in its own arm.
        Assert.Empty(MonsterDescriptions.GetWeaknessDescription(
            new MonsterWeaknessInstance(MonsterWeaknessType.None), ignoreBasicWeaknesses: true));
    }

    [Fact]
    public void ACreatureWithoutAbilitiesHasNothingToSay()
    {
        Assert.Empty(MonsterDescriptions.GetPropertiesDescription(MonsterType.Peasant));
    }

    [Fact]
    public void TheElementalsDescriptionIsTheHiddenEntriesPlusTheirOwn()
    {
        // Water elemental: ELEMENTAL, WATER_CREATURE, COLD_SPELL_IMMUNITY — the element creature is basic, the
        // elemental turns into "No Morale", and "Double damage from Fire creatures" is a basic weakness.
        Assert.Equal(
            ["No Morale.", "Immune to Cold spells.", "Double damage from Fire spells."],
            MonsterDescriptions.GetPropertiesDescription(MonsterType.WaterElement));
    }

    [Fact]
    public void TheEarthElementalsImmunitiesAreOneLineWithTheMassSpellDropped()
    {
        // Three IMMUNE_TO_CERTAIN_SPELL entries at 100 percent become one line, chain lightning disappears because
        // lightning bolt is there too, and the remaining bolt is printed as "Lightning".
        Assert.Equal(
            ["No Morale.", "Immune to Lightning, Elemental Storm.", "200% damage from Meteor Shower."],
            MonsterDescriptions.GetPropertiesDescription(MonsterType.EarthElement));
    }

    [Fact]
    public void TheCrusadersTwoCurseImmunitiesCollapseIntoOne()
    {
        // Double strike, double damage to undead, then IMMUNE_TO_CERTAIN_SPELL for CURSE and for MASSCURSE; the mass
        // spell is dropped because the single one is present.
        Assert.Equal(
            ["Double strike.", "Double damage to Undead.", "Immune to Curse."],
            MonsterDescriptions.GetPropertiesDescription(MonsterType.Crusader));
    }

    [Fact]
    public void ThePlainSentencesComeBeforeTheGroupedOnes()
    {
        // Iron golem: a damage reduction for elemental spells and one for armageddon. The first is a plain sentence,
        // the second is grouped, and the reference emits the plain ones first.
        Assert.Equal(
            ["50% damage from Elemental spells.", "50% damage from Armageddon."],
            MonsterDescriptions.GetPropertiesDescription(MonsterType.IronGolem));
    }

    [Fact]
    public void TheCyclopsSaysItsSpellCasterSentence()
    {
        Assert.Equal(
            ["Two hexes attack.", "20% chance to Paralyze."],
            MonsterDescriptions.GetPropertiesDescription(MonsterType.Cyclops));
    }

    [Fact]
    public void EveryEntryOfEveryTableHasWords()
    {
        // A new ability or weakness without a sentence would fall through to the empty default and vanish from the
        // info screen without a word; this walks the tables the reference keeps.
        foreach (MonsterType monster in Enum.GetValues<MonsterType>())
        {
            foreach (MonsterAbilityInstance ability in MonsterAbilities.GetInstances(monster))
            {
                Assert.NotEmpty(MonsterDescriptions.GetAbilityDescription(ability));

                if (!MonsterAbilities.IsBasic(ability.Type))
                {
                    Assert.NotEmpty(MonsterDescriptions.GetAbilityDescription(ability, ignoreBasicAbilities: true));
                }
            }

            foreach (MonsterWeaknessInstance weakness in MonsterWeaknesses.GetInstances(monster))
            {
                Assert.NotEmpty(MonsterDescriptions.GetWeaknessDescription(weakness));

                if (!MonsterWeaknesses.IsBasic(weakness.Type))
                {
                    Assert.NotEmpty(MonsterDescriptions.GetWeaknessDescription(weakness, ignoreBasicWeaknesses: true));
                }
            }
        }
    }

    [Fact]
    public void EveryLineOfAPropertiesListIsASentence()
    {
        foreach (MonsterType monster in Enum.GetValues<MonsterType>())
        {
            foreach (string line in MonsterDescriptions.GetPropertiesDescription(monster))
            {
                Assert.EndsWith(".", line);

                // "None." would be the placeholder of a hidden entry leaking through; the reference never prints it.
                Assert.NotEqual("None.", line);
            }
        }
    }
}
