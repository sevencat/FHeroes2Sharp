using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// <c>fheroes2::MonsterAbilityType</c> of <c>src/fheroes2/monster/monster_info.h:35</c>: the abilities a
/// creature has in battle.
/// </summary>
/// <remarks>
/// The reference stores one <c>MonsterAbility</c> object per ability and the object carries two extra
/// numbers — a percentage and a spell id — which the battle engine reads (<c>MAGIC_RESISTANCE, 100, 0</c>,
/// <c>SPELL_CASTER, 20, Spell::BLIND</c>). <see cref="MonsterAbilityInstance"/> is that object; the mask of
/// this enumeration is derived from the instances and answers the one question everything outside the battle
/// engine asks — "does this creature have this ability".
/// </remarks>
[Flags]
public enum MonsterAbility : ulong
{
    /// <summary>No ability at all.</summary>
    None = 0,

    /// <summary><c>DOUBLE_HEX_SIZE</c>: the creature occupies two hexes.</summary>
    DoubleHexSize = 1ul << 1,

    /// <summary><c>FLYING</c>.</summary>
    Flying = 1ul << 2,

    /// <summary><c>DRAGON</c>: the creature counts as a dragon for dragon slayer and the like.</summary>
    Dragon = 1ul << 3,

    /// <summary><c>EARTH_CREATURE</c>.</summary>
    EarthCreature = 1ul << 4,

    /// <summary><c>AIR_CREATURE</c>.</summary>
    AirCreature = 1ul << 5,

    /// <summary><c>FIRE_CREATURE</c>.</summary>
    FireCreature = 1ul << 6,

    /// <summary><c>WATER_CREATURE</c>.</summary>
    WaterCreature = 1ul << 7,

    /// <summary><c>UNDEAD</c>: morale does not apply and life magic cannot heal it.</summary>
    Undead = 1ul << 8,

    /// <summary><c>ELEMENTAL</c>: morale does not apply, mind spells cannot touch it.</summary>
    Elemental = 1ul << 9,

    /// <summary><c>DOUBLE_SHOOTING</c>: shoots twice.</summary>
    DoubleShooting = 1ul << 10,

    /// <summary><c>DOUBLE_MELEE_ATTACK</c>: strikes twice in melee.</summary>
    DoubleMeleeAttack = 1ul << 11,

    /// <summary><c>DOUBLE_DAMAGE_TO_UNDEAD</c>.</summary>
    DoubleDamageToUndead = 1ul << 12,

    /// <summary><c>MAGIC_RESISTANCE</c>.</summary>
    MagicResistance = 1ul << 13,

    /// <summary><c>MIND_SPELL_IMMUNITY</c>.</summary>
    MindSpellImmunity = 1ul << 14,

    /// <summary><c>ELEMENTAL_SPELL_IMMUNITY</c>.</summary>
    ElementalSpellImmunity = 1ul << 15,

    /// <summary><c>FIRE_SPELL_IMMUNITY</c>.</summary>
    FireSpellImmunity = 1ul << 16,

    /// <summary><c>COLD_SPELL_IMMUNITY</c>.</summary>
    ColdSpellImmunity = 1ul << 17,

    /// <summary><c>IMMUNE_TO_CERTAIN_SPELL</c>: immunity to the one spell the reference names.</summary>
    ImmuneToCertainSpell = 1ul << 18,

    /// <summary><c>ELEMENTAL_SPELL_DAMAGE_REDUCTION</c>.</summary>
    ElementalSpellDamageReduction = 1ul << 19,

    /// <summary><c>CERTAIN_SPELL_DAMAGE_REDUCTION</c>.</summary>
    CertainSpellDamageReduction = 1ul << 20,

    /// <summary><c>SPELL_CASTER</c>: casts a spell in battle.</summary>
    SpellCaster = 1ul << 21,

    /// <summary><c>HP_REGENERATION</c>.</summary>
    HpRegeneration = 1ul << 22,

    /// <summary><c>TWO_CELL_MELEE_ATTACK</c>: melee hits the hex behind the target as well.</summary>
    TwoCellMeleeAttack = 1ul << 23,

    /// <summary><c>UNLIMITED_RETALIATION</c>.</summary>
    UnlimitedRetaliation = 1ul << 24,

