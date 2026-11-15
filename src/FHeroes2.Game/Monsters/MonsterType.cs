namespace FHeroes2.Game.Monsters;

/// <summary>
/// <c>Monster::MonsterType</c> of <c>src/fheroes2/monster/monster.h</c>, in the same order and with
/// the same values: the id is what MP2 metadata and stacks store, so it is part of the map format.
/// </summary>
/// <remarks>
/// The reference nests the enumeration in <c>class Monster</c>; the port has no class of that name
/// yet and <c>Monster</c> is going to be the class with the behavior, so the enumeration keeps the
/// name the reference gave it — <c>MonsterType</c>.
/// </remarks>
public enum MonsterType : int
{
    Unknown = 0,

    Peasant,
    Archer,
    Ranger,
    Pikeman,
    VeteranPikeman,
    Swordsman,
    MasterSwordsman,
    Cavalry,
    Champion,
    Paladin,
    Crusader,
    Goblin,
    Orc,
    OrcChief,
    Wolf,
    Ogre,
    OgreLord,
    Troll,
    WarTroll,
    Cyclops,
    Sprite,
    Dwarf,
    BattleDwarf,
    Elf,
    GrandElf,
    Druid,
    GreaterDruid,
    Unicorn,
    Phoenix,
    Centaur,
    Gargoyle,
    Griffin,
    Minotaur,
    MinotaurKing,
    Hydra,
    GreenDragon,
    RedDragon,
    BlackDragon,
    Halfling,
    Boar,
    IronGolem,
    SteelGolem,
    Roc,
    Mage,
    Archmage,
    Giant,
    Titan,
    Skeleton,
    Zombie,
    MutantZombie,
    Mummy,
    RoyalMummy,
    Vampire,
    VampireLord,
    Lich,
    PowerLich,
    BoneDragon,

    Rogue,
    Nomad,
    Ghost,
    Genie,
    Medusa,
    EarthElement,
    AirElement,
    FireElement,
    WaterElement,

// Editor-related monsters.
    RandomMonster,
    RandomMonsterLevel1,
    RandomMonsterLevel2,
    RandomMonsterLevel3,
    RandomMonsterLevel4,

// IMPORTANT! Put all new monsters just above this line.
    MonsterCount,
}
