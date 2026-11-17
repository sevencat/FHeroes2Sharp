using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The resistance of a creature to one spell, <c>fheroes2::getSpellResistance()</c>
/// (<c>src/fheroes2/monster/monster_info.cpp:880-940</c>).
/// </summary>
/// <remarks>
/// <para>
/// The answer is a percentage: zero means the spell applies normally, 100 means the creature is immune. The
/// two things in between are the dwarf's <c>MAGIC_RESISTANCE 25</c> — which only defends against damage
/// spells and spells aimed at enemies — and the dragons' <c>MAGIC_RESISTANCE 100</c>, which is an
/// unconditional immunity and therefore short-circuits before the per-spell check.
/// </para>
/// <para>
/// The per-spell <c>IMMUNE_TO_CERTAIN_SPELL</c> entries are consulted last and take the maximum rather than
/// the last value, because two entries can name the same spell in the reference's tables.
/// </para>
/// </remarks>
public static class MonsterSpellResistance
{
    /// <summary>
    /// The resistance of a creature to a spell, <c>getSpellResistance()</c>.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <param name="spell">The spell.</param>
    /// <returns>0 to 100, the chance in percent that the spell does nothing.</returns>
    public static uint GetSpellResistance(MonsterType monster, SpellType spell)
    {
        var cast = new Spell(spell);
        MonsterAbility abilities = MonsterAbilities.Get(monster);

        if (cast.IsMindInfluence() && HasAny(abilities, MonsterAbility.MindSpellImmunity, MonsterAbility.Undead, MonsterAbility.Elemental))
        {
            return 100;
        }

        if (cast.IsAliveOnly() && Has(abilities, MonsterAbility.Undead))
        {
            return 100;
        }

        if (cast.IsUndeadOnly() && !Has(abilities, MonsterAbility.Undead))
        {
            return 100;
        }

        if (cast.IsCold() && Has(abilities, MonsterAbility.ColdSpellImmunity))
        {
            return 100;
        }

        if (cast.IsFire() && Has(abilities, MonsterAbility.FireSpellImmunity))
        {
            return 100;
        }

        if (cast.IsElementalSpell() && Has(abilities, MonsterAbility.ElementalSpellImmunity))
        {
            return 100;
        }

        uint resistance = 0;

        if (MonsterAbilities.TryGetInstance(monster, MonsterAbility.MagicResistance, out MonsterAbilityInstance magicResistance))
        {
            if (magicResistance.Percentage == 100)
            {
                // Immune to everything.
                return 100;
            }

            if (cast.IsDamage() || cast.IsApplyToEnemies())
            {
                resistance = (uint)magicResistance.Percentage;
            }
        }

        foreach (MonsterAbilityInstance instance in MonsterAbilities.GetInstances(monster))
        {
            if (instance.Type == MonsterAbility.ImmuneToCertainSpell && instance.Value == (int)spell)
            {
                resistance = Math.Max(resistance, (uint)instance.Percentage);
            }
        }

        return resistance;
    }

    private static bool Has(MonsterAbility abilities, MonsterAbility ability) => (abilities & ability) == ability;

    private static bool HasAny(MonsterAbility abilities, MonsterAbility first, MonsterAbility second, MonsterAbility third) =>
        Has(abilities, first) || Has(abilities, second) || Has(abilities, third);
}
