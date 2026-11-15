using FHeroes2.Assets.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The tables behind <see cref="BuildingType"/>: what a building costs, what it needs first, what it upgrades
/// into, and whether it is a dwelling.
/// </summary>
/// <remarks>
/// <para>
/// Four of the reference's free functions are gathered here, because they are all "read the building tables" and
/// all of them are needed before a <c>Castle</c> object can exist: <see cref="GetCost"/>
/// (<c>buildinginfo.cpp:203-212</c>), <see cref="GetRequirement"/> (<c>castle_building_info.cpp:1097-1350</c>),
/// <see cref="GetUpgradeForBuilding"/> (<c>castle_building_info.cpp:980-1095</c>) and <see cref="IsDwelling"/>
/// (<c>buildinginfo.cpp:253-273</c>).
/// </para>
/// <para>
/// Not here: the names and descriptions (translated strings), the sprite rectangle table (needs the AGG), and
/// <c>BuildingInfo</c> itself, which is the castle dialog's per-building widget and needs a <c>Castle</c> to point
/// at.
/// </para>
/// </remarks>
public static class CastleBuildings
{
    /// <summary>
    /// The price of a building, <c>BuildingInfo::GetCost()</c> (<c>buildinginfo.cpp:203-212</c>).
    /// </summary>
    /// <remarks>
    /// The reference walks its 86-row <c>buildingStats</c> table and returns the first row whose building and
    /// race match, where the race column is a mask with <c>Race::ALL</c> for the buildings every town shares. The
    /// port keeps the same walk and the same order but writes the shared rows with no race at all
    /// (<see cref="AnyRace"/>), which is what <c>Race::ALL</c> means; a building never appears both ways, so
    /// "first match" and "the only match" are the same thing here.
    /// </remarks>
    /// <param name="building">The building to price.</param>
    /// <param name="race">The town's race — several buildings cost differently in different towns.</param>
    /// <returns>The price, or an empty <see cref="Funds"/> when the town cannot build it.</returns>
    public static Funds GetCost(BuildingType building, MapRace race)
    {
        foreach (BuildingPrice price in Prices)
        {
            if (price.Building == building && (price.Race is null || price.Race == race))
            {
                return new Funds(price.Cost);
            }
        }

        return new Funds();
    }

