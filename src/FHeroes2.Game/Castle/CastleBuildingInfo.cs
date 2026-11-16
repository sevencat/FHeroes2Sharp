using System.Text;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Castle;

/// <summary>
/// What a building looks like and what it is called: the sprite rectangle in the town view, the name, the
/// description and the drawing order — <c>castle/castle_building_info.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the six towns as six anonymous-namespace functions returning an area, a name, a description
/// and a drawing order, and then one public function per question that switches on the race and calls the right
/// one. Both halves are here: the public functions below, and <see cref="CastleBuildingInfoTables"/>, which is
/// generated from the reference's tables by a script.
/// </para>
/// <para>
/// The requirement table (<see cref="CastleBuildings.GetRequirement"/>) and the upgrade chain
/// (<see cref="CastleBuildings.GetUpgradeForBuilding"/>) live in <see cref="CastleBuildings"/> because a
/// <c>Castle</c> needs them before it can draw anything; <see cref="GetRequirementString"/> is here because it
/// needs <see cref="GetName"/> and is only used by the dialog.
/// </para>
/// <para>
/// The reference returns <c>const char *</c> from the translation macros, and <see langword="nullptr"/> for a race
/// it does not know. The port returns <see cref="string.Empty"/> instead: every caller concatenates the answer, and
/// the reference asserts before it can reach the null.
/// </para>
/// </remarks>
public static class CastleBuildingInfo
{
    /// <summary>
    /// The rectangles the building occupies in the town view, <c>fheroes2::getCastleBuildingArea()</c>
    /// (<c>castle_building_info.cpp:806-834</c>).
    /// </summary>
    /// <remarks>
    /// Several buildings are drawn as more than one sprite — the statue is a body and a sword, the fifth dwelling of
    /// the knight is a hut and a roof — which is why this is a list and not one rectangle.
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building.</param>
    /// <returns>The rectangles, empty for <see cref="BuildingType.Nothing"/> and for an unknown race.</returns>
    public static IReadOnlyList<Rect> GetArea(MapRace race, BuildingType building)
    {
        if (building == BuildingType.Nothing)
        {
            return [];
        }

        return race switch
        {
            MapRace.Knight => CastleBuildingInfoTables.KnightArea(building),
            MapRace.Barbarian => CastleBuildingInfoTables.BarbarianArea(building),
            MapRace.Sorceress => CastleBuildingInfoTables.SorceressArea(building),
            MapRace.Warlock => CastleBuildingInfoTables.WarlockArea(building),
            MapRace.Wizard => CastleBuildingInfoTables.WizardArea(building),
            MapRace.Necromancer => CastleBuildingInfoTables.NecromancerArea(building),
            _ => [],
        };
    }

