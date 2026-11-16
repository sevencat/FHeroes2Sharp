using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
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
/// the three siblings (display), and the battle-only overrides (<c>isBattle()</c>,
/// <c>GetHitPointsLeft()</c>, the mode flags). The ability queries (<c>isUndead()</c>,
/// <c>isAffectedByMorale()</c>) are here since <see cref="MonsterAbilities"/> landed; the rest of them are
/// battle rules and wait for the battle engine. The upgrade pair is here since
/// <see cref="MonsterUpgrades"/> landed — the dwelling check that decides whether an upgrade is *available*
/// lives in the castle dialog, not in the troop.
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

    /// <summary>Whether the stack flies, <c>Monster::isFlying()</c>.</summary>
    public bool IsFlying => MonsterAbilities.IsFlying(_monster);

    /// <summary>Whether the stack is undead, <c>Monster::isUndead()</c> (<c>monster.h:237</c>).</summary>
    public bool IsUndead => MonsterAbilities.IsUndead(_monster);

    /// <summary>
    /// Whether morale applies to the stack, <c>Monster::isAffectedByMorale()</c> (<c>monster.h:287</c>):
    /// everything except the undead and the four elementals.
    /// </summary>
    public bool IsAffectedByMorale => MonsterAbilities.IsAffectedByMorale(_monster);

    /// <summary>
    /// The race of the stack's monster, <c>Monster::GetRace()</c> (<c>monster.cpp:136</c>).
    /// </summary>
    /// <returns>
    /// The race of the table row; <see cref="MapRace.None"/> for a neutral creature. The army's morale
    /// counts the distinct races, so this is the value it groups by.
    /// </returns>
    public MapRace GetRace() => MonsterStatistics.GetGeneralStats(_monster).Race;

    /// <summary>The level of the monster, <c>Monster::GetMonsterLevel()</c>.</summary>
    public int MonsterLevel => MonsterStatistics.GetGeneralStats(_monster).Level;

    /// <summary>
    /// The dwelling the stack's monster is recruited from, <c>Monster::GetDwelling()</c>
    /// (<c>monster.cpp:691</c>).
    /// </summary>
    /// <remarks>
    /// This is the third use of the same table: it says which shelf the stack came from, whether the town has the
    /// upgraded dwelling that an upgrade needs, and where a recruited stack is taken out of.
    /// </remarks>
    /// <returns>The dwelling bit, or <see cref="BuildingType.Nothing"/> for a neutral creature.</returns>
    public BuildingType GetDwelling() => DwellingMonsters.GetDwelling(_monster);

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
    /// The strength of the stack, <c>Troop::GetStrength()</c> (<c>army_troop.cpp:52</c>).
    /// </summary>
    /// <remarks>
    /// The reference multiplies the strength of one creature by the count without looking at
    /// <c>isValid()</c> — an empty stack is a stack of zero creatures, which is the same answer.
    /// </remarks>
    /// <returns>The strength of one creature times the count.</returns>
    public double GetStrength() => MonsterStatistics.GetMonsterStrength(_monster) * _count;

    /// <summary>
    /// The strength of the stack with a flat attack and defense bonus, <c>Troop::GetStrengthWithBonus()</c>
    /// (<c>army_troop.cpp:57</c>).
    /// </summary>
    /// <remarks>
    /// This is how a hero's attack and defense skills reach the strength of the army: the reference adds them
    /// to every stack's own attack and defense before computing the creature strength, so the bonus grows with
    /// the base stats of the creature rather than being a flat percentage.
    /// </remarks>
    /// <param name="bonusAttack">Attack to add, normally the hero's attack skill.</param>
    /// <param name="bonusDefense">Defense to add, normally the hero's defense skill.</param>
    /// <returns>The strength of one creature times the count.</returns>
    public double GetStrengthWithBonus(int bonusAttack, int bonusDefense)
    {
        Debug.Assert(bonusAttack >= 0 && bonusDefense >= 0);

        MonsterBattleStats stats = GetBattleStats();

        return MonsterStatistics.GetMonsterStrength(_monster, stats.Attack + bonusAttack, stats.Defense + bonusDefense) * _count;
    }

    /// <summary>
    /// Cost of the whole stack, <c>Troop::GetTotalCost()</c> (<c>army_troop.cpp:93</c>).
    /// </summary>
    /// <returns>The per-creature cost times the count.</returns>
    public Funds GetTotalCost() => GetCost() * _count;

    /// <summary>
    /// Whether the stack can be upgraded, <c>Troop::isAllowUpgrade()</c> inherited from
    /// <c>Monster::isAllowUpgrade()</c> (<c>monster.h:257</c>).
    /// </summary>
    /// <remarks>
    /// True for any monster with an upgrade, whatever the castle says. The other half of the reference's test —
    /// "and the castle of this race has the upgraded dwelling" — belongs to
    /// <c>Troops::UpgradeTroops</c>, which needs a castle the port does not have yet.
    /// </remarks>
    public bool IsAllowUpgrade => MonsterUpgrades.IsAllowUpgrade(_monster);

    /// <summary>
    /// The creature this stack upgrades into, <c>Monster::GetUpgrade()</c> (<c>monster.cpp:285</c>).
    /// </summary>
    /// <remarks>
    /// The monster itself when it has no upgrade, so a caller may upgrade a stack of peasants and get the same
    /// stack back rather than an error.
    /// </remarks>
    /// <returns>The upgraded monster, or <see cref="Monster"/> when there is none.</returns>
    public MonsterType GetUpgrade() => MonsterUpgrades.GetUpgrade(_monster);

    /// <summary>
    /// Turns the whole stack into its upgraded monster, <c>Monster::Upgrade()</c> (<c>monster.h:168</c>).
    /// </summary>
    /// <remarks>
    /// The reference only rewrites the id — the creatures are not replaced, so the count survives, and the
    /// health that a stack may have lost in battle would survive too (<c>Monster::Upgrade()</c> is called on a
    /// <c>Troop</c> whose count is all it has). A stack of green dragons becomes red dragons, and calling it
    /// again makes black dragons: the chain is walked one step at a time.
    /// </remarks>
    public void Upgrade() => _monster = MonsterUpgrades.GetUpgrade(_monster);

    /// <summary>
    /// What upgrading one creature costs, <c>Troop::GetUpgradeCost()</c> — public in the reference only
    /// through <c>Monster::GetUpgradeCost()</c> being protected and <c>GetTotalUpgradeCost()</c> being the
    /// public door (<c>army_troop.h:96</c>).
    /// </summary>
    /// <remarks>
    /// The reference's comment is explicit that this must only be called after <see cref="IsAllowUpgrade"/>:
    /// for a monster without an upgrade it returns an empty <see cref="Funds"/>, which is a fine answer but a
    /// misleading one if the caller meant to charge for something.
    /// </remarks>
    /// <returns>Twice the price difference, or an empty <see cref="Funds"/>.</returns>
    public Funds GetUpgradeCost() => MonsterUpgrades.GetUpgradeCost(_monster);

    /// <summary>
    /// What upgrading the whole stack costs, <c>Troop::GetTotalUpgradeCost()</c> (<c>army_troop.cpp:98</c>).
    /// </summary>
    /// <returns>The per-creature upgrade cost times the count.</returns>
    public Funds GetTotalUpgradeCost() => GetUpgradeCost() * _count;

    /// <summary>Whether the stack holds the given monster, <c>Troop::isMonster()</c>.
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
