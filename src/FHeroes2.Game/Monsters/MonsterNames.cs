using System.Diagnostics;
using FHeroes2.Game.Castle;

namespace FHeroes2.Game.Monsters;

/// <summary>
/// The names of the creatures, the two text columns of <c>monsterGeneralStats</c> in
/// <c>monster/monster_info.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MonsterStatistics"/> keeps the numbers of the same table and drops these two columns on purpose, so
/// the numbers stay comparable in tests that do not care about text. The names live here, in the order of
/// <see cref="MonsterType"/>, and they are the reference's own English strings — the port has no translation layer
/// yet, and inventing one here would make every other module wait for it.
/// </para>
/// <para>
/// The plural is the reference's, not a mechanical "add an s": a mage answers <c>Magi</c>, an archmage
/// <c>Archmagi</c>, a cavalryman <c>Cavalries</c>. Messages about counts use it, which is why
/// <see cref="GetMulti"/> exists next to <see cref="GetSingular"/>.
/// </para>
/// </remarks>
public static class MonsterNames
{
    /// <summary>
    /// The singular name of a creature, <c>Monster::GetName()</c> (<c>monster.cpp</c>).
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>Its name; <c>"Unknown Monster"</c> for <see cref="MonsterType.Unknown"/>.</returns>
    public static string GetSingular(MonsterType monster) => Names[Index(monster)].Singular;

    /// <summary>
    /// The plural name of a creature, <c>Monster::GetMultiName()</c> (<c>monster.cpp</c>).
    /// </summary>
    /// <param name="monster">The creature.</param>
    /// <returns>Its plural name; <c>"Unknown Monsters"</c> for <see cref="MonsterType.Unknown"/>.</returns>
    public static string GetMulti(MonsterType monster) => Names[Index(monster)].Multi;

    /// <summary>
    /// The name a count asks for, <c>Monster::GetPluralName( count )</c> (<c>monster.cpp</c>).
    /// </summary>
    /// <remarks>
    /// One creature is named in the singular, everything else — zero included — in the plural, because the reference
    /// is literally <c>count == 1 ? GetName() : GetMultiName()</c>. Dialogs that say "3 Peasants" go through here.
    /// </remarks>
    /// <param name="monster">The creature.</param>
    /// <param name="count">How many of it a sentence is about.</param>
    /// <returns><see cref="GetSingular"/> for one, <see cref="GetMulti"/> otherwise.</returns>
    public static string GetPluralName(MonsterType monster, uint count) =>
        count == 1 ? GetSingular(monster) : GetMulti(monster);

    /// <summary>
    /// What a dwelling of a random town produces, <c>Monster::getRandomRaceMonstersName()</c>
    /// (<c>monster.cpp</c>).
    /// </summary>
    /// <remarks>
    /// The name of the level, not of a creature, because a random town's dwelling is not tied to a race yet. The
    /// reference asserts on anything that is not a dwelling and answers <c>"Unknown Monsters"</c>; the port does
    /// the same instead of throwing, because the answer is only ever printed.
    /// </remarks>
    /// <param name="dwelling">A dwelling or a dwelling upgrade.</param>
    /// <returns>The level name, for example <c>"level 1 creatures"</c>.</returns>
    public static string GetRandomRaceMonstersName(BuildingType dwelling) => dwelling switch
    {
        BuildingType.DwellingMonster1 => "level 1 creatures",
        BuildingType.DwellingMonster2 or BuildingType.DwellingUpgrade2 => "level 2 creatures",
        BuildingType.DwellingMonster3 or BuildingType.DwellingUpgrade3 => "level 3 creatures",
        BuildingType.DwellingMonster4 or BuildingType.DwellingUpgrade4 => "level 4 creatures",
        BuildingType.DwellingMonster5 or BuildingType.DwellingUpgrade5 => "level 5 creatures",
        BuildingType.DwellingMonster6 or BuildingType.DwellingUpgrade6 or BuildingType.DwellingUpgrade7 =>
            "level 6 creatures",
        _ => UnknownMultiName(),
    };