    /// <summary>
    /// The name of a building, <c>fheroes2::getBuildingName()</c> (<c>castle_building_info.cpp:836-906</c>).
    /// </summary>
    /// <remarks>
    /// The buildings every town shares are named first and the same way everywhere — the tavern is a tavern in all
    /// six towns — and only then does the race's own table answer. That is also why the race's table returns
    /// <see cref="string.Empty"/> for the shared buildings rather than the same string six times.
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building.</param>
    /// <returns>The name, empty for <see cref="BuildingType.Nothing"/>.</returns>
    public static string GetName(MapRace race, BuildingType building)
    {
        if (building == BuildingType.Nothing)
        {
            return string.Empty;
        }

        string? common = building switch
        {
            BuildingType.ThievesGuild => "Thieves' Guild",
            BuildingType.Tavern => "Tavern",
            BuildingType.Shipyard => "Shipyard",
            BuildingType.Well => "Well",
            BuildingType.Statue => "Statue",
            BuildingType.LeftTurret => "Left Turret",
            BuildingType.RightTurret => "Right Turret",
            BuildingType.Marketplace => "Marketplace",
            BuildingType.Moat => "Moat",
            BuildingType.Castle => "Castle",
            BuildingType.Tent => "Tent",
            BuildingType.Captain => "Captain's Quarters",
            BuildingType.MageGuild1 => "Mage Guild, Level 1",
            BuildingType.MageGuild2 => "Mage Guild, Level 2",
            BuildingType.MageGuild3 => "Mage Guild, Level 3",
            BuildingType.MageGuild4 => "Mage Guild, Level 4",
            BuildingType.MageGuild5 => "Mage Guild, Level 5",
            _ => null,
        };

        if (common is not null)
        {
            return common;
        }

        return race switch
        {
            MapRace.Knight => CastleBuildingInfoTables.KnightName(building),
            MapRace.Barbarian => CastleBuildingInfoTables.BarbarianName(building),
            MapRace.Sorceress => CastleBuildingInfoTables.SorceressName(building),
            MapRace.Warlock => CastleBuildingInfoTables.WarlockName(building),
            MapRace.Wizard => CastleBuildingInfoTables.WizardName(building),
            MapRace.Necromancer => CastleBuildingInfoTables.NecromancerName(building),
            MapRace.Random => CastleBuildingInfoTables.RandomName(building),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The description of a building, <c>fheroes2::getBuildingDescription()</c>
    /// (<c>castle_building_info.cpp:908-978</c>).
    /// </summary>
    /// <remarks>
    /// The shared buildings are described first, the race's own afterwards — the same two-step answer as
    /// <see cref="GetName"/>. The placeholders (<c>%{count}</c>) are the reference's: the caller substitutes the
    /// value the building is worth in that town, which is why the texts cannot be merged per building.
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building.</param>
    /// <returns>The description, empty for <see cref="BuildingType.Nothing"/>.</returns>
    public static string GetDescription(MapRace race, BuildingType building)
    {
        if (building == BuildingType.Nothing)
        {
            return string.Empty;
        }

        string? common = building switch
        {
            BuildingType.Shrine => "The Shrine increases the necromancy skill of all your necromancers by 10 percent.",
            BuildingType.ThievesGuild => "The Thieves' Guild provides information on enemy players. Thieves' Guilds can also provide scouting information on enemy towns. Additional Guilds provide more information.",
            BuildingType.Tavern => "The Tavern increases morale for troops defending the castle.",
            BuildingType.Shipyard => "The Shipyard allows ships to be built.",
            BuildingType.Well => "The Well increases the growth rate of all dwellings by %{count} creatures per week.",
            BuildingType.Statue => "The Statue increases the town's income by %{count} gold per day.",
            BuildingType.LeftTurret => "The Left Turret provides extra firepower during castle combat.",
            BuildingType.RightTurret => "The Right Turret provides extra firepower during castle combat.",
            BuildingType.Marketplace => "The Marketplace can be used to convert one type of resource into another. The more marketplaces you control, the better the exchange rate.",
            BuildingType.Moat => "The Moat slows and weakens attacking units. Any walking unit entering the moat must end its turn there and can only move within the moat one hex at a time. Any creature present in the moat will have its defense skill reduced by %{count}.",
            BuildingType.Castle => "The Castle improves the town's defense and increases its income to %{count} gold per day.",
            BuildingType.Tent => "The Tent provides workers to build a castle, provided the materials and the gold are available.",
            BuildingType.Captain => "The Captain's Quarters provides a captain to assist in the castle's defense when no hero is present.",
            BuildingType.MageGuild1 or BuildingType.MageGuild2 or BuildingType.MageGuild3 or BuildingType.MageGuild4 or BuildingType.MageGuild5 => "The Mage Guild allows heroes to learn spells and replenish their spell points.",
            _ => null,
        };

        if (common is not null)
        {
            return common;
        }

        return race switch
        {
            MapRace.Knight => CastleBuildingInfoTables.KnightDescription(building),
            MapRace.Barbarian => CastleBuildingInfoTables.BarbarianDescription(building),
            MapRace.Sorceress => CastleBuildingInfoTables.SorceressDescription(building),
            MapRace.Warlock => CastleBuildingInfoTables.WarlockDescription(building),
            MapRace.Wizard => CastleBuildingInfoTables.WizardDescription(building),
            MapRace.Necromancer => CastleBuildingInfoTables.NecromancerDescription(building),
            MapRace.Random => CastleBuildingInfoTables.RandomDescription(building),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The prerequisites of a building as names, one per line, <c>fheroes2::getBuildingRequirementString()</c>
    /// (<c>castle_building_info.cpp:1352-1371</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="CastleBuildings.GetRequirement"/> answers a bitmask; the dialog shows text. The reference walks the
    /// mask bit by bit and asks <c>Castle::GetStringBuilding</c>, which is <see cref="GetName"/> with the building as
    /// the only difference — so the order is by bit, not by the order the requirements were written in.
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <param name="building">The building to be built.</param>
    /// <returns>The names separated by <c>\n</c>, or an empty string when nothing must be built first.</returns>
    public static string GetRequirementString(MapRace race, BuildingType building)
    {
        uint requirements = (uint)CastleBuildings.GetRequirement(race, building);

        if (requirements == 0)
        {
            return string.Empty;
        }

        StringBuilder text = new();

        for (uint bit = 0x00000001; bit != 0; bit <<= 1)
        {
            if ((requirements & bit) != 0)
            {
                text.Append(GetName(race, (BuildingType)bit)).Append('\n');
            }
        }

        // Remove the last separator.
        if (text.Length > 0)
        {
            text.Length -= 1;
        }

        return text.ToString();
    }

    /// <summary>
    /// Which sprite of the town's icon set draws a building, <c>fheroes2::getIndexBuildingSprite()</c>
    /// (<c>castle_building_info.cpp:1373-1438</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The numbers are the reference's indices into <c>ICN::TOWN*</c>: 0 is the mage guild, 1 the thieves' guild,
    /// 2 the tavern (and the necromancer's shrine, which is drawn as one), 3 the shipyard, 4 the well, 6 the castle,
    /// and the dwellings start at 19 with their upgrades at 25.
    /// </para>
    /// <para>
    /// Five, fourteen, sixteen and up are not used by any building, and the tent — which has no sprite of its own,
    /// the town draws the unfinished castle instead — falls into the reference's default and answers zero.
    /// </para>
    /// </remarks>
    /// <param name="building">The building.</param>
    /// <returns>The index into the town's icon set; zero for a building with no sprite of its own.</returns>
    public static int GetSpriteIndex(BuildingType building) => building switch
    {
        BuildingType.DwellingMonster1 => 19,
        BuildingType.DwellingMonster2 => 20,
        BuildingType.DwellingMonster3 => 21,
        BuildingType.DwellingMonster4 => 22,
        BuildingType.DwellingMonster5 => 23,
        BuildingType.DwellingMonster6 => 24,
        BuildingType.DwellingUpgrade2 => 25,
        BuildingType.DwellingUpgrade3 => 26,
        BuildingType.DwellingUpgrade4 => 27,
        BuildingType.DwellingUpgrade5 => 28,
        BuildingType.DwellingUpgrade6 => 29,
        BuildingType.DwellingUpgrade7 => 30,

        // All five levels of the mage guild share one sprite: the guild only grows taller.
        BuildingType.MageGuild => 0,

        BuildingType.ThievesGuild => 1,
        BuildingType.Shrine or BuildingType.Tavern => 2,
        BuildingType.Shipyard => 3,
        BuildingType.Well => 4,
        BuildingType.Castle => 6,
        BuildingType.Statue => 7,
        BuildingType.LeftTurret => 8,
        BuildingType.RightTurret => 9,
        BuildingType.Marketplace => 10,
        BuildingType.Well2 => 11,
        BuildingType.Moat => 12,
        BuildingType.Spec => 13,
        BuildingType.Captain => 15,
        _ => 0,
    };

    /// <summary>
    /// The order the town view draws the buildings in, <c>fheroes2::getBuildingDrawingPriorities()</c>
    /// (<c>castle_building_info.cpp:1440-1638</c>).
    /// </summary>
    /// <remarks>
    /// Later buildings cover earlier ones, so the order is not the order of the bits and not alphabetical: it is the
    /// reference's painter's algorithm. The necromancer's shrine is a <see cref="MapVersion.PriceOfLoyalty"/>
    /// addition and is drawn right after his special building on every map that is not a
    /// <see cref="MapVersion.SuccessionWars"/> one.
    /// </remarks>
    /// <param name="race">The town's race.</param>
    /// <param name="version">The map version, which decides whether the shrine is in the list.</param>
    /// <returns>The buildings, from the back to the front.</returns>
    public static IReadOnlyList<BuildingType> GetDrawingPriorities(MapRace race, MapVersion version) =>
        CastleBuildingInfoTables.DrawingPriorities(race, version);
}
