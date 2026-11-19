using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A horizontal slider with two step arrows and a draggable thumb, a port of
/// <c>fheroes2::HorizontalSlider</c> (<c>src/fheroes2/gui/ui_slider.h:37-77</c>,
/// <c>ui_slider.cpp:40-155</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference builds the whole widget out of six frames of <c>TRADPOST.ICN</c> and paints it into the display
/// inside the constructor. The port keeps the same frame table — frame 1 is the 230 pixel groove, frame 2 the thumb
/// artwork, frames 3/4 and 5/6 the left and right arrows — but composes the groove into a sprite
/// (<see cref="Background"/>) instead of blitting it, so the slider can live inside any window.
/// </para>
/// <para>
/// <b>The groove is cut, not repeated.</b> The reference takes the first <c>24 + width / 2</c> pixels of the artwork
/// and the last <c>19 + width - width / 2</c> pixels, which is why a slider of a width other than 230 still has the
/// original end caps. The two halves meet in the middle with no seam only because the source is a solid bar.
/// </para>
/// <para>
/// <b>Events answer «did the value change».</b> The reference returns <see langword="true"/> only when the thumb moved
/// or the index changed, and the two arrows report <see langword="false"/> at the ends of the range — a window that
/// redraws on the answer therefore never repaints for a click that did nothing. Dragging the thumb keeps that
/// property: the press is consumed by the drag branch, so a click that lands on the thumb does not also step it.
/// </para>
/// </remarks>
public sealed class Slider
{
    /// <summary>Width the reference's slider was drawn for, <c>230</c> in <c>ui_slider.cpp:50</c>.</summary>
    public const int DefaultWidth = 230;

    /// <summary>Width of both arrow buttons, <c>buttonWidth</c> of the reference.</summary>
    public const int ArrowWidth = 15;

    /// <summary>Room left of the groove, <c>leftOffset</c> of the reference.</summary>
    public const int LeftOffset = 24;

    /// <summary>Room right of the groove, <c>rightOffset</c> of the reference.</summary>
    public const int RightOffset = 19;

    /// <summary>Height of the thumb and of the area it travels in, <c>11</c> in <c>ui_slider.cpp:74</c>.</summary>
    public const int ThumbHeight = 11;

    /// <summary>Vertical distance of the thumb area from the top of the slider.</summary>
    public const int ThumbOffsetY = 3;

    /// <summary>Frames the widget needs: groove, thumb, two arrow pairs.</summary>
    public const int RequiredSpriteCount = 7;

    private const int BarFrame = 1;
    private const int ThumbFrame = 2;
    private const int LeftReleasedFrame = 3;
    private const int LeftPressedFrame = 4;
    private const int RightReleasedFrame = 5;
    private const int RightPressedFrame = 6;

    private readonly ScrollBar _scrollBar = new();
    private readonly ButtonSprite _leftButton;
    private readonly ButtonSprite _rightButton;
    private readonly TimedEventValidator _leftRepeat = new();
    private readonly TimedEventValidator _rightRepeat = new();

    /// <summary>
    /// The thumb artwork as it came out of the ICN, <c>_originalImage</c> of the reference. <see cref="SetRange"/>
    /// stretches <b>this</b> frame and not the current one, otherwise every range change would stretch an already
    /// stretched thumb and the bevel would smear.
    /// </summary>
    private readonly Sprite _originalThumb;

    private Sprite? _enabledThumb;
    private Sprite? _disabledThumb;
    private Sprite? _disabledThumbSource;