    private static string UnknownMultiName()
    {
        Debug.Assert(false, "only a dwelling has a random-race creature name");
        return "Unknown Monsters";
    }

    private static int Index(MonsterType monster)
    {
        int index = (int)monster;

        Debug.Assert(index >= 0 && index < Names.Length, "monster id outside the name table");

        return index;
    }

    /// <summary>
    /// The reference's table, row for row: index is <c>MonsterType</c>, and <see cref="MonsterType.MonsterCount"/>
    /// is the length.
    /// </summary>
    private static readonly (string Singular, string Multi)[] Names =
    [
        ("Unknown Monster", "Unknown Monsters"), // Unknown
        ("Peasant", "Peasants"),
        ("Archer", "Archers"),
        ("Ranger", "Rangers"),
        ("Pikeman", "Pikemen"),
        ("Veteran Pikeman", "Veteran Pikemen"),
        ("Swordsman", "Swordsmen"),
        ("Master Swordsman", "Master Swordsmen"),
        ("Cavalry", "Cavalries"),
        ("Champion", "Champions"),
        ("Paladin", "Paladins"),
        ("Crusader", "Crusaders"),
        ("Goblin", "Goblins"),
        ("Orc", "Orcs"),
        ("Orc Chief", "Orc Chiefs"),
        ("Wolf", "Wolves"),
        ("Ogre", "Ogres"),
        ("Ogre Lord", "Ogre Lords"),
        ("Troll", "Trolls"),
        ("War Troll", "War Trolls"),
        ("Cyclops", "Cyclopes"),
        ("Sprite", "Sprites"),
        ("Dwarf", "Dwarves"),
        ("Battle Dwarf", "Battle Dwarves"),
        ("Elf", "Elves"),
        ("Grand Elf", "Grand Elves"),
        ("Druid", "Druids"),
        ("Greater Druid", "Greater Druids"),
        ("Unicorn", "Unicorns"),
        ("Phoenix", "Phoenixes"),
        ("Centaur", "Centaurs"),
        ("Gargoyle", "Gargoyles"),
        ("Griffin", "Griffins"),
        ("Minotaur", "Minotaurs"),
        ("Minotaur King", "Minotaur Kings"),
        ("Hydra", "Hydras"),
        ("Green Dragon", "Green Dragons"),
        ("Red Dragon", "Red Dragons"),
        ("Black Dragon", "Black Dragons"),
        ("Halfling", "Halflings"),
        ("Boar", "Boars"),
        ("Iron Golem", "Iron Golems"),
        ("Steel Golem", "Steel Golems"),
        ("Roc", "Rocs"),
        ("Mage", "Magi"),
        ("Archmage", "Archmagi"),
        ("Giant", "Giants"),
        ("Titan", "Titans"),
        ("Skeleton", "Skeletons"),
        ("Zombie", "Zombies"),
        ("Mutant Zombie", "Mutant Zombies"),
        ("Mummy", "Mummies"),
        ("Royal Mummy", "Royal Mummies"),
        ("Vampire", "Vampires"),
        ("Vampire Lord", "Vampire Lords"),
        ("Lich", "Liches"),
        ("Power Lich", "Power Liches"),
        ("Bone Dragon", "Bone Dragons"),
        ("Rogue", "Rogues"),
        ("Nomad", "Nomads"),
        ("Ghost", "Ghosts"),
        ("Genie", "Genies"),
        ("Medusa", "Medusas"),
        ("Earth Elemental", "Earth Elementals"),
        ("Air Elemental", "Air Elementals"),
        ("Fire Elemental", "Fire Elementals"),
        ("Water Elemental", "Water Elementals"),
        ("Random Monster", "Random Monsters"),
        ("Random Monster 1", "Random Monsters 1"),
        ("Random Monster 2", "Random Monsters 2"),
        ("Random Monster 3", "Random Monsters 3"),
        ("Random Monster 4", "Random Monsters 4"),
    ];

    static MonsterNames()
    {
        Debug.Assert(Names.Length == (int)MonsterType.MonsterCount, "the name table must cover every creature");
    }
}
