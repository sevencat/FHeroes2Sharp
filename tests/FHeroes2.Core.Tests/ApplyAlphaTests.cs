using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// <c>fheroes2::ApplyAlpha()</c> (<c>src/engine/image.cpp:1153-1168</c>): darken every palette entry by
/// <c>alpha / 255</c>, look the dark colour up in the game palette and recolour the source through that table.
/// </summary>
public sealed class ApplyAlphaTests
{
    private const int RedId = 40;
    private const int BlackId = 20;

    /// <summary>A full alpha keeps the colour of an entry that exists in the palette.</summary>
    [Fact]
    public void ApplyAlpha_WithFullAlphaKeepsTheColour()
    {
        Palette palette = BuildPalette();
        PixelBuffer source = new(2, 2);
        PixelBuffer target = new(2, 2);
        ImageOps.FillRect(source, source.Bounds, RedId);

        ImageOps.ApplyAlpha(source, source.Bounds, target, new Point(0, 0), 255, palette);

        Assert.Equal(RedId, target[0, 0]);
        Assert.Equal(RedId, target[1, 1]);
    }

    /// <summary>A zero alpha sends every defined entry to black.</summary>
    [Fact]
    public void ApplyAlpha_WithZeroAlphaMakesTheFrameBlack()
    {
        Palette palette = BuildPalette();
        PixelBuffer source = new(2, 2);
        PixelBuffer target = new(2, 2);
        ImageOps.FillRect(source, source.Bounds, RedId);

        ImageOps.ApplyAlpha(source, source.Bounds, target, new Point(0, 0), 0, palette);

        Assert.Equal(PALColorId.Get(0, 0, 0, palette), target[0, 0]);
    }

    /// <summary>The darkening happens on the six bit colour before the lookup.</summary>
    [Fact]
    public void ApplyAlpha_DarkensTheColourBeforeTheLookup()
    {
        Palette palette = BuildPalette();
        PixelBuffer source = new(1, 1);
        PixelBuffer target = new(1, 1);
        source[0, 0] = RedId;

        ImageOps.ApplyAlpha(source, source.Bounds, target, new Point(0, 0), 128, palette);

        byte expected = PALColorId.Get(63 * 128 / 255, 0, 0, palette);
        Assert.Equal(expected, target[0, 0]);
    }

    /// <summary>The undefined palette entries point at index zero, which is black in every game palette.</summary>
    [Fact]
    public void ApplyAlpha_SendsUndefinedEntriesToIndexZero()
    {
        Palette palette = BuildPalette();
        PixelBuffer source = new(1, 2);
        PixelBuffer target = new(1, 2);
        source[0, 0] = 5;
        source[0, 1] = 255;

        ImageOps.ApplyAlpha(source, source.Bounds, target, new Point(0, 0), 128, palette);

        Assert.Equal(0, target[0, 0]);
        Assert.Equal(0, target[0, 1]);
    }

    /// <summary>Only the destination region changes, and a partially visible source is clipped, not shifted.</summary>
    [Fact]
    public void ApplyAlpha_WritesOnlyTheDestinationRegion()
    {
        Palette palette = BuildPalette();
        PixelBuffer source = new(4, 4);
        PixelBuffer target = new(4, 4);
        ImageOps.FillRect(source, source.Bounds, RedId);
        ImageOps.FillRect(target, target.Bounds, BlackId);

        // Take the bottom right 3x3 of the source, place its top left corner on (2, 2): only 2x2 fits.
        ImageOps.ApplyAlpha(source, new Rect(1, 1, 3, 3), target, new Point(2, 2), 255, palette);

        Assert.Equal(RedId, target[2, 2]);
        Assert.Equal(RedId, target[3, 3]);
        Assert.Equal(BlackId, target[1, 1]);
        Assert.Equal(BlackId, target[0, 0]);
    }

    /// <summary>A palette whose entry 40 is pure red and entry 20 is black, everything else black as well.</summary>
    private static Palette BuildPalette()
    {
        byte[] raw = new byte[PaletteTable.Size * 3];

        raw[RedId * 3] = 252;
        raw[BlackId * 3] = 0;

        return new Palette(raw);
    }
}
