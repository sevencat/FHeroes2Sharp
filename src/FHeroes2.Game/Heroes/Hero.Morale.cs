using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The luck and the morale of a hero, <c>Heroes::GetLuck()</c> and <c>Heroes::GetMorale()</c>
/// (<c>heroes.cpp:1033-1103</c>).
/// </summary>
/// <remarks>
/// <para>
/// Both are a sum of four terms in the reference, in this order: the secondary skill (luck or leadership,
/// one point per level), the objects the hero has visited (<c>GameStatic::getObjectLuckEffect</c> and its
/// morale twin, e.g. a fountain for +1), the artifact bonuses, and — last, overriding everything — an
/// artifact that grants the maximum of the scale outright. The sum is clamped at the end, so a hero with
/// expert leadership and a fountain is still only <see cref="Morale.Blood"/>.
/// </para>
/// <para>
/// The port has the first term. The other three need the visited-object set, the artifact bag and the two
/// object effect tables of <c>game_static.cpp</c>, none of which exist yet; they are additions, so the
/// order above still describes what is computed here.
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// The morale of the hero's army, <c>Heroes::GetMorale()</c> (<c>heroes.cpp:1033</c>).
    /// </summary>
    /// <returns>
    /// <see cref="Morale.Normal"/> plus the leadership skill, clamped into the scale. The army's own share
    /// (one race, undead in the ranks) is <see cref="Army.Army.GetMoraleModificator()"/>, which the
    /// reference adds here as well and which is not part of this number yet.
    /// </returns>
    public int GetMorale() => Morale.Normalize(Morale.Normal + (int)SecondarySkills.GetValue(SecondarySkill.Leadership));

    /// <summary>
    /// The luck of the hero's army, <c>Heroes::GetLuck()</c> (<c>heroes.cpp:1069</c>).
    /// </summary>
    /// <returns><see cref="Luck.Normal"/> plus the luck skill, clamped into the scale.</returns>
    public int GetLuck() => Luck.Normalize(Luck.Normal + (int)SecondarySkills.GetValue(SecondarySkill.Luck));
}
