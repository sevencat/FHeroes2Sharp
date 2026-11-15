using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The dwelling bits of <c>BuildingType</c> (<c>castle/castle.h:94-105</c>).
/// </summary>
/// <remarks>
/// In the reference these are enumerators of <c>BuildingType</c> and live with the rest of the castle
/// buildings. The port declares them here because the dwelling of a monster is a monster fact
/// (<see cref="DwellingMonsters.FromDwelling"/>) and the castle module does not exist yet; when it lands the
/// enumerators move there and this class becomes the reference to them.
/// </remarks>
public static class Dwelling
{
    /// <summary>The first dwelling of a town, <c>DWELLING_MONSTER1</c>.</summary>
    public const uint Monster1 = 0x00100000;

    /// <summary>The second dwelling, <c>DWELLING_MONSTER2</c>.</summary>
    public const uint Monster2 = 0x00200000;

    /// <summary>The third dwelling, <c>DWELLING_MONSTER3</c>.</summary>
    public const uint Monster3 = 0x00400000;

    /// <summary>The fourth dwelling, <c>DWELLING_MONSTER4</c>.</summary>
    public const uint Monster4 = 0x00800000;

    /// <summary>The fifth dwelling, <c>DWELLING_MONSTER5</c>.</summary>
    public const uint Monster5 = 0x01000000;

    /// <summary>The sixth dwelling, <c>DWELLING_MONSTER6</c>.</summary>
    public const uint Monster6 = 0x02000000;

    /// <summary>Upgrade of the second dwelling, <c>DWELLING_UPGRADE2</c>.</summary>
    public const uint Upgrade2 = 0x04000000;

    /// <summary>Upgrade of the third dwelling, <c>DWELLING_UPGRADE3</c>.</summary>
    public const uint Upgrade3 = 0x08000000;

    /// <summary>Upgrade of the fourth dwelling, <c>DWELLING_UPGRADE4</c>.</summary>
    public const uint Upgrade4 = 0x10000000;

    /// <summary>Upgrade of the fifth dwelling, <c>DWELLING_UPGRADE5</c>.</summary>
    public const uint Upgrade5 = 0x20000000;

    /// <summary>Upgrade of the sixth dwelling, <c>DWELLING_UPGRADE6</c>.</summary>
    public const uint Upgrade6 = 0x40000000;

    /// <summary>
    /// The second upgrade of the sixth dwelling, <c>DWELLING_UPGRADE7</c> (<c>castle.h:106</c>).
    /// </summary>
    /// <remarks>
    /// The reference defines it as <c>0x80000000</c>, which does not fit the signed <c>int</c> of the
    /// enumerator list; the port keeps it unsigned for that reason.
    /// </remarks>
    public const uint Upgrade7 = 0x80000000;
}

