using System.Diagnostics;

using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The four primary skills of a hero and the artifact bag that raises them: <c>heroes_base.h:222-260</c>,
/// <c>heroes_base.cpp:180-360</c> and <c>heroes.cpp:903-943</c>.
/// </summary>
/// <remarks>
/// <para>
/// A hero has four numbers — attack, defense, spell power, knowledge — that start from the race and grow by one
/// per level-up. Each has a base value (<see cref="BaseAttack"/> and friends), a modifier from the equipment
/// (<see cref="GetAttackModificator"/>), and an effective value clamped to a byte
/// (<see cref="GetAttack"/>), which is what every caller uses.
/// </para>
/// <para>
/// <b>Port substitutions</b>, all because the systems they read are not ported:
/// </para>
/// <list type="bullet">
/// <item>
/// The castle's contribution is missing. The reference's modifier method ends with
/// <c>castle-&gt;GetAttackModificator( nullptr )</c> (and the three twins), which the port cannot ask yet:
/// there is no castle the hero belongs to. It is the only term the reference adds besides the artifacts.
/// </item>
/// </list>
/// <para>
/// The spell side — the book, the scrolls, the spell points and <see cref="GetMagicStrategicValue"/> — is in
/// <c>Hero.Spells.cs</c>; this file only holds the four numbers the equipment raises.
/// </para>
/// <para>
/// The reference's modifier methods are <c>HeroBase::GetAttackModificator( std::string * )</c>: the string
/// collects the human-readable breakdown for the hero screen. The port drops the parameter and returns the
/// number; the strings belong to the text layer.
/// </para>
/// </remarks>
public sealed partial class Hero : IArmyCommander
{
    private readonly ArtifactBag _bagArtifacts = new();

    private int _attack;
    private int _defense;
    private int _power;
    private int _knowledge;

    /// <summary>The equipment of the hero, <c>bag_artifacts</c> (<c>heroes_base.h:249</c>).</summary>
    public ArtifactBag BagArtifacts => _bagArtifacts;

    /// <summary>The base attack, before the artifacts, <c>attack</c> (<c>heroes_base.h:241</c>).</summary>
    public int BaseAttack => _attack;

    /// <summary>The base defense, before the artifacts, <c>defense</c> (<c>heroes_base.h:242</c>).</summary>
    public int BaseDefense => _defense;

    /// <summary>The base spell power, before the artifacts, <c>power</c> (<c>heroes_base.h:243</c>).</summary>
    public int BasePower => _power;

    /// <summary>The base knowledge, before the artifacts, <c>knowledge</c> (<c>heroes_base.h:244</c>).</summary>
    public int BaseKnowledge => _knowledge;

    /// <summary>
    /// The attack the hero fights with, <c>Heroes::GetAttack()</c> (<c>heroes.cpp:903</c>).
    /// </summary>
    /// <remarks>
    /// Base plus artifacts, clamped to 0..255 because the original game stores the value in a byte and the
    /// reference keeps that limit even though it computes in <see langword="int"/>.
    /// </remarks>
    /// <returns>The effective attack.</returns>
    public int GetAttack() => Math.Clamp(_attack + GetAttackModificator(), 0, 255);

