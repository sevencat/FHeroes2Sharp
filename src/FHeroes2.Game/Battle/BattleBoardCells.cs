using System.Diagnostics;

using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The 99 cells a battlefield consists of, i.e. the array the reference reaches through
/// <c>Board::GetCell()</c> (<c>battle_board.cpp</c>) and keeps in <c>Battle::Arena::board</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考的格子数组挂在 <c>Arena</c> 单例上，<c>Board::GetCell(index)</c> 只是一个静态转发。端口没有单例，
/// 于是把数组本身做成对象：<see cref="BattleArena"/> 落地时它就是竞技场的成员，眼下由
/// <see cref="BattlePosition"/> 与 <see cref="BattleUnit"/> 持有。
/// </para>
/// <para>
/// 越界问格子一律答 <see langword="null"/>，和参考一致——<c>Board::GetCell()</c> 对非法下标返回
/// <c>nullptr</c>，调用方靠这一点判断「棋盘外」。
/// </para>
/// </remarks>
public sealed class BattleBoardCells
{
    private readonly BattleCell[] _cells;

    /// <summary>Builds the 99 cells, all laid out at the origin.</summary>
    public BattleBoardCells()
    {
        _cells = new BattleCell[BattleBoard.SizeInCells];

        for (int index = 0; index < _cells.Length; ++index)
        {
            _cells[index] = new BattleCell(index);
        }
    }

    /// <summary>How many cells there are.</summary>
    public int Count => _cells.Length;

    /// <summary>One cell by index, <c>Board::GetCell()</c>.</summary>
    /// <param name="index">The index on the board.</param>
    /// <returns>The cell, or <see langword="null"/> when the index is off the board.</returns>
    public BattleCell? GetCell(int index) => BattleBoard.IsValidIndex(index) ? _cells[index] : null;

    /// <summary>One cell next to another, <c>Board::GetCell( index, direction )</c>.</summary>
    /// <param name="index">The cell to start from.</param>
    /// <param name="direction">Which way to step.</param>
    /// <returns>The neighbouring cell, or <see langword="null"/> when the step leaves the board.</returns>
    public BattleCell? GetCell(int index, CellDirection direction)
    {
        if (!BattleBoard.IsValidDirection(index, direction))
        {
            return null;
        }

        return _cells[BattleBoard.GetIndexDirection(index, direction)];
    }

    /// <summary>Places every cell inside the battlefield rectangle, <c>Cell::SetArea()</c> for all of them.</summary>
    /// <param name="area">The battlefield's rectangle in window coordinates.</param>
    public void SetArea(Rect area)
    {
        foreach (BattleCell cell in _cells)
        {
            cell.SetArea(area);
        }
    }

    /// <summary>
    /// Lays the ground decoration of a field battle, <c>Board::SetCovrObjects()</c>
    /// (<c>battle_board.cpp:573-695</c>).
    /// </summary>
    /// <param name="icn">The chosen <c>COVR</c> image; <see cref="IcnType.Unknown"/> places nothing.</param>
    /// <remarks>
    /// <para>
    /// The decoration is not merely cosmetic: the blocked cells it writes (<c>0x40</c>) are the ones the pathfinder
    /// refuses to walk through and that obstacle placement avoids. The reference does the same thing — ground
    /// decoration first, then objects — so the number of free cells the objects may use depends on this call.
    /// </para>
    /// <para>每组格号是画面上同一块装饰占住的格子；参考把它们按 <c>ICN</c> 号分组，此处照抄。</para>
    /// </remarks>
    public void SetCovrObjects(IcnType icn)
    {
        switch (icn)
        {
        case IcnType.Covr0001:
        case IcnType.Covr0007:
        case IcnType.Covr0013:
        case IcnType.Covr0019:
            Block(25, 26, 27, 28, 40, 51);
            break;

        case IcnType.Covr0002:
        case IcnType.Covr0008:
        case IcnType.Covr0014:
        case IcnType.Covr0020:
            Block(47, 48, 49, 50, 51);
            break;

        case IcnType.Covr0003:
        case IcnType.Covr0015:
        case IcnType.Covr0021:
            Block(35, 41, 46, 47, 48, 49, 50, 51);
            break;

        case IcnType.Covr0009:
            Block(35, 40, 46, 47, 48, 49, 50);
            break;

        case IcnType.Covr0004:
        case IcnType.Covr0010:
        case IcnType.Covr0016:
        case IcnType.Covr0022:
            Block(41, 51, 58, 59, 60, 61, 62);
            break;

        case IcnType.Covr0005:
        case IcnType.Covr0017:
            Block(24, 25, 26, 27, 28, 29, 30, 58, 59, 60, 61, 62, 63, 68, 74);
            break;

        case IcnType.Covr0006:
        case IcnType.Covr0018:
            Block(14, 15, 16, 17, 18, 24, 68, 80, 81, 82, 83, 84);
            break;

        case IcnType.Covr0011:
        case IcnType.Covr0023:
            Block(15, 25, 36, 51, 62, 71, 72);
            break;

        case IcnType.Covr0012:
        case IcnType.Covr0024:
            Block(18, 29, 41, 59, 70, 82, 83);
            break;

        default:
            break;
        }
    }

