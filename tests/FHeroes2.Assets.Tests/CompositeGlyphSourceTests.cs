using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using GlyphRaster;
using Xunit.Abstractions;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests the provider the UI draws with: a bitmap font that owns the layout and a TrueType face
/// that fills in the characters the ICN tables cannot express. The bundled face ships inside the
/// rasterizer package, so every test except the archive one runs without game data.
/// </summary>
public sealed class CompositeGlyphSourceTests
{
    private const char LetterA = 'A';
    private const char LetterN = 'N';
    private const char ChineseCharacter = '中';
    private const char UnmappedCharacter = '\uE000';
    private const byte GreyTextPixel = 10;

    /// <summary>The lowest index of the yellow text ramp of <c>KB.PAL</c>.</summary>
    private const byte FirstYellowIndex = 114;

    /// <summary>The highest index of the yellow text ramp of <c>KB.PAL</c>.</summary>
    private const byte LastYellowIndex = 130;

    private static readonly TextFont Face = TextFont.BuiltIn;

    private readonly ITestOutputHelper _output;

    public CompositeGlyphSourceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(BitmapFontSize.Small, 4, 11)]
    [InlineData(BitmapFontSize.Normal, 6, 17)]
    public void Metrics_ComeFromTheBitmapFont(BitmapFontSize size, int spaceWidth, int lineHeight)
    {
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(CreateTestFont(size), Face);

        Assert.Equal(spaceWidth, source.SpaceWidth);
        Assert.Equal(lineHeight, source.LineHeight);
        Assert.Equal(lineHeight, source.Fallback.LineHeight);
        Assert.True(((TrueTypeGlyphSource)source.Fallback).EmSize > 0f);
    }

    [Fact]
    public void LatinCharacter_ComesFromTheBitmapFont()
    {
        BitmapFont font = CreateTestFont();
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(font, Face);

        Assert.True(source.IsAvailable(LetterA));
        Assert.True(source.TryGetGlyph(LetterA, out Sprite glyph));

        // Not the rasterized letter: the original ICN sprite, so Latin text stays pixel identical.
        Assert.Same(font.GetSprite(LetterA), glyph);
        Assert.Equal(font.GetWidth(LetterA), source.GetAdvance(LetterA));
    }

    [Fact]
    public void ChineseCharacter_ComesFromTheFallback()
    {
        BitmapFont font = CreateTestFont();
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(font, Face);

        Assert.False(font.IsAvailable(ChineseCharacter));
        Assert.True(source.IsAvailable(ChineseCharacter));
        Assert.True(source.TryGetGlyph(ChineseCharacter, out Sprite glyph));

        TrueTypeGlyphSource fallback = (TrueTypeGlyphSource)source.Fallback;
        Assert.Same(fallback.GetSprite(ChineseCharacter), glyph);
        Assert.Equal(fallback.GetAdvance(ChineseCharacter), source.GetAdvance(ChineseCharacter));
    }

    [Fact]
    public void Space_KeepsTheBitmapAdvance()
    {
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(CreateTestFont(), Face);

        Assert.True(source.IsAvailable(' '));
        Assert.Equal(6, source.GetAdvance(' '));

        // A space has no sprite in either provider; the composite mirrors the bitmap contract.
        Assert.False(source.TryGetGlyph(' ', out _));
        Assert.Equal(0, source.GetAdvance(GlyphText.LineSeparator));
        Assert.True(source.IsAvailable(GlyphText.LineSeparator));
    }

    [Fact]
    public void UnmappedCharacter_YieldsTheBitmapSubstitute()
    {
        // U+E000 is a private use code point: the bitmap table cannot express it and the face does
        // not map it, so the composite ends on the glyph the original game would have printed.
        BitmapFont font = CreateTestFont();
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(font, Face);

        Assert.False(source.IsAvailable(UnmappedCharacter));
        Assert.False(source.TryGetGlyph(UnmappedCharacter, out Sprite glyph));
        Assert.Same(font.GetSprite('?'), glyph);
        Assert.Equal(font.GetWidth('?'), source.GetAdvance(UnmappedCharacter));
    }

    [Fact]
    public void ForBitmapFont_InheritsTheColourOfTheBitmapFont()
    {
        BitmapFont yellow = CreateTestFont().WithPalette(PaletteId.YellowFont);
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(yellow, Face);

        Assert.Equal(PaletteId.YellowFont, yellow.Palette);

        // Latin: the recoloured ICN sprite.
        Assert.True(source.TryGetGlyph(LetterA, out Sprite latin));
        Assert.Equal(FirstYellowIndex, latin.Image[0, 0]);

        // CJK: the rasterized glyph lands on the same ramp, so both halves of a mixed string match.
        Assert.True(source.TryGetGlyph(ChineseCharacter, out Sprite chinese));
        Assert.All(PaintedPixels(chinese), index => Assert.InRange(index, FirstYellowIndex, LastYellowIndex));
    }

    [Fact]
    public void ForBitmapFont_KeepsTheDefaultGreyColour()
    {
        BitmapFont grey = CreateTestFont();
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(grey, Face);

        Assert.Equal(PaletteId.Standard, grey.Palette);
        Assert.Equal(
            new byte[] { 25, 21, 16, 12, 10 }, ((TrueTypeGlyphSource)source.Fallback).Ramp.Levels.ToArray());

        Assert.True(source.TryGetGlyph(ChineseCharacter, out Sprite chinese));
        Assert.All(PaintedPixels(chinese), index => Assert.InRange(index, 10, 36));
    }

    [Fact]
    public void ForBitmapFont_UsesAnExplicitRamp()
    {
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(
            CreateTestFont(), Face, new GlyphCoverageRamp([7], minCoverage: 128));

        Assert.True(source.TryGetGlyph(ChineseCharacter, out Sprite chinese));

        // A single level with a high threshold is the hard edged variant some sizes look better with.
        Assert.All(PaintedPixels(chinese), index => Assert.Equal(7, index));
    }

    [Fact]
    public void Constructors_RejectMissingProviders()
    {
        Assert.Throws<ArgumentNullException>(() => new CompositeGlyphSource(null!, CreateTestFont()));
        Assert.Throws<ArgumentNullException>(() => new CompositeGlyphSource(CreateTestFont(), null!));
        Assert.Throws<ArgumentNullException>(() => CompositeGlyphSource.ForBitmapFont(null!, Face));
        Assert.Throws<ArgumentNullException>(() => CompositeGlyphSource.ForBitmapFont(CreateTestFont(), null!));
    }

    [Fact]
    public void DrawLine_MixesBothProvidersOnOneLine()
    {
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(CreateTestFont(), Face);
        PixelBuffer target = new(96, 18);

        const string Text = "英雄 A";
        int endX = GlyphText.DrawLine(source, target, new Point(2, 0), Text);

        _output.WriteLine($"'{Text}' ends at {endX}, width {GlyphText.Measure(source, Text)}");

        Assert.Equal(2 + GlyphText.Measure(source, Text), endX);
        Assert.True(target.Pixels.Count(pixel => pixel != 0) > 20);

        // Two full width characters are wider than two Latin ones: that is the advance the fallback
        // has to contribute for the layout to stay right.
        Assert.True(GlyphText.Measure(source, "英雄") > GlyphText.Measure(source, "AA"));

        // The whole line stays inside one 17 pixel cell.
        for (int x = 0; x < target.Width; ++x)
        {
            Assert.Equal(0, target[x, 17]);
        }
    }

    [Fact]
    public void ArchiveFonts_CombineTheBitmapTablesWithTheCjkFace()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            _output.WriteLine("Game data is not available; skipping.");
            return;
        }

        using AssetBundle bundle = AssetBundle.Open(dataDirectory);

        BitmapFont grey = BitmapFont.Load(bundle, BitmapFontSize.Normal);
        BitmapFont yellow = BitmapFont.Load(bundle, BitmapFontSize.Normal, PaletteId.YellowFont);
        CompositeGlyphSource source = CompositeGlyphSource.ForBitmapFont(yellow, Face);

        Sprite greyGlyph = grey.GetSprite(LetterN);
        Sprite yellowGlyph = yellow.GetSprite(LetterN);

        TrueTypeGlyphSource fallback = (TrueTypeGlyphSource)source.Fallback;
        _output.WriteLine(
            $"{source.LineHeight} px line, '{LetterN}' advance {source.GetAdvance(LetterN)}, " +
            $"'新' advance {source.GetAdvance('新')}, fallback em size {fallback.EmSize:0.##}, " +
            $"ascent {fallback.Ascent}, family {fallback.FamilyName}");

        Assert.Equal(17, source.LineHeight);
        Assert.True(source.IsAvailable('新'));
        Assert.False(grey.IsAvailable('新'));

        // Same sprite geometry, converted through YELLOW_FONT.
        Assert.Equal(greyGlyph.Image.Pixels.Length, yellowGlyph.Image.Pixels.Length);
        Assert.False(greyGlyph.Image.Pixels.SequenceEqual(yellowGlyph.Image.Pixels));

        PixelBuffer target = new(320, 40);
        const string Text = "新游戏 New Game";
        int endX = GlyphText.DrawLine(source, target, new Point(4, 4), Text);

        Assert.Equal(4 + GlyphText.Measure(source, Text), endX);
        Assert.True(target.Pixels.Count(pixel => pixel != 0) > 50);
    }

    private static BitmapFont CreateTestFont(BitmapFontSize size = BitmapFontSize.Normal, byte color = GreyTextPixel)
    {
        Sprite[] glyphs = new Sprite[0x5B];
        for (int index = 0; index < glyphs.Length; ++index)
        {
            glyphs[index] = new Sprite(6, 13, -1, -2);
            glyphs[index].Image.Fill(color);
        }

        return BitmapFont.Create(glyphs, size, "synthetic");
    }

    private static byte[] PaintedPixels(Sprite sprite)
    {
        byte[]? transform = sprite.Transform;
        List<byte> painted = [];

        for (int index = 0; index < sprite.Image.Pixels.Length; ++index)
        {
            if (transform is null || transform[index] == 0)
            {
                painted.Add(sprite.Image.Pixels[index]);
            }
        }

        return [.. painted];
    }
}
