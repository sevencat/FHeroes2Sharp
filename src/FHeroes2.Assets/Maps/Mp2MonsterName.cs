namespace FHeroes2.Assets.Maps;

/// <summary>
/// The names of the creatures, in the order of <c>Monster::monster_t</c>: the strings
/// <c>Monster::GetName</c> and <c>Monster::GetPluralName</c> return in the reference.
/// </summary>
/// <remarks>
/// A map does not store the name of the creatures of a stack, only their kind as the frame of
/// <c>ICN::MONS32</c> (<c>MP2::OBJ_ICN_TYPE_MONS32</c> starts from the peasant), so this table turns
/// that frame back into a name. Ids 67 to 71 are the four random monsters of the editor, which have
/// names but no graphics of their own.
/// </remarks>
public static class Mp2MonsterName
{
    /// <summary>Id of "no creature", <c>Monster::UNKNOWN</c>.</summary>
    public const int Unknown = 0;

    /// <summary>The singular name of a kind of creature, <c>Monster::GetName</c>.</summary>
    /// <param name="monsterId">Creature id, 1 is the peasant.</param>
    /// <returns>Its English name, "Unknown Monster" for an id this table does not know.</returns>
    public static string Of(int monsterId) => Entry(monsterId).Singular;

    /// <summary>
    /// The name to use for a stack of <paramref name="count"/> creatures,
    /// <c>Monster::GetPluralName</c>: English pluralises on the count, one peasant but two peasants.
    /// </summary>
    /// <param name="monsterId">Creature id of the stack.</param>
    /// <param name="count">Size of the stack.</param>
    /// <returns>Its English name in the right number.</returns>
    public static string Plural(int monsterId, uint count)
    {
        (string singular, string plural) = Entry(monsterId);

        return count == 1 ? singular : plural;
    }

    /// <summary>
    /// The entry of the table for <paramref name="monsterId"/>, the unknown creature when the id is
    /// outside it.
    /// </summary>
    private static (string Singular, string Plural) Entry(int monsterId)
    {
        return monsterId > 0 && monsterId < Names.Length ? Names[monsterId] : Names[Unknown];
    }

    /// <summary>Every creature of the game, <c>Monster::UNKNOWN</c> first.</summary>
    private static readonly (string Singular, string Plural)[] Names =
    [
        new("Unknown Monster", "Unknown Monsters"),
        new("Peasant", "Peasants"),
        new("Archer", "Archers"),
        new("Ranger", "Rangers"),
        new("Pikeman", "Pikemen"),
        new("Veteran Pikeman", "Veteran Pikemen"),
        new("Swordsman", "Swordsmen"),
        new("Master Swordsman", "Master Swordsmen"),
        new("Cavalry", "Cavalries"),
        new("Champion", "Champions"),
        new("Paladin", "Paladins"),
        new("Crusader", "Crusaders"),
        new("Goblin", "Goblins"),
        new("Orc", "Orcs"),
        new("Orc Chief", "Orc Chiefs"),
        new("Wolf", "Wolves"),
        new("Ogre", "Ogres"),
        new("Ogre Lord", "Ogre Lords"),
        new("Troll", "Trolls"),
        new("War Troll", "War Trolls"),
        new("Cyclops", "Cyclopes"),
        new("Sprite", "Sprites"),
        new("Dwarf", "Dwarves"),
        new("Battle Dwarf", "Battle Dwarves"),
        new("Elf", "Elves"),
        new("Grand Elf", "Grand Elves"),
        new("Druid", "Druids"),
        new("Greater Druid", "Greater Druids"),
        new("Unicorn", "Unicorns"),
        new("Phoenix", "Phoenixes"),
        new("Centaur", "Centaurs"),
        new("Gargoyle", "Gargoyles"),
        new("Griffin", "Griffins"),
        new("Minotaur", "Minotaurs"),
        new("Minotaur King", "Minotaur Kings"),
        new("Hydra", "Hydras"),
        new("Green Dragon", "Green Dragons"),
        new("Red Dragon", "Red Dragons"),
        new("Black Dragon", "Black Dragons"),
        new("Halfling", "Halflings"),
        new("Boar", "Boars"),
        new("Iron Golem", "Iron Golems"),
        new("Steel Golem", "Steel Golems"),
        new("Roc", "Rocs"),
        new("Mage", "Magi"),
        new("Archmage", "Archmagi"),
        new("Giant", "Giants"),
        new("Titan", "Titans"),
        new("Skeleton", "Skeletons"),
        new("Zombie", "Zombies"),
        new("Mutant Zombie", "Mutant Zombies"),
        new("Mummy", "Mummies"),
        new("Royal Mummy", "Royal Mummies"),
        new("Vampire", "Vampires"),
        new("Vampire Lord", "Vampire Lords"),
        new("Lich", "Liches"),
        new("Power Lich", "Power Liches"),
        new("Bone Dragon", "Bone Dragons"),
        new("Rogue", "Rogues"),
        new("Nomad", "Nomads"),
        new("Ghost", "Ghosts"),
        new("Genie", "Genies"),
        new("Medusa", "Medusas"),
        new("Earth Elemental", "Earth Elementals"),
        new("Air Elemental", "Air Elementals"),
        new("Fire Elemental", "Fire Elementals"),
        new("Water Elemental", "Water Elementals"),
        new("Random Monster", "Random Monsters"),
        new("Random Monster 1", "Random Monsters 1"),
        new("Random Monster 2", "Random Monsters 2"),
        new("Random Monster 3", "Random Monsters 3"),
        new("Random Monster 4", "Random Monsters 4"),
    ];
}