    /// <summary>
    /// Scatters the obstacles of a field battle, <c>Board::SetCobjObjects()</c> (<c>battle_board.cpp:416-561</c>).
    /// </summary>
    /// <param name="mainObjectType">The tile's main object; a graveyard replaces the ground-specific set.</param>
    /// <param name="ground">The tile's ground, which decides which objects may appear.</param>
    /// <param name="generator">The battle's seeded generator, one per field battle.</param>
    /// <remarks>
    /// <para>
    /// The reference takes the <c>Maps::Tile</c> and asks it for the main object and the ground. The port takes the two
    /// answers instead of the tile, so the board does not have to know the world at all.
    /// </para>
    /// <para>
    /// 两条硬约束照抄：双格障碍不能落在第 8 列（会把留给部队的第 9 列盖掉），高障碍不能落在最上面两行。
    /// 放置数量取「剩余候选」与「随机上限」的较小值，上限按已有大障碍（<c>0x40</c>）的格数分四档。
    /// </para>
    /// </remarks>
    public void SetCobjObjects(Mp2ObjectType mainObjectType, MapGround ground, Pcg32 generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        List<IcnType> objects = [];

        if (mainObjectType == Mp2ObjectType.Graveyard)
        {
            objects.Add(IcnType.Cobj0000);
            objects.Add(IcnType.Cobj0001);
            objects.Add(IcnType.Cobj0025);
        }
        else
        {
            switch (ground)
            {
            case MapGround.Desert:
                objects.Add(IcnType.Cobj0009);
                objects.Add(IcnType.Cobj0024);
                break;

            case MapGround.Snow:
                objects.Add(IcnType.Cobj0022);
                objects.Add(IcnType.Cobj0026);
                break;

            case MapGround.Swamp:
                objects.Add(IcnType.Cobj0005);
                objects.Add(IcnType.Cobj0006);
                objects.Add(IcnType.Cobj0007);
                objects.Add(IcnType.Cobj0008);
                objects.Add(IcnType.Cobj0011);
                objects.Add(IcnType.Cobj0012);
                objects.Add(IcnType.Cobj0014);
                objects.Add(IcnType.Cobj0015);
                objects.Add(IcnType.Cobj0016);
                objects.Add(IcnType.Cobj0017);
                objects.Add(IcnType.Cobj0027);
                break;

            case MapGround.Beach:
                objects.Add(IcnType.Cobj0005);
                objects.Add(IcnType.Cobj0011);
                objects.Add(IcnType.Cobj0017);
                break;

            case MapGround.Dirt:
                objects.Add(IcnType.Cobj0002);
                objects.Add(IcnType.Cobj0005);
                objects.Add(IcnType.Cobj0007);
                objects.Add(IcnType.Cobj0011);
                objects.Add(IcnType.Cobj0014);
                objects.Add(IcnType.Cobj0019);
                objects.Add(IcnType.Cobj0027);
                break;

            case MapGround.Grass:
                objects.Add(IcnType.Cobj0002);
                objects.Add(IcnType.Cobj0004);
                objects.Add(IcnType.Cobj0005);
                objects.Add(IcnType.Cobj0008);
                objects.Add(IcnType.Cobj0011);
                objects.Add(IcnType.Cobj0012);
                objects.Add(IcnType.Cobj0014);
                objects.Add(IcnType.Cobj0015);
                objects.Add(IcnType.Cobj0019);
                objects.Add(IcnType.Cobj0027);
                objects.Add(IcnType.Cobj0028);
                break;

            case MapGround.Wasteland:
                objects.Add(IcnType.Cobj0009);
                objects.Add(IcnType.Cobj0013);
                objects.Add(IcnType.Cobj0018);
                objects.Add(IcnType.Cobj0020);
                objects.Add(IcnType.Cobj0021);
                objects.Add(IcnType.Cobj0024);
                break;

            case MapGround.Lava:
                objects.Add(IcnType.Cobj0007);
                objects.Add(IcnType.Cobj0029);
                objects.Add(IcnType.Cobj0031);
                break;

            case MapGround.Water:
                objects.Add(IcnType.Cobj0003);
                objects.Add(IcnType.Cobj0010);
                objects.Add(IcnType.Cobj0023);
                break;

            default:
                break;
            }
        }

        Rand.ShuffleWithGen(objects, generator);

        int largeObstacleHexCount = 0;
        foreach (BattleCell cell in _cells)
        {
            if (cell.Object == 0x40)
            {
                ++largeObstacleHexCount;
            }
        }

        int maxSmallObstacleCount;
        int maxTwoHexObstacleCount;

        if (largeObstacleHexCount == 0)
        {
            maxSmallObstacleCount = 6;
            maxTwoHexObstacleCount = 3;
        }
        else if (largeObstacleHexCount <= 7)
        {
            maxSmallObstacleCount = 4;
            maxTwoHexObstacleCount = 2;
        }
        else if (largeObstacleHexCount <= 13)
        {
            maxSmallObstacleCount = 3;
            maxTwoHexObstacleCount = 2;
        }
        else
        {
            maxSmallObstacleCount = 2;
            maxTwoHexObstacleCount = 1;
        }

        int twoHexCount = 0;
        objects.RemoveAll(icn => IsTwoHexObject(icn) && ++twoHexCount > maxTwoHexObstacleCount);

        int objectsToPlace = Math.Min(objects.Count, (int)Rand.GetWithGen(0U, (uint)maxSmallObstacleCount, generator));

        for (int i = 0; i < objectsToPlace; ++i)
        {
            // Two-hex obstacles may not start on column 8: the second half would cover column 9, which is kept for units.
            bool checkRightCell = IsTwoHexObject(objects[i]);

            // Tall obstacles such as trees may not be placed on the top two rows.
            bool isTallObstacle = IsTallObject(objects[i]);

            int destination;
            do
            {
                destination = GetRandomObstaclePosition(generator);
            }
            while (_cells[destination].Object != 0
                || (checkRightCell && (_cells[destination + 1].Object != 0 || (destination % BattleBoard.WidthInCells) == 8))
                || (isTallObstacle && destination < (BattleBoard.WidthInCells * 2)));

            SetCobjObject(objects[i], destination);
        }
    }

