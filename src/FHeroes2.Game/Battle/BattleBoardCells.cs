using FHeroes2.Core.Geometry;

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
}
