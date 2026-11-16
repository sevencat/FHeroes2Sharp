using System.Text;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Castle;

/// <summary>
/// What the town adds to the army standing in it: <c>castle.cpp:2003-2063</c> and the three trivial
/// modificators of <c>castle.h:296-309</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference hands every one of these a <c>std::string *</c> that the town screen fills with "Tavern +1" lines.
/// The pointer is nullable there and every use is guarded by <c>if ( strs )</c>; the port takes a
/// <see cref="StringBuilder"/> that may be <see langword="null"/> and writes the same lines: the building's name,
/// <see cref="AppendModifier"/>, and a newline for all but the last.
/// </para>
/// <para>
/// Attack, defense and knowledge are the three the reference inlines as <c>return 0</c> — no town raises them. They
/// are here anyway so that a caller that asks a town for all six answers gets six answers, which is what the hero
/// screen does.
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>
    /// What the town adds to attack, <c>Castle::GetAttackModificator()</c> (<c>castle.h:296-299</c>): nothing.
    /// </summary>
    /// <param name="explanation">Ignored, the reference's <c>unused</c> argument.</param>
    /// <returns>Zero.</returns>
    public int GetAttackModificator(StringBuilder? explanation = null) => 0;

    /// <summary>
    /// What the town adds to defense, <c>Castle::GetDefenseModificator()</c> (<c>castle.h:301-304</c>): nothing.
    /// </summary>
    /// <param name="explanation">Ignored, the reference's <c>unused</c> argument.</param>
    /// <returns>Zero.</returns>
    public int GetDefenseModificator(StringBuilder? explanation = null) => 0;

    /// <summary>
    /// What the town adds to knowledge, <c>Castle::GetKnowledgeModificator()</c> (<c>castle.h:308-311</c>): nothing.
    /// </summary>
    /// <param name="explanation">Ignored, the reference's <c>unused</c> argument.</param>
    /// <returns>Zero.</returns>
    public int GetKnowledgeModificator(StringBuilder? explanation = null) => 0;

    /// <summary>
    /// What the town adds to spell power, <c>Castle::GetPowerModificator()</c> (<c>castle.cpp:2003-2017</c>).
    /// </summary>
    /// <remarks>
    /// The necromancer's special building — the shrine of the undead — is worth two. It is the town's own building,
    /// not a shrine on the map, which is why the necromancer buys it like any other and gets the spell power from
    /// merely owning it.
    /// </remarks>
    /// <param name="explanation">Where to write the line, or <see langword="null"/> for the number alone.</param>
    /// <returns>The bonus, zero for a town without the building.</returns>
    public int GetPowerModificator(StringBuilder? explanation = null)
    {
        int result = 0;

        if (Race == MapRace.Necromancer && IsBuild(BuildingType.Spec))
        {
            const int modifier = 2;
            result += modifier;

            if (explanation is not null)
            {
                explanation.Append(CastleBuildingInfo.GetName(Race, BuildingType.Spec));
                AppendModifier(explanation, modifier);
            }
        }

        return result;
    }

    /// <summary>
    /// What the town adds to morale, <c>Castle::GetMoraleModificator()</c> (<c>castle.cpp:2019-2046</c>).
    /// </summary>
    /// <remarks>
    /// Two buildings: the tavern, worth one in every town, and the barbarian coliseum, worth two more. The coliseum
    /// is the barbarian's special building; no other race's special building touches morale.
    /// </remarks>
    /// <param name="explanation">Where to write the lines, or <see langword="null"/> for the number alone.</param>
    /// <returns>The bonus, <see cref="Morale.Normal"/> for a bare town.</returns>
    public int GetMoraleModificator(StringBuilder? explanation = null)
    {
        int result = Morale.Normal;

        if (IsBuild(BuildingType.Tavern))
        {
            const int modifier = 1;
            result += modifier;

            if (explanation is not null)
            {
                explanation.Append(CastleBuildingInfo.GetName(Race, BuildingType.Tavern));
                AppendModifier(explanation, modifier);
                explanation.Append('\n');
            }
        }

        if (Race == MapRace.Barbarian && IsBuild(BuildingType.Spec))
        {
            const int modifier = 2;
            result += modifier;

            if (explanation is not null)
            {
                explanation.Append(CastleBuildingInfo.GetName(Race, BuildingType.Spec));
                AppendModifier(explanation, modifier);
                explanation.Append('\n');
            }
        }

        return result;
    }

    /// <summary>
    /// What the town adds to luck, <c>Castle::GetLuckModificator()</c> (<c>castle.cpp:2048-2063</c>).
    /// </summary>
    /// <remarks>
    /// The sorceress's special building — the fountains — are worth two luck. It is the only building in the game
    /// that changes luck from the town side; the rest comes from skills and objects.
    /// </remarks>
    /// <param name="explanation">Where to write the line, or <see langword="null"/> for the number alone.</param>
    /// <returns>The bonus, <see cref="Luck.Normal"/> for a town without it.</returns>
    public int GetLuckModificator(StringBuilder? explanation = null)
    {
        int result = Luck.Normal;

        if (Race == MapRace.Sorceress && IsBuild(BuildingType.Spec))
        {
            const int modifier = 2;
            result += modifier;

            if (explanation is not null)
            {
                explanation.Append(CastleBuildingInfo.GetName(Race, BuildingType.Spec));
                AppendModifier(explanation, modifier);
                explanation.Append('\n');
            }
        }

        return result;
    }

    /// <summary>
    /// Writes a modifier the way the reference's text does, <c>fheroes2::appendModifierToString()</c>
    /// (<c>tools.cpp:168-179</c>).
    /// </summary>
    /// <remarks>
    /// The sign is the whole point: a positive modifier gets <c>" +"</c>, a negative one gets a single space
    /// because its own minus sign is about to follow, and zero gets nothing. The reference writes the number with
    /// <c>std::to_string</c>, which is culture-invariant; so is <see cref="int.ToString()"/> here.
    /// </remarks>
    /// <param name="text">The text to append to.</param>
    /// <param name="modifier">The value.</param>
    private static void AppendModifier(StringBuilder text, int modifier)
    {
        if (modifier < 0)
        {
            text.Append(' ');
        }
        else if (modifier > 0)
        {
            text.Append(" +");
        }

        text.Append(modifier);
    }
}
