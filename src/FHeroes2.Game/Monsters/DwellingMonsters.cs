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

    /// <summary>
    /// Which dwelling holds a creature, <c>Monster::GetDwelling()</c> (<c>monster.cpp:691-780</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reverse of <see cref="FromDwelling"/>, and deliberately a second table rather than a search through the
    /// first one: <see cref="FromDwelling"/> answers by race, and a creature's dwelling is a property of the
    /// creature alone (an Archer is always in a second dwelling, whichever town it is in). The reference keeps the
    /// two tables separate for the same reason.
    /// </para>
    /// <para>
    /// Only an upgraded dwelling is returned for an upgraded creature, so this is also what decides whether a town
    /// can upgrade a stack: <see cref="FHeroes2.Game.Castle.Castle.IsBuild"/> of the answer.
    /// </para>
    /// <para>
    /// The last three groups are what makes the sixth dwelling's three names work: the base creature answers
    /// <c>DwellingMonster6</c>, its first upgrade <c>DwellingUpgrade6</c>, its second <c>DwellingUpgrade7</c> —
    /// and a town that has no upgrade for a creature answers its base dwelling.
    /// </para>
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <returns>The dwelling bit, or <see cref="BuildingType.Nothing"/> for a neutral, invalid or random monster.</returns>
    public static BuildingType GetDwelling(MonsterType monster) => monster switch
    {
        MonsterType.Peasant or MonsterType.Goblin or MonsterType.Sprite or MonsterType.Centaur
            or MonsterType.Halfling or MonsterType.Skeleton => BuildingType.DwellingMonster1,

        MonsterType.Archer or MonsterType.Orc or MonsterType.Zombie or MonsterType.Dwarf
            or MonsterType.Gargoyle or MonsterType.Boar => BuildingType.DwellingMonster2,

        MonsterType.Ranger or MonsterType.OrcChief or MonsterType.BattleDwarf
            or MonsterType.MutantZombie => BuildingType.DwellingUpgrade2,

        MonsterType.Pikeman or MonsterType.Wolf or MonsterType.Elf or MonsterType.IronGolem
            or MonsterType.Mummy or MonsterType.Griffin => BuildingType.DwellingMonster3,

        MonsterType.VeteranPikeman or MonsterType.GrandElf or MonsterType.SteelGolem
            or MonsterType.RoyalMummy => BuildingType.DwellingUpgrade3,

        MonsterType.Swordsman or MonsterType.Ogre or MonsterType.Druid or MonsterType.Minotaur
            or MonsterType.Roc or MonsterType.Vampire => BuildingType.DwellingMonster4,

        MonsterType.MasterSwordsman or MonsterType.OgreLord or MonsterType.GreaterDruid
            or MonsterType.MinotaurKing or MonsterType.VampireLord => BuildingType.DwellingUpgrade4,

        MonsterType.Cavalry or MonsterType.Troll or MonsterType.Mage or MonsterType.Lich
            or MonsterType.Unicorn or MonsterType.Hydra => BuildingType.DwellingMonster5,

        MonsterType.Champion or MonsterType.WarTroll or MonsterType.Archmage
            or MonsterType.PowerLich => BuildingType.DwellingUpgrade5,

        MonsterType.Paladin or MonsterType.Cyclops or MonsterType.Phoenix or MonsterType.GreenDragon
            or MonsterType.Giant or MonsterType.BoneDragon => BuildingType.DwellingMonster6,

        MonsterType.Crusader or MonsterType.RedDragon or MonsterType.Titan => BuildingType.DwellingUpgrade6,

        MonsterType.BlackDragon => BuildingType.DwellingUpgrade7,

        _ => BuildingType.Nothing,
    };
}
