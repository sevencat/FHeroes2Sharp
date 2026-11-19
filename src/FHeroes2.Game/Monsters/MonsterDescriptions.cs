using System.Text;

using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The words of the creature info screen, the three description functions of
/// <c>src/fheroes2/monster/monster_info.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetAbilityDescription"/> (<c>monster_info.cpp:611-716</c>) and
/// <see cref="GetWeaknessDescription"/> (<c>monster_info.cpp:717-746</c>) name one entry of the ability or weakness
/// list; <see cref="GetPropertiesDescription"/> (<c>monster_info.cpp:795-878</c>) is the list the army info dialog
/// shows — one sentence per line, each closed with a full stop — and it is the only thing the game calls
/// (<c>dialog_armyinfo.cpp:431</c>).
/// </para>
/// <para>
/// The words are the reference's own English (ADR-137, ADR-172): no translation layer, and no <c>%{spell}</c>
/// placeholder left over — the two functions that carry one substitute it here, because the spell name comes from
/// <see cref="SpellText"/>. The numbers are not substituted for anyone: <c>"25% magic resistance"</c> is the finished
/// text, unlike a spell's description, whose <c>%{count}</c> the battle engine fills in.
/// </para>
/// <para>
/// <b>The basic entries are hidden on purpose.</b> A creature's shape — two hexes, flying, a dragon, an elemental of
/// one element — says nothing a reader of the dialog does not see, so <see cref="GetPropertiesDescription"/> asks for
/// the descriptions with the flag on and the reference drops those entries. <see cref="MonsterAbilities.IsBasic"/>
/// and <see cref="MonsterWeaknesses.IsBasic"/> draw that line, and one ability is special: an elemental stops being
/// "Elemental" and becomes <c>"No Morale"</c>, which is the fact worth printing.
/// </para>
/// </remarks>
public static class MonsterDescriptions
{
    /// <summary>
    /// What one ability says, <c>getMonsterAbilityDescription()</c> (<c>monster_info.cpp:611-716</c>).
    /// </summary>
    /// <remarks>
    /// The ability's own numbers end up in the sentence — <c>"25% magic resistance"</c>, <c>"20% chance to Paralyze"</c>,
    /// <c>"Decreases enemy's morale by 1"</c> — and its spell, where it has one, is named through
    /// <see cref="Spell"/>.<see cref="Spell.GetName"/>. The sentence has no trailing full stop: the caller adds one,
    /// exactly as the reference does.
    /// </remarks>
    /// <param name="ability">The entry.</param>
    /// <param name="ignoreBasicAbilities">Whether the entries every creature of that shape shares return nothing.</param>
    /// <returns>The sentence, or an empty string when it is a hidden one.</returns>
    public static string GetAbilityDescription(MonsterAbilityInstance ability, bool ignoreBasicAbilities = false)
    {
        if (ignoreBasicAbilities && MonsterAbilities.IsBasic(ability.Type))
        {
            return string.Empty;
        }

        return ability.Type switch
        {
            MonsterAbility.None => "None",
            MonsterAbility.DoubleShooting => "Double shot",
            MonsterAbility.DoubleHexSize => "2-hex monster",
            MonsterAbility.DoubleMeleeAttack => "Double strike",
            MonsterAbility.DoubleDamageToUndead => "Double damage to Undead",
            MonsterAbility.MagicResistance => $"{ability.Percentage}% magic resistance",
            MonsterAbility.MindSpellImmunity => "Immune to Mind spells",
            MonsterAbility.ElementalSpellImmunity => "Immune to Elemental spells",
            MonsterAbility.FireSpellImmunity => "Immune to Fire spells",
            MonsterAbility.ColdSpellImmunity => "Immune to Cold spells",

            // One hundred percent is immunity, and the reference words it that way instead of "100% immunity to".
            MonsterAbility.ImmuneToCertainSpell => ability.Percentage == 100
                ? "Immune to " + new Spell(ability.Spell).GetName()
                : $"{ability.Percentage}% immunity to {new Spell(ability.Spell).GetName()} spell",

            MonsterAbility.ElementalSpellDamageReduction => $"{ability.Percentage}% damage from Elemental spells",
            MonsterAbility.CertainSpellDamageReduction => $"{ability.Percentage}% damage from {new Spell(ability.Spell).GetName()} spell",
            MonsterAbility.SpellCaster => GetSpellCasterDescription(ability),
            MonsterAbility.HpRegeneration => "HP regeneration",
            MonsterAbility.TwoCellMeleeAttack => "Two hexes attack",
            MonsterAbility.Flying => "Flyer",
            MonsterAbility.UnlimitedRetaliation => "Unlimited retaliation",
            MonsterAbility.AllAdjacentCellMeleeAttack => "Attacks all adjacent enemies",
            MonsterAbility.NoMeleePenalty => "No melee penalty",
            MonsterAbility.Dragon => "Dragon",
            MonsterAbility.Undead => "Undead",
            MonsterAbility.NoEnemyRetaliation => "No enemy retaliation",
            MonsterAbility.HpDrain => "HP drain",
            MonsterAbility.AreaShot => "Cloud attack",
            MonsterAbility.MoralDecrement => $"Decreases enemy's morale by {ability.Value}",
            MonsterAbility.EnemyHalving => $"{ability.Percentage}% chance to halve enemy",
            MonsterAbility.SoulEater => "Soul Eater",

            // Not a basic entry: hiding it turns it into the fact that matters.
            MonsterAbility.Elemental => ignoreBasicAbilities ? "No Morale" : "Elemental",

            MonsterAbility.EarthCreature => "Earth creature",
            MonsterAbility.AirCreature => "Air creature",
            MonsterAbility.FireCreature => "Fire creature",
            MonsterAbility.WaterCreature => "Water creature",

            _ => string.Empty,
        };
    }