    /// <summary>
    /// What must be built before this building, <c>getBuildingRequirement()</c>
    /// (<c>castle_building_info.cpp:1097-1350</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer is a mask: building a dwelling may need three other dwellings, and all of them at once. The
    /// dependency is pruned to what the race actually needs — the knight's archers need only the peasant hut, the
    /// sorceress's need the hut and a tavern — which is why the table is two-dimensional and why the reference
    /// nests a switch inside a switch.
    /// </para>
    /// <para>
    /// Most buildings need nothing at all, either because they are always available (the tavern, the market place)
    /// or because the castle's own rules govern them (the captain and the turrets need a castle) and
    /// <c>Castle::CheckBuyBuilding</c> checks that separately.
    /// </para>
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building to be built.</param>
    /// <returns>The mask of buildings that must already exist, <see cref="BuildingType.Nothing"/> for none.</returns>
    public static BuildingType GetRequirement(MapRace race, BuildingType building) => (building, race) switch
    {
        // Only the wizard's special building asks for anything; every other town's special is free of
        // prerequisites.
        (BuildingType.Spec, MapRace.Wizard) => BuildingType.MageGuild1,

        (BuildingType.DwellingMonster2, MapRace.Sorceress) => BuildingType.DwellingMonster1 | BuildingType.Tavern,
        (BuildingType.DwellingMonster2, MapRace.Knight or MapRace.Barbarian or MapRace.Wizard or MapRace.Warlock or MapRace.Necromancer) => BuildingType.DwellingMonster1,

        (BuildingType.DwellingMonster3, MapRace.Knight) => BuildingType.DwellingMonster1 | BuildingType.Well,
        (BuildingType.DwellingMonster3, MapRace.Barbarian or MapRace.Sorceress or MapRace.Wizard or MapRace.Warlock or MapRace.Necromancer) => BuildingType.DwellingMonster1,

        (BuildingType.DwellingMonster4, MapRace.Knight) => BuildingType.DwellingMonster1 | BuildingType.Tavern,
        (BuildingType.DwellingMonster4, MapRace.Barbarian) => BuildingType.DwellingMonster1,
        (BuildingType.DwellingMonster4, MapRace.Sorceress) => BuildingType.DwellingMonster3 | BuildingType.MageGuild1,
        (BuildingType.DwellingMonster4, MapRace.Wizard or MapRace.Warlock) => BuildingType.DwellingMonster2,
        (BuildingType.DwellingMonster4, MapRace.Necromancer) => BuildingType.DwellingMonster3 | BuildingType.ThievesGuild,

        (BuildingType.DwellingMonster5, MapRace.Knight or MapRace.Barbarian) => BuildingType.DwellingMonster2 | BuildingType.DwellingMonster3 | BuildingType.DwellingMonster4,
        (BuildingType.DwellingMonster5, MapRace.Sorceress) => BuildingType.DwellingMonster4,
        (BuildingType.DwellingMonster5, MapRace.Warlock) => BuildingType.DwellingMonster3,
        (BuildingType.DwellingMonster5, MapRace.Wizard) => BuildingType.DwellingMonster3 | BuildingType.MageGuild1,
        (BuildingType.DwellingMonster5, MapRace.Necromancer) => BuildingType.DwellingMonster2 | BuildingType.MageGuild1,

        (BuildingType.DwellingMonster6, MapRace.Knight) => BuildingType.DwellingMonster2 | BuildingType.DwellingMonster3 | BuildingType.DwellingMonster4,
        (BuildingType.DwellingMonster6, MapRace.Barbarian or MapRace.Sorceress or MapRace.Necromancer) => BuildingType.DwellingMonster5,
        (BuildingType.DwellingMonster6, MapRace.Warlock or MapRace.Wizard) => BuildingType.DwellingMonster4 | BuildingType.DwellingMonster5,

        (BuildingType.DwellingUpgrade2, MapRace.Knight or MapRace.Barbarian) => BuildingType.DwellingMonster2 | BuildingType.DwellingMonster3 | BuildingType.DwellingMonster4,
        (BuildingType.DwellingUpgrade2, MapRace.Sorceress) => BuildingType.DwellingMonster2 | BuildingType.Well,
        (BuildingType.DwellingUpgrade2, MapRace.Necromancer) => BuildingType.DwellingMonster2,

        (BuildingType.DwellingUpgrade3, MapRace.Knight) => BuildingType.DwellingMonster2 | BuildingType.DwellingMonster3 | BuildingType.DwellingMonster4,
        (BuildingType.DwellingUpgrade3, MapRace.Sorceress) => BuildingType.DwellingMonster3 | BuildingType.DwellingMonster4,
        (BuildingType.DwellingUpgrade3, MapRace.Wizard) => BuildingType.DwellingMonster3 | BuildingType.Well,
        (BuildingType.DwellingUpgrade3, MapRace.Necromancer) => BuildingType.DwellingMonster3,

        (BuildingType.DwellingUpgrade4, MapRace.Knight or MapRace.Barbarian) => BuildingType.DwellingMonster2 | BuildingType.DwellingMonster3 | BuildingType.DwellingMonster4,
        (BuildingType.DwellingUpgrade4, MapRace.Sorceress or MapRace.Warlock or MapRace.Necromancer) => BuildingType.DwellingMonster4,

        (BuildingType.DwellingUpgrade5, MapRace.Knight or MapRace.Barbarian) => BuildingType.DwellingMonster5,
        (BuildingType.DwellingUpgrade5, MapRace.Wizard) => BuildingType.Spec | BuildingType.DwellingMonster5,
        (BuildingType.DwellingUpgrade5, MapRace.Necromancer) => BuildingType.MageGuild2 | BuildingType.DwellingMonster5,

        (BuildingType.DwellingUpgrade6, MapRace.Knight or MapRace.Warlock or MapRace.Wizard) => BuildingType.DwellingMonster6,

        // The black dragon lair is the only building in the game whose prerequisite is another upgrade.
        (BuildingType.DwellingUpgrade7, MapRace.Warlock) => BuildingType.DwellingUpgrade6,

        _ => BuildingType.Nothing,
    };

