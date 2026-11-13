using FHeroes2.Core.Timing;

namespace FHeroes2.Core.Tests;

public sealed class FrameTimerTests
{
    [Fact]
    public void Tick_AccumulatesFramesAndTime()
    {
        FrameTimer timer = new();

        for (int i = 0; i < 60; ++i)
        {
            timer.Tick(1.0 / 60.0);
        }

        Assert.Equal(60, timer.FrameCount);
        Assert.Equal(1.0, timer.TotalSeconds, 3);
        Assert.InRange(timer.FramesPerSecond, 55.0, 65.0);
    }

    [Fact]
    public void Tick_IgnoresNonPositiveElapsedTime()
    {
        FrameTimer timer = new();

        timer.Tick(0);
        timer.Tick(-1);

        Assert.Equal(0, timer.FrameCount);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        FrameTimer timer = new();
        timer.Tick(0.016);

        timer.Reset();

        Assert.Equal(0, timer.FrameCount);
        Assert.Equal(0, timer.TotalSeconds);
        Assert.Equal(0, timer.FramesPerSecond);
    }

    [Fact]
    public void Constructor_RejectsInvalidSmoothing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameTimer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameTimer(1));
    }
}
