using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The slider of a scroll bar, a port of <c>fheroes2::Scrollbar</c> (<c>src/fheroes2/gui/ui_scrollbar.h:33-93</c>,
/// <c>ui_scrollbar.cpp:33-119</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference makes the scroll bar a sprite: the slider <b>is</b> the image, its size is the size of the thumb and
/// its position inside the groove is the state. The port keeps the same three numbers (area, thumb size, position) but
/// no drawing, because every window of the port composes its own frame.
/// </para>
/// <para>
/// The mapping index → pixel is integer arithmetic and <b>truncates</b>, exactly like the reference:
/// <c>( current - min ) * room / ( max - min )</c>. Replacing it with rounding moves the thumb by a pixel on some
/// lengths, and <see cref="MoveToPosition"/> has its own <c>+ length / 2</c> for the way back — that asymmetry is in
/// the reference and has to stay.
/// </para>
/// <para>
/// A range with <c>min == max</c> means «there is nothing to scroll»: the thumb is centered and
/// <see cref="MoveToIndex"/> never reports a move, which is what <c>SetRange</c> does before the first
/// <c>moveToIndex( 0 )</c> of a list.
/// </para>
/// </remarks>
public sealed class ScrollBar
{
    /// <summary>The groove the thumb travels in, <c>_area</c> of the reference.</summary>
    private Rect _area;

    /// <summary>Lower bound of the index range, <c>_minIndex</c>.</summary>
    private int _minIndex;

    /// <summary>Upper bound of the index range, <c>_maxIndex</c>.</summary>
    private int _maxIndex;

    /// <summary>Index the thumb currently stands for, <c>_currentIndex</c>.</summary>
    private int _currentIndex;

    /// <summary>
    /// The thumb artwork, or <see langword="null"/> while nothing has been set. The reference inherits it from
    /// <c>Image</c>; the port only needs its size, and hands the sprite back to whoever draws the bar.
    /// </summary>
    private Sprite? _slider;

    /// <summary>Where the thumb sits, the sprite position of the reference.</summary>
    public Point Position { get; private set; }

    /// <summary>The groove the thumb travels in, <c>setArea()</c>.</summary>
    public Rect Area
    {
        get => _area;
        set => _area = value;
    }

    /// <summary>The artwork of the thumb, <c>setImage()</c> of the reference.</summary>
    /// <remarks>
    /// The reference stores the image in itself, so a new image also changes the thumb size and the next
    /// <see cref="MoveToIndex"/> lays the thumb out for the new size. The port does the same: the caller draws
    /// <see cref="Slider"/> at <see cref="Position"/>.
    /// </remarks>
    public Sprite? Slider => _slider;

    /// <summary>Width of the thumb, <c>width()</c> of the reference.</summary>
    public int SliderWidth => _slider?.Width ?? 0;

    /// <summary>Height of the thumb, <c>height()</c> of the reference.</summary>
    public int SliderHeight => _slider?.Height ?? 0;

    /// <summary>Lower bound of the index range, <c>minIndex()</c>.</summary>
    public int MinIndex => _minIndex;

    /// <summary>Upper bound of the index range, <c>maxIndex()</c>.</summary>
    public int MaxIndex => _maxIndex;

    /// <summary>Index of the thumb, <c>currentIndex()</c>.</summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Whether the bar runs vertically, <c>isVertical()</c> of the reference: decided by the groove being taller than
    /// it is wide, not by any flag the caller sets.
    /// </summary>
    public bool IsVertical => _area.Width < _area.Height;

    /// <summary>Replaces the thumb artwork, <c>setImage()</c> of the reference.</summary>
    /// <param name="slider">
    /// The thumb, or <see langword="null"/> to forget it. A null thumb keeps <see cref="MoveToIndex"/> working — it only
    /// loses the size that centers it.
    /// </param>
    public void SetSlider(Sprite? slider) => _slider = slider;

    /// <summary>
    /// Sets the index range and puts the thumb at the start of it, <c>setRange()</c> (<c>ui_scrollbar.cpp:33</c>).
    /// </summary>
    /// <param name="minIndex">Lower bound, <c>minIndex</c>.</param>
    /// <param name="maxIndex">Upper bound, <c>maxIndex</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxIndex"/> is smaller than <paramref name="minIndex"/>; the reference asserts on it.
    /// </exception>
    public void SetRange(int minIndex, int maxIndex)
    {
        if (maxIndex < minIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIndex), maxIndex, "The upper bound cannot be below the lower one.");
        }

        _minIndex = minIndex;
        _maxIndex = maxIndex;
        _currentIndex = _minIndex;

        int roomX = _area.Width - SliderWidth;
        int roomY = _area.Height - SliderHeight;

        if (_maxIndex == _minIndex)
        {
            Position = new Point(_area.X + (roomX / 2), _area.Y + (roomY / 2));
        }
        else if (IsVertical)
        {
            Position = new Point(_area.X + (roomX / 2), _area.Y);
        }
        else
        {
            Position = new Point(_area.X, _area.Y + (roomY / 2));
        }
    }

    /// <summary>Moves the thumb one index forward, <c>forward()</c>.</summary>
    public void Forward()
    {
        if (_currentIndex < _maxIndex)
        {
            MoveToIndex(_currentIndex + 1);
        }
    }

    /// <summary>Moves the thumb one index backward, <c>backward()</c>.</summary>
    public void Backward()
    {
        if (_currentIndex > _minIndex)
        {
            MoveToIndex(_currentIndex - 1);
        }
    }

    /// <summary>
    /// Moves the thumb to <paramref name="index"/>, <c>moveToIndex()</c> (<c>ui_scrollbar.cpp:68</c>).
    /// </summary>
    /// <param name="index">Wanted index; it is clamped into the range.</param>
    /// <returns>
    /// <see langword="true"/> when the thumb position changed. A single-entry range always answers
    /// <see langword="false"/> — the reference centers the thumb there and calls it «nothing moved», so a caller that
    /// redraws only on <see langword="true"/> never repaints a bar with nothing to scroll.
    /// </returns>
    public bool MoveToIndex(int index)
    {
        int roomX = _area.Width - SliderWidth;
        int roomY = _area.Height - SliderHeight;

        if (_maxIndex == _minIndex)
        {
            Position = new Point(_area.X + (roomX / 2), _area.Y + (roomY / 2));

            return false;
        }

        _currentIndex = Math.Clamp(index, _minIndex, _maxIndex);

        Point next = IsVertical
            ? new Point(_area.X + (roomX / 2), _area.Y + ((_currentIndex - _minIndex) * roomY / (_maxIndex - _minIndex)))
            : new Point(_area.X + ((_currentIndex - _minIndex) * roomX / (_maxIndex - _minIndex)), _area.Y + (roomY / 2));

        if (next.X == Position.X && next.Y == Position.Y)
        {
            return false;
        }

        Position = next;

        return true;
    }

    /// <summary>
    /// Moves the thumb under the cursor, <c>moveToPos()</c> (<c>ui_scrollbar.cpp:105</c>).
    /// </summary>
    /// <remarks>
    /// The cursor is turned into an index with the thumb <b>centred</b> on it — half a thumb is subtracted — and the
    /// division adds <c>length / 2</c> so the nearest index wins instead of the one below. A range with nothing to
    /// scroll ignores the request, and <see cref="CurrentIndex"/> does not change when the computed index is the one
    /// the thumb already stands for.
    /// </remarks>
    /// <param name="position">Cursor position in logical pixels.</param>
    public void MoveToPosition(Point position)
    {
        if (_maxIndex == _minIndex)
        {
            return;
        }

        int length = IsVertical ? _area.Height - SliderHeight : _area.Width - SliderWidth;

        if (length <= 0)
        {
            return;
        }

        int offset = IsVertical
            ? Math.Clamp(position.Y - (SliderHeight / 2) - _area.Y, 0, length)
            : Math.Clamp(position.X - (SliderWidth / 2) - _area.X, 0, length);

        int index = (((_maxIndex - _minIndex) * offset) + (length / 2)) / length + _minIndex;

        if (index != _currentIndex)
        {
            MoveToIndex(index);
        }
    }

    /// <summary>
    /// Re-lays the thumb out for the current index, <c>updatePosition()</c> of the reference — what a caller does after
    /// swapping the artwork.
    /// </summary>
    /// <returns><see langword="true"/> when the position changed.</returns>
    public bool UpdatePosition() => MoveToIndex(_currentIndex);
}
