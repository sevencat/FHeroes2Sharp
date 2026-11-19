using System.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Castle;

/// <summary>What clicking a dwelling of the town screen is about to do.</summary>
/// <remarks>
/// The reference calls the recruit or the buy-building dialog straight from the click. The port has no dialogs here,
/// so the click answers what it would have done and the caller decides — which is also what makes the answer worth
/// testing.
/// </remarks>
public enum DwellingAction
{
    /// <summary>The dwelling is built: open the recruit dialog.</summary>
    Recruit,

    /// <summary>The dwelling is not built and the town is a castle: open the buy-building dialog.</summary>
    Build,

    /// <summary>The dwelling is not built and the town is still a tent: tell the player a castle comes first.</summary>
    NeedCastle,

    /// <summary>The right button: show what the dwelling holds and produces.</summary>
    ShowInfo,
}

/// <summary>One dwelling in the town screen's bottom bar, <c>castle/buildinginfo.h:84-91</c>.</summary>
public sealed class DwellingItem
{
    /// <summary>Wraps a dwelling bit, <c>DwellingItem::DwellingItem()</c>.</summary>
    /// <param name="dwelling">One of the six base dwellings — never an upgrade.</param>
    public DwellingItem(BuildingType dwelling)
    {
        Debug.Assert(CastleBuildings.IsDwelling(dwelling), "a dwelling item holds a dwelling");

        Dwelling = dwelling;
    }

    /// <summary>The dwelling, <c>DwellingItem::dwType</c>.</summary>
    public BuildingType Dwelling { get; }
}

/// <summary>
/// The bottom bar of the town screen: the six dwellings, their stock and their weekly growth —
/// <c>castle/buildinginfo.cpp:635-717</c> and the layout of <c>gui/interface_itemsbar.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference's <c>ItemsBar</c> is a template over the item type, so the port keeps the layout half here rather
/// than making a generic bar for its single user: the content is always the six dwellings, in level order, and the
/// table size and the rendering offset are the town screen's to set.
/// </para>
/// <para>
/// The item positions are recomputed whenever the item size, the gap or the table size changes, exactly like
/// <c>calculateRoi()</c> and <c>calculateItemsPos()</c>: the reference's order of setter calls matters because the
/// layout is not redone by <c>SetContent()</c>.
/// </para>
/// <para>
/// Drawing and the dialogs are the town screen's job: <see cref="GetGrowth"/> and <see cref="GetMonstersInDwelling"/>
/// answer what it would print, <see cref="GetLeftClickAction"/> what a click would open.
/// </para>
/// </remarks>
public sealed class DwellingsBar
{
    private readonly Castle _castle;
    private readonly DwellingItem[] _items;

    private Size _itemSize;
    private Size _tableSize;
    private Size _inBetweenItemsSize;
    private Point _renderingOffset;
    private Rect _area;
    private Rect[] _itemAreas;

    /// <summary>
    /// Fills the bar with the six dwellings of a town, <c>DwellingsBar::DwellingsBar()</c>
    /// (<c>buildinginfo.cpp:635-651</c>).
    /// </summary>
    /// <param name="castle">The town whose dwellings the bar shows.</param>
    /// <param name="itemSize">The size of one dwelling's icon.</param>
    public DwellingsBar(Castle castle, Size itemSize)
    {
        ArgumentNullException.ThrowIfNull(castle);

        _castle = castle;
        _items =
        [
            new DwellingItem(BuildingType.DwellingMonster1),
            new DwellingItem(BuildingType.DwellingMonster2),
            new DwellingItem(BuildingType.DwellingMonster3),
            new DwellingItem(BuildingType.DwellingMonster4),
            new DwellingItem(BuildingType.DwellingMonster5),
            new DwellingItem(BuildingType.DwellingMonster6),
        ];

        _itemSize = itemSize;
        _area = Rect.Empty;
        _itemAreas = [];
    }

