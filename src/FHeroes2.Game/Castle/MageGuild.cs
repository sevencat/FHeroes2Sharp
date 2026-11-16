using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Castle;

/// <summary>
/// The spell library of a town, <c>MageGuild</c> (<c>castle/mageguild.h</c>, <c>castle/mageguild.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// A mage guild is not a set of spells but a <b>layout</b>: three slots for the first two levels and two for the
/// next, plus one slot per level in the library when the town has one, and the spells are placed into those slots
/// when the town is created. Which slot a spell sits in matters only twice — the library's slot per level is the
/// extra spell the upgrade buys, and a map can force a spell into a particular slot through the
/// <c>mustHaveSpells</c> of its castle block.
/// </para>
/// <para>
/// Two things are guaranteed by the reference and therefore by the port: a damage spell and a spell that cancels
/// one (Dispel, Mass Dispel, Anti Magic, Cure), drawn before the level loops. At most one <b>adventure</b> spell per
/// level is allowed, which is why the second initialization overload checks
/// <c>moreSpellsThanSlots</c> before honouring the race weights — a map that forces three adventure spells of one
/// level gets them all.
/// </para>
/// <para>
/// <b>The random generator is an argument</b> (<c>Rand::Get</c> in the reference): the guild is laid out once per
/// town, and tests need to lay it out the same way twice.
/// </para>
/// </remarks>
public sealed class MageGuild
{
    /// <summary>
    /// The spells of each level a guild holds without a library, <c>guildMaxSpellsCount</c>
    /// (<c>mageguild.cpp:38</c>).
    /// </summary>
    private static readonly int[] GuildMaxSpellsCount = [3, 3, 2, 2, 1];

    /// <summary>The first slot of each level in <see cref="_general"/>, from the table above.</summary>
    private static readonly int[] LevelOffsets = [0, 3, 6, 8, 10];

    private readonly SpellType[] _general = new SpellType[11];

    private readonly SpellType[] _library = new SpellType[5];

    /// <summary>
    /// How many spells the guild of a level holds, <c>MageGuild::getMaxSpellsCount()</c>
    /// (<c>mageguild.cpp:391-410</c>).
    /// </summary>
    /// <param name="spellLevel">A spell level, or -1 for all of them.</param>
    /// <param name="hasLibrary">Whether the town has a library.</param>
    /// <returns>The number of slots.</returns>
    public static int GetMaxSpellsCount(int spellLevel, bool hasLibrary)
    {
        if (spellLevel == -1)
        {
            return 11 + (hasLibrary ? 5 : 0);
        }

        if (spellLevel is < 1 or > 5)
        {
            return 0;
        }

        return GuildMaxSpellsCount[spellLevel - 1] + (hasLibrary ? 1 : 0);
    }

