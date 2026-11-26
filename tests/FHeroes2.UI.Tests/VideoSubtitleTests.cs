using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The text burnt over a video, <c>Video::Subtitle</c>'s layout, as used by the campaign rating over <c>WIN.SMK</c>
/// (<c>game_campaign.cpp:1267-1269</c>).
/// </summary>
public sealed class VideoSubtitleTests
{
    private const int LineHeight = 12;
    private const int Advance = 6;
    private const byte TextColor = 30;

    [Fact]
    public void AnEmptyTextDrawsNothing()
    {
        PixelBuffer canvas = Canvas();

        Rect area = VideoSubtitle.Draw(Font(), canvas, string.Empty, new Point(10, 10));

        Assert.Equal(Rect.Empty, area);
        Assert.Equal(0, canvas[10, 10]);
    }

    [Fact]
    public void TheTwoParagraphsAreDrawnOnTwoRows()
    {
        PixelBuffer canvas = Canvas();

        Rect area = VideoSubtitle.Draw(Font(), canvas, "aa\nbb", new Point(10, 10), maxWidth: 0);

        Assert.Equal(new Rect(10, 10, Advance * 2, LineHeight * 2), area);
        Assert.Equal(TextColor, canvas[11, 13]);
        Assert.Equal(0, canvas[11, 9]);
        Assert.Equal(TextColor, canvas[11, 10 + LineHeight + 3]);
    }

    [Fact]
    public void ALongWordWrapsInsideTheWidth()
    {
        PixelBuffer canvas = Canvas();

        // Six characters of six pixels are thirty-six pixels wide; eighteen pixels hold three of them per line.
        Rect area = VideoSubtitle.Draw(Font(), canvas, "aaaaaa", new Point(10, 10), maxWidth: 18);

        Assert.Equal(LineHeight * 2, area.Height);
    }

    [Fact]
    public void CentringMovesTheLineInsideTheWidth()
    {
        PixelBuffer canvas = Canvas();

        // "aa" is twelve pixels wide, so inside sixty it starts twenty-four pixels to the right.
        VideoSubtitle.Draw(Font(), canvas, "aa", new Point(10, 10), maxWidth: 60);

        Assert.Equal(0, canvas[10 + 23, 13]);
        Assert.Equal(TextColor, canvas[10 + 24, 13]);
    }

    [Fact]
    public void WithoutAWidthTheLineStartsWhereItWasTold()
    {
        PixelBuffer canvas = Canvas();

        VideoSubtitle.Draw(Font(), canvas, "aa", new Point(10, 10), maxWidth: 0);

        Assert.Equal(TextColor, canvas[10, 13]);
    }

    [Fact]
    public void TheReferenceWidthIsTheBurnedSquare()
    {
        Assert.Equal(140, VideoSubtitle.ReferenceWidth);
    }

    private static PixelBuffer Canvas()
    {
        PixelBuffer canvas = new(320, 200);
        canvas.Fill(0);

        return canvas;
    }

    private static IGlyphSource Font() => new FakeGlyphSource(TextColor, LineHeight);

    /// <summary>A glyph source whose glyphs are solid blocks of one colour, so a test can see where text landed.</summary>
    private sealed class FakeGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;
        private readonly int _advance;

        public FakeGlyphSource(byte color, int lineHeight, int advance = Advance)
        {
            _advance = advance;
            LineHeight = lineHeight;

            PixelBuffer image = new(advance, lineHeight);
            image.Fill(color);
            _glyph = new Sprite(image, 0, 0);
        }

        public int LineHeight { get; }

        public int SpaceWidth => _advance;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => _advance;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
