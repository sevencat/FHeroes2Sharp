using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the castle screen's resource panel, <c>fheroes2::drawResourcePanel</c>
/// (<c>ui_castle.cpp:242-301</c>).
/// </summary>
public sealed class ResourcePanelTests
{
    private const byte PanelFill = 0;
    private const byte IconColour = 90;
    private const byte GlyphColour = 200;
    private const byte ExitColour = 55;

    private const int LineHeight = 8;
    private const int Advance = 6;

    [Fact]
    public void AreaIsTheReferenceRectangle()
    {
        Assert.Equal(new Rect(552, 262, 82, 192), ResourcePanel.GetArea(new Point(0, 0)));
        Assert.Equal(new Rect(559, 265, 82, 192), ResourcePanel.GetArea(new Point(7, 3)));
    }

    [Fact]
    public void RowsAreTheReferenceOffsets()
    {
        // 参考 :256：{ 0, maxHeight + fontHeight + 2, ( maxHeight + fontHeight ) * 2 - 1, ( maxHeight + fontHeight ) * 3 + 1 }
        Assert.Equal(new[] { 0, 42, 79, 121 }, ResourcePanel.GetRowOffsets(Font()));
    }

    [Fact]
    public void IconsAreBottomAlignedExceptGold()
    {
        Rect area = ResourcePanel.GetArea(new Point(0, 0));
        Sprite icon = Filled(20, 16, IconColour);

        // 左列 x = 552 + 1 + ( 39 - 20 ) / 2；右列再加 39 + 2。
        Assert.Equal(new Point(562, 278), ResourcePanel.GetIconPosition(area, 0, false, icon, Font()));
        Assert.Equal(new Point(603, 278), ResourcePanel.GetIconPosition(area, 0, true, icon, Font()));
        Assert.Equal(new Point(562, 320), ResourcePanel.GetIconPosition(area, 1, false, icon, Font()));
        Assert.Equal(new Point(562, 357), ResourcePanel.GetIconPosition(area, 2, false, icon, Font()));

        // 金子不底对齐：参考 :275 直接画在行顶，x 在整块面板里居中。
        Assert.Equal(new Point(583, 383), ResourcePanel.GetIconPosition(area, 3, false, icon, Font()));
    }

    [Fact]
    public void NumbersSitOnePixelUnderTheirCell()
    {
        Rect area = ResourcePanel.GetArea(new Point(0, 0));
        IGlyphSource font = Font();

        Assert.Equal(new Point(569, 295), ResourcePanel.GetNumberPosition(area, 0, false, "0", font));
        Assert.Equal(new Point(610, 295), ResourcePanel.GetNumberPosition(area, 0, true, "0", font));
        Assert.Equal(new Point(590, 416), ResourcePanel.GetNumberPosition(area, 3, false, "0", font));
    }

    [Fact]
    public void DrawFillsThePanelBeforeBlitting()
    {
        PixelBuffer target = new(640, 480);
        target.Fill(255);

        Rect area = ResourcePanel.Draw(target, Bag(), new Point(0, 0), Font(), Icons(), Filled(61, 19, ExitColour));

        Assert.Equal(new Rect(552, 262, 82, 192), area);
        Assert.Equal(PanelFill, target[552, 262]);
        Assert.Equal(PanelFill, target[633, 453]);
    }

    [Fact]
    public void DrawPutsTheIconsOfBothColumnsAndGold()
    {
        PixelBuffer target = new(640, 480);

        ResourcePanel.Draw(target, Bag(), new Point(0, 0), Font(), Icons(), Filled(61, 19, ExitColour));

        Assert.Equal(IconColour, target[562, 278]);
        Assert.Equal(IconColour, target[603, 278]);
        Assert.Equal(IconColour, target[562, 320]);
        Assert.Equal(IconColour, target[603, 320]);
        Assert.Equal(IconColour, target[562, 357]);
        Assert.Equal(IconColour, target[603, 357]);
        Assert.Equal(IconColour, target[583, 383]);
    }

    [Fact]
    public void DrawPutsTheNumbersUnderTheIcons()
    {
        PixelBuffer target = new(640, 480);

        ResourcePanel.Draw(target, Bag(), new Point(0, 0), Font(), Icons(), Filled(61, 19, ExitColour));

        Assert.Equal(GlyphColour, target[569, 295]);
        Assert.Equal(GlyphColour, target[610, 295]);
        Assert.Equal(GlyphColour, target[590, 416]);
    }

    [Fact]
    public void DrawPutsTheExitFrameAtTheBottomOfThePanel()
    {
        PixelBuffer target = new(640, 480);

        ResourcePanel.Draw(target, Bag(), new Point(0, 0), Font(), Icons(), Filled(61, 19, ExitColour));

        Assert.Equal(ExitColour, target[553, 428]);

        // 参考 :298 用的是 roi 内的固定偏移，不随 EXIT 帧的高度走。
        Assert.Equal(ExitColour, target[553, 262 + ResourcePanel.ExitFrameOffsetY]);
    }

    [Fact]
    public void DrawIsOffsetByTheScreenOrigin()
    {
        PixelBuffer target = new(640, 480);

        ResourcePanel.Draw(target, Bag(), new Point(7, 3), Font(), Icons(), Filled(61, 19, ExitColour));

        Assert.Equal(IconColour, target[569, 281]);
        Assert.Equal(ExitColour, target[560, 431]);
    }

    [Fact]
    public void DrawRejectsAWrongIconCount()
    {
        PixelBuffer target = new(640, 480);
        Sprite[] icons = [.. Icons()];

        Assert.Throws<ArgumentException>(
            () => ResourcePanel.Draw(target, Bag(), new Point(0, 0), Font(), icons[..6], Filled(61, 19, ExitColour)));
    }

    private static Funds Bag() => new()
    {
        Wood = 11,
        Mercury = 12,
        Ore = 13,
        Sulfur = 14,
        Crystal = 15,
        Gems = 16,
        Gold = 17,
    };

    private static Sprite[] Icons() =>
    [
        Filled(20, 16, IconColour),
        Filled(20, 16, IconColour),
        Filled(20, 16, IconColour),
        Filled(20, 16, IconColour),
        Filled(20, 16, IconColour),
        Filled(20, 16, IconColour),
        Filled(20, 16, IconColour),
    ];

    private static IGlyphSource Font() => new BlockFont(LineHeight, Advance, GlyphColour);

    private static Sprite Filled(int width, int height, byte colour)
    {
        PixelBuffer image = new(width, height);
        image.Fill(colour);

        return new Sprite(image, 0, 0);
    }

    /// <summary>A glyph source whose glyphs are solid blocks, used to measure the numbers.</summary>
    private sealed class BlockFont : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockFont(int lineHeight, int advance, byte colour)
        {
            LineHeight = lineHeight;
            SpaceWidth = advance;
            _glyph = Filled(advance, lineHeight, colour);
        }

        public int LineHeight { get; }

        public int SpaceWidth { get; }

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;

            return true;
        }
    }
}
