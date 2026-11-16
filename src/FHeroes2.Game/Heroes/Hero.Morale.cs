using FHeroes2.Assets.Maps;

using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;

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
/// The port has the first two terms. The artifact ones need the artifact bag's luck/morale fields, which are
/// still missing; they are additions, so the order above still describes what is computed here.
/// </para>
/// <para>
/// <b>The objects are counted by type, not by visit</b>: <c>getAllVisitedObjectTypes()</c> is a set, so three
/// temples are still +2. That is also what makes the effect disappear by itself — the ticks in
/// <c>Hero.Visit.cs</c> drop the records, and the next <see cref="GetMorale"/> recomputes without them
/// (ADR-122).
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// The morale of the hero's army, <c>Heroes::GetMorale()</c> (<c>heroes.cpp:1033</c>).
    /// </summary>
    /// <returns>
    /// <see cref="Morale.Normal"/> plus the leadership skill and the visited-object effect, clamped into the
    /// scale. The army's own share (one race, undead in the ranks) is
    /// <see cref="Army.Army.GetMoraleModificator()"/>, which the reference adds here as well and which is not
    /// part of this number yet.
    /// </returns>
    public int GetMorale() =>
        Morale.Normalize(Morale.Normal + (int)SecondarySkills.GetValue(SecondarySkill.Leadership) + GetVisitedObjectsMorale());

    /// <summary>
    /// The luck of the hero's army, <c>Heroes::GetLuck()</c> (<c>heroes.cpp:1069</c>).
    /// </summary>
    /// <returns>
    /// <see cref="Luck.Normal"/> plus the luck skill and the visited-object effect, clamped into the scale.
    /// </returns>
    public int GetLuck() =>
        Luck.Normalize(Luck.Normal + (int)SecondarySkills.GetValue(SecondarySkill.Luck) + GetVisitedObjectsLuck());

    /// <summary>
    /// The morale the visited objects are worth, <c>getObjectMoraleModifiers( getAllVisitedObjectTypes() )</c>.
    /// </summary>
    /// <returns>The sum over the distinct visited types; a negative number for graveyards and wrecks.</returns>
    private int GetVisitedObjectsMorale() => GetAllVisitedObjectTypes().Sum(GameStatic.GetObjectMoraleEffect);

    /// <summary>
    /// The luck the visited objects are worth, <c>getObjectLuckModifiers( getAllVisitedObjectTypes() )</c>.
    /// </summary>
    /// <returns>The sum over the distinct visited types; a negative number for the pyramid and the black cat.</returns>
    private int GetVisitedObjectsLuck() => GetAllVisitedObjectTypes().Sum(GameStatic.GetObjectLuckEffect);
}
