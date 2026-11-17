using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Battle;

/// <summary>
/// Where a unit stands: one cell for a one-hex creature, head plus tail for a wide one,
/// <c>Battle::Position</c> (<c>battle_cell.h:196-256</c>, <c>battle_cell.cpp:48-254</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考继承 <c>std::pair&lt;Cell *, Cell *&gt;</c>，<c>first</c> 是头、<c>second</c> 是尾。头尾都是**格子对象本身**
/// 而不是下标，因此「谁站在哪」只有一处真相：格子上的 <c>Unit *</c>。这里同样持有
/// <see cref="BattleCell"/> 引用。
/// </para>
/// <para>
/// <see cref="GetPosition(BattleUnit, int)"/> 只看格子占用与单位朝向；<see cref="GetReachable"/> 还要问竞技场
/// 「这一步走不走得到、花多少移动力」，所以竞技场作为参数传进来，而不是像参考那样从单例取。
/// </para>
/// </remarks>
public sealed class BattlePosition
{
    private readonly BattleBoardCells _board;

    /// <summary>Builds an empty position on the given board.</summary>
    /// <param name="board">The cells the position refers to.</param>
    public BattlePosition(BattleBoardCells board) => _board = board;

    /// <summary>Builds a position and places it right away.</summary>
    /// <param name="board">The cells the position refers to.</param>
    /// <param name="head">Index of the head cell.</param>
    /// <param name="wide">Whether the unit occupies two cells.</param>
    /// <param name="reflect">Whether the tail lies to the right of the head.</param>
    public BattlePosition(BattleBoardCells board, int head, bool wide, bool reflect)
        : this(board)
    {
        Set(head, wide, reflect);
    }

    /// <summary>The cells this position refers to.</summary>
    public BattleBoardCells Board => _board;

    /// <summary>The head cell, <c>Position::GetHead()</c>.</summary>
    public BattleCell? Head { get; private set; }

    /// <summary>The tail cell of a wide unit, <c>Position::GetTail()</c>.</summary>
    public BattleCell? Tail { get; private set; }

    /// <summary>The head's index, or <c>-1</c> when empty.</summary>
    public int HeadIndex => Head?.Index ?? -1;

    /// <summary>The tail's index, or <c>-1</c> when there is none.</summary>
    public int TailIndex => Tail?.Index ?? -1;

    /// <summary>Whether nothing stands here, <c>Position::isEmpty()</c>.</summary>
    public bool IsEmpty => Head == null;

    /// <summary>
    /// Whether the unit faces left, <c>Position::isReflect()</c>: the head has the lower index of the two cells.
    /// </summary>
    public bool IsReflect => Head != null && Tail != null && Head.Index < Tail.Index;

    /// <summary>
    /// Places the position on a cell, <c>Position::Set()</c>.
    /// </summary>
    /// <param name="head">Index of the head cell; an off-board index leaves the position empty.</param>
    /// <param name="wide">Whether the unit occupies two cells.</param>
    /// <param name="reflect">Whether the tail lies to the right of the head.</param>
    /// <remarks>
    /// 参考：<c>battle_cell.cpp:48-58</c>。宽单位的尾巴按朝向取左或右邻格；邻格出界时尾巴为空，
    /// 这一步不报错——调用方用 <see cref="IsEmpty"/> 与 <see cref="IsValidForUnit(BattleUnit)"/> 判断。
    /// </remarks>
    public void Set(int head, bool wide, bool reflect)
    {
        Head = _board.GetCell(head);

        if (Head != null && wide)
        {
            Tail = _board.GetCell(Head.Index, reflect ? CellDirection.Right : CellDirection.Left);
        }
        else
        {
            Tail = null;
        }
    }

    /// <summary>Swaps head and tail, <c>Position::Swap()</c>; a one-hex position does nothing.</summary>
    public void Swap()
    {
        if (Head == null || Tail == null)
        {
            return;
        }

        (Head, Tail) = (Tail, Head);
    }

    /// <summary>Copies another position, the reference's <c>_position = pos</c>.</summary>
    /// <param name="other">The position to copy.</param>
    public void CopyFrom(BattlePosition other)
    {
        Head = other.Head;
        Tail = other.Tail;
    }

    /// <summary>Empties the position.</summary>
    public void Clear()
    {
        Head = null;
        Tail = null;
    }

