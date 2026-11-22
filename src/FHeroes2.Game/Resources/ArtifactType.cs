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
/// <summary>
/// The predicates of the artifact ids, <c>Artifact::isValid()</c> (<c>artifact.h:186-189</c>).
/// </summary>
/// <remarks>
/// <para>
/// Four ids are numbers but not artifacts: <see cref="ArtifactType.Unknown"/> (an empty slot),
/// <see cref="ArtifactType.EditorAnyUltimateArtifact"/> (the map editor's placeholder) and the three holes the
/// original game left in the middle of the list (<see cref="ArtifactType.Unused84"/> to
/// <see cref="ArtifactType.Unused86"/>). The reference keeps them in range so that the sprite index stays a
/// subtraction, and rejects them here.
/// </para>
/// <para>
/// <see cref="ArtifactType.ArtifactCount"/> is not a valid artifact either: it is the sentinel, and the check
/// <c>id &lt; ARTIFACT_COUNT</c> is what excludes it.
/// </para>
/// </remarks>
public static class ArtifactTypes
{
    /// <summary>The frame an empty slot asks for, <c>IndexSprite32()</c>'s <c>255</c>.</summary>
    public const uint UnknownMiniSpriteIndex = 255;

    /// <summary>
    /// Whether the id names an artifact a hero can own, <c>Artifact::isValid()</c>.
    /// </summary>
    /// <param name="artifact">The id to check.</param>
    /// <returns>Whether the id names an artifact.</returns>
    public static bool IsValid(ArtifactType artifact)
    {
        int id = (int)artifact;

        return id != (int)ArtifactType.Unknown
            && id != (int)ArtifactType.EditorAnyUltimateArtifact
            && id < (int)ArtifactType.ArtifactCount
            && (id < (int)ArtifactType.Unused84 || id > (int)ArtifactType.Unused86);
    }

    /// <summary>
    /// The frame of the 32x32 icon in <c>ARTFX</c>, <c>Artifact::IndexSprite32()</c> (<c>artifact.h:236-243</c>).
    /// </summary>
    /// <remarks>
    /// One less than the id, so that the two sprite sets line up with the same numbering. An empty slot has no icon and
    /// gets the reference's <c>255</c>, which is past the end of the frame list — a caller that draws an empty slot must
    /// skip it rather than blit that frame.
    /// </remarks>
    /// <param name="artifact">The artifact whose icon is wanted.</param>
    /// <returns>The frame index.</returns>
    public static uint MiniSpriteIndex(ArtifactType artifact) =>
        artifact == ArtifactType.Unknown ? UnknownMiniSpriteIndex : (uint)((int)artifact - 1);

    /// <summary>
    /// The frame of the 64x64 icon in <c>ARTIFACT</c>, <c>Artifact::IndexSprite64()</c> (<c>artifact.h:246-249</c>).
    /// </summary>
    /// <param name="artifact">The artifact whose icon is wanted.</param>
    /// <returns>The frame index, which is the id itself.</returns>
    public static uint FullSpriteIndex(ArtifactType artifact) => (uint)(int)artifact;
}