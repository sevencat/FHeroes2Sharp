using FHeroes2.Core.Timing;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// Covers <c>fheroes2::TimeDelay</c> (<c>src/engine/timing.h:57</c>) through the port's injectable clock,
/// so the tests control time instead of sleeping.
/// </summary>
public class TimeDelayTests
{
    [Fact]
    public void AFreshDelayHasNotPassed()
    {
        long now = 1000;
        TimeDelay delay = new(250, () => now);

        Assert.Equal(250ul, delay.Delay);
        Assert.False(delay.IsPassed());
    }

    [Fact]
    public void TheDelayPassesInclusiveOfItsOwnLength()
    {
        long now = 1000;
        TimeDelay delay = new(250, () => now);

        now = 1249;
        Assert.False(delay.IsPassed());

        // 参考比较的是 >=，第 250 毫秒就算到了。
        now = 1250;
        Assert.True(delay.IsPassed());

        now = 5000;
        Assert.True(delay.IsPassed());
    }

    [Fact]
    public void AResetRestartsTheCount()
    {
        long now = 1000;
        TimeDelay delay = new(100, () => now);

        now = 1200;
        Assert.True(delay.IsPassed());

        delay.Reset();

        Assert.False(delay.IsPassed());

        now = 1300;
        Assert.True(delay.IsPassed());
    }

    [Fact]
    public void PassingPutsItIntoThePassedStateImmediately()
    {
        long now = 1000;
        TimeDelay delay = new(500, () => now);

        delay.Pass();

        // 参考把起点拨回 now - 2 * delay，因此不必等到时间真的过去。
        Assert.True(delay.IsPassed());

        now = 1100;
        Assert.True(delay.IsPassed());
    }

    [Fact]
    public void TheDelayCanBeChangedAfterwards()
    {
        long now = 0;
        TimeDelay delay = new(1000, () => now)
        {
            Delay = 10,
        };

        now = 10;

        Assert.True(delay.IsPassed());
    }

    [Fact]
    public void AnArbitraryLengthCanBeAskedWithoutTouchingTheField()
    {
        long now = 0;
        TimeDelay delay = new(1000, () => now);

        now = 300;

        Assert.False(delay.IsPassed());
        Assert.True(delay.IsPassed(300));
        Assert.False(delay.IsPassed(301));
    }

    [Fact]
    public void AClockThatGoesBackwardsDoesNotPass()
    {
        long now = 1000;
        TimeDelay delay = new(100, () => now);

        // 假时钟（或调整过的系统时钟）倒退时不制造无符号回绕，直接答「还没到」。
        now = 900;

        Assert.False(delay.IsPassed());
    }
}
