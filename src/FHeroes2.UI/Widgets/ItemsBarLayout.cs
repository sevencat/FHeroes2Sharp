using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// Where the cells of a bar of item slots sit, <c>Interface::ItemsBar&lt;Item&gt;</c>
/// (<c>gui/interface_itemsbar.h:35-311</c>) without its contents.
/// </summary>
/// <remarks>
/// <para>
/// A table of equal cells with a gap between them, laid out from a rendering offset, with the option of a different
/// number of cells per row (the artifact and secondary skill bars of the hero dialog put the odd ones in the middle
/// of their row). The widget half of the reference's class — the items, the selection and the event handling — is
/// <see cref="ArmyBar"/>'s business, and <see cref="Castle.DwellingsBar"/> keeps its own two row layout instead of
/// using this.
/// </para>
/// <para>
/// <b>Sizes are recomputed, not cached.</b> The reference calls <c>calculateRoi()</c> and
/// <c>calculateItemsPos()</c> on every setter; the port does the same, so the order the setters are called in does
/// not matter and there is no "you changed the size and forgot to relayout" state to get wrong.
/// </para>
/// <para>
/// <b>The table size is the caller's.</b> The reference's <c>setTableSize()</c> is never called by an army bar
/// itself — every screen that shows one asks for <c>{ 5, 1 }</c> — so this class refuses a table of zero and leaves
/// the job where the reference leaves it, with the screen.
/// </para>
/// </remarks>
public sealed class ItemsBarLayout
{
    private readonly List<Point> _itemPositions = [];
    private readonly List<int> _itemsCountPerRow = [];

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
    /// The area the cells occupy, anchored at the rendering offset and sized by
    /// <see cref="TableSize"/> and <see cref="SingleItemSize"/>.
    /// </summary>
    public Rect RenderingArea => _renderingArea;

    /// <summary>Top left corner of every cell, row by row.</summary>
    public IReadOnlyList<Point> ItemPositions => _itemPositions;

    /// <summary>Number of cells the table holds.</summary>
    public int Count => _itemPositions.Count;

    /// <summary>Whether any cell is laid out at all.</summary>
    public bool IsEmpty => _itemPositions.Count == 0;

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
    /// Gives a row fewer cells than the columns allow, <c>_customItemsCountInRow</c>
    /// (<c>interface_itemsbar.h:242</c>).
    /// </summary>
    /// <remarks>
    /// A row with fewer cells than the others is centred: its cells are shifted right by half of the room the
    /// missing ones would have taken. The reference reads the list only when it has exactly one entry per row, so a
    /// list of the wrong length is ignored rather than applied to the rows it happens to fit.
    /// </remarks>
    /// <param name="counts">Cells per row, or an empty list to use the table's column count everywhere.</param>
    public void SetItemsCountPerRow(IReadOnlyList<int>? counts)
    {
        _itemsCountPerRow.Clear();

        if (counts is not null)
        {
            _itemsCountPerRow.AddRange(counts);
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
    /// The cell under a point, <c>GetItemIterPos()</c> (<c>interface_itemsbar.h:233</c>).
    /// </summary>
    /// <param name="position">The point to test.</param>
    /// <returns>The index of the cell, or -1 when the point is on none of them.</returns>
    public int IndexAt(Point position)
    {
        for (int i = 0; i < _itemPositions.Count; ++i)
        {
            if (new Rect(_itemPositions[i], _singleItemSize).Contains(position))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Sizes the table's area from the cell size, <c>calculateRoi()</c> (<c>interface_itemsbar.h:289</c>).</summary>
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

    /// <summary>Places every cell, <c>calculateItemsPos()</c> (<c>interface_itemsbar.h:267</c>).</summary>
    private void CalculateItemPositions()
    {
        _itemPositions.Clear();

        if (_tableSize.Width <= 0 || _tableSize.Height <= 0)
        {
            return;
        }

        bool useCustomCount = _itemsCountPerRow.Count == _tableSize.Height;

        int stepX = _offsetBetweenItems.Width + _singleItemSize.Width;
        int stepY = _offsetBetweenItems.Height + _singleItemSize.Height;

        for (int y = 0; y < _tableSize.Height; ++y)
        {
            int countX = useCustomCount ? _itemsCountPerRow[y] : _tableSize.Width;
            int offsetX = useCustomCount ? ((_tableSize.Width - _itemsCountPerRow[y]) * stepX / 2) : 0;

            for (int x = 0; x < countX; ++x)
            {
                _itemPositions.Add(new Point(
                    _renderingArea.X + (x * stepX) + offsetX,
                    _renderingArea.Y + (y * stepY)));
            }
        }
    }
}
