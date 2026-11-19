using FHeroes2.Core.Random;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// What the current week is called and which monster it favours, <c>struct Week</c>
/// (<c>week.h:75-97</c>).
/// </summary>
/// <remarks>
/// <para>
/// A value type in both languages, and for the same reason: the world holds one <c>Week</c> and copies it into
/// messages. <c>new Week()</c> is the reference's default constructor — <see cref="WeekName.Unnamed"/> and
/// <see cref="MonsterType.Unknown"/> — because both underlying values are zero.
/// </para>
/// <para>
/// <c>Week::GetName()</c> (<c>week.cpp:126-206</c>) is here as <see cref="GetName"/>: the reference runs every
/// word through the translation table, the port returns the reference's own English (ADR-171), and a monster's
/// week answers with the creature's singular name through <see cref="MonsterNames"/>.
/// </para>
/// </remarks>
public readonly struct Week
{
    /// <summary>
    /// A week, <c>Week::Week()</c> (<c>week.h:77</c>).
    /// </summary>
    /// <param name="type">Its name.</param>
    /// <param name="monster">The monster it favours; <see cref="MonsterType.Unknown"/> unless it is a monster's
    /// week.</param>
    public Week(WeekName type = WeekName.Unnamed, MonsterType monster = MonsterType.Unknown)
    {
        Type = type;
        Monster = monster;
    }

    /// <summary>The name of the week, <c>Week::GetType()</c>.</summary>
    public WeekName Type { get; }

    /// <summary>The favoured monster, <c>Week::GetMonster()</c>.</summary>
    public MonsterType Monster { get; }

    /// <summary>
    /// What the week is called, <c>Week::GetName()</c> (<c>week.cpp:126-206</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The words are the reference's own English, without the <c>week|</c> context prefix it hands to gettext, which
    /// is what <c>Experience</c> already does (ADR-137): the port has no translation layer, and returning a key
    /// instead of a word would make the one caller — "Astrologers proclaim the Week of the %{name}." — unreadable.
    /// </para>
    /// <para>
    /// Two rows surprise readers and are kept on purpose. A monster's week is <b>not</b> "Week of the Peasant": the
    /// reference returns the creature's singular name and lets the sentence supply the rest, so that is what
    /// <see cref="MonsterNames.GetSingular"/> answers. And the plague is <c>"PLAGUE"</c> in capitals, because the
    /// reference's own string is (<c>week.cpp:198</c>) — the original game prints it that way.
    /// </para>
    /// <para>
    /// A week whose type is somehow outside the enum answers <c>"Unnamed"</c>, matching the reference's
    /// <c>assert</c>-then-default, since the answer is only ever printed.
    /// </para>
    /// </remarks>
    /// <returns>The name, for example <c>"Squirrel"</c>, <c>"Ant"</c>, <c>"Peasant"</c> or <c>"PLAGUE"</c>.</returns>
    public string GetName() => Type switch
    {
        WeekName.Unnamed => "Unnamed",

        // A regular week of a month.
        WeekName.Squirrel => "Squirrel",
        WeekName.Rabbit => "Rabbit",
        WeekName.Gopher => "Gopher",
        WeekName.Badger => "Badger",
        WeekName.Rat => "Rat",
        WeekName.Eagle => "Eagle",
        WeekName.Weasel => "Weasel",
        WeekName.Raven => "Raven",
        WeekName.Mongoose => "Mongoose",
        WeekName.Dog => "Dog",
        WeekName.Aardvark => "Aardvark",
        WeekName.Lizard => "Lizard",
        WeekName.Tortoise => "Tortoise",
        WeekName.Hedgehog => "Hedgehog",
        WeekName.Condor => "Condor",

        // The first week of a month, which is a month.
        WeekName.Ant => "Ant",
        WeekName.Grasshopper => "Grasshopper",
        WeekName.Dragonfly => "Dragonfly",
        WeekName.Spider => "Spider",
        WeekName.Butterfly => "Butterfly",
        WeekName.Bumblebee => "Bumblebee",
        WeekName.Locust => "Locust",
        WeekName.Earthworm => "Earthworm",
        WeekName.Hornet => "Hornet",
        WeekName.Beetle => "Beetle",

        // A monster's week: the creature's name, not the phrase around it (week.cpp:200).
        WeekName.Monsters => MonsterNames.GetSingular(Monster),

        WeekName.Plague => "PLAGUE",

        _ => "Unnamed",
    };

    /// <summary>
    /// Draws the next week, <c>Week::RandomWeek()</c> (<c>week.cpp:185</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The draw is driven entirely by the map's week seed, so the same seed always gives the same week: the
    /// reference salts the seed six different ways (five for the type, one for the monster) rather than
    /// drawing twice in a row, which is why <see cref="Rand.CombineSeedWithValueHash"/> exists.
    /// </para>
    /// <para>
    /// The odds are the reference's: a regular week is 75%, a monster's week 25%; the first week of a month is
    /// 50% a regular month, 40% a monster's month and 10% the plague (<c>week.cpp:43-70</c>).
    /// </para>
    /// </remarks>
    /// <param name="isNewMonth">Whether this is the first week of a month, drawn from the month tables.</param>
    /// <param name="weekSeed">The week seed to draw from.</param>
    /// <returns>The week.</returns>
    public static Week RandomWeek(bool isNewMonth, uint weekSeed)
    {
        WeekName weekName = isNewMonth ? MonthRand(weekSeed) : WeekRand(weekSeed);

        if (weekName == WeekName.Monsters)
        {
            return isNewMonth
                ? new Week(weekName, RandomMonsterMonthOf(weekSeed))
                : new Week(weekName, RandomMonsterWeekOf(weekSeed));
        }

        return new Week(weekName, MonsterType.Unknown);
    }

    /// <summary>A regular week or a monster's week, <c>WeekRand()</c> (<c>week.cpp:35</c>).</summary>
    private static WeekName WeekRand(uint seed)
    {
        uint weekType = Rand.GetWithSeed(0, 3, Rand.CombineSeedWithValueHash(seed, 367245));

        // A regular week, probability 75%.
        if (weekType < 3)
        {
            uint weekSeed = Rand.CombineSeedWithValueHash(seed, 1946256);

            return (WeekName)Rand.GetWithSeed((uint)WeekName.Squirrel, (uint)WeekName.Condor, weekSeed);
        }

        // The Week of a monster, probability 25%.
        return WeekName.Monsters;
    }

    /// <summary>A regular month, a monster's month or the plague, <c>MonthRand()</c> (<c>week.cpp:53</c>).</summary>
    private static WeekName MonthRand(uint seed)
    {
        uint monthType = Rand.GetWithSeed(0, 9, Rand.CombineSeedWithValueHash(seed, 9536582));

        // A regular month, probability 50%.
        if (monthType < 5)
        {
            uint monthSeed = Rand.CombineSeedWithValueHash(seed, 5544783);

            return (WeekName)Rand.GetWithSeed((uint)WeekName.Ant, (uint)WeekName.Beetle, monthSeed);
        }

        // The Month of a monster, probability 40%.
        if (monthType < 9)
        {
            return WeekName.Monsters;
        }

        // The Month of the Plague, probability 10%.
        return WeekName.Plague;
    }

    /// <summary>The monster of a monster's week, <c>RandomMonsterWeekOf()</c> (<c>week.cpp:73</c>).</summary>
    /// <remarks>
    /// Any monster of the original seventy-two, peasants through bone dragons, is eligible; the reference draws
    /// an id and that range covers exactly the non-neutral part of the <c>Monster::MonsterType</c> enum.
    /// </remarks>
    private static MonsterType RandomMonsterWeekOf(uint seed)
    {
        uint monsterSeed = Rand.CombineSeedWithValueHash(seed, 886473);

        return (MonsterType)Rand.GetWithSeed((uint)MonsterType.Peasant, (uint)MonsterType.BoneDragon, monsterSeed);
    }

    /// <summary>The monster of a monster's month, <c>RandomMonsterMonthOf()</c> (<c>week.cpp:79</c>).</summary>
    /// <remarks>
    /// A month picks from twelve monsters rather than all seventy-two: the reference draws one of the twelve
    /// and maps it with a switch, so the month of a monster is never a dragon.
    /// </remarks>
    private static MonsterType RandomMonsterMonthOf(uint seed)
    {
        uint monsterSeed = Rand.CombineSeedWithValueHash(seed, 1130906);

        return Rand.GetWithSeed(1, 12, monsterSeed) switch
        {
            1 => MonsterType.Peasant,
            2 => MonsterType.Wolf,
            3 => MonsterType.Ogre,
            4 => MonsterType.Troll,
            5 => MonsterType.Dwarf,
            6 => MonsterType.Druid,
            7 => MonsterType.Unicorn,
            8 => MonsterType.Centaur,
            9 => MonsterType.Gargoyle,
            10 => MonsterType.Roc,
            11 => MonsterType.Vampire,
            12 => MonsterType.Lich,

            // Unreachable: the draw is bounded to [1, 12] (week.cpp:83), where the reference asserts.
            _ => MonsterType.Unknown,
        };
    }
}
