using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <c>fadeOutDisplay()</c> / <c>fadeInDisplay()</c> (<c>src/fheroes2/gui/ui_tool.cpp:653-696</c>):
/// the frame counts, the alpha sequence of <c>fadeDisplay()</c> and the region it covers.
/// </summary>
public sealed class DisplayFadeTests
{
    private const byte WhiteId = 1;

    /// <summary>A full fade out runs six frames from one step below full bright to full dark.</summary>
    [Fact]
    public void Out_RunsSixFramesAndEndsBlack()
    {
        PixelBuffer display = BuildDisplay();
        DisplayFade fade = Assert.IsType<DisplayFade>(DisplayFade.Out(display));

        Assert.Equal(6u, fade.FrameCount);
        Assert.Equal(16, fade.DelayMs);
        Assert.Equal(213, fade.StartAlpha);
        Assert.Equal(0, fade.EndAlpha);

        double[] expected = { 213, 170.4, 127.8, 85.2, 42.6, 0 };
        List<double> alphas = new(expected.Length);

        while (!fade.IsFinished)
        {
            alphas.Add(fade.CurrentAlpha);
            Assert.True(fade.TryAdvance(BuildPalette()));
        }

        Assert.Equal(expected.Length, alphas.Count);
        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.Equal(expected[i], alphas[i], 3);
        }

        Assert.False(fade.TryAdvance(BuildPalette()));
        Assert.Equal(0, display[0, 0]);
        Assert.Equal(0, display[3, 3]);
    }

    /// <summary>A half fade out runs half the frames and stops half darkened.</summary>
    [Fact]
    public void Out_HalfFadeRunsThreeFrames()
    {
        PixelBuffer display = BuildDisplay();
        DisplayFade fade = Assert.IsType<DisplayFade>(DisplayFade.Out(display, display.Bounds, true));

        Assert.Equal(3u, fade.FrameCount);
        Assert.Equal(127, fade.EndAlpha);
        Assert.Equal(16, fade.DelayMs);

        int frames = 0;
        while (fade.TryAdvance(BuildPalette()))
        {
            ++frames;
        }

        Assert.Equal(3, frames);
    }

    /// <summary>A full fade in ends on the untouched picture: the last frame copies it back.</summary>
    [Fact]
    public void In_DarkensFirstAndRestoresTheColourOnTheLastFrame()
    {
        PixelBuffer display = BuildDisplay();
        DisplayFade fade = Assert.IsType<DisplayFade>(DisplayFade.In(display));
        Palette palette = BuildPalette();

        Assert.Equal(6u, fade.FrameCount);
        Assert.Equal(16, fade.DelayMs);
        Assert.True(fade.TryAdvance(palette));

        // The first frame is a step below full bright, so the white entry turns black.
        Assert.NotEqual(WhiteId, display[0, 0]);

        while (fade.TryAdvance(palette))
        {
        }

        Assert.Equal(WhiteId, display[0, 0]);
        Assert.Equal(WhiteId, display[3, 3]);
    }

    /// <summary>The half fade in adds one frame so that the last one is fully bright.</summary>
    [Fact]
    public void In_HalfFadeAddsAFrame()
    {
        PixelBuffer display = BuildDisplay();
        DisplayFade fade = Assert.IsType<DisplayFade>(DisplayFade.In(display, display.Bounds, true));

        Assert.Equal(4u, fade.FrameCount);
        Assert.Equal(18, fade.DelayMs);
        Assert.Equal(255, fade.EndAlpha);
    }

    /// <summary>Only the requested region changes, and it is clipped to the frame.</summary>
    [Fact]
    public void Out_ClipsTheRegionToTheFrame()
    {
        PixelBuffer display = BuildDisplay();
        DisplayFade fade = Assert.IsType<DisplayFade>(DisplayFade.Out(display, new Rect(2, 2, 3, 3), false));
        Palette palette = BuildPalette();

        Assert.Equal(new Rect(2, 2, 2, 2), fade.Roi);

        while (fade.TryAdvance(palette))
        {
        }

        Assert.Equal(0, display[2, 2]);
        Assert.Equal(0, display[3, 3]);
        Assert.Equal(WhiteId, display[0, 0]);
        Assert.Equal(WhiteId, display[1, 3]);
    }

    /// <summary>An empty region has nothing to fade.</summary>
    [Fact]
    public void Out_ReturnsNullForAnEmptyRegion()
    {
        PixelBuffer display = BuildDisplay();

        Assert.Null(DisplayFade.Out(display, new Rect(0, 0, 0, 0), false));
    }

    /// <summary>A frame painted with the white entry, everything else black.</summary>
    private static PixelBuffer BuildDisplay()
    {
        PixelBuffer display = new(4, 4);
        ImageOps.FillRect(display, display.Bounds, WhiteId);

        return display;
    }

    /// <summary>Entry 1 is near white in video values, every other entry is black.</summary>
    private static Palette BuildPalette()
    {
        byte[] raw = new byte[PaletteTable.Size * 3];
        raw[WhiteId * 3] = 252;
        raw[(WhiteId * 3) + 1] = 252;
        raw[(WhiteId * 3) + 2] = 252;

        return new Palette(raw);
    }
}
