using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>week.h</c> / <c>week.cpp</c>: the two draw tables and the <see cref="Week"/> value they produce.
/// The seed mixing they draw with is covered by <c>RandTests.CombineSeedWithValueHash_IsTheReferenceHashCombine</c>.
/// </summary>
public sealed class WeekTests
{
    /// <summary>The twelve monsters a monster's month can draw, <c>week.cpp:83-116</c>.</summary>
    private static readonly MonsterType[] MonthMonsters =
    [
        MonsterType.Peasant, MonsterType.Wolf, MonsterType.Ogre, MonsterType.Troll,
        MonsterType.Dwarf, MonsterType.Druid, MonsterType.Unicorn, MonsterType.Centaur,
        MonsterType.Gargoyle, MonsterType.Roc, MonsterType.Vampire, MonsterType.Lich,
    ];

    [Fact]
    public void TheDefaultWeekIsUnnamedAndCarriesNoMonster()
    {
        // Both underlying values are zero, so `new Week()` is the reference's default constructor.
        Week week = new();

        Assert.Equal(WeekName.Unnamed, week.Type);
        Assert.Equal(MonsterType.Unknown, week.Monster);
    }

    [Fact]
    public void TheWeekKeepsWhatItWasGiven()
    {
        Week week = new(WeekName.Monsters, MonsterType.Ranger);

        Assert.Equal(WeekName.Monsters, week.Type);
        Assert.Equal(MonsterType.Ranger, week.Monster);
    }

    [Fact]
    public void TheSameSeedAlwaysGivesTheSameWeek()
    {
        foreach (uint seed in new uint[] { 0, 1, 7, 42, 12345, 4000000000 })
        {
            Week first = Week.RandomWeek(false, seed);
            Week second = Week.RandomWeek(false, seed);
            Week monthFirst = Week.RandomWeek(true, seed);
            Week monthSecond = Week.RandomWeek(true, seed);

            Assert.Equal(first, second);
            Assert.Equal(monthFirst, monthSecond);
        }
    }

    [Theory]
    // A snapshot: every table below is driven by a salt, so a mistyped constant would keep the odds right and
    // change every week name. These eleven seeds pin the salts down.
    [InlineData(0u, WeekName.Rabbit, MonsterType.Unknown, WeekName.Monsters, MonsterType.Unicorn)]
    [InlineData(1u, WeekName.Weasel, MonsterType.Unknown, WeekName.Monsters, MonsterType.Gargoyle)]
    [InlineData(2u, WeekName.Raven, MonsterType.Unknown, WeekName.Monsters, MonsterType.Unicorn)]
    [InlineData(3u, WeekName.Gopher, MonsterType.Unknown, WeekName.Monsters, MonsterType.Centaur)]
    [InlineData(7u, WeekName.Monsters, MonsterType.Ranger, WeekName.Earthworm, MonsterType.Unknown)]
    [InlineData(42u, WeekName.Condor, MonsterType.Unknown, WeekName.Butterfly, MonsterType.Unknown)]
    [InlineData(255u, WeekName.Raven, MonsterType.Unknown, WeekName.Butterfly, MonsterType.Unknown)]
    [InlineData(12345u, WeekName.Condor, MonsterType.Unknown, WeekName.Monsters, MonsterType.Wolf)]
    [InlineData(65535u, WeekName.Eagle, MonsterType.Unknown, WeekName.Spider, MonsterType.Unknown)]
    [InlineData(123456789u, WeekName.Mongoose, MonsterType.Unknown, WeekName.Ant, MonsterType.Unknown)]
    [InlineData(4000000000u, WeekName.Raven, MonsterType.Unknown, WeekName.Monsters, MonsterType.Unicorn)]
    public void TheDrawsMatchTheirSnapshot(
        uint seed,
        WeekName weekType,
        MonsterType weekMonster,
        WeekName monthType,
        MonsterType monthMonster)
    {
        Assert.Equal(new Week(weekType, weekMonster), Week.RandomWeek(false, seed));
        Assert.Equal(new Week(monthType, monthMonster), Week.RandomWeek(true, seed));
    }

    [Fact]
    public void OnlyAMonstersWeekNamesAMonster()
    {
        for (uint seed = 0; seed < 5000; ++seed)
        {
            Week week = Week.RandomWeek(false, seed);

            // A regular week is one of the fifteen animals, and names no monster (week.cpp:47-49).
            if (week.Type is >= WeekName.Squirrel and <= WeekName.Condor)
            {
                Assert.Equal(MonsterType.Unknown, week.Monster);
            }
            else
            {
                Assert.Equal(WeekName.Monsters, week.Type);
                Assert.InRange((int)week.Monster, (int)MonsterType.Peasant, (int)MonsterType.BoneDragon);
            }
        }
    }

    [Fact]
    public void OnlyAMonstersMonthNamesAMonsterAndThePlagueNamesNone()
    {
        for (uint seed = 0; seed < 5000; ++seed)
        {
            Week month = Week.RandomWeek(true, seed);

            if (month.Type is >= WeekName.Ant and <= WeekName.Beetle)
            {
                // A regular month is one of the ten insects, and names no monster (week.cpp:64-66).
                Assert.Equal(MonsterType.Unknown, month.Monster);
            }
            else if (month.Type == WeekName.Monsters)
            {
                // A monster's month draws from twelve monsters, never from all seventy-two (week.cpp:79-116).
                Assert.Contains(month.Monster, MonthMonsters);
            }
            else
            {
                Assert.Equal(WeekName.Plague, month.Type);
                Assert.Equal(MonsterType.Unknown, month.Monster);
            }
        }
    }

    [Fact]
    public void TheOddsAreTheReferences()
    {
        const uint Samples = 20000;

        int monsterWeeks = 0;
        int monsterMonths = 0;
        int plagues = 0;
        int regularMonths = 0;

        for (uint seed = 0; seed < Samples; ++seed)
        {
            if (Week.RandomWeek(false, seed).Type == WeekName.Monsters)
            {
                ++monsterWeeks;
            }

            switch (Week.RandomWeek(true, seed).Type)
            {
                case WeekName.Monsters:
                    ++monsterMonths;
                    break;
                case WeekName.Plague:
                    ++plagues;
                    break;
                default:
                    ++regularMonths;
                    break;
            }
        }

        // week.cpp:32-35 and week.cpp:56-70: 75% / 25% for a week, 50% / 40% / 10% for a month.
        Assert.InRange((double)monsterWeeks / Samples, 0.22, 0.28);
        Assert.InRange((double)monsterMonths / Samples, 0.37, 0.43);
        Assert.InRange((double)plagues / Samples, 0.07, 0.13);
        Assert.InRange((double)regularMonths / Samples, 0.47, 0.53);
    }
}
