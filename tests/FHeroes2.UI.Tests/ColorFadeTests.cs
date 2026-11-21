using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The grayscale fade of <c>colorFade()</c> (<c>src/fheroes2/gui/ui_tool.cpp:395-483</c>): the search over
/// entries 10..36, the step count, the integer blend and the final remap of the frame.
/// </summary>
public sealed class ColorFadeTests
{
    private const int GrayscaleCount = ColorFade.EndGrayScaleColorId - ColorFade.StartGrayScaleColorId + 1;

    /// <summary>Every palette entry maps to the closest grayscale entry of the game palette.</summary>
    [Fact]
    public void ColorMap_PicksTheNearestGrayscaleEntry()
    {
        Palette game = BuildGrayscaleGamePalette();
        byte[] screen = new byte[Palette.RawPaletteSize];

        // Palette entry 5 is exactly the grayscale entry 20, entry 7 is nearly entry 30.
        CopyColor(game, 20, screen, 5);
        CopyColor(game, 30, screen, 7);
        screen[(7 * 3) + 2] = (byte)Math.Min(255, screen[(7 * 3) + 2] + 3);

        ColorFade fade = new(new Palette(screen), game, new Rect(0, 0, 4, 4), 100, 25);

        Assert.Equal(20, fade.ColorMap[5]);
        Assert.Equal(30, fade.ColorMap[7]);
    }

    /// <summary>The fade is split into <c>duration / round( 1000 / fps )</c> steps.</summary>
    [Fact]
    public void StepCount_ComesFromTheDurationAndTheFrameRate()
    {
        ColorFade fade = new(Palette.FromRaw(new byte[Palette.RawPaletteSize]), BuildGrayscaleGamePalette(),
            new Rect(0, 0, 4, 4), 500, 25);

        Assert.Equal(40, fade.DelayMs);
        Assert.Equal(12, fade.StepCount);
    }

    /// <summary>The last step is never rendered: twelve steps hand out eleven palettes.</summary>
    [Fact]
    public void TryAdvance_StopsOneStepShortOfTheEnd()
    {
        ColorFade fade = new(Palette.FromRaw(new byte[Palette.RawPaletteSize]), BuildGrayscaleGamePalette(),
            new Rect(0, 0, 4, 4), 500, 25);

        int rendered = 0;
        while (fade.TryAdvance(out _))
        {
            ++rendered;
        }

        Assert.Equal(fade.StepCount - 1, rendered);
        Assert.True(fade.IsFinished);
    }

    /// <summary>The first step blends <c>( start * ( steps - 1 ) + end ) / steps</c>.</summary>
    [Fact]
    public void TryAdvance_BlendsThePalettesWithIntegerArithmetic()
    {
        Palette game = BuildGrayscaleGamePalette();
        byte[] screen = new byte[Palette.RawPaletteSize];
        CopyColor(game, 0, screen, 0);

        ColorFade fade = new(new Palette(screen), game, new Rect(0, 0, 4, 4), 500, 25);

        Assert.True(fade.TryAdvance(out Palette grading));

        int expectedRed = (screen[0] * (fade.StepCount - 1) + 0) / fade.StepCount;
        Assert.Equal((byte)expectedRed, grading.ToRaw()[0]);
    }

    /// <summary>A zero duration renders nothing and only remaps the frame.</summary>
    [Fact]
    public void TryAdvance_WithAZeroDurationIsAlreadyOver()
    {
        ColorFade fade = new(Palette.FromRaw(new byte[Palette.RawPaletteSize]), BuildGrayscaleGamePalette(),
            new Rect(0, 0, 4, 4), 0, 25);

        Assert.Equal(0, fade.StepCount);
        Assert.True(fade.IsFinished);
        Assert.False(fade.TryAdvance(out _));
    }

    /// <summary>Completing recolours the region of interest and leaves the rest of the frame alone.</summary>
    [Fact]
    public void Complete_RemapsOnlyTheRegionOfInterest()
    {
        Palette game = BuildGrayscaleGamePalette();
        byte[] screen = new byte[Palette.RawPaletteSize];
        CopyColor(game, 20, screen, 3);

        ColorFade fade = new(new Palette(screen), game, new Rect(1, 1, 2, 2), 100, 25);
        PixelBuffer frame = new(4, 4);
        for (int y = 0; y < 4; ++y)
        {
            for (int x = 0; x < 4; ++x)
            {
                frame[x, y] = 3;
            }
        }

        fade.Complete(frame);

        Assert.Equal((byte)20, frame[1, 1]);
        Assert.Equal((byte)20, frame[2, 2]);
        Assert.Equal((byte)3, frame[0, 0]);
        Assert.Equal((byte)3, frame[3, 3]);
    }

    /// <summary>A game palette whose entries 10..36 are a ramp from black to white.</summary>
    private static Palette BuildGrayscaleGamePalette()
    {
        byte[] raw = new byte[Palette.RawPaletteSize];

        for (int step = 0; step < GrayscaleCount; ++step)
        {
            byte value = (byte)(step * 255 / (GrayscaleCount - 1));
            int index = (ColorFade.StartGrayScaleColorId + step) * 3;
            raw[index] = value;
            raw[index + 1] = value;
            raw[index + 2] = value;
        }

        return new Palette(raw);
    }

    private static void CopyColor(Palette source, int sourceId, byte[] target, int targetId)
    {
        byte[] raw = source.ToRaw();
        Array.Copy(raw, sourceId * 3, target, targetId * 3, 3);
    }
}
