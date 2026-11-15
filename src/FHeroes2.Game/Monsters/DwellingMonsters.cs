using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;

namespace FHeroes2.Game.Monsters;

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
/// <para>
/// The dwelling bits are <see cref="BuildingType"/> members now that the castle module exists, and this table is
/// the bridge between them and <see cref="MonsterType"/>: a castle's dwelling upgrade and a monster's upgrade are
/// the same fact seen from two sides, which is what the castle's upgrade tests check.
/// </para>
/// </remarks>
public static class DwellingMonsters
{
    /// <summary>The monster of a dwelling, <c>Monster::FromDwelling()</c>.</summary>
    /// <param name="race">Race of the town.</param>
    /// <param name="dwelling">The dwelling building, one of the <c>Dwelling*</c> members of <see cref="BuildingType"/>.</param>
    /// <returns>The monster, or <see cref="MonsterType.Unknown"/> when the pair is not in the table.</returns>
    public static MonsterType FromDwelling(MapRace race, BuildingType dwelling) => (race, dwelling) switch
    {
        (MapRace.Knight, BuildingType.DwellingMonster1) => MonsterType.Peasant,
        (MapRace.Barbarian, BuildingType.DwellingMonster1) => MonsterType.Goblin,
        (MapRace.Sorceress, BuildingType.DwellingMonster1) => MonsterType.Sprite,
        (MapRace.Warlock, BuildingType.DwellingMonster1) => MonsterType.Centaur,
        (MapRace.Wizard, BuildingType.DwellingMonster1) => MonsterType.Halfling,
        (MapRace.Necromancer, BuildingType.DwellingMonster1) => MonsterType.Skeleton,

        (MapRace.Knight, BuildingType.DwellingMonster2) => MonsterType.Archer,
        (MapRace.Barbarian, BuildingType.DwellingMonster2) => MonsterType.Orc,
        (MapRace.Sorceress, BuildingType.DwellingMonster2) => MonsterType.Dwarf,
        (MapRace.Warlock, BuildingType.DwellingMonster2) => MonsterType.Gargoyle,
        (MapRace.Wizard, BuildingType.DwellingMonster2) => MonsterType.Boar,
        (MapRace.Necromancer, BuildingType.DwellingMonster2) => MonsterType.Zombie,

        (MapRace.Knight, BuildingType.DwellingUpgrade2) => MonsterType.Ranger,
        (MapRace.Barbarian, BuildingType.DwellingUpgrade2) => MonsterType.OrcChief,
        (MapRace.Sorceress, BuildingType.DwellingUpgrade2) => MonsterType.BattleDwarf,
        (MapRace.Warlock, BuildingType.DwellingUpgrade2) => MonsterType.Gargoyle,
        (MapRace.Wizard, BuildingType.DwellingUpgrade2) => MonsterType.Boar,
        (MapRace.Necromancer, BuildingType.DwellingUpgrade2) => MonsterType.MutantZombie,

        (MapRace.Knight, BuildingType.DwellingMonster3) => MonsterType.Pikeman,
        (MapRace.Barbarian, BuildingType.DwellingMonster3) => MonsterType.Wolf,
        (MapRace.Sorceress, BuildingType.DwellingMonster3) => MonsterType.Elf,
        (MapRace.Warlock, BuildingType.DwellingMonster3) => MonsterType.Griffin,
        (MapRace.Wizard, BuildingType.DwellingMonster3) => MonsterType.IronGolem,
        (MapRace.Necromancer, BuildingType.DwellingMonster3) => MonsterType.Mummy,

        (MapRace.Knight, BuildingType.DwellingUpgrade3) => MonsterType.VeteranPikeman,
        (MapRace.Barbarian, BuildingType.DwellingUpgrade3) => MonsterType.Wolf,
        (MapRace.Sorceress, BuildingType.DwellingUpgrade3) => MonsterType.GrandElf,
        (MapRace.Warlock, BuildingType.DwellingUpgrade3) => MonsterType.Griffin,
        (MapRace.Wizard, BuildingType.DwellingUpgrade3) => MonsterType.SteelGolem,
        (MapRace.Necromancer, BuildingType.DwellingUpgrade3) => MonsterType.RoyalMummy,

        (MapRace.Knight, BuildingType.DwellingMonster4) => MonsterType.Swordsman,
        (MapRace.Barbarian, BuildingType.DwellingMonster4) => MonsterType.Ogre,
        (MapRace.Sorceress, BuildingType.DwellingMonster4) => MonsterType.Druid,
        (MapRace.Warlock, BuildingType.DwellingMonster4) => MonsterType.Minotaur,
        (MapRace.Wizard, BuildingType.DwellingMonster4) => MonsterType.Roc,
        (MapRace.Necromancer, BuildingType.DwellingMonster4) => MonsterType.Vampire,

        (MapRace.Knight, BuildingType.DwellingUpgrade4) => MonsterType.MasterSwordsman,
        (MapRace.Barbarian, BuildingType.DwellingUpgrade4) => MonsterType.OgreLord,
        (MapRace.Sorceress, BuildingType.DwellingUpgrade4) => MonsterType.GreaterDruid,
        (MapRace.Warlock, BuildingType.DwellingUpgrade4) => MonsterType.MinotaurKing,
        (MapRace.Wizard, BuildingType.DwellingUpgrade4) => MonsterType.Roc,
        (MapRace.Necromancer, BuildingType.DwellingUpgrade4) => MonsterType.VampireLord,

        (MapRace.Knight, BuildingType.DwellingMonster5) => MonsterType.Cavalry,
        (MapRace.Barbarian, BuildingType.DwellingMonster5) => MonsterType.Troll,
        (MapRace.Sorceress, BuildingType.DwellingMonster5) => MonsterType.Unicorn,
        (MapRace.Warlock, BuildingType.DwellingMonster5) => MonsterType.Hydra,
        (MapRace.Wizard, BuildingType.DwellingMonster5) => MonsterType.Mage,
        (MapRace.Necromancer, BuildingType.DwellingMonster5) => MonsterType.Lich,

        (MapRace.Knight, BuildingType.DwellingUpgrade5) => MonsterType.Champion,
        (MapRace.Barbarian, BuildingType.DwellingUpgrade5) => MonsterType.WarTroll,
        (MapRace.Sorceress, BuildingType.DwellingUpgrade5) => MonsterType.Unicorn,
        (MapRace.Warlock, BuildingType.DwellingUpgrade5) => MonsterType.Hydra,
        (MapRace.Wizard, BuildingType.DwellingUpgrade5) => MonsterType.Archmage,
        (MapRace.Necromancer, BuildingType.DwellingUpgrade5) => MonsterType.PowerLich,

        (MapRace.Knight, BuildingType.DwellingMonster6) => MonsterType.Paladin,
        (MapRace.Barbarian, BuildingType.DwellingMonster6) => MonsterType.Cyclops,
        (MapRace.Sorceress, BuildingType.DwellingMonster6) => MonsterType.Phoenix,
        (MapRace.Warlock, BuildingType.DwellingMonster6) => MonsterType.GreenDragon,
        (MapRace.Wizard, BuildingType.DwellingMonster6) => MonsterType.Giant,
        (MapRace.Necromancer, BuildingType.DwellingMonster6) => MonsterType.BoneDragon,

        (MapRace.Knight, BuildingType.DwellingUpgrade6) => MonsterType.Crusader,
        (MapRace.Barbarian, BuildingType.DwellingUpgrade6) => MonsterType.Cyclops,
        (MapRace.Sorceress, BuildingType.DwellingUpgrade6) => MonsterType.Phoenix,
        (MapRace.Warlock, BuildingType.DwellingUpgrade6) => MonsterType.RedDragon,
        (MapRace.Wizard, BuildingType.DwellingUpgrade6) => MonsterType.Titan,
        (MapRace.Necromancer, BuildingType.DwellingUpgrade6) => MonsterType.BoneDragon,

        (MapRace.Knight, BuildingType.DwellingUpgrade7) => MonsterType.Crusader,
        (MapRace.Barbarian, BuildingType.DwellingUpgrade7) => MonsterType.Cyclops,
        (MapRace.Sorceress, BuildingType.DwellingUpgrade7) => MonsterType.Phoenix,
        (MapRace.Warlock, BuildingType.DwellingUpgrade7) => MonsterType.BlackDragon,
        (MapRace.Wizard, BuildingType.DwellingUpgrade7) => MonsterType.Titan,
        (MapRace.Necromancer, BuildingType.DwellingUpgrade7) => MonsterType.BoneDragon,

        _ => MonsterType.Unknown,
    };
}
