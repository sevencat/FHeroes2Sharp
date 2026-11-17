using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The town screen's building-to-ICN table, <c>Castle::GetICNBuilding()</c> (<c>castle.cpp:1451-1814</c>).
/// </summary>
/// <remarks>
/// <para>
/// A town is drawn from dozens of ICN files — one per building per race — and this table names them. It is not
/// <see cref="CastleBuildingInfo.GetSpriteIndex"/>: that one is the offset of a building's icon inside the town's own
/// icon set, a number the town screen adds to a base; this one is the ICN file an overlay is drawn from.
/// </para>
/// <para>
/// The reference writes the table as six <c>if</c>/<c>switch</c> blocks, one per race, and falls through to
/// <c>ICN::UNKNOWN</c> with a warning. The port keeps the six blocks as six methods, so the race dispatch stays a
/// single expression and each race's table reads on its own.
/// </para>
/// <para>
/// Two entries are not part of a <c>TWN&lt;race&gt;*</c> series and are worth knowing about: the knight's
/// <see cref="BuildingType.Well2"/> is a farm drawn from its own file, and the necromancer's
/// <see cref="BuildingType.Captain"/> is a standalone building rather than an overlay.
/// </para>
/// <para>
/// The quirks are the reference's and are kept: the knight's second dwelling uses the <c>TWNKUP_1</c> (upgraded)
/// sprite while its upgrade uses <c>TWNKDW_1</c> (base) — the two are swapped — and the necromancer's tavern slot
/// belongs to its shrine, since the necromancer has no tavern.
/// </para>
/// </remarks>
public sealed partial class Castle
{
    /// <summary>
    /// The ICN a building is drawn from, <c>Castle::GetICNBuilding()</c> (<c>castle.cpp:1451-1814</c>).
    /// </summary>
    /// <param name="buildingType">The building, a single bit of <see cref="BuildingType"/>.</param>
    /// <param name="race">The town's race.</param>
    /// <returns>The ICN, or <see cref="IcnType.Unknown"/> when the race has no sprite for the building.</returns>
    public static IcnType GetICNBuilding(BuildingType buildingType, MapRace race)
    {
        // The reference lists the five mage guild levels as five cases that all name one sprite. The port's table is
        // keyed by the mask instead, so a level is folded into it here rather than repeated six times.
        buildingType = buildingType switch
        {
            BuildingType.MageGuild1 or BuildingType.MageGuild2 or BuildingType.MageGuild3 or BuildingType.MageGuild4
                or BuildingType.MageGuild5 => BuildingType.MageGuild,
            _ => buildingType,
        };

        return race switch
        {
            MapRace.Barbarian => GetBarbarianBuildingIcn(buildingType),
            MapRace.Knight => GetKnightBuildingIcn(buildingType),
            MapRace.Necromancer => GetNecromancerBuildingIcn(buildingType),
            MapRace.Sorceress => GetSorceressBuildingIcn(buildingType),
            MapRace.Warlock => GetWarlockBuildingIcn(buildingType),
            MapRace.Wizard => GetWizardBuildingIcn(buildingType),
            _ => IcnType.Unknown,
        };
    }

    private static IcnType GetBarbarianBuildingIcn(BuildingType buildingType) => buildingType switch
    {
        BuildingType.Castle => IcnType.Twnbcstl,
        BuildingType.Tent => IcnType.Twnbtent,
        BuildingType.Spec => IcnType.Twnbspec,
        BuildingType.Captain => IcnType.Twnbcapt,
        BuildingType.Well2 => IcnType.Twnbwel2,
        BuildingType.LeftTurret => IcnType.Twnbltur,
        BuildingType.RightTurret => IcnType.Twnbrtur,
        BuildingType.Moat => IcnType.Twnbmoat,
        BuildingType.Marketplace => IcnType.Twnbmark,
        BuildingType.ThievesGuild => IcnType.Twnbthie,
        BuildingType.Tavern => IcnType.Twnbtvrn,
        BuildingType.Well => IcnType.Twnbwell,
        BuildingType.Statue => IcnType.Twnbstat,
        BuildingType.Shipyard => IcnType.Twnbdock,
        BuildingType.MageGuild => IcnType.Twnbmage,
        BuildingType.DwellingMonster1 => IcnType.Twnbdw0,
        BuildingType.DwellingMonster2 => IcnType.Twnbdw1,
        BuildingType.DwellingUpgrade2 => IcnType.Twnbup1,
        BuildingType.DwellingMonster3 => IcnType.Twnbdw2,
        BuildingType.DwellingMonster4 => IcnType.Twnbdw3,
        BuildingType.DwellingUpgrade4 => IcnType.Twnbup3,
        BuildingType.DwellingMonster5 => IcnType.Twnbdw4,
        BuildingType.DwellingUpgrade5 => IcnType.Twnbup4,
        BuildingType.DwellingMonster6 => IcnType.Twnbdw5,
        _ => IcnType.Unknown,
    };

