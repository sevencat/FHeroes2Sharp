using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the 1:1 port of <c>ui_text.cpp</c>'s <c>MultiFontText</c>.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class MultiFontTextTests
{
    [Fact]
    public void EmptyRunsAreIgnoredAndRunsAreCopied()
    {
        MultiFontText multi = new();

        Assert.True(multi.IsEmpty);
        Assert.Equal(string.Empty, multi.Content);
        Assert.Equal(0, multi.Width());
        Assert.Equal(0, multi.Height());
        Assert.Equal(0, multi.Rows(100));

        multi.Add(new Text(string.Empty, FontType.NormalWhite()));
        Assert.True(multi.IsEmpty);

        Text run = new("AA", FontType.NormalWhite());
        multi.Add(run);

        // add() takes the text by value in the reference, so later changes to `run` must not leak in.
        run.Set("ZZZZ", FontType.NormalWhite());

        Assert.False(multi.IsEmpty);
        Assert.Equal("AA", multi.Content);
    }

    [Fact]
    public void WidthSumsRunsWhileHeightTakesTheTallest()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Text small = new("AAA", FontType.SmallWhite());
            Text normal = new("BBB", FontType.NormalWhite());

            MultiFontText multi = new();
            multi.Add(small);
            multi.Add(normal);

            Assert.Equal(small.Width() + normal.Width(), multi.Width());

            // height() is the tallest run, not the sum: 13 for SMALL, 15 for NORMAL.
            Assert.Equal(15, multi.Height());
            Assert.Equal("AAABBB", multi.Content);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void SingleLineDrawMatchesDrawingEachRunSeparately()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Text first = new("ABC", FontType.NormalWhite());
            Text second = new("DEF", FontType.NormalWhite());

            MultiFontText multi = new();
            multi.Add(first);
            multi.Add(second);

            Rect roi = new(0, 0, 200, 60);
            PixelBuffer actual = new(200, 60);
            actual.Fill(0);

            PixelBuffer expected = new(200, 60);
            expected.Fill(0);

            multi.DrawInRoi(10, 5, actual, roi);
            first.DrawInRoi(10, 5, expected, roi);
            second.DrawInRoi(10 + first.Width(), 5, expected, roi);

            AssertBuffersEqual(expected, actual);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void SmallerRunsAreVerticallyCentredAgainstTheTallestOne()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Text small = new("AAA", FontType.SmallWhite());
            Text normal = new("BBB", FontType.NormalWhite());

            MultiFontText multi = new();
            multi.Add(small);
            multi.Add(normal);

            Rect roi = new(0, 0, 200, 60);
            PixelBuffer actual = new(200, 60);
            actual.Fill(0);

            PixelBuffer expected = new(200, 60);
            expected.Fill(0);

            multi.DrawInRoi(10, 5, actual, roi);

            // Max run height 15, SMALL is 13 tall, so it drops by ( 15 - 13 ) / 2 = 1.
            small.DrawInRoi(10, 5 + 1, expected, roi);
            normal.DrawInRoi(10 + small.Width(), 5, expected, roi);

            AssertBuffersEqual(expected, actual);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void MaxWidthWrapsTheNextRunOntoASecondLine()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Text first = new("AAAA", FontType.NormalWhite());
            Text second = new("BBBB", FontType.NormalWhite());

            MultiFontText multi = new();
            multi.Add(first);
            multi.Add(second);

            // Wide enough for either run on its own, too narrow for both on one line.
            int maxWidth = Math.Max(first.Width(), second.Width());

            Assert.Equal(30, multi.Height(maxWidth));
            Assert.Equal(Math.Max(first.Width(), second.Width()), multi.Width(maxWidth));

            // The reference logs a zero character line where the wrapped run leaves the first line, so rows() is
            // one more than the two lines that actually carry glyphs.
            Assert.Equal(3, multi.Rows(maxWidth));

            Rect roi = new(0, 0, 200, 80);
            PixelBuffer target = new(200, 80);
            target.Fill(0);

            multi.DrawInRoi(10, 5, maxWidth, target, roi);

            Assert.True(HasInk(target, 0, 15), "the first run must be painted on the first line");
            Assert.True(HasInk(target, 15, 30), "the second run must be painted on the second line");
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void FitToOneRowTruncatesTheFirstRunAndDropsTheRest()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Text first = new("AAAAAAAA", FontType.NormalWhite());
            Text second = new("BBBB", FontType.NormalWhite());

            MultiFontText multi = new();
            multi.Add(first);
            multi.Add(second);

            // Leaves room for the truncation symbol.
            int maxWidth = first.Width() - 20;
            multi.FitToOneRow(maxWidth);

            Assert.EndsWith("...", multi.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("B", multi.Content, StringComparison.Ordinal);
            Assert.True(multi.Content.Length < first.Content.Length + 3, $"content was {multi.Content}");
            Assert.True(multi.Width() <= maxWidth, $"width {multi.Width()} must not exceed {maxWidth}");
            Assert.Equal(1, multi.Rows(maxWidth));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    private static bool HasInk(PixelBuffer buffer, int top, int bottom)
    {
        for (int y = top; y < bottom; ++y)
        {
            for (int x = 0; x < buffer.Width; ++x)
            {
                if (buffer[x, y] != 0)
                {
                    return true;
                }
            }
        }

        return false;
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