    /// <summary>
    /// Lays the guild out for a race, <c>MageGuild::initialize( race, hasLibrary )</c>
    /// (<c>mageguild.cpp:100-178</c>).
    /// </summary>
    /// <param name="race">The race of the town.</param>
    /// <param name="hasLibrary">Whether the town has a library.</param>
    /// <param name="generator">The random generator to draw with.</param>
    public void Initialize(MapRace race, bool hasLibrary, Pcg32? generator = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero((int)race);

        Array.Fill(_general, SpellType.None);
        Array.Fill(_library, SpellType.None);

        HashSet<SpellType> spellsInUse = [];
        int[] freeSlots = new int[5];
        int[] generalPlaced = new int[5];
        bool[] hasAdventureSpell = new bool[5];

        // The library slot is one of the level's free slots (mageguild.cpp:91-92), and the reference appends the
        // general spells to a vector — the port has a fixed array, so it counts what it has placed instead.
        for (int level = 0; level < 5; level++)
        {
            freeSlots[level] = GuildMaxSpellsCount[level] + (hasLibrary ? 1 : 0);
        }

        void AddSpell(Spell spell)
        {
            int level = spell.Level();

            if (!spellsInUse.Add(spell.Type))
            {
                return;
            }

            if (hasLibrary && freeSlots[level - 1] == 1)
            {
                _library[level - 1] = spell.Type;
            }
            else
            {
                _general[LevelOffsets[level - 1] + generalPlaced[level - 1]] = spell.Type;
                generalPlaced[level - 1]++;
            }

            freeSlots[level - 1]--;

            if (spell.IsAdventure())
            {
                hasAdventureSpell[level - 1] = true;
            }
        }

        // A guild always holds one of the damage spells and one that cancels a spell.
        AddSpell(GetGuaranteedDamageSpell(generator));
        AddSpell(GetGuaranteedCancellationSpell(generator));

        for (int level = 1; level <= 5; level++)
        {
            List<SpellType> allSpellsOfLevel = [.. Spell.SuitableForSpellBook(level)];

            while (freeSlots[level - 1] > 0)
            {
                int spellIndex = Next(generator, 0, allSpellsOfLevel.Count - 1);
                Spell spell = new(allSpellsOfLevel[spellIndex]);

                // Some spells are less likely depending on the race. The roll is `> weight`, not `>=`, so a
                // weight of zero still lets the spell through once in eleven attempts.
                if (Next(generator, 0, 10) > spell.WeightForRace(race))
                {
                    continue;
                }

                // One adventure spell per level.
                if (!hasAdventureSpell[level - 1] || !spell.IsAdventure())
                {
                    AddSpell(spell);
                }

                allSpellsOfLevel.RemoveAt(spellIndex);
            }
        }
    }

