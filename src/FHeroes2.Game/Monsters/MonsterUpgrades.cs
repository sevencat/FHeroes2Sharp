using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The upgrade relation between monsters — <c>Monster::GetUpgrade()</c> (<c>monster.cpp:285-336</c>),
/// <c>Monster::GetDowngrade()</c> (<c>monster.cpp:232-283</c>) and <c>Monster::GetUpgradeCost()</c>
/// (<c>monster.cpp:830-838</c>).
/// </summary>
/// <remarks>
/// <para>
/// Twenty-one monsters upgrade, and most of them in a single step. The only two-step line is the warlock's
/// dragon — green to red, red to black — which is why <see cref="GetUpgrade"/> is not its own inverse at the
/// top of that chain and <see cref="GetDowngrade"/> exists as a second table rather than as a search.
/// </para>
/// <para>
/// Everything else — the peasants, the neutrals, the unknown monster and the five editor-only random markers —
/// maps to itself, exactly as the reference's <c>default: break;</c> does. A caller therefore never has to ask
/// "does this monster upgrade" before calling <see cref="GetUpgrade"/>; it asks
/// <see cref="IsAllowUpgrade"/> when the difference matters.
/// </para>
/// <para>
/// The reference returns a <c>Monster</c> from both tables; the port returns a <see cref="MonsterType"/>, which
/// is the only thing those two methods key on.
/// </para>
/// </remarks>
public static class MonsterUpgrades
{
    /// <summary>
    /// The upgraded monster, <c>Monster::GetUpgrade()</c> (<c>monster.cpp:285-336</c>).
    /// </summary>
    /// <param name="monster">The monster to upgrade.</param>
    /// <returns>The upgrade, or <paramref name="monster"/> itself when it has none.</returns>
    public static MonsterType GetUpgrade(MonsterType monster) => monster switch
    {
        MonsterType.Archer => MonsterType.Ranger,
        MonsterType.Pikeman => MonsterType.VeteranPikeman,
        MonsterType.Swordsman => MonsterType.MasterSwordsman,
        MonsterType.Cavalry => MonsterType.Champion,
        MonsterType.Paladin => MonsterType.Crusader,
        MonsterType.Orc => MonsterType.OrcChief,
        MonsterType.Ogre => MonsterType.OgreLord,
        MonsterType.Troll => MonsterType.WarTroll,
        MonsterType.Dwarf => MonsterType.BattleDwarf,
        MonsterType.Elf => MonsterType.GrandElf,
        MonsterType.Druid => MonsterType.GreaterDruid,
        MonsterType.Zombie => MonsterType.MutantZombie,
        MonsterType.Mummy => MonsterType.RoyalMummy,
        MonsterType.Vampire => MonsterType.VampireLord,
        MonsterType.Lich => MonsterType.PowerLich,
        MonsterType.Minotaur => MonsterType.MinotaurKing,
        MonsterType.GreenDragon => MonsterType.RedDragon,
        MonsterType.RedDragon => MonsterType.BlackDragon,
        MonsterType.IronGolem => MonsterType.SteelGolem,
        MonsterType.Mage => MonsterType.Archmage,
        MonsterType.Giant => MonsterType.Titan,
        _ => monster,
    };

    /// <summary>
    /// The monster this one is an upgrade of, <c>Monster::GetDowngrade()</c> (<c>monster.cpp:232-283</c>).
    /// </summary>
    /// <remarks>
    /// The reference's callers are the battle arrangement (<c>Army::ArrangeForBattle</c>) and the map's random
    /// monster picker, both of which want "the plain version of this creature". The top of a chain — the black
    /// dragon, the titan — answers itself, so <c>GetDowngrade</c> is safe to call in a loop.
    /// </remarks>
    /// <param name="monster">The monster to downgrade.</param>
    /// <returns>The monster it upgrades from, or <paramref name="monster"/> itself when it is not an upgrade.</returns>
    public static MonsterType GetDowngrade(MonsterType monster) => monster switch
    {
        MonsterType.Ranger => MonsterType.Archer,
        MonsterType.VeteranPikeman => MonsterType.Pikeman,
        MonsterType.MasterSwordsman => MonsterType.Swordsman,
        MonsterType.Champion => MonsterType.Cavalry,
        MonsterType.Crusader => MonsterType.Paladin,
        MonsterType.OrcChief => MonsterType.Orc,
        MonsterType.OgreLord => MonsterType.Ogre,
        MonsterType.WarTroll => MonsterType.Troll,
        MonsterType.BattleDwarf => MonsterType.Dwarf,
        MonsterType.GrandElf => MonsterType.Elf,
        MonsterType.GreaterDruid => MonsterType.Druid,
        MonsterType.MutantZombie => MonsterType.Zombie,
        MonsterType.RoyalMummy => MonsterType.Mummy,
        MonsterType.VampireLord => MonsterType.Vampire,
        MonsterType.PowerLich => MonsterType.Lich,
        MonsterType.MinotaurKing => MonsterType.Minotaur,
        MonsterType.RedDragon => MonsterType.GreenDragon,
        MonsterType.BlackDragon => MonsterType.RedDragon,
        MonsterType.SteelGolem => MonsterType.IronGolem,
        MonsterType.Archmage => MonsterType.Mage,
        MonsterType.Titan => MonsterType.Giant,
        _ => monster,
    };

    /// <summary>
    /// Whether the monster has an upgrade at all, <c>Monster::isAllowUpgrade()</c> (<c>monster.h:257</c>).
    /// </summary>
    /// <remarks>
    /// The reference spells this as <c>id != GetUpgrade().id</c>. The port keeps that spelling rather than a
    /// second table, because the two must never disagree: a monster that "has an upgrade" which is itself would
    /// make the upgrade button in the army dialog do nothing.
    /// </remarks>
    /// <param name="monster">The monster to test.</param>
    /// <returns><see langword="true"/> when <see cref="GetUpgrade"/> returns a different monster.</returns>
    public static bool IsAllowUpgrade(MonsterType monster) => GetUpgrade(monster) != monster;

    /// <summary>
    /// What upgrading one creature costs, <c>Monster::GetUpgradeCost()</c> (<c>monster.cpp:830-838</c>).
    /// </summary>
    /// <remarks>
    /// Twice the price difference: the dwelling already paid for the cheaper creature, and the game charges the
    /// difference again as a fee. A monster without an upgrade costs nothing — the reference returns an empty
    /// <c>Funds</c> there, and the port returns the same zeroed struct, so callers can add the value up without
    /// checking <see cref="IsAllowUpgrade"/> first.
    /// </remarks>
    /// <param name="monster">The monster to upgrade.</param>
    /// <returns>The difference in price times two, or an empty <see cref="Funds"/>.</returns>
    public static Funds GetUpgradeCost(MonsterType monster)
    {
        MonsterType upgrade = GetUpgrade(monster);

        if (upgrade == monster)
        {
            return new Funds();
        }

        return (GetCost(upgrade) - GetCost(monster)) * 2u;
    }

    /// <summary>The price of one creature, <c>Monster::GetCost()</c> (<c>monster.h:306</c>).</summary>
    /// <param name="monster">The monster.</param>
    /// <returns>The cost.</returns>
    private static Funds GetCost(MonsterType monster) => MonsterStatistics.GetGeneralStats(monster).GetCost();
}
