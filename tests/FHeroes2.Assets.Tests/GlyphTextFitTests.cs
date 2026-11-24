using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// The one line truncation of <see cref="GlyphText.FitToOneRow"/>, ported from
/// <c>fheroes2::Text::fitToOneRow()</c> (<c>ui_text.cpp:495-522</c>).
/// </summary>
public sealed class GlyphTextFitTests
{
    /// <summary>A font whose every character is one pixel wide; the ellipsis is three.</summary>
    private sealed class UniformGlyphSource : IGlyphSource
    {
        public int LineHeight => 17;

        public int SpaceWidth => 1;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => character == GlyphText.LineSeparator ? 0 : 1;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = new Sprite(1, 1);

            return true;
        }
    }

    private static readonly UniformGlyphSource Font = new();

    [Fact]
    public void ATextThatFitsIsReturnedUnchanged()
    {
        Assert.Equal("abc", GlyphText.FitToOneRow(Font, "abc", 10));
        Assert.Equal("abc", GlyphText.FitToOneRow(Font, "abc", 3));
    }

    [Fact]
    public void ATextThatDoesNotFitIsCutAndEndsInTheEllipsis()
    {
        // Five pixels of budget minus the three of "..." leaves two characters.
        Assert.Equal("ab" + GlyphText.TruncationSymbol, GlyphText.FitToOneRow(Font, "abcdef", 5));
    }

    [Fact]
    public void OnlyTheEllipsisFitsWhenTheBudgetIsTheEllipsis()
    {
        // Two pixels cannot hold the three pixel ellipsis, so nothing else survives either.
        Assert.Equal(GlyphText.TruncationSymbol, GlyphText.FitToOneRow(Font, "abc", 2));
        Assert.Equal("abc", GlyphText.FitToOneRow(Font, "abc", 4));
    }

    [Fact]
    public void AnEmptyTextOrANonPositiveWidthIsLeftAlone()
    {
        Assert.Equal(string.Empty, GlyphText.FitToOneRow(Font, string.Empty, 10));
        Assert.Equal("abc", GlyphText.FitToOneRow(Font, "abc", 0));
        Assert.Equal("abc", GlyphText.FitToOneRow(Font, "abc", -4));
    }

    [Fact]
    public void ASurrogatePairIsNeverSplit()
    {
        // "a" + U+1F600 + "bcdef": the emoji occupies two code units and two pixels, the rest one each.
        string text = "a\U0001F600bcdef";

        string fitted = GlyphText.FitToOneRow(Font, text, 6);

        Assert.Equal("a\U0001F600" + GlyphText.TruncationSymbol, fitted);
    }

    [Fact]
    public void TheLineSeparatorCostsNoWidth()
    {
        Assert.Equal("a\nb", GlyphText.FitToOneRow(Font, "a\nb", 2));
    }
}
