using System.Security.Cryptography;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Core.Tests;

/// <summary>
/// The recolouring palettes are copied from the reference engine, so these tests double as a
/// provenance check: the SHA-256 pins the exact bytes of the literals in fheroes2
/// <c>src/engine/pal.cpp</c>. The value assertions pin the meaning of the two tables the UI is
/// built on, the yellow and the gray text palette.
/// </summary>
public sealed class PaletteTableTests
{
    private const string ExpectedSha256 = "4DB8D4E137B069B4F964D8A9C58A97CD827BCDC44A670DB203B6727A6C81FA3C";

    [Fact]
    public void All_HoldsOneTablePerPaletteId()
    {
        Assert.Equal(PaletteTable.Count, Enum.GetValues<PaletteId>().Length);
        Assert.Equal(PaletteTable.Count * PaletteTable.Size, PaletteTable.All.Length);
    }

    [Fact]
    public void Data_MatchesTheReferencePaletteTables()
    {
        string actual = Convert.ToHexString(SHA256.HashData(PaletteTable.All));

        Assert.Equal(ExpectedSha256, actual);
    }

    [Fact]
    public void Standard_IsTheIdentity()
    {
        ReadOnlySpan<byte> table = PaletteTable.Get(PaletteId.Standard);

        for (int index = 0; index < PaletteTable.Size; ++index)
        {
            Assert.Equal((byte)index, table[index]);
        }
    }

    [Fact]
    public void YellowFont_TurnsTheGreyTextRampIntoTheYellowOne()
    {
        // The bitmap font is always FONT.ICN; the yellow face is that table with its grey ramp
        // 10..36 replaced by the yellow ramp 114..130.
        Assert.Equal(114, PaletteTable.Apply(PaletteId.YellowFont, 10));
        Assert.Equal(130, PaletteTable.Apply(PaletteId.YellowFont, 36));

        // The yellow ramp has fewer steps than the grey one, so two grey entries may collapse onto
        // the same yellow entry; the conversion must still never move backwards.
        byte previous = 0;
        for (int index = 10; index <= 36; ++index)
        {
            byte current = PaletteTable.Apply(PaletteId.YellowFont, (byte)index);

            Assert.InRange(current, 114, 130);
            Assert.True(current >= previous, $"Grey {index} mapped to {current} after {previous}.");
            previous = current;
        }

        // The faintest step of GlyphCoverageRamp.Heroes2Text is a mid ramp entry, not an endpoint.
        Assert.Equal(124, PaletteTable.Apply(PaletteId.YellowFont, 25));
    }

    [Fact]
    public void WhiteFont_KeepsTheGreyTextRamp()
    {
        for (int index = 10; index <= 36; ++index)
        {
            Assert.Equal((byte)index, PaletteTable.Apply(PaletteId.WhiteFont, (byte)index));
        }

        // Outside the text ramp the table clears the entry, which is what makes it a font palette.
        Assert.Equal(0, PaletteTable.Apply(PaletteId.WhiteFont, 200));
    }

    [Fact]
    public void GrayFont_AndDarkening_MoveTheRampTowardsTheDarkerEntries()
    {
        // Higher indices are darker in KB.PAL, so both tables darken their input.
        Assert.Equal(15, PaletteTable.Apply(PaletteId.GrayFont, 10));
        Assert.Equal(16, PaletteTable.Apply(PaletteId.Darkening, 10));
    }

    [Fact]
    public void Get_RejectsUnknownIds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaletteTable.Get((PaletteId)PaletteTable.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaletteTable.Get((PaletteId)(-1)));
    }

    [Fact]
    public void Combine_AppliesBothTablesInOrder()
    {
        Assert.Equal(PaletteTable.Get(PaletteId.YellowFont).ToArray(), PaletteTable.Combine(PaletteId.Standard, PaletteId.YellowFont));

        // A disabled entry is gray and then darkened, the order the reference engine uses.
        byte[] combined = PaletteTable.Combine(PaletteId.GrayFont, PaletteId.Darkening);

        for (int index = 0; index < PaletteTable.Size; ++index)
        {
            Assert.Equal(
                PaletteTable.Apply(PaletteId.Darkening, PaletteTable.Apply(PaletteId.GrayFont, (byte)index)),
                combined[index]);
        }
    }

    [Fact]
    public void Combine_RejectsTablesOfTheWrongSize()
    {
        Assert.Throws<ArgumentException>(() => PaletteTable.Combine(new byte[16], PaletteTable.Get(PaletteId.GrayFont)));
        Assert.Throws<ArgumentException>(() => PaletteTable.Combine(PaletteTable.Get(PaletteId.GrayFont), new byte[16]));
    }

    [Fact]
    public void ApplyPalette_ConvertsEveryPixelOfAnImage()
    {
        PixelBuffer buffer = new(3, 2);
        buffer.Fill(10);

        ImageOps.ApplyPalette(buffer, PaletteId.YellowFont);

        Assert.All(buffer.Pixels.ToArray(), pixel => Assert.Equal(114, pixel));
    }

    [Fact]
    public void ApplyPalette_RejectsTablesOfTheWrongSize()
    {
        PixelBuffer buffer = new(1, 1);

        Assert.Throws<ArgumentException>(() => ImageOps.ApplyPalette(buffer, new byte[16]));
    }
}