    /// <summary><c>ALL_ADJACENT_CELL_MELEE_ATTACK</c>: hits every neighbouring stack.</summary>
    AllAdjacentCellMeleeAttack = 1ul << 25,

    /// <summary><c>NO_MELEE_PENALTY</c>.</summary>
    NoMeleePenalty = 1ul << 26,

    /// <summary><c>NO_ENEMY_RETALIATION</c>.</summary>
    NoEnemyRetaliation = 1ul << 27,

    /// <summary><c>HP_DRAIN</c>.</summary>
    HpDrain = 1ul << 28,

    /// <summary><c>AREA_SHOT</c>: a shot damages the stack behind the target too.</summary>
    AreaShot = 1ul << 29,

    /// <summary><c>MORAL_DECREMENT</c>: lowers the morale of the enemy.</summary>
    MoralDecrement = 1ul << 30,

    /// <summary><c>ENEMY_HALVING</c>: halves the enemy stack once per battle.</summary>
    EnemyHalving = 1ul << 31,

    /// <summary><c>SOUL_EATER</c>.</summary>
    SoulEater = 1ul << 32,
}

/// <summary>
/// One entry of a creature's ability list, <c>fheroes2::MonsterAbility</c> of
/// <c>src/fheroes2/monster/monster_info.h:174</c>.
/// </summary>
/// <remarks>
/// <para>
/// The two numbers mean different things per ability and the reference does not say which is which, so both
/// are copied verbatim: <c>MAGIC_RESISTANCE, 25, 0</c> is a chance in percent, <c>SPELL_CASTER, 20,
/// Spell::BLIND</c> is a chance and a spell, <c>MORAL_DECREMENT, 100, 1</c> is a chance and the amount of
/// morale the enemy loses, and <c>ENEMY_HALVING, 10, 0</c> is the chance to halve the enemy stack once.
/// </para>
/// <para>
/// Only <c>getMonsterBaseStrength()</c> reads them so far, and it only looks at <c>SPELL_CASTER</c> — through
/// <see cref="Spell"/>; the battle engine will read the rest.
/// </para>
/// </remarks>
/// <param name="Type">The ability.</param>
/// <param name="Percentage">The first number of the reference's constructor, zero when it passes none.</param>
/// <param name="Value">The second number, zero when it passes none.</param>
public readonly record struct MonsterAbilityInstance(MonsterAbility Type, int Percentage = 0, int Value = 0)
{
    /// <summary>The spell of a <see cref="MonsterAbility.SpellCaster"/> entry, the reference's <c>value</c>.</summary>
    public SpellType Spell => (SpellType)Value;
}

