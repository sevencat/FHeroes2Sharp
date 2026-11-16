using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// <c>Speed::</c> of <c>src/fheroes2/kingdom/speed.h</c>, in the same order and with the same values: the
/// map stores the speed of a stack, so the numbers are part of the map format.
/// </summary>
public enum MonsterSpeed : uint
{
    Standing = 0,
    Crawling = 1,
    VerySlow = 2,
    Slow = 3,
    Average = 4,
    Fast = 5,
    VeryFast = 6,
    UltraFast = 7,
    Blazing = 8,
    Instant = 9,
}

/// <summary>
/// <c>fheroes2::MonsterBattleStats</c> minus the parts the port does not have (<c>monster_info.h:160</c>).
/// </summary>
/// <remarks>
/// The reference also carries <c>abilities</c> and <c>weaknesses</c> (vectors of <c>MonsterAbilityType</c>)
/// and a cached <c>monsterBaseStrength</c> computed from them. The abilities live in
/// <see cref="MonsterAbilities"/> and the two strengths are computed here from them; the weaknesses and the
/// <c>icnIds</c>/sounds are battle-side data that waits for the battle engine and the renderer.
/// </remarks>
/// <param name="Attack">Attack skill.</param>
/// <param name="Defense">Defense skill.</param>
/// <param name="DamageMin">Lowest damage of one creature without bonuses.</param>
/// <param name="DamageMax">Highest damage of one creature without bonuses.</param>
/// <param name="HitPoints">Hit points of one creature.</param>
/// <param name="Speed">Speed of the stack.</param>
/// <param name="Shots">Shots per battle, zero for melee-only creatures.</param>
public readonly record struct MonsterBattleStats(
    int Attack,
    int Defense,
    int DamageMin,
    int DamageMax,
    int HitPoints,
    MonsterSpeed Speed,
    int Shots)
{
    /// <summary>Whether the creature can shoot, <c>shots &gt; 0</c>.</summary>
    public bool IsArchers => Shots > 0;
}

/// <summary>
/// <c>fheroes2::MonsterGeneralStats</c> minus the two names (<c>monster_info.h:179</c>).
/// </summary>
/// <remarks>
/// The names are translated strings with plural forms, which the port has no text layer for; every other
/// field is a number the game logic reads.
/// </remarks>
/// <param name="BaseGrowth">Creatures added to the dwelling per week.</param>
/// <param name="Race">Race the creature belongs to, <see cref="MapRace.None"/> for the neutral ones.</param>
/// <param name="Level">Dwelling level, one to six.</param>
/// <param name="Cost">Cost of one creature.</param>
public readonly record struct MonsterGeneralStats(MapRace Race, int Level, uint BaseGrowth, Cost Cost)
{
    /// <summary>The cost of one creature as <c>Funds</c>, <c>Monster::GetCost()</c> (<c>monster.h:306</c>).</summary>
    public Funds GetCost() => new(Cost);
}

/// <summary>
/// The numeric half of <c>fheroes2::monsterData</c> (<c>monster_info.cpp:233-382</c>): the battle stats and
/// the general stats of every monster, in the order of <see cref="MonsterType"/>.
/// </summary>
/// <remarks>
/// <para>
/// The arrays are filled row by row from the reference, with the reference's comment as the row header so
/// that a mistyped row can be found by name. The row count is asserted once, at first use: the two tables
/// must have one entry per <see cref="MonsterType"/>, and a missing row would silently shift every monster
/// after it onto the wrong stats — the kind of bug that only shows up as "the dragons are weak".
/// </para>
/// <para>
/// The reference resolves a <c>Monster</c> to its row through <c>getMonsterData()</c>; the port takes the
/// <see cref="MonsterType"/> directly, which is what that function keys on.
/// </para>
/// </remarks>
public static class MonsterStatistics
{
    /// <summary>The battle stats of a monster, <c>Monster::GetAttack()</c> and friends.</summary>
    /// <param name="monster">Monster to look up.</param>
    /// <returns>The row of <paramref name="monster"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="monster"/> is not a known monster.</exception>
    public static MonsterBattleStats GetBattleStats(MonsterType monster)
    {
        int index = (int)monster;

        if (index < 0 || index >= BattleStats.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(monster), monster, "Not a monster type.");
        }

