using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Widgets;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The mutable state of the castle details window: the nineteen cells, the four toggles and the army,
/// plus the two places they are written back — <c>updateMetadata()</c> of
/// <c>castleDetailsDialog()</c> (<c>editor_castle_details_window.cpp:810-842</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把这些都摊在一个函数里：勾选标记就是状态（<c>isHidden()</c>），格子自己记等级，军队是窗口的局部变量。端口没有
/// Display，勾选标记不能当状态用，所以状态搬到这里，画的时候读它。
/// </para>
/// <para>
/// <b>town 只在打开时判一次</b>：参考 <c>:645</c> 用当时的已建掩码算 <c>isTown</c>，之后哪怕作者在格子里把城堡建起来也不
/// 重算，于是写回时仍然塞 <c>BUILD_TENT</c>。这个奇怪之处是参考的行为，照抄。
/// </para>
/// <para>
/// <b>两个勾选框的默认值来自地图</b>：「允许建城堡」是「禁建掩码里没有要塞」，「默认守军」是「守军是那串负数哨兵」。
/// 反过来「默认建筑」是 <c>customBuildings</c> 取反——元数据里存的是「作者自己挑了建筑」。
/// </para>
/// </remarks>
public sealed class CastleDetailsState
{
    private readonly List<CastleBuildingCell> _cells;

    /// <summary>
    /// Reads a castle out of the metadata.
    /// </summary>
    /// <param name="metadata">The castle the window edits.</param>
    /// <param name="race">Race of the castle, which decides what upgrades into what.</param>
    /// <param name="color">Owner colour, <see cref="PlayerColor.None"/> for a neutral castle.</param>
    /// <param name="army">The army bar's stack, filled from the metadata by the caller.</param>
    public CastleDetailsState(CastleMetadata metadata, MapRace race, PlayerColor color, ArmyStack army)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(army);

        Metadata = metadata;
        Race = race;
        Army = army;
        IsNeutral = color == PlayerColor.None;
        IsTown = CastleDetailsGrid.IsTown(metadata);
        AllowCastle = CastleDetailsGrid.IsCastleAllowed(metadata);
        CustomBuildings = metadata.CustomBuildings;
        DefaultArmy = IsNeutral && MapFormatArmy.IsDefaultCastleDefenderArmy(metadata);

