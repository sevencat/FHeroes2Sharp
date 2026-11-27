using FHeroes2.Game.System;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of <see cref="GameDate.Describe"/>, the port of <c>Game::getDateDescription()</c>
/// (<c>game_tools.cpp:28-40</c>).
/// </summary>
public sealed class GameDateTests
{
    [Theory]
    [InlineData(1, "Day: 1 Week: 1 Month: 1")]
    [InlineData(7, "Day: 7 Week: 1 Month: 1")]
    [InlineData(8, "Day: 1 Week: 2 Month: 1")]
    [InlineData(28, "Day: 7 Week: 4 Month: 1")]
    [InlineData(29, "Day: 1 Week: 1 Month: 2")]
    [InlineData(56, "Day: 7 Week: 4 Month: 2")]
    [InlineData(365, "Day: 1 Week: 1 Month: 14")]
    public void TheDayIsReportedAsDayWeekAndMonth(int day, string expected)
    {
        Assert.Equal(expected, GameDate.Describe(day));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ADayBelowOneIsRejected(int day)
    {
        // 参考不校验（会算出没有意义的日子）；端口跟着数值控件的下限要求至少是 1。
        Assert.Throws<ArgumentOutOfRangeException>(() => GameDate.Describe(day));
    }

    [Fact]
    public void TheConstantsMatchTheGameCalendar()
    {
        Assert.Equal(7, GameDate.DaysPerWeek);
        Assert.Equal(4, GameDate.WeeksPerMonth);
        Assert.Equal(28, GameDate.DaysPerMonth);
    }
}