    /// <summary>
    /// What a built building can be upgraded into, <c>getUpgradeForBuilding()</c>
    /// (<c>castle_building_info.cpp:980-1095</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Five upgrades are the same in every town — the tent to the castle, and the four mage guild levels — and the
    /// rest are the dwellings, whose lines differ per race: the knight upgrades all five of his dwellings, the
    /// warlock only two (and his dragon twice), the barbarian and the sorceress never upgrade their sixth.
    /// </para>
    /// <para>
    /// A building with no upgrade answers itself, so a caller may walk the chain in a loop: the black dragon lair
    /// and the fifth mage guild both terminate.
    /// </para>
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building that is already built.</param>
    /// <returns>The upgrade, or <paramref name="building"/> itself when there is none.</returns>
    public static BuildingType GetUpgradeForBuilding(MapRace race, BuildingType building) => building switch
    {
        BuildingType.Tent => BuildingType.Castle,
        BuildingType.MageGuild1 => BuildingType.MageGuild2,
        BuildingType.MageGuild2 => BuildingType.MageGuild3,
        BuildingType.MageGuild3 => BuildingType.MageGuild4,
        BuildingType.MageGuild4 => BuildingType.MageGuild5,
        _ => GetDwellingUpgrade(race, building),
    };

    /// <summary>
    /// Whether the value is one of the twelve dwelling bits, <c>BuildingInfo::isDwelling()</c>
    /// (<c>buildinginfo.cpp:253-273</c>).
    /// </summary>
    /// <remarks>
    /// The test is on the single bits, not on <see cref="BuildingType.DwellingMonsters"/> and
    /// <see cref="BuildingType.DwellingUpgrades"/>: a mask is not a dwelling. A boolean answer for a mask would
    /// have to mean "some" or "all", and the reference's switch says neither.
    /// </remarks>
    /// <param name="building">The building to test.</param>
    /// <returns><see langword="true"/> for a dwelling or a dwelling upgrade.</returns>
    public static bool IsDwelling(BuildingType building) => building is
        BuildingType.DwellingMonster1 or BuildingType.DwellingMonster2 or BuildingType.DwellingMonster3 or
        BuildingType.DwellingMonster4 or BuildingType.DwellingMonster5 or BuildingType.DwellingMonster6 or
        BuildingType.DwellingUpgrade2 or BuildingType.DwellingUpgrade3 or BuildingType.DwellingUpgrade4 or
        BuildingType.DwellingUpgrade5 or BuildingType.DwellingUpgrade6 or BuildingType.DwellingUpgrade7;