    private static IcnType GetKnightBuildingIcn(BuildingType buildingType) => buildingType switch
    {
        BuildingType.Castle => IcnType.Twnkcstl,
        BuildingType.Tent => IcnType.Twnktent,
        BuildingType.Spec => IcnType.Twnkspec,
        BuildingType.Captain => IcnType.Twnkcapt,
        BuildingType.Well2 => IcnType.KnightCastleRightFarm,
        BuildingType.LeftTurret => IcnType.Twnkltur,
        BuildingType.RightTurret => IcnType.Twnkrtur,
        BuildingType.Moat => IcnType.Twnkmoat,
        BuildingType.Marketplace => IcnType.Twnkmark,
        BuildingType.ThievesGuild => IcnType.Twnkthie,
        BuildingType.Tavern => IcnType.Twnktvrn,
        BuildingType.Well => IcnType.Twnkwell,
        BuildingType.Statue => IcnType.Twnkstat,
        BuildingType.Shipyard => IcnType.Twnkdock,
        BuildingType.MageGuild => IcnType.Twnkmage,

        // The reference's swap: the second dwelling is drawn as its upgrade and the upgrade as the base sprite.
        BuildingType.DwellingMonster1 => IcnType.Twnkdw0,
        BuildingType.DwellingMonster2 => IcnType.Twnkup1,
        BuildingType.DwellingUpgrade2 => IcnType.Twnkdw1,
        BuildingType.DwellingMonster3 => IcnType.Twnkdw2,
        BuildingType.DwellingUpgrade3 => IcnType.Twnkup2,
        BuildingType.DwellingMonster4 => IcnType.Twnkdw3,
        BuildingType.DwellingUpgrade4 => IcnType.Twnkup3,
        BuildingType.DwellingMonster5 => IcnType.Twnkdw4,
        BuildingType.DwellingUpgrade5 => IcnType.Twnkup4,
        BuildingType.DwellingMonster6 => IcnType.Twnkdw5,
        BuildingType.DwellingUpgrade6 => IcnType.Twnkup5,
        _ => IcnType.Unknown,
    };

    private static IcnType GetNecromancerBuildingIcn(BuildingType buildingType) => buildingType switch
    {
        BuildingType.Castle => IcnType.Twnncstl,
        BuildingType.Tent => IcnType.Twnntent,
        BuildingType.Spec => IcnType.Twnnspec,

        // Not an overlay like the other races' captains: the necromancer's quarters are a building of their own.
        BuildingType.Captain => IcnType.NecromancerCastleStandaloneCaptainQuarters,
        BuildingType.Well2 => IcnType.Twnnwel2,
        BuildingType.LeftTurret => IcnType.Twnnltur,
        BuildingType.RightTurret => IcnType.Twnnrtur,
        BuildingType.Moat => IcnType.Twnnmoat,
        BuildingType.Marketplace => IcnType.Twnnmark,
        BuildingType.ThievesGuild => IcnType.Twnnthie,

        // The necromancer has a shrine where the other races have a tavern.
        BuildingType.Shrine => IcnType.Twnntvrn,
        BuildingType.Well => IcnType.Twnnwell,
        BuildingType.Statue => IcnType.Twnnstat,
        BuildingType.Shipyard => IcnType.Twnndock,
        BuildingType.MageGuild => IcnType.Twnnmage,
        BuildingType.DwellingMonster1 => IcnType.Twnndw0,
        BuildingType.DwellingMonster2 => IcnType.Twnndw1,
        BuildingType.DwellingUpgrade2 => IcnType.Twnnup1,
        BuildingType.DwellingMonster3 => IcnType.Twnndw2,
        BuildingType.DwellingUpgrade3 => IcnType.Twnnup2,
        BuildingType.DwellingMonster4 => IcnType.Twnndw3,
        BuildingType.DwellingUpgrade4 => IcnType.Twnnup3,
        BuildingType.DwellingMonster5 => IcnType.Twnndw4,
        BuildingType.DwellingUpgrade5 => IcnType.Twnnup4,
        BuildingType.DwellingMonster6 => IcnType.Twnndw5,
        _ => IcnType.Unknown,
    };

