namespace FHeroes2.Game.Resources;

/// <summary>
/// The artifact ids: the anonymous <c>enum : int32_t</c> of <c>class Artifact</c> in
/// <c>src/fheroes2/resource/artifact.h</c>, in the same order and with the same values.
/// </summary>
/// <remarks>
/// <para>
/// The reference notes that every id is one bigger than in the original game, which is what lets a
/// patch add artifacts in the middle and keeps the numbering aligned with the object types.
/// </para>
/// <para>
/// The reference gives the enumeration no name; the port calls it <c>ArtifactType</c> because
/// <c>Artifact</c> is going to be the class with the behavior, mirroring <c>MonsterType</c>.
/// </para>
/// </remarks>
public enum ArtifactType : int
{
    Unknown = 0,

// The Succession Wars artifacts.
    UltimateBook,
    UltimateSword,
    UltimateCloak,
    UltimateWand,
    UltimateShield,
    UltimateStaff,
    UltimateCrown,
    GoldenGoose,
    ArcaneNecklace,
    CasterBracelet,
    MageRing,
    WitchesBroach,
    MedalValor,
    MedalCourage,
    MedalHonor,
    MedalDistinction,
    FizbinMisfortune,
    ThunderMace,
    ArmoredGauntlets,
    DefenderHelm,
    GiantFlail,
    Ballista,
    StealthShield,
    DragonSword,
    PowerAxe,
    DivineBreastplate,
    MinorScroll,
    MajorScroll,
    SuperiorScroll,
    ForemostScroll,
    EndlessSackGold,
    EndlessBagGold,
    EndlessPurseGold,
    NomadBootsMobility,
    TravelerBootsMobility,
    RabbitFoot,
    GoldenHorseshoe,
    GamblerLuckyCoin,
    FourLeafClover,
    TrueCompassMobility,
    SailorsAstrolabeMobility,
    EvilEye,
    EnchantedHourglass,
    GoldWatch,
    Skullcap,
    IceCloak,
    FireCloak,
    LightningHelm,
    EvercoldIcicle,
    EverhotLavaRock,
    LightningRod,
    SnakeRing,
    Ankh,
    BookElements,
    ElementalRing,
    HolyPendant,
    PendantFreeWill,
    PendantLife,
    SerenityPendant,
    SeeingEyePendant,
    KineticPendant,
    PendantDeath,
    WandNegation,
    GoldenBow,
    Telescope,
    StatesmanQuill,
    WizardHat,
    PowerRing,
    AmmoCart,
    TaxLien,
    HideousMask,
    EndlessPouchSulfur,
    EndlessVialMercury,
    EndlessPouchGems,
    EndlessCordWood,
    EndlessCartOre,
    EndlessPouchCrystal,
    SpikedHelm,
    SpikedShield,
    WhitePearl,
    BlackPearl,

    MagicBook,

// This entry is used in Editor for the special victory conditions.
    EditorAnyUltimateArtifact,

// These four artifacts are used in original editor for the Ultimate and Random artifacts.
// They should not exist and used on the adventure map after the game is properly started.
// We do not use these or create extra entries for the Ultimate and Random artifacts in editor.
    Unused84,
    Unused85,
    Unused86,

// The Price of Loyalty artifacts.
    SpellScroll,
    ArmMartyr,
    BreastplateAnduran,
    BroachShielding,
    BattleGarb,
    CrystalBall,
    HeartFire,
    HeartIce,
    HelmetAnduran,
    HolyHammer,
    LegendaryScepter,
    Masthead,
    SphereNegation,
    StaffWizardry,
    SwordBreaker,
    SwordAnduran,
    SpadeNecromancy,

// Resurrection artifacts.

// IMPORTANT! Put all new artifacts just above this line.
    ArtifactCount,
}
