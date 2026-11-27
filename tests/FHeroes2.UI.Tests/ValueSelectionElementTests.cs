using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
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

    [Fact]
    public void ProcessEvents_ClickingAnArrowStepsOnce()
    {
        ValueSelectionElement element = CreateElement(value: 3);
        Point up = Center(element.UpButton.Area);

        ValueSelectionFrame frame = element.ProcessEvents(Click(up));

        Assert.True(frame.Changed);
        Assert.Equal(ValueSelectionRequest.None, frame.Request);
        Assert.Equal(4, element.Value);

        // 一次点击只走一步：参考的 processEvents 也只用 MouseClickLeft 计数。
        Assert.False(element.ProcessEvents(PointerAt(up)).Changed);
        Assert.Equal(4, element.Value);

        Assert.True(element.ProcessEvents(Click(Center(element.DownButton.Area))).Changed);
        Assert.Equal(3, element.Value);
    }

    [Fact]
    public void ProcessEvents_HoldingAnArrowRepeatsAfterTheDelays()
    {
        long now = 0;
        ValueSelectionElement element = CreateElement(value: 2, clockMs: () => now);
        Point up = Center(element.UpButton.Area);

        // 参考的两个默认值是按住 500 ms 之后开始，之后每 100 ms 一步。
        Assert.False(element.ProcessEvents(Press(up)).Changed);
        Assert.True(element.IsUpPressed);

        now = 499;
        Assert.False(element.ProcessEvents(Press(up)).Changed);
        Assert.Equal(2, element.Value);

        now = 500;
        Assert.True(element.ProcessEvents(Press(up)).Changed);
        Assert.Equal(3, element.Value);

        now = 599;
        Assert.False(element.ProcessEvents(Press(up)).Changed);

        now = 600;
        Assert.True(element.ProcessEvents(Press(up)).Changed);
        Assert.Equal(4, element.Value);

        // 松开之后不再重复，按下的样子也收掉。
        Assert.False(element.ProcessEvents(Release(up)).Changed);
        Assert.False(element.IsUpPressed);

        now = 5000;
        Assert.False(element.ProcessEvents(PointerAt(up)).Changed);
        Assert.Equal(4, element.Value);
    }

    [Fact]
    public void ProcessEvents_HoldingTheDownArrowRepeatsToo()
    {
        long now = 0;
        ValueSelectionElement element = CreateElement(value: 4, clockMs: () => now);
        Point down = Center(element.DownButton.Area);

        Assert.False(element.ProcessEvents(Press(down)).Changed);
        Assert.True(element.IsDownPressed);

        now = 500;
        Assert.True(element.ProcessEvents(Press(down)).Changed);
        Assert.Equal(3, element.Value);
    }

    [Fact]
    public void ProcessEvents_StopsAtTheBounds()
    {
        ValueSelectionElement element = CreateElement(value: 5);
        Point up = Center(element.UpButton.Area);

        Assert.False(element.ProcessEvents(Click(up)).Changed);
        Assert.Equal(5, element.Value);

        ValueSelectionElement low = CreateElement(value: 1);
        Assert.False(low.ProcessEvents(Click(Center(low.DownButton.Area))).Changed);
        Assert.Equal(1, low.Value);
    }

    [Fact]
    public void ProcessEvents_WheelCountsOverTheEditBoxOnly()
    {
        ValueSelectionElement element = CreateElement(value: 3);
        Point box = Center(element.EditBox);
        Point arrow = Center(element.UpButton.Area);

        // 参考的 _isIncreaseValueEvent 先看指针在不在数字框里。
        Assert.False(element.ProcessEvents(Wheel(arrow, 1)).Changed);
        Assert.Equal(3, element.Value);

        Assert.True(element.ProcessEvents(Wheel(box, 1)).Changed);
        Assert.Equal(4, element.Value);

        Assert.True(element.ProcessEvents(Wheel(box, -1)).Changed);
        Assert.Equal(3, element.Value);
    }

    [Fact]
    public void ProcessEvents_WheelCheckCanBeSwitchedOff()
    {
        ValueSelectionElement element = CreateElement(value: 3);
        element.IgnoreMouseWheelEventRoiCheck = true;

        // setIgnoreMouseWheelEventRoiCheck() 之后哪儿滚都算。
        Assert.True(element.ProcessEvents(Wheel(Center(element.UpButton.Area), 1)).Changed);
        Assert.Equal(4, element.Value);
    }

    [Fact]
    public void ProcessEvents_ArrowKeysStepInsideTheBoxOnly()
    {
        ValueSelectionElement element = CreateElement(value: 3);
        Point box = Center(element.EditBox);

        Assert.True(element.ProcessEvents(KeyAt(box, Key.Up)).Changed);
        Assert.Equal(4, element.Value);

        Assert.True(element.ProcessEvents(KeyAt(box, Key.Down)).Changed);
        Assert.Equal(3, element.Value);

        // 指针不在数字框上时上下键不算。
        Assert.False(element.ProcessEvents(KeyAt(Center(element.UpButton.Area), Key.Up)).Changed);
        Assert.Equal(3, element.Value);
    }

    [Fact]
    public void ProcessEvents_ClickingTheEditBoxAsksForTheNumberPad()
    {
        ValueSelectionElement element = CreateElement(value: 3);

        ValueSelectionFrame frame = element.ProcessEvents(Click(Center(element.EditBox)));

        Assert.False(frame.Changed);
        Assert.Equal(ValueSelectionRequest.Numpad, frame.Request);
        Assert.Equal(3, element.Value);
    }

    [Fact]
    public void ProcessEvents_RightClickingTheEditBoxAsksForTheRange()
    {
        ValueSelectionElement element = CreateElement(value: 3);

        ValueSelectionFrame frame = element.ProcessEvents(RightPress(Center(element.EditBox)));

        Assert.False(frame.Changed);
        Assert.Equal(ValueSelectionRequest.Range, frame.Request);

        Assert.Equal("The available values range from 1 to 5.", element.RangeMessage);
        Assert.Equal(1, element.Minimum);
        Assert.Equal(5, element.Maximum);

        // 右键落在别处不算。
        Assert.Equal(ValueSelectionRequest.None,
            element.ProcessEvents(RightPress(Center(element.UpButton.Area))).Request);
    }

    [Fact]
    public void ProcessEvents_RejectsMissingEvents()
    {
        Assert.Throws<ArgumentNullException>(() => CreateElement().ProcessEvents(null!));
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

    private static ValueSelectionElement CreateElement(Point? offset = null, int value = 3,
        Func<long>? clockMs = null) =>
        new(1, 5, value, 1, offset ?? new Point(0, 0), CreateFont(), CreateFrames(), clockMs);

    private static LocalEvent PointerAt(Point cursor)
    {
        LocalEvent events = new(() => 0);
        events.OnMouseMotion(cursor);

        return events;
    }

    private static LocalEvent Click(Point cursor)
    {
        LocalEvent events = PointerAt(cursor);
        events.OnMouseButton(true, MouseButtonType.Left, cursor);
        events.OnMouseButton(false, MouseButtonType.Left, cursor);

        return events;
    }

    private static LocalEvent Press(Point cursor)
    {
        LocalEvent events = PointerAt(cursor);
        events.OnMouseButton(true, MouseButtonType.Left, cursor);

        return events;
    }

    private static LocalEvent Release(Point cursor)
    {
        LocalEvent events = PointerAt(cursor);
        events.OnMouseButton(false, MouseButtonType.Left, cursor);

        return events;
    }

    private static LocalEvent RightPress(Point cursor)
    {
        LocalEvent events = PointerAt(cursor);
        events.OnMouseButton(true, MouseButtonType.Right, cursor);

        return events;
    }

    private static LocalEvent Wheel(Point cursor, int direction)
    {
        LocalEvent events = PointerAt(cursor);
        events.OnMouseWheel(new Point(0, direction));

        return events;
    }

    private static LocalEvent KeyAt(Point cursor, Key key)
    {
        LocalEvent events = PointerAt(cursor);
        events.OnKeyboard(key, KeyModifier.None, KeyboardEventState.Down);

        return events;
    }

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