/// <summary>
/// The ability table of <c>src/fheroes2/monster/monster_info.cpp:388-590</c>: which creature has which
/// abilities.
/// </summary>
/// <remarks>
/// <para>
/// Just like <see cref="MonsterStatistics"/> the rows are copied one by one from the reference, in the
/// reference's own order, with a comment only where a row carries more than one ability. Creatures without
/// an entry — peasants, goblins, most of the low level stacks — have <see cref="MonsterAbility.None"/>, so
/// the dictionary holds the fifty creatures the reference calls out and the default answers for the rest.
/// </para>
/// <para>
/// This table exists because two very different computations need it. The morale of an army asks whether a
/// stack is undead (<c>Monster::isUndead()</c>), and the battle engine asks about fifteen of the other bits.
/// The adventure map only needs presence, which is why <see cref="Get"/> returns a mask — see
/// <see cref="MonsterAbility"/> for what the reference stores next to the bit.
/// </para>
/// </remarks>
public static class MonsterAbilities
{
    private static readonly Dictionary<MonsterType, MonsterAbilityInstance[]> Table = new()
    {
        [MonsterType.Ranger] = [new(MonsterAbility.DoubleShooting)],
        [MonsterType.Cavalry] = [new(MonsterAbility.DoubleHexSize)],
        [MonsterType.Champion] = [new(MonsterAbility.DoubleHexSize)],
        [MonsterType.Paladin] = [new(MonsterAbility.DoubleMeleeAttack)],

        // The crusader is immune to both curse spells, one entry each.
        [MonsterType.Crusader] =
        [
            new(MonsterAbility.DoubleMeleeAttack),
            new(MonsterAbility.DoubleDamageToUndead),
            new(MonsterAbility.ImmuneToCertainSpell, 100, (int)SpellType.Curse),
            new(MonsterAbility.ImmuneToCertainSpell, 100, (int)SpellType.MassCurse),
        ],
        [MonsterType.Wolf] = [new(MonsterAbility.DoubleHexSize), new(MonsterAbility.DoubleMeleeAttack)],
        [MonsterType.Troll] = [new(MonsterAbility.HpRegeneration)],
        [MonsterType.WarTroll] = [new(MonsterAbility.HpRegeneration)],
        [MonsterType.Cyclops] = [new(MonsterAbility.TwoCellMeleeAttack), new(MonsterAbility.SpellCaster, 20, (int)SpellType.Paralyze)],
        [MonsterType.Sprite] = [new(MonsterAbility.NoEnemyRetaliation), new(MonsterAbility.Flying)],
        [MonsterType.Dwarf] = [new(MonsterAbility.MagicResistance, 25, 0)],
        [MonsterType.BattleDwarf] = [new(MonsterAbility.MagicResistance, 25, 0)],
        [MonsterType.Elf] = [new(MonsterAbility.DoubleShooting)],
        [MonsterType.GrandElf] = [new(MonsterAbility.DoubleShooting)],
        [MonsterType.Unicorn] = [new(MonsterAbility.DoubleHexSize), new(MonsterAbility.SpellCaster, 20, (int)SpellType.Blind)],
        [MonsterType.Phoenix] =
        [
            new(MonsterAbility.DoubleHexSize),
            new(MonsterAbility.TwoCellMeleeAttack),
            new(MonsterAbility.Flying),
            new(MonsterAbility.ElementalSpellImmunity),
        ],
        [MonsterType.Centaur] = [new(MonsterAbility.DoubleHexSize)],
        [MonsterType.Gargoyle] = [new(MonsterAbility.Flying)],
        [MonsterType.Griffin] = [new(MonsterAbility.DoubleHexSize), new(MonsterAbility.Flying), new(MonsterAbility.UnlimitedRetaliation)],
        [MonsterType.Hydra] =
        [
            new(MonsterAbility.DoubleHexSize),
            new(MonsterAbility.AllAdjacentCellMeleeAttack),
            new(MonsterAbility.NoEnemyRetaliation),
        ],

        // The three living dragons: magic resistant, two-hex, flying, dragon, and the melee hit reaches behind.
        [MonsterType.GreenDragon] =
        [
            new(MonsterAbility.Dragon),
            new(MonsterAbility.DoubleHexSize),
            new(MonsterAbility.Flying),
            new(MonsterAbility.MagicResistance, 100, 0),
            new(MonsterAbility.TwoCellMeleeAttack),
        ],
        [MonsterType.RedDragon] =
        [
            new(MonsterAbility.Dragon),
            new(MonsterAbility.DoubleHexSize),
            new(MonsterAbility.Flying),
            new(MonsterAbility.MagicResistance, 100, 0),
            new(MonsterAbility.TwoCellMeleeAttack),
        ],
        [MonsterType.BlackDragon] =
        [
            new(MonsterAbility.Dragon),
            new(MonsterAbility.DoubleHexSize),
            new(MonsterAbility.Flying),
            new(MonsterAbility.MagicResistance, 100, 0),
            new(MonsterAbility.TwoCellMeleeAttack),
        ],
        [MonsterType.Boar] = [new(MonsterAbility.DoubleHexSize)],

        // Both golems reduce elemental damage and the damage of one named spell (armageddon).
        [MonsterType.IronGolem] =
        [
            new(MonsterAbility.ElementalSpellDamageReduction, 50, 0),
            new(MonsterAbility.CertainSpellDamageReduction, 50, (int)SpellType.Armageddon),
        ],
        [MonsterType.SteelGolem] =
        [
            new(MonsterAbility.ElementalSpellDamageReduction, 50, 0),
            new(MonsterAbility.CertainSpellDamageReduction, 50, (int)SpellType.Armageddon),
        ],
        [MonsterType.Roc] = [new(MonsterAbility.DoubleHexSize), new(MonsterAbility.Flying)],
        [MonsterType.Mage] = [new(MonsterAbility.NoMeleePenalty)],
        [MonsterType.Archmage] = [new(MonsterAbility.NoMeleePenalty), new(MonsterAbility.SpellCaster, 20, (int)SpellType.Dispel)],
        [MonsterType.Giant] = [new(MonsterAbility.MindSpellImmunity)],
        [MonsterType.Titan] = [new(MonsterAbility.NoMeleePenalty), new(MonsterAbility.MindSpellImmunity)],

        // The necropolis. Every one of them is undead, which is why the whole line is repeated.
        [MonsterType.Skeleton] = [new(MonsterAbility.Undead)],
        [MonsterType.Zombie] = [new(MonsterAbility.Undead)],
        [MonsterType.MutantZombie] = [new(MonsterAbility.Undead)],
        [MonsterType.Mummy] = [new(MonsterAbility.Undead), new(MonsterAbility.SpellCaster, 20, (int)SpellType.Curse)],
        [MonsterType.RoyalMummy] = [new(MonsterAbility.Undead), new(MonsterAbility.SpellCaster, 30, (int)SpellType.Curse)],
        [MonsterType.Vampire] = [new(MonsterAbility.Undead), new(MonsterAbility.Flying), new(MonsterAbility.NoEnemyRetaliation)],
        [MonsterType.VampireLord] =
        [
            new(MonsterAbility.Undead),
            new(MonsterAbility.Flying),
            new(MonsterAbility.NoEnemyRetaliation),
            new(MonsterAbility.HpDrain),
        ],
        [MonsterType.Lich] = [new(MonsterAbility.Undead), new(MonsterAbility.AreaShot)],
        [MonsterType.PowerLich] = [new(MonsterAbility.Undead), new(MonsterAbility.AreaShot)],

        // Bone dragon: undead on top of the three living dragons, and it lowers the enemy morale.
        [MonsterType.BoneDragon] =
        [
            new(MonsterAbility.Undead),
            new(MonsterAbility.Dragon),
            new(MonsterAbility.DoubleHexSize),
            new(MonsterAbility.Flying),
            new(MonsterAbility.MoralDecrement, 100, 1),
        ],
        [MonsterType.Rogue] = [new(MonsterAbility.NoEnemyRetaliation)],
        [MonsterType.Ghost] = [new(MonsterAbility.SoulEater), new(MonsterAbility.Flying), new(MonsterAbility.Undead)],
        [MonsterType.Genie] = [new(MonsterAbility.EnemyHalving, 10, 0), new(MonsterAbility.Flying)],
        [MonsterType.Medusa] = [new(MonsterAbility.DoubleHexSize), new(MonsterAbility.SpellCaster, 20, (int)SpellType.Petrify)],
        [MonsterType.Nomad] = [new(MonsterAbility.DoubleHexSize)],

        // The four elementals: elemental on top of the immunity their own element gives them.
        [MonsterType.AirElement] =
        [
            new(MonsterAbility.Elemental),
            new(MonsterAbility.AirCreature),
            new(MonsterAbility.ImmuneToCertainSpell, 100, (int)SpellType.MeteorShower),
        ],
        [MonsterType.EarthElement] =
        [
            new(MonsterAbility.Elemental),
            new(MonsterAbility.EarthCreature),
            new(MonsterAbility.ImmuneToCertainSpell, 100, (int)SpellType.ChainLightning),
            new(MonsterAbility.ImmuneToCertainSpell, 100, (int)SpellType.ElementalStorm),
            new(MonsterAbility.ImmuneToCertainSpell, 100, (int)SpellType.LightningBolt),
        ],
        [MonsterType.FireElement] =
        [
            new(MonsterAbility.Elemental),
            new(MonsterAbility.FireCreature),
            new(MonsterAbility.FireSpellImmunity),
        ],
        [MonsterType.WaterElement] =
        [
            new(MonsterAbility.Elemental),
            new(MonsterAbility.WaterCreature),
            new(MonsterAbility.ColdSpellImmunity),
        ],
    };

