using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// <c>fheroes2::MonsterWeaknessType</c> of <c>src/fheroes2/monster/monster_info.h:186</c>: the things a
/// creature takes extra damage from.
/// </summary>
/// <remarks>
/// The first four entries are "basic" — the reference never shows them in the creature's description, they
/// only matter to the damage calculation; the rest are shown. <see cref="MonsterWeaknesses.IsBasic"/> draws
/// that line.
/// </remarks>
public enum MonsterWeaknessType
{
    /// <summary>No weakness at all.</summary>
    None = 0,

    /// <summary><c>DOUBLE_DAMAGE_FROM_EARTH_CREATURES</c>: the air elemental's.</summary>
    DoubleDamageFromEarthCreatures,

    /// <summary><c>DOUBLE_DAMAGE_FROM_AIR_CREATURES</c>: the earth elemental's.</summary>
    DoubleDamageFromAirCreatures,

    /// <summary><c>DOUBLE_DAMAGE_FROM_FIRE_CREATURES</c>: the water elemental's.</summary>
    DoubleDamageFromFireCreatures,

    /// <summary><c>DOUBLE_DAMAGE_FROM_WATER_CREATURES</c>: the fire elemental's.</summary>
    DoubleDamageFromWaterCreatures,

    /// <summary><c>DOUBLE_DAMAGE_FROM_FIRE_SPELLS</c>: the water elemental's.</summary>
    DoubleDamageFromFireSpells,

    /// <summary><c>DOUBLE_DAMAGE_FROM_COLD_SPELLS</c>: the fire elemental's.</summary>
    DoubleDamageFromColdSpells,

    /// <summary>
    /// <c>EXTRA_DAMAGE_FROM_CERTAIN_SPELL</c>: one named spell hurts more by <c>percentage</c> percent.
    /// </summary>
    ExtraDamageFromCertainSpell,
}

/// <summary>
/// One entry of a creature's weakness list, <c>fheroes2::MonsterWeakness</c> of
/// <c>src/fheroes2/monster/monster_info.h:186</c>.
/// </summary>
/// <remarks>
/// Same shape as <see cref="MonsterAbilityInstance"/>: two numbers whose meaning depends on the type.
/// <c>EXTRA_DAMAGE_FROM_CERTAIN_SPELL, 100, Spell::CHAINLIGHTNING</c> is a percentage and a spell; the other
/// types carry neither and are copied with zeroes.
/// </remarks>
/// <param name="Type">The weakness.</param>
/// <param name="Percentage">The extra damage in percent, zero when the reference passes none.</param>
/// <param name="Value">The spell of an <see cref="MonsterWeaknessType.ExtraDamageFromCertainSpell"/>.</param>
public readonly record struct MonsterWeaknessInstance(MonsterWeaknessType Type, int Percentage = 0, int Value = 0)
{
    /// <summary>The spell of an <see cref="MonsterWeaknessType.ExtraDamageFromCertainSpell"/> entry.</summary>
    public SpellType Spell => (SpellType)Value;
}

/// <summary>
/// The weakness table of <c>src/fheroes2/monster/monster_info.cpp:531-561</c>: which creature is weak to what.
/// </summary>
/// <remarks>
/// <para>
/// Only the four elementals have weaknesses in this version of the reference, and they come in the same
/// pairs as their immunities: the air elemental is immune to meteor shower and takes double damage from the
/// three lightning spells, the earth elemental is the mirror image, the fire elemental burns twice from cold
/// spells and the water elemental from fire spells.
/// </para>
/// <para>
/// The order inside a row is the reference's, and it matters: <c>getSpellResistance()</c> walks the list and
/// the battle engine will too.
/// </para>
/// </remarks>
public static class MonsterWeaknesses
{
    private static readonly Dictionary<MonsterType, MonsterWeaknessInstance[]> Table = new()
    {
        [MonsterType.AirElement] =
        [
            new(MonsterWeaknessType.ExtraDamageFromCertainSpell, 100, (int)SpellType.ChainLightning),
            new(MonsterWeaknessType.ExtraDamageFromCertainSpell, 100, (int)SpellType.ElementalStorm),
            new(MonsterWeaknessType.ExtraDamageFromCertainSpell, 100, (int)SpellType.LightningBolt),
            new(MonsterWeaknessType.DoubleDamageFromEarthCreatures),
        ],
        [MonsterType.EarthElement] =
        [
            new(MonsterWeaknessType.ExtraDamageFromCertainSpell, 100, (int)SpellType.MeteorShower),
            new(MonsterWeaknessType.DoubleDamageFromAirCreatures),
        ],
        [MonsterType.FireElement] =
        [
            new(MonsterWeaknessType.DoubleDamageFromColdSpells),
            new(MonsterWeaknessType.DoubleDamageFromWaterCreatures),
        ],
        [MonsterType.WaterElement] =
        [
            new(MonsterWeaknessType.DoubleDamageFromFireSpells),
            new(MonsterWeaknessType.DoubleDamageFromFireCreatures),
        ],
    };

    private static readonly MonsterWeaknessInstance[] NoWeaknesses = [];

    /// <summary>
    /// The weaknesses of a creature, <c>Monster::getWeaknesses()</c> of the reference's base class.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The entries in the reference's order; empty for the creatures without a row.</returns>
    public static IReadOnlyList<MonsterWeaknessInstance> GetInstances(MonsterType monster) =>
        Table.TryGetValue(monster, out MonsterWeaknessInstance[]? instances) ? instances : NoWeaknesses;

    /// <summary>
    /// The entry of one weakness, <c>std::find( weaknesses.begin(), weaknesses.end(), type )</c>.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <param name="weakness">The weakness to look for.</param>
    /// <param name="instance">The entry with its numbers.</param>
    /// <returns><see langword="true"/> when the creature has the weakness.</returns>
    public static bool TryGetInstance(MonsterType monster, MonsterWeaknessType weakness, out MonsterWeaknessInstance instance)
    {
        foreach (MonsterWeaknessInstance candidate in GetInstances(monster))
        {
            if (candidate.Type == weakness)
            {
                instance = candidate;
                return true;
            }
        }

        instance = default;
        return false;
    }

    /// <summary>
    /// Whether a creature has a weakness, <c>fheroes2::isWeaknessPresent()</c>
    /// (<c>monster_info.cpp:947</c>) and <c>Monster::isWeaknessPresent()</c> (<c>monster.cpp:227</c>).
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <param name="weakness">The weakness to look for.</param>
    /// <returns><see langword="true"/> when the creature has an entry of that type.</returns>
    public static bool IsPresent(MonsterType monster, MonsterWeaknessType weakness)
    {
        foreach (MonsterWeaknessInstance instance in GetInstances(monster))
        {
            if (instance.Type == weakness)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a weakness is one of the four the reference hides from the creature's description,
    /// <c>getMonsterWeaknessDescription( weakness, ignoreBasicWeaknesses )</c>.
    /// </summary>
    /// <param name="weakness">The weakness.</param>
    /// <returns><see langword="true"/> for the four "double damage from creatures" entries.</returns>
    public static bool IsBasic(MonsterWeaknessType weakness) =>
        weakness is MonsterWeaknessType.DoubleDamageFromEarthCreatures
            or MonsterWeaknessType.DoubleDamageFromAirCreatures
            or MonsterWeaknessType.DoubleDamageFromFireCreatures
            or MonsterWeaknessType.DoubleDamageFromWaterCreatures;
}
