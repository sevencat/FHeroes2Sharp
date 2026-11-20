using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <c>fheroes2::ValueSelectionDialogElement</c> (<c>ui_dialog.cpp:937</c>): the 94x25 number element
/// built from <c>TOWNWIND.ICN</c> frames 4-8.
/// </summary>
public sealed class ValueSelectionElementTests
{
    private const byte Canvas = 0;
    private const byte EditBoxColor = 11;
    private const byte ArrowColor = 22;
    private const byte Glyph = 100;

    private static readonly Size EditBoxSize = new(68, 19);
    private static readonly Size ArrowSize = new(20, 10);
    private static readonly Size PressedArrowSize = new(19, 10);
    private static readonly Point ArrowAnchor = new(3, 2);

    [Fact]
    public void Constructor_LaysOutTheEditBoxAndBothArrows()
    {
        ValueSelectionElement element = CreateElement(new Point(10, 20));

        // 68 + 6 + 20 wide and 10 + 5 + 10 tall, the sizes of TOWNWIND.ICN.
        Assert.Equal(new Rect(10, 20, 94, 25), element.Area);

        // The box is vertically centered: (25 - 19) / 2 = 3.
        Assert.Equal(new Rect(10, 23, 68, 19), element.EditBox);

        // The anchor of the arrows (3,2) is subtracted, then the arrows sit right of the box.
        Assert.Equal(new Rect(10 + 68 + 6, 20, 20, 10), element.UpButton.Area);
        Assert.Equal(new Rect(10 + 68 + 6, 20 + 10 + 5, 20, 10), element.DownButton.Area);
    }

    [Fact]
    public void Constructor_ClampsTheInitialValue()
    {
        Assert.Equal(3, CreateElement(value: 3).Value);
        Assert.Equal(5, CreateElement(value: 500).Value);
        Assert.Equal(1, CreateElement(value: -7).Value);
    }

    [Fact]
    public void SetValue_Clamps()
    {
        ValueSelectionElement element = CreateElement();

        element.Value = 50;
        Assert.Equal(5, element.Value);

        element.Value = -50;
        Assert.Equal(1, element.Value);
    }

    [Fact]
    public void SetLeftButton_UpArrowAddsTheStep()
    {
        ValueSelectionElement element = CreateElement(value: 2);
        Point arrow = Center(element.UpButton.Area);

        Assert.False(element.SetLeftButton(true, arrow));
        Assert.True(element.IsUpPressed);
        Assert.Equal(2, element.Value);

        Assert.True(element.SetLeftButton(false, arrow));
        Assert.False(element.IsUpPressed);
        Assert.Equal(3, element.Value);
    }

    [Fact]
    public void SetLeftButton_DownArrowSubtractsTheStep()
    {
        ValueSelectionElement element = CreateElement(value: 2);
        Point arrow = Center(element.DownButton.Area);

        element.SetLeftButton(true, arrow);
        Assert.True(element.IsDownPressed);

        Assert.True(element.SetLeftButton(false, arrow));
        Assert.Equal(1, element.Value);
    }

    [Fact]
    public void SetLeftButton_StopsAtTheBounds()
    {
        ValueSelectionElement element = CreateElement(value: 5);
        Point arrow = Center(element.UpButton.Area);

        element.SetLeftButton(true, arrow);
        Assert.False(element.SetLeftButton(false, arrow));

        Assert.Equal(5, element.Value);

        element.Value = 1;
        Point down = Center(element.DownButton.Area);
        element.SetLeftButton(true, down);
        Assert.False(element.SetLeftButton(false, down));
        Assert.Equal(1, element.Value);
    }

    [Fact]
    public void SetLeftButton_IgnoresAReleaseOutsideTheArrow()
    {
        ValueSelectionElement element = CreateElement(value: 2);

        element.SetLeftButton(true, Center(element.UpButton.Area));

        Assert.False(element.SetLeftButton(false, new Point(0, 0)));
        Assert.Equal(2, element.Value);
        Assert.False(element.IsUpPressed);
    }

