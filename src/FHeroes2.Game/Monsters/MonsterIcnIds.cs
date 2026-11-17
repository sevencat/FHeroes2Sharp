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
}
