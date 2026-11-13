using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using GlyphRaster;
using Xunit.Abstractions;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Tests the TrueType glyph provider that backs CJK and other characters the bitmap tables cannot
/// express. The bundled face (WenQuanYi Micro Hei) ships inside the rasterizer package, so these
/// tests never touch the game data and always run.
/// </summary>
public sealed class TrueTypeGlyphSourceTests
{
    private const float EmSize = 16f;
    private const char ChineseCharacter = '中';

    private static readonly TextFont Font = TextFont.BuiltIn;

    private readonly ITestOutputHelper _output;

    public TrueTypeGlyphSourceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Metrics_ComeFromTheFace()
    {
        TrueTypeGlyphSource source = new(Font, EmSize);

        // WenQuanYi Micro Hei: ascent 14.98, line height 18.76 and a 4 pixel space at em 16.
        Assert.Equal(19, source.LineHeight);
        Assert.Equal(15, source.Ascent);
        Assert.Equal(4, source.SpaceWidth);
        Assert.Equal(EmSize, source.EmSize);
        Assert.Equal(Font.FamilyName, source.FamilyName);
    }

    [Fact]
    public void ChineseCharacter_IsAFullWidthGlyph()
    {
        IGlyphSource source = new TrueTypeGlyphSource(Font, EmSize);

        Assert.True(source.IsAvailable(ChineseCharacter));
        Assert.Equal(16, source.GetAdvance(ChineseCharacter));
    }

    [Fact]
    public void LatinCharacter_IsNarrowerThanChinese()
    {
        IGlyphSource source = new TrueTypeGlyphSource(Font, EmSize);

        Assert.Equal(10, source.GetAdvance('A'));
        Assert.True(source.GetAdvance('A') < source.GetAdvance(ChineseCharacter));
        Assert.True(source.GetAdvance('i') < source.GetAdvance('A'));
    }

    [Fact]
    public void Space_IsBlankButStillAdvances()
    {
        TrueTypeGlyphSource source = new(Font, EmSize);

        Assert.True(source.GetSprite(' ').Image.IsEmpty);
        Assert.True(source.IsAvailable(' '));
        Assert.Equal(source.SpaceWidth, source.GetAdvance(' '));

        // A line separator is never a glyph and never advances.
        Assert.Equal(0, source.GetAdvance(GlyphText.LineSeparator));
        Assert.False(source.IsAvailable(GlyphText.LineSeparator));
    }

    [Fact]
    public void Glyph_AnchorsRelativeToTheTopOfTheTextCell()
    {
        TrueTypeGlyphSource source = new(Font, EmSize);
        GlyphMask mask = Font.GetGlyphMask(ChineseCharacter, EmSize);

        Sprite glyph = source.GetSprite(ChineseCharacter);

        // The rasterizer measures OffsetY from the baseline; the sprite keeps the top left origin
        // the bitmap font uses, so the ascent is added exactly once.
        Assert.Equal(mask.Width, glyph.Width);
        Assert.Equal(mask.Height, glyph.Height);
        Assert.Equal(mask.OffsetX, glyph.OffsetX);
        Assert.Equal(mask.OffsetY + source.Ascent, glyph.OffsetY);
        Assert.InRange(glyph.OffsetY, 0, source.LineHeight);
        Assert.InRange(glyph.OffsetY + glyph.Height, 0, source.LineHeight);
    }

    [Fact]
    public void Glyph_OnlyPaintsRampIndicesAndSkipsTheRest()
    {
        TrueTypeGlyphSource source = new(Font, EmSize);
        Sprite glyph = source.GetSprite(ChineseCharacter);

        Assert.True(glyph.HasTransformLayer);

        byte[] transform = glyph.Transform!;
        HashSet<byte> painted = [];
        int skipped = 0;

        for (int index = 0; index < transform.Length; ++index)
        {
            if (transform[index] == 1)
            {
                ++skipped;
                continue;
            }

            Assert.Equal(0, transform[index]);
            Assert.Contains(glyph.Image.Pixels[index], source.Ramp.Levels);
            painted.Add(glyph.Image.Pixels[index]);
        }

        Assert.NotEmpty(painted);
        Assert.True(skipped > 0);
        Assert.True(painted.Count > 1, "An antialiased glyph must use more than one ramp step.");

        _output.WriteLine(
            $"'{ChineseCharacter}' @{EmSize}: {glyph.Width}x{glyph.Height} at ({glyph.OffsetX},{glyph.OffsetY}), " +
            $"{transform.Length - skipped}/{transform.Length} pixels painted, {painted.Count} ramp steps");
    }

