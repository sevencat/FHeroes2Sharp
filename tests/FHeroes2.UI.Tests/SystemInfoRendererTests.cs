using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The clock and FPS readout of <c>SystemInfoRenderer</c> (<c>src/fheroes2/gui/ui_tool.cpp:288</c>):
/// the one second window, the <c>+ 0.05</c> of the reference and the tenths below ten frames per second.
/// </summary>
public sealed class SystemInfoRendererTests
{
    private const string Label = SystemInfoRenderer.FpsLabel;

    /// <summary>Fifty frames a second are reported as <c>FPS: 50</c>.</summary>
    [Fact]
    public void BuildInfo_ReportsTheAverageOfTheLastSecond()
    {
        FakeClock clock = new();
        SystemInfoRenderer renderer = clock.CreateRenderer();

        // 51 samples of 20 ms fill the one second window exactly, i.e. 50 frames per second.
        string info = string.Empty;
        for (int frame = 0; frame < 51; ++frame)
        {
            clock.Advance(20);
            info = renderer.BuildInfo();
        }

        Assert.EndsWith($"{Label}50", info, StringComparison.Ordinal);
    }

    /// <summary>Below ten frames per second the tenths are shown, <c>FPS: 3.3</c> for 300 ms frames.</summary>
    [Fact]
    public void BuildInfo_ShowsTenthsBelowTenFramesPerSecond()
    {
        FakeClock clock = new();
        SystemInfoRenderer renderer = clock.CreateRenderer();

        string info = string.Empty;
        for (int frame = 0; frame < 4; ++frame)
        {
            clock.Advance(300);
            info = renderer.BuildInfo();
        }

        Assert.EndsWith($"{Label}3.3", info, StringComparison.Ordinal);
    }

    /// <summary>The readout starts with the wall clock, <c>HH:MM:SS</c>.</summary>
    [Fact]
    public void BuildInfo_StartsWithTheTimeOfDay()
    {
        FakeClock clock = new();
        SystemInfoRenderer renderer = clock.CreateRenderer(new DateTime(2026, 11, 21, 13, 5, 9));

        clock.Advance(16);
        string info = renderer.BuildInfo();

        Assert.StartsWith("13:05:09", info, StringComparison.Ordinal);
    }

    /// <summary>A clock that does not move yields no frames per second instead of an infinity.</summary>
    [Fact]
    public void BuildInfo_WithAStalledClockReportsZeroFramesPerSecond()
    {
        FakeClock clock = new();
        SystemInfoRenderer renderer = clock.CreateRenderer();

        string info = renderer.BuildInfo();

        Assert.Equal($"00:00:00{Label}0.0", info);
    }

    /// <summary>
    /// The window stops at the sample that crosses one second: twelve 100 ms frames are counted as
    /// eleven, which is exactly ten frames per second.
    /// </summary>
    [Fact]
    public void BuildInfo_DropsSamplesOlderThanOneSecond()
    {
        FakeClock clock = new();
        SystemInfoRenderer renderer = clock.CreateRenderer();

        string info = string.Empty;
        for (int frame = 0; frame < 12; ++frame)
        {
            clock.Advance(100);
            info = renderer.BuildInfo();
        }

        Assert.EndsWith($"{Label}10", info, StringComparison.Ordinal);
    }

    /// <summary>A monotonic clock the test moves by hand.</summary>
    private sealed class FakeClock
    {
        private long _nowMs;

        public void Advance(long milliseconds) => _nowMs += milliseconds;

        public SystemInfoRenderer CreateRenderer(DateTime? timeOfDay = null) => new(
            () => _nowMs,
            () => timeOfDay ?? new DateTime(2026, 11, 21, 0, 0, 0));
    }
}
