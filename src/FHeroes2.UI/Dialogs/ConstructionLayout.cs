using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where the construction window puts everything, <c>Castle::_openConstructionDialog()</c>
/// (<c>castle_town.cpp:186-480</c>).
/// </summary>
/// <remarks>
/// 全屏 640×480 的一屏：左边 6 行 × 3 列的建筑格子，右边是城镇图、船长、两个招募位与资源面板，底下是状态条。
/// 坐标全部是参考里的字面值，不随分辨率缩放。
/// </remarks>
public static class ConstructionLayout
{
    /// <summary>The width of the window, <c>fheroes2::Display::DEFAULT_WIDTH</c>.</summary>
    public const int Width = 640;

    /// <summary>The height of the window, <c>fheroes2::Display::DEFAULT_HEIGHT</c>.</summary>
    public const int Height = 480;

    /// <summary>The left edge of the first column of buildings, <c>castle_town.cpp:221</c>.</summary>
    public const int ColumnLeft = 5;

    /// <summary>The left edge of the second column, <c>castle_town.cpp:225</c>.</summary>
    public const int ColumnMiddle = 149;

    /// <summary>The left edge of the third column, <c>castle_town.cpp:229</c>.</summary>
    public const int ColumnRight = 293;

    /// <summary>The top edge of the first row of buildings.</summary>
    public const int RowTop = 2;

    /// <summary>The top edge of the second row, <c>castle_town.cpp:232</c>.</summary>
    public const int RowSecond = 77;

    /// <summary>The top edge of the third row, <c>castle_town.cpp:266</c>.</summary>
    public const int RowThird = 157;

    /// <summary>The top edge of the fourth row, <c>castle_town.cpp:283</c>.</summary>
    public const int RowFourth = 232;

    /// <summary>The top edge of the fifth row, <c>castle_town.cpp:300</c>.</summary>
    public const int RowFifth = 307;

    /// <summary>The top edge of the sixth row, <c>castle_town.cpp:317</c>.</summary>
    public const int RowSixth = 387;

    /// <summary>Where the town picture goes, <c>DrawImageCastle( cur_pt + (459, 6) )</c>.</summary>
    public static readonly Point CastleImageOffset = new(459, 6);

    /// <summary>Where the town name is centred, <c>castle_town.cpp:216</c>.</summary>
    public const int NameCenterX = 538;

    /// <summary>The top edge of the town name, <c>castle_town.cpp:216</c>.</summary>
    public const int NameTop = 2;

    /// <summary>
    /// The stone plate that covers the captain's stats while there is no captain,
    /// <c>castle_town.cpp:198-207</c>.
    /// </summary>
    public static readonly Rect CaptainPlateArea = new(530, 163, 110, 84);

    /// <summary>Where the captain's cell goes, <c>castle_town.cpp:336</c>.</summary>
    public static readonly Point CaptainOffset = new(444, 165);

    /// <summary>The top-left of the first captain stat label, <c>castle_town.cpp:360-361</c>.</summary>
    public static readonly Point CaptainSkillOffset = new(535, 170);

    /// <summary>How far the stat value sits from its label, <c>castle_town.cpp:356</c>.</summary>
    public const int CaptainSkillValueOffset = 90;

    /// <summary>The distance between two stat lines, <c>castle_town.cpp:357</c>.</summary>
    public const int CaptainSkillLineHeight = 12;

    /// <summary>The "spread formation" icon, <c>castle_town.cpp:342</c>.</summary>
    public static readonly Point SpreadFormationOffset = new(550, 220);

    /// <summary>The "grouped formation" icon, <c>castle_town.cpp:343</c>.</summary>
    public static readonly Point GroupedFormationOffset = new(585, 220);

    /// <summary>The first hero to recruit, <c>castle_town.cpp:411-413</c>.</summary>
    public static readonly Rect FirstHeroArea = new(443, 260, 102, 93);

    /// <summary>The second hero to recruit, <c>castle_town.cpp:430-432</c>.</summary>
    public static readonly Rect SecondHeroArea = new(443, 363, 102, 94);

    /// <summary>Where EXIT goes, <c>castle_town.cpp:468</c>.</summary>
    public static readonly Point ExitOffset = new(553, 428);

    /// <summary>The top edge of the bottom bar, <c>castle_town.cpp:447</c>.</summary>
    public const int BottomBarTop = Height - 19;