    [Fact]
    public void SetLeftButton_IgnoresTheEditBox()
    {
        ValueSelectionElement element = CreateElement(value: 2);

        Assert.False(element.SetLeftButton(true, Center(element.EditBox)));
        Assert.False(element.SetLeftButton(false, Center(element.EditBox)));
        Assert.Equal(2, element.Value);
    }

    [Fact]
    public void Constructor_RejectsABadRangeOrStep()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ValueSelectionElement(5, 1, 3, 1, new Point(0, 0), CreateFont(), CreateFrames()));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ValueSelectionElement(1, 5, 3, 0, new Point(0, 0), CreateFont(), CreateFrames()));
    }

    [Fact]
    public void Constructor_NeedsFiveFrames()
    {
        Assert.Throws<ArgumentException>(
            () => new ValueSelectionElement(1, 5, 3, 1, new Point(0, 0), CreateFont(), CreateFrames(4)));
    }

    [Fact]
    public void Constructor_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ValueSelectionElement(1, 5, 3, 1, new Point(0, 0), null!, CreateFrames()));
        Assert.Throws<ArgumentNullException>(
            () => new ValueSelectionElement(1, 5, 3, 1, new Point(0, 0), CreateFont(), null!));
    }

    [Fact]
    public void Draw_PaintsTheBoxTheNumberAndTheArrows()
    {
        ValueSelectionElement element = CreateElement(new Point(10, 20), value: 7);
        PixelBuffer target = new(200, 100);

        element.Draw(target);

        Assert.Equal(EditBoxColor, target[40, 30]);
        Assert.Equal(ArrowColor, target[10 + 68 + 6 + 5, 20 + 5]);
        Assert.Equal(ArrowColor, target[10 + 68 + 6 + 5, 20 + 10 + 5 + 5]);

        // The number is drawn with the glyph source: one solid block per digit, centered in the box.
        Assert.Contains(Glyph, EnumerateColors(target));
        Assert.Equal(Canvas, target[0, 0]);
    }

    [Fact]
    public void Draw_RejectsAMissingTarget()
    {
        Assert.Throws<ArgumentNullException>(() => CreateElement().Draw(null!));
    }

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

    private static Point Center(Rect area) => new(area.X + (area.Width / 2), area.Y + (area.Height / 2));

    private static ValueSelectionElement CreateElement(Point? offset = null, int value = 3) =>
        new(1, 5, value, 1, offset ?? new Point(0, 0), CreateFont(), CreateFrames());

    private static IGlyphSource CreateFont() => new BlockGlyphSource(Glyph);

    /// <summary>TOWNWIND.ICN frames 4-8 with their real sizes and anchors.</summary>
    private static IReadOnlyList<Sprite> CreateFrames(int count = ValueSelectionElement.RequiredFrameCount)
    {
        Sprite[] frames = new Sprite[count];

        if (count > ValueSelectionElement.EditBoxFrame)
        {
            frames[ValueSelectionElement.EditBoxFrame] =
                new Sprite(Filled(EditBoxSize, EditBoxColor), 0, 0);
        }

        for (int index = ValueSelectionElement.UpReleasedFrame; index < count; ++index)
        {
            Size size = index is ValueSelectionElement.UpPressedFrame or ValueSelectionElement.DownPressedFrame
                ? PressedArrowSize
                : ArrowSize;

            frames[index] = new Sprite(Filled(size, ArrowColor), ArrowAnchor.X, ArrowAnchor.Y);
        }

        return frames;
    }

    private static PixelBuffer Filled(Size size, byte color)
    {
        PixelBuffer buffer = new(size.Width, size.Height);
        buffer.Fill(color);

        return buffer;
    }

    /// <summary>A font whose glyphs are solid blocks of one colour.</summary>
    private sealed class BlockGlyphSource : IGlyphSource
    {
        private readonly Sprite _glyph;

        public BlockGlyphSource(byte color)
        {
            PixelBuffer image = new(6, 15);
            image.Fill(color);
            _glyph = new Sprite(image);
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