    [Fact]
    public void Glyph_IsCachedPerCharacter()
    {
        TrueTypeGlyphSource source = new(Font, EmSize);

        Assert.Same(source.GetSprite(ChineseCharacter), source.GetSprite(ChineseCharacter));
        Assert.Same(source.GetSprite('A'), source.GetSprite('A'));
        Assert.NotSame(source.GetSprite('A'), source.GetSprite('B'));
    }

    [Fact]
    public void UnmappedCharacter_FallsBackToQuestionMark()
    {
        TrueTypeGlyphSource source = new(Font, EmSize);

        // U+E000 is a private use code point: the face does not map it.
        Assert.False(source.IsAvailable('\uE000'));
        Assert.False(source.TryGetGlyph('\uE000', out Sprite substitute));
        Assert.Same(source.GetSprite('?'), substitute);

        Assert.True(source.TryGetGlyph(ChineseCharacter, out Sprite glyph));
        Assert.Same(source.GetSprite(ChineseCharacter), glyph);
    }

    [Fact]
    public void ForLineHeight_LandsOnTheBitmapFontGrid()
    {
        TrueTypeGlyphSource source = TrueTypeGlyphSource.ForLineHeight(Font, 17);

        // The normal bitmap font uses a 17 pixel line; the em size is solved from the face metrics.
        Assert.Equal(17, source.LineHeight);
        Assert.InRange(source.EmSize, 14.3f, 14.7f);
        Assert.InRange(source.GetAdvance(ChineseCharacter), 14, 17);
    }

    [Fact]
    public void CustomRamp_ProducesAHardEdgedGlyph()
    {
        TrueTypeGlyphSource antialiased = new(Font, EmSize);
        TrueTypeGlyphSource monochrome = new(Font, EmSize, new GlyphCoverageRamp([7], minCoverage: 128));

        Sprite smooth = antialiased.GetSprite('A');
        Sprite hard = monochrome.GetSprite('A');

        Assert.Equal(7, monochrome.Ramp.Levels[0]);
        Assert.True(CountPainted(hard) < CountPainted(smooth));

        for (int index = 0; index < hard.Transform!.Length; ++index)
        {
            if (hard.Transform[index] == 0)
            {
                Assert.Equal(7, hard.Image.Pixels[index]);
            }
        }
    }

    [Fact]
    public void DrawLine_PaintsTextThroughTheSharedLayout()
    {
        IGlyphSource source = new TrueTypeGlyphSource(Font, EmSize);
        PixelBuffer target = new(96, 32);

        int endX = GlyphText.DrawLine(source, target, new Point(4, 2), "英雄 A");

        Assert.Equal(4 + GlyphText.Measure(source, "英雄 A"), endX);
        Assert.True(CountSetPixels(target) > 0);

        // Nothing is painted outside the text cell: the top row and the right edge stay untouched.
        for (int x = 0; x < target.Width; ++x)
        {
            Assert.Equal(0, target[x, 0]);
        }

        for (int y = 0; y < target.Height; ++y)
        {
            Assert.Equal(0, target[target.Width - 1, y]);
        }
    }

    [Fact]
    public void WrapLines_BreaksCjkBetweenCharacters()
    {
        IGlyphSource source = new TrueTypeGlyphSource(Font, EmSize);

        // Four full width characters at 16 px each do not fit 40 pixels.
        List<string> lines = GlyphText.WrapLines(source, "英雄无敌", 40);

        Assert.Equal(2, lines.Count);
        Assert.Equal("英雄", lines[0]);
        Assert.Equal("无敌", lines[1]);
        Assert.True(GlyphText.Measure(source, lines[0]) <= 40);
    }

    private static int CountPainted(Sprite sprite)
    {
        byte[]? transform = sprite.Transform;
        int painted = 0;

        for (int index = 0; index < sprite.Image.Pixels.Length; ++index)
        {
            if (transform is null || transform[index] == 0)
            {
                ++painted;
            }
        }

        return painted;
    }

    private static int CountSetPixels(PixelBuffer buffer)
    {
        int count = 0;
        foreach (byte pixel in buffer.AsSpan())
        {
            if (pixel != 0)
            {
                ++count;
            }
        }

        return count;
    }
}