    /// <summary>
    /// The per-race half of <see cref="GetUpgradeForBuilding"/> (<c>castle_building_info.cpp:997-1094</c>).
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building that is already built.</param>
    /// <returns>The upgrade, or <paramref name="building"/> itself when the race has none.</returns>
    private static BuildingType GetDwellingUpgrade(MapRace race, BuildingType building) => (race, building) switch
    {
        // The knight upgrades every dwelling he has.
        (MapRace.Knight, BuildingType.DwellingMonster2) => BuildingType.DwellingUpgrade2,
        (MapRace.Knight, BuildingType.DwellingMonster3) => BuildingType.DwellingUpgrade3,
        (MapRace.Knight, BuildingType.DwellingMonster4) => BuildingType.DwellingUpgrade4,
        (MapRace.Knight, BuildingType.DwellingMonster5) => BuildingType.DwellingUpgrade5,
        (MapRace.Knight, BuildingType.DwellingMonster6) => BuildingType.DwellingUpgrade6,

        (MapRace.Barbarian, BuildingType.DwellingMonster2) => BuildingType.DwellingUpgrade2,
        (MapRace.Barbarian, BuildingType.DwellingMonster4) => BuildingType.DwellingUpgrade4,
        (MapRace.Barbarian, BuildingType.DwellingMonster5) => BuildingType.DwellingUpgrade5,

        // The necromancer's bone dragon is his seventh unit and has no upgrade, so his sixth dwelling does not
        // either.
        (MapRace.Necromancer, BuildingType.DwellingMonster2) => BuildingType.DwellingUpgrade2,
        (MapRace.Necromancer, BuildingType.DwellingMonster3) => BuildingType.DwellingUpgrade3,
        (MapRace.Necromancer, BuildingType.DwellingMonster4) => BuildingType.DwellingUpgrade4,
        (MapRace.Necromancer, BuildingType.DwellingMonster5) => BuildingType.DwellingUpgrade5,

        (MapRace.Sorceress, BuildingType.DwellingMonster2) => BuildingType.DwellingUpgrade2,
        (MapRace.Sorceress, BuildingType.DwellingMonster3) => BuildingType.DwellingUpgrade3,
        (MapRace.Sorceress, BuildingType.DwellingMonster4) => BuildingType.DwellingUpgrade4,

        // The warlock is the only town with a two-step line: the green dragon's lair to the red's, the red's to
        // the black's.
        (MapRace.Warlock, BuildingType.DwellingMonster4) => BuildingType.DwellingUpgrade4,
        (MapRace.Warlock, BuildingType.DwellingMonster6) => BuildingType.DwellingUpgrade6,
        (MapRace.Warlock, BuildingType.DwellingUpgrade6) => BuildingType.DwellingUpgrade7,

        (MapRace.Wizard, BuildingType.DwellingMonster3) => BuildingType.DwellingUpgrade3,
        (MapRace.Wizard, BuildingType.DwellingMonster5) => BuildingType.DwellingUpgrade5,
        (MapRace.Wizard, BuildingType.DwellingMonster6) => BuildingType.DwellingUpgrade6,

        // A random town is given a race at map generation, but the table still has to answer: it upgrades like a
        // town that upgrades everything, plus the second dragon step.
        (MapRace.Random, BuildingType.DwellingMonster2) => BuildingType.DwellingUpgrade2,
        (MapRace.Random, BuildingType.DwellingMonster3) => BuildingType.DwellingUpgrade3,
        (MapRace.Random, BuildingType.DwellingMonster4) => BuildingType.DwellingUpgrade4,
        (MapRace.Random, BuildingType.DwellingMonster5) => BuildingType.DwellingUpgrade5,
        (MapRace.Random, BuildingType.DwellingMonster6) => BuildingType.DwellingUpgrade6,
        (MapRace.Random, BuildingType.DwellingUpgrade6) => BuildingType.DwellingUpgrade7,

        _ => building,
    };

    /// <summary>One row of the reference's <c>buildingStats</c> table (<c>buildinginfo.cpp:99-200</c>).</summary>
    /// <param name="Building">The building.</param>
    /// <param name="Race">The town it belongs to, <see langword="null"/> for every town.</param>
    /// <param name="Cost">The price.</param>
    private readonly record struct BuildingPrice(BuildingType Building, MapRace? Race, Cost Cost);

