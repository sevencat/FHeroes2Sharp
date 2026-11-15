namespace FHeroes2.Game.Artifacts;

/// <summary>
/// What an artifact grants, <c>fheroes2::ArtifactBonusType</c> (<c>artifact_info.h:29-95</c>).
/// </summary>
/// <remarks>
/// The order is the reference's and is not alphabetical on purpose: the values are only used as dictionary
/// keys, but keeping the order makes a diff against the reference trivial.
/// </remarks>
public enum ArtifactBonusType
{
    None,
    KnowledgeSkill,
    AttackSkill,
    DefenceSkill,
    SpellPowerSkill,
    GoldIncome,
    WoodIncome,
    MercuryIncome,
    OreIncome,
    SulfurIncome,
    CrystalIncome,
    GemsIncome,
    Morale,
    Luck,
    SeaBattleMoraleBoost,
    SeaBattleLuckBoost,
    LandMobility,
    SeaMobility,
    SpellPointsDailyGeneration,
    EveryCombatSpellDuration,
    ExtraCatapultShots,
    AreaRevealDistance,
    AddSpell,
    NecromancySkill,
    SurrenderCostReductionPercent,
    CurseSpellCostReductionPercent,
    BlessSpellCostReductionPercent,
    SummoningSpellCostReductionPercent,
    MindInfluenceSpellCostReductionPercent,
    ColdSpellDamageReductionPercent,
    FireSpellDamageReductionPercent,
    LightningSpellDamageReductionPercent,
    ElementalSpellDamageReductionPercent,
    HypnotizeSpellExtraEffectivenessPercent,
    ColdSpellExtraEffectivenessPercent,
    FireSpellExtraEffectivenessPercent,
    LightningSpellExtraEffectivenessPercent,
    ResurrectSpellExtraEffectivenessPercent,
    SummoningSpellExtraEffectivenessPercent,
    CurseSpellImmunity,
    HypnotizeSpellImmunity,
    DeathSpellImmunity,
    BerserkSpellImmunity,
    BlindSpellImmunity,
    ParalyzeSpellImmunity,
    HolySpellImmunity,
    DispelSpellImmunity,
    EndlessAmmunition,
    NoShootingPenalty,
    ViewMonsterInformation,
    DisableAllSpellCombatCasting,
    MaximumMorale,
    MaximumLuck,
}

/// <summary>
/// What an artifact takes away, <c>fheroes2::ArtifactCurseType</c> (<c>artifact_info.h:97-114</c>).
/// </summary>
public enum ArtifactCurseType
{
    GoldPenalty,
    SpellPowerSkill,
    Morale,
    FireSpellExtraDamagePercent,
    ColdSpellExtraDamagePercent,
    NoJoiningArmies,
    UndeadMoralePenalty,
}
