using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The spell-to-creature constructor, <c>Monster::Monster( const Spell &amp; sp )</c>
/// (<c>src/fheroes2/monster/monster.cpp:77-108</c>).
/// </summary>
/// <remarks>
/// The eight summon and guardian spells name the elemental they call, and <c>HAUNT</c> is the only spell that
/// calls a ghost. Everything else — the damage spells, the buffs, the adventure spells — puts no creature on
/// the battlefield, so they answer <see cref="MonsterType.Unknown"/>, which is the reference's <c>UNKNOWN</c>
/// initial value.
/// </remarks>
public static class MonsterSummons
{
    /// <summary>
    /// The creature a spell summons or guards with, <c>Monster::Monster( const Spell &amp; )</c>.
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <returns>The creature, or <see cref="MonsterType.Unknown"/> for a spell that summons none.</returns>
    public static MonsterType FromSpell(SpellType spell) => spell switch
    {
        SpellType.SetEGuardian or SpellType.SummonEElement => MonsterType.EarthElement,
        SpellType.SetAGuardian or SpellType.SummonAElement => MonsterType.AirElement,
        SpellType.SetFGuardian or SpellType.SummonFElement => MonsterType.FireElement,
        SpellType.SetWGuardian or SpellType.SummonWElement => MonsterType.WaterElement,
        SpellType.Haunt => MonsterType.Ghost,
        _ => MonsterType.Unknown,
    };
}
