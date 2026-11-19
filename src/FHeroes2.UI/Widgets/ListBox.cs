using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A scrolling list of rows, a port of <c>Interface::ListBox&lt;Item&gt;</c>
/// (<c>src/fheroes2/gui/interface_list.h:54-667</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference mixes three things in one class: the selection and scroll state machine, the scroll bar with its two
/// step buttons, and the drawing of the rows. The port keeps the same split between a generic state machine and the
/// subclass, but two globals are gone: the rows are drawn into the frame buffer the caller passes instead of a
/// singleton display, and the pressed look of the two step buttons is updated from the frame's
/// <see cref="LocalEvent"/> by <see cref="ButtonBase.UpdatePressState"/> instead of being painted on the spot.
/// </para>
/// <para>
/// <b>The row geometry is integer arithmetic.</b> A row at index <c>id</c> starts at
/// <c>ItemsArea.Y + ( id - TopId ) * ItemsArea.Height / VisibleItemCount</c>, and the reverse mapping used by the mouse
/// truncates as well. When the area is not an exact multiple of the row count the last row is therefore a pixel or two
/// short rather than overlapped — that is the reference's layout, and rounding it changes the hit boxes.
/// </para>
/// <para>
/// <b>Scroll by dragging the list</b> is a separate path from the scroll bar: the reference registers a drag, remembers
/// where it started and, once the cursor has moved past one row, moves the bar by whole rows and sets
/// <c>_lockClick</c> so that finishing the drag is not taken as a click on a row
/// (<c>interface_list.h:497-519</c>). Both that flag and the remembered point reset on the next press.
/// </para>
/// </remarks>
/// <typeparam name="T">The row. A reference type, because the reference hands rows out by pointer.</typeparam>
public abstract class ListBox<T> : ListBoxBase
    where T : class
{
    /// <summary>Where the list is painted, <c>ptRedraw</c> of the reference.</summary>
    private Point _redrawPoint;

    /// <summary>The rows, <c>content</c> of the reference; the list does not own them.</summary>
    private List<T>? _content;

    /// <summary>How many rows fit at once, <c>maxItems</c>.</summary>
    private int _maxItems;

    /// <summary>Where the last drag started, <c>_dragStartPos</c>.</summary>
    private Point _dragStartPosition;

    /// <summary>Whether the keyboard moves the selection, <c>useHotkeys</c>.</summary>
    private bool _useHotkeys = true;

    /// <summary>Whether the thumb still has to be laid out after a drag, <c>_updateScrollbar</c>.</summary>
    private bool _updateScrollbar;

    /// <summary>Whether the running drag must not end as a click, <c>_lockClick</c>.</summary>
    private bool _lockClick;

    /// <summary>The repeat of the step-up button, <c>_timedButtonPgUp</c>.</summary>
    private readonly TimedEventValidator _timedButtonPageUp = new();

    /// <summary>The repeat of the step-down button, <c>_timedButtonPgDn</c>.</summary>
    private readonly TimedEventValidator _timedButtonPageDown = new();

    /// <summary>Creates an empty list, <c>ListBox( const fheroes2::Point &amp; pt )</c>.</summary>
    /// <param name="redrawPoint">Top-left corner the background is painted at, <c>ptRedraw</c>.</param>
    protected ListBox(Point redrawPoint)
    {
        _redrawPoint = redrawPoint;
    }

    /// <summary>Paints one row, <c>RedrawItem()</c>.</summary>
    /// <param name="item">The row to paint.</param>
    /// <param name="x">Left edge of the row.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is the selected one.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public abstract void RedrawItem(T item, int x, int y, bool current, PixelBuffer output);

    /// <summary>Paints everything behind the rows — header, frames, scroll bar groove — <c>RedrawBackground()</c>.</summary>
    /// <param name="destination">Top-left corner of the list, <c>ptRedraw</c>.</param>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public abstract void RedrawBackground(Point destination, PixelBuffer output);

    /// <summary>Called after the selection moved up by a key, <c>ActionCurrentUp()</c>.</summary>
    public abstract void ActionCurrentUp();

    /// <summary>Called after the selection moved down by a key, <c>ActionCurrentDn()</c>.</summary>
    public abstract void ActionCurrentDn();

    /// <summary>Called when the left button is held on a row, <c>ActionListLongPress()</c>.</summary>
    /// <param name="item">The row under the cursor.</param>
    public virtual void ActionListLongPress(T item)
    {
        // Do nothing, as in the reference.
    }

    /// <summary>Called on a click that selects another row, <c>ActionListSingleClick( Item &amp; )</c>.</summary>
    /// <param name="item">The clicked row, already selected.</param>
    public abstract void ActionListSingleClick(T item);

    /// <summary>Called on a click on the already selected row, <c>ActionListDoubleClick( Item &amp; )</c>.</summary>
    /// <param name="item">The clicked row.</param>
    public abstract void ActionListDoubleClick(T item);

    /// <summary>Called on a right click, <c>ActionListPressRight( Item &amp; )</c>.</summary>
    /// <param name="item">The clicked row.</param>
    public abstract void ActionListPressRight(T item);

    /// <summary>
    /// The click with the cursor position and the row origin, <c>ActionListSingleClick( Item &amp;,
    /// const fheroes2::Point &amp;, int32_t, int32_t )</c>.
    /// </summary>
    /// <remarks>
    /// The overloads exist because a list whose rows contain small widgets has to know <b>where</b> inside the row the
    /// click landed. The reference's default implementations drop the extra arguments; the port keeps that so a plain
    /// text list only implements three methods.
    /// </remarks>
    /// <param name="item">The clicked row, already selected.</param>
    /// <param name="mousePosition">Cursor position.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public virtual void ActionListSingleClick(T item, Point mousePosition, int offsetX, int offsetY) =>
        ActionListSingleClick(item);

    /// <summary>The double click with the cursor and the row origin, <c>ActionListDoubleClick( ... )</c>.</summary>
    /// <param name="item">The clicked row.</param>
    /// <param name="mousePosition">Cursor position.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public virtual void ActionListDoubleClick(T item, Point mousePosition, int offsetX, int offsetY) =>
        ActionListDoubleClick(item);

    /// <summary>The right click with the cursor and the row origin, <c>ActionListPressRight( ... )</c>.</summary>
    /// <param name="item">The clicked row.</param>
    /// <param name="mousePosition">Cursor position.</param>
    /// <param name="offsetX">Left edge of the row.</param>
    /// <param name="offsetY">Top edge of the row.</param>
    public virtual void ActionListPressRight(T item, Point mousePosition, int offsetX, int offsetY) =>
        ActionListPressRight(item);

    /// <summary>
    /// Called for every frame the cursor spends on a row, before the click is looked at, <c>ActionListCursor()</c>.
    /// </summary>
    /// <param name="item">The row under the cursor.</param>
    /// <param name="cursor">Cursor position.</param>
    /// <returns><see langword="true"/> when the row consumed the frame.</returns>
    public virtual bool ActionListCursor(T item, Point cursor) => false;

    /// <summary>Moves where the list is painted, <c>SetTopLeft()</c>.</summary>
    /// <param name="topLeft">New top-left corner.</param>
    public void SetTopLeft(Point topLeft) => _redrawPoint = topLeft;

    /// <summary>Sets the step-up button, <c>SetScrollButtonUp()</c>.</summary>
    /// <remarks>
    /// The reference takes the ICN and two frame indices here because it loads the artwork itself. The port has no
    /// asset table in the widget layer, so the caller builds the <see cref="ButtonSprite"/> and passes it in; the
    /// position comes with it.
    /// </remarks>
    /// <param name="button">The button, or <see langword="null"/> to remove it.</param>
    public void SetScrollButtonUp(ButtonBase? button) => ButtonPageUp = button;

    /// <summary>Sets the step-down button, <c>SetScrollButtonDn()</c>.</summary>
    /// <param name="button">The button, or <see langword="null"/> to remove it.</param>
    public void SetScrollButtonDown(ButtonBase? button) => ButtonPageDown = button;

    /// <summary>Sets the groove the thumb travels in, <c>setScrollBarArea()</c>.</summary>
    /// <param name="area">The groove.</param>
    public void SetScrollBarArea(Rect area) => ScrollBar.Area = area;

    /// <summary>Sets the thumb artwork, <c>setScrollBarImage()</c>.</summary>
    /// <param name="image">The thumb, usually from <see cref="ScrollSlider.Create"/>.</param>
    public void SetScrollBarImage(Sprite? image) => ScrollBar.SetSlider(image);

    /// <summary>
    /// Rebuilds the thumb for the current row count and re-lays it out, <c>updateScrollBarImage()</c>.
    /// </summary>
    /// <remarks>
    /// The reference stretches the artwork it already holds, using its own width for the caps and rows 0-7 / 7-15 of
    /// the original slider. The port does the same through <see cref="ScrollSlider.Create"/>; a list without artwork
    /// does nothing, which is what the reference would do with an empty image.
    /// </remarks>
    public void UpdateScrollBarImage()
    {
        if (ScrollBar.Slider is not { } slider)
        {
            return;
        }

        int width = slider.Width;

        SetScrollBarImage(ScrollSlider.Create(slider, false, ScrollBar.Area.Height, VisibleItemCount, Size,
            new Rect(0, 0, width, 8), new Rect(0, 7, width, 8)));
        ScrollBar.MoveToIndex(TopId);
    }

    /// <summary>The scroll bar itself, <c>GetScrollbar()</c>.</summary>
    public ScrollBar ScrollBar { get; } = new();

    /// <summary>The step-up button, <c>_buttonPgUp</c>.</summary>
    protected ButtonBase? ButtonPageUp { get; private set; }

    /// <summary>The step-down button, <c>_buttonPgDn</c>.</summary>
    protected ButtonBase? ButtonPageDown { get; private set; }

    /// <summary>How many rows are visible at once, <c>VisibleItemCount()</c>.</summary>
    /// <remarks>
    /// The reference keeps this protected; the port makes it readable from outside because a dialog that lays out the
    /// frame around the list needs the same number and would otherwise have to duplicate it.
    /// </remarks>
    public int VisibleItemCount => _maxItems;

    /// <summary>How many rows the list holds, <c>_size()</c>.</summary>
    public int Size => _content?.Count ?? 0;

    /// <summary>The area the rows are painted in, <c>rtAreaItems</c>.</summary>
    public Rect ItemsArea { get; private set; }

    /// <summary>Whether the list has anything to show, <c>IsValid()</c>.</summary>
    public bool IsValid =>
        _content is { Count: > 0 } && TopId >= 0 && TopId < Size && CurrentId < Size && _maxItems > 0;

    /// <summary>Whether the list has to be painted again, <c>IsNeedRedraw()</c>.</summary>
    public override bool IsNeedRedraw => NeedRedraw;

    /// <summary>The row the selection points at, <c>GetCurrent()</c>; only valid while <see cref="IsValid"/>.</summary>
    public T GetCurrent() => _content![CurrentId];

    /// <summary>How many rows fit at once, <c>SetAreaMaxItems()</c>.</summary>
    /// <param name="maxValue">Row count.</param>
    public void SetAreaMaxItems(int maxValue)
    {
        _maxItems = maxValue;
        Reset();
    }

    /// <summary>Sets the area the rows are painted in, <c>SetAreaItems()</c>.</summary>
    /// <param name="area">The area.</param>
    public void SetAreaItems(Rect area) => ItemsArea = area;

    /// <summary>Points the list at new rows, <c>SetListContent()</c>.</summary>
    /// <remarks>
    /// The reference stores a pointer to the vector, so the caller keeps ownership and the list sees later changes at
    /// once. The port takes the same <see cref="List{T}"/> instead of a copy, for the same reason:
    /// <see cref="RemoveSelected"/> edits it.
    /// </remarks>
    /// <param name="list">The rows.</param>
    public void SetListContent(List<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        _content = list;
        Reset();

        if (IsValid)
        {
            CurrentId = 0;
        }
    }

    /// <summary>Forgets the selection and the scroll position, <c>Reset()</c>.</summary>
    public void Reset()
    {
        if (_content is not { Count: > 0 })
        {
            CurrentId = -1;
            TopId = -1;
            ScrollBar.SetRange(0, 0);
        }
        else
        {
            CurrentId = -1; // no selection
            TopId = 0;

            int maxTop = Math.Max(Size - _maxItems, 0);
            ScrollBar.SetRange(0, maxTop);
        }
    }

    /// <summary>Turns the keyboard shortcuts off or on, <c>DisableHotkeys()</c>.</summary>
    /// <param name="disable">Whether the shortcuts are off.</param>
    public void DisableHotkeys(bool disable) => _useHotkeys = !disable;

    /// <summary>Paints the background, the two buttons, the thumb and every visible row, <c>Redraw()</c>.</summary>
    /// <param name="output">Indexed frame buffer of the scene.</param>
    public override void Redraw(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        NeedRedraw = false;

        RedrawBackground(_redrawPoint, output);

        ButtonPageUp?.Draw(output);
        ButtonPageDown?.Draw(output);

        if (ScrollBar.Slider is { } slider)
        {
            ImageOps.BlitSprite(output, ScrollBar.Position, slider);
        }

        Verify(); // reset values if they are wrong

        if (!IsValid)
        {
            return;
        }

        int end = TopId + _maxItems > Size ? Size - TopId : TopId + _maxItems;

        for (int id = TopId; id < end; ++id)
        {
            RedrawItem(_content![id], ItemsArea.X, ItemsArea.Y + ((id - TopId) * ItemsArea.Height / _maxItems),
                id == CurrentId, output);
        }
    }

    /// <summary>The row under <paramref name="position"/>, <c>GetFromPosition()</c>.</summary>
    /// <param name="position">Cursor position.</param>
    /// <returns>The row, or <see langword="null"/> when the cursor is outside the rows or below the last one.</returns>
    public T? GetFromPosition(Point position)
    {
        Verify();

        if (!IsValid)
        {
            return null;
        }

        if (position.Y < ItemsArea.Y || position.Y >= ItemsArea.Y + ItemsArea.Height)
        {
            return null;
        }

        if (position.X < ItemsArea.X || position.X >= ItemsArea.X + ItemsArea.Width)
        {
            return null;
        }

        int id = (position.Y - ItemsArea.Y) * _maxItems / ItemsArea.Height;

        if (TopId + id >= Size)
        {
            return null;
        }

        return _content![TopId + id];
    }

    /// <summary>Selects the row with this index, <c>SetCurrent( size_t )</c>.</summary>
    /// <param name="position">Row index; ignored when the list is shorter.</param>
    public void SetCurrent(int position)
    {
        if (_content is not null && position >= 0 && position < _content.Count)
        {
            CurrentId = position;
        }

        SetCurrentVisible();
    }

    /// <summary>Scrolls so that the selected row is visible, <c>SetCurrentVisible()</c>.</summary>
    /// <remarks>
    /// The row is put on top when the selection is above the view and at the bottom when it is below it, so a single
    /// step of the arrow keys scrolls by one row and not by a whole page. A list that has become invalid is reset
    /// instead of scrolled.
    /// </remarks>
    public void SetCurrentVisible()
    {
        Verify();

        if (!IsValid)
        {
            Reset();

            return;
        }

        if (CurrentId >= 0 && (TopId > CurrentId || TopId + _maxItems <= CurrentId))
        {
            if (TopId > CurrentId)
            {
                TopId = CurrentId;
            }
            else if (TopId + _maxItems <= CurrentId)
            {
                TopId = CurrentId + 1 - _maxItems;
            }
        }

        UpdateScrollbarRange();
        ScrollBar.MoveToIndex(TopId);
    }

    /// <summary>Scrolls so that this row is the first one visible, <c>setTopVisibleItem()</c>.</summary>
    /// <remarks>
    /// The argument is clamped into <c>[0, size - visible]</c>, so the list can never be scrolled past its end and a
    /// saved <c>-1</c> (an empty list of the previous session) simply shows the top.
    /// </remarks>
    /// <param name="topId">Wanted first visible row.</param>
    public void SetTopVisibleItem(int topId)
    {
        Verify();

        if (!IsValid)
        {
            Reset();

            return;
        }

        TopId = Math.Max(0, Math.Min(topId, Size - _maxItems));

        UpdateScrollbarRange();
        ScrollBar.MoveToIndex(TopId);
    }

    /// <summary>Selects this row, <c>SetCurrent( const Item &amp; )</c>.</summary>
    /// <param name="item">The row; a row that is not in the list resets the list.</param>
    public void SetCurrent(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        int position = _content?.IndexOf(item) ?? -1;

        if (position < 0)
        {
            Reset();
        }
        else
        {
            CurrentId = position;
        }

        SetCurrentVisible();
    }

    /// <summary>Removes the selected row from the list, <c>RemoveSelected()</c>.</summary>
    /// <remarks>
    /// The scroll position is corrected by hand: a removed row above the view moves the view up by one, and removing the
    /// last row moves the selection up by one. Without those two fixes the list would show a gap or lose the selection.
    /// </remarks>
    public void RemoveSelected()
    {
        if (_content is null || CurrentId < 0 || CurrentId >= Size)
        {
            return;
        }

        _content.RemoveAt(CurrentId);

        if (CurrentId < TopId && TopId > 0)
        {
            --TopId;
        }

        if (CurrentId >= Size)
        {
            --CurrentId;
        }

        NeedRedraw = true;
    }

    /// <summary>Whether a row is selected, <c>isSelected()</c>.</summary>
    public bool IsSelected => IsValid && CurrentId >= 0;

    /// <summary>Drops the selection, <c>Unselect()</c>.</summary>
    public void Unselect()
    {
        Verify();

        if (IsValid)
        {
            CurrentId = -1;

            NeedRedraw = true;
        }
    }

    /// <summary>Whether the list is supposed to be painted again, <c>needRedraw</c>.</summary>
    protected bool NeedRedraw { get; set; }

    /// <summary>The last visible row, <c>_lastVisibleId()</c>.</summary>
    protected int LastVisibleId => Math.Min(TopId + _maxItems, Size) - 1;

    /// <summary>Consumes one frame of input, <c>QueueEventProcessing()</c>.</summary>
    /// <remarks>
    /// <para>
    /// The order is the reference's and matters: the two step buttons take their pressed look first (even with an empty
    /// list), then the keyboard, then the wheel and the held buttons, then the long press, then the press-and-drag
    /// block, and only then the hover and click on the rows. The press block returns early when the thumb did not move,
    /// which is what keeps a plain click from being treated as a drag.
    /// </para>
    /// <para>
    /// A held step button repeats through <see cref="TimedEventValidator"/>, exactly like the arrows of the castle
    /// windows; the validator is driven with the button's own pressed state, which
    /// <see cref="ButtonBase.UpdatePressState"/> has just set.
    /// </para>
    /// <para>
    /// One guard is new: a row area too small for the row count would divide by zero in the drag path. The reference
    /// would crash there, so the port skips the drag instead of reproducing an impossible layout.
    /// </para>
    /// </remarks>
    /// <param name="events">Input state of the frame.</param>
    /// <returns><see langword="true"/> when the list changed and has to be painted again.</returns>
    public override bool QueueEventProcessing(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        ButtonPageUp?.UpdatePressState(events);
        ButtonPageDown?.UpdatePressState(events);

        if (!IsValid)
        {
            return false;
        }

        if (_useHotkeys && events.IsKeyPressed(Key.PageUp))
        {
            SetCurrentVisible();

            if (TopId > 0)
            {
                NeedRedraw = true;

                int previousTop = TopId;
                TopId = TopId > _maxItems ? TopId - _maxItems : 0;

                UpdateScrollbarRange();
                ScrollBar.MoveToIndex(TopId);
                CurrentId = Math.Clamp(CurrentId - (previousTop - TopId), TopId, LastVisibleId);
            }
            else if (CurrentId != 0)
            {
                // The view is at the top of the list, but the selection is not.
                CurrentId = 0;
                NeedRedraw = true;
            }

            return true;
        }

        if (_useHotkeys && events.IsKeyPressed(Key.PageDown))
        {
            SetCurrentVisible();

            if (TopId + _maxItems < Size)
            {
                NeedRedraw = true;

                int previousTop = TopId;
                TopId += _maxItems;

                if (TopId + _maxItems >= Size)
                {
                    TopId = Size - _maxItems;
                }

                UpdateScrollbarRange();
                ScrollBar.MoveToIndex(TopId);
                CurrentId = Math.Clamp(CurrentId + (TopId - previousTop), TopId, LastVisibleId);
            }
            else if (CurrentId < Size - 1)
            {
                // The view is at the bottom of the list, but the selection is not.
                CurrentId = Size - 1;
                NeedRedraw = true;
            }

            return true;
        }

        if (_useHotkeys && events.IsKeyPressed(Key.Up) && CurrentId > 0)
        {
            NeedRedraw = true;

            --CurrentId;
            SetCurrentVisible();
            ActionCurrentUp();

            return true;
        }

        if (_useHotkeys && events.IsKeyPressed(Key.Down) && CurrentId + 1 < Size)
        {
            NeedRedraw = true;

            ++CurrentId;
            SetCurrentVisible();
            ActionCurrentDn();

            return true;
        }

        if (_useHotkeys && events.IsKeyPressed(Key.Home) && (TopId > 0 || CurrentId > 0))
        {
            NeedRedraw = true;

            TopId = 0;
            CurrentId = TopId;

            UpdateScrollbarRange();
            ScrollBar.MoveToIndex(TopId);

            return true;
        }

        if (_useHotkeys && events.IsKeyPressed(Key.End) && (TopId + _maxItems < Size || CurrentId < LastVisibleId))
        {
            NeedRedraw = true;

            TopId = Math.Max(0, Size - _maxItems);
            CurrentId = Size - 1;

            UpdateScrollbarRange();
            ScrollBar.MoveToIndex(TopId);

            return true;
        }

        if ((events.MouseClickLeft(ButtonPageUp?.Area ?? Rect.Empty) ||
             events.IsMouseWheelUpInArea(ItemsArea) ||
             events.IsMouseWheelUpInArea(ScrollBar.Area) ||
             _timedButtonPageUp.IsDelayPassed(ButtonPageUp?.IsPressed == true)) &&
            TopId > 0)
        {
            NeedRedraw = true;

            --TopId;
            ScrollBar.Backward();

            return true;
        }

        if ((events.MouseClickLeft(ButtonPageDown?.Area ?? Rect.Empty) ||
             events.IsMouseWheelDownInArea(ItemsArea) ||
             events.IsMouseWheelDownInArea(ScrollBar.Area) ||
             _timedButtonPageDown.IsDelayPassed(ButtonPageDown?.IsPressed == true)) &&
            TopId + _maxItems < Size)
        {
            NeedRedraw = true;

            ++TopId;
            ScrollBar.Forward();

            return true;
        }

        if (!_lockClick && events.MouseLongPressLeft(ItemsArea))
        {
            Point longPressPosition = events.CursorPosition;
            int longPressId = (((longPressPosition.Y - ItemsArea.Y) * _maxItems) / ItemsArea.Height) + TopId;

            if (longPressId >= 0 && longPressId < _content!.Count)
            {
                ActionListLongPress(_content[longPressId]);
            }

            return true;
        }

        if (events.IsMouseLeftButtonPressedInArea(ScrollBar.Area) || events.IsMouseLeftButtonPressedInArea(ItemsArea))
        {
            Point mousePosition = events.CursorPosition;

            Point previousScrollbarPosition = ScrollBar.Position;

            UpdateScrollbarRange();

            if (events.IsMouseLeftButtonPressedInArea(ScrollBar.Area) && Size > _maxItems)
            {
                ScrollBar.MoveToPosition(mousePosition);
            }

            if (events.IsMouseLeftButtonPressedInArea(ItemsArea))
            {
                if (!events.IsDragInProgress)
                {
                    // Remember where the drag started and claim it.
                    _dragStartPosition = mousePosition;
                    events.RegisterDrag();

                    // It may as well turn out to be a legitimate click.
                    _lockClick = false;
                }

                int itemSize = (ScrollBar.IsVertical ? ItemsArea.Height : ItemsArea.Width) / _maxItems;

                if (itemSize > 0)
                {
                    int delta = ScrollBar.IsVertical
                        ? _dragStartPosition.Y - mousePosition.Y
                        : _dragStartPosition.X - mousePosition.X;

                    // The cursor has moved past the size of one row.
                    if (Math.Abs(delta) > itemSize)
                    {
                        ScrollBar.MoveToIndex(ScrollBar.CurrentIndex + (delta / itemSize));
                        _dragStartPosition = mousePosition;

                        // The drag must not end as a click on a row.
                        _lockClick = true;
                    }
                }
            }

            // The scrollbar does not have to be painted when the thumb stayed put.
            if (ScrollBar.Position == previousScrollbarPosition)
            {
                return false;
            }

            TopId = ScrollBar.CurrentIndex;

            NeedRedraw = true;
            _updateScrollbar = true;

            return true;
        }

        if (_updateScrollbar)
        {
            _updateScrollbar = false;

            if (ScrollBar.UpdatePosition())
            {
                NeedRedraw = true;

                return true;
            }
        }

        Point rowPosition = events.CursorPosition;

        if (ItemsArea.Contains(rowPosition))
        {
            NeedRedraw = true;

            int id = (((rowPosition.Y - ItemsArea.Y) * _maxItems) / ItemsArea.Height) + TopId;

            if (id >= 0 && id < Size)
            {
                T item = _content![id];
                int offsetY = (id - TopId) * ItemsArea.Height / _maxItems;

                if (ActionListCursor(item, rowPosition))
                {
                    return true;
                }

                if (!_lockClick && events.MouseClickLeft(ItemsArea))
                {
                    if (id == CurrentId)
                    {
                        ActionListDoubleClick(item, rowPosition, ItemsArea.X, ItemsArea.Y + offsetY);
                    }
                    else
                    {
                        CurrentId = id;
                        ActionListSingleClick(item, rowPosition, ItemsArea.X, ItemsArea.Y + offsetY);
                    }

                    return true;
                }

                if (events.IsMouseLeftButtonPressedInArea(ItemsArea))
                {
                    ActionListLongPress(item);

                    return true;
                }

                if (events.IsMouseRightButtonPressedInArea(ItemsArea))
                {
                    ActionListPressRight(item, rowPosition, ItemsArea.X, ItemsArea.Y + offsetY);

                    return true;
                }
            }

            NeedRedraw = false;
        }

        return false;
    }

    /// <summary>Resets the indices when they cannot describe the current rows, <c>Verify()</c>.</summary>
    /// <remarks>
    /// Two things happen here beyond clamping: an invalid selection becomes <c>-1</c> rather than «last row», and a
    /// selection that is out of view is centred — <c>current - visible / 2</c> — instead of being scrolled to the
    /// nearest edge, which is what makes <see cref="SetTopVisibleItem"/> survive a list that changed behind its back.
    /// </remarks>
    protected void Verify()
    {
        if (_content is not { Count: > 0 })
        {
            CurrentId = -1;
            TopId = -1;

            return;
        }

        int maxTopId = Math.Max(Size - _maxItems, 0);

        if (CurrentId >= Size)
        {
            CurrentId = -1;
        }

        if (TopId < 0 || TopId > maxTopId)
        {
            TopId = CurrentId == -1
                ? 0
                : Math.Clamp(CurrentId - (_maxItems / 2), 0, maxTopId);
        }

        // Update the scrollbar range when it disagrees with the rows.
        if (ScrollBar.MaxIndex != maxTopId)
        {
            ScrollBar.SetRange(0, maxTopId);
            ScrollBar.MoveToIndex(TopId);
        }
    }

    /// <summary>Fits the scroll bar range to the current rows, <c>UpdateScrollbarRange()</c>.</summary>
    protected void UpdateScrollbarRange()
    {
        int maxValue = _content is not null && Size - _maxItems > 0 ? Size - _maxItems : 0;

        if (ScrollBar.MaxIndex != maxValue)
        {
            ScrollBar.SetRange(0, maxValue);
        }
    }
}
