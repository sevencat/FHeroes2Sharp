using FHeroes2.Core.Imaging;
using Xunit;

namespace FHeroes2.Core.Tests;

/// <summary>
/// The nearest game palette entry, <c>GetPALColorId()</c> (<c>src/engine/image.cpp:419-437</c>):
/// the 227 candidate entries, the Redmean distance and the step onto the no-cycle table.
/// </summary>
public sealed class PALColorIdTests
{
    /// <summary>An exact colour finds its own entry.</summary>
    [Fact]
    public void Get_FindsTheExactEntry()
    {
        Palette palette = BuildPalette((40, 252, 0, 0));

        Assert.Equal(40, PALColorId.Get(63, 0, 0, palette));
    }

    /// <summary>Cycling entries come back as their static copy, exactly as the reference's transform layer 15.</summary>
    [Fact]
    public void Get_MapsACyclingEntryOntoItsStaticCopy()
    {
        Palette palette = BuildPalette((231, 252, 0, 0));

        Assert.Equal(PaletteTable.Apply(PaletteId.NoCycle, 231), PALColorId.Get(63, 0, 0, palette));
    }

    /// <summary>Of two close colours the nearer one wins.</summary>
    [Fact]
    public void Get_PicksTheNearerOfTwoEntries()
    {
        Palette palette = BuildPalette((21, 252, 252, 252));

        // Every other candidate is black, so a dark grey goes to the black entry and a light one to entry 21.
        Assert.Equal(PALColorId.Get(0, 0, 0, palette), PALColorId.Get(10, 10, 10, palette));
        Assert.Equal(PaletteTable.Apply(PaletteId.NoCycle, 21), PALColorId.Get(60, 60, 60, palette));
    }

    /// <summary>The undefined entries 0..9 and 254..255 are never returned, even when they match exactly.</summary>
    [Fact]
    public void Get_NeverReturnsAnUndefinedEntry()
    {
        (int, byte, byte, byte)[] undefined =
        {
            (0, 252, 0, 0), (1, 252, 0, 0), (2, 252, 0, 0), (3, 252, 0, 0), (4, 252, 0, 0),
            (5, 252, 0, 0), (6, 252, 0, 0), (7, 252, 0, 0), (8, 252, 0, 0), (9, 252, 0, 0),
            (254, 252, 0, 0), (255, 252, 0, 0),
        };

        byte result = PALColorId.Get(63, 0, 0, BuildPalette(undefined));

        Assert.InRange(result, 10, 253);
    }

    /// <summary>The channels are six bit values, so anything above 63 is a caller bug.</summary>
    [Fact]
    public void Get_RejectsChannelsAboveSixBits()
    {
        Palette palette = BuildPalette((40, 252, 0, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => PALColorId.Get(64, 0, 0, palette));
        Assert.Throws<ArgumentOutOfRangeException>(() => PALColorId.Get(0, 64, 0, palette));
        Assert.Throws<ArgumentOutOfRangeException>(() => PALColorId.Get(0, 0, 64, palette));
    }

    /// <summary>A palette of the wrong size is refused.</summary>
    [Fact]
    public void Get_RejectsATruncatedPalette()
    {
        Assert.Throws<ArgumentException>(() => PALColorId.Get(0, 0, 0, new byte[6]));
    }

    /// <summary>Builds a game palette in video values with the given entries set.</summary>
    private static Palette BuildPalette(params (int Id, byte Red, byte Green, byte Blue)[] colors)
    {
        byte[] raw = new byte[PaletteTable.Size * 3];

        foreach ((int id, byte red, byte green, byte blue) in colors)
        {
            raw[id * 3] = red;
            raw[(id * 3) + 1] = green;
            raw[(id * 3) + 2] = blue;
        }

        return new Palette(raw);
    }
}