    /// <summary>
    /// Lays the guild out with the spells a map forces into it, <c>MageGuild::initialize( race, hasLibrary,
    /// mustHaveSpells, bannedSpells )</c> (<c>mageguild.cpp:181-359</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The map's castle block names a slot, not a level: <c>place / 10</c> is the level from one and
    /// <c>place % 10</c> the position — <c>30</c> is the first third-level slot, <c>33</c> the library slot of
    /// that level. A library slot for a town without a library is dropped, which is what a random town gets when
    /// the rolled race has no library.
    /// </para>
    /// <para>
    /// The forced spells do not have to obey the weight of the race, the adventure limit or the guarantee above;
    /// the random fill around them does, and it also treats them as unusable, exactly like the banned ones.
    /// </para>
    /// </remarks>
    /// <param name="race">The race of the town.</param>
    /// <param name="hasLibrary">Whether the town has a library.</param>
    /// <param name="mustHave">Forced spells by slot.</param>
    /// <param name="banned">Spells the random fill must not use.</param>
    /// <param name="generator">The random generator to draw with.</param>
    public void Initialize(
        MapRace race,
        bool hasLibrary,
        IReadOnlyDictionary<int, SpellType> mustHave,
        IReadOnlyCollection<SpellType> banned,
        Pcg32? generator = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero((int)race);

        if (mustHave.Count == 0 && banned.Count == 0)
        {
            Initialize(race, hasLibrary, generator);
            return;
        }

        Array.Fill(_general, SpellType.None);
        Array.Fill(_library, SpellType.None);

        HashSet<SpellType> spellsInUse = [.. banned];

        // The free slot count is the level's capacity *including* the library slot
        // (mageguild.cpp:91-92: freeSlots( getMaxSpellsCount( spellLevel, hasLibrary ) )).
        int[] freeSlots = new int[5];

        for (int level = 0; level < 5; level++)
        {
            freeSlots[level] = GuildMaxSpellsCount[level] + (hasLibrary ? 1 : 0);
        }
        bool[] hasAdventureSpell = new bool[5];

        bool hasGuaranteedDamageSpell = false;
        bool hasGuaranteedCancellationSpell = false;

        List<SpellType> guaranteedDamageSpells = [];
        List<SpellType> guaranteedCancellationSpells = [];

        foreach (SpellType spell in new[]
                 {
                     SpellType.Arrow, SpellType.LightningBolt, SpellType.Fireball, SpellType.ColdRay, SpellType.ColdRing,
                 })
        {
            if (!spellsInUse.Contains(spell))
            {
                guaranteedDamageSpells.Add(spell);
            }
        }

        foreach (SpellType spell in new[] { SpellType.Dispel, SpellType.MassDispel, SpellType.AntiMagic, SpellType.Cure })
        {
            if (!spellsInUse.Contains(spell))
            {
                guaranteedCancellationSpells.Add(spell);
            }
        }

        foreach ((int place, SpellType spellId) in mustHave)
        {
            int levelIndex = place / 10;
            int pos = place % 10;

            if (levelIndex is < 0 or > 4)
            {
                continue;
            }

            if (pos == GuildMaxSpellsCount[levelIndex] && !hasLibrary)
            {
                continue;
            }

            if (pos < GuildMaxSpellsCount[levelIndex])
            {
                _general[LevelOffsets[levelIndex] + pos] = spellId;
            }
            else if (pos == GuildMaxSpellsCount[levelIndex])
            {
                _library[levelIndex] = spellId;
            }

            freeSlots[levelIndex]--;

            if (new Spell(spellId).IsAdventure())
            {
                hasAdventureSpell[levelIndex] = true;
            }
            else if (guaranteedDamageSpells.Contains(spellId))
            {
                hasGuaranteedDamageSpell = true;
            }
            else if (guaranteedCancellationSpells.Contains(spellId))
            {
                hasGuaranteedCancellationSpell = true;
            }

            spellsInUse.Add(spellId);
        }

        bool AddSpell(Spell spell)
        {
            int level = spell.Level();
            int levelIndex = level - 1;

            if (levelIndex is < 0 or > 4 || freeSlots[levelIndex] < 1)
            {
                return false;
            }

            if (!spellsInUse.Add(spell.Type))
            {
                return false;
            }

            if (hasLibrary && _library[levelIndex] == SpellType.None)
            {
                _library[levelIndex] = spell.Type;
            }
            else
            {
                int offset = LevelOffsets[levelIndex];

                for (int slot = 0; slot < GuildMaxSpellsCount[levelIndex]; slot++)
                {
                    if (_general[offset + slot] == SpellType.None)
                    {
                        _general[offset + slot] = spell.Type;
                        break;
                    }
                }
            }

            freeSlots[levelIndex]--;

            // The reference sets the flag but never reads it outside its own assert; kept for the same reason.
            if (!hasAdventureSpell[levelIndex] && spell.IsAdventure())
            {
                hasAdventureSpell[levelIndex] = true;
            }

            return true;
        }

        void RandomlyAddAnySpellFromVector(List<SpellType> spells)
        {
            while (spells.Count > 0)
            {
                int index = Next(generator, 0, spells.Count - 1);

                if (AddSpell(new Spell(spells[index])))
                {
                    break;
                }

                spells.RemoveAt(index);
            }
        }

        if (!hasGuaranteedDamageSpell)
        {
            RandomlyAddAnySpellFromVector(guaranteedDamageSpells);
        }

        if (!hasGuaranteedCancellationSpell)
        {
            RandomlyAddAnySpellFromVector(guaranteedCancellationSpells);
        }

        for (int level = 1; level <= 5; level++)
        {
            List<SpellType> allSpellsOfLevel = [.. Spell.SuitableForSpellBook(level, spellsInUse)];

            while (freeSlots[level - 1] > 0)
            {
                int spellIndex = Next(generator, 0, allSpellsOfLevel.Count - 1);
                Spell spell = new(allSpellsOfLevel[spellIndex]);

                // A map that forces more spells of a level than the guild holds gets them without a roll.
                bool moreSpellsThanSlots = allSpellsOfLevel.Count > freeSlots[level - 1];

                if (moreSpellsThanSlots && Next(generator, 0, 10) > spell.WeightForRace(race))
                {
                    continue;
                }

                if (!moreSpellsThanSlots || !hasAdventureSpell[level - 1] || !spell.IsAdventure())
                {
                    AddSpell(spell);
                }

                allSpellsOfLevel.RemoveAt(spellIndex);
            }
        }
    }

