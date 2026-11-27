using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

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
/// <b>Two ways in.</b> <see cref="ProcessEvents"/> is the reference's <c>processEvents()</c> and the one to use:
/// it takes the pressed look of both arrows from the frame's input, adds the step on a click, on a held arrow
/// (a <see cref="TimedEventValidator"/> repeats it after 500 ms and then every 100 ms), on the wheel and on the
/// up/down keys while the cursor is over the edit box, and reports the two dialogs the reference opens from
/// here — the number pad on a click of the box and the range message on a right click of it. <see cref="SetLeftButton"/>
/// is the older press/release pair for callers whose host only maps buttons to booleans; a caller uses one of the
/// two, never both, or a click would step twice.
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

    /// <summary>
    /// Body of the message a right click on the edit box shows, <c>:1014</c>; <c>%{min}</c> and <c>%{max}</c> are
    /// replaced with the range, which is what <see cref="RangeMessage"/> does.
    /// </summary>
    public const string RangeMessageFormat = "The available values range from %{min} to %{max}.";

    /// <summary>
    /// The size an element built from <paramref name="editBox"/> and <paramref name="upArrow"/> takes,
    /// <c>ValueSelectionDialogElement::getArea()</c> of the reference.
    /// </summary>
    /// <param name="editBox">The edit box, <c>TOWNWIND.ICN</c> frame <see cref="EditBoxFrame"/>.</param>
    /// <param name="upArrow">The released up arrow, <c>TOWNWIND.ICN</c> frame <see cref="UpReleasedFrame"/>.</param>
    /// <returns>Width of the box, the gap and the arrow; twice the arrow height plus the gap.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="editBox"/> or <paramref name="upArrow"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// 参考的 <c>getArea()</c> 返回的是写死的尺寸，调用方在创建控件之前就要用它来居中；
    /// 端口没有那张写死的表，改成从实际美术件算出同一组数，创建控件与布局测量共用它。
    /// </remarks>
    public static Size GetArea(Sprite editBox, Sprite upArrow)
    {
        ArgumentNullException.ThrowIfNull(editBox);
        ArgumentNullException.ThrowIfNull(upArrow);

        return new Size(
            editBox.Width + ArrowGap + upArrow.Width, (upArrow.Height * 2) + ButtonGap);
    }

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
    private readonly TimedEventValidator _upRepeat;
    private readonly TimedEventValidator _downRepeat;
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
    /// <param name="clockMs">
    /// Monotonic clock the two repeat timers read, or <see langword="null"/> for the system clock; tests drive
    /// the repeats with it.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="font"/> or <paramref name="frames"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="frames"/> is too short.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimum"/> is greater than <paramref name="maximum"/>, or <paramref name="step"/> is
    /// not positive.
    /// </exception>
    public ValueSelectionElement(int minimum, int maximum, int value, int step, Point offset,
        IGlyphSource font, IReadOnlyList<Sprite> frames, Func<long>? clockMs = null)
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

        _area = new Rect(offset, GetArea(_editBoxSprite, _upSprite));
        int areaHeight = _area.Height;
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

        // The reference subscribes the two validators to the buttons and keeps the default 500/100 ms.
        _upRepeat = new TimedEventValidator(clockMs: clockMs);
        _downRepeat = new TimedEventValidator(clockMs: clockMs);
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

    /// <summary>Smallest value the arrows stop at, <c>_minimum</c> of the reference.</summary>
    public int Minimum => _minimum;

    /// <summary>Largest value the arrows stop at, <c>_maximum</c> of the reference.</summary>
    public int Maximum => _maximum;

    /// <summary>
    /// Whether the wheel and the up/down keys act on the element wherever the cursor is,
    /// <c>setIgnoreMouseWheelEventRoiCheck()</c> of the reference.
    /// </summary>
    public bool IgnoreMouseWheelEventRoiCheck { get; set; }

    /// <summary>
    /// Body of the message a right click on the edit box shows, and the answer to the number pad's error,
    /// <c>"The available values range from %{min} to %{max}."</c> with the range filled in.
    /// </summary>
    public string RangeMessage => RangeMessageFormat
        .Replace("%{min}", _minimum.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("%{max}", _maximum.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Whether the up arrow is currently held down, <c>_buttonUp.isPressed()</c>.</summary>
    public bool IsUpPressed => _upButton.IsPressed;

    /// <summary>Whether the down arrow is currently held down, <c>_buttonDown.isPressed()</c>.</summary>
    public bool IsDownPressed => _downButton.IsPressed;

    /// <summary>
    /// A left button transition over the element, for hosts that map buttons to booleans instead of handing
    /// the frame's <see cref="LocalEvent"/> over to <see cref="ProcessEvents"/>.
    /// </summary>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns><see langword="true"/> when the value changed.</returns>
    /// <remarks>One press moves the value exactly once: the repeat lives in <see cref="ProcessEvents"/> only.</remarks>
    public bool SetLeftButton(bool down, Point point)
    {
        if (down)
        {
            if (_upButton.Area.Contains(point))
            {
                _upButton.Press();
            }
            else if (_downButton.Area.Contains(point))
            {
                _downButton.Press();
            }

            return false;
        }

        bool changed = false;

        if (_upButton.IsPressed && _upButton.Area.Contains(point))
        {
            changed = MoveBy(_step);
        }
        else if (_downButton.IsPressed && _downButton.Area.Contains(point))
        {
            changed = MoveBy(-_step);
        }

        _upButton.Release();
        _downButton.Release();

        return changed;
    }

    /// <summary>
    /// One frame of input, the reference's <c>processEvents()</c> (<c>ui_dialog.cpp:986-1019</c>).
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>
    /// Whether the value changed, and which dialog the reference would have opened here: the number pad on a
    /// click of the edit box, the range message on a right click of it.
    /// </returns>
    /// <remarks>
    /// 参考在这里直接开窗（<c>openVirtualNumpad()</c> 与 <c>showStandardTextMessage()</c>），端口把窗口交回宿主：
    /// 元素只说「要哪个」，范围与当前值由 <see cref="Minimum"/>、<see cref="Maximum"/>、<see cref="Value"/> 给出，
    /// 右键那段话就是 <see cref="RangeMessage"/>。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public ValueSelectionFrame ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // drawOnState( le.isMouseLeftButtonPressedAndHeldInArea( area() ) ) for both arrows.
        _upButton.UpdatePressState(events);
        _downButton.UpdatePressState(events);

        if (_value + _step <= _maximum
            && (events.MouseClickLeft(_upButton.Area)
                || IsIncreaseValueEvent(events)
                || _upRepeat.IsDelayPassed(IsUpPressed)))
        {
            _value += _step;

            return new ValueSelectionFrame(true);
        }

        if (_value - _step >= _minimum
            && (events.MouseClickLeft(_downButton.Area)
                || IsDecreaseValueEvent(events)
                || _downRepeat.IsDelayPassed(IsDownPressed)))
        {
            _value -= _step;

            return new ValueSelectionFrame(true);
        }

        if (events.MouseClickLeft(_editBox))
        {
            return new ValueSelectionFrame(false, ValueSelectionRequest.Numpad);
        }

        if (events.IsMouseRightButtonPressedInArea(_editBox))
        {
            return new ValueSelectionFrame(false, ValueSelectionRequest.Range);
        }

        return default;
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

    /// <summary>
    /// Whether this frame asks for a bigger value: the wheel up or the up key, the reference's
    /// <c>_isIncreaseValueEvent</c> (<c>:971-983</c>).
    /// </summary>
    /// <remarks>
    /// 参考问的是 <c>Game::HotKeyPressEvent( DEFAULT_UP )</c>，默认绑定就是上方向键；键位表还没接进来，端口直接认上键。
    /// 滚轮与按键只算落在数字框上，除非 <see cref="IgnoreMouseWheelEventRoiCheck"/> 把这条检查关掉。
    /// </remarks>
    private bool IsIncreaseValueEvent(LocalEvent events) =>
        IsValueEventArea(events) && (events.IsMouseWheelUp || events.IsKeyPressed(Key.Up));

    /// <summary>Whether this frame asks for a smaller value, the down half of <c>_isDecreaseValueEvent</c>.</summary>
    private bool IsDecreaseValueEvent(LocalEvent events) =>
        IsValueEventArea(events) && (events.IsMouseWheelDown || events.IsKeyPressed(Key.Down));

    private bool IsValueEventArea(LocalEvent events) =>
        IgnoreMouseWheelEventRoiCheck || events.IsMouseCursorPosInArea(_editBox);

    private bool MoveBy(int delta)
    {
        int previous = _value;
        _value = Math.Clamp(_value + delta, _minimum, _maximum);

        return _value != previous;
    }
}

/// <summary>
/// The dialog the reference opens from <c>ValueSelectionDialogElement::processEvents()</c> and the port leaves to
/// its host.
/// </summary>
public enum ValueSelectionRequest
{
    /// <summary>Nothing to open.</summary>
    None,

    /// <summary>The number pad of a click on the edit box, <c>openVirtualNumpad()</c>, <c>ui_dialog.cpp:1007</c>.</summary>
    Numpad,

    /// <summary>The range message of a right click on the edit box, <c>:1013-1016</c>.</summary>
    Range
}

/// <summary>What one frame of <see cref="ValueSelectionElement.ProcessEvents"/> asks its owner to do.</summary>
/// <param name="Changed">Whether the value changed, i.e. whether the window has to be repainted.</param>
/// <param name="Request">The dialog to open, or <see cref="ValueSelectionRequest.None"/>.</param>
public readonly record struct ValueSelectionFrame(
    bool Changed, ValueSelectionRequest Request = ValueSelectionRequest.None);
