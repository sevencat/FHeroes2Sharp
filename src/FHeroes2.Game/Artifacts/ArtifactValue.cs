using System.Diagnostics;

using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// The price of an artifact in "artifact units", <c>Artifact::getArtifactValue()</c>
/// (<c>artifact.cpp:269-382</c>).
/// </summary>
/// <remarks>
/// <para>
/// The number is not gold: it is an arbitrary score the AI compares bags by, and the reference builds it out of the
/// artifact's own bonuses with a hand-tuned weight per bonus type. The weights are grouped below exactly as the
/// reference groups them, and a unit of the same bonus is always worth the same, whatever the artifact.
/// </para>
/// <para>
/// Two weights look arbitrary and are: a percentage discount on a spell is worth 1/50 of a point per percent while
/// a gold income is worth 1/800 per unit, because that is roughly how often the reference's battles meet them. They
/// are not to be "fixed" — an AI's artefact ranking shifts with them.
/// </para>
/// <para>
/// The reference asserts that no bonus type is unhandled; the port asserts in debug builds and scores zero
/// otherwise, so a new bonus type makes tests fail rather than silently devaluing every artifact carrying it.
/// </para>
/// </remarks>
public static class ArtifactValues
{
    /// <summary>
    /// The value of the artifact, <c>Artifact::getArtifactValue()</c>.
    /// </summary>
    /// <param name="artifact">The artifact to price.</param>
    /// <returns>The score; zero for an id that carries neither a bonus nor a curse.</returns>
    public static double Value(ArtifactType artifact)
    {
        double value = 0;
        ArtifactData data = ArtifactTable.Get(artifact);

        foreach (ArtifactBonus bonus in data.Bonuses)
        {
            switch (bonus.Type)
            {
                case ArtifactBonusType.GoldIncome:
                    value += bonus.Value / 800.0;
                    break;

                case ArtifactBonusType.SeaMobility:
                    value += bonus.Value / 500.0;
                    break;

                case ArtifactBonusType.LandMobility:
                    value += bonus.Value / 200.0;
                    break;

                case ArtifactBonusType.CurseSpellCostReductionPercent:
                case ArtifactBonusType.BlessSpellCostReductionPercent:
                case ArtifactBonusType.SummoningSpellCostReductionPercent:
                case ArtifactBonusType.MindInfluenceSpellCostReductionPercent:
                case ArtifactBonusType.ColdSpellDamageReductionPercent:
                case ArtifactBonusType.FireSpellDamageReductionPercent:
                case ArtifactBonusType.LightningSpellDamageReductionPercent:
                case ArtifactBonusType.ElementalSpellDamageReductionPercent:
                case ArtifactBonusType.HypnotizeSpellExtraEffectivenessPercent:
                case ArtifactBonusType.ColdSpellExtraEffectivenessPercent:
                case ArtifactBonusType.FireSpellExtraEffectivenessPercent:
                case ArtifactBonusType.LightningSpellExtraEffectivenessPercent:
                case ArtifactBonusType.ResurrectSpellExtraEffectivenessPercent:
                case ArtifactBonusType.SummoningSpellExtraEffectivenessPercent:
                    value += bonus.Value / 50.0;
                    break;

                case ArtifactBonusType.NecromancySkill:
                case ArtifactBonusType.SurrenderCostReductionPercent:
                    value += bonus.Value / 10.0;
                    break;

                case ArtifactBonusType.EveryCombatSpellDuration:
                case ArtifactBonusType.SpellPointsDailyGeneration:
                    value += bonus.Value / 2.0;
                    break;

                // The flags: a whole extra point each, no matter how the artifact spells the value out.
                case ArtifactBonusType.CurseSpellImmunity:
                case ArtifactBonusType.HypnotizeSpellImmunity:
                case ArtifactBonusType.DeathSpellImmunity:
                case ArtifactBonusType.BerserkSpellImmunity:
                case ArtifactBonusType.BlindSpellImmunity:
                case ArtifactBonusType.ParalyzeSpellImmunity:
                case ArtifactBonusType.HolySpellImmunity:
                case ArtifactBonusType.DispelSpellImmunity:
                case ArtifactBonusType.EndlessAmmunition:
                case ArtifactBonusType.NoShootingPenalty:
                case ArtifactBonusType.ViewMonsterInformation:
                case ArtifactBonusType.AddSpell:
                case ArtifactBonusType.ExtraCatapultShots:
                    value += 1;
                    break;

                case ArtifactBonusType.MaximumMorale:
                case ArtifactBonusType.DisableAllSpellCombatCasting:
                case ArtifactBonusType.MaximumLuck:
                    value += 3;
                    break;

                // One point per point: the primary skills, the morale and luck steps, the seven incomes.
                case ArtifactBonusType.KnowledgeSkill:
                case ArtifactBonusType.AttackSkill:
                case ArtifactBonusType.DefenceSkill:
                case ArtifactBonusType.SpellPowerSkill:
                case ArtifactBonusType.Morale:
                case ArtifactBonusType.Luck:
                case ArtifactBonusType.AreaRevealDistance:
                case ArtifactBonusType.CrystalIncome:
                case ArtifactBonusType.MercuryIncome:
                case ArtifactBonusType.OreIncome:
                case ArtifactBonusType.GemsIncome:
                case ArtifactBonusType.WoodIncome:
                case ArtifactBonusType.SulfurIncome:
                case ArtifactBonusType.SeaBattleLuckBoost:
                case ArtifactBonusType.SeaBattleMoraleBoost:
                    value += bonus.Value;
                    break;

                case ArtifactBonusType.None:
                    break;

                default:
                    Debug.Assert(false, $"The artifact bonus {bonus.Type} has no value weight.");
                    break;
            }
        }

        foreach (ArtifactCurse curse in data.Curses)
        {
            switch (curse.Type)
            {
                case ArtifactCurseType.GoldPenalty:
                    value -= curse.Value / 200.0;
                    break;

                case ArtifactCurseType.ColdSpellExtraDamagePercent:
                case ArtifactCurseType.FireSpellExtraDamagePercent:
                    value -= curse.Value / 100.0;
                    break;

                case ArtifactCurseType.NoJoiningArmies:
                case ArtifactCurseType.UndeadMoralePenalty:
                    value -= 1;
                    break;

                case ArtifactCurseType.Morale:
                case ArtifactCurseType.SpellPowerSkill:
                    value -= curse.Value;
                    break;

                default:
                    Debug.Assert(false, $"The artifact curse {curse.Type} has no value weight.");
                    break;
            }
        }

        return value;
    }
}
