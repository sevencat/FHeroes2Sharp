using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Imaging;
using GlyphRaster;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests the text colour path: the original engine owns a single <c>FONT.ICN</c> table and derives
/// the yellow and gray faces by converting every pixel with a palette, and the TrueType provider
/// inherits the colour by converting its coverage ramp the same way. No game data is needed here.
/// </summary>
public sealed class FontPaletteTests
{
    private const char LetterA = 'A';
    private const byte GreyTextPixel = 10;
    private const float EmSize = 16f;

    [Fact]
    public void BitmapFont_WithPalette_ConvertsTheGlyphPixels()
    {
        BitmapFont font = CreateTestFont();
        BitmapFont yellow = font.WithPalette(PaletteId.YellowFont);

        Assert.Equal(114, yellow.GetSprite(LetterA).Image[0, 0]);
        Assert.Equal(PaletteId.YellowFont, yellow.Palette);
        Assert.Equal(PaletteId.Standard, font.Palette);

        // The source font keeps its own pixels; the conversion happens on a copy.
        Assert.Equal(GreyTextPixel, font.GetSprite(LetterA).Image[0, 0]);
        Assert.Contains(nameof(PaletteId.YellowFont), yellow.SourceName, StringComparison.Ordinal);
    }

    [Fact]
    public void BitmapFont_WithPalette_KeepsTheMetricsAndTheTransformLayer()
    {
        Sprite[] glyphs = new Sprite[0x5B];
        for (int index = 0; index < glyphs.Length; ++index)
        {
            glyphs[index] = new Sprite(6, 13, -1, -2);
            glyphs[index].Image.Fill(GreyTextPixel);
        }

        glyphs[LetterA].Transform![0] = 1;

        BitmapFont font = BitmapFont.Create(glyphs, BitmapFontSize.Normal, "synthetic");
        BitmapFont gray = font.WithPalette(PaletteId.GrayFont);

        Assert.Equal(font.SpaceWidth, gray.SpaceWidth);
        Assert.Equal(font.LineHeight, gray.LineHeight);
        Assert.Equal(font.GlyphCount, gray.GlyphCount);
        Assert.Equal(font.GetWidth(LetterA), gray.GetWidth(LetterA));

        Sprite source = font.GetSprite(LetterA);
        Sprite converted = gray.GetSprite(LetterA);
        Assert.Equal(source.Width, converted.Width);
        Assert.Equal(source.Height, converted.Height);
        Assert.Equal(source.OffsetX, converted.OffsetX);
        Assert.Equal(source.OffsetY, converted.OffsetY);

        // A skipped pixel stays skipped: the transform layer is not a colour.
        Assert.Equal(1, converted.Transform![0]);
        Assert.Equal(15, converted.Image[1, 0]);
    }

    [Fact]
    public void BitmapFont_WithStandardPalette_ReturnsTheSameInstance()
    {
        BitmapFont font = CreateTestFont();

        Assert.Same(font, font.WithPalette(PaletteId.Standard));
    }

    [Fact]
    public void BitmapFont_ConvertsAReusedGlyphOnlyOnce()
    {
        // BitmapFont.FromIcn fills the 32 control codes with the very same sprite instance; a
        // second conversion of that sprite would run the table twice and zero the pixels.
        Sprite shared = new(6, 13, -1, -2);
        shared.Image.Fill(GreyTextPixel);
        BitmapFont font = BitmapFont.Create(Enumerable.Repeat(shared, 0x5B).ToArray(), BitmapFontSize.Normal, "shared");

        BitmapFont yellow = font.WithPalette(PaletteId.YellowFont);

        Assert.Equal(114, yellow.GetSprite(LetterA).Image[0, 0]);
        Assert.Equal(114, yellow.GetSprite((char)0x01).Image[0, 0]);
        Assert.Same(yellow.GetSprite((char)0x01), yellow.GetSprite(LetterA));
    }

    [Fact]
    public void GlyphCoverageRamp_WithPalette_MapsTheGreyRampOntoTheYellowOne()
    {
        GlyphCoverageRamp yellow = GlyphCoverageRamp.Heroes2Text.WithPalette(PaletteId.YellowFont);

        Assert.Equal(new byte[] { 124, 121, 118, 115, 114 }, yellow.Levels.ToArray());
        Assert.Equal(GlyphCoverageRamp.Heroes2Text.MinCoverage, yellow.MinCoverage);

        // The shared instance is untouched.
        Assert.Equal(new byte[] { 25, 21, 16, 12, 10 }, GlyphCoverageRamp.Heroes2Text.Levels.ToArray());
        Assert.Same(GlyphCoverageRamp.Heroes2Text, GlyphCoverageRamp.Heroes2Text.WithPalette(PaletteId.Standard));
    }

    [Fact]
    public void GlyphCoverageRamp_WithPalette_RejectsForeignTables()
    {
        Assert.Throws<ArgumentException>(() => GlyphCoverageRamp.Heroes2Text.WithPalette(new byte[16]));
    }

    [Fact]
    public void TrueTypeGlyphs_InheritTheColourOfTheBitmapFont()
    {
        TrueTypeGlyphSource source = new(
            TextFont.BuiltIn,
            EmSize,
            GlyphCoverageRamp.Heroes2Text.WithPalette(PaletteId.YellowFont));

        Sprite glyph = source.GetSprite('中');

        for (int index = 0; index < glyph.Transform!.Length; ++index)
        {
            if (glyph.Transform[index] == 1)
            {
                continue;
            }

            // Every painted pixel comes from the yellow ramp 114..130 of KB.PAL.
            Assert.InRange(glyph.Image.Pixels[index], 114, 130);
        }
    }

    private static BitmapFont CreateTestFont()
    {
        Sprite[] glyphs = new Sprite[0x5B];
        for (int index = 0; index < glyphs.Length; ++index)
        {
            glyphs[index] = new Sprite(6, 13, -1, -2);
            glyphs[index].Image.Fill(GreyTextPixel);
        }

        return BitmapFont.Create(glyphs, BitmapFontSize.Normal, "synthetic");
    }
}
