namespace FHeroes2.Game.Castle;

/// <summary>
/// The buildings of a town, the reference's <c>enum BuildingType</c> (<c>castle/castle.h:68-110</c>).
/// </summary>
/// <remarks>
/// <para>
/// A bitmask, not an enumeration: <c>Castle::building</c> is a single <c>uint32_t</c> holding every building the
/// town owns, and "is the market place built" is one bit test. The three combined members
/// (<see cref="MageGuild"/>, <see cref="DwellingMonsters"/>, <see cref="DwellingUpgrades"/>) are masks over their
/// siblings and are as much values of the enum as the single bits are.
/// </para>
/// <para>
/// The dwelling bits used to live in <c>Monsters/DwellingMonsters.cs</c> as a <c>Dwelling</c> class of <c>uint</c>
/// constants, because the castle module did not exist when <see cref="Monsters.DwellingMonsters"/> landed and the
/// dwelling of a monster is a monster fact. They are where the reference keeps them now; the <c>Dwelling</c> class
/// is gone and every caller names <see cref="BuildingType.DwellingMonster1"/> and its siblings directly.
/// </para>
/// <para>
/// Two names are kept close to the reference on purpose. <see cref="Well2"/> is <c>BUILD_WEL2</c>, the second
/// "well" that is really the race's landmark building (the farm, the garbage heap, the crystal garden, the
/// waterfall, the orchard, the skull pile); <see cref="Spec"/> is <c>BUILD_SPEC</c>, the race's special building
/// (fortifications, the coliseum, the rainbow, the dungeon, the library, the storm). Renaming them to something
/// prettier would break the one thing the names are for — grepping the reference.
/// </para>
/// </remarks>
[Flags]
public enum BuildingType : uint
{
    /// <summary>No building at all, <c>BUILD_NOTHING</c>.</summary>
    Nothing = 0x00000000,

    /// <summary>The thieves' guild, <c>BUILD_THIEVESGUILD</c>.</summary>
    ThievesGuild = 0x00000001,

    /// <summary>The tavern, <c>BUILD_TAVERN</c>.</summary>
    Tavern = 0x00000002,

    /// <summary>The shipyard, <c>BUILD_SHIPYARD</c>.</summary>
    Shipyard = 0x00000004,

    /// <summary>The well, <c>BUILD_WELL</c>.</summary>
    Well = 0x00000008,

    /// <summary>The statue, <c>BUILD_STATUE</c>.</summary>
    Statue = 0x00000010,

    /// <summary>The left turret, <c>BUILD_LEFTTURRET</c>.</summary>
    LeftTurret = 0x00000020,

    /// <summary>The right turret, <c>BUILD_RIGHTTURRET</c>.</summary>
    RightTurret = 0x00000040,

    /// <summary>The market place, <c>BUILD_MARKETPLACE</c>.</summary>
    Marketplace = 0x00000080,

    /// <summary>
    /// The race's landmark building, <c>BUILD_WEL2</c>: the farm, the garbage heap, the crystal garden, the
    /// waterfall, the orchard or the skull pile depending on the race.
    /// </summary>
    Well2 = 0x00000100,

    /// <summary>The moat, <c>BUILD_MOAT</c>.</summary>
    Moat = 0x00000200,

    /// <summary>
    /// The race's special building, <c>BUILD_SPEC</c>: the fortifications, the coliseum, the rainbow, the
    /// dungeon, the library or the storm.
    /// </summary>
    Spec = 0x00000400,

    /// <summary>The castle proper — the wall and the gate, <c>BUILD_CASTLE</c>.</summary>
    Castle = 0x00000800,

    /// <summary>The captain's quarters, <c>BUILD_CAPTAIN</c>.</summary>
    Captain = 0x00001000,

    /// <summary>
    /// The shrine, <c>BUILD_SHRINE</c> — the building the necromancer town gets instead of a tavern in the later
    /// map versions.
    /// </summary>
    Shrine = 0x00002000,

