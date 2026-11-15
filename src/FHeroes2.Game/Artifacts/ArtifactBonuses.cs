namespace FHeroes2.Game.Artifacts;

/// <summary>
/// How a bonus or a curse stacks, the six <c>isBonus*</c>/<c>isCurse*</c> switches of
/// <c>artifact_info.cpp:839-947</c>.
/// </summary>
/// <remarks>
/// <para>
/// The switches name three piles, and the pile decides how
/// <see cref="ArtifactBag.GetTotalArtifactEffectValue(ArtifactBonusType)"/> adds it up:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Cumulative</b> — every copy of every artifact counts: the four primary skills and the seven resource
/// incomes. Two Ultimate Swords are worth +24 attack, not +12.
/// </item>
/// <item>
/// <b>Multiplied</b> — a percentage, collected through a separate API because percentages multiply instead of
/// adding. No artifact table row currently uses one that the port asks for.
/// </item>
/// <item>
/// <b>Unique</b> — a flag: one copy is as good as three. Adding the value would be meaningless, so the
/// summing method asserts it is never called for one of these.
/// </item>
/// </list>
/// <para>
/// A type that is in none of the three is the reference's fourth pile,
/// <c>[ cumulative per artifact type ]</c> (<c>artifact_info.cpp:1028</c>): morale, luck, mobility, the
/// spell point ring, <c>ADD_SPELL</c> and friends. It is summed like cumulative, except that a second copy of
/// the same artifact adds nothing (<c>artifact.cpp:579-594</c>). So the three switches are not a partition:
/// fifteen of the fifty-three bonus types and the <see cref="ArtifactCurseType.Morale"/> curse land in none
/// of their three piles and belong to the fourth.
/// </para>
/// <para>
/// The reference asserts the same thing from the other side: calling
/// <c>getTotalArtifactEffectValue( type )</c> with a multiplied or unique type trips
/// <c>assert( !isBonusMultiplied( bonus ) &amp;&amp; !isBonusUnique( bonus ) )</c>.
/// </para>
/// </remarks>
public static class ArtifactBonuses
{
    /// <summary>
    /// Whether every copy counts, <c>isBonusCumulative()</c> (<c>artifact_info.cpp:839</c>).
    /// </summary>
    /// <param name="bonus">The bonus type.</param>
    /// <returns>Whether the values add up.</returns>
    public static bool IsCumulative(ArtifactBonusType bonus) => bonus switch
    {
        ArtifactBonusType.KnowledgeSkill => true,
        ArtifactBonusType.SpellPowerSkill => true,
        ArtifactBonusType.AttackSkill => true,
        ArtifactBonusType.DefenceSkill => true,
        ArtifactBonusType.GoldIncome => true,
        ArtifactBonusType.WoodIncome => true,
        ArtifactBonusType.MercuryIncome => true,
        ArtifactBonusType.OreIncome => true,
        ArtifactBonusType.SulfurIncome => true,
        ArtifactBonusType.CrystalIncome => true,
        ArtifactBonusType.GemsIncome => true,
        _ => false,
    };

    /// <summary>
    /// Whether the bonus is a percentage, <c>isBonusMultiplied()</c> (<c>artifact_info.cpp:861</c>).
    /// </summary>
    /// <param name="bonus">The bonus type.</param>
    /// <returns>Whether the values multiply rather than add.</returns>
    public static bool IsMultiplied(ArtifactBonusType bonus) => bonus switch
    {
        ArtifactBonusType.SurrenderCostReductionPercent => true,
        ArtifactBonusType.CurseSpellCostReductionPercent => true,
        ArtifactBonusType.BlessSpellCostReductionPercent => true,
        ArtifactBonusType.SummoningSpellCostReductionPercent => true,
        ArtifactBonusType.MindInfluenceSpellCostReductionPercent => true,
        ArtifactBonusType.ColdSpellDamageReductionPercent => true,
        ArtifactBonusType.FireSpellDamageReductionPercent => true,
        ArtifactBonusType.LightningSpellDamageReductionPercent => true,
        ArtifactBonusType.ElementalSpellDamageReductionPercent => true,
        ArtifactBonusType.HypnotizeSpellExtraEffectivenessPercent => true,
        ArtifactBonusType.ColdSpellExtraEffectivenessPercent => true,
        ArtifactBonusType.FireSpellExtraEffectivenessPercent => true,
        ArtifactBonusType.LightningSpellExtraEffectivenessPercent => true,
        ArtifactBonusType.ResurrectSpellExtraEffectivenessPercent => true,
        ArtifactBonusType.SummoningSpellExtraEffectivenessPercent => true,
        _ => false,
    };

    /// <summary>
    /// Whether one copy is enough, <c>isBonusUnique()</c> (<c>artifact_info.cpp:887</c>).
    /// </summary>
    /// <param name="bonus">The bonus type.</param>
    /// <returns>Whether the bonus is a flag rather than a number.</returns>
    public static bool IsUnique(ArtifactBonusType bonus) => bonus switch
    {
        ArtifactBonusType.CurseSpellImmunity => true,
        ArtifactBonusType.HypnotizeSpellImmunity => true,
        ArtifactBonusType.DeathSpellImmunity => true,
        ArtifactBonusType.BerserkSpellImmunity => true,
        ArtifactBonusType.BlindSpellImmunity => true,
        ArtifactBonusType.ParalyzeSpellImmunity => true,
        ArtifactBonusType.HolySpellImmunity => true,
        ArtifactBonusType.DispelSpellImmunity => true,
        ArtifactBonusType.EndlessAmmunition => true,
        ArtifactBonusType.NoShootingPenalty => true,
        ArtifactBonusType.ViewMonsterInformation => true,
        ArtifactBonusType.DisableAllSpellCombatCasting => true,
        _ => false,
    };

    /// <summary>
    /// Whether every copy of the curse counts, <c>isCurseCumulative()</c> (<c>artifact_info.cpp:910</c>).
    /// </summary>
    /// <param name="curse">The curse type.</param>
    /// <returns>Whether the values add up.</returns>
    public static bool IsCurseCumulative(ArtifactCurseType curse) => curse switch
    {
        ArtifactCurseType.SpellPowerSkill => true,
        ArtifactCurseType.GoldPenalty => true,
        _ => false,
    };

    /// <summary>
    /// Whether the curse is a percentage, <c>isCurseMultiplied()</c> (<c>artifact_info.cpp:923</c>).
    /// </summary>
    /// <param name="curse">The curse type.</param>
    /// <returns>Whether the values multiply rather than add.</returns>
    public static bool IsCurseMultiplied(ArtifactCurseType curse) => curse switch
    {
        ArtifactCurseType.FireSpellExtraDamagePercent => true,
        ArtifactCurseType.ColdSpellExtraDamagePercent => true,
        _ => false,
    };

    /// <summary>
    /// Whether one copy of the curse is enough, <c>isCurseUnique()</c> (<c>artifact_info.cpp:936</c>).
    /// </summary>
    /// <param name="curse">The curse type.</param>
    /// <returns>Whether the curse is a flag rather than a number.</returns>
    public static bool IsCurseUnique(ArtifactCurseType curse) => curse switch
    {
        ArtifactCurseType.NoJoiningArmies => true,
        ArtifactCurseType.UndeadMoralePenalty => true,
        _ => false,
    };
}