    /// <summary>
    /// The attack minus the base value, <c>HeroBase::GetAttackModificator()</c>
    /// (<c>heroes_base.cpp:180</c>).
    /// </summary>
    /// <remarks>
    /// Artifacts only, for now: the castle term of the reference is missing.
    /// </remarks>
    /// <returns>The attack the equipment adds.</returns>
    public int GetAttackModificator() => _bagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.AttackSkill);

    /// <summary>
    /// The defense the hero fights with, <c>Heroes::GetDefense()</c> (<c>heroes.cpp:914</c>).
    /// </summary>
    /// <remarks>The <see cref="GetAttack"/> twin, squared at zero low, clamped to 0..255.</remarks>
    /// <returns>The effective defense.</returns>
    public int GetDefense() => Math.Clamp(_defense + GetDefenseModificator(), 0, 255);

    /// <summary>
    /// The defense minus the base value, <c>HeroBase::GetDefenseModificator()</c>
    /// (<c>heroes_base.cpp:217</c>).
    /// </summary>
    /// <returns>The defense the equipment adds.</returns>
    public int GetDefenseModificator() => _bagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.DefenceSkill);

    /// <summary>
    /// The spell power, <c>Heroes::GetPower()</c> (<c>heroes.cpp:925</c>).
    /// </summary>
    /// <remarks>
    /// Clamped to 1..255 rather than 0..255: a hero with no power cannot cast the cheapest scroll at the range
    /// the original game expects, so the reference never lets it reach zero.
    /// </remarks>
    /// <returns>The effective spell power.</returns>
    public int GetPower() => Math.Clamp(_power + GetPowerModificator(), 1, 255);

    /// <summary>
    /// The spell power minus the base value, <c>HeroBase::GetPowerModificator()</c>
    /// (<c>heroes_base.cpp:254</c>).
    /// </summary>
    /// <remarks>
    /// Artifacts on both sides: the spell power tome adds and the cursed artifacts take away.
    /// </remarks>
    /// <returns>The spell power the equipment adds, curses included.</returns>
    public int GetPowerModificator()
        => _bagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.SpellPowerSkill)
        - _bagArtifacts.GetTotalArtifactEffectValue(ArtifactCurseType.SpellPowerSkill);

    /// <summary>
    /// The knowledge, <c>Heroes::GetKnowledge()</c> (<c>heroes.cpp:936</c>).
    /// </summary>
    /// <remarks>The <see cref="GetPower"/> twin: clamped to 1..255.</remarks>
    /// <returns>The effective knowledge.</returns>
    public int GetKnowledge() => Math.Clamp(_knowledge + GetKnowledgeModificator(), 1, 255);

    /// <summary>
    /// The knowledge minus the base value, <c>HeroBase::GetKnowledgeModificator()</c>
    /// (<c>heroes_base.cpp:291</c>).
    /// </summary>
    /// <returns>The knowledge the equipment adds.</returns>
    public int GetKnowledgeModificator() => _bagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.KnowledgeSkill);

    /// <summary>
    /// Raises a primary skill by one, <c>Heroes::IncreasePrimarySkill()</c> (<c>heroes.cpp:3182</c>).
    /// </summary>
    /// <param name="skill">The skill to raise.</param>
    public void IncreasePrimarySkill(PrimarySkill skill)
    {
        switch (skill)
        {
            case PrimarySkill.Attack:
                ++_attack;
                break;

            case PrimarySkill.Defense:
                ++_defense;
                break;

            case PrimarySkill.Power:
                ++_power;
                break;

            case PrimarySkill.Knowledge:
                ++_knowledge;
                break;

            default:
                Debug.Assert(false, "Increasing a skill that is not one of the four.");
                break;
        }
    }

    /// <summary>
    /// Sets a base primary skill, <c>HeroBase::SetPrimarySkill()</c> (<c>heroes_base.cpp:126</c>).
    /// </summary>
    /// <remarks>
    /// The MP2 loader and the map editor write the four values directly; the reference's setter clamps to
    /// 0..255, which is the width of the field in the save format.
    /// </remarks>
    /// <param name="skill">The skill to set.</param>
    /// <param name="value">Its new base value.</param>
    public void SetPrimarySkill(PrimarySkill skill, int value)
    {
        value = Math.Clamp(value, 0, 255);

        switch (skill)
        {
            case PrimarySkill.Attack:
                _attack = value;
                break;

            case PrimarySkill.Defense:
                _defense = value;
                break;

            case PrimarySkill.Power:
                _power = value;
                break;

            case PrimarySkill.Knowledge:
                _knowledge = value;
                break;

            default:
                Debug.Assert(false, "Setting a skill that is not one of the four.");
                break;
        }
    }

    /// <summary>
    /// A secondary skill's value, <c>HeroBase::GetSecondarySkillValue()</c> (<c>heroes.h:262</c>).
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <returns>Its value; zero when the hero does not have it.</returns>
    public uint GetSecondarySkillValue(SecondarySkill skill) => _secondarySkills.GetValue(skill);
}
