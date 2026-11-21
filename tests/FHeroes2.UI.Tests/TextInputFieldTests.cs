using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Widgets;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of <c>TextInputField</c> (<c>ui_tool.cpp:208-281</c>): the areas it owns, what
/// <see cref="TextInputField.Draw"/> paints, the caret position and the multi-line scrolling rule.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class TextInputFieldTests
{
    [Fact]
    public void ANewFieldIsEmptyAndKeepsTheAreasOfTheReference()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            TextInputField field = new(new Rect(10, 20, 120, 30), isMultiLine: false, isCenterAligned: false);

            Assert.True(field.Text.IsEmpty);
            Assert.Equal(0, field.VerticalTextOffset);
            Assert.Equal(new Rect(10, 20, 120, 30), field.TextInputArea);

            // The overall area is enlarged by one pixel at both sides and by two pixels at the top.
            Assert.Equal(new Rect(9, 18, 122, 32), field.OverallArea);

            Assert.NotNull(field.Cursor.Sprite);
            Assert.True(field.CursorArea.Width > 0);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void DrawPaintsTheTextInsideTheFieldOnly()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            Rect area = new(10, 10, 120, 30);
            TextInputField field = new(area, isMultiLine: false, isCenterAligned: false);
            PixelBuffer output = new(200, 100);

            Rect painted = field.Draw("AB", 2, output);

            Assert.Equal(field.OverallArea, painted);
            Assert.Contains(output.Pixels, pixel => pixel != 0);

            // Nothing outside the overall area is touched.
            for (int y = 0; y < output.Height; ++y)
            {
                for (int x = 0; x < output.Width; ++x)
                {
                    if (!field.OverallArea.Contains(new Point(x, y)))
                    {
                        Assert.Equal(0, output[x, y]);
                    }
                }
            }
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void TheCaretFollowsTheTextAndIsSkippedWhileItIsHidden()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            TextInputField field = new(new Rect(10, 10, 120, 30), isMultiLine: false, isCenterAligned: false);

            PixelBuffer withCaret = new(200, 100);
            field.Draw("AB", 2, withCaret);

            Text text = new("AB", FontType.NormalWhite());

            // Left aligned single line: the caret sits at the end of the text, near the top of the area.
            Assert.InRange(field.CursorArea.X, 10 + text.Width() - 1, 10 + text.Width());
            Assert.InRange(field.CursorArea.Y, 10, 13);

            int pixelsWithCaret = withCaret.Pixels.Count(pixel => pixel != 0);

            // Hiding the caret is the blink off phase: the text is still drawn, the caret is not.
            field.Cursor.Hide();

            PixelBuffer withoutCaret = new(200, 100);
            Rect painted = field.Draw("AB", 2, withoutCaret);

            Assert.True(painted.Width > 0);
            Assert.True(withoutCaret.Pixels.Count(pixel => pixel != 0) < pixelsWithCaret);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void ASingleLineIsCentredOnlyWhenAsked()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            Rect area = new(10, 10, 120, 30);
            PixelBuffer output = new(200, 100);

            TextInputField leftAligned = new(area, isMultiLine: false, isCenterAligned: false);
            leftAligned.Draw("AB", 2, output);

            TextInputField centred = new(area, isMultiLine: false, isCenterAligned: true);
            centred.Draw("AB", 2, output);

            int textWidth = new Text("AB", FontType.NormalWhite()).Width();

            Assert.Equal(leftAligned.CursorArea.X + ((area.Width - textWidth) / 2), centred.CursorArea.X);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void AMultiLineTextScrollsByWholeLines()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            // Only three text lines fit into the area, so the field has to scroll to keep the caret visible.
            TextInputField field = new(new Rect(10, 10, 120, 12), isMultiLine: true, isCenterAligned: false);
            PixelBuffer output = new(200, 100);

            const string text = "A\nA\nA\nA\nA\nA\nA\nA";
            field.Draw(text, text.Length, output);

            int lineHeight = new Text("A", FontType.NormalWhite()).Height();

            Assert.True(field.VerticalTextOffset > 0);
            Assert.Equal(0, field.VerticalTextOffset % lineHeight);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void HeightMeasuresTheTextWithoutTouchingTheFieldText()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            TextInputField field = new(new Rect(10, 10, 120, 30), isMultiLine: true, isCenterAligned: false);
            field.Set("XY", 2);

            int lineHeight = new Text("A", FontType.NormalWhite()).Height();

            Assert.Equal(lineHeight, field.Height("A"));
            Assert.Equal(2 * lineHeight, field.Height("A\nB"));

            // The field keeps its own text.
            Assert.Equal("XY", field.Text.Content);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void APointInsideTheFieldMapsToACaretOffset()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            TextInputField field = new(new Rect(10, 10, 120, 30), isMultiLine: false, isCenterAligned: false);
            PixelBuffer output = new(200, 100);
            field.Draw("ABCD", 4, output);

            Assert.Equal(0, field.GetCursorInTextPosition(new Point(10, 10)));
            Assert.Equal(4, field.GetCursorInTextPosition(new Point(1000, 10)));
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;
}
