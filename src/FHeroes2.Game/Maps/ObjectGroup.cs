namespace FHeroes2.Game.Maps;

/// <summary>
/// The editor groups map objects into, the port of <c>Maps::ObjectGroup</c>
/// (<c>参考：src/fheroes2/maps/map_object_info.h:127</c>).
///
/// <para>
/// Objects that share an ICN resource do not necessarily share a type, and one type may be spread
/// over several resources; the group and the index inside it are what the fheroes2 editor stores to
/// place an object again, so <b>the order is part of the map format and must not be changed</b>.
/// These groups are not the ones of the original editor.
/// </para>
/// </summary>
public enum ObjectGroup : byte
{
    /// <summary><c>NONE</c>: not used by the editor but still part of a tile.</summary>
    None = 0,

    /// <summary><c>ROADS</c>: the 512 road layouts plus the castle entrance road.</summary>
    Roads = 1,

    /// <summary><c>STREAMS</c>: the thirteen stream sprites, no direction combinations.</summary>
    Streams = 2,

    /// <summary><c>LANDSCAPE_MOUNTAINS</c>.</summary>
    LandscapeMountains = 3,

    /// <summary><c>LANDSCAPE_ROCKS</c>.</summary>
    LandscapeRocks = 4,

    /// <summary><c>LANDSCAPE_TREES</c>.</summary>
    LandscapeTrees = 5,

    /// <summary><c>LANDSCAPE_WATER</c>.</summary>
    LandscapeWater = 6,

    /// <summary><c>LANDSCAPE_MISCELLANEOUS</c>.</summary>
    LandscapeMiscellaneous = 7,

    /// <summary><c>LANDSCAPE_TOWN_BASEMENTS</c>: placed together with towns, never alone.</summary>
    LandscapeTownBasements = 8,

    /// <summary><c>LANDSCAPE_FLAGS</c>: placed together with other objects.</summary>
    LandscapeFlags = 9,

    /// <summary><c>ADVENTURE_ARTIFACTS</c>.</summary>
    AdventureArtifacts = 10,

    /// <summary><c>ADVENTURE_DWELLINGS</c>.</summary>
    AdventureDwellings = 11,

    /// <summary><c>ADVENTURE_MINES</c>.</summary>
    AdventureMines = 12,

    /// <summary><c>ADVENTURE_POWER_UPS</c>.</summary>
    AdventurePowerUps = 13,

    /// <summary><c>ADVENTURE_TREASURES</c>.</summary>
    AdventureTreasures = 14,

    /// <summary><c>ADVENTURE_WATER</c>.</summary>
    AdventureWater = 15,

    /// <summary><c>ADVENTURE_MISCELLANEOUS</c>.</summary>
    AdventureMiscellaneous = 16,

    /// <summary><c>KINGDOM_HEROES</c>.</summary>
    KingdomHeroes = 17,

    /// <summary><c>KINGDOM_TOWNS</c>.</summary>
    KingdomTowns = 18,

    /// <summary><c>MONSTERS</c>.</summary>
    Monsters = 19,

    /// <summary><c>MAP_EXTRAS</c>: objects that are not placed by the editor.</summary>
    MapExtras = 20,

    /// <summary><c>GROUP_COUNT</c>: number of groups, not a group itself.</summary>
    GroupCount = 21,
}
