using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A <see cref="ItemsBar{T}"/> that remembers which slot was clicked and can trade items with a second bar,
/// <c>Interface::ItemsActionBar&lt;Item&gt;</c> (<c>gui/interface_itemsbar.h:313-508</c>).
/// </summary>
/// <remarks>
/// <para>
/// The selection is the reference's <c>curItemPos</c>: an item cursor plus the area it sits on. It is kept here as
/// an item index plus that area, -1 meaning "nothing is selected", which is what the reference spells
/// <c>items.end()</c>.
/// </para>
/// <para>
/// <b>The two-bar handlers get the item of the other bar, not the other bar.</b> The reference passes
/// <c>Item &amp;</c> pairs; the port does the same, so a subclass swaps through the two items it was given.
/// </para>
/// </remarks>
/// <typeparam name="T">What one slot holds, <c>Item</c>.</typeparam>
public abstract class ItemsActionBar<T> : ItemsBar<T>
    where T : notnull
{
    private int _selectedItemIndex = -1;
    private Rect _selectedPos;

    /// <summary>
    /// The first item of the bar, <c>topItem</c> (<c>interface_itemsbar.h:505</c>).
    /// </summary>
    /// <remarks>
    /// The reference keeps it next to the selection cursor and only ever resets it in <c>ResetSelected()</c>; it
    /// takes part in no computation, so the port keeps it for the same reason and nothing else.
    /// </remarks>
    private int _topItemIndex;

    /// <summary>Whether a slot is selected, <c>isSelected()</c> (<c>interface_itemsbar.h:414-417</c>).</summary>
    public bool IsSelected => _selectedItemIndex >= 0;

    /// <summary>The first item of the bar, the reference's <c>topItem</c> (<c>interface_itemsbar.h:505</c>).</summary>
    public int TopItemIndex => _topItemIndex;

    /// <summary>
    /// The selected item, <c>GetSelectedItem()</c> (<c>interface_itemsbar.h:399-402</c>).
    /// </summary>
    /// <returns>The item, or the default value when nothing is selected.</returns>
    /// <remarks>
    /// The reference dereferences the cursor and leaves the "nothing is selected" case to the caller's earlier
    /// <c>isSelected()</c>; a C# value type cannot be null, so the port answers with the default value and expects
    /// the same discipline.
    /// </remarks>
    public T? GetSelectedItem() => _selectedItemIndex < 0 ? default : ItemAt(_selectedItemIndex);

    /// <summary>The area of the selected slot, <c>GetSelectedPos()</c> (<c>interface_itemsbar.h:404-407</c>).</summary>
    public Rect GetSelectedPos() => _selectedPos;

    /// <summary>
    /// The index of the selected slot, <c>GetSelectedIndex()</c> (<c>interface_itemsbar.h:409-412</c>).
    /// </summary>
    /// <remarks>
    /// The reference measures the distance between the item cursor and the end, so "nothing selected" is the item
    /// count itself rather than -1; the port keeps that.
    /// </remarks>
    public int GetSelectedIndex() => _selectedItemIndex < 0 ? ItemCount : _selectedItemIndex;

    /// <summary>Forgets the selection, <c>ResetSelected()</c> (<c>interface_itemsbar.h:419-423</c>).</summary>
    public void ResetSelected()
    {
        _topItemIndex = 0;
        _selectedItemIndex = -1;
        _selectedPos = default;
    }

    /// <summary>
    /// Moves the selection to a slot without a click.
    /// </summary>
    /// <remarks>
    /// The reference writes <c>curItemPos</c> directly from its click handling, so it needs no such entry point; the
    /// port's subclasses keep the selection private to this class and a screen that has to frame a slot without input
    /// (an artifact bar painting a cursor, an editor previewing a move) needs one. <c>Select( -1 )</c> is
    /// <see cref="ResetSelected"/>.
    /// </remarks>
    /// <param name="index">The slot to select, or a negative number to clear the selection.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is past the last item.</exception>
    public void Select(int index)
    {
        if (index < 0)
        {
            ResetSelected();

            return;
        }

        if (index >= ItemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The bar has no such slot.");
        }

        _selectedItemIndex = index;
        _selectedPos = GetItemRect(index);
    }

    /// <summary>
    /// Handles one event, <c>QueueEventProcessing()</c> (<c>interface_itemsbar.h:425-428</c>).
    /// </summary>
    /// <returns>True when the event was consumed.</returns>
    public new bool QueueEventProcessing() => base.QueueEventProcessing();

    /// <summary>
    /// Handles one event against a second bar, <c>QueueEventProcessing( other )</c>
    /// (<c>interface_itemsbar.h:430-444</c>).
    /// </summary>
    /// <param name="other">The bar the player is dragging from or to.</param>
    /// <returns>True when the event was consumed.</returns>
    public bool QueueEventProcessing(ItemsActionBar<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        LocalEvent events = LocalEvent.Current;
        Point cursor = events.CursorPosition;

        if (IsItemsEmpty() && other.IsItemsEmpty())
        {
            return false;
        }

        if (other.TryGetItem(events.LeftButtonPressPosition, out _) && ActionCrossItemBarDrag(cursor, other))
        {
            return true;
        }

        return other.IsSelected
            ? ActionCursorItemIter(cursor, other)
            : ActionCursorItemIter(cursor, GetItemIterPos(cursor));
    }

    /// <summary>
    /// Paints one slot, <c>RedrawItem( Item &amp;, const Rect &amp;, fheroes2::Image &amp; )</c> — overridden empty
    /// (<c>interface_itemsbar.h:324-327</c>) because the selected-aware overload does the work.
    /// </summary>
    /// <param name="item">The item to paint.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="output">The frame to paint into.</param>
    public override void RedrawItem(T item, Rect position, PixelBuffer output)
    {
        // Do nothing.
        _ = item;
        _ = position;
        _ = output;
    }

    /// <summary>
    /// Paints one slot knowing whether it is selected, <c>RedrawItem( Item &amp;, const Rect &amp;, bool, ... )</c>
    /// (<c>interface_itemsbar.h:329-332</c>).
    /// </summary>
    /// <param name="item">The item to paint.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="selected">Whether this slot is the selection.</param>
    /// <param name="output">The frame to paint into.</param>
    public virtual void RedrawItem(T item, Rect position, bool selected, PixelBuffer output)
    {
        // Do nothing.
        _ = item;
        _ = position;
        _ = selected;
        _ = output;
    }

    /// <summary>Updates the tooltip and cursor for a slot, <c>ActionBarCursor()</c> (<c>interface_itemsbar.h:334-337</c>).</summary>
    /// <param name="item">The item under the cursor.</param>
    /// <returns>True when the event was consumed.</returns>
    public override bool ActionBarCursor(T item) => false;

    /// <summary>Updates the tooltip of two bars at once, <c>ActionBarCursor( Item &amp;, Item &amp; )</c>
    /// (<c>interface_itemsbar.h:339-342</c>).</summary>
    /// <param name="item">The item under the cursor.</param>
    /// <param name="otherItem">The item selected in the other bar.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarCursor(T item, T otherItem)
    {
        _ = item;
        _ = otherItem;

        return false;
    }

    /// <summary>Handles a left click on a slot, <c>ActionBarLeftMouseSingleClick()</c>
    /// (<c>interface_itemsbar.h:349-352</c>).</summary>
    /// <param name="item">The item that was clicked.</param>
    /// <returns>True when the event was consumed.</returns>
    public override bool ActionBarLeftMouseSingleClick(T item) => false;

    /// <summary>Handles a left click while the other bar has a selection, <c>ActionBarLeftMouseSingleClick( Item &amp;, Item &amp; )</c>
    /// (<c>interface_itemsbar.h:344-347</c>).</summary>
    /// <param name="item">The item that was clicked.</param>
    /// <param name="otherItem">The item selected in the other bar.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarLeftMouseSingleClick(T item, T otherItem)
    {
        _ = item;
        _ = otherItem;

        return false;
    }

    /// <summary>
    /// Handles a left click on the already selected slot, <c>ActionBarLeftMouseDoubleClick()</c>
    /// (<c>interface_itemsbar.h:354-357</c>).
    /// </summary>
    /// <param name="item">The item that was clicked twice.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarLeftMouseDoubleClick(T item) => ActionBarLeftMouseSingleClick(item);

    /// <summary>Handles the release of the left button on a slot, <c>ActionBarLeftMouseRelease()</c>
    /// (<c>interface_itemsbar.h:359-362</c>).</summary>
    /// <param name="item">The item under the button.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarLeftMouseRelease(T item)
    {
        _ = item;

        return false;
    }

    /// <summary>Handles the release of the left button onto another bar's item,
    /// <c>ActionBarLeftMouseRelease( Item &amp;, Item &amp; )</c> (<c>interface_itemsbar.h:364-367</c>).</summary>
    /// <param name="item">The item under the button.</param>
    /// <param name="otherItem">The item that was pressed in the other bar.</param>
    /// <returns>True when the item was dropped.</returns>
    public virtual bool ActionBarLeftMouseRelease(T item, T otherItem)
    {
        _ = item;
        _ = otherItem;

        return false;
    }

    /// <summary>Handles the left button being held on a slot, <c>ActionBarLeftMouseHold()</c>
    /// (<c>interface_itemsbar.h:374-377</c>).</summary>
    /// <param name="item">The item under the button.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarLeftMouseHold(T item, Rect position)
    {
        _ = item;
        _ = position;

        return false;
    }

    /// <summary>Handles the left button being held after pressing another bar's item,
    /// <c>ActionBarLeftMouseHold( Item &amp;, Item &amp; )</c> (<c>interface_itemsbar.h:379-382</c>).</summary>
    /// <param name="item">The item under the button.</param>
    /// <param name="otherItem">The item that was pressed in the other bar.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarLeftMouseHold(T item, T otherItem)
    {
        _ = item;
        _ = otherItem;

        return false;
    }

    /// <summary>Handles a right button hold on a slot, <c>ActionBarRightMouseHold()</c>
    /// (<c>interface_itemsbar.h:384-387</c>).</summary>
    /// <param name="item">The item under the button.</param>
    /// <returns>True when the event was consumed.</returns>
    public override bool ActionBarRightMouseHold(T item) => false;

    /// <summary>Handles a right button hold after pressing another bar's item,
    /// <c>ActionBarRightMouseHold( Item &amp;, Item &amp; )</c> (<c>interface_itemsbar.h:369-372</c>).</summary>
    /// <param name="item">The item under the button.</param>
    /// <param name="otherItem">The item that was pressed in the other bar.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarRightMouseHold(T item, T otherItem)
    {
        _ = item;
        _ = otherItem;

        return false;
    }

    /// <summary>Handles a right click on a slot, <c>ActionBarRightMouseSingleClick()</c>
    /// (<c>interface_itemsbar.h:389-392</c>).</summary>
    /// <param name="item">The item that was clicked.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarRightMouseSingleClick(T item)
    {
        _ = item;

        return false;
    }

    /// <summary>Handles a right click while the other bar has a selection,
    /// <c>ActionBarRightMouseSingleClick( Item &amp;, Item &amp; )</c> (<c>interface_itemsbar.h:394-397</c>).</summary>
    /// <param name="item">The item that was clicked.</param>
    /// <param name="otherItem">The item selected in the other bar.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarRightMouseSingleClick(T item, T otherItem)
    {
        _ = item;
        _ = otherItem;

        return false;
    }

    /// <summary>Forgets the selection whenever the content changes,
    /// <c>SetContentItems() final</c> (<c>interface_itemsbar.h:455-458</c>).</summary>
    protected sealed override void SetContentItems() => ResetSelected();

    /// <summary>Paints one slot with its selection state, <c>RedrawItemIter()</c>
    /// (<c>interface_itemsbar.h:460-463</c>).</summary>
    /// <param name="index">The slot's index in the item order.</param>
    /// <param name="position">Where the slot sits.</param>
    /// <param name="output">The frame to paint into.</param>
    protected override void RedrawItemIter(int index, Rect position, PixelBuffer output) =>
        RedrawItem(ItemAt(index), position, index == _selectedItemIndex, output);

    /// <summary>
    /// Dispatches the mouse to one slot, <c>ActionCursorItemIter()</c> (<c>interface_itemsbar.h:487-535</c>).
    /// </summary>
    /// <param name="cursor">Where the cursor is.</param>
    /// <param name="iterPos">The slot under the cursor.</param>
    /// <returns>True when the event was consumed.</returns>
    protected override bool ActionCursorItemIter(Point cursor, ItemIterPos iterPos)
    {
        _ = cursor;

        if (iterPos.Cell < 0)
        {
            return false;
        }

        T item = ItemAt(iterPos.Cell);

        if (ActionBarCursor(item))
        {
            return true;
        }

        LocalEvent events = LocalEvent.Current;

        if (events.MouseClickLeft(iterPos.Area))
        {
            if (iterPos.Cell == _selectedItemIndex)
            {
                return ActionBarLeftMouseDoubleClick(item);
            }

            if (ActionBarLeftMouseSingleClick(item))
            {
                _selectedItemIndex = iterPos.Cell;
                _selectedPos = iterPos.Area;
            }
            else
            {
                ResetSelected();
            }

            return true;
        }

        if (events.IsMouseLeftButtonPressedInArea(iterPos.Area))
        {
            return ActionBarLeftMouseHold(item, iterPos.Area);
        }

        if (events.IsMouseLeftButtonReleasedInArea(iterPos.Area))
        {
            return ActionBarLeftMouseRelease(item);
        }

        if (events.MouseClickRight(iterPos.Area))
        {
            return ActionBarRightMouseSingleClick(item);
        }

        if (events.IsMouseRightButtonPressedInArea(iterPos.Area))
        {
            return ActionBarRightMouseHold(item);
        }

        return false;
    }

    /// <summary>
    /// Dispatches the mouse against the selection of another bar, <c>ActionCursorItemIter( cursor, other )</c>
    /// (<c>interface_itemsbar.h:537-573</c>).
    /// </summary>
    /// <param name="cursor">Where the cursor is.</param>
    /// <param name="other">The bar whose selection is the source item.</param>
    /// <returns>True when the event was consumed.</returns>
    private bool ActionCursorItemIter(Point cursor, ItemsActionBar<T> other)
    {
        ItemIterPos iterPos1 = GetItemIterPos(cursor);

        if (iterPos1.Cell < 0)
        {
            return false;
        }

        T item = ItemAt(iterPos1.Cell);
        T otherItem = other.GetSelectedItem()!;

        if (ActionBarCursor(item, otherItem))
        {
            return true;
        }

        LocalEvent events = LocalEvent.Current;

        if (events.MouseClickLeft(iterPos1.Area))
        {
            if (ActionBarLeftMouseSingleClick(item, otherItem))
            {
                _selectedItemIndex = iterPos1.Cell;
                _selectedPos = iterPos1.Area;
            }
            else
            {
                ResetSelected();
            }

            other.ResetSelected();

            return true;
        }

        if (events.IsMouseLeftButtonPressedInArea(iterPos1.Area))
        {
            return ActionBarLeftMouseHold(item, otherItem);
        }

        // Let ActionCrossItemBarDrag() handle the release: the press started on the other bar.

        if (events.MouseClickRight(iterPos1.Area))
        {
            ActionBarRightMouseSingleClick(item, otherItem);
            other.ResetSelected();

            // The reference returns true so that the caller redraws the reset selection.
            return true;
        }

        if (events.IsMouseRightButtonPressedInArea(iterPos1.Area))
        {
            return ActionBarRightMouseHold(item, otherItem);
        }

        return false;
    }

    /// <summary>
    /// Handles a drag that started on another bar and is now over this one, <c>ActionCrossItemBarDrag()</c>
    /// (<c>interface_itemsbar.h:466-492</c>).
    /// </summary>
    /// <param name="cursor">Where the cursor is.</param>
    /// <param name="other">The bar the press started on.</param>
    /// <returns>True when the event was consumed.</returns>
    private bool ActionCrossItemBarDrag(Point cursor, ItemsActionBar<T> other)
    {
        LocalEvent events = LocalEvent.Current;

        if (!other.TryGetItem(events.LeftButtonPressPosition, out T otherItemPress))
        {
            throw new InvalidOperationException("A drag was reported from a bar with no item under the press.");
        }

        ItemIterPos iterPos1 = GetItemIterPos(cursor);

        if (iterPos1.Cell < 0)
        {
            return false;
        }

        T item = ItemAt(iterPos1.Cell);

        if (events.IsMouseLeftButtonPressedInArea(iterPos1.Area))
        {
            return ActionBarLeftMouseHold(item, otherItemPress);
        }

        if (events.IsMouseLeftButtonReleasedInArea(iterPos1.Area))
        {
            if (ActionBarLeftMouseRelease(item, otherItemPress))
            {
                other.ResetSelected();
            }

            return true;
        }

        return false;
    }
}
