using System.Buffers.Binary;
using System.Collections.Generic;

using FHeroes2.Core.Random;
using FHeroes2.Game.Kingdom;

using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the clock of the world: the three counters, the derived display values, the week drawn from them
/// and the daily event pruning of <c>World::NewDay()</c> (<c>src/fheroes2/world/world.h:371-424</c>,
/// <c>world.cpp:410</c>, <c>world.cpp:425-460</c>, <c>world.cpp:888</c>, <c>world.cpp:1263</c>).
/// </summary>
public sealed class WorldDateTests
{
    [Fact]
    public void AFreshWorldIsAtZeroCounters()
    {
        World world = new();

        Assert.Equal(0u, world.DayCount);
        Assert.Equal(0u, world.WeekCount);
        Assert.Equal(0u, world.Month);
        Assert.False(world.IsBeginWeek);
        Assert.False(world.IsBeginMonth);
        Assert.False(world.IsFirstWeek);

        // The reference's LastDay() / LastWeek() are bare modulo tests, so day 0 counts as the last day
        // of the last week and both getters promote the zero remainder to the full period — a fresh world
        // reports day 7 and week 4 (world.h:371-378, :406-419).
        Assert.True(world.IsLastDay);
        Assert.True(world.IsLastWeek);
        Assert.Equal(7u, world.Day);
        Assert.Equal(4u, world.Week);
    }

    [Fact]
    public void TheFirstDayOpensTheFirstWeekAndTheFirstMonth()
    {
        World world = new();

        world.NewDay();

        Assert.Equal(1u, world.DayCount);
        Assert.Equal(1u, world.WeekCount);
        Assert.Equal(1u, world.Month);
        Assert.Equal(1u, world.Day);
        Assert.Equal(1u, world.Week);
        Assert.True(world.IsBeginWeek);
        Assert.True(world.IsBeginMonth);
        Assert.True(world.IsFirstWeek);
        Assert.False(world.IsLastDay);
        Assert.False(world.IsLastWeek);
    }

    [Fact]
    public void TheSeventhDayIsTheLastDayOfTheWeek()
    {
        World world = AdvanceDays(7);

        Assert.Equal(7u, world.DayCount);
        Assert.Equal(1u, world.WeekCount);
        Assert.Equal(1u, world.Month);
        Assert.Equal(7u, world.Day);
        Assert.True(world.IsLastDay);
        Assert.False(world.IsBeginWeek);
    }

    [Fact]
    public void TheFourthWeekIsTheLastWeekOfTheMonth()
    {
        World world = AdvanceDays(28);

        Assert.Equal(4u, world.WeekCount);
        Assert.Equal(1u, world.Month);
        Assert.Equal(4u, world.Week);
        Assert.True(world.IsLastWeek);
        Assert.False(world.IsFirstWeek);
    }

    [Fact]
    public void TheTwentyNinthDayOpensTheSecondMonth()
    {
        World world = AdvanceDays(29);

        Assert.Equal(29u, world.DayCount);
        Assert.Equal(5u, world.WeekCount);
        Assert.Equal(2u, world.Month);
        Assert.Equal(1u, world.Week);
        Assert.Equal(1u, world.Day);
        Assert.True(world.IsBeginMonth);
    }

    [Fact]
    public void TheDateReadsAsTheStatusPanelPrintsIt()
    {
        // Thirty-five days is the seventh day of the first week of the second month.
        World world = AdvanceDays(35);

        Assert.Equal("month: 2, week: 1, day: 7", world.DateString());
    }

    [Fact]
    public void ADateStartsOverAfterReset()
    {
        World world = AdvanceDays(40);

        world.Reset();

        Assert.Equal(0u, world.DayCount);
        Assert.Equal(0u, world.WeekCount);
        Assert.Equal(0u, world.Month);
    }

    [Fact]
    public void AnEventIsDroppedOneDayAfterItsOnlyOccurrence()
    {
        World world = new();
        world.AddEventDate(EventAlreadyFired(10, repeatPeriod: 0));

        // The reference prunes against the day that just ended, so an event that fires on day 10 is
        // still in the list while the clock stands at 11 (world.cpp:459).
        AdvanceDays(11, world);
        Assert.Single(world.DailyEvents);

        AdvanceDays(1, world);
        Assert.Empty(world.DailyEvents);
    }