    /// <summary>How many cells the window shows, <c>castle_town.cpp:220-337</c>.</summary>
    public const int SlotCount = 19;

    /// <summary>
    /// The 19 building cells of the window in the reference's order, <c>castle_town.cpp:220-337</c>.
    /// </summary>
    /// <remarks>
    /// 顺序就是参考画它们的顺序，也是状态栏与点击的优先级。第一个格子是六种住所，接着是法师塔（传当前等级，
    /// <see cref="BuildingInfo"/> 自己会换成下一级）、酒馆（亡灵族在新型地图里由 <see cref="BuildingInfo"/>
    /// 换成祭坛）、盗贼公会、船坞、雕像、市场、水井、种族地标、种族特殊建筑、左右箭塔、护城河，最后是船长。
    /// </remarks>
    /// <param name="castle">The town whose mage guild level picks that slot.</param>
    /// <returns>The cells, top to bottom, left to right.</returns>
    public static IReadOnlyList<ConstructionSlot> GetSlots(Castle castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        return
        [
            new(BuildingType.DwellingMonster1, new Point(ColumnLeft, RowTop)),
            new(BuildingType.DwellingMonster2, new Point(ColumnMiddle, RowTop)),
            new(BuildingType.DwellingMonster3, new Point(ColumnRight, RowTop)),
            new(BuildingType.DwellingMonster4, new Point(ColumnLeft, RowSecond)),
            new(BuildingType.DwellingMonster5, new Point(ColumnMiddle, RowSecond)),
            new(BuildingType.DwellingMonster6, new Point(ColumnRight, RowSecond)),
            new(GetMageGuildSlot(castle.GetLevelMageGuild()), new Point(ColumnLeft, RowThird)),
            new(BuildingType.Tavern, new Point(ColumnMiddle, RowThird)),
            new(BuildingType.ThievesGuild, new Point(ColumnRight, RowThird)),
            new(BuildingType.Shipyard, new Point(ColumnLeft, RowFourth)),
            new(BuildingType.Statue, new Point(ColumnMiddle, RowFourth)),
            new(BuildingType.Marketplace, new Point(ColumnRight, RowFourth)),
            new(BuildingType.Well, new Point(ColumnLeft, RowFifth)),
            new(BuildingType.Well2, new Point(ColumnMiddle, RowFifth)),
            new(BuildingType.Spec, new Point(ColumnRight, RowFifth)),
            new(BuildingType.LeftTurret, new Point(ColumnLeft, RowSixth)),
            new(BuildingType.RightTurret, new Point(ColumnMiddle, RowSixth)),
            new(BuildingType.Moat, new Point(ColumnRight, RowSixth)),
            new(BuildingType.Captain, CaptainOffset),
        ];
    }

    /// <summary>
    /// The mage guild level whose next level the slot offers, <c>castle_town.cpp:247-264</c>.
    /// </summary>
    /// <param name="level">The level the town owns, <c>0</c> for none.</param>
    /// <returns>The building to pass to <see cref="BuildingInfo"/>.</returns>
    public static BuildingType GetMageGuildSlot(int level) => level switch
    {
        <= 0 => BuildingType.MageGuild1,
        1 => BuildingType.MageGuild2,
        2 => BuildingType.MageGuild3,
        3 => BuildingType.MageGuild4,
        _ => BuildingType.MageGuild5,
    };

    /// <summary>
    /// Where the flag sits on the captain's picture, <c>GetFlagOffset()</c> (<c>captain.cpp:64-82</c>).
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <returns>The offset from the captain cell's top-left corner.</returns>
    public static Point GetCaptainFlagOffset(MapRace race) => race switch
    {
        MapRace.Knight => new Point(43, 9),
        MapRace.Barbarian => new Point(42, 8),
        MapRace.Sorceress => new Point(43, 9),
        MapRace.Warlock => new Point(41, 9),
        MapRace.Wizard => new Point(42, 10),
        MapRace.Necromancer => new Point(42, 9),
        _ => default,
    };
}

/// <summary>One building cell of the construction window: what may be built there and where.</summary>
/// <param name="Building">The building as the window names it; upgrades and the shrine are resolved later.</param>
/// <param name="Offset">The top-left corner of the cell inside the 640×480 window.</param>
public readonly record struct ConstructionSlot(BuildingType Building, Point Offset);