    /// <summary>The town the bar belongs to.</summary>
    public Castle Castle => _castle;

    /// <summary>The six dwellings, in level order.</summary>
    public IReadOnlyList<DwellingItem> Items => _items;

    /// <summary>The rectangle the bar occupies, <c>ItemsBar::GetArea()</c>.</summary>
    public Rect Area => _area;

    /// <summary>The rectangles of the icons, row by row, <c>ItemsBar::itemPos</c>.</summary>
    public IReadOnlyList<Rect> ItemAreas => _itemAreas;

    /// <summary>The size of one icon.</summary>
    public Size ItemSize
    {
        get => _itemSize;
        set
        {
            Debug.Assert(value.Width > 0 && value.Height > 0, "an icon has a size");

            _itemSize = value;
            CalculateRoi();
            CalculateItemsPos();
        }
    }

    /// <summary>How many icons per row and per column, <c>ItemsBar::setTableSize()</c>.</summary>
    public Size TableSize
    {
        get => _tableSize;
        set
        {
            Debug.Assert(value.Width > 0 && value.Height > 0, "a table has a size");

            _tableSize = value;
            CalculateRoi();
            CalculateItemsPos();
        }
    }

    /// <summary>The gap between two icons, <c>ItemsBar::setInBetweenItemsOffset()</c>.</summary>
    public Size InBetweenItemsSize
    {
        get => _inBetweenItemsSize;
        set
        {
            _inBetweenItemsSize = value;
            CalculateRoi();
            CalculateItemsPos();
        }
    }

    /// <summary>Where the top-left of the bar sits on the screen, <c>ItemsBar::setRenderingOffset()</c>.</summary>
    /// <remarks>
    /// The reference recomputes only the icon positions here and leaves the bar's own rectangle where it was, so a
    /// screen that moves the bar after setting the table ends up with a stale <c>getArea()</c>. The port recomputes
    /// both: <see cref="Area"/> answers about the bar as it is now, whatever the order of the setter calls was.
    /// </remarks>
    public Point RenderingOffset
    {
        get => _renderingOffset;
        set
        {
            _renderingOffset = value;
            CalculateRoi();
            CalculateItemsPos();
        }
    }

    /// <summary>
    /// The dwelling under a point of the screen, <c>ItemsBar::GetItem()</c>.
    /// </summary>
    /// <param name="position">A point in screen coordinates.</param>
    /// <returns>The dwelling, or <see langword="null"/> when the point hits nothing.</returns>
    public DwellingItem? GetItemAt(Point position)
    {
        if (_tableSize.Width <= 0 || _tableSize.Height <= 0)
        {
            return null;
        }

        int index = 0;

        for (int y = 0; y < _tableSize.Height; ++y)
        {
            for (int x = 0; x < _tableSize.Width; ++x, ++index)
            {
                if (index >= _items.Length)
                {
                    return null;
                }

                if (_itemAreas[index].Contains(position))
                {
                    return _items[index];
                }
            }
        }

        return null;
    }

    /// <summary>Whether the dwelling has been built, after the upgrade rules of the town.</summary>
    /// <param name="item">The dwelling.</param>
    /// <returns><see langword="true"/> when the town can recruit from it.</returns>
    public bool IsBuilt(DwellingItem item) => _castle.IsBuild(GetActualDwelling(item));

    /// <summary>How many creatures are waiting in the dwelling, <c>Castle::getMonstersInDwelling()</c>.</summary>
    /// <param name="item">The dwelling.</param>
    /// <returns>The count, zero for a dwelling that is not built.</returns>
    public uint GetMonstersInDwelling(DwellingItem item) => _castle.GetMonstersInDwelling(GetActualDwelling(item));

