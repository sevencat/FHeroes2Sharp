using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Army;

/// <summary>
/// <c>Troop</c> of <c>src/fheroes2/army/army_troop.h</c>: a monster type and a pile of that monster.
/// </summary>
/// <remarks>
/// <para>
/// The reference <c>Troop</c> derives from <c>Monster</c>, so every stat is reachable without asking the
/// monster id — <c>troop.GetAttack()</c> is <c>monster.GetAttack()</c>. The port keeps the two apart on
/// purpose: <see cref="MonsterStatistics"/> owns the tables, the troop owns the count. The result is the
/// same numbers with one less inheritance edge, which matters because the reference's <c>Monster</c> is also
/// the value stored inside map objects and scripted events.
/// </para>
/// <para>
/// Not ported: <c>GetName()</c>/<c>GetPluralName()</c> (translated strings), <c>GetAttackString()</c> and
/// the three siblings (display), <c>isUndead()</c>/<c>isFlying()</c>/<c>isAffectedByMorale()</c> (monster
/// abilities, <c>monster_info.cpp</c>), <c>GetStrength()</c>/<c>GetStrengthWithBonus()</c> (they need the
/// cached <c>monsterBaseStrength</c>, which is computed from those abilities), <c>GetUpgrade()</c> and the
/// upgrade cost (they need the dwelling bits of <c>castle.h</c>), and the battle-only overrides
/// (<c>isBattle()</c>, <c>GetHitPointsLeft()</c>, the mode flags).
/// </para>
/// </remarks>
public sealed class Troop
{
    private MonsterType _monster;

    private uint _count;

    /// <summary>An empty slot, <c>Troop()</c> (<c>army_troop.h:42</c>).</summary>
    public Troop()
    {
        _monster = MonsterType.Unknown;
    }

    /// <summary>A stack, <c>Troop( const Monster &amp;, uint32_t )</c> (<c>army_troop.h:48</c>).</summary>
    /// <param name="monster">Monster of the stack.</param>
    /// <param name="count">Number of creatures in it.</param>
    public Troop(MonsterType monster, uint count)
    {
        _monster = monster;
        _count = count;
    }

    /// <summary>The monster of the stack, <c>GetID()</c> of the reference's base class.</summary>
    public MonsterType Monster
    {
        get => _monster;
        set => _monster = value;
    }

    /// <summary>Number of creatures, <c>GetCount()</c>/<c>SetCount()</c> (<c>army_troop.h:78</c>).</summary>
    public uint Count
    {
        get => _count;
        set => _count = value;
    }

    /// <summary>
    /// A slot that holds something, <c>Troop::isValid()</c> (<c>army_troop.cpp:88</c>): a real monster and a
    /// non-zero count. A stack of zero creatures is an empty slot, not a valid one.
    /// </summary>
    public bool IsValid => MonsterStatistics.IsValid(_monster) && _count > 0;

    /// <summary>An empty or zero-count slot, <c>isEmpty()</c> (<c>army_troop.h:99</c>).</summary>
    public bool IsEmpty => !IsValid;

    /// <summary>Whether the stack shoots, <c>Monster::isArchers()</c>.</summary>
    public bool IsArchers => GetBattleStats().IsArchers;

    /// <summary>The level of the monster, <c>Monster::GetMonsterLevel()</c>.</summary>
    public int MonsterLevel => MonsterStatistics.GetGeneralStats(_monster).Level;

    /// <summary>Hit points of the whole stack, <c>Troop::GetHitPoints()</c> (<c>army_troop.cpp:61</c>).</summary>
    /// <returns>Hit points of one creature times the count.</returns>
    public uint GetHitPoints() => (uint)(GetBattleStats().HitPoints * (long)_count);

    /// <summary>Lowest damage of the whole stack, <c>Troop::GetDamageMin()</c>.</summary>
    /// <returns>Damage of one creature times the count.</returns>
    public uint GetDamageMin() => (uint)(GetBattleStats().DamageMin * (long)_count);

    /// <summary>Highest damage of the whole stack, <c>Troop::GetDamageMax()</c>.</summary>
    /// <returns>Damage of one creature times the count.</returns>
    public uint GetDamageMax() => (uint)(GetBattleStats().DamageMax * (long)_count);

    /// <summary>Speed of the stack, <c>Troop::GetSpeed()</c> — the slowest member sets the pace.</summary>
    /// <returns>The speed of the stack's monster.</returns>
    public MonsterSpeed GetSpeed() => GetBattleStats().Speed;

    /// <summary>Cost of one creature, <c>Monster::GetCost()</c> (<c>monster.h:306</c>).</summary>
    /// <returns>The cost.</returns>
    public Funds GetCost() => MonsterStatistics.GetGeneralStats(_monster).GetCost();

    /// <summary>
    /// Cost of the whole stack, <c>Troop::GetTotalCost()</c> (<c>army_troop.cpp:93</c>).
    /// </summary>
    /// <returns>The per-creature cost times the count.</returns>
    public Funds GetTotalCost() => GetCost() * _count;

    /// <summary>Whether the stack holds the given monster, <c>Troop::isMonster()</c>.</summary>
    /// <param name="monster">Monster to compare with.</param>
    /// <returns><see langword="true"/> when the ids are equal.</returns>
    public bool IsMonster(MonsterType monster) => _monster == monster;

    /// <summary>Copies another stack, <c>Troop::Set( const Troop &amp; )</c> (<c>army_troop.cpp:40</c>).</summary>
    /// <param name="troop">The stack to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="troop"/> is <see langword="null"/>.</exception>
    public void Set(Troop troop)
    {
        ArgumentNullException.ThrowIfNull(troop);

        _monster = troop._monster;
        _count = troop._count;
    }

    /// <summary>Fills the slot, <c>Troop::Set( const Monster &amp;, uint32_t )</c> (<c>army_troop.cpp:46</c>).</summary>
    /// <param name="monster">Monster of the stack.</param>
    /// <param name="count">Number of creatures in it.</param>
    public void Set(MonsterType monster, uint count)
    {
        _monster = monster;
        _count = count;
    }

    /// <summary>Clears the slot, <c>Troop::Reset()</c> (<c>army_troop.h:69</c>).</summary>
    public void Reset()
    {
        _monster = MonsterType.Unknown;
        _count = 0;
    }

    /// <summary>
    /// The battle stats of the stack's monster; on an empty slot the unknown monster's row, which is all
    /// zeroes, so callers that ignore <see cref="IsValid"/> get zeroes instead of an exception.
    /// </summary>
    /// <returns>The row of <see cref="Monster"/>.</returns>
    private MonsterBattleStats GetBattleStats() => MonsterStatistics.GetBattleStats(_monster);
}