    /// <summary>
    /// What one weakness says, <c>getMonsterWeaknessDescription()</c> (<c>monster_info.cpp:717-746</c>).
    /// </summary>
    /// <remarks>
    /// The extra damage is stored as the bonus over 100 percent, so an entry of 100 reads
    /// <c>"200% damage from Chain Lightning spell"</c> — the doubling, not the extra.
    /// </remarks>
    /// <param name="weakness">The entry.</param>
    /// <param name="ignoreBasicWeaknesses">Whether the entries every creature of that shape shares return nothing.</param>
    /// <returns>The sentence, or an empty string when it is a hidden one.</returns>
    public static string GetWeaknessDescription(MonsterWeaknessInstance weakness, bool ignoreBasicWeaknesses = false)
    {
        if (ignoreBasicWeaknesses
            && (weakness.Type == MonsterWeaknessType.None || MonsterWeaknesses.IsBasic(weakness.Type)))
        {
            return string.Empty;
        }

        return weakness.Type switch
        {
            MonsterWeaknessType.None => "None",
            MonsterWeaknessType.DoubleDamageFromFireSpells => "Double damage from Fire spells",
            MonsterWeaknessType.DoubleDamageFromColdSpells => "Double damage from Cold spells",
            MonsterWeaknessType.ExtraDamageFromCertainSpell =>
                $"{weakness.Percentage + 100}% damage from {new Spell(weakness.Spell).GetName()} spell",
            MonsterWeaknessType.DoubleDamageFromEarthCreatures => "Double damage from Earth creatures",
            MonsterWeaknessType.DoubleDamageFromAirCreatures => "Double damage from Air creatures",
            MonsterWeaknessType.DoubleDamageFromFireCreatures => "Double damage from Fire creatures",
            MonsterWeaknessType.DoubleDamageFromWaterCreatures => "Double damage from Water creatures",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The creature's abilities and weaknesses as the info screen lists them,
    /// <c>getMonsterPropertiesDescription()</c> (<c>monster_info.cpp:795-878</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plain entries come first, in the table's order, then the grouped ones: the reference collects every
    /// <c>IMMUNE_TO_CERTAIN_SPELL</c> and <c>CERTAIN_SPELL_DAMAGE_REDUCTION</c> by percentage and prints one line per
    /// percentage — <c>"Immune to Lightning, Elemental Storm."</c> rather than two immunity lines — and the same for
    /// <c>EXTRA_DAMAGE_FROM_CERTAIN_SPELL</c>. A group is a <see cref="SortedDictionary{TKey,TValue}"/> because the
    /// reference's <c>std::map</c> is one, and the spell list inside a line is sorted by id, which is why the earth
    /// elemental's lightning comes before its elemental storm.
    /// </para>
    /// <para>
    /// Mass spells are dropped from a group when the single-target spell is there too
    /// (<see cref="ReplaceMassSpells"/>): the earth elemental is immune to both chain lightning and lightning bolt, and
    /// printing both would say the same thing twice. The remaining lightning bolt prints as <c>"Lightning"</c>, the
    /// one spell the reference renames here.
    /// </para>
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <returns>One sentence per attribute, each with a full stop; empty for a creature without any.</returns>
    public static List<string> GetPropertiesDescription(MonsterType monster)
    {
        List<string> output = [];

        SortedDictionary<int, List<int>> immuneToSpells = [];
        SortedDictionary<int, List<int>> reducedDamageFromSpells = [];

        foreach (MonsterAbilityInstance ability in MonsterAbilities.GetInstances(monster))
        {
            if (ability.Type == MonsterAbility.ImmuneToCertainSpell)
            {
                AddSpell(immuneToSpells, ability.Percentage, ability.Spell);
                continue;
            }

            if (ability.Type == MonsterAbility.CertainSpellDamageReduction)
            {
                AddSpell(reducedDamageFromSpells, ability.Percentage, ability.Spell);
                continue;
            }

            string description = GetAbilityDescription(ability, ignoreBasicAbilities: true);

            if (description.Length != 0)
            {
                output.Add(description + '.');
            }
        }

        foreach (KeyValuePair<int, List<int>> group in immuneToSpells)
        {
            output.Add((group.Key == 100 ? "Immune to " : $"{group.Key}% immunity to ") + ListSpells(group.Value));
        }

        foreach (KeyValuePair<int, List<int>> group in reducedDamageFromSpells)
        {
            output.Add($"{group.Key}% damage from " + ListSpells(group.Value));
        }

        SortedDictionary<int, List<int>> extraDamageFromSpells = [];

        foreach (MonsterWeaknessInstance weakness in MonsterWeaknesses.GetInstances(monster))
        {
            if (weakness.Type == MonsterWeaknessType.ExtraDamageFromCertainSpell)
            {
                AddSpell(extraDamageFromSpells, weakness.Percentage, weakness.Spell);
                continue;
            }

            string description = GetWeaknessDescription(weakness, ignoreBasicWeaknesses: true);

            if (description.Length != 0)
            {
                output.Add(description + '.');
            }
        }

        foreach (KeyValuePair<int, List<int>> group in extraDamageFromSpells)
        {
            output.Add($"{group.Key + 100}% damage from " + ListSpells(group.Value));
        }

        return output;
    }

    /// <summary>
    /// The sentences of one percentage, <c>std::map&lt;uint32_t, std::vector&lt;int&gt;&gt;::operator[]</c> plus
    /// <c>emplace_back</c>.
    /// </summary>
    private static void AddSpell(SortedDictionary<int, List<int>> groups, int percentage, SpellType spell)
    {
        if (!groups.TryGetValue(percentage, out List<int>? spells))
        {
            spells = [];
            groups[percentage] = spells;
        }

        spells.Add((int)spell);
    }

    /// <summary>
    /// The spells of one line, <c>", "</c>-separated and closed with a full stop, <c>listSpells()</c> of
    /// <c>getMonsterPropertiesDescription()</c>.
    /// </summary>
    /// <remarks>
    /// A lightning bolt is printed as <c>"Lightning"</c> — the reference's own word, and the only renaming here.
    /// </remarks>
    private static string ListSpells(IEnumerable<int> spellIds)
    {
        StringBuilder result = new();

        foreach (int id in ReplaceMassSpells(spellIds))
        {
            if (result.Length != 0)
            {
                result.Append(", ");
            }

            Spell spell = new(id);

            result.Append(spell.Type == SpellType.LightningBolt ? "Lightning" : spell.GetName());
        }

        return result.Append('.').ToString();
    }

    /// <summary>
    /// The spell list without the mass versions of the spells it already has, <c>replaceMassSpells()</c>
    /// (<c>monster_info.cpp:573-596</c>).
    /// </summary>
    /// <remarks>
    /// A set first — the reference reads <c>std::set&lt;int&gt;</c> — so the answer is sorted by id and duplicates
    /// collapse; the reference's own tables can name both a mass spell and its single-target version (the crusader is
    /// immune to both curse spells), and one line saying "Curse" is enough.
    /// </remarks>
    private static List<int> ReplaceMassSpells(IEnumerable<int> spellIds)
    {
        SortedSet<int> sorted = new(spellIds);

        foreach ((SpellType mass, SpellType single) in MassSpellPairs)
        {
            if (sorted.Contains((int)mass) && sorted.Contains((int)single))
            {
                sorted.Remove((int)mass);
            }
        }

        return [.. sorted];
    }

    /// <summary>
    /// The eight mass/single pairs of <c>removeDuplicateSpell()</c> calls in <c>replaceMassSpells()</c>.
    /// </summary>
    private static readonly (SpellType Mass, SpellType Single)[] MassSpellPairs =
    [
        (SpellType.ChainLightning, SpellType.LightningBolt),
        (SpellType.MassCure, SpellType.Cure),
        (SpellType.MassCurse, SpellType.Curse),
        (SpellType.MassBless, SpellType.Bless),
        (SpellType.MassHaste, SpellType.Haste),
        (SpellType.MassShield, SpellType.Shield),
        (SpellType.MassDispel, SpellType.Dispel),
        (SpellType.MassSlow, SpellType.Slow),
    ];

    /// <summary>
    /// What a spell caster says, the <c>SPELL_CASTER</c> arm of <c>getMonsterAbilityDescription()</c>.
    /// </summary>
    /// <remarks>
    /// Five spells have a sentence of their own because the sentence is about the effect and not about the spell —
    /// "20% chance to Paralyze" reads better than "20% chance to cast Paralyze spell". Everything else, fireball
    /// included, goes through the general form.
    /// </remarks>
    private static string GetSpellCasterDescription(MonsterAbilityInstance ability) => ability.Spell switch
    {
        SpellType.Dispel => $"{ability.Percentage}% chance to Dispel beneficial spells",
        SpellType.Paralyze => $"{ability.Percentage}% chance to Paralyze",
        SpellType.Petrify => $"{ability.Percentage}% chance to Petrify",
        SpellType.Blind => $"{ability.Percentage}% chance to Blind",
        SpellType.Curse => $"{ability.Percentage}% chance to Curse",
        _ => $"{ability.Percentage}% chance to cast {new Spell(ability.Spell).GetName()} spell",
    };
}