    private static IcnType GetSorceressBuildingIcn(BuildingType buildingType) => buildingType switch
    {
        BuildingType.Castle => IcnType.Twnscstl,
        BuildingType.Tent => IcnType.Twnstent,
        BuildingType.Spec => IcnType.Twnsspec,
        BuildingType.Captain => IcnType.Twnscapt,
        BuildingType.Well2 => IcnType.Twnswel2,
        BuildingType.LeftTurret => IcnType.Twnsltur,
        BuildingType.RightTurret => IcnType.Twnsrtur,
        BuildingType.Moat => IcnType.Twnsmoat,
        BuildingType.Marketplace => IcnType.Twnsmark,
        BuildingType.ThievesGuild => IcnType.Twnsthie,
        BuildingType.Tavern => IcnType.Twnstvrn,
        BuildingType.Well => IcnType.Twnswell,
        BuildingType.Statue => IcnType.Twnsstat,
        BuildingType.Shipyard => IcnType.Twnsdock,
        BuildingType.MageGuild => IcnType.Twnsmage,
        BuildingType.DwellingMonster1 => IcnType.Twnsdw0,
        BuildingType.DwellingMonster2 => IcnType.Twnsdw1,
        BuildingType.DwellingUpgrade2 => IcnType.Twnsup1,
        BuildingType.DwellingMonster3 => IcnType.Twnsdw2,
        BuildingType.DwellingUpgrade3 => IcnType.Twnsup2,
        BuildingType.DwellingMonster4 => IcnType.Twnsdw3,
        BuildingType.DwellingUpgrade4 => IcnType.Twnsup3,
        BuildingType.DwellingMonster5 => IcnType.Twnsdw4,
        BuildingType.DwellingMonster6 => IcnType.Twnsdw5,
        _ => IcnType.Unknown,
    };

    private static IcnType GetWarlockBuildingIcn(BuildingType buildingType) => buildingType switch
    {
        BuildingType.Castle => IcnType.Twnwcstl,
        BuildingType.Tent => IcnType.Twnwtent,
        BuildingType.Spec => IcnType.Twnwspec,
        BuildingType.Captain => IcnType.Twnwcapt,
        BuildingType.Well2 => IcnType.Twnwwel2,
        BuildingType.LeftTurret => IcnType.Twnwltur,
        BuildingType.RightTurret => IcnType.Twnwrtur,
        BuildingType.Moat => IcnType.Twnwmoat,
        BuildingType.Marketplace => IcnType.Twnwmark,
        BuildingType.ThievesGuild => IcnType.Twnwthie,
        BuildingType.Tavern => IcnType.Twnwtvrn,
        BuildingType.Well => IcnType.Twnwwell,
        BuildingType.Statue => IcnType.Twnwstat,
        BuildingType.Shipyard => IcnType.Twnwdock,
        BuildingType.MageGuild => IcnType.Twnwmage,
        BuildingType.DwellingMonster1 => IcnType.Twnwdw0,
        BuildingType.DwellingMonster2 => IcnType.Twnwdw1,
        BuildingType.DwellingMonster3 => IcnType.Twnwdw2,
        BuildingType.DwellingMonster4 => IcnType.Twnwdw3,
        BuildingType.DwellingUpgrade4 => IcnType.Twnwup3,
        BuildingType.DwellingMonster5 => IcnType.Twnwdw4,
        BuildingType.DwellingMonster6 => IcnType.Twnwdw5,
        BuildingType.DwellingUpgrade6 => IcnType.Twnwup5,

        // The warlock is the only race with a second dwelling upgrade: the green tower's own upgrade.
        BuildingType.DwellingUpgrade7 => IcnType.Twnwup5b,
        _ => IcnType.Unknown,
    };

    private static IcnType GetWizardBuildingIcn(BuildingType buildingType) => buildingType switch
    {
        BuildingType.Castle => IcnType.Twnzcstl,
        BuildingType.Tent => IcnType.Twnztent,
        BuildingType.Spec => IcnType.Twnzspec,
        BuildingType.Captain => IcnType.Twnzcapt,
        BuildingType.Well2 => IcnType.Twnzwel2,
        BuildingType.LeftTurret => IcnType.Twnzltur,
        BuildingType.RightTurret => IcnType.Twnzrtur,
        BuildingType.Moat => IcnType.Twnzmoat,
        BuildingType.Marketplace => IcnType.Twnzmark,
        BuildingType.ThievesGuild => IcnType.Twnzthie,
        BuildingType.Tavern => IcnType.Twnztvrn,
        BuildingType.Well => IcnType.Twnzwell,
        BuildingType.Statue => IcnType.Twnzstat,
        BuildingType.Shipyard => IcnType.Twnzdock,
        BuildingType.MageGuild => IcnType.Twnzmage,
        BuildingType.DwellingMonster1 => IcnType.Twnzdw0,
        BuildingType.DwellingMonster2 => IcnType.Twnzdw1,
        BuildingType.DwellingMonster3 => IcnType.Twnzdw2,
        BuildingType.DwellingUpgrade3 => IcnType.Twnzup2,
        BuildingType.DwellingMonster4 => IcnType.Twnzdw3,
        BuildingType.DwellingMonster5 => IcnType.Twnzdw4,
        BuildingType.DwellingUpgrade5 => IcnType.Twnzup4,
        BuildingType.DwellingMonster6 => IcnType.Twnzdw5,
        BuildingType.DwellingUpgrade6 => IcnType.Twnzup5,
        _ => IcnType.Unknown,
    };
}
