using FHeroes2.Assets.Icn;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The ICN lookups of <c>src/fheroes2/monster/monster.cpp:40-75</c> and <c>:825-828</c>: the projectile a
/// shooter throws and the sprite that shows a creature inside its dwelling.
/// </summary>
public static class MonsterIcnIds
{
    /// <summary>
    /// The projectile of a shooter, <c>Monster::GetMissileICN()</c> (<c>monster.cpp:40-75</c>).
    /// </summary>
    /// <remarks>
    /// Only the shooters answer, and the centaur is the odd one out: it has no projectile file of its own and
    /// the game draws the elf's, which the reference documents in a comment rather than by a fallback.
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <returns>The projectile ICN, <see cref="IcnType.Unknown"/> for a creature that does not shoot.</returns>
    public static IcnType GetMissileIcn(MonsterType monster) => monster switch
    {
        MonsterType.Archer or MonsterType.Ranger => IcnType.ArchMsl,
        MonsterType.Orc or MonsterType.OrcChief => IcnType.OrcMsl,
        MonsterType.Troll => IcnType.Trollmsl,
        MonsterType.WarTroll => IcnType.Troll2msl,
        MonsterType.Elf or MonsterType.GrandElf => IcnType.ElfMsl,
        MonsterType.Druid or MonsterType.GreaterDruid => IcnType.Druidmsl,

        // Doesn't have own missile file, game falls back to ELF__MSL.
        MonsterType.Centaur => IcnType.ElfMsl,

        MonsterType.Halfling => IcnType.Halflmsl,
        MonsterType.Titan => IcnType.Titanmsl,
        MonsterType.Lich or MonsterType.PowerLich => IcnType.LichMsl,
        _ => IcnType.Unknown,
    };

    /// <summary>
    /// The sprite of a creature in its dwelling, <c>Monster::ICNMonh()</c> (<c>monster.cpp:825-828</c>):
    /// <c>ICN::MONH0000 + id - PEASANT</c>.
    /// </summary>
    /// <remarks>
    /// <c>MONH0000</c> onwards holds one standing sprite per creature, in id order, and the range stops at the
    /// water elemental — the rogue, the nomad and the rest have no dwelling of their own. The port's
    /// <see cref="IcnType"/> lists the frames consecutively (<c>Monh0000</c> … <c>Monh0064</c>), so the offset
    /// is an addition, exactly like the reference's.
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <returns>The ICN, <see cref="IcnType.Unknown"/> outside the dwelling range.</returns>
    public static IcnType GetDwellingIcn(MonsterType monster) =>
        monster >= MonsterType.Peasant && monster <= MonsterType.WaterElement
            ? IcnType.Monh0000 + ((int)monster - (int)MonsterType.Peasant)
            : IcnType.Unknown;