    /// <summary>A building every town has at the same price, the reference's <c>Race::ALL</c>.</summary>
    /// <param name="building">The building.</param>
    /// <param name="gold">The gold part.</param>
    /// <param name="wood">The wood part.</param>
    /// <param name="mercury">The mercury part.</param>
    /// <param name="ore">The ore part.</param>
    /// <param name="sulfur">The sulfur part.</param>
    /// <param name="crystal">The crystal part.</param>
    /// <param name="gems">The gems part.</param>
    /// <returns>The table row.</returns>
    private static BuildingPrice AnyRace(BuildingType building, ushort gold, byte wood = 0, byte mercury = 0, byte ore = 0, byte sulfur = 0, byte crystal = 0, byte gems = 0) =>
        new(building, null, new Cost { Gold = gold, Wood = wood, Mercury = mercury, Ore = ore, Sulfur = sulfur, Crystal = crystal, Gems = gems });

    /// <summary>A building whose price depends on the town, one row per race.</summary>
    /// <param name="race">The town.</param>
    /// <param name="building">The building.</param>
    /// <param name="gold">The gold part.</param>
    /// <param name="wood">The wood part.</param>
    /// <param name="mercury">The mercury part.</param>
    /// <param name="ore">The ore part.</param>
    /// <param name="sulfur">The sulfur part.</param>
    /// <param name="crystal">The crystal part.</param>
    /// <param name="gems">The gems part.</param>
    /// <returns>The table row.</returns>
    private static BuildingPrice ForRace(MapRace race, BuildingType building, ushort gold, byte wood = 0, byte mercury = 0, byte ore = 0, byte sulfur = 0, byte crystal = 0, byte gems = 0) =>
        new(building, race, new Cost { Gold = gold, Wood = wood, Mercury = mercury, Ore = ore, Sulfur = sulfur, Crystal = crystal, Gems = gems });