    private static int GetRandomObstaclePosition(Pcg32 generator) =>
        (int)Rand.GetWithGen(2U, 8U, generator) + (11 * (int)Rand.GetWithGen(0U, 8U, generator));

    private static bool IsTwoHexObject(IcnType icn) => icn switch
    {
        IcnType.Cobj0004 or IcnType.Cobj0005 or IcnType.Cobj0007 or IcnType.Cobj0011 or IcnType.Cobj0014
            or IcnType.Cobj0015 or IcnType.Cobj0017 or IcnType.Cobj0018 or IcnType.Cobj0019 or IcnType.Cobj0020
            or IcnType.Cobj0022 or IcnType.Cobj0030 or IcnType.Cobj0031 => true,
        _ => false,
    };

    private static bool IsTallObject(IcnType icn) => icn switch
    {
        IcnType.Cobj0002 or IcnType.Cobj0009 or IcnType.Cobj0013 or IcnType.Cobj0021 or IcnType.Cobj0027
            or IcnType.Cobj0028 or IcnType.Cobj0031 => true,
        _ => false,
    };

    private void SetCobjObject(IcnType icn, int destination)
    {
        _cells[destination].Object = 0x80 + ((int)icn - (int)IcnType.Cobj0000);

        if (IsTwoHexObject(icn))
        {
            Debug.Assert(_cells[destination + 1].Object == 0, "A two-hex obstacle needs its second cell free.");
            _cells[destination + 1].Object = 0x40;
        }
    }

    private void Block(params int[] indexes)
    {
        foreach (int index in indexes)
        {
            _cells[index].Object = 0x40;
        }
    }
}
