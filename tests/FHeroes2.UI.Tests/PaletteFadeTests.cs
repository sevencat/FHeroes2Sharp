using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <c>FadeDisplayWithPalette()</c>, <c>InvertedFadeWithPalette()</c> and <c>InvertedShadow()</c>
/// (<c>src/fheroes2/gui/ui_tool.cpp:698-764</c>).
/// </summary>
public sealed class PaletteFadeTests
{
    private const byte SourceId = 40;

    /// <summary>The four parts around the excluded region are transformed, the region itself is not.</summary>
    [Fact]
    public void InvertedShadow_LeavesTheExcludedRegionAlone()
    {
        PixelBuffer image = new(10, 10);
        ImageOps.FillRect(image, image.Bounds, SourceId);

        InvertedShadow.Apply(image, new Rect(0, 0, 10, 10), new Rect(3, 3, 4, 4), PaletteId.Darkening, 1);

        byte darkened = PaletteTable.Apply(PaletteId.Darkening, SourceId);
        Assert.Equal(darkened, image[0, 0]);
        Assert.Equal(darkened, image[9, 9]);
        Assert.Equal(darkened, image[2, 5]);
        Assert.Equal(darkened, image[7, 5]);
        Assert.Equal(SourceId, image[4, 4]);
    }

    /// <summary>Two passes darken twice, which is how the fade gains depth.</summary>
    [Fact]
    public void InvertedShadow_AppliesTheTransformAsOftenAsAsked()
    {
        PixelBuffer image = new(4, 4);
        ImageOps.FillRect(image, image.Bounds, SourceId);

        InvertedShadow.Apply(image, new Rect(0, 0, 4, 4), new Rect(1, 1, 2, 2), PaletteId.Darkening, 2);

        byte once = PaletteTable.Apply(PaletteId.Darkening, SourceId);
        byte twice = PaletteTable.Apply(PaletteId.Darkening, once);
        Assert.Equal(twice, image[0, 0]);
    }

    /// <summary>A zero sized part is skipped instead of touching the pixels next to it.</summary>
    [Fact]
    public void InvertedShadow_SkipsDegenerateParts()
    {
        PixelBuffer image = new(6, 6);
        ImageOps.FillRect(image, image.Bounds, SourceId);

        // The excluded region touches the top edge, so the part above it has no height.
        InvertedShadow.Apply(image, new Rect(0, 0, 6, 6), new Rect(1, 0, 4, 6), PaletteId.Darkening, 1);

        Assert.Equal(PaletteTable.Apply(PaletteId.Darkening, SourceId), image[0, 0]);
        Assert.Equal(SourceId, image[2, 2]);
    }

    /// <summary>The flash draws the tinted image for every frame it was given.</summary>
    [Fact]
    public void FadeDisplayWithPalette_DrawsEveryFrame()
    {
        PixelBuffer display = new(8, 8);
        PixelBuffer top = new(4, 4);
        ImageOps.FillRect(top, top.Bounds, SourceId);

        DisplayFadeWithPalette flash = new(display, top, new Point(2, 2), PaletteId.Darkening, 120, 4);

        Assert.Equal(new Rect(2, 2, 4, 4), flash.Roi);
        Assert.Equal(30, flash.DelayMs);

        int frames = 0;
        while (flash.TryAdvance())
        {
            ++frames;
        }

        Assert.Equal(4, frames);

        // The reference re-transforms the saved copy on every frame, so the tint accumulates.
        byte expected = SourceId;
        for (int i = 0; i < 4; ++i)
        {
            expected = PaletteTable.Apply(PaletteId.Darkening, expected);
        }

        Assert.Equal(expected, display[2, 2]);
        Assert.Equal(expected, display[5, 5]);
        Assert.Equal(0, display[0, 0]);
    }

    /// <summary>The inverted fade shades once per frame and never touches the window.</summary>
    [Fact]
    public void InvertedFade_ShadesOncePerFrame()
    {
        PixelBuffer image = new(8, 8);
        ImageOps.FillRect(image, image.Bounds, SourceId);

        InvertedFade fade = new(image, new Rect(0, 0, 8, 8), new Rect(2, 2, 4, 4), PaletteId.Darkening, 90, 3);
        Assert.Equal(30, fade.DelayMs);

        int frames = 0;
        while (fade.TryAdvance())
        {
            ++frames;
        }

        Assert.Equal(3, frames);

        byte once = PaletteTable.Apply(PaletteId.Darkening, SourceId);
        byte twice = PaletteTable.Apply(PaletteId.Darkening, once);
        byte thrice = PaletteTable.Apply(PaletteId.Darkening, twice);
        Assert.Equal(thrice, image[0, 0]);
        Assert.Equal(SourceId, image[3, 3]);
    }
}
