using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// A table of item slots: the layout, the painting order and the event dispatch,
/// <c>Interface::ItemsBar&lt;Item&gt;</c> (<c>gui/interface_itemsbar.h:35-311</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Items are indices, not pointers.</b> The reference keeps a <c>std::vector&lt;Item *&gt;</c> into a container
/// the caller owns, so the bar always paints the container's current value and never a stale copy. The port keeps
/// the same shape with <see cref="SetContent"/> holding the container itself plus a list of indices into it; a
/// subclass that reorders or filters its items does it in <see cref="SetContentItems"/> by calling
/// <see cref="SetItemIndices"/>, exactly where the reference does its own <c>items</c> juggling.
/// </para>
/// <para>
/// <b>Ten bare enum values are legitimate content.</b> <c>Skill::Primary::Type</c> and <c>Artifact</c> come in as
/// value types; that is why the index indirection is not an optimisation but the only way to keep
/// <see cref="Redraw"/> reading what the hero has now.
/// </para>
/// <para>
/// <b>Sizes are recomputed, not cached.</b> The reference calls <c>calculateRoi()</c> and
/// <c>calculateItemsPos()</c> on every setter; the port does the same, so the order the setters are called in does
/// not matter.
/// </para>
/// </remarks>
/// <typeparam name="T">What one slot holds, <c>Item</c>.</typeparam>
public abstract class ItemsBar<T>
    where T : notnull
{
    private readonly List<int> _itemIndices = [];
    private readonly List<int> _customItemsCountInRow = [];
    private readonly List<Point> _itemPositions = [];

    private IReadOnlyList<T> _content = Array.Empty<T>();

    private Size _singleItemSize = new(1, 1);
    private Size _tableSize;
    private Size _offsetBetweenItems;
    private Rect _renderingArea;

    /// <summary>Size of one cell.</summary>
    public Size SingleItemSize => _singleItemSize;

    /// <summary>Cells per column and per row.</summary>
    public Size TableSize => _tableSize;

    /// <summary>Gap between two neighbouring cells.</summary>
    public Size OffsetBetweenItems => _offsetBetweenItems;

    /// <summary>
    /// The area the cells occupy, anchored at the rendering offset and sized by <see cref="TableSize"/> and
    /// <see cref="SingleItemSize"/>, <c>GetArea()</c> (<c>interface_itemsbar.h:100-103</c>).
    /// </summary>
    public Rect RenderingArea => _renderingArea;

    /// <summary>Top left corner of every cell, row by row.</summary>
    public IReadOnlyList<Point> ItemPositions => _itemPositions;

    /// <summary>How many cells the table holds, <c>itemPos.size()</c>.</summary>
    public int Count => _itemPositions.Count;

    /// <summary>How many items are in the bar, <c>items.size()</c>.</summary>
    public int ItemCount => _itemIndices.Count;

    /// <summary>Whether no cell is laid out at all.</summary>
    public bool IsEmpty => _itemPositions.Count == 0;

    /// <summary>
    /// Makes the bar show <paramref name="content"/>, <c>SetContent()</c> (<c>interface_itemsbar.h:56-66</c>).
    /// </summary>
    /// <param name="content">The container the bar reads its items from; it is not copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    public void SetContent(IReadOnlyList<T> content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _content = content;
        _itemIndices.Clear();

        for (int i = 0; i < content.Count; ++i)
        {
            _itemIndices.Add(i);
        }

        SetContentItems();
    }

    /// <summary>
    /// Sets how many cells the table holds, <c>setTableSize()</c> (<c>interface_itemsbar.h:46</c>).
    /// </summary>
    /// <param name="size">Columns and rows; both must be positive.</param>
    public void SetTableSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "a table must have at least one cell");
        }

        _tableSize = size;

        CalculateRoi();
        CalculateItemPositions();
    }

    /// <summary>
    /// Sets the size of one cell, <c>setSingleItemSize()</c> (<c>interface_itemsbar.h:76</c>).
    /// </summary>
    /// <param name="size">The size; both sides must be positive.</param>
    public void SetSingleItemSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "a cell must have a positive size");
        }

        _singleItemSize = size;

        CalculateRoi();
        CalculateItemPositions();
    }

    /// <summary>
    /// Sets the gap between two cells, <c>setInBetweenItemsOffset()</c> (<c>interface_itemsbar.h:86</c>).
    /// </summary>
    /// <param name="offset">The gap; zero for cells that touch.</param>
    public void SetInBetweenItemsOffset(Size offset)
    {
        _offsetBetweenItems = offset;

        CalculateRoi();
        CalculateItemPositions();
    }

    /// <summary>
    /// Moves the whole table, <c>setRenderingOffset()</c> (<c>interface_itemsbar.h:68</c>).
    /// </summary>
    /// <param name="offset">The top left corner of the first cell.</param>
    public void SetRenderingOffset(Point offset)
    {
        _renderingArea = new Rect(offset.X, offset.Y, _renderingArea.Width, _renderingArea.Height);

        CalculateItemPositions();
    }

    /// <summary>
    /// Gives a row fewer cells than the columns allow, <c>setCustomItemsCountInRow()</c>
    /// (<c>interface_itemsbar.h:161-174</c>).
    /// </summary>
    /// <remarks>
    /// A row with fewer cells than the most populated one is centred; the table widens to the longest row. The
    /// reference reads the list only when it has exactly one entry per row, so a list of the wrong length is
    /// ignored rather than applied to the rows it happens to fit.
    /// </remarks>
    /// <param name="counts">Cells per row, or <see langword="null"/> to use the table's column count everywhere.</param>
    public void SetItemsCountPerRow(IReadOnlyList<int>? counts)
    {
        _customItemsCountInRow.Clear();

        if (counts is not null)
        {
            _customItemsCountInRow.AddRange(counts);
        }

        // The reference widens the table only when the list has exactly one entry per row; a list of the wrong
        // length is kept but never consulted, because the layout checks its length on every use.
        if (_customItemsCountInRow.Count == _tableSize.Height)
        {
            int maximum = 0;

            foreach (int count in _customItemsCountInRow)
            {
                maximum = Math.Max(maximum, count);
            }

            if (_tableSize.Width != maximum)
            {
                _tableSize = new Size(maximum, _tableSize.Height);

                CalculateRoi();
            }
        }

        CalculateItemPositions();
    }

    /// <summary>
    /// The area of one cell, <c>fheroes2::Rect( itemPos, singleItemSize )</c>.
    /// </summary>
    /// <param name="index">Index of the cell.</param>
    /// <returns>The cell's area, empty when the index is out of range.</returns>
    public Rect GetItemRect(int index) =>
        index < 0 || index >= _itemPositions.Count ? default : new Rect(_itemPositions[index], _singleItemSize);

    /// <summary>
    /// The cell under a point, <c>GetItemIterPos()</c> (<c>interface_itemsbar.h:233-261</c>).
    /// </summary>
    /// <param name="position">The point to test.</param>
    /// <returns>The index of the cell, or -1 when the point is on none of them.</returns>
    /// <remarks>
    /// Cells past the last item still exist in the table but are never reported: the reference stops advancing its
    /// item cursor once <c>items</c> is exhausted, so an empty slot of a half-filled bar answers "nothing".
    /// </remarks>
    public int IndexAt(Point position) => GetItemIterPos(position).Cell;

    /// <summary>
    /// The item under a point, <c>GetItem()</c> (<c>interface_itemsbar.h:94-98</c>).
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <param name="item">The item, when the point is on a filled cell.</param>
    /// <returns>Whether the point is on a filled cell.</returns>
    /// <remarks>
    /// The reference returns a pointer and lets the caller compare it with <c>nullptr</c>; a C# value type cannot
    /// be null, so the port answers with a <c>TryGet</c>.
    /// </remarks>
    public bool TryGetItem(Point point, out T item)
    {
        ItemIterPos iterPos = GetItemIterPos(point);

        if (iterPos.Cell < 0)
        {
            item = default!;

            return false;
        }

        item = ItemAt(iterPos.Cell);

        return true;
    }

    /// <summary>
    /// Paints every slot, <c>Redraw()</c> (<c>interface_itemsbar.h:105-134</c>).
    /// </summary>
    /// <param name="output">The frame to paint into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The background is painted for every cell of the table, the item only while there is one left — a bar with
    /// more cells than items shows its empty slots.
    /// </remarks>
    public void Redraw(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (_tableSize.Width <= 0 || _tableSize.Height <= 0)
        {
            return;
        }

        int itemIter = 0;
        int itemPosIter = 0;

        bool useCustomCountX = _customItemsCountInRow.Count == _tableSize.Height;

        for (int y = 0; y < _tableSize.Height; ++y)
        {
            int countX = useCustomCountX ? _customItemsCountInRow[y] : _tableSize.Width;

            for (int x = 0; x < countX; ++x)
            {
                if (itemPosIter >= _itemPositions.Count)
                {
                    throw new InvalidOperationException("The table has more cells than were laid out.");
                }

                Rect itemRoi = new(_itemPositions[itemPosIter], _singleItemSize);

                RedrawBackground(itemRoi, output);

                ++itemPosIter;

                if (itemIter == _itemIndices.Count)
                {
                    continue;
                }

                RedrawItemIter(itemIter, itemRoi, output);

                ++itemIter;
            }
        }
    }

    /// <summary>
    /// Handles one event, <c>QueueEventProcessing()</c> (<c>interface_itemsbar.h:151-159</c>).
    /// </summary>
    /// <returns>True when the event was consumed.</returns>
    public bool QueueEventProcessing()
    {
        if (IsItemsEmpty())
        {
            return false;
        }

        Point cursor = LocalEvent.Current.CursorPosition;

        return ActionCursorItemIter(cursor, GetItemIterPos(cursor));
    }

    /// <summary>
    /// Paints the plate behind a slot, <c>RedrawBackground()</c> (<c>interface_itemsbar.h:38</c>).
    /// </summary>
    /// <param name="area">Where the background goes.</param>
    /// <param name="output">The frame to paint into.</param>
    public abstract void RedrawBackground(Rect area, PixelBuffer output);

    /// <summary>
    /// Paints one item, <c>RedrawItem()</c> (<c>interface_itemsbar.h:39</c>).
    /// </summary>
    /// <param name="item">The item to paint.</param>
    /// <param name="position">Where its slot sits.</param>
    /// <param name="output">The frame to paint into.</param>
    public abstract void RedrawItem(T item, Rect position, PixelBuffer output);

    /// <summary>
    /// Called once the content is in, <c>SetContentItems()</c> (<c>interface_itemsbar.h:181-184</c>).
    /// </summary>
    /// <remarks>
    /// A subclass that filters or reorders the item list overrides this and calls
    /// <see cref="SetItemIndices"/>; the default keeps every element in container order.
    /// </remarks>
    protected virtual void SetContentItems()
    {
        // Do nothing.
    }

    /// <summary>
    /// Replaces the item order, the reference's direct writes into <c>items</c>.
    /// </summary>
    /// <param name="indices">Indices into the container passed to <see cref="SetContent"/>.</param>
    protected void SetItemIndices(IReadOnlyList<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);

        _itemIndices.Clear();
        _itemIndices.AddRange(indices);
    }

    /// <summary>The item behind one of the bar's slots.</summary>
    /// <param name="itemIndex">The slot's index in the item order, not in the container.</param>
    /// <returns>The item.</returns>
    protected T ItemAt(int itemIndex) => _content[_itemIndices[itemIndex]];

    /// <summary>
    /// The item cursor and the cell it sits on, <c>ItemIterPos</c> (<c>interface_itemsbar.h:179</c>).
    /// </summary>
    /// <param name="Cell">The cell's index, -1 for "past the last item".</param>
    /// <param name="Area">The cell's area, empty for "past the last item".</param>
    protected readonly record struct ItemIterPos(int Cell, Rect Area);

    /// <summary>
    /// The cell and item under a point, <c>GetItemIterPos()</c> (<c>interface_itemsbar.h:233-261</c>).
    /// </summary>
    /// <param name="position">The point to test.</param>
    /// <returns>The cursor, with <see cref="ItemIterPos.Cell"/> -1 when the point is past the last item.</returns>
    protected ItemIterPos GetItemIterPos(Point position)
    {
        if (_tableSize.Width <= 0 || _tableSize.Height <= 0)
        {
            return new ItemIterPos(-1, default);
        }

        int itemIter = 0;
        int itemPosIter = 0;

        bool useCustomCountX = _customItemsCountInRow.Count == _tableSize.Height;

        for (int y = 0; y < _tableSize.Height; ++y)
        {
            int countX = useCustomCountX ? _customItemsCountInRow[y] : _tableSize.Width;

            for (int x = 0; x < countX; ++x)
            {
                // The reference only advances its cursor while there is an item to advance to.
                if (itemIter < _itemIndices.Count)
                {
                    if (itemPosIter >= _itemPositions.Count)
                    {
                        throw new InvalidOperationException("The table has more cells than were laid out.");
                    }

                    Rect itemRoi = new(_itemPositions[itemPosIter], _singleItemSize);

                    if (itemRoi.Contains(position))
                    {
                        return new ItemIterPos(itemIter, itemRoi);
                    }

                    ++itemIter;
                    ++itemPosIter;
                }
            }
        }

        return new ItemIterPos(-1, default);
    }

    /// <summary>Whether the bar has no items at all, <c>isItemsEmpty()</c> (<c>interface_itemsbar.h:223</c>).</summary>
    /// <returns>True when there is nothing to act on.</returns>
    protected bool IsItemsEmpty() => _itemIndices.Count == 0;

    /// <summary>
    /// Paints one slot, <c>RedrawItemIter()</c> (<c>interface_itemsbar.h:196-199</c>).
    /// </summary>
    /// <param name="index">The slot's index in the item order.</param>
    /// <param name="position">Where the slot sits.</param>
    /// <param name="output">The frame to paint into.</param>
    protected virtual void RedrawItemIter(int index, Rect position, PixelBuffer output) =>
        RedrawItem(ItemAt(index), position, output);

    /// <summary>
    /// Dispatches the mouse to one slot, <c>ActionCursorItemIter()</c> (<c>interface_itemsbar.h:201-221</c>).
    /// </summary>
    /// <param name="cursor">Where the cursor is.</param>
    /// <param name="iterPos">The slot under the cursor.</param>
    /// <returns>True when the event was consumed.</returns>
    protected virtual bool ActionCursorItemIter(Point cursor, ItemIterPos iterPos)
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
            return ActionBarLeftMouseSingleClick(item);
        }

        if (events.IsMouseRightButtonPressedInArea(iterPos.Area))
        {
            return ActionBarRightMouseHold(item);
        }

        return false;
    }

    /// <summary>Updates the tooltip and cursor for a slot, <c>ActionBarCursor()</c> (<c>interface_itemsbar.h:146</c>).</summary>
    /// <param name="item">The item under the cursor.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarCursor(T item)
    {
        _ = item;

        return false;
    }

    /// <summary>Handles a left click on a slot, <c>ActionBarLeftMouseSingleClick()</c> (<c>interface_itemsbar.h:136</c>).</summary>
    /// <param name="item">The item that was clicked.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarLeftMouseSingleClick(T item)
    {
        _ = item;

        return false;
    }

    /// <summary>Handles a right button hold on a slot, <c>ActionBarRightMouseHold()</c> (<c>interface_itemsbar.h:141</c>).</summary>
    /// <param name="item">The item under the button.</param>
    /// <returns>True when the event was consumed.</returns>
    public virtual bool ActionBarRightMouseHold(T item)
    {
        _ = item;

        return false;
    }

    /// <summary>Sizes the table's area from the cell size, <c>calculateRoi()</c> (<c>interface_itemsbar.h:289-304</c>).</summary>
    private void CalculateRoi()
    {
        int width = _tableSize.Width > 0
            ? (_tableSize.Width * _singleItemSize.Width) + ((_tableSize.Width - 1) * _offsetBetweenItems.Width)
            : 0;
        int height = _tableSize.Height > 0
            ? (_tableSize.Height * _singleItemSize.Height) + ((_tableSize.Height - 1) * _offsetBetweenItems.Height)
            : 0;

        _renderingArea = new Rect(_renderingArea.X, _renderingArea.Y, width, height);
    }

    /// <summary>Places every cell, <c>calculateItemsPos()</c> (<c>interface_itemsbar.h:267-287</c>).</summary>
    private void CalculateItemPositions()
    {
        _itemPositions.Clear();

        if (_tableSize.Width <= 0 || _tableSize.Height <= 0)
        {
            return;
        }

        bool useCustomCount = _customItemsCountInRow.Count == _tableSize.Height;

        int stepX = _offsetBetweenItems.Width + _singleItemSize.Width;
        int stepY = _offsetBetweenItems.Height + _singleItemSize.Height;

        for (int y = 0; y < _tableSize.Height; ++y)
        {
            int countX = useCustomCount ? _customItemsCountInRow[y] : _tableSize.Width;
            int offsetX = useCustomCount ? ((_tableSize.Width - _customItemsCountInRow[y]) * stepX / 2) : 0;

            for (int x = 0; x < countX; ++x)
            {
                _itemPositions.Add(new Point(
                    _renderingArea.X + (x * stepX) + offsetX,
                    _renderingArea.Y + (y * stepY)));
            }
        }
    }
}
