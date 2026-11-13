using System.Security.Cryptography;
using FHeroes2.Assets.Agg;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using Xunit.Abstractions;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests the bitmap font metrics and drawing rules ported from
/// <c>fheroes2::FontCharHandler</c> / <c>renderSingleLine()</c>. The synthetic glyphs pin the
/// arithmetic; the archive tests pin the real <c>FONT.ICN</c> and <c>SMALFONT.ICN</c> tables.
/// </summary>
public sealed class BitmapFontTests
{
    private const char LetterA = 'A';
    private const char LetterB = 'B';

    private readonly ITestOutputHelper _output;

    public BitmapFontTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(BitmapFontSize.Small, 4, 11)]
    [InlineData(BitmapFontSize.Normal, 6, 17)]
    [InlineData(BitmapFontSize.Large, 8, 33)]
    public void Metrics_MatchTheReferenceEngine(BitmapFontSize size, int spaceWidth, int lineHeight)
    {
        BitmapFont font = CreateTestFont(size);

        Assert.Equal(spaceWidth, font.SpaceWidth);
        Assert.Equal(lineHeight, font.LineHeight);
        Assert.Equal(spaceWidth, font.GetWidth(' '));
        Assert.Equal(0, font.GetWidth('\n'));
    }

    [Fact]
    public void GetWidth_AddsTheSpriteOffset()
    {
        BitmapFont font = CreateTestFont();

        // Every synthetic glyph is 6 pixels wide with an offset of -1.
        Assert.Equal(5, font.GetWidth(LetterA));
        Assert.Equal(5, font.GetWidth(LetterB));
    }

    [Fact]
    public void UnsupportedCharacters_FallBackToQuestionMark()
    {
        BitmapFont font = CreateTestFont();
        Sprite questionMark = font.GetSprite('?');

        Assert.True(font.IsAvailable(' '));
        Assert.True(font.IsAvailable('\n'));
        Assert.True(font.IsAvailable(LetterA));
        Assert.False(font.IsAvailable((char)0x01));

        // Control codes and anything above the single byte range are not representable.
        Assert.Same(questionMark, font.GetSprite((char)0x01));
        Assert.Same(questionMark, font.GetSprite('\u4E2D'));
        Assert.NotSame(questionMark, font.GetSprite(LetterA));
    }

    [Fact]
    public void Measure_MatchesGetLineWidth()
    {
        BitmapFont font = CreateTestFont();

        Assert.Equal(0, font.Measure(string.Empty));
        Assert.Equal(10, font.Measure("AB"));

        // A space is 6 pixels wide between two glyphs.
        Assert.Equal(16, font.Measure("A B"));

        // Trailing spaces are dropped unless explicitly kept.
        Assert.Equal(5, font.Measure("A "));
        Assert.Equal(11, font.Measure("A ", keepTrailingSpaces: true));

        // A line separator contributes nothing to the width of a single line.
        Assert.Equal(10, font.Measure("A\nB"));
    }

    [Fact]
    public void DrawText_PlacesGlyphsAtThePenPositionPlusTheSpriteOffset()
    {
        BitmapFont font = CreateTestFont();
        PixelBuffer target = new(32, 24);

        int endX = font.DrawText(target, new Point(4, 6), "A");

        // The pen advances by sprite.OffsetX + sprite.Width; the glyph starts at the offset.
        Assert.Equal(9, endX);
        Assert.NotEqual(0, target[3, 4]);
        Assert.NotEqual(0, target[8, 16]);
        Assert.Equal(0, target[2, 4]);
        Assert.Equal(0, target[9, 4]);
        Assert.Equal(0, target[3, 17]);
    }

    [Fact]
    public void DrawText_ClipsToTheProvidedArea()
    {
        BitmapFont font = CreateTestFont();
        PixelBuffer target = new(32, 24);

        font.DrawText(target, new Point(4, 6), "A", new Rect(0, 0, 5, 8));

        // The glyph covers x 3..8 and y 4..16; the area keeps only x 3..4 and y 4..7.
        Assert.NotEqual(0, target[3, 4]);
        Assert.NotEqual(0, target[4, 7]);
        Assert.Equal(0, target[5, 4]);
        Assert.Equal(0, target[3, 8]);
    }

    [Fact]
    public void DrawMultiline_UsesTheLineHeightBetweenLines()
    {
        BitmapFont font = CreateTestFont();
        PixelBuffer target = new(64, 64);

        Size area = font.DrawMultiline(target, new Point(2, 3), "A\nB");

        Assert.Equal(5, area.Width);
        Assert.Equal(2 * font.LineHeight, area.Height);

        // The first line starts at y = 3 + OffsetY, the second one a line height below it.
        Assert.NotEqual(0, target[1, 1]);
        Assert.NotEqual(0, target[1, 3 + font.LineHeight - 2]);
        Assert.Equal(0, target[1, 3 + font.LineHeight - 3]);
    }

    [Fact]
    public void DrawMultiline_WrapsAtSpaces()
    {
        BitmapFont font = CreateTestFont();
        PixelBuffer target = new(64, 64);

        // "AB" is 10 pixels wide and "AB C" would need 21, so the limit of 11 wraps after "AB".
        Size area = font.DrawMultiline(target, new Point(4, 4), "AB CD", 11);
        Sprite glyph = font.GetSprite(LetterA);

        Assert.Equal(10, area.Width);
        Assert.Equal(2 * font.LineHeight, area.Height);
        Assert.NotEqual(0, target[4 + glyph.OffsetX, 4 + glyph.OffsetY]);
        Assert.NotEqual(0, target[4 + glyph.OffsetX, 4 + font.LineHeight + glyph.OffsetY]);
    }

    [Fact]
    public void DrawMultiline_EmptyTextCoversNothing()
    {
        BitmapFont font = CreateTestFont();
        PixelBuffer target = new(8, 8);

        Assert.Equal(Size.Empty, font.DrawMultiline(target, new Point(0, 0), string.Empty));
        Assert.Equal(0, target.Pixels.Count(pixel => pixel != 0));
    }

    [Fact]
    public void Create_RejectsInvalidTables()
    {
        Assert.Throws<ArgumentException>(() => BitmapFont.Create([], BitmapFontSize.Normal, "empty"));
        Assert.Throws<ArgumentException>(() => BitmapFont.Create([null!], BitmapFontSize.Normal, "null"));
        Assert.Throws<ArgumentException>(() => BitmapFont.Create([new Sprite(1, 1)], BitmapFontSize.Normal, " "));
        Assert.Throws<ArgumentNullException>(() => BitmapFont.Create(null!, BitmapFontSize.Normal, "null"));
    }

    [Fact]
    public void FontWithoutQuestionMark_StillAdvancesByZero()
    {
        // A table that ends before '?' has no fallback glyph; unknown characters must not draw.
        BitmapFont font = BitmapFont.Create(CreateGlyphs('?', 6, 13, -1, -2), BitmapFontSize.Normal, "short");
        PixelBuffer target = new(16, 16);

        Assert.Equal(0, font.GetWidth((char)0x01));

        font.DrawText(target, new Point(0, 0), "\u0001");

        Assert.Equal(0, target.Pixels.Count(pixel => pixel != 0));
    }

    [Fact]
    public void NormalFont_LoadsFromTheArchive()
    {
        byte[]? data = ReadEntry("FONT.ICN");
        if (data is null)
        {
            return;
        }

        BitmapFont font = BitmapFont.FromIcn(IcnSpriteFile.Parse(data), BitmapFontSize.Normal, "FONT.ICN");

        _output.WriteLine($"FONT.ICN: {font.GlyphCount} glyphs, space {font.SpaceWidth}, line {font.LineHeight}");
        _output.WriteLine($"'A' advance {font.GetWidth(LetterA)}, 'Heroes' width {font.Measure("Heroes")}");

        Sprite wide = font.GetSprite(LetterA);
        Sprite narrow = font.GetSprite('i');
        _output.WriteLine(
            $"'A' {wide.Width}x{wide.Height} @ ({wide.OffsetX},{wide.OffsetY}), 'i' {narrow.Width}x{narrow.Height} @ ({narrow.OffsetX},{narrow.OffsetY})");

        Assert.True(font.GlyphCount > 'Z');
        Assert.Equal(17, font.LineHeight);
        Assert.True(font.GetWidth(LetterA) > 0);
        Assert.True(font.Measure("Heroes") > 0);
        Assert.True(font.IsAvailable('Z'));
        Assert.False(font.IsAvailable((char)0x01));
    }

    [Fact]
    public void SmallFont_LoadsFromTheArchive()
    {
        byte[]? data = ReadEntry("SMALFONT.ICN");
        if (data is null)
        {
            return;
        }

        BitmapFont font = BitmapFont.FromIcn(IcnSpriteFile.Parse(data), BitmapFontSize.Small, "SMALFONT.ICN");

        _output.WriteLine($"SMALFONT.ICN: {font.GlyphCount} glyphs, space {font.SpaceWidth}, line {font.LineHeight}");
        _output.WriteLine($"'A' advance {font.GetWidth(LetterA)}, 'Heroes' width {font.Measure("Heroes")}");

        Assert.True(font.GlyphCount > 'Z');
        Assert.Equal(11, font.LineHeight);
        Assert.True(font.GetWidth(LetterA) > 0);
    }

    [Fact]
    public void NormalFont_RendersTheMenuTitle()
    {
        byte[]? data = ReadEntry("FONT.ICN");
        if (data is null)
        {
            return;
        }

        BitmapFont font = BitmapFont.FromIcn(IcnSpriteFile.Parse(data), BitmapFontSize.Normal, "FONT.ICN");
        PixelBuffer target = new(320, 64);
        int endX = font.DrawText(target, new Point(4, 4), "New Game");

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(target.Pixels);
        string digest = Convert.ToHexString(hash.GetHashAndReset());

        _output.WriteLine($"'New Game' pen end {endX}, width {font.Measure("New Game")}, digest {digest}");

        Assert.Equal(font.Measure("New Game") + 4, endX);
        Assert.True(target.Pixels.Count(pixel => pixel != 0) > 50);
    }

    [Fact]
    public void GlyphSource_ExposesTheSameMetricsAsTheBitmapFont()
    {
        BitmapFont font = CreateTestFont();
        IGlyphSource source = font;

        Assert.Equal(font.LineHeight, source.LineHeight);
        Assert.Equal(font.SpaceWidth, source.SpaceWidth);
        Assert.Equal(font.GetWidth(LetterA), source.GetAdvance(LetterA));
        Assert.Equal(font.SpaceWidth, source.GetAdvance(' '));
        Assert.True(source.IsAvailable(LetterA));
        Assert.False(source.IsAvailable((char)0x01));

        Assert.True(source.TryGetGlyph(LetterA, out Sprite glyph));
        Assert.Same(font.GetSprite(LetterA), glyph);

        // Characters without a glyph still yield the substitute so a layout pass can draw it.
        Assert.False(source.TryGetGlyph((char)0x01, out Sprite fallback));
        Assert.Same(font.GetSprite('?'), fallback);
    }

    private static BitmapFont CreateTestFont(BitmapFontSize size = BitmapFontSize.Normal) =>
        BitmapFont.Create(CreateGlyphs(0x5B, 6, 13, -1, -2), size, "synthetic");

    private static Sprite[] CreateGlyphs(int count, int width, int height, int offsetX, int offsetY)
    {
        Sprite[] glyphs = new Sprite[count];
        for (int index = 0; index < glyphs.Length; ++index)
        {
            glyphs[index] = CreateGlyph(width, height, offsetX, offsetY, (byte)((index % 200) + 1));
        }

        return glyphs;
    }

    private static Sprite CreateGlyph(int width, int height, int offsetX, int offsetY, byte color)
    {
        Sprite glyph = new(width, height, offsetX, offsetY);
        glyph.Image.Fill(color);
        return glyph;
    }

    private static byte[]? ReadEntry(string entryName)
    {
        string? path = GameData.FindArchive(AssetLocator.BaseArchiveName);
        if (path is null)
        {
            return null;
        }

        using AssetArchive archive = AssetArchive.Open(path);
        return archive.Read(entryName);
    }
}
