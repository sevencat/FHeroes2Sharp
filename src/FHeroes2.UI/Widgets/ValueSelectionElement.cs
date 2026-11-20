using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A number in an edit box with a pair of arrow buttons, a port of
/// <c>fheroes2::ValueSelectionDialogElement</c> (<c>src/fheroes2/gui/ui_dialog.cpp:937</c>).
/// </summary>
/// <remarks>
/// <para>
/// The element is a fixed 94x25 block: the 68x19 edit box of <c>TOWNWIND.ICN</c> frame 4 with a 6 pixel gap
/// to the two 20x10 arrows of frames 5-8, which stack on top of each other with a 5 pixel gap. The number
/// is drawn in white, centered in the box.
/// </para>
/// <para>
/// The reference also auto-repeats a held arrow every 150 milliseconds (<c>TimedEventValidator</c>). The
/// port has no frame clock in the widget layer and the callers that need the repeat drive it from their
/// own loop, so a press here moves the value exactly once.
/// </para>
/// <para>
/// A press that leaves the arrow before the button is released does not change the value, the
/// <c>isMouseLeftButtonPressedAndHeldInArea</c> rule of <c>processEvents()</c>.
/// </para>
/// </remarks>
public sealed class ValueSelectionElement
{
    /// <summary>Frame of the edit box in <c>TOWNWIND.ICN</c>.</summary>
    public const int EditBoxFrame = 4;

    /// <summary>Frame of the released up arrow in <c>TOWNWIND.ICN</c>.</summary>
    public const int UpReleasedFrame = 5;

    /// <summary>Frame of the pressed up arrow in <c>TOWNWIND.ICN</c>.</summary>
    public const int UpPressedFrame = 6;

    /// <summary>Frame of the released down arrow in <c>TOWNWIND.ICN</c>.</summary>
    public const int DownReleasedFrame = 7;

    /// <summary>Frame of the pressed down arrow in <c>TOWNWIND.ICN</c>.</summary>
    public const int DownPressedFrame = 8;

    /// <summary>Number of <c>TOWNWIND.ICN</c> frames this element needs.</summary>
    public const int RequiredFrameCount = DownPressedFrame + 1;

    /// <summary>Gap between the edit box and the arrows.</summary>
    public const int ArrowGap = 6;

    /// <summary>Gap between the two arrows.</summary>
    public const int ButtonGap = 5;

    private readonly IGlyphSource _font;
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int _step;
    private readonly Sprite _editBoxSprite;
    private readonly Sprite _upSprite;
    private readonly Rect _editBox;
    private readonly Rect _area;
    private readonly ButtonSprite _upButton;
    private readonly ButtonSprite _downButton;
    private bool _upPressed;
    private bool _downPressed;
    private int _value;

    /// <summary>
    /// Builds the element.
    /// </summary>
    /// <param name="minimum">Smallest value the arrows stop at.</param>
    /// <param name="maximum">Largest value the arrows stop at.</param>
    /// <param name="value">Value shown first, clamped into the range.</param>
    /// <param name="step">Amount one press changes the value by.</param>
    /// <param name="offset">Top left corner of the element.</param>
    /// <param name="font">Font of the number, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="frames">Frames of <c>TOWNWIND.ICN</c>, at least <see cref="RequiredFrameCount"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="font"/> or <paramref name="frames"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="frames"/> is too short.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimum"/> is greater than <paramref name="maximum"/>, or <paramref name="step"/> is
    /// not positive.
    /// </exception>
    public ValueSelectionElement(int minimum, int maximum, int value, int step, Point offset,
        IGlyphSource font, IReadOnlyList<Sprite> frames)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count < RequiredFrameCount)
        {
            throw new ArgumentException(
                $"The element needs {RequiredFrameCount} TOWNWIND frames, got {frames.Count}.", nameof(frames));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);

        _font = font;
        _minimum = minimum;
        _maximum = maximum;
        _step = step;
        _value = Math.Clamp(value, minimum, maximum);

        _editBoxSprite = frames[EditBoxFrame];
        _upSprite = frames[UpReleasedFrame];

        int areaWidth = _editBoxSprite.Width + ArrowGap + _upSprite.Width;
        int areaHeight = (_upSprite.Height * 2) + ButtonGap;
        _area = new Rect(offset, new Size(areaWidth, areaHeight));
        _editBox = new Rect(
            offset.X, offset.Y + ((areaHeight - _editBoxSprite.Height) / 2),
            _editBoxSprite.Width, _editBoxSprite.Height);

        // The arrows carry their own anchor in the ICN header; the reference subtracts it so that the
        // visible arrow lands on the element origin, then stacks the second one below the first.
        _upButton = new ButtonSprite(
            frames[UpReleasedFrame], frames[UpPressedFrame],
            offsetX: _editBox.X + _editBox.Width + ArrowGap - _upSprite.OffsetX,
            offsetY: offset.Y - _upSprite.OffsetY);
        _downButton = new ButtonSprite(
            frames[DownReleasedFrame], frames[DownPressedFrame],
            offsetX: _upButton.OffsetX,
            offsetY: _upButton.OffsetY + _upSprite.Height + ButtonGap);
    }

    /// <summary>Bounds of the whole element, <c>getArea()</c> of the reference.</summary>
    public Rect Area => _area;

    /// <summary>Bounds of the edit box that holds the number.</summary>
    public Rect EditBox => _editBox;

    /// <summary>The up arrow.</summary>
    public ButtonBase UpButton => _upButton;

    /// <summary>The down arrow.</summary>
    public ButtonBase DownButton => _downButton;

    /// <summary>The number shown, always inside the range.</summary>
    public int Value
    {
        get => _value;
        set => _value = Math.Clamp(value, _minimum, _maximum);
    }

    /// <summary>Amount one press changes the value by.</summary>
    public int Step => _step;

    /// <summary>Whether the up arrow is currently held down.</summary>
    public bool IsUpPressed => _upPressed;

    /// <summary>Whether the down arrow is currently held down.</summary>
    public bool IsDownPressed => _downPressed;

    /// <summary>
    /// A left button transition over the element.
    /// </summary>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns><see langword="true"/> when the value changed.</returns>
    public bool SetLeftButton(bool down, Point point)
    {
        if (down)
        {
            _upPressed = _upButton.Area.Contains(point);
            _downPressed = _downButton.Area.Contains(point);

            if (_upPressed)
            {
                _upButton.Press();
            }
            else if (_downPressed)
            {
                _downButton.Press();
            }

            return false;
        }

        bool changed = false;

        if (_upPressed && _upButton.Area.Contains(point))
        {
            changed = MoveBy(_step);
        }
        else if (_downPressed && _downButton.Area.Contains(point))
        {
            changed = MoveBy(-_step);
        }

        _upPressed = false;
        _downPressed = false;
        _upButton.Release();
        _downButton.Release();

        return changed;
    }

    /// <summary>Draws the edit box, the number and both arrows.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, _editBox.Position, _editBoxSprite);

        string text = _value.ToString(CultureInfo.InvariantCulture);
        int textWidth = GlyphText.Measure(_font, text);

        GlyphText.DrawLine(
            _font,
            target,
            new Point(
                _editBox.X + ((_editBox.Width - textWidth) / 2),
                _editBox.Y + ((_editBox.Height - _font.LineHeight) / 2)),
            text);

        _upButton.Draw(target);
        _downButton.Draw(target);
    }

    private bool MoveBy(int delta)
    {
        int previous = _value;
        _value = Math.Clamp(_value + delta, _minimum, _maximum);

        return _value != previous;
    }
}