    /// <summary>Whether the cell is one of the two this position covers, <c>Position::contains()</c>.</summary>
    /// <param name="index">The cell index.</param>
    public bool Contains(int index) => (Head != null && Head.Index == index) || (Tail != null && Tail.Index == index);

    /// <summary>
    /// Whether the position fits a unit, <c>Position::isValidForUnit()</c>: one cell for a narrow unit, two for a
    /// wide one.
    /// </summary>
    /// <param name="unit">The unit that would stand here; <see langword="null"/> never fits.</param>
    public bool IsValidForUnit(BattleUnit? unit)
    {
        if (unit == null || Head == null)
        {
            return false;
        }

        return unit.IsWide ? Tail != null : Tail == null;
    }

    /// <summary>
    /// The rectangle the unit covers, <c>Position::GetRect()</c>.
    /// </summary>
    /// <returns>The head cell's rectangle, its union with the tail's, or <see cref="Rect.Empty"/>.</returns>
    public Rect GetRect()
    {
        if (Head == null)
        {
            return Rect.Empty;
        }

        if (Tail == null)
        {
            return Head.Position;
        }

        return BoundaryRect(Head.Position, Tail.Position);
    }

    /// <summary>
    /// Where a unit would stand after stepping onto a cell, <c>Position::GetPosition()</c>
    /// (<c>battle_cell.cpp:78-129</c>).
    /// </summary>
    /// <param name="unit">The unit that would move.</param>
    /// <param name="dst">The cell it moves to.</param>
    /// <returns>
    /// The position, or an empty one when the move is impossible in principle: the head cell is off the board or
    /// taken, or a wide unit finds no room for its tail.
    /// </returns>
    /// <remarks>
    /// 「本来就站在那儿」的格子算通得过：<paramref name="unit"/> 自己的头尾格永远可以重复使用，否则宽单位向
    /// 自己尾巴的方向挪半格会被自己挡住。参考先试尾巴在左/右，再试另一种，两种朝向都试过才算不能走。
    /// </remarks>
    public static BattlePosition GetPosition(BattleUnit unit, int dst)
    {
        BattlePosition result = new(unit.Position.Board);

        if (unit.IsWide)
        {
            BattlePosition CheckCells(BattleCell? headCell, BattleCell? tailCell)
            {
                BattlePosition pos = new(unit.Position.Board);

                if (headCell == null || (!unit.Position.Contains(headCell.Index) && !headCell.IsPassable(true)))
                {
                    return pos;
                }

                if (tailCell == null || (!unit.Position.Contains(tailCell.Index) && !tailCell.IsPassable(true)))
                {
                    return pos;
                }

                pos.SetHeadAndTail(headCell, tailCell);

                return pos;
            }

            BattlePosition TryTail(CellDirection tailDirection)
            {
                BattleCell? headCell = unit.Position.Board.GetCell(dst);
                BattleCell? tailCell = unit.Position.Board.GetCell(dst, tailDirection);

                return CheckCells(headCell, tailCell);
            }

            result = TryTail(unit.IsReflect ? CellDirection.Right : CellDirection.Left);

            if (result.Head == null || result.Tail == null)
            {
                result = TryTail(unit.IsReflect ? CellDirection.Left : CellDirection.Right);
            }
        }
        else
        {
            BattleCell? headCell = unit.Position.Board.GetCell(dst);

            if (headCell != null && (unit.Position.Contains(headCell.Index) || headCell.IsPassable(true)))
            {
                result.SetHeadAndTail(headCell, null);
            }
        }

        return result;
    }

    /// <summary>
    /// Where a unit can actually end up when it steps onto a cell this turn, <c>Position::GetReachable()</c>
    /// (<c>battle_cell.cpp:131-209</c>).
    /// </summary>
    /// <param name="arena">The battlefield, asked whether the position is reachable and what it costs.</param>
    /// <param name="unit">The unit that would move.</param>
    /// <param name="dst">The cell it is sent to.</param>
    /// <param name="speed">
    /// The movement to spend. The reference's default — no limit — leaves <paramref name="speed"/> empty and asks the
    /// arena whether the cell is reachable <em>this turn</em> instead, which is the cheaper question.
    /// </param>
    /// <returns>The position, or an empty one when the move cannot be made.</returns>
    /// <remarks>
    /// <para>
    /// 与 <see cref="GetPosition"/> 的区别：那个只问「格子上有没有人、尾巴放不放得下」，这个还要问竞技场
    /// 「这一步走不走得到、是不是这一回合能走到」。因此障碍、护城河罚款与移动力都在这里合并判断。
    /// </para>
    /// <para>
    /// 宽单位先试「头落在 dst、尾巴在身后」，再试「尾巴落在 dst、头在身前」——前者是它按当前朝向走过去的结果，
    /// 后者是它倒着蹭过去的结果。参考<b>不</b>检查格子占用：可达性已经由竞技场的寻路器回答过了。
    /// </para>
    /// </remarks>
    public static BattlePosition GetReachable(BattleArena arena, BattleUnit unit, int dst, uint? speed = null)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(unit);