    /// <summary>The first level of the mage guild, <c>BUILD_MAGEGUILD1</c>.</summary>
    MageGuild1 = 0x00004000,

    /// <summary>The second level of the mage guild, <c>BUILD_MAGEGUILD2</c>.</summary>
    MageGuild2 = 0x00008000,

    /// <summary>The third level of the mage guild, <c>BUILD_MAGEGUILD3</c>.</summary>
    MageGuild3 = 0x00010000,

    /// <summary>The fourth level of the mage guild, <c>BUILD_MAGEGUILD4</c>.</summary>
    MageGuild4 = 0x00020000,

    /// <summary>The fifth level of the mage guild, <c>BUILD_MAGEGUILD5</c>.</summary>
    MageGuild5 = 0x00040000,

    /// <summary>Any level of the mage guild, <c>BUILD_MAGEGUILD</c>.</summary>
    MageGuild = MageGuild1 | MageGuild2 | MageGuild3 | MageGuild4 | MageGuild5,

    /// <summary>The tent, <c>BUILD_TENT</c> — the town before its castle is built.</summary>
    Tent = 0x00080000,

    /// <summary>The first dwelling, <c>DWELLING_MONSTER1</c>.</summary>
    DwellingMonster1 = 0x00100000,

    /// <summary>The second dwelling, <c>DWELLING_MONSTER2</c>.</summary>
    DwellingMonster2 = 0x00200000,

    /// <summary>The third dwelling, <c>DWELLING_MONSTER3</c>.</summary>
    DwellingMonster3 = 0x00400000,

    /// <summary>The fourth dwelling, <c>DWELLING_MONSTER4</c>.</summary>
    DwellingMonster4 = 0x00800000,

    /// <summary>The fifth dwelling, <c>DWELLING_MONSTER5</c>.</summary>
    DwellingMonster5 = 0x01000000,

    /// <summary>The sixth dwelling, <c>DWELLING_MONSTER6</c>.</summary>
    DwellingMonster6 = 0x02000000,

    /// <summary>Any of the six dwellings, <c>DWELLING_MONSTERS</c>.</summary>
    DwellingMonsters = DwellingMonster1 | DwellingMonster2 | DwellingMonster3 | DwellingMonster4 | DwellingMonster5 | DwellingMonster6,

    /// <summary>The upgrade of the second dwelling, <c>DWELLING_UPGRADE2</c>.</summary>
    DwellingUpgrade2 = 0x04000000,

    /// <summary>The upgrade of the third dwelling, <c>DWELLING_UPGRADE3</c>.</summary>
    DwellingUpgrade3 = 0x08000000,

    /// <summary>The upgrade of the fourth dwelling, <c>DWELLING_UPGRADE4</c>.</summary>
    DwellingUpgrade4 = 0x10000000,

    /// <summary>The upgrade of the fifth dwelling, <c>DWELLING_UPGRADE5</c>.</summary>
    DwellingUpgrade5 = 0x20000000,

    /// <summary>The upgrade of the sixth dwelling, <c>DWELLING_UPGRADE6</c>.</summary>
    DwellingUpgrade6 = 0x40000000,

    /// <summary>
    /// The second upgrade of the sixth dwelling — the warlock's black dragon lair, <c>DWELLING_UPGRADE7</c>.
    /// </summary>
    /// <remarks>
    /// The only building whose bit is the top of the <c>uint32_t</c>, which is why the reference's enumerator
    /// list has to spell it <c>0x80000000</c> and why the port's enum is <c>uint</c> rather than <c>int</c>.
    /// </remarks>
    DwellingUpgrade7 = 0x80000000,

    /// <summary>Any of the dwelling upgrades, <c>DWELLING_UPGRADES</c>.</summary>
    DwellingUpgrades = DwellingUpgrade2 | DwellingUpgrade3 | DwellingUpgrade4 | DwellingUpgrade5 | DwellingUpgrade6 | DwellingUpgrade7,
}