    private static readonly MonsterAbilityInstance[] NoAbilities = [];

    /// <summary>The mask of every row, folded once from <see cref="Table"/>.</summary>
    private static readonly Dictionary<MonsterType, MonsterAbility> Masks = BuildMasks();

    private static Dictionary<MonsterType, MonsterAbility> BuildMasks()
    {
        Dictionary<MonsterType, MonsterAbility> masks = new(Table.Count);

        foreach ((MonsterType monster, MonsterAbilityInstance[] instances) in Table)
        {
            MonsterAbility mask = MonsterAbility.None;

            foreach (MonsterAbilityInstance instance in instances)
            {
                mask |= instance.Type;
            }

            masks[monster] = mask;
        }

        return masks;
    }

    /// <summary>
    /// The abilities of a creature, <c>Monster::getAbilities()</c> of the reference's base class.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The mask; <see cref="MonsterAbility.None"/> for the creatures without a row.</returns>
    public static MonsterAbility Get(MonsterType monster) => Masks.TryGetValue(monster, out MonsterAbility abilities) ? abilities : MonsterAbility.None;

    /// <summary>
    /// The ability entries of a creature, in the reference's order, <c>Monster::getAbilities()</c>.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The entries with their numbers; empty for the creatures without a row.</returns>
    public static IReadOnlyList<MonsterAbilityInstance> GetInstances(MonsterType monster) =>
        Table.TryGetValue(monster, out MonsterAbilityInstance[]? instances) ? instances : NoAbilities;