        BattleBoardCells board = unit.Position.Board;

        BattlePosition Empty() => new(board);

        BattlePosition CheckReachability(BattlePosition pos)
        {
            if (speed.HasValue)
            {
                if (arena.IsPositionReachable(unit, pos, false) && arena.CalculateMoveCost(unit, pos) <= speed.Value)
                {
                    return pos;
                }
            }
            else if (arena.IsPositionReachable(unit, pos, true))
            {
                return pos;
            }

            return Empty();
        }

        BattlePosition CheckCells(BattleCell? headCell, BattleCell? tailCell)
        {
            if (headCell == null || tailCell == null)
            {
                return Empty();
            }

            BattlePosition pos = new(board);
            pos.SetHeadAndTail(headCell, tailCell);

            return CheckReachability(pos);
        }

        if (unit.IsWide)
        {
            CellDirection tailDirection = unit.IsReflect ? CellDirection.Right : CellDirection.Left;

            if (BattleBoard.IsValidDirection(dst, tailDirection))
            {
                BattlePosition headPosition = CheckCells(
                    board.GetCell(dst),
                    board.GetCell(BattleBoard.GetIndexDirection(dst, tailDirection)));

                if (headPosition.Head != null && headPosition.Tail != null)
                {
                    return headPosition;
                }
            }

            CellDirection headDirection = unit.IsReflect ? CellDirection.Left : CellDirection.Right;

            if (BattleBoard.IsValidDirection(dst, headDirection))
            {
                return CheckCells(
                    board.GetCell(BattleBoard.GetIndexDirection(dst, headDirection)),
                    board.GetCell(dst));
            }

            return Empty();
        }

        BattleCell? headCell = board.GetCell(dst);

        if (headCell == null)
        {
            return Empty();
        }

        BattlePosition position = new(board);
        position.SetHeadAndTail(headCell, null);

        return CheckReachability(position);
    }

    /// <summary>
    /// A stable order over positions, <c>Position::operator&lt;</c>: by cell index, never by object identity.
    /// </summary>
    /// <param name="other">The position to compare to.</param>
    /// <returns>A negative number, zero, or a positive number, like <see cref="IComparable{T}.CompareTo(T)"/>.</returns>
    /// <remarks>
    /// 参考特意改成按下标比较，否则同一个局面在两次运行里指针不同、排序结果就不同。
    /// </remarks>
    public int CompareTo(BattlePosition other)
    {
        int headCompare = HeadIndex.CompareTo(other.HeadIndex);

        return headCompare != 0 ? headCompare : TailIndex.CompareTo(other.TailIndex);
    }

    /// <summary>Fills the two cells without any checks; the reference's plain field assignment.</summary>
    /// <param name="head">The head cell.</param>
    /// <param name="tail">The tail cell, <see langword="null"/> for a one-hex position.</param>
    private void SetHeadAndTail(BattleCell? head, BattleCell? tail)
    {
        Head = head;
        Tail = tail;
    }

    /// <summary>The bounding rectangle of two cell rectangles, <c>getBoundaryRect()</c>.</summary>
    /// <param name="first">One rectangle.</param>
    /// <param name="second">The other rectangle.</param>
    /// <remarks>
    /// 参考的 <c>getBoundaryRect()</c> 是纯 <c>std::min</c>/<c>std::max</c>；<see cref="Rect.Union"/> 会把空矩形当成
    /// 单位元，语义在这里并不等价，所以两步都照抄。
    /// </remarks>
    private static Rect BoundaryRect(Rect first, Rect second)
    {
        int left = Math.Min(first.X, second.X);
        int top = Math.Min(first.Y, second.Y);
        int right = Math.Max(first.X + first.Width, second.X + second.Width);
        int bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);

        return new Rect(left, top, right - left, bottom - top);
    }
}