/// <summary>
/// <c>Monster::FromDwelling()</c> (<c>monster.cpp:338</c>): which creature a town of a race puts in one of
/// its dwellings.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place where "the third dwelling of the Warlock town" becomes a concrete monster, so the
/// table is the source of truth for both <c>Army::Reset()</c> and the castle screen. The rows follow the
/// reference exactly, including the four cases where the "upgrade" dwelling holds the same creature as the
/// base one — the Warlock and Wizard towns have no upgrade for Gargoyles, Griffins and Rocs, and the
/// Barbarian and Sorceress towns never upgrade their sixth dwelling.
/// </para>
/// <para>
/// An unknown race or dwelling returns <see cref="MonsterType.Unknown"/>, which is what the reference's
/// <c>default:</c> branches fall through to.
/// </para>
/// </remarks>
public static class DwellingMonsters
{
    /// <summary>The monster of a dwelling, <c>Monster::FromDwelling()</c>.</summary>
    /// <param name="race">Race of the town.</param>
    /// <param name="dwelling">Dwelling bit, one of the <see cref="Dwelling"/> constants.</param>
    /// <returns>The monster, or <see cref="MonsterType.Unknown"/> when the pair is not in the table.</returns>
    public static MonsterType FromDwelling(MapRace race, uint dwelling) => (race, dwelling) switch
    {
        (MapRace.Knight, Dwelling.Monster1) => MonsterType.Peasant,
        (MapRace.Barbarian, Dwelling.Monster1) => MonsterType.Goblin,
        (MapRace.Sorceress, Dwelling.Monster1) => MonsterType.Sprite,
        (MapRace.Warlock, Dwelling.Monster1) => MonsterType.Centaur,
        (MapRace.Wizard, Dwelling.Monster1) => MonsterType.Halfling,
        (MapRace.Necromancer, Dwelling.Monster1) => MonsterType.Skeleton,

        (MapRace.Knight, Dwelling.Monster2) => MonsterType.Archer,
        (MapRace.Barbarian, Dwelling.Monster2) => MonsterType.Orc,
        (MapRace.Sorceress, Dwelling.Monster2) => MonsterType.Dwarf,
        (MapRace.Warlock, Dwelling.Monster2) => MonsterType.Gargoyle,
        (MapRace.Wizard, Dwelling.Monster2) => MonsterType.Boar,
        (MapRace.Necromancer, Dwelling.Monster2) => MonsterType.Zombie,

        (MapRace.Knight, Dwelling.Upgrade2) => MonsterType.Ranger,
        (MapRace.Barbarian, Dwelling.Upgrade2) => MonsterType.OrcChief,
        (MapRace.Sorceress, Dwelling.Upgrade2) => MonsterType.BattleDwarf,
        (MapRace.Warlock, Dwelling.Upgrade2) => MonsterType.Gargoyle,
        (MapRace.Wizard, Dwelling.Upgrade2) => MonsterType.Boar,
        (MapRace.Necromancer, Dwelling.Upgrade2) => MonsterType.MutantZombie,

        (MapRace.Knight, Dwelling.Monster3) => MonsterType.Pikeman,
        (MapRace.Barbarian, Dwelling.Monster3) => MonsterType.Wolf,
        (MapRace.Sorceress, Dwelling.Monster3) => MonsterType.Elf,
        (MapRace.Warlock, Dwelling.Monster3) => MonsterType.Griffin,
        (MapRace.Wizard, Dwelling.Monster3) => MonsterType.IronGolem,
        (MapRace.Necromancer, Dwelling.Monster3) => MonsterType.Mummy,

        (MapRace.Knight, Dwelling.Upgrade3) => MonsterType.VeteranPikeman,
        (MapRace.Barbarian, Dwelling.Upgrade3) => MonsterType.Wolf,
        (MapRace.Sorceress, Dwelling.Upgrade3) => MonsterType.GrandElf,
        (MapRace.Warlock, Dwelling.Upgrade3) => MonsterType.Griffin,
        (MapRace.Wizard, Dwelling.Upgrade3) => MonsterType.SteelGolem,
        (MapRace.Necromancer, Dwelling.Upgrade3) => MonsterType.RoyalMummy,

        (MapRace.Knight, Dwelling.Monster4) => MonsterType.Swordsman,
        (MapRace.Barbarian, Dwelling.Monster4) => MonsterType.Ogre,
        (MapRace.Sorceress, Dwelling.Monster4) => MonsterType.Druid,
        (MapRace.Warlock, Dwelling.Monster4) => MonsterType.Minotaur,
        (MapRace.Wizard, Dwelling.Monster4) => MonsterType.Roc,
        (MapRace.Necromancer, Dwelling.Monster4) => MonsterType.Vampire,

        (MapRace.Knight, Dwelling.Upgrade4) => MonsterType.MasterSwordsman,
        (MapRace.Barbarian, Dwelling.Upgrade4) => MonsterType.OgreLord,
        (MapRace.Sorceress, Dwelling.Upgrade4) => MonsterType.GreaterDruid,
        (MapRace.Warlock, Dwelling.Upgrade4) => MonsterType.MinotaurKing,
        (MapRace.Wizard, Dwelling.Upgrade4) => MonsterType.Roc,
        (MapRace.Necromancer, Dwelling.Upgrade4) => MonsterType.VampireLord,

        (MapRace.Knight, Dwelling.Monster5) => MonsterType.Cavalry,
        (MapRace.Barbarian, Dwelling.Monster5) => MonsterType.Troll,
        (MapRace.Sorceress, Dwelling.Monster5) => MonsterType.Unicorn,
        (MapRace.Warlock, Dwelling.Monster5) => MonsterType.Hydra,
        (MapRace.Wizard, Dwelling.Monster5) => MonsterType.Mage,
        (MapRace.Necromancer, Dwelling.Monster5) => MonsterType.Lich,

        (MapRace.Knight, Dwelling.Upgrade5) => MonsterType.Champion,
        (MapRace.Barbarian, Dwelling.Upgrade5) => MonsterType.WarTroll,
        (MapRace.Sorceress, Dwelling.Upgrade5) => MonsterType.Unicorn,
        (MapRace.Warlock, Dwelling.Upgrade5) => MonsterType.Hydra,
        (MapRace.Wizard, Dwelling.Upgrade5) => MonsterType.Archmage,
        (MapRace.Necromancer, Dwelling.Upgrade5) => MonsterType.PowerLich,

        (MapRace.Knight, Dwelling.Monster6) => MonsterType.Paladin,
        (MapRace.Barbarian, Dwelling.Monster6) => MonsterType.Cyclops,
        (MapRace.Sorceress, Dwelling.Monster6) => MonsterType.Phoenix,
        (MapRace.Warlock, Dwelling.Monster6) => MonsterType.GreenDragon,
        (MapRace.Wizard, Dwelling.Monster6) => MonsterType.Giant,
        (MapRace.Necromancer, Dwelling.Monster6) => MonsterType.BoneDragon,

        (MapRace.Knight, Dwelling.Upgrade6) => MonsterType.Crusader,
        (MapRace.Barbarian, Dwelling.Upgrade6) => MonsterType.Cyclops,
        (MapRace.Sorceress, Dwelling.Upgrade6) => MonsterType.Phoenix,
        (MapRace.Warlock, Dwelling.Upgrade6) => MonsterType.RedDragon,
        (MapRace.Wizard, Dwelling.Upgrade6) => MonsterType.Titan,
        (MapRace.Necromancer, Dwelling.Upgrade6) => MonsterType.BoneDragon,

        (MapRace.Knight, Dwelling.Upgrade7) => MonsterType.Crusader,
        (MapRace.Barbarian, Dwelling.Upgrade7) => MonsterType.Cyclops,
        (MapRace.Sorceress, Dwelling.Upgrade7) => MonsterType.Phoenix,
        (MapRace.Warlock, Dwelling.Upgrade7) => MonsterType.BlackDragon,
        (MapRace.Wizard, Dwelling.Upgrade7) => MonsterType.Titan,
        (MapRace.Necromancer, Dwelling.Upgrade7) => MonsterType.BoneDragon,

        _ => MonsterType.Unknown,
    };
}