    /// <summary>
    /// Creates the slider, <c>HorizontalSlider( width, position, minIndex, maxIndex, currentIndex )</c>
    /// (<c>ui_slider.cpp:40</c>).
    /// </summary>
    /// <param name="sprites">
    /// Frames of <c>TRADPOSE.ICN</c> / <c>TRADPOST.ICN</c>, at least <see cref="RequiredSpriteCount"/> of them.
    /// </param>
    /// <param name="width">Length of the groove, <see cref="DefaultWidth"/> for the original look.</param>
    /// <param name="position">Top left corner of the slider in screen coordinates.</param>
    /// <param name="minIndex">Lowest selectable value.</param>
    /// <param name="maxIndex">Highest selectable value.</param>
    /// <param name="currentIndex">Value the thumb starts on, inside the range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sprites"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sprites"/> holds fewer than <see cref="RequiredSpriteCount"/> frames.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> is not positive, <paramref name="minIndex"/> is above <paramref name="maxIndex"/>, or
    /// <paramref name="currentIndex"/> is outside the range; the reference asserts on the last two.
    /// </exception>
    public Slider(IReadOnlyList<Sprite> sprites, int width, Point position, int minIndex, int maxIndex, int currentIndex)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The groove has to be at least one pixel long.");
        }

        if (sprites.Count < RequiredSpriteCount)
        {
            throw new ArgumentException(
                $"The slider needs {RequiredSpriteCount} frames, got {sprites.Count}.", nameof(sprites));
        }

        if (minIndex > maxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(minIndex), minIndex, "The lower bound cannot be above the upper one.");
        }

        if (currentIndex < minIndex || currentIndex > maxIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentIndex), currentIndex, "The starting value has to be inside the range.");
        }

        Position = position;
        Width = width;

        Sprite bar = sprites[BarFrame];
        int leftArea = LeftOffset + (width / 2);
        int rightArea = RightOffset + width - (width / 2);

        Background = new Sprite(width + LeftOffset + RightOffset, bar.Height);
        ImageOps.Copy(bar, new Rect(0, 0, leftArea, bar.Height), Background, new Point(0, 0));
        ImageOps.Copy(bar, new Rect(bar.Width - rightArea, 0, rightArea, bar.Height), Background, new Point(leftArea, 0));

        _leftButton = new ButtonSprite(
            sprites[LeftReleasedFrame], sprites[LeftPressedFrame], offsetX: position.X + 6, offsetY: position.Y + 1);

        _rightButton = new ButtonSprite(
            sprites[RightReleasedFrame],
            sprites[RightPressedFrame],
            offsetX: position.X + LeftOffset + width + RightOffset - ArrowWidth,
            offsetY: position.Y + 1);

        Sprite thumb = sprites[ThumbFrame];
        _originalThumb = thumb;

        _scrollBar.SetSlider(ScrollSlider.Create(
            thumb,
            horizontal: true,
            areaLength: width,
            visibleCount: 1,
            totalCount: maxIndex - minIndex + 1,
            startArea: new Rect(0, 0, 2, thumb.Height),
            middleArea: new Rect(2, 0, 8, thumb.Height)));
        _scrollBar.Area = new Rect(position.X + LeftOffset, position.Y + ThumbOffsetY, width, ThumbHeight);
        _scrollBar.SetRange(minIndex, maxIndex);
        _scrollBar.MoveToIndex(currentIndex);
    }

    /// <summary>Top left corner of the slider, the <c>position</c> of the reference constructor.</summary>
    public Point Position { get; }

    /// <summary>Length of the groove in pixels.</summary>
    public int Width { get; }

    /// <summary>The groove with both end caps, ready to be blitted at <see cref="Position"/>.</summary>
    public Sprite Background { get; }

    /// <summary>The value the thumb stands for, <c>getCurrentValue()</c> of the reference.</summary>
    public int CurrentValue => _scrollBar.CurrentIndex;

    /// <summary>Lower bound of the range.</summary>
    public int MinValue => _scrollBar.MinIndex;

    /// <summary>Upper bound of the range.</summary>
    public int MaxValue => _scrollBar.MaxIndex;

    /// <summary>
    /// The groove the thumb travels in — <b>not</b> the thumb itself — <c>_scrollbar.getArea()</c> of the reference.
    /// A drag, a wheel step and the right click popup all use this area.
    /// </summary>
    public Rect GrooveArea => _scrollBar.Area;

    /// <summary>Where the thumb currently sits, <c>_scrollbar.x()</c> / <c>position()</c>.</summary>
    public Point ThumbPosition => _scrollBar.Position;

    /// <summary>The thumb artwork currently in use, darkened while the slider is disabled.</summary>
    public Sprite? Thumb => _scrollBar.Slider;

    /// <summary>The left step arrow, exposed so a caller can draw it with the rest of the window.</summary>
    public ButtonSprite LeftButton => _leftButton;

    /// <summary>The right step arrow.</summary>
    public ButtonSprite RightButton => _rightButton;

    /// <summary>Whether the arrows accept input; the thumb is not affected by the reference either.</summary>
    public bool IsEnabled => _leftButton.IsEnabled && _rightButton.IsEnabled;

    /// <summary>Title of the right click popup, <c>setPopupDialog</c> of the reference.</summary>
    public string PopupTitle { get; set; } = string.Empty;

    /// <summary>Body of the right click popup; a popup needs both strings.</summary>
    public string PopupDescription { get; set; } = string.Empty;

    /// <summary>
    /// Rebuilds the thumb for a new range, <c>setRange()</c> (<c>ui_slider.cpp:81</c>): the thumb is regenerated for
    /// the new count, the current value is clamped into the range and the thumb is laid out again.
    /// </summary>
    /// <param name="minIndex">New lower bound.</param>
    /// <param name="maxIndex">New upper bound.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minIndex"/> is above <paramref name="maxIndex"/>.</exception>
    public void SetRange(int minIndex, int maxIndex)
    {
        if (minIndex > maxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(minIndex), minIndex, "The lower bound cannot be above the upper one.");
        }

        Sprite stretched = ScrollSlider.Create(
            _originalThumb,
            horizontal: true,
            areaLength: Width,
            visibleCount: 1,
            totalCount: maxIndex - minIndex + 1,
            startArea: new Rect(0, 0, 2, _originalThumb.Height),
            middleArea: new Rect(2, 0, 8, _originalThumb.Height));

        _scrollBar.SetSlider(stretched);
        _enabledThumb = stretched;

        int current = Math.Min(_scrollBar.CurrentIndex, maxIndex);

        _scrollBar.SetRange(minIndex, maxIndex);
        _scrollBar.MoveToIndex(current);
    }

    /// <summary>
    /// Processes one frame of input, <c>processEvents()</c> (<c>ui_slider.cpp:91</c>).
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>
    /// Whether the value changed, plus the right click popup of <see cref="PopupTitle"/> if one was requested.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public SliderFrame ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // The reference's processEvents() has no disabled check: it relies on the dialog not feeding events to a
        // disabled slider, and a click on `_buttonLeft.area()` still steps it. The port stops here instead, because a
        // caller that forgets the check would get a slider that visibly does nothing at its ends but jumps when the
        // thumb is dragged. The arrows are disabled by `Disable()`; the thumb follows the arrows.
        if (!IsEnabled)
        {
            return default;
        }

        _leftButton.UpdatePressState(events);
        _rightButton.UpdatePressState(events);

        if (events.IsMouseLeftButtonPressedInArea(_scrollBar.Area))
        {
            int previousValue = _scrollBar.CurrentIndex;
            Point previousPosition = _scrollBar.Position;

            _scrollBar.MoveToPosition(events.CursorPosition);

            bool changed = previousValue != _scrollBar.CurrentIndex || previousPosition != _scrollBar.Position;

            return new SliderFrame(changed);
        }

        if (events.MouseClickLeft(_leftButton.Area) || events.IsMouseWheelDownInArea(_scrollBar.Area)
            || _leftRepeat.IsDelayPassed(_leftButton.IsPressed))
        {
            if (_scrollBar.CurrentIndex == _scrollBar.MinIndex)
            {
                return new SliderFrame(false);
            }

            _scrollBar.Backward();

            return new SliderFrame(true);
        }

        if (events.MouseClickLeft(_rightButton.Area) || events.IsMouseWheelUpInArea(_scrollBar.Area)
            || _rightRepeat.IsDelayPassed(_rightButton.IsPressed))
        {
            if (_scrollBar.CurrentIndex == _scrollBar.MaxIndex)
            {
                return new SliderFrame(false);
            }

            _scrollBar.Forward();

            return new SliderFrame(true);
        }

        if (PopupTitle.Length > 0 && PopupDescription.Length > 0 && events.IsMouseRightButtonPressedInArea(_scrollBar.Area))
        {
            return new SliderFrame(false, new SliderTooltip(PopupTitle, PopupDescription));
        }

        return new SliderFrame(false);
    }

    /// <summary>
    /// Draws the groove, both arrows and the thumb, the painting the reference does in its constructor and with
    /// <c>_scrollbar.show()</c>. Everything is placed in absolute screen coordinates, exactly like the reference,
    /// so a window that owns a slider builds <see cref="Position"/> from its own origin.
    /// </summary>
    /// <param name="target">Indexed frame buffer of the display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ImageOps.BlitSprite(target, Position, Background);
        _leftButton.Draw(target);
        _rightButton.Draw(target);

        if (_scrollBar.Slider is { } thumb)
        {
            ImageOps.BlitSprite(target, _scrollBar.Position, thumb);
        }
    }

    /// <summary>
    /// Darkens the thumb and disables both arrows, <c>disable()</c> (<c>ui_slider.cpp:130</c>).
    /// </summary>
    public void Disable()
    {
        _leftButton.Disable();
        _rightButton.Disable();

        Sprite? thumb = _scrollBar.Slider;

        if (thumb is null)
        {
            return;
        }

        _enabledThumb = thumb;

        if (_disabledThumb is null || !ReferenceEquals(_disabledThumbSource, thumb))
        {
            _disabledThumbSource = thumb;
            _disabledThumb = thumb.Clone();
            ImageOps.ApplyPalette(_disabledThumb.Image, PaletteId.Darkening);
        }

        _scrollBar.SetSlider(_disabledThumb);
        _scrollBar.UpdatePosition();
    }

    /// <summary>
    /// Restores the original artwork and enables both arrows, <c>enable()</c> (<c>ui_slider.cpp:143</c>).
    /// </summary>
    public void Enable()
    {
        _leftButton.Enable();
        _rightButton.Enable();

        if (_enabledThumb is null)
        {
            return;
        }

        _scrollBar.SetSlider(_enabledThumb);
        _scrollBar.UpdatePosition();
    }
}

/// <summary>The right click popup of a <see cref="Slider"/>, the title and body of a standard message.</summary>
/// <param name="Title">Header of the popup, <c>_popupTitle</c> of the reference.</param>
/// <param name="Text">Body of the popup, <c>_popupDescription</c>.</param>
public readonly record struct SliderTooltip(string Title, string Text);

/// <summary>What one frame of <see cref="Slider.ProcessEvents"/> asks its owner to do.</summary>
/// <param name="Changed">Whether the value changed, i.e. whether the window has to be repainted.</param>
/// <param name="Tooltip">The popup a right click asked for, or <see langword="null"/>.</param>
public readonly record struct SliderFrame(bool Changed, SliderTooltip? Tooltip = null);