    /// <summary>
    /// The entry of one ability, <c>std::find(abilities.begin(), abilities.end(), type)</c> in
    /// <c>monster_info.cpp</c>.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <param name="ability">The ability to look for.</param>
    /// <param name="instance">The entry with its numbers.</param>
    /// <returns><see langword="true"/> when the creature has the ability.</returns>
    public static bool TryGetInstance(MonsterType monster, MonsterAbility ability, out MonsterAbilityInstance instance)
    {
        foreach (MonsterAbilityInstance candidate in GetInstances(monster))
        {
            if (candidate.Type == ability)
            {
                instance = candidate;
                return true;
            }
        }

        instance = default;
        return false;
    }

    /// <summary>
    /// Whether a creature has an ability, <c>fheroes2::isAbilityPresent()</c>
    /// (<c>monster_info.cpp</c>).
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <param name="ability">The ability to look for.</param>
    /// <returns><see langword="true"/> when the mask has the bit.</returns>
    public static bool IsPresent(MonsterType monster, MonsterAbility ability) => (Get(monster) & ability) == ability;

    /// <summary>Whether the creature is undead, <c>Monster::isUndead()</c> (<c>monster.h:237</c>).</summary>
    /// <param name="monster">The creature.</param>
    /// <returns><see langword="true"/> for the necropolis line and the ghost.</returns>
    public static bool IsUndead(MonsterType monster) => IsPresent(monster, MonsterAbility.Undead);

    /// <summary>Whether the creature is an elemental, <c>Monster::isElemental()</c>.</summary>
    /// <param name="monster">The creature.</param>
    /// <returns><see langword="true"/> for the four elementals.</returns>
    public static bool IsElemental(MonsterType monster) => IsPresent(monster, MonsterAbility.Elemental);

    /// <summary>Whether the creature flies, <c>Monster::isFlying()</c>.</summary>
    /// <param name="monster">The creature.</param>
    /// <returns><see langword="true"/> when movement ignores the ground.</returns>
    public static bool IsFlying(MonsterType monster) => IsPresent(monster, MonsterAbility.Flying);

    /// <summary>Whether the creature is a dragon, <c>Monster::isDragons()</c>.</summary>
    /// <param name="monster">The creature.</param>
    /// <returns><see langword="true"/> for the four dragons.</returns>
    public static bool IsDragons(MonsterType monster) => IsPresent(monster, MonsterAbility.Dragon);

    /// <summary>Whether the creature takes two hexes, <c>Monster::isWide()</c>.</summary>
    /// <param name="monster">The creature.</param>
    /// <returns><see langword="true"/> when it occupies two hexes on the battlefield.</returns>
    public static bool IsWide(MonsterType monster) => IsPresent(monster, MonsterAbility.DoubleHexSize);

    /// <summary>
    /// Whether morale applies to the creature, <c>Monster::isAffectedByMorale()</c>
    /// (<c>monster.h:287</c>): everything except the undead and the elementals.
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns><see langword="true"/> when the stack can freeze or strike again from morale.</returns>
    public static bool IsAffectedByMorale(MonsterType monster) => !IsUndead(monster) && !IsElemental(monster);
}
