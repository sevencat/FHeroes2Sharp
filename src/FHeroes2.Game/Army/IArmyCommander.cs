using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Army;

/// <summary>
/// What an army asks its commander: the four calls <c>Army::GetStrength()</c> makes on
/// <c>Army::commander</c> (<c>army.cpp:1291-1333</c>) and the one identity question the transfer methods ask
/// (<c>army.cpp:1428</c>, <c>army.cpp:1440</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference's back-pointer is <c>HeroBase *</c>, a class that both <c>Heroes</c> and the battle-only
/// <c>Captain</c> derive from. The port has no such base class — <see cref="Heroes.Hero"/> is a sealed class of
/// its own — so the five members the army actually calls are named here instead of pulling a base class in for
/// them. A captain, when it lands, implements this interface and needs no change in <see cref="Army"/>.
/// </para>
/// <para>
/// The army only reads; nothing here lets a commander push back on the army. That is deliberate: the reference
/// back-pointer is used in exactly these places — four of them inside the strength calculation, one where an
/// army is about to be emptied and a hero must not be.
/// </para>
/// </remarks>
public interface IArmyCommander
{
    /// <summary>
    /// The hero's attack, <c>commander-&gt;GetAttack()</c> (<c>heroes.cpp:903</c>).
    /// </summary>
    /// <remarks>
    /// This is the effective attack, artifacts included — the same number the hero screen shows.
    /// </remarks>
    /// <returns>The attack skill, at least zero.</returns>
    int GetAttack();

    /// <summary>
    /// The hero's defense, <c>commander-&gt;GetDefense()</c> (<c>heroes.cpp:914</c>).
    /// </summary>
    /// <returns>The defense skill, at least zero.</returns>
    int GetDefense();

    /// <summary>
    /// A secondary skill's value, <c>commander-&gt;GetSecondarySkillValue( skill )</c>
    /// (<c>heroes.h:262</c>).
    /// </summary>
    /// <remarks>
    /// The army asks for one skill only — <see cref="SecondarySkill.Archery"/> — but the method is the
    /// general one so that the next caller does not have to widen the interface.
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <returns>Its value: 10, 25 or 50 per level for archery, zero when the hero does not have it.</returns>
    uint GetSecondarySkillValue(SecondarySkill skill);

    /// <summary>
    /// What the hero's spells add to the army, <c>commander-&gt;GetMagicStrategicValue( strength )</c>
    /// (<c>heroes.cpp:2124</c>).
    /// </summary>
    /// <remarks>
    /// Called once, with the strength of the stacks as the argument. The value grows with the spell book and the
    /// spell points: it is the best <c>Spell::GetStrategicValue</c> over the combat spells the hero knows, and
    /// zero for a hero with no book or no mana.
    /// </remarks>
    /// <param name="armyStrength">The strength of the stacks.</param>
    /// <returns>The extra strength the spells are worth.</returns>
    double GetMagicStrategicValue(double armyStrength);

    /// <summary>
    /// Whether the commander is a hero and not a captain, <c>HeroBase::isHeroes()</c>
    /// (<c>heroes_base.cpp:91</c>).
    /// </summary>
    /// <remarks>
    /// The reference answers <c>GetType() == HEROES</c>: true for a map hero, false for the battle-only
    /// captain. The army asks it to decide whether the army may be emptied — a hero who loses his last stack
    /// has lost the game, a garrison that empties is just an empty garrison — and the reference guards that in
    /// <c>JoinStrongestFromArmy</c> and <c>MoveTroops</c>. <see cref="Heroes.Hero"/> answers true.
    /// </remarks>
    bool IsHeroes { get; }
}