        _cells = [.. CastleDetailsGrid.CreateCells(race, MapFormatArmy.GetBuiltBuildings(metadata),
            MapFormatArmy.GetBannedBuildings(metadata))];
    }

    /// <summary>The castle being edited.</summary>
    public CastleMetadata Metadata { get; }

    /// <summary>Race of the castle.</summary>
    public MapRace Race { get; }

    /// <summary>The army the army bar shows and edits.</summary>
    public ArmyStack Army { get; }

    /// <summary>Whether the castle has no owner, <c>isNeutral</c>.</summary>
    public bool IsNeutral { get; }

    /// <summary>
    /// Whether the object is a town rather than a castle, fixed when the window opens (<c>:645</c>).
    /// </summary>
    public bool IsTown { get; }

    /// <summary>
    /// State of the RESTRICT button, <c>buildingRestriction</c>: a click on a cell steps down instead of up.
    /// </summary>
    public bool BuildingRestriction { get; set; }

    /// <summary>
    /// Whether the town may build its fort, <c>allowCastleSign</c>. Only meaningful for a town.
    /// </summary>
    public bool AllowCastle { get; set; }

    /// <summary>
    /// Whether the author picked the buildings, <c>defaultBuildingsSign.isHidden()</c>.
    /// </summary>
    public bool CustomBuildings { get; set; }

    /// <summary>
    /// Whether a neutral castle uses the army of its race, <c>defaultArmySign</c>. Only meaningful when neutral.
    /// </summary>
    public bool DefaultArmy { get; set; }

    /// <summary>The nineteen cells, in the order of <see cref="CastleDetailsGrid.GetSlotBuildings"/>.</summary>
    public IReadOnlyList<CastleBuildingCell> Cells => _cells;

    /// <summary>
    /// Moves the eighteen grid cells and the captain onto the window (<c>:751-768</c>).
    /// </summary>
    /// <param name="dialogX">Left edge of the window's active area.</param>
    /// <param name="dialogY">Top edge of the window's active area.</param>
    public void LayoutCells(int dialogX, int dialogY)
    {
        for (int i = 0; i < CastleDetailsGrid.GridSlotCount; ++i)
        {
            Point position = CastleDetailsGrid.GetPosition(i, dialogX, dialogY);
            _cells[i].SetPosition(position.X, position.Y);
        }

        Point captain = CastleDetailsGrid.GetCaptainPosition(dialogX, dialogY);
        _cells[^1].SetPosition(captain.X, captain.Y);
    }

    /// <summary>
    /// Toggles the default buildings checkbox, <c>:927-950</c>.
    /// </summary>
    /// <remarks>
    /// 从「自定义」切回「默认」时参考把每一格都复位，回不到作者刚才挑的那些建筑；从「默认」切到「自定义」则不复位，
    /// 因为格子本来就还在默认状态上。两个方向都只是换勾选标记。
    /// </remarks>
    /// <returns>Whether the window now uses the default buildings.</returns>
    public bool ToggleDefaultBuildings()
    {
        if (!CustomBuildings)
        {
            CustomBuildings = true;
            return false;
        }

        foreach (CastleBuildingCell cell in _cells)
        {
            cell.Reset();
        }

        CustomBuildings = false;
        return true;
    }

    /// <summary>
    /// Toggles the default army checkbox, <c>:962-979</c>.
    /// </summary>
    /// <remarks>
    /// 勾上「默认守军」时参考顺手把军队清空（<c>castleArmy.Reset( false )</c>）：地图里存的将是一串负数，军队那一排显示
    /// 空槽才对得上。取消勾选不清空——此刻军队已经空了，作者从空开始挑。
    /// </remarks>
    /// <returns>Whether the castle now uses the default army.</returns>
    public bool ToggleDefaultArmy()
    {
        if (!DefaultArmy)
        {
            DefaultArmy = true;
            Army.Reset();
            return true;
        }

        DefaultArmy = false;
        return false;
    }

    /// <summary>
    /// Empties the army, what the reset button does (<c>:981-991</c>).
    /// </summary>
    public void ResetArmy() => Army.Reset();

    /// <summary>
    /// Writes the state back into the metadata, <c>updateMetadata()</c> plus the name and the spells the caller already
    /// wrote (<c>:810-842</c>).
    /// </summary>
    /// <remarks>
    /// 建筑那两个列表是整体重建的，不是增删：先清空，再放「城镇的帐篷或城堡的要塞」，再按需放禁建的要塞，最后在
    /// <see cref="CustomBuildings"/> 为真时把每一格的升级链与限制位追加进去。所以「用默认建筑」时元数据里只剩要塞那一条。
    /// </remarks>
    public void WriteMetadata()
    {
        if (IsNeutral && DefaultArmy)
        {
            MapFormatArmy.SetDefaultCastleDefenderArmy(Metadata);
        }
        else
        {
            MapFormatArmy.SaveCastleArmy(Army, Metadata);
        }

        Metadata.BuiltBuildings.Clear();
        Metadata.BannedBuildings.Clear();

        Metadata.BuiltBuildings.Add((uint)(IsTown ? BuildingType.Tent : BuildingType.Castle));

        if (IsTown && !AllowCastle)
        {
            Metadata.BannedBuildings.Add((uint)BuildingType.Castle);
        }

        Metadata.CustomBuildings = CustomBuildings;

        if (!CustomBuildings)
        {
            return;
        }

        foreach (CastleBuildingCell cell in _cells)
        {
            foreach (BuildingType building in cell.BuiltChain)
            {
                Metadata.BuiltBuildings.Add((uint)building);
            }

            if (cell.RestrictedBuilding != BuildingType.Nothing)
            {
                Metadata.BannedBuildings.Add((uint)cell.RestrictedBuilding);
            }
        }
    }
}
