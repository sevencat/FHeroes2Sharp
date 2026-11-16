using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// The rarity bits of an artifact, <c>Artifact::ArtLevel</c> (<c>artifact.h:53-63</c>,
/// <c>artifact.cpp:98-267</c>).
/// </summary>
/// <remarks>
/// <para>
/// The bits answer two questions at once: how rare the artifact is (treasure, minor, major, ultimate) and whether
/// the random-artifact picker may hand it out at all. <see cref="NoRandom"/> is the second answer for the artifacts
/// a map never gives away — the magic book, the two cursed ones and the Hideous Mask.
/// </para>
/// <para>
/// The Price of Loyalty artifacts are the third answer: on a <see cref="MapVersion.SuccessionWars"/> map they are
/// <see cref="Loyalty"/> alone, which no random roll tests for, and on the two later versions the bit is joined
/// with the rarity their <c>LoyaltyLevel()</c> assigned. That is why a necklace of the expansion can be a minor
/// artifact on an <c>.mx2</c> map and no artifact at all on a <c>.mp2</c> one.
/// </para>
/// </remarks>
[Flags]
public enum ArtifactLevel
{
    /// <summary>Not a random artifact, <c>ART_NONE</c>.</summary>
    None = 0x00,

    /// <summary>The commonest tier, <c>ART_LEVEL_TREASURE</c>.</summary>
    Treasure = 0x01,

    /// <summary>Second tier, <c>ART_LEVEL_MINOR</c>.</summary>
    Minor = 0x02,

    /// <summary>Third tier, <c>ART_LEVEL_MAJOR</c>.</summary>
    Major = 0x04,

    /// <summary>
    /// Every ordinary tier at once, <c>ART_LEVEL_ALL_NORMAL</c>, the mask the "random artifact" object rolls.
    /// </summary>
    AllNormal = Treasure | Minor | Major,

    /// <summary>The eight artifacts every map but the shop can hide, <c>ART_ULTIMATE</c>.</summary>
    Ultimate = 0x08,

    /// <summary>
    /// An artifact of the expansion, <c>ART_LOYALTY</c>; only the two later map versions also give it a tier.
    /// </summary>
    Loyalty = 0x10,

    /// <summary>
    /// An artifact the random picker never returns, <c>ART_NORANDOM</c>: the magic book and the cursed ones.
    /// </summary>
    NoRandom = 0x20,
}

/// <summary>
/// The tier tables of <c>Artifact::Level()</c> and <c>Artifact::LoyaltyLevel()</c>
/// (<c>artifact.cpp:117-267</c>).
/// </summary>
public static class ArtifactLevels
{
    /// <summary>
    /// Whether the artifact is one of the eight ultimates, <c>Artifact::isUltimate()</c>
    /// (<c>artifact.cpp:98-115</c>).
    /// </summary>
    /// <remarks>
    /// The Golden Goose is an ultimate in this sense although it is not one of the eight named pieces: the map
    /// object still places it as the ultimate prize, so the flag is about "may the ultimate object hold it", not
    /// about the artifact's name.
    /// </remarks>
    /// <param name="artifact">The artifact.</param>
    /// <returns>Whether it counts as an ultimate artifact.</returns>
    public static bool IsUltimate(ArtifactType artifact) => artifact switch
    {
        ArtifactType.UltimateBook => true,
        ArtifactType.UltimateSword => true,
        ArtifactType.UltimateCloak => true,
        ArtifactType.UltimateWand => true,
        ArtifactType.UltimateShield => true,
        ArtifactType.UltimateStaff => true,
        ArtifactType.UltimateCrown => true,
        ArtifactType.GoldenGoose => true,
        _ => false,
    };