    /// <summary>
    /// The sprite file of a creature, the <c>icnId</c> column of the monster table
    /// (<c>monster_info.cpp:136-144</c>), reached in the reference as <c>getMonsterData( id ).icnId</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first eleven creatures do not map one-to-one onto their upgrade: the archer's upgrade draws from
    /// <c>ARCHER2</c> while the ranger's own file is <c>ARCHRFRM.BIN</c>, and the two swordsmen share
    /// <c>SWRDSFRM.BIN</c>. The table is therefore a switch and not arithmetic — the names in it are the
    /// archive's, not the port's, which is why <c>Crusader</c> reads <c>Paladin2</c>.
    /// </para>
    /// <para>
    /// The five random placeholders have no sprite: the reference gives them <c>ICN::UNKNOWN</c>, and so does this
    /// method. <see cref="GetDwellingIcn"/> stops at the water elemental too, for the same reason.
    /// </para>
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <returns>The ICN the sprite and the animation frames are read from.</returns>
    public static IcnType GetSpriteIcn(MonsterType monster) => monster switch
    {
        MonsterType.Peasant => IcnType.Peasant,
        MonsterType.Archer => IcnType.Archer,
        MonsterType.Ranger => IcnType.Archer2,
        MonsterType.Pikeman => IcnType.Pikeman,
        MonsterType.VeteranPikeman => IcnType.Pikeman2,
        MonsterType.Swordsman => IcnType.Swordsmn,
        MonsterType.MasterSwordsman => IcnType.Swordsm2,
        MonsterType.Cavalry => IcnType.Cavalryr,
        MonsterType.Champion => IcnType.Cavalryb,
        MonsterType.Paladin => IcnType.Paladin,
        MonsterType.Crusader => IcnType.Paladin2,
        MonsterType.Goblin => IcnType.Goblin,
        MonsterType.Orc => IcnType.Orc,
        MonsterType.OrcChief => IcnType.Orc2,
        MonsterType.Wolf => IcnType.Wolf,
        MonsterType.Ogre => IcnType.Ogre,
        MonsterType.OgreLord => IcnType.Ogre2,
        MonsterType.Troll => IcnType.Troll,
        MonsterType.WarTroll => IcnType.Troll2,
        MonsterType.Cyclops => IcnType.Cyclops,
        MonsterType.Sprite => IcnType.Sprite,
        MonsterType.Dwarf => IcnType.Dwarf,
        MonsterType.BattleDwarf => IcnType.Dwarf2,
        MonsterType.Elf => IcnType.Elf,
        MonsterType.GrandElf => IcnType.Elf2,
        MonsterType.Druid => IcnType.Druid,
        MonsterType.GreaterDruid => IcnType.Druid2,
        MonsterType.Unicorn => IcnType.Unicorn,
        MonsterType.Phoenix => IcnType.Phoenix,
        MonsterType.Centaur => IcnType.Centaur,
        MonsterType.Gargoyle => IcnType.Gargoyle,
        MonsterType.Griffin => IcnType.Griffin,
        MonsterType.Minotaur => IcnType.Minotaur,
        MonsterType.MinotaurKing => IcnType.Minotau2,
        MonsterType.Hydra => IcnType.Hydra,
        MonsterType.GreenDragon => IcnType.Draggree,
        MonsterType.RedDragon => IcnType.Dragred,
        MonsterType.BlackDragon => IcnType.Dragblak,
        MonsterType.Halfling => IcnType.Halfling,
        MonsterType.Boar => IcnType.Boar,
        MonsterType.IronGolem => IcnType.Golem,
        MonsterType.SteelGolem => IcnType.Golem2,
        MonsterType.Roc => IcnType.Roc,
        MonsterType.Mage => IcnType.Mage1,
        MonsterType.Archmage => IcnType.Mage2,
        MonsterType.Giant => IcnType.Titanblu,
        MonsterType.Titan => IcnType.Titanbla,
        MonsterType.Skeleton => IcnType.Skeleton,
        MonsterType.Zombie => IcnType.Zombie,
        MonsterType.MutantZombie => IcnType.Zombie2,
        MonsterType.Mummy => IcnType.Mummyw,
        MonsterType.RoyalMummy => IcnType.Mummy2,
        MonsterType.Vampire => IcnType.Vampire,
        MonsterType.VampireLord => IcnType.Vampire2,
        MonsterType.Lich => IcnType.Lich,
        MonsterType.PowerLich => IcnType.Lich2,
        MonsterType.BoneDragon => IcnType.Dragbone,
        MonsterType.Rogue => IcnType.Rogue,
        MonsterType.Nomad => IcnType.Nomad,
        MonsterType.Ghost => IcnType.Ghost,
        MonsterType.Genie => IcnType.Genie,
        MonsterType.Medusa => IcnType.Medusa,
        MonsterType.EarthElement => IcnType.Eelem,
        MonsterType.AirElement => IcnType.Aelem,
        MonsterType.FireElement => IcnType.Felem,
        MonsterType.WaterElement => IcnType.Welem,
        _ => IcnType.Unknown,
    };
}
