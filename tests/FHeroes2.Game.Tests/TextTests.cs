using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Gui;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the 1:1 port of <c>ui_text.cpp</c>'s <c>Text</c> and of
/// <c>editor_ui_helper.cpp</c>'s <c>drawCheckboxWithText</c>.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class TextTests
{
    [Fact]
    public void FontHeightFollowsTheReferenceTable()
    {
        Assert.Equal(13, FontCharHandler.GetFontHeight(FontSize.Small));
        Assert.Equal(15, FontCharHandler.GetFontHeight(FontSize.Normal));
        Assert.Equal(15, FontCharHandler.GetFontHeight(FontSize.Large));
        Assert.Equal(16, FontCharHandler.GetFontHeight(FontSize.ButtonReleased));
        Assert.Equal(16, FontCharHandler.GetFontHeight(FontSize.ButtonPressed));
    }

    [Fact]
    public void GlyphsAreLookedUpByAsciiCode()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            // The archive's frame 0 is ' ', so the glyph of 'A' ( 0x41 ) is frame 0x41 - 0x20.
            Sprite glyph = FontCharHandler.GetChar((byte)'A', FontType.NormalWhite());
            Sprite? raw = Assets.GetImage("FONT.ICN", 'A' - 0x20);

            Assert.NotNull(raw);
            Assert.Equal(raw!.Width, glyph.Width);
            Assert.Equal(raw.Height, glyph.Height);
            Assert.Equal(raw.OffsetX, glyph.OffsetX);
            Assert.Equal(raw.OffsetY, glyph.OffsetY);

            for (int y = 0; y < raw.Height; ++y)
            {
                for (int x = 0; x < raw.Width; ++x)
                {
                    Assert.Equal(raw.Image[x, y], glyph.Image[x, y]);
                }
            }

            // A byte the font cannot show falls back to '?' through getSprite().
            FontCharHandler charHandler = new(FontType.NormalWhite());
            Sprite fallback = charHandler.GetSprite(0x01);
            Sprite questionMark = charHandler.GetSprite((byte)'?');
            Assert.Equal(questionMark.Width, fallback.Width);
            Assert.Equal(questionMark.Height, fallback.Height);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void YellowAndGrayFacesRemapTheWhiteGlyphs()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            const byte character = (byte)'A';
            Sprite? white = Assets.GetImage("FONT.ICN", character - 0x20);
            Assert.NotNull(white);

            Check(FontColor.Yellow, PaletteId.YellowFont);
            Check(FontColor.Gray, PaletteId.GrayFont);

            void Check(FontColor color, PaletteId paletteId)
            {
                Sprite face = FontCharHandler.GetChar(character, new FontType(FontSize.Normal, color));

                Assert.Equal(white!.Width, face.Width);
                Assert.Equal(white.Height, face.Height);
                Assert.Equal(white.OffsetX, face.OffsetX);
                Assert.Equal(white.OffsetY, face.OffsetY);

                // `CopyICNWithPalette` 就是逐像素过一次调色板表。
                ReadOnlySpan<byte> palette = PaletteTable.Get(paletteId);

                for (int y = 0; y < white.Height; ++y)
                {
                    for (int x = 0; x < white.Width; ++x)
                    {
                        Assert.Equal(palette[white.Image[x, y]], face.Image[x, y]);
                    }
                }
            }

            // The small font has the same two derived faces.
            Sprite smallYellow = FontCharHandler.GetChar(character, FontType.SmallYellow());
            Sprite? smallWhite = Assets.GetImage("SMALFONT.ICN", character - 0x20);
            Assert.NotNull(smallWhite);
            Assert.Equal(smallWhite!.Width, smallYellow.Width);
            Assert.Equal(smallWhite.Height, smallYellow.Height);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void DerivingAColouredFaceKeepsTheArchivedGlyphIntact()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            const byte character = (byte)'A';

            // Taking the coloured face first is the order that would corrupt the shared archive
            // cache if the palette were applied in place.
            Sprite yellow = FontCharHandler.GetChar(character, FontType.NormalYellow());
            Sprite white = FontCharHandler.GetChar(character, FontType.NormalWhite());
            Sprite? raw = Assets.GetImage("FONT.ICN", character - 0x20);
            Assert.NotNull(raw);

            Assert.Equal(raw!.Width, white.Width);
            Assert.Equal(raw.Height, white.Height);

            bool differs = false;

            for (int y = 0; y < raw.Height; ++y)
            {
                for (int x = 0; x < raw.Width; ++x)
                {
                    Assert.Equal(raw.Image[x, y], white.Image[x, y]);
                    differs |= raw.Image[x, y] != yellow.Image[x, y];
                }
            }

            Assert.True(differs, "the yellow face is identical to the white one");
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void CharacterLimitCountsTheImplicitSpaceFrames()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            // The reference prepends 32 empty frames so that an ASCII code can be used as a frame index.
            Assert.Equal(32u + (uint)Assets.GetImageCount("FONT.ICN"), FontCharHandler.GetCharacterLimit(FontSize.Normal));
            Assert.Equal(32u + (uint)Assets.GetImageCount("SMALFONT.ICN"), FontCharHandler.GetCharacterLimit(FontSize.Small));

            // The large face is grown from FONT.ICN, so it covers the same characters.
            Assert.Equal(FontCharHandler.GetCharacterLimit(FontSize.Normal), FontCharHandler.GetCharacterLimit(FontSize.Large));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheLargeFaceIsTheNormalOneDoubled()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            // `WHITE_LARGE_FONT` is FONT.ICN with every frame grown twice, position included.
            const byte character = (byte)'A';
            Sprite normal = FontCharHandler.GetChar(character, FontType.NormalWhite());
            Sprite large = FontCharHandler.GetChar(character, FontType.LargeWhite());

            Assert.Equal(normal.Width * 2, large.Width);
            Assert.Equal(normal.Height * 2, large.Height);
            Assert.Equal(normal.OffsetX * 2, large.OffsetX);
            Assert.Equal(normal.OffsetY * 2, large.OffsetY);

            for (int y = 0; y < large.Height; ++y)
            {
                for (int x = 0; x < large.Width; ++x)
                {
                    Assert.Equal(normal.Image[x / 2, y / 2], large.Image[x, y]);
                }
            }

            // The reference has no yellow or gray large font.
            Assert.Throws<NotSupportedException>(() =>
                FontCharHandler.GetChar(character, new FontType(FontSize.Large, FontColor.Yellow)));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void TheGradientFacesAreGrownFromTheWhiteGlyphs()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            const byte character = (byte)'A';
            Sprite normal = FontCharHandler.GetChar(character, FontType.NormalWhite());
            Sprite large = FontCharHandler.GetChar(character, FontType.LargeWhite());

            // `generateGradientFont` 的四组参数，见 `game_assets.cpp:2572`。
            Check(FontCharHandler.GetChar(character, new FontType(FontSize.Normal, FontColor.GoldenGradient)),
                normal, 108, 133, 55, 62);
            Check(FontCharHandler.GetChar(character, new FontType(FontSize.Normal, FontColor.SilverGradient)),
                normal, 10, 31, 29, 0);

            // 大号渐变从 WHITE_LARGE_FONT 长出来，渐变区间略窄。
            Check(FontCharHandler.GetChar(character, new FontType(FontSize.Large, FontColor.GoldenGradient)),
                large, 108, 127, 55, 62);
            Check(FontCharHandler.GetChar(character, new FontType(FontSize.Large, FontColor.SilverGradient)),
                large, 10, 26, 29, 0);

            // 参考只给普通号和大号做渐变面。
            Assert.Throws<NotSupportedException>(() =>
                FontCharHandler.GetChar(character, new FontType(FontSize.Small, FontColor.GoldenGradient)));

            void Check(Sprite face, Sprite baseline, int inner, int outer, int contourInner, int contourOuter)
            {
                // 留出 3 像素的边给两圈描边，位置左移上移 2 像素补偿。
                Assert.Equal(baseline.Width + 6, face.Width);
                Assert.Equal(baseline.Height + 6, face.Height);
                Assert.Equal(baseline.OffsetX - 2, face.OffsetX);
                Assert.Equal(baseline.OffsetY - 2, face.OffsetY);
                Assert.NotNull(face.Transform);

                int rampMin = Math.Min(inner, outer);
                int rampMax = Math.Max(inner, outer);
                int contourMin = Math.Min(contourInner, contourOuter);
                int contourMax = Math.Max(contourInner, contourOuter);

                HashSet<byte> ramp = new();

                for (int y = 0; y < face.Height; ++y)
                {
                    for (int x = 0; x < face.Width; ++x)
                    {
                        if (face.Transform![(y * face.Width) + x] != 0)
                        {
                            continue;
                        }

                        byte color = face.Image[x, y];

                        if (color >= rampMin && color <= rampMax)
                        {
                            ramp.Add(color);
                        }
                        else
                        {
                            // 只剩描边色：内描边、黑边（0）、外描边。
                            Assert.True(
                                color == 0 || (color >= contourMin && color <= contourMax),
                                $"unexpected colour {color} in the outline");
                        }
                    }
                }

                Assert.True(ramp.Count > 1, "the face carries no vertical gradient");
            }
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void EmptyTextHasNoSizeAndDrawsNothing()
    {
        Text text = new("", FontType.NormalWhite());

        Assert.True(text.IsEmpty);
        Assert.Equal(0, text.Width());
        Assert.Equal(0, text.Rows(100));
        Assert.Equal(new Rect(0, 0, 0, 0), text.Area());

        // The reference's height() only looks at the font size, so an empty text is still one line tall.
        Assert.Equal(15, text.Height());
    }

    [Fact]
    public void WidthIsTheSumOfTheGlyphAdvances()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            FontType fontType = FontType.NormalWhite();
            Text text = new("HELLO", fontType);
            FontCharHandler handler = new(fontType);

            int expected = 0;
            foreach (char character in "HELLO")
            {
                expected += handler.GetWidth((byte)character);
            }

            Assert.Equal(15, text.Height());
            Assert.Equal(expected, text.Width());
            Assert.Equal(1, text.Rows(1000));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void FitToOneRowCutsWithTheTruncationSymbol()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            FontType fontType = FontType.NormalWhite();
            Text text = new("A LONG LABEL THAT CANNOT FIT INTO A NARROW COLUMN", fontType);

            int maxWidth = 80;
            Assert.True(text.Width() > maxWidth);

            text.FitToOneRow(maxWidth);

            Assert.EndsWith("...", text.Content);
            Assert.True(text.Width() <= maxWidth, $"cut text is {text.Width()} px wide, limit was {maxWidth}");

            // A text that already fits is left alone.
            Text shortText = new("OK", fontType);
            string before = shortText.Content;
            shortText.FitToOneRow(maxWidth);
            Assert.Equal(before, shortText.Content);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void FitToAreaCutsTheTextToAHeightBudget()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            const int maxWidth = 120;
            const int maxHeight = 30;

            Text text = new("ONE TWO THREE FOUR FIVE SIX SEVEN EIGHT", FontType.NormalWhite());
            Assert.True(text.Height(maxWidth) > maxHeight, $"height at {maxWidth} px: {text.Height(maxWidth)}");

            text.FitToArea(maxWidth, maxHeight);

            Assert.EndsWith("...", text.Content);
            Assert.True(text.Height(maxWidth) <= maxHeight, $"cut text is {text.Height(maxWidth)} px tall");

            // A text that fits is left alone.
            Text shortText = new("OK", FontType.NormalWhite());
            shortText.FitToArea(maxWidth, maxHeight);
            Assert.Equal("OK", shortText.Content);

            // A non-positive limit is a no-op, matching `Text::fitToArea`.
            Text untouched = new("SOMETHING", FontType.NormalWhite());
            untouched.FitToArea(0, 0);
            Assert.Equal("SOMETHING", untouched.Content);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void LongTextIsWrappedIntoSeveralRows()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Text text = new("ONE TWO THREE FOUR FIVE SIX SEVEN EIGHT", FontType.NormalWhite());

            Assert.Equal(1, text.Rows(1000));
            Assert.True(text.Rows(60) > 1, $"rows at 60 px: {text.Rows(60)}, single line width: {text.Width()}");
            Assert.Equal(text.Rows(60) * text.Height(), text.Height(60));
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void DrawPaintsGlyphsInsideTheBuffer()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            PixelBuffer target = new(120, 40);
            target.Fill(0);

            Text text = new("HI", FontType.NormalWhite());
            text.Draw(4, 5, target);

            int painted = 0;
            for (int y = 0; y < target.Height; ++y)
            {
                for (int x = 0; x < target.Width; ++x)
                {
                    if (target[x, y] != 0)
                    {
                        ++painted;
                    }
                }
            }

            Assert.True(painted > 0, "the glyphs must be painted");
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void DrawCheckboxWithTextPaintsTheBoxAndTheLabel()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        IcnSpriteFile? cellwin = assets.TryReadIcn("CELLWIN.ICN");
        if (cellwin is null || cellwin.Count < 3)
        {
            return;
        }

        Sprite background = cellwin.Sprites[1];

        Assets.Current = assets;
        try
        {
            PixelBuffer target = new(200, 60);
            target.Fill(0);
            MovableSprite checkSprite = new();

            Rect area = EditorUiHelper.DrawCheckboxWithText(checkSprite, "CHECKBOX", target, 10, 6, false, 150);

            Assert.Equal(10, area.X);
            Assert.Equal(6, area.Y);
            Assert.Equal(background.Height, area.Height);

            Text label = new("CHECKBOX", FontType.NormalWhite());
            Assert.Equal(23 + label.Width(), area.Width);

            // The box itself lands at the given position.
            for (int row = 0; row < background.Height; ++row)
            {
                for (int column = 0; column < background.Width; ++column)
                {
                    Assert.Equal(background.Image[column, row], target[10 + column, 6 + row]);
                }
            }

            // The reference's operator= does not touch the visibility flag: a fresh MovableSprite stays hidden
            // and the caller shows it once the box is drawn.
            Assert.True(checkSprite.IsHidden);
            Assert.Equal(12, checkSprite.X);
            Assert.Equal(8, checkSprite.Y);
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void DrawCheckboxWithTextRejectsTheEvilBoxUntilItsGeneratorIsPorted()
    {
        PixelBuffer target = new(100, 40);
        MovableSprite checkSprite = new();

        Assert.Throws<NotSupportedException>(() =>
            EditorUiHelper.DrawCheckboxWithText(checkSprite, "x", target, 0, 0, true, 90));
    }

    [Fact]
    public void TheBaseFontsAreRepairedBeforeAnyFaceIsDerivedFromThem()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            Sprite slash = FontCharHandler.GetChar((byte)'/', FontType.NormalWhite());
            Sprite backslash = FontCharHandler.GetChar((byte)'\\', FontType.NormalWhite());
            Sprite bracket = FontCharHandler.GetChar((byte)'[', FontType.NormalWhite());
            Sprite bar = FontCharHandler.GetChar((byte)'|', FontType.NormalWhite());

            // '\' 归档里没有：modifyBaseNormalFont 造一张 8x14 的图，把 '/' 镜像过来。
            Assert.Equal(8, backslash.Width);
            Assert.Equal(14, backslash.Height);
            Assert.Equal(slash.OffsetX, backslash.OffsetX);
            Assert.Equal(slash.OffsetY, backslash.OffsetY);

            for (int y = 0; y < 12; ++y)
            {
                for (int x = 0; x < 7; ++x)
                {
                    int mirroredX = 1 + (6 - x);

                    if (backslash.Transform![(y * backslash.Width) + mirroredX] == 0)
                    {
                        Assert.Equal(slash.Image[x, y], backslash.Image[mirroredX, y]);
                    }
                }
            }

            // '|' 也没有：抄 '[' 的竖笔，高度比它多 3 像素。
            Assert.Equal(3, bar.Width);
            Assert.Equal(bracket.Height + 3, bar.Height);
            Assert.Equal(bracket.OffsetX, bar.OffsetX);
            Assert.Equal(bracket.OffsetY, bar.OffsetY);

            // '$' 与 '&' 的锚点被挪过。
            Assert.Equal(2, FontCharHandler.GetChar((byte)'$', FontType.NormalWhite()).OffsetY);
            Assert.Equal(1, FontCharHandler.GetChar((byte)'&', FontType.NormalWhite()).OffsetY);

            // 小号字体同样的三处：星号、反斜杠、小写 k。
            Assert.Equal(6, FontCharHandler.GetChar((byte)'*', FontType.SmallWhite()).Width);
            Assert.Equal(8, FontCharHandler.GetChar((byte)'*', FontType.SmallWhite()).Height);
            Assert.Equal(5, FontCharHandler.GetChar((byte)'\\', FontType.SmallWhite()).Width);
            Assert.Equal(9, FontCharHandler.GetChar((byte)'\\', FontType.SmallWhite()).Height);
            Assert.Equal(6, FontCharHandler.GetChar((byte)'k', FontType.SmallWhite()).Width);
            Assert.Equal(8, FontCharHandler.GetChar((byte)'k', FontType.SmallWhite()).Height);

            // 派生面是从修补后的表长出来的，所以黄/灰/大号也有这些字形。
            Assert.Equal(backslash.Width, FontCharHandler.GetChar((byte)'\\', FontType.NormalYellow()).Width);
            Assert.Equal(bar.Height, FontCharHandler.GetChar((byte)'|', new FontType(FontSize.Normal, FontColor.Gray)).Height);
            Assert.Equal(backslash.Width * 2, FontCharHandler.GetChar((byte)'\\', FontType.LargeWhite()).Width);

            // 没被点名的字形一个像素都不该动。
            Sprite raw = Assets.GetImage("FONT.ICN", 'A' - (int)FontCharHandler.GlyphFrameOffset)!;
            Sprite glyph = FontCharHandler.GetChar((byte)'A', FontType.NormalWhite());

            for (int y = 0; y < raw.Height; ++y)
            {
                for (int x = 0; x < raw.Width; ++x)
                {
                    Assert.Equal(raw.Image[x, y], glyph.Image[x, y]);
                    Assert.Equal(raw.Transform![(y * raw.Width) + x], glyph.Transform![(y * glyph.Width) + x]);
                }
            }
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void UnportedFontColoursAreRejected()
    {
        // 按钮字体只有白（good）和灰（evil）两种颜色，参考 `ui_text.cpp:61` 的断言里没有黄和渐变。
        Assert.Throws<NotSupportedException>(() =>
            FontCharHandler.GetChar((byte)'A', new FontType(FontSize.ButtonReleased, FontColor.Yellow)));
        Assert.Throws<NotSupportedException>(() =>
            FontCharHandler.GetChar((byte)'A', new FontType(FontSize.ButtonPressed, FontColor.GoldenGradient)));

        // The reference itself has no gray or yellow large font.
        Assert.Throws<NotSupportedException>(() =>
            FontCharHandler.GetChar((byte)'A', new FontType(FontSize.Large, FontColor.Gray)));
    }

    [Fact]
    public void TheButtonFacesAreDrawnFromScratch()
    {
        FontType released = new(FontSize.ButtonReleased, FontColor.White);
        FontType pressed = new(FontSize.ButtonPressed, FontColor.White);
        FontType evilReleased = new(FontSize.ButtonReleased, FontColor.Gray);
        FontType evilPressed = new(FontSize.ButtonPressed, FontColor.Gray);

        // 按钮字体是一整张 ASCII 表，字符就是下标，所以上限就是表长。
        Assert.Equal((uint)ButtonFont.AsciiCharSetSize, FontCharHandler.GetCharacterLimit(FontSize.ButtonReleased));
        Assert.Equal(16, FontCharHandler.GetFontHeight(FontSize.ButtonReleased));

        // '!' 在参考里是 2×10 的字身，四周留 2 像素给阴影和描边。
        foreach (FontType face in new[] { released, pressed, evilReleased, evilPressed })
        {
            Sprite glyph = FontCharHandler.GetChar((byte)'!', face);

            Assert.Equal(2 + 4, glyph.Width);
            Assert.Equal(10 + 4, glyph.Height);
        }

        // 四个面的笔画颜色各自不同：56 未按下、62 按下、30 / 36 是坏字面。
        Assert.True(ContainsVisibleColour(FontCharHandler.GetChar((byte)'A', released), 56));
        Assert.True(ContainsVisibleColour(FontCharHandler.GetChar((byte)'A', pressed), 62));
        Assert.True(ContainsVisibleColour(FontCharHandler.GetChar((byte)'A', evilReleased), 30));
        Assert.True(ContainsVisibleColour(FontCharHandler.GetChar((byte)'A', evilPressed), 36));

        // 参考只画大写：小写字母与 '$'、'@' 都是 1×1 的透明占位。
        Assert.Equal(1, FontCharHandler.GetChar((byte)'a', released).Width);
        Assert.Equal(1, FontCharHandler.GetChar((byte)'$', released).Height);
        Assert.Equal(1, FontCharHandler.GetChar((byte)'@', released).Width);

        // 未按下的字面比按下的大一圈：多一圈描边。
        Sprite releasedA = FontCharHandler.GetChar((byte)'A', released);
        Sprite pressedA = FontCharHandler.GetChar((byte)'A', pressed);

        Assert.True(CountVisibleContour(releasedA) > 0);
        Assert.Equal(0, CountVisibleContour(pressedA));
    }

    /// <summary>Whether a glyph draws at least one fully opaque pixel of <paramref name="colour"/>.</summary>
    private static bool ContainsVisibleColour(Sprite glyph, int colour) =>
        Enumerable.Range(0, glyph.Width * glyph.Height)
            .Any(index => glyph.Transform![index] == 0 && glyph.Image.Pixels[index] == colour);

    /// <summary>Number of visible pixels in the contour colour, the ring `addContour` draws.</summary>
    private static int CountVisibleContour(Sprite glyph) =>
        Enumerable.Range(0, glyph.Width * glyph.Height)
            .Count(index => glyph.Transform![index] == 0 && glyph.Image.Pixels[index] == 10);
}