    /// <summary>
    /// The tier an expansion artifact would have, <c>Artifact::LoyaltyLevel()</c> (<c>artifact.cpp:117-148</c>).
    /// </summary>
    /// <remarks>
    /// Only <see cref="Level"/> calls this, and only for the artifacts of the expansion. Seven of the seventeen
    /// return <see cref="ArtifactLevel.NoRandom"/>: the scroll, the four Anduran pieces and the two artifacts the
    /// campaign hands out by name.
    /// </remarks>
    /// <param name="artifact">The artifact.</param>
    /// <returns>Its tier, <see cref="ArtifactLevel.None"/> for everything not in the expansion list.</returns>
    public static ArtifactLevel LoyaltyLevel(ArtifactType artifact) => artifact switch
    {
        ArtifactType.Masthead => ArtifactLevel.Minor,
        ArtifactType.SpadeNecromancy => ArtifactLevel.Minor,
        ArtifactType.HeartFire => ArtifactLevel.Minor,
        ArtifactType.HeartIce => ArtifactLevel.Minor,

        ArtifactType.ArmMartyr => ArtifactLevel.Major,
        ArtifactType.HolyHammer => ArtifactLevel.Major,
        ArtifactType.LegendaryScepter => ArtifactLevel.Major,
        ArtifactType.StaffWizardry => ArtifactLevel.Major,
        ArtifactType.SwordBreaker => ArtifactLevel.Major,
        ArtifactType.CrystalBall => ArtifactLevel.Major,

        ArtifactType.SpellScroll => ArtifactLevel.NoRandom,
        ArtifactType.BroachShielding => ArtifactLevel.NoRandom,
        ArtifactType.SwordAnduran => ArtifactLevel.NoRandom,
        ArtifactType.BreastplateAnduran => ArtifactLevel.NoRandom,
        ArtifactType.BattleGarb => ArtifactLevel.NoRandom,
        ArtifactType.HelmetAnduran => ArtifactLevel.NoRandom,
        ArtifactType.SphereNegation => ArtifactLevel.NoRandom,

        _ => ArtifactLevel.None,
    };

