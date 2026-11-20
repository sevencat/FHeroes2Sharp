using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <c>Interface::displayStandardPopupWindow()</c> and <c>getPopupWindowPosition()</c>
/// (<c>src/fheroes2/gui/ui_map_interface.cpp:20-81</c>): the 158x94 <c>QWIKINFO.ICN</c> frame that hangs
/// off the tile under the cursor, with the text centered in the 126 pixels between its borders.
/// </summary>
public sealed class StandardPopupWindowTests
{
    private const byte Canvas = 0;
    private const byte FrameColor = 55;
    private const byte Glyph = 77;

    private static readonly Size FrameSize = new(158, 94);
    private static readonly Rect Interface = new(16, 16, 640, 480);

    [Fact]
    public void PositionOf_HangsTheWindowOffTheLowerRightCornerOfTheTile()
    {
        // Cursor at (100,100): the tile starts 16 pixels in, is 32 pixels wide, so the corner is (112,112)
        // and a 158x94 window centred on it starts at (33,65).
        Rect area = StandardPopupWindow.PositionOf(new Point(100, 100), Interface, FrameSize);

        Assert.Equal(new Rect(33, 65, 158, 94), area);

        // The centre of the window is the lower right corner of the tile, the property the reference
        // relies on to keep the popup next to the pointer.
        Assert.Equal(112, area.X + (area.Width / 2));
        Assert.Equal(112, area.Y + (area.Height / 2));
    }

    [Fact]
    public void PositionOf_ClampsToTheLeftAndTopBorders()
    {
        Rect area = StandardPopupWindow.PositionOf(PointOf(0, 0), Interface, FrameSize);

        Assert.Equal(Interface.X, area.X);
        Assert.Equal(Interface.Y, area.Y);
    }

    [Fact]
    public void PositionOf_ClampsToTheRightAndBottomBorders()
    {
        Rect area = StandardPopupWindow.PositionOf(PointOf(639, 479), Interface, FrameSize);

        Assert.Equal(Interface.Right - FrameSize.Width, area.X);
        Assert.Equal(Interface.Bottom - FrameSize.Height, area.Y);
    }

    [Fact]
    public void TextArea_IsCenteredBetweenTheTwentyTwoPixelInsetAndTheBorders()
    {
        StandardPopupWindow window = CreateWindow("Beach");

        // 158 - 2 * 16 = 126 wide, starting 22 pixels in.
        Assert.Equal(window.Area.X + StandardPopupWindow.TextInset, window.TextArea.X);
        Assert.Equal(126, window.TextArea.Width);

        // One 15 pixel line of the fake font: -6 + (94 - 15) / 2 = 33 pixels below the top of the frame.
        Assert.Equal(window.Area.Y + StandardPopupWindow.TextVerticalOffset + ((94 - 15) / 2), window.TextArea.Y);
        Assert.Equal(15, window.TextArea.Height);
    }

    [Fact]
    public void TextArea_GrowsWithTheWrappedLines()
    {
        StandardPopupWindow window = CreateWindow(new string('x', 60));

        // 21 glyphs of 6 pixels fit one 126 pixel line, so 60 of them take three lines.
        Assert.Equal(45, window.TextArea.Height);
    }

    [Fact]
    public void TryCreate_RefusesAFrameBiggerThanTheInterface()
    {
        Sprite frame = CreateFrame(new Size(700, 94));

        Assert.Null(StandardPopupWindow.TryCreate(
            frame, CreateFont(), "text", new Point(100, 100), Interface));
    }

    [Fact]
    public void TryCreate_RejectsMissingArguments()
    {
        Sprite frame = CreateFrame(FrameSize);

        Assert.Throws<ArgumentNullException>(() => StandardPopupWindow.TryCreate(
            null!, CreateFont(), "text", new Point(100, 100), Interface));
        Assert.Throws<ArgumentNullException>(() => StandardPopupWindow.TryCreate(
            frame, null!, "text", new Point(100, 100), Interface));
        Assert.Throws<ArgumentNullException>(() => StandardPopupWindow.TryCreate(
            frame, CreateFont(), null!, new Point(100, 100), Interface));
    }

    [Fact]
    public void Draw_PaintsTheFrameAndTheText()
    {
        StandardPopupWindow window = CreateWindow("Beach");
        PixelBuffer target = new(Interface.Width, Interface.Height);

        window.Draw(target);

        Assert.Equal(FrameColor, target[window.Area.X + 1, window.Area.Y + 1]);
        Assert.Contains(Glyph, EnumerateColors(target));
        Assert.Equal(Canvas, target[0, 0]);
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        Assert.Throws<ArgumentNullException>(() => CreateWindow("Beach").Draw(null!));
    }

    private static Point PointOf(int x, int y) => new(x, y);

    private static StandardPopupWindow CreateWindow(string text) =>
        StandardPopupWindow.TryCreate(
            CreateFrame(FrameSize), CreateFont(), text, new Point(100, 100), Interface)!;

    private static Sprite CreateFrame(Size size)
    {
        PixelBuffer pixels = new(size.Width, size.Height);
        pixels.Fill(FrameColor);

        return new Sprite(pixels);
    }

    private static IGlyphSource CreateFont() => new BlockGlyphSource();

    private static IEnumerable<byte> EnumerateColors(PixelBuffer target)
    {
        for (int y = 0; y < target.Height; ++y)
        {
            for (int x = 0; x < target.Width; ++x)
            {
                yield return target[x, y];
            }
        }
    }

    /// <summary>A 6x15 solid block font, wide enough to measure a wrapped line.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockGlyphSource()
        {
            PixelBuffer pixels = new(6, 15);
            pixels.Fill(Glyph);
            _glyph = new Sprite(pixels);
        }

        public int LineHeight => 15;

        public int SpaceWidth => 6;

        public bool IsAvailable(char character) => true;

        public int GetAdvance(char character) => SpaceWidth;

        public bool TryGetGlyph(char character, out Sprite glyph)
        {
            glyph = _glyph;
            return true;
        }
    }
}
