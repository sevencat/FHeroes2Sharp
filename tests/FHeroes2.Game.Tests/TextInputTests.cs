using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the 1:1 port of <c>ui_text.cpp</c>'s <c>TextInput</c>.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class TextInputTests
{
    [Fact]
    public void ANewInputIsEmptyAndStillHasACaret()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            TextInput input = new(FontType.NormalWhite(), 100, false);

            Assert.True(input.IsEmpty);
            Assert.Equal(string.Empty, input.Content);
            Assert.Equal(0, input.Width());

            // The caret area comes from the '|' glyph even when there is nothing to edit yet.
            Assert.True(input.CursorArea.Width > 0);
            Assert.True(input.CursorArea.Height > 0);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void ShortTextMeasuresAndDrawsLikeAPlainText()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Text plain = new("HELLO", FontType.NormalWhite());

            TextInput input = new(FontType.NormalWhite(), 500, false);
            input.Set("HELLO", 5);

            Assert.Equal("HELLO", input.Content);
            Assert.Equal(plain.Width(), input.Width());
            Assert.Equal(plain.Height(), input.Height());

            // Nothing is truncated, so the input must paint exactly the same pixels as the plain text.
            Rect roi = new(0, 0, 200, 40);
            PixelBuffer expected = new(200, 40);
            expected.Fill(0);
            PixelBuffer actual = new(200, 40);
            actual.Fill(0);

            plain.DrawInRoi(10, 5, expected, roi);
            input.DrawInRoi(10, 5, actual, roi);

            AssertBuffersEqual(expected, actual);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheCaretMovesByTheWidthOfTheCharactersBeforeIt()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            TextInput atBegin = new(FontType.NormalWhite(), 500, false);
            atBegin.Set("ABCDEF", 0);

            TextInput atEnd = new(FontType.NormalWhite(), 500, false);
            atEnd.Set("ABCDEF", 6);

            int textWidth = new Text("ABCDEF", FontType.NormalWhite()).Width();

            Assert.Equal(atBegin.CursorArea.X + textWidth, atEnd.CursorArea.X);
            Assert.Equal(atBegin.CursorArea.Y, atEnd.CursorArea.Y);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TooLongTextIsTruncatedInsteadOfOverflowing()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            const string text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const int maxWidth = 150;

            // Wide enough for two truncation symbols ( 45 pixels each ) and a few characters.
            TextInput input = new(FontType.NormalWhite(), maxWidth, false);

            // The caret is at the very end, so the text has to be cut at the beginning.
            input.Set(text, text.Length);

            Assert.Equal(text, input.Content);
            Assert.True(input.Width() <= maxWidth, $"width {input.Width()} must not exceed {maxWidth}");
            Assert.True(input.Width() > TextAlgorithms.GetTruncationSymbolWidth(FontType.NormalWhite()));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void AWidthNarrowerThanTheTruncationSymbolDoesNotCrash()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            // Narrower than a single "..."; the reference walks off the text here.
            TextInput input = new(FontType.NormalWhite(), 5, false);
            input.Set("ABCDEFGH", 8);

            int truncationWidth = TextAlgorithms.GetTruncationSymbolWidth(FontType.NormalWhite());

            Assert.True(input.Width() > 0);
            Assert.True(input.Width() <= 2 * truncationWidth, $"width was {input.Width()}");
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void MultiLineInputWrapsWithTheOwnWidth()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            TextInput input = new(FontType.NormalWhite(), 100, true);
            input.Set("AAAA BBBB CCCC DDDD", 0);

            // width() of a multi-line input is the wrapped width, not the single line one.
            Assert.Equal(input.Width(100), input.Width());
            Assert.True(input.Rows(100) > 1, $"rows were {input.Rows(100)}");
            Assert.True(input.Height(100) > input.Height());
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void HoveringTheLeftEdgeOfASecondLinePutsTheCaretAtThatLineStart()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            const int maxWidth = 100;
            TextInput input = new(FontType.NormalWhite(), maxWidth, true);
            input.Set("AAAA BBBB CCCC", 0);

            List<TextLineInfo> lineInfos = [];
            input.GetTextLineInfos(lineInfos, maxWidth, input.Height(), true);

            Assert.True(lineInfos.Count >= 2, $"rows were {lineInfos.Count}");

            int expected = lineInfos[0].CharacterCount;
            Rect roi = new(0, 0, maxWidth, 60);

            // The y offset lands on the second line, x is to the left of it.
            Assert.Equal(expected, input.GetCursorPosition(new Point(0, input.Height() + 1), roi, false));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void AdjacentLineMovementStaysInsideTheTargetLine()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            const int maxWidth = 100;
            TextInput input = new(FontType.NormalWhite(), maxWidth, true);
            input.Set("AAAA BBBB CCCC", 0);

            List<TextLineInfo> lineInfos = [];
            input.GetTextLineInfos(lineInfos, maxWidth, input.Height(), true);

            Assert.True(lineInfos.Count >= 2, $"rows were {lineInfos.Count}");

            int firstLineEnd = lineInfos[0].CharacterCount;
            int secondLineEnd = firstLineEnd + lineInfos[1].CharacterCount;

            int movedDown = input.GetCursorPositionInAdjacentLine(firstLineEnd, maxWidth, false);
            Assert.InRange(movedDown, firstLineEnd, secondLineEnd);

            int movedUp = input.GetCursorPositionInAdjacentLine(movedDown, maxWidth, true);
            Assert.InRange(movedUp, 0, firstLineEnd);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void ClickingInsideASingleLineReturnsTheNearestBoundary()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            FontCharHandler charHandler = new(FontType.NormalWhite());
            TextInput input = new(FontType.NormalWhite(), 500, false);
            input.Set("ABCDEF", 0);

            Rect roi = new(10, 10, 500, 15);

            // Left of the field, and past the last character.
            Assert.Equal(0, input.GetCursorPosition(new Point(0, 12), roi, false));
            Assert.Equal(6, input.GetCursorPosition(new Point(400, 12), roi, false));

            // Just past the middle of the first character: still before it.
            int firstWidth = charHandler.GetWidth((byte)'A');
            Assert.Equal(0, input.GetCursorPosition(new Point(10 + (firstWidth / 2) - 1, 12), roi, false));

            // Past the middle of the first character: the caret moves behind it.
            Assert.Equal(1, input.GetCursorPosition(new Point(10 + (firstWidth / 2) + 1, 12), roi, false));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    private static void AssertBuffersEqual(PixelBuffer expected, PixelBuffer actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (int y = 0; y < expected.Height; ++y)
        {
            for (int x = 0; x < expected.Width; ++x)
            {
                if (expected[x, y] != actual[x, y])
                {
                    Assert.Fail($"pixel mismatch at {x},{y}: expected {expected[x, y]}, got {actual[x, y]}");
                }
            }
        }
    }
}
