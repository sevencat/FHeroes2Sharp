using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Editor;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The nineteen building slots of the castle details window and where they sit,
/// <c>castleDetailsDialog()</c> (<c>editor_castle_details_window.cpp:729-768</c>).
/// </summary>
/// <remarks>
/// <para>
/// 十八格排成三列六行，从 <c>(5, 2)</c> 起、行距 75；行与行之间有两处多 5 像素（第一行与第四行之后），因为法师公会与
/// 珍宝店那一列的名字条更高。第十九格是队长，单独钉在右侧面板上，不在这张网格里。
/// </para>
/// <para>
/// 槽位顺序是参考写死的：六个住所、法师公会、酒馆（亡灵族是神龛）、盗贼公会、船坞、雕像、市场、水井、喷泉、特殊建筑、
/// 左右箭塔、护城河、队长。名字条的宽度按这个顺序预先算好，换顺序会让整块界面错位。
/// </para>
/// </remarks>
public static class CastleDetailsGrid
{
    /// <summary>Number of building slots, <c>buildingsSize</c>.</summary>
    public const int SlotCount = 19;

    /// <summary>Number of slots in the three-column grid, that is, all of them but the captain.</summary>
    public const int GridSlotCount = SlotCount - 1;

    /// <summary>Number of columns, <c>i % 3</c>.</summary>
    public const int ColumnCount = 3;

    /// <summary>Horizontal distance between two slots, <c>144</c>.</summary>
    public const int CellSpacingX = 144;

    /// <summary>Left edge of the right panel, <c>rightPartOffsetX</c>.</summary>
    public const int RightPanelOffsetX = 438;

    /// <summary>
    /// The building of each slot, <c>buildings[i]</c> (<c>:730-749</c>).
    /// </summary>
    /// <param name="race">Race of the castle, which only decides between the tavern and the shrine.</param>
    /// <returns>Nineteen buildings, in the order of the slots.</returns>
    public static IReadOnlyList<BuildingType> GetSlotBuildings(MapRace race)
    {
        return
        [
            BuildingType.DwellingMonster1,
            BuildingType.DwellingMonster2,
            BuildingType.DwellingMonster3,
            BuildingType.DwellingMonster4,
            BuildingType.DwellingMonster5,
            BuildingType.DwellingMonster6,
            BuildingType.MageGuild1,
            race == MapRace.Necromancer ? BuildingType.Shrine : BuildingType.Tavern,
            BuildingType.ThievesGuild,
            BuildingType.Shipyard,
            BuildingType.Statue,
            BuildingType.Marketplace,
            BuildingType.Well,
            BuildingType.Well2,
            BuildingType.Spec,
            BuildingType.LeftTurret,
            BuildingType.RightTurret,
            BuildingType.Moat,
            BuildingType.Captain
        ];
    }

    /// <summary>
    /// The cells of the window, one per slot, all reading the two masks of the castle.
    /// </summary>
    /// <param name="race">Race of the castle.</param>
    /// <param name="builtBuildings">Mask of the buildings the castle stands with.</param>
    /// <param name="bannedBuildings">Mask of the buildings it can never get.</param>
    /// <returns>Nineteen cells, in the order of the slots.</returns>
    public static CastleBuildingCell[] CreateCells(MapRace race, BuildingType builtBuildings, BuildingType bannedBuildings)
    {
        IReadOnlyList<BuildingType> slots = GetSlotBuildings(race);
        CastleBuildingCell[] cells = new CastleBuildingCell[slots.Count];

        for (int i = 0; i < cells.Length; ++i)
        {
            cells[i] = new CastleBuildingCell(slots[i], race, builtBuildings, bannedBuildings);
        }

        return cells;
    }

    /// <summary>
    /// Where a grid slot sits, <c>buildings[i].setPosition()</c> (<c>:755</c>).
    /// </summary>
    /// <param name="index">Slot index, 0 to <see cref="GridSlotCount"/> - 1.</param>
    /// <param name="dialogX">Left edge of the window's active area.</param>
    /// <param name="dialogY">Top edge of the window's active area.</param>
    /// <returns>The top-left corner of the slot.</returns>
    public static Point GetPosition(int index, int dialogX, int dialogY)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, GridSlotCount);

        return new Point(dialogX + 5 + (index % ColumnCount) * CellSpacingX, dialogY + GetRowOffset(index / ColumnCount));
    }

    /// <summary>
    /// Where the captain slot sits, pinned to the right panel (<c>:763-764</c>).
    /// </summary>
    /// <param name="dialogX">Left edge of the window's active area.</param>
    /// <param name="dialogY">Top edge of the window's active area.</param>
    /// <returns>The top-left corner of the slot.</returns>
    public static Point GetCaptainPosition(int dialogX, int dialogY) => new(dialogX + RightPanelOffsetX + 33, dialogY + 232);

    /// <summary>
    /// Whether the object is a town rather than a castle, that is, it stands without a fort,
    /// <c>isTown</c> (<c>:645</c>).
    /// </summary>
    /// <remarks>
    /// 名字容易读反：参考里「建了城堡（要塞）」的是 castle，没建的是 town。这个判断同时决定名字框的占位提示、
    /// 军队那一行的标题，以及要不要显示「允许建城堡」的勾选框。
    /// </remarks>
    /// <param name="metadata">The castle.</param>
    /// <returns><see langword="true"/> when the fort is <b>not</b> built.</returns>
    public static bool IsTown(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return !metadata.BuiltBuildings.Contains((uint)BuildingType.Castle);
    }

    /// <summary>
    /// Whether the cells should be drawn as the author left them, <c>defaultBuildingsSign.isHidden()</c> (<c>:683</c>).
    /// </summary>
    /// <param name="metadata">The castle.</param>
    /// <returns><see langword="true"/> when the castle uses the default buildings.</returns>
    public static bool ShowsDefaultBuildings(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return !metadata.CustomBuildings;
    }

    /// <summary>
    /// Whether the castle may be built, <c>allowCastleSign</c> (<c>:670-675</c>).
    /// </summary>
    /// <param name="metadata">The castle.</param>
    /// <returns><see langword="false"/> when the fort sits in the banned mask.</returns>
    public static bool IsCastleAllowed(CastleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return !metadata.BannedBuildings.Contains((uint)BuildingType.Castle);
    }

    /// <summary>
    /// The top offset of a grid row; the rows after the first and the fourth are five pixels taller,
    /// <c>dstPt.y += ( i == 5 || i == 14 ) ? 80 : 75</c> (<c>:758</c>).
    /// </summary>
    /// <param name="row">Row index, 0 to 5.</param>
    /// <returns>The offset from the top of the active area.</returns>
    private static int GetRowOffset(int row)
    {
        int offset = 2;

        for (int r = 0; r < row; ++r)
        {
            offset += r is 1 or 4 ? 80 : 75;
        }

        return offset;
    }
}
