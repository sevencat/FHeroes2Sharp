using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The timed repeat of a held button, <c>fheroes2::TimedEventValidator</c>
/// (<c>src/fheroes2/gui/ui_tool.h:227-259</c>).
/// </summary>
public sealed class TimedEventValidatorTests
{
    [Fact]
    public void IsDelayPassed_WaitsForTheFirstDelay()
    {
        long now = 1000;
        TimedEventValidator repeat = new(clockMs: () => now);

        Assert.False(repeat.IsDelayPassed(isHeld: true));

        now += 499;
        Assert.False(repeat.IsDelayPassed(isHeld: true));

        // The reference compares with >=, so the 500th millisecond already fires.
        now += 1;
        Assert.True(repeat.IsDelayPassed(isHeld: true));
    }

    [Fact]
    public void IsDelayPassed_RepeatsEveryBetweenDelay()
    {
        long now = 0;
        TimedEventValidator repeat = new(clockMs: () => now);

        repeat.IsDelayPassed(isHeld: true);
        now += 500;

        Assert.True(repeat.IsDelayPassed(isHeld: true));

        // Firing restarts only the between delay, so the next repeat comes 100 ms later.
        now += 99;
        Assert.False(repeat.IsDelayPassed(isHeld: true));

        now += 1;
        Assert.True(repeat.IsDelayPassed(isHeld: true));
    }

    [Fact]
    public void IsDelayPassed_NeverFiresWhileTheButtonIsUp()
    {
        long now = 0;
        TimedEventValidator repeat = new(clockMs: () => now);

        now += 10_000;

        Assert.False(repeat.IsDelayPassed(isHeld: false));
    }

    [Fact]
    public void IsDelayPassed_RestartsTheCountOnANewGesture()
    {
        long now = 0;
        TimedEventValidator repeat = new(clockMs: () => now);

        repeat.IsDelayPassed(isHeld: true);
        now += 600;
        Assert.True(repeat.IsDelayPassed(isHeld: true));

        // Releasing notifies the validator, and so does pressing again: both delays start over.
        Assert.False(repeat.IsDelayPassed(isHeld: false));
        Assert.False(repeat.IsDelayPassed(isHeld: true));

        now += 499;
        Assert.False(repeat.IsDelayPassed(isHeld: true));

        now += 1;
        Assert.True(repeat.IsDelayPassed(isHeld: true));
    }

    [Fact]
    public void Reset_StartsBothDelaysOver()
    {
        long now = 0;
        TimedEventValidator repeat = new(clockMs: () => now);

        repeat.IsDelayPassed(isHeld: true);
        now += 600;
        Assert.True(repeat.IsDelayPassed(isHeld: true));

        repeat.Reset();
        now += 499;

        Assert.False(repeat.IsDelayPassed(isHeld: true));

        now += 1;
        Assert.True(repeat.IsDelayPassed(isHeld: true));
    }

    [Fact]
    public void Constructor_TakesTheDelaysOfTheReference()
    {
        long now = 0;
        TimedEventValidator repeat = new(delayBeforeFirstUpdateMs: 300, delayBetweenUpdatesMs: 50, clockMs: () => now);

        // The first frame of the gesture only starts the count.
        Assert.False(repeat.IsDelayPassed(isHeld: true));

        now += 300;
        Assert.True(repeat.IsDelayPassed(isHeld: true));

        now += 50;
        Assert.True(repeat.IsDelayPassed(isHeld: true));

        now += 49;
        Assert.False(repeat.IsDelayPassed(isHeld: true));
    }
}