    /// <summary>
    /// The spells the guild of a level holds, <c>MageGuild::GetSpells()</c> (<c>mageguild.cpp:361-389</c>).
    /// </summary>
    /// <param name="guildLevel">The level of the guild, from 1 to 5.</param>
    /// <param name="hasLibrary">Whether the town has a library.</param>
    /// <param name="spellLevel">A spell level to keep, or -1 for every one the guild level reaches.</param>
    /// <returns>The spells, general slots first, then the library.</returns>
    public SpellStorage GetSpells(int guildLevel, bool hasLibrary, int spellLevel = -1)
    {
        SpellStorage result = new();

        void AppendLevel(int level)
        {
            int offset = LevelOffsets[level - 1];

            for (int slot = 0; slot < GuildMaxSpellsCount[level - 1]; slot++)
            {
                result.Append(new Spell(_general[offset + slot]));
            }

            if (hasLibrary)
            {
                result.Append(new Spell(_library[level - 1]));
            }
        }

        if (spellLevel == -1)
        {
            for (int level = 1; level <= Math.Min(guildLevel, 5); level++)
            {
                AppendLevel(level);
            }
        }
        else if (spellLevel is >= 1 and <= 5 && spellLevel <= guildLevel)
        {
            AppendLevel(spellLevel);
        }

        return result;
    }

    /// <summary>
    /// Writes the guild's spells into a hero's book, <c>MageGuild::trainHero()</c> (<c>mageguild.cpp:412-427</c>).
    /// </summary>
    /// <remarks>
    /// The wisdom requirement is inside <see cref="ISpellBookOwner.AppendSpellToBook"/>, not here: a hero without
    /// the book learns nothing and a hero without wisdom learns only the first two levels, which is also what a
    /// captain gets.
    /// </remarks>
    /// <param name="hero">The hero — or the captain — to teach.</param>
    /// <param name="guildLevel">The level of the guild, from 1 to 5.</param>
    /// <param name="hasLibrary">Whether the town has a library.</param>
    public void TrainHero(ISpellBookOwner hero, int guildLevel, bool hasLibrary)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (!hero.HasSpellBook || guildLevel < 1)
        {
            return;
        }

        foreach (Spell spell in GetSpells(guildLevel, hasLibrary))
        {
            hero.AppendSpellToBook(spell);
        }
    }

    /// <summary>One of the five damage spells a guild always holds, <c>getGuaranteedDamageSpell()</c>.</summary>
    private static Spell GetGuaranteedDamageSpell(Pcg32? generator) => Next(generator, 0, 99) switch
    {
        < 20 => new Spell(SpellType.Arrow),
        < 40 => new Spell(SpellType.LightningBolt),
        < 60 => new Spell(SpellType.Fireball),
        < 80 => new Spell(SpellType.ColdRay),
        _ => new Spell(SpellType.ColdRing),
    };

    /// <summary>One of the four spells that cancel a spell, <c>getGuaranteedCancellationSpell()</c>.</summary>
    private static Spell GetGuaranteedCancellationSpell(Pcg32? generator) => Next(generator, 0, 99) switch
    {
        < 25 => new Spell(SpellType.Dispel),
        < 50 => new Spell(SpellType.MassDispel),
        < 75 => new Spell(SpellType.AntiMagic),
        _ => new Spell(SpellType.Cure),
    };

    /// <summary>Draws from the injected generator, or from the shared one when there is none.</summary>
    private static int Next(Pcg32? generator, int min, int max) =>
        generator is null ? Rand.Get(min, max) : (int)Rand.GetWithGen((uint)min, (uint)max, generator);
}