    /// <summary>
    /// The tier of the artifact, <c>Artifact::Level()</c> (<c>artifact.cpp:150-267</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference reads the tier of an expansion artifact from the current map's version
    /// (<c>Settings::Get().getCurrentMapInfo().version</c>). The port has no settings singleton to consult, so the
    /// version is a parameter — the same choice <see cref="Castle.Castle"/> made for its buildings.
    /// </para>
    /// <para>
    /// A <see cref="MapVersion.SuccessionWars"/> map answers <see cref="ArtifactLevel.Loyalty"/> for the expansion
    /// artifacts, i.e. no tier bit. That is not an accident of the table: it is how the original game keeps the
    /// expansion artifacts out of the random rolls of a <c>.mp2</c> map.
    /// </para>
    /// </remarks>
    /// <param name="artifact">The artifact.</param>
    /// <param name="version">Version of the map the artifact is on.</param>
    /// <returns>The tier bits, <see cref="ArtifactLevel.None"/> for an id that is not an artifact.</returns>
    public static ArtifactLevel Level(ArtifactType artifact, MapVersion version)
    {
        if (IsUltimate(artifact))
        {
            return ArtifactLevel.Ultimate;
        }

        switch (artifact)
        {
            case ArtifactType.MedalValor:
            case ArtifactType.MedalCourage:
            case ArtifactType.MedalHonor:
            case ArtifactType.MedalDistinction:
            case ArtifactType.ThunderMace:
            case ArtifactType.ArmoredGauntlets:
            case ArtifactType.DefenderHelm:
            case ArtifactType.GiantFlail:
            case ArtifactType.RabbitFoot:
            case ArtifactType.GoldenHorseshoe:
            case ArtifactType.GamblerLuckyCoin:
            case ArtifactType.FourLeafClover:
            case ArtifactType.EnchantedHourglass:
            case ArtifactType.IceCloak:
            case ArtifactType.FireCloak:
            case ArtifactType.LightningHelm:
            case ArtifactType.SnakeRing:
            case ArtifactType.HolyPendant:
            case ArtifactType.PendantFreeWill:
            case ArtifactType.PendantLife:
            case ArtifactType.SerenityPendant:
            case ArtifactType.SeeingEyePendant:
            case ArtifactType.KineticPendant:
            case ArtifactType.PendantDeath:
            case ArtifactType.GoldenBow:
            case ArtifactType.Telescope:
            case ArtifactType.StatesmanQuill:
                return ArtifactLevel.Treasure;

            case ArtifactType.CasterBracelet:
            case ArtifactType.MageRing:
            case ArtifactType.StealthShield:
            case ArtifactType.PowerAxe:
            case ArtifactType.MinorScroll:
            case ArtifactType.EndlessPurseGold:
            case ArtifactType.SailorsAstrolabeMobility:
            case ArtifactType.EvilEye:
            case ArtifactType.GoldWatch:
            case ArtifactType.Skullcap:
            case ArtifactType.EvercoldIcicle:
            case ArtifactType.EverhotLavaRock:
            case ArtifactType.LightningRod:
            case ArtifactType.Ankh:
            case ArtifactType.BookElements:
            case ArtifactType.ElementalRing:
            case ArtifactType.PowerRing:
            case ArtifactType.AmmoCart:
            case ArtifactType.EndlessCordWood:
            case ArtifactType.EndlessCartOre:
            case ArtifactType.SpikedHelm:
            case ArtifactType.WhitePearl:
                return ArtifactLevel.Minor;

            case ArtifactType.ArcaneNecklace:
            case ArtifactType.WitchesBroach:
            case ArtifactType.Ballista:
            case ArtifactType.DragonSword:
            case ArtifactType.DivineBreastplate:
            case ArtifactType.MajorScroll:
            case ArtifactType.SuperiorScroll:
            case ArtifactType.ForemostScroll:
            case ArtifactType.EndlessSackGold:
            case ArtifactType.EndlessBagGold:
            case ArtifactType.NomadBootsMobility:
            case ArtifactType.TravelerBootsMobility:
            case ArtifactType.TrueCompassMobility:
            case ArtifactType.WandNegation:
            case ArtifactType.WizardHat:
            case ArtifactType.EndlessPouchSulfur:
            case ArtifactType.EndlessVialMercury:
            case ArtifactType.EndlessPouchGems:
            case ArtifactType.EndlessPouchCrystal:
            case ArtifactType.SpikedShield:
            case ArtifactType.BlackPearl:
                return ArtifactLevel.Major;

            case ArtifactType.MagicBook:
            case ArtifactType.FizbinMisfortune:
            case ArtifactType.TaxLien:
            case ArtifactType.HideousMask:
                return ArtifactLevel.NoRandom;

            case ArtifactType.SpellScroll:
            case ArtifactType.ArmMartyr:
            case ArtifactType.BreastplateAnduran:
            case ArtifactType.BroachShielding:
            case ArtifactType.BattleGarb:
            case ArtifactType.CrystalBall:
            case ArtifactType.HelmetAnduran:
            case ArtifactType.HolyHammer:
            case ArtifactType.LegendaryScepter:
            case ArtifactType.Masthead:
            case ArtifactType.SphereNegation:
            case ArtifactType.StaffWizardry:
            case ArtifactType.SwordBreaker:
            case ArtifactType.SwordAnduran:
            case ArtifactType.SpadeNecromancy:
            case ArtifactType.HeartFire:
            case ArtifactType.HeartIce:
                return version is MapVersion.PriceOfLoyalty or MapVersion.Resurrection
                    ? ArtifactLevel.Loyalty | LoyaltyLevel(artifact)
                    : ArtifactLevel.Loyalty;

            default:
                return ArtifactLevel.None;
        }
    }

    /// <summary>
    /// Whether the id belongs to the expansion, <c>isPriceOfLoyaltyArtifact()</c> (<c>artifact.cpp:1099-1102</c>).
    /// </summary>
    /// <remarks>
    /// The expansion artifacts are one contiguous block at the end of the list, so the check is a range. It is
    /// used by the map format reader to decide whether an expansion artifact may appear on this map.
    /// </remarks>
    /// <param name="artifact">The artifact.</param>
    /// <returns>Whether it is an artifact of the expansion.</returns>
    public static bool IsPriceOfLoyalty(ArtifactType artifact) =>
        artifact >= ArtifactType.SpellScroll && artifact <= ArtifactType.SpadeNecromancy;
}