    /// <summary>
    /// What the dwelling adds to its creature's growth every week, <c>DwellingsBar::RedrawItem()</c>
    /// (<c>buildinginfo.cpp:676-682</c>).
    /// </summary>
    /// <remarks>
    /// The well adds to every dwelling, the landmark building adds its eight to the first level only — the reference's
    /// <c>DWELLING_MONSTER1 == dwType</c> test is against the resolved dwelling, so an upgraded first level still
    /// counts, which cannot happen in the shipped towns but is the reference's answer.
    /// </remarks>
    /// <param name="item">The dwelling.</param>
    /// <returns>The growth shown next to the icon.</returns>
    public uint GetGrowth(DwellingItem item)
    {
        BuildingType dwelling = GetActualDwelling(item);
        uint growth = MonsterStatistics.GetGeneralStats(DwellingMonsters.FromDwelling(_castle.Race, dwelling))
            .BaseGrowth;

        if (_castle.IsBuild(BuildingType.Well))
        {
            growth += Castle.GrownWell;
        }

        if (_castle.IsBuild(BuildingType.Well2) && dwelling == BuildingType.DwellingMonster1)
        {
            growth += Castle.GrownWel2;
        }

        return growth;
    }

    /// <summary>
    /// What a left click on the dwelling would open, <c>DwellingsBar::ActionBarLeftMouseSingleClick()</c>
    /// (<c>buildinginfo.cpp:689-708</c>).
    /// </summary>
    /// <remarks>
    /// Three answers in the reference's order of interest: recruit from a built dwelling, ask for a castle when the
    /// town is still a tent, buy the dwelling otherwise.
    /// </remarks>
    /// <param name="item">The dwelling.</param>
    /// <returns>The action to take.</returns>
    public DwellingAction GetLeftClickAction(DwellingItem item)
    {
        if (IsBuilt(item))
        {
            return DwellingAction.Recruit;
        }

        return _castle.IsBuild(BuildingType.Castle) ? DwellingAction.Build : DwellingAction.NeedCastle;
    }

    /// <summary>
    /// What a right click on the dwelling shows, <c>DwellingsBar::ActionBarRightMouseHold()</c>
    /// (<c>buildinginfo.cpp:710-716</c>).
    /// </summary>
    /// <param name="item">The dwelling.</param>
    /// <returns>Always <see cref="DwellingAction.ShowInfo"/>.</returns>
    public DwellingAction GetRightClickAction(DwellingItem _) => DwellingAction.ShowInfo;

    private BuildingType GetActualDwelling(DwellingItem item) => _castle.GetActualDwelling(item.Dwelling);

    /// <summary>The bar's own rectangle, <c>ItemsBar::calculateRoi()</c>.</summary>
    private void CalculateRoi()
    {
        int width = _tableSize.Width > 0
            ? (_tableSize.Width * _itemSize.Width) + ((_tableSize.Width - 1) * _inBetweenItemsSize.Width)
            : 0;
        int height = _tableSize.Height > 0
            ? (_tableSize.Height * _itemSize.Height) + ((_tableSize.Height - 1) * _inBetweenItemsSize.Height)
            : 0;

        _area = new Rect(_renderingOffset.X, _renderingOffset.Y, width, height);
    }

    /// <summary>The icon rectangles, <c>ItemsBar::calculateItemsPos()</c>.</summary>
    private void CalculateItemsPos()
    {
        if (_tableSize.Width <= 0 || _tableSize.Height <= 0)
        {
            _itemAreas = [];
            return;
        }

        int stepX = _inBetweenItemsSize.Width + _itemSize.Width;
        int stepY = _inBetweenItemsSize.Height + _itemSize.Height;
        var positions = new Rect[_tableSize.Width * _tableSize.Height];
        int index = 0;

        for (int y = 0; y < _tableSize.Height; ++y)
        {
            for (int x = 0; x < _tableSize.Width; ++x, ++index)
            {
                positions[index] = new Rect(
                    _renderingOffset.X + (x * stepX),
                    _renderingOffset.Y + (y * stepY),
                    _itemSize.Width,
                    _itemSize.Height);
            }
        }

        _itemAreas = positions;
    }
}