    /// <summary>
    /// The reference's <c>buildingStats</c>, row for row (<c>buildinginfo.cpp:106-200</c>).
    /// </summary>
    /// <remarks>
    /// The order and the numbers are the reference's, including the six identical rows for the landmark building
    /// and the six different ones for the special building — collapsed or reordered, a table this size stops being
    /// comparable side by side with the original, which is the only thing that keeps a transcription honest.
    /// </remarks>
    private static readonly BuildingPrice[] Prices =
    [
        // Buildings every town has.
        AnyRace(BuildingType.ThievesGuild, 750, wood: 5),
        AnyRace(BuildingType.Tavern, 500, wood: 5),
        AnyRace(BuildingType.Shipyard, 2000, wood: 20),
        AnyRace(BuildingType.Well, 500),
        AnyRace(BuildingType.Statue, 1250, ore: 5),
        AnyRace(BuildingType.LeftTurret, 1500, ore: 5),
        AnyRace(BuildingType.RightTurret, 1500, ore: 5),
        AnyRace(BuildingType.Marketplace, 500, wood: 5),
        AnyRace(BuildingType.Moat, 750),
        AnyRace(BuildingType.Castle, 5000, wood: 20, ore: 20),
        AnyRace(BuildingType.Captain, 500),
        AnyRace(BuildingType.MageGuild1, 2000, wood: 5, ore: 5),
        AnyRace(BuildingType.MageGuild2, 1000, wood: 5, mercury: 4, ore: 5, sulfur: 4, crystal: 4, gems: 4),
        AnyRace(BuildingType.MageGuild3, 1000, wood: 5, mercury: 6, ore: 5, sulfur: 6, crystal: 6, gems: 6),
        AnyRace(BuildingType.MageGuild4, 1000, wood: 5, mercury: 8, ore: 5, sulfur: 8, crystal: 8, gems: 8),
        AnyRace(BuildingType.MageGuild5, 1000, wood: 5, mercury: 10, ore: 5, sulfur: 10, crystal: 10, gems: 10),

        // The landmark building, the same price in all six towns.
        ForRace(MapRace.Knight, BuildingType.Well2, 1000),
        ForRace(MapRace.Barbarian, BuildingType.Well2, 1000),
        ForRace(MapRace.Sorceress, BuildingType.Well2, 1000),
        ForRace(MapRace.Warlock, BuildingType.Well2, 1000),
        ForRace(MapRace.Wizard, BuildingType.Well2, 1000),
        ForRace(MapRace.Necromancer, BuildingType.Well2, 1000),

        // The special building, which is where the towns really differ.
        ForRace(MapRace.Knight, BuildingType.Spec, 1500, wood: 5, ore: 15),
        ForRace(MapRace.Barbarian, BuildingType.Spec, 2000, wood: 10, ore: 10),
        ForRace(MapRace.Sorceress, BuildingType.Spec, 1500, crystal: 10),
        ForRace(MapRace.Warlock, BuildingType.Spec, 3000, wood: 5, ore: 10),
        ForRace(MapRace.Wizard, BuildingType.Spec, 1500, wood: 5, mercury: 5, ore: 5, sulfur: 5, crystal: 5, gems: 5),
        ForRace(MapRace.Necromancer, BuildingType.Spec, 1000, mercury: 10, sulfur: 10),

        // The necromancer's stand-in for the tavern.
        ForRace(MapRace.Necromancer, BuildingType.Shrine, 4000, wood: 10, crystal: 10),

        // The knight's dwellings: the only town that upgrades all of them.
        ForRace(MapRace.Knight, BuildingType.DwellingMonster1, 200),
        ForRace(MapRace.Knight, BuildingType.DwellingMonster2, 1000),
        ForRace(MapRace.Knight, BuildingType.DwellingUpgrade2, 1500, wood: 5),
        ForRace(MapRace.Knight, BuildingType.DwellingMonster3, 1000, ore: 5),
        ForRace(MapRace.Knight, BuildingType.DwellingUpgrade3, 1500, ore: 5),
        ForRace(MapRace.Knight, BuildingType.DwellingMonster4, 2000, wood: 10, ore: 10),
        ForRace(MapRace.Knight, BuildingType.DwellingUpgrade4, 2000, wood: 5, ore: 5),
        ForRace(MapRace.Knight, BuildingType.DwellingMonster5, 3000, wood: 20),
        ForRace(MapRace.Knight, BuildingType.DwellingUpgrade5, 3000, wood: 10),
        ForRace(MapRace.Knight, BuildingType.DwellingMonster6, 5000, wood: 20, crystal: 20),
        ForRace(MapRace.Knight, BuildingType.DwellingUpgrade6, 5000, wood: 10, crystal: 10),

        ForRace(MapRace.Barbarian, BuildingType.DwellingMonster1, 300),
        ForRace(MapRace.Barbarian, BuildingType.DwellingMonster2, 800, wood: 5),
        ForRace(MapRace.Barbarian, BuildingType.DwellingUpgrade2, 1200, wood: 5),
        ForRace(MapRace.Barbarian, BuildingType.DwellingMonster3, 1000),
        ForRace(MapRace.Barbarian, BuildingType.DwellingMonster4, 2000, wood: 10, ore: 10),
        ForRace(MapRace.Barbarian, BuildingType.DwellingUpgrade4, 3000, wood: 5, ore: 5),
        ForRace(MapRace.Barbarian, BuildingType.DwellingMonster5, 4000, ore: 20),
        ForRace(MapRace.Barbarian, BuildingType.DwellingUpgrade5, 2000, ore: 10),
        ForRace(MapRace.Barbarian, BuildingType.DwellingMonster6, 6000, ore: 20, crystal: 20),

        ForRace(MapRace.Sorceress, BuildingType.DwellingMonster1, 500, wood: 5),
        ForRace(MapRace.Sorceress, BuildingType.DwellingMonster2, 1000, wood: 5),
        ForRace(MapRace.Sorceress, BuildingType.DwellingUpgrade2, 1500, wood: 5),
        ForRace(MapRace.Sorceress, BuildingType.DwellingMonster3, 1500),
        ForRace(MapRace.Sorceress, BuildingType.DwellingUpgrade3, 1500, wood: 5),
        ForRace(MapRace.Sorceress, BuildingType.DwellingMonster4, 2500, ore: 10),
        ForRace(MapRace.Sorceress, BuildingType.DwellingUpgrade4, 1500, mercury: 5),
        ForRace(MapRace.Sorceress, BuildingType.DwellingMonster5, 3000, wood: 10, gems: 10),
        ForRace(MapRace.Sorceress, BuildingType.DwellingMonster6, 10000, mercury: 20, ore: 30),

        // The warlock: two dwelling upgrades, and a sixth dwelling that upgrades twice.
        ForRace(MapRace.Warlock, BuildingType.DwellingMonster1, 500),
        ForRace(MapRace.Warlock, BuildingType.DwellingMonster2, 1000, ore: 10),
        ForRace(MapRace.Warlock, BuildingType.DwellingMonster3, 2000),
        ForRace(MapRace.Warlock, BuildingType.DwellingMonster4, 3000, gems: 10),
        ForRace(MapRace.Warlock, BuildingType.DwellingUpgrade4, 2000, gems: 5),
        ForRace(MapRace.Warlock, BuildingType.DwellingMonster5, 4000, sulfur: 10),
        ForRace(MapRace.Warlock, BuildingType.DwellingMonster6, 15000, ore: 30, sulfur: 20),
        ForRace(MapRace.Warlock, BuildingType.DwellingUpgrade6, 5000, ore: 5, sulfur: 10),
        ForRace(MapRace.Warlock, BuildingType.DwellingUpgrade7, 5000, ore: 5, sulfur: 10),

        ForRace(MapRace.Wizard, BuildingType.DwellingMonster1, 400),
        ForRace(MapRace.Wizard, BuildingType.DwellingMonster2, 800),
        ForRace(MapRace.Wizard, BuildingType.DwellingMonster3, 1500, wood: 5, ore: 5),
        ForRace(MapRace.Wizard, BuildingType.DwellingUpgrade3, 1500, mercury: 5),
        ForRace(MapRace.Wizard, BuildingType.DwellingMonster4, 3000, wood: 5),
        ForRace(MapRace.Wizard, BuildingType.DwellingMonster5, 3500, wood: 5, mercury: 5, ore: 5, sulfur: 5, crystal: 5, gems: 5),
        ForRace(MapRace.Wizard, BuildingType.DwellingUpgrade5, 4000, wood: 5, ore: 5),
        ForRace(MapRace.Wizard, BuildingType.DwellingMonster6, 12500, wood: 5, ore: 5, gems: 20),
        ForRace(MapRace.Wizard, BuildingType.DwellingUpgrade6, 12500, wood: 5, ore: 5, gems: 20),

        ForRace(MapRace.Necromancer, BuildingType.DwellingMonster1, 400),
        ForRace(MapRace.Necromancer, BuildingType.DwellingMonster2, 1000),
        ForRace(MapRace.Necromancer, BuildingType.DwellingUpgrade2, 1000),
        ForRace(MapRace.Necromancer, BuildingType.DwellingMonster3, 1500, ore: 10),
        ForRace(MapRace.Necromancer, BuildingType.DwellingUpgrade3, 1500, ore: 5),
        ForRace(MapRace.Necromancer, BuildingType.DwellingMonster4, 3000, wood: 10),
        ForRace(MapRace.Necromancer, BuildingType.DwellingUpgrade4, 4000, wood: 5, crystal: 10, gems: 10),
        ForRace(MapRace.Necromancer, BuildingType.DwellingMonster5, 4000, wood: 10, sulfur: 10),
        ForRace(MapRace.Necromancer, BuildingType.DwellingUpgrade5, 3000, ore: 5, crystal: 5),
        ForRace(MapRace.Necromancer, BuildingType.DwellingMonster6, 10000, wood: 10, mercury: 5, ore: 10, sulfur: 5, crystal: 5, gems: 5),
    ];
}