        return BattleStats[index];
    }

    /// <summary>The general stats of a monster, <c>Monster::GetMonsterLevel()</c> and friends.</summary>
    /// <param name="monster">Monster to look up.</param>
    /// <returns>The row of <paramref name="monster"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="monster"/> is not a known monster.</exception>
    public static MonsterGeneralStats GetGeneralStats(MonsterType monster)
    {
        int index = (int)monster;

        if (index < 0 || index >= GeneralStats.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(monster), monster, "Not a monster type.");
        }

        return GeneralStats[index];
    }

    /// <summary>
    /// The strength of one creature against a default opponent, <c>Monster::GetMonsterStrength()</c>
    /// (<c>monster.cpp:105</c>: <c>attackDefenseFactor * baseStrength</c>, i.e. <c>1 + attack * 0.1 +
    /// defense * 0.05</c> times the base strength).
    /// </summary>
    /// <remarks>
    /// The reference lets a subclass override <c>GetAttack()</c>/<c>GetDefense()</c> — the battle engine uses
    /// that to pass a hero's or a spell's bonus in. There are no subclasses here, so the bonuses come in as
    /// arguments and a negative value means "take the stats of the creature".
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <param name="attack">Attack skill to use, or a negative value for the creature's own.</param>
    /// <param name="defense">Defense skill to use, or a negative value for the creature's own.</param>
    /// <returns>The strength of one creature.</returns>
    public static double GetMonsterStrength(MonsterType monster, int attack = -1, int defense = -1)
    {
        MonsterBattleStats stats = GetBattleStats(monster);

        if (attack < 0)
        {
            attack = stats.Attack;
        }

        if (defense < 0)
        {
            defense = stats.Defense;
        }

        double attackDefense = 1.0 + attack * 0.1 + defense * 0.05;

        return attackDefense * GetMonsterBaseStrength(monster);
    }

    /// <summary>
    /// The number of creatures a wandering stack of this kind starts with, <c>Monster::GetRNDSize()</c>
    /// (<c>monster.cpp:165-220</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without an outlier most creatures roll half to the whole <b>stack size of their level</b> — a level one
    /// monster rolls 25 to 50, a level seven one 4 to 8 — and the reference keeps that table as
    /// <c>{ 0, 50, 30, 25, 25, 12, 8 }</c> indexed by level.
    /// </para>
    /// <para>
    /// The outliers are the creatures whose hit points make the general rule look wrong: peasants come in
    /// crowds of 80, rogues 40, pikemen and wolves 30, ghosts and medusae 20, minotaurs 16, cavalry 18,
    /// paladins and phoenixes 12. They are copied one by one, because the numbers do not follow a formula.
    /// </para>
    /// <para>
    /// The roll is inclusive on both ends (<c>Rand::Get( result / 2, result )</c>), and a table entry of one or
    /// less turns into a single creature rather than a zero-count stack.
    /// </para>
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <returns>The number of creatures, zero for an invalid monster.</returns>
    public static uint GetRandomCount(MonsterType monster)
    {
        if (!IsValid(monster))
        {
            return 0;
        }

        uint result = monster switch
        {
            MonsterType.Peasant => 80,
            MonsterType.Rogue => 40,
            MonsterType.Pikeman or MonsterType.VeteranPikeman or MonsterType.Wolf or MonsterType.Elf
                or MonsterType.GrandElf => 30,
            MonsterType.Gargoyle => 25,
            MonsterType.Ghost or MonsterType.Medusa => 20,
            MonsterType.Minotaur or MonsterType.MinotaurKing or MonsterType.Roc or MonsterType.Vampire
                or MonsterType.VampireLord or MonsterType.Unicorn => 16,
            MonsterType.Cavalry or MonsterType.Champion => 18,
            MonsterType.Paladin or MonsterType.Crusader or MonsterType.Cyclops or MonsterType.Phoenix => 12,

            // The general table, indexed by level; level zero never happens for a valid monster.
            _ => DefaultArmySizePerLevel[GetGeneralStats(monster).Level]
        };

        return result > 1 ? (uint)Rand.Get((int)(result / 2), (int)result) : 1;
    }

    /// <summary>
    /// The stack size of a creature of each level, <c>defaultArmySizePerLevel</c> (<c>monster.cpp:170</c>).
    /// </summary>
    private static readonly uint[] DefaultArmySizePerLevel = [0, 50, 30, 25, 25, 12, 8];

    /// <summary>
    /// The base strength of one creature — its stats and abilities with no bonuses —,
    /// <c>getMonsterBaseStrength()</c> (<c>monster_info.cpp:45-132</c>).
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>The base strength, zero for the unknown monster and the random markers.</returns>
    public static double GetMonsterBaseStrength(MonsterType monster)
    {
        int index = (int)monster;

        if (index < 0 || index >= BaseStrength.Value.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(monster), monster, "Not a monster type.");
        }

        return BaseStrength.Value[index];
    }

    /// <summary>
    /// <c>Monster::isValid()</c> (<c>monster.h:196</c>): neither the unknown monster nor one of the five
    /// editor-only random markers.
    /// </summary>
    /// <param name="monster">Monster to test.</param>
    /// <returns><see langword="true"/> for a real creature.</returns>
    public static bool IsValid(MonsterType monster) =>
        monster > MonsterType.Unknown && monster < MonsterType.RandomMonster;

    /// <summary>
    /// The base strength of every monster, folded once from the stats and the ability table.
    /// </summary>
    /// <remarks>
    /// The reference fills <c>battleStats.monsterBaseStrength</c> the same way at start-up. Every factor below
    /// is a guess the original authors wrote down as a comment; they are copied verbatim, including the ones
    /// that look odd (<c>DOUBLE_DAMAGE_TO_UNDEAD</c> is worth 1.15 because roughly a seventh of the creatures
    /// are undead).
    /// </remarks>
    private static readonly Lazy<double[]> BaseStrength = new(BuildBaseStrength);

    private static double[] BuildBaseStrength()
    {
        double[] strength = new double[BattleStats.Length];

        for (int index = 0; index < BattleStats.Length; index++)
        {
            MonsterType monster = (MonsterType)index;
            MonsterBattleStats stats = BattleStats[index];
            MonsterAbility abilities = MonsterAbilities.Get(monster);

            bool Has(MonsterAbility ability) => (abilities & ability) != 0;

            // A creature that takes no retaliation effectively has more hit points: it lives through the
            // attacks it would have eaten back.
            double effectiveHitPoints = stats.HitPoints * (Has(MonsterAbility.NoEnemyRetaliation) ? 1.4 : 1.0);

            double damagePotential = (stats.DamageMin + stats.DamageMax) / 2.0;

            if (Has(MonsterAbility.DoubleShooting))
            {
                Debug.Assert(stats.IsArchers, "DOUBLE_SHOOTING on a creature without shots.");

                damagePotential *= 2;
            }
            else if (Has(MonsterAbility.DoubleMeleeAttack))
            {
                // Melee attacker will lose potential on second attack after retaliation.
                damagePotential *= Has(MonsterAbility.NoEnemyRetaliation) ? 2 : 1.75;
            }

            if (Has(MonsterAbility.DoubleDamageToUndead))
            {
                damagePotential *= 1.15; // 15% of all Monsters are Undead, deals double damage.
            }

            if (Has(MonsterAbility.TwoCellMeleeAttack))
            {
                damagePotential *= 1.2;
            }

            if (Has(MonsterAbility.UnlimitedRetaliation))
            {
                damagePotential *= 1.25;
            }

            if (Has(MonsterAbility.AllAdjacentCellMeleeAttack) || Has(MonsterAbility.AreaShot))
            {
                damagePotential *= 1.3;
            }

            double monsterSpecial = 1.0;

            if (stats.IsArchers)
            {
                monsterSpecial += Has(MonsterAbility.NoMeleePenalty) ? 0.5 : 0.4;
            }

            if (Has(MonsterAbility.Flying))
            {
                monsterSpecial += 0.3;
            }

            if (Has(MonsterAbility.EnemyHalving))
            {
                monsterSpecial += 1;
            }

            if (Has(MonsterAbility.SoulEater))
            {
                monsterSpecial += 2;
            }

            if (Has(MonsterAbility.HpDrain))
            {
                monsterSpecial += 0.3;
            }

            if (MonsterAbilities.TryGetInstance(monster, MonsterAbility.SpellCaster, out MonsterAbilityInstance caster))
            {
                // This is a tricky evaluation. Spell casting ability depends on a type of spell and chance to
                // inflict the spell.
                switch (caster.Spell)
                {
                case SpellType.Paralyze:
                case SpellType.Blind:
                case SpellType.Petrify:
                    monsterSpecial += caster.Percentage / 100.0;
                    break;
                case SpellType.Dispel:
                case SpellType.Curse:
                    // These spell are very weak and do not impact much during battle.
                    monsterSpecial += caster.Percentage / 100.0 / 10.0;
                    break;
                default:
                    Debug.Assert(false, "A new spell casting ability needs a weight in getMonsterBaseStrength().");
                    break;
                }
            }

            // Higher speed gives initiative advantage/first attack. Remap speed value to -0.2...+0.15,
            // AVERAGE is 0. Punish slow speeds more as unit won't participate in first rounds and slows down
            // strategic army.
            int speedDifference = (int)stats.Speed - (int)MonsterSpeed.Average;

            monsterSpecial += (speedDifference < 0) ? speedDifference * 0.1 : speedDifference * 0.05;

            // Additional HP and Damage effectiveness diminishes with every combat round; strictly x4 HP ==
            // x2 unit count.
            strength[index] = Math.Sqrt(damagePotential * effectiveHitPoints) * monsterSpecial;
        }

        return strength;
    }

    // attack | defence | damageMin | damageMax | hp | speed | shots
    private static readonly MonsterBattleStats[] BattleStats =
    [
        new(0, 0, 0, 0, 0, MonsterSpeed.VerySlow, 0), // Unknown Monster
        new(1, 1, 1, 1, 1, MonsterSpeed.VerySlow, 0), // Peasant
        new(5, 3, 2, 3, 10, MonsterSpeed.VerySlow, 12), // Archer
        new(5, 3, 2, 3, 10, MonsterSpeed.Average, 24), // Ranger
        new(5, 9, 3, 4, 15, MonsterSpeed.Average, 0), // Pikeman
        new(5, 9, 3, 4, 20, MonsterSpeed.Fast, 0), // Veteran Pikeman
        new(7, 9, 4, 6, 25, MonsterSpeed.Average, 0), // Swordsman
        new(7, 9, 4, 6, 30, MonsterSpeed.Fast, 0), // Master Swordsman
        new(10, 9, 5, 10, 30, MonsterSpeed.VeryFast, 0), // Cavalry
        new(10, 9, 5, 10, 40, MonsterSpeed.UltraFast, 0), // Champion
        new(11, 12, 10, 20, 50, MonsterSpeed.Fast, 0), // Paladin
        new(11, 12, 10, 20, 65, MonsterSpeed.VeryFast, 0), // Crusader
        new(3, 1, 1, 2, 3, MonsterSpeed.Average, 0), // Goblin
        new(3, 4, 2, 3, 10, MonsterSpeed.VerySlow, 8), // Orc
        new(3, 4, 3, 4, 15, MonsterSpeed.Slow, 16), // Orc Chief
        new(6, 2, 3, 5, 20, MonsterSpeed.VeryFast, 0), // Wolf
        new(9, 5, 4, 6, 40, MonsterSpeed.VerySlow, 0), // Ogre
        new(9, 5, 5, 7, 60, MonsterSpeed.Average, 0), // Ogre Lord
        new(10, 5, 5, 7, 40, MonsterSpeed.Average, 8), // Troll
        new(10, 5, 7, 9, 40, MonsterSpeed.Fast, 16), // War Troll
        new(12, 9, 12, 24, 80, MonsterSpeed.Fast, 0), // Cyclops
        new(4, 2, 1, 2, 2, MonsterSpeed.Average, 0), // Sprite
        new(6, 5, 2, 4, 20, MonsterSpeed.VerySlow, 0), // Dwarf
        new(6, 6, 2, 4, 20, MonsterSpeed.Average, 0), // Battle Dwarf
        new(4, 3, 2, 3, 15, MonsterSpeed.Average, 24), // Elf
        new(5, 5, 2, 3, 15, MonsterSpeed.VeryFast, 24), // Grand Elf
        new(7, 5, 5, 8, 25, MonsterSpeed.Fast, 8), // Druid
        new(7, 7, 5, 8, 25, MonsterSpeed.VeryFast, 16), // Greater Druid
        new(10, 9, 7, 14, 40, MonsterSpeed.Fast, 0), // Unicorn
        new(12, 10, 20, 40, 100, MonsterSpeed.UltraFast, 0), // Phoenix
        new(3, 1, 1, 2, 5, MonsterSpeed.Average, 8), // Centaur
        new(4, 7, 2, 3, 15, MonsterSpeed.VeryFast, 0), // Gargoyle
        new(6, 6, 3, 5, 25, MonsterSpeed.Average, 0), // Griffin
        new(9, 8, 5, 10, 35, MonsterSpeed.Average, 0), // Minotaur
        new(9, 8, 5, 10, 45, MonsterSpeed.VeryFast, 0), // Minotaur King
        new(8, 9, 6, 12, 75, MonsterSpeed.VerySlow, 0), // Hydra
        new(12, 12, 25, 50, 200, MonsterSpeed.Average, 0), // Green Dragon
        new(13, 13, 25, 50, 250, MonsterSpeed.Fast, 0), // Red Dragon
        new(14, 14, 25, 50, 300, MonsterSpeed.VeryFast, 0), // Black Dragon
        new(2, 1, 1, 3, 3, MonsterSpeed.Slow, 12), // Halfling
        new(5, 4, 2, 3, 15, MonsterSpeed.VeryFast, 0), // Boar
        new(5, 10, 4, 5, 30, MonsterSpeed.VerySlow, 0), // Iron Golem
        new(7, 10, 4, 5, 35, MonsterSpeed.Slow, 0), // Steel Golem
        new(7, 7, 4, 8, 40, MonsterSpeed.Average, 0), // Roc
        new(11, 7, 7, 9, 30, MonsterSpeed.Fast, 12), // Mage
        new(12, 8, 7, 9, 35, MonsterSpeed.VeryFast, 24), // Archmage
        new(13, 10, 20, 30, 150, MonsterSpeed.Average, 0), // Giant
        new(15, 15, 20, 30, 300, MonsterSpeed.VeryFast, 24), // Titan
        new(4, 3, 2, 3, 4, MonsterSpeed.Average, 0), // Skeleton
        new(5, 2, 2, 3, 15, MonsterSpeed.VerySlow, 0), // Zombie
        new(5, 2, 2, 3, 20, MonsterSpeed.Average, 0), // Mutant Zombie
        new(6, 6, 3, 4, 25, MonsterSpeed.Average, 0), // Mummy
        new(6, 6, 3, 4, 30, MonsterSpeed.Fast, 0), // Royal Mummy
        new(8, 6, 5, 7, 30, MonsterSpeed.Average, 0), // Vampire
        new(8, 6, 5, 7, 40, MonsterSpeed.Fast, 0), // Vampire Lord
        new(7, 12, 8, 10, 25, MonsterSpeed.Fast, 12), // Lich
        new(7, 13, 8, 10, 35, MonsterSpeed.VeryFast, 24), // Power Lich
        new(11, 9, 25, 45, 150, MonsterSpeed.Average, 0), // Bone Dragon
        new(6, 1, 1, 2, 4, MonsterSpeed.Fast, 0), // Rogue
        new(7, 6, 2, 5, 20, MonsterSpeed.VeryFast, 0), // Nomad
        new(8, 7, 4, 6, 20, MonsterSpeed.Fast, 0), // Ghost
        new(10, 9, 20, 30, 50, MonsterSpeed.VeryFast, 0), // Genie
        new(8, 9, 6, 10, 35, MonsterSpeed.Average, 0), // Medusa
        new(8, 8, 4, 5, 50, MonsterSpeed.Slow, 0), // Earth Elemental
        new(7, 7, 2, 8, 35, MonsterSpeed.VeryFast, 0), // Air Elemental
        new(8, 6, 4, 6, 40, MonsterSpeed.Fast, 0), // Fire Elemental
        new(6, 8, 3, 7, 45, MonsterSpeed.Average, 0), // Water Elemental
        new(0, 0, 0, 0, 0, MonsterSpeed.VerySlow, 0), // Random Monster
        new(0, 0, 0, 0, 0, MonsterSpeed.VerySlow, 0), // Random Monster 1
        new(0, 0, 0, 0, 0, MonsterSpeed.VerySlow, 0), // Random Monster 2
        new(0, 0, 0, 0, 0, MonsterSpeed.VerySlow, 0), // Random Monster 3
        new(0, 0, 0, 0, 0, MonsterSpeed.VerySlow, 0), // Random Monster 4
    ];

    // growth | race | level | cost { gold, wood, mercury, ore, sulfur, crystal, gems }
    private static readonly MonsterGeneralStats[] GeneralStats =
    [
        new(MapRace.None, 0, 0, new Cost()), // Unknown Monster
        new(MapRace.Knight, 1, 12, new Cost { Gold = 20 }), // Peasant
        new(MapRace.Knight, 2, 8, new Cost { Gold = 150 }), // Archer
        new(MapRace.Knight, 2, 8, new Cost { Gold = 200 }), // Ranger
        new(MapRace.Knight, 3, 5, new Cost { Gold = 200 }), // Pikeman
        new(MapRace.Knight, 3, 5, new Cost { Gold = 250 }), // Veteran Pikeman
        new(MapRace.Knight, 4, 4, new Cost { Gold = 250 }), // Swordsman
        new(MapRace.Knight, 4, 4, new Cost { Gold = 300 }), // Master Swordsman
        new(MapRace.Knight, 5, 3, new Cost { Gold = 300 }), // Cavalry
        new(MapRace.Knight, 5, 3, new Cost { Gold = 375 }), // Champion
        new(MapRace.Knight, 6, 2, new Cost { Gold = 600 }), // Paladin
        new(MapRace.Knight, 6, 2, new Cost { Gold = 1000 }), // Crusader
        new(MapRace.Barbarian, 1, 10, new Cost { Gold = 40 }), // Goblin
        new(MapRace.Barbarian, 2, 8, new Cost { Gold = 140 }), // Orc
        new(MapRace.Barbarian, 2, 8, new Cost { Gold = 175 }), // Orc Chief
        new(MapRace.Barbarian, 3, 5, new Cost { Gold = 200 }), // Wolf
        new(MapRace.Barbarian, 4, 4, new Cost { Gold = 300 }), // Ogre
        new(MapRace.Barbarian, 4, 4, new Cost { Gold = 500 }), // Ogre Lord
        new(MapRace.Barbarian, 5, 3, new Cost { Gold = 600 }), // Troll
        new(MapRace.Barbarian, 5, 3, new Cost { Gold = 700 }), // War Troll
        new(MapRace.Barbarian, 6, 2, new Cost { Gold = 750 }), // Cyclops
        new(MapRace.Sorceress, 1, 8, new Cost { Gold = 50 }), // Sprite
        new(MapRace.Sorceress, 2, 6, new Cost { Gold = 200 }), // Dwarf
        new(MapRace.Sorceress, 2, 6, new Cost { Gold = 250 }), // Battle Dwarf
        new(MapRace.Sorceress, 3, 4, new Cost { Gold = 250 }), // Elf
        new(MapRace.Sorceress, 3, 4, new Cost { Gold = 300 }), // Grand Elf
        new(MapRace.Sorceress, 4, 3, new Cost { Gold = 350 }), // Druid
        new(MapRace.Sorceress, 4, 3, new Cost { Gold = 400 }), // Greater Druid
        new(MapRace.Sorceress, 5, 2, new Cost { Gold = 500 }), // Unicorn
        new(MapRace.Sorceress, 6, 1, new Cost { Gold = 1500, Mercury = 1 }), // Phoenix
        new(MapRace.Warlock, 1, 8, new Cost { Gold = 60 }), // Centaur
        new(MapRace.Warlock, 2, 6, new Cost { Gold = 200 }), // Gargoyle
        new(MapRace.Warlock, 3, 4, new Cost { Gold = 300 }), // Griffin
        new(MapRace.Warlock, 4, 3, new Cost { Gold = 400 }), // Minotaur
        new(MapRace.Warlock, 4, 3, new Cost { Gold = 500 }), // Minotaur King
        new(MapRace.Warlock, 5, 2, new Cost { Gold = 800 }), // Hydra
        new(MapRace.Warlock, 6, 1, new Cost { Gold = 3000, Sulfur = 1 }), // Green Dragon
        new(MapRace.Warlock, 6, 1, new Cost { Gold = 3500, Sulfur = 1 }), // Red Dragon
        new(MapRace.Warlock, 6, 1, new Cost { Gold = 4000, Sulfur = 2 }), // Black Dragon
        new(MapRace.Wizard, 1, 8, new Cost { Gold = 50 }), // Halfling
        new(MapRace.Wizard, 2, 6, new Cost { Gold = 150 }), // Boar
        new(MapRace.Wizard, 3, 4, new Cost { Gold = 300 }), // Iron Golem
        new(MapRace.Wizard, 3, 4, new Cost { Gold = 350 }), // Steel Golem
        new(MapRace.Wizard, 4, 3, new Cost { Gold = 400 }), // Roc
        new(MapRace.Wizard, 5, 2, new Cost { Gold = 600 }), // Mage
        new(MapRace.Wizard, 5, 2, new Cost { Gold = 700 }), // Archmage
        new(MapRace.Wizard, 6, 1, new Cost { Gold = 2000, Gems = 1 }), // Giant
        new(MapRace.Wizard, 6, 1, new Cost { Gold = 5000, Gems = 2 }), // Titan
        new(MapRace.Necromancer, 1, 8, new Cost { Gold = 75 }), // Skeleton
        new(MapRace.Necromancer, 2, 6, new Cost { Gold = 150 }), // Zombie
        new(MapRace.Necromancer, 2, 6, new Cost { Gold = 200 }), // Mutant Zombie
        new(MapRace.Necromancer, 3, 4, new Cost { Gold = 250 }), // Mummy
        new(MapRace.Necromancer, 3, 4, new Cost { Gold = 300 }), // Royal Mummy
        new(MapRace.Necromancer, 4, 3, new Cost { Gold = 500 }), // Vampire
        new(MapRace.Necromancer, 4, 3, new Cost { Gold = 650 }), // Vampire Lord
        new(MapRace.Necromancer, 5, 2, new Cost { Gold = 750 }), // Lich
        new(MapRace.Necromancer, 5, 2, new Cost { Gold = 900 }), // Power Lich
        new(MapRace.Necromancer, 6, 1, new Cost { Gold = 1500 }), // Bone Dragon
        new(MapRace.None, 1, 8, new Cost { Gold = 50 }), // Rogue
        new(MapRace.None, 2, 4, new Cost { Gold = 200 }), // Nomad
        new(MapRace.None, 3, 3, new Cost { Gold = 1000 }), // Ghost
        new(MapRace.None, 6, 2, new Cost { Gold = 650, Gems = 1 }), // Genie
        new(MapRace.None, 4, 5, new Cost { Gold = 500 }), // Medusa
        new(MapRace.None, 4, 4, new Cost { Gold = 500 }), // Earth Elemental
        new(MapRace.None, 4, 4, new Cost { Gold = 500 }), // Air Elemental
        new(MapRace.None, 4, 4, new Cost { Gold = 500 }), // Fire Elemental
        new(MapRace.None, 4, 4, new Cost { Gold = 500 }), // Water Elemental
        new(MapRace.None, 0, 0, new Cost()), // Random Monster
        new(MapRace.None, 1, 0, new Cost()), // Random Monster 1
        new(MapRace.None, 2, 0, new Cost()), // Random Monster 2
        new(MapRace.None, 3, 0, new Cost()), // Random Monster 3
        new(MapRace.None, 4, 0, new Cost()), // Random Monster 4
    ];
}