    [Fact]
    public void ARepeatingEventIsNeverDropped()
    {
        World world = new();
        world.AddEventDate(EventAlreadyFired(10, repeatPeriod: 5));

        AdvanceDays(100, world);

        Assert.Single(world.DailyEvents);
    }

    [Fact]
    public void TheWeekSeedIsTheMapSeedSaltedWithTheWeek()
    {
        World world = new();
        world.MapSeed = 12345;

        // world.cpp:1263: the map seed mixed with the week counter, so consecutive weeks do not draw alike.
        Assert.Equal(12345u, world.MapSeed);
        Assert.Equal(Rand.CombineSeedWithValueHash(12345u, 0u), world.WeekSeed);

        AdvanceDays(7, world);

        Assert.Equal(1u, world.WeekCount);
        Assert.Equal(Rand.CombineSeedWithValueHash(12345u, 1u), world.WeekSeed);
    }

    [Fact]
    public void TheWeekTypeIsDrawnFromTheWeekSeedAndTheFirstWeekFlag()
    {
        World world = new();
        world.MapSeed = 12345;

        // Week 0 is not the first week of a month, so the draw comes from the week table (week.cpp:35-51).
        Assert.False(world.IsFirstWeek);
        Assert.Equal(Week.RandomWeek(isNewMonth: false, Rand.CombineSeedWithValueHash(12345u, 0u)), world.WeekType);

        // One day later the clock stands in week 1, which is the first week of the month: the month table.
        AdvanceDays(1, world);

        Assert.True(world.IsFirstWeek);
        Assert.Equal(Week.RandomWeek(isNewMonth: true, Rand.CombineSeedWithValueHash(12345u, 1u)), world.WeekType);

        // Week by week: the type is a function of the date and the map seed, and of nothing else.
        for (uint week = 2; week <= 9; ++week)
        {
            AdvanceDays(7, world);

            Assert.Equal(week, world.WeekCount);
            Assert.Equal(week % 4 == 1, world.IsFirstWeek);
            Assert.Equal(
                Week.RandomWeek(world.IsFirstWeek, Rand.CombineSeedWithValueHash(12345u, week)),
                world.WeekType);
        }
    }

    [Fact]
    public void TheWeeksOfOneMapAreNotAllTheSameWeek()
    {
        // A sanity check on the salt: without it every week of a map would draw from the same seed and, being
        // nine draws of the same three-outcome table, would repeat a lot more than this.
        World world = new();
        world.MapSeed = 12345;

        HashSet<Week> drawn = [];

        for (uint week = 0; week < 9; ++week)
        {
            drawn.Add(world.WeekType);
            AdvanceDays(7, world);
        }

        Assert.True(drawn.Count >= 4, $"nine weeks of one map gave only {drawn.Count} distinct draws");
    }

    [Fact]
    public void AResetForgetsTheMapSeed()
    {
        // world.cpp:335: a new map draws a new seed, so the old one must not survive the reset.
        World world = new();
        world.MapSeed = 12345;

        world.Reset();

        Assert.Equal(0u, world.MapSeed);
    }

    private static World AdvanceDays(uint days)
    {
        World world = new();
        AdvanceDays(days, world);
        return world;
    }

    private static void AdvanceDays(uint days, World world)
    {
        for (uint day = 0; day < days; ++day)
        {
            world.NewDay();
        }
    }

    /// <summary>
    /// Builds an <see cref="EventDate"/> the way the loader does, through
    /// <see cref="EventDate.TryRead"/>: the constructor is private and an event only ever comes from a
    /// map block.
    /// </summary>
    private static EventDate EventAlreadyFired(ushort firstDay, ushort repeatPeriod)
    {
        // MP2_EVENT_STRUCTURE_MIN_SIZE is 50; the marker at 42 tells an event from a rumor.
        byte[] block = new byte[50];
        block[42] = 0x01;
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(33), firstDay);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(35), repeatPeriod);

        Assert.True(EventDate.TryRead(block, out EventDate? eventDate));
        return eventDate!;
    }
}
