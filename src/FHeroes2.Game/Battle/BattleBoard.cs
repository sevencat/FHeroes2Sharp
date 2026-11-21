using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The battlefield grid of <c>Battle::Board</c> (<c>battle_board.h:69-168</c>, <c>battle_board.cpp</c>), minus the
/// parts that need a unit or a rendered cell.
/// </summary>
/// <remarks>
/// <para>
/// The board is 11 columns by 9 rows of offset hexes: even rows are shifted half a cell to the right, which is why
/// <see cref="IsValidDirection"/>, <see cref="GetIndexDirection"/> and <see cref="GetDistance"/> all branch on
/// <c>y % 2</c>. Index 0 is the top left corner and index 98 the bottom right one.
/// </para>
/// <para>
/// This type keeps only the index arithmetic. The overloads that take a <c>Unit</c> or a <c>Position</c> wait for
/// the battle-troop batch; the two that take a unit's state — <see cref="IsMoatIndex"/> and
/// <see cref="GetMoatCellsForUnit"/>'s bridge question — take the answers as parameters instead.
/// </para>
/// </remarks>
public static class BattleBoard
{
    /// <summary>Width of the battlefield, measured in cells, <c>Board::widthInCells</c>.</summary>
    public const int WidthInCells = 11;

    /// <summary>Height of the battlefield, measured in cells, <c>Board::heightInCells</c>.</summary>
    public const int HeightInCells = 9;

    /// <summary>Total number of cells on the battlefield, <c>Board::sizeInCells</c>.</summary>
    public const int SizeInCells = WidthInCells * HeightInCells;

    /// <summary>
    /// The one moat cell whose water is covered by the drawbridge, and which is therefore passable while the bridge
    /// is down. The reference spells the number <c>49</c> out in <c>isMoatIndex()</c>.
    /// </summary>
    public const int BridgeMoatIndex = 49;

    /// <summary>
    /// <c>Board::isValidIndex()</c>: is the index on the board at all?
    /// </summary>
    /// <param name="index">Cell index.</param>
    /// <returns><see langword="true"/> for 0 to 98.</returns>
    public static bool IsValidIndex(int index) => index >= 0 && index < SizeInCells;

    /// <summary>
    /// <c>Board::isNearIndexes()</c>: are the two indexes neighbours (but not the same cell)?
    /// </summary>
    /// <param name="index1">First cell index.</param>
    /// <param name="index2">Second cell index.</param>
    /// <returns><see langword="true"/> if they are directly adjacent.</returns>
    public static bool IsNearIndexes(int index1, int index2) => index1 != index2 && GetDirection(index1, index2) != CellDirection.Unknown;

    /// <summary>
    /// <c>Board::GetDirection()</c>: which way does <paramref name="index2"/> lie from
    /// <paramref name="index1"/>?
    /// </summary>
    /// <remarks>
    /// The reference walks the six directions in clockwise order and returns the first match, so the answer is the
    /// one whose index happens to agree; walking is how it avoids a delta table. The same cell answers
    /// <see cref="CellDirection.Center"/>, anything else <see cref="CellDirection.Unknown"/>.
    /// </remarks>
    /// <param name="index1">The cell the direction is measured from.</param>
    /// <param name="index2">The cell the direction points at.</param>
    /// <returns>The direction, or <see cref="CellDirection.Unknown"/>.</returns>
    public static CellDirection GetDirection(int index1, int index2)
    {
        if (!IsValidIndex(index1) || !IsValidIndex(index2))
        {
            return CellDirection.Unknown;
        }

        if (index1 == index2)
        {
            return CellDirection.Center;
        }

        foreach (CellDirection direction in BattleCellDirections.All)
        {
            if (IsValidDirection(index1, direction) && index2 == GetIndexDirection(index1, direction))
            {
                return direction;
            }
        }

        return CellDirection.Unknown;
    }

    /// <summary>
    /// <c>Board::GetReflectDirection()</c>: the direction on the other side of the hex.
    /// </summary>
    /// <param name="direction">A single direction.</param>
    /// <returns>The opposite direction; <see cref="CellDirection.Center"/>, the masks and
    /// <see cref="CellDirection.Unknown"/> have none and answer <see cref="CellDirection.Unknown"/>.</returns>
    public static CellDirection GetReflectDirection(CellDirection direction) => direction switch
    {
        CellDirection.TopLeft => CellDirection.BottomRight,
        CellDirection.TopRight => CellDirection.BottomLeft,
        CellDirection.Left => CellDirection.Right,
        CellDirection.Right => CellDirection.Left,
        CellDirection.BottomLeft => CellDirection.TopRight,
        CellDirection.BottomRight => CellDirection.TopLeft,
        _ => CellDirection.Unknown,
    };

    /// <summary>
    /// <c>Board::GetDirectionFromDelta()</c>: the direction of a movement delta.
    /// </summary>
    /// <remarks>
    /// Diagonal beats orthogonal: the reference looks at the sign of both axes first and only falls through to
    /// left/right when the vertical delta is zero.
    /// </remarks>
    /// <param name="moveX">Horizontal delta.</param>
    /// <param name="moveY">Vertical delta.</param>
    /// <returns>The direction; <see cref="CellDirection.Center"/> for a zero delta.</returns>
    public static CellDirection GetDirectionFromDelta(int moveX, int moveY)
    {
        if (moveX == 0 && moveY == 0)
        {
            return CellDirection.Center;
        }

        if (moveY < 0)
        {
            if (moveX < 0)
            {
                return CellDirection.TopLeft;
            }

            if (moveX > 0)
            {
                return CellDirection.TopRight;
            }
        }
        else if (moveY > 0)
        {
            if (moveX < 0)
            {
                return CellDirection.BottomLeft;
            }

            if (moveX > 0)
            {
                return CellDirection.BottomRight;
            }
        }

        if (moveX < 0)
        {
            return CellDirection.Left;
        }

        if (moveX > 0)
        {
            return CellDirection.Right;
        }

        // A zero delta was handled above, so the remaining case is unreachable; the reference asserts here.
        return CellDirection.Unknown;
    }

    /// <summary>
    /// <c>Board::isValidDirection()</c>: does the cell have a neighbour in that direction?
    /// </summary>
    /// <remarks>
    /// The odd rows are shifted half a cell, so the left-hand diagonals fall off the board on even rows at
    /// <c>x == 0</c> and the right-hand ones on odd rows at <c>x == 10</c>. The top and bottom rows lose the
    /// vertical neighbours, and <see cref="CellDirection.Center"/> is always valid by definition.
    /// </remarks>
    /// <param name="index">Cell index.</param>
    /// <param name="direction">A single direction.</param>
    /// <returns><see langword="true"/> if the neighbour exists.</returns>
    public static bool IsValidDirection(int index, CellDirection direction)
    {
        if (!IsValidIndex(index))
        {
            return false;
        }

        if (direction == CellDirection.Center)
        {
            return true;
        }

        int x = index % WidthInCells;
        int y = index / WidthInCells;

        return direction switch
        {
            CellDirection.TopLeft => !(y == 0 || (x == 0 && y % 2 != 0)),
            CellDirection.TopRight => !(y == 0 || (x == WidthInCells - 1 && y % 2 == 0)),
            CellDirection.Left => x != 0,
            CellDirection.Right => x != WidthInCells - 1,
            CellDirection.BottomLeft => !(y == HeightInCells - 1 || (x == 0 && y % 2 != 0)),
            CellDirection.BottomRight => !(y == HeightInCells - 1 || (x == WidthInCells - 1 && y % 2 == 0)),
            _ => false,
        };
    }

    /// <summary>
    /// <c>Board::GetIndexDirection()</c>: the index of the neighbour in that direction.
    /// </summary>
    /// <remarks>
    /// The reference does not check the edges: callers are expected to ask
    /// <see cref="IsValidDirection"/> first. An index that is off the board at all answers -1, and so does a
    /// direction that is not a single one — the reference's <c>default</c> case.
    /// </remarks>
    /// <param name="index">Cell index.</param>
    /// <param name="direction">A single direction.</param>
    /// <returns>The neighbour's index, or -1.</returns>
    public static int GetIndexDirection(int index, CellDirection direction)
    {
        if (!IsValidIndex(index))
        {
            return -1;
        }

        bool oddRow = index / WidthInCells % 2 != 0;

        return direction switch
        {
            CellDirection.Center => index,
            CellDirection.TopLeft => index - (oddRow ? WidthInCells + 1 : WidthInCells),
            CellDirection.TopRight => index - (oddRow ? WidthInCells : WidthInCells - 1),
            CellDirection.Left => index - 1,
            CellDirection.Right => index + 1,
            CellDirection.BottomLeft => index + (oddRow ? WidthInCells - 1 : WidthInCells),
            CellDirection.BottomRight => index + (oddRow ? WidthInCells : WidthInCells + 1),
            _ => -1,
        };
    }

    /// <summary>
    /// <c>Board::GetDistance()</c>: the number of steps between two cells.
    /// </summary>
    /// <remarks>
    /// Computed on the axial coordinates the offset hexes map onto: the vertical difference and the sheared
    /// horizontal difference are either of the same sign, in which case the larger of them is the distance, or of
    /// opposite signs, in which case they add up. Either index off the board answers 0, as the reference does.
    /// </remarks>
    /// <param name="index1">First cell index.</param>
    /// <param name="index2">Second cell index.</param>
    /// <returns>The distance in steps.</returns>
    public static uint GetDistance(int index1, int index2)
    {
        if (!IsValidIndex(index1) || !IsValidIndex(index2))
        {
            return 0;
        }

        int x1 = index1 % WidthInCells;
        int y1 = index1 / WidthInCells;

        int x2 = index2 % WidthInCells;
        int y2 = index2 / WidthInCells;

        int du = y2 - y1;
        int dv = x2 + y2 / 2 - (x1 + y1 / 2);

        if ((du >= 0 && dv >= 0) || (du < 0 && dv < 0))
        {
            return (uint)Math.Max(Math.Abs(du), Math.Abs(dv));
        }

        return (uint)(Math.Abs(du) + Math.Abs(dv));
    }

    /// <summary>
    /// How many steps separate two units, <c>Board::GetDistance( const Position &amp;, const Position &amp; )</c>.
    /// </summary>
    /// <remarks>
    /// 宽单位占两格，此时取四种头尾配对里最小的那个——「离得最近的那半格」才是两个单位之间的距离。
    /// 任一位置为空时参考会解引用空指针，端口改答 0：调用方本来就该先确认位置有效。
    /// </remarks>
    /// <param name="first">One unit's position.</param>
    /// <param name="second">The other unit's position.</param>
    /// <returns>The distance in steps.</returns>
    public static uint GetDistance(BattlePosition first, BattlePosition second)
    {
        if (first.Head == null || second.Head == null)
        {
            return 0;
        }

        uint headToHead = GetDistance(first.HeadIndex, second.HeadIndex);

        if (first.Tail == null)
        {
            return second.Tail == null ? headToHead : Math.Min(headToHead, GetDistance(first.HeadIndex, second.TailIndex));
        }

        uint tailToHead = GetDistance(first.TailIndex, second.HeadIndex);

        if (second.Tail == null)
        {
            return Math.Min(headToHead, tailToHead);
        }

        uint tailToTail = GetDistance(first.TailIndex, second.TailIndex);
        uint headToTail = GetDistance(first.HeadIndex, second.TailIndex);

        return Math.Min(Math.Min(headToHead, headToTail), Math.Min(tailToHead, tailToTail));
    }

    /// <summary>
    /// <c>Board::GetDistanceFromBoardEdgeAlongXAxis()</c>: how many cells is the column from one edge?
    /// </summary>
    /// <remarks>
    /// The count starts at one for the outermost column, so the two edges give 1 and <c>widthInCells</c>.
    /// </remarks>
    /// <param name="index">Cell index.</param>
    /// <param name="fromRightEdge">Measure from the right edge instead of the left.</param>
    /// <returns>1 to 11.</returns>
    public static uint GetDistanceFromBoardEdgeAlongXAxis(int index, bool fromRightEdge)
    {
        if (!IsValidIndex(index))
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "cell index is off the board");
        }

        uint x = (uint)(index % WidthInCells);

        return fromRightEdge ? WidthInCells - x : x + 1;
    }

    /// <summary>
    /// <c>Board::isCastleIndex()</c>: is the cell inside the castle during a siege?
    /// </summary>
    /// <remarks>
    /// The nine rows of ranges are the castle courtyard plus the destructible wall cells, which count as inside.
    /// The reference writes them as a chain of comparisons; the ranges are kept here as they are.
    /// </remarks>
    /// <param name="index">Cell index.</param>
    /// <returns><see langword="true"/> for the courtyard and wall cells.</returns>
    public static bool IsCastleIndex(int index) =>
        (index >= 8 && index <= 10) ||
        (index > 19 && index <= 21) ||
        (index >= 29 && index <= 32) ||
        (index > 40 && index <= 43) ||
        (index > 50 && index <= 54) ||
        (index > 62 && index <= 65) ||
        (index >= 73 && index <= 76) ||
        (index > 85 && index <= 87) ||
        (index >= 96 && index <= 98);

    /// <summary>
    /// <c>Board::isOutOfWallsIndex()</c>: is the cell outside the castle walls during a siege?
    /// </summary>
    /// <remarks>
    /// The complement of <see cref="IsCastleIndex"/> within 0 to 98 except for the moat and the gate row, which is
    /// why the two tables are written out separately in the reference rather than as one range.
    /// </remarks>
    /// <param name="index">Cell index.</param>
    /// <returns><see langword="true"/> for the open field in front of the walls.</returns>
    public static bool IsOutOfWallsIndex(int index) =>
        index <= 8 ||
        (index >= 11 && index <= 19) ||
        (index >= 22 && index <= 29) ||
        (index >= 33 && index <= 40) ||
        (index >= 44 && index <= 50) ||
        (index >= 55 && index <= 62) ||
        (index >= 66 && index <= 73) ||
        (index >= 77 && index <= 85) ||
        (index >= 88 && index <= 96);

    /// <summary>
    /// <c>Board::isMoatIndex()</c>: does the cell hold the castle moat for this unit?
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eight cells are moat cells unconditionally, one per row of the courtyard's front. The ninth, index
    /// <see cref="BridgeMoatIndex"/>, is covered by the drawbridge and only hurts a unit that is either flying or
    /// facing a bridge that is not passable — a raised bridge, or none at all.
    /// </para>
    /// <para>
    /// The reference asks the arena for the bridge and the bridge about the unit; both live in the battle batch, so
    /// the two answers arrive as parameters here.
    /// </para>
    /// </remarks>
    /// <param name="index">Cell index.</param>
    /// <param name="unitIsFlying">Is the unit a flyer? Flyers ignore water.</param>
    /// <param name="bridgeIsPassable">Is there a drawbridge and is it down and passable for the unit?</param>
    /// <returns><see langword="true"/> if the cell is moat water for that unit.</returns>
    public static bool IsMoatIndex(int index, bool unitIsFlying, bool bridgeIsPassable) => index switch
    {
        7 or 18 or 28 or 39 or 61 or 72 or 84 or 95 => true,
        BridgeMoatIndex => unitIsFlying || !bridgeIsPassable,
        _ => false,
    };

    /// <summary>
    /// Whether a unit may strike from a given cell, <c>Board::CanAttackFromCell()</c>
    /// (<c>battle_board.cpp:972-1007</c>).
    /// </summary>
    /// <remarks>
    /// 唯一的拦路虎是护城河：站在河里的地面单位打不到对岸，除非它本来就贴着目标、或者它会飞。参考从全局
    /// <c>Arena::GetCastle()</c> 取城堡，端口把竞技场显式传进来——城堡与桥都在它手上。
    /// </remarks>
    /// <param name="arena">The arena, for the castle and its drawbridge.</param>
    /// <param name="unit">The unit that would strike.</param>
    /// <param name="from">The cell it would strike from.</param>
    /// <returns>Whether the strike is allowed from that cell.</returns>
    public static bool CanAttackFromCell(BattleArena arena, BattleUnit unit, int from)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(unit);

        BattlePosition position = BattlePosition.GetReachable(arena, unit, from);

        if (position.Head == null)
        {
            return false;
        }

        if (arena.Castle is not { } castle || !castle.IsBuild(FHeroes2.Game.Castle.BuildingType.Moat))
        {
            return true;
        }

        bool bridgeIsPassable = arena.Bridge is { } bridge && bridge.IsPassable(unit);

        if (!IsMoatIndex(from, unit.IsFlying, bridgeIsPassable))
        {
            return true;
        }

        if (unit.IsFlying)
        {
            return true;
        }

        return from == unit.HeadIndex || (unit.IsWide && from == unit.TailIndex);
    }

    /// <summary>
    /// Whether a unit could actually reach its target from a given cell,
    /// <c>Board::CanAttackTargetFromPosition()</c> (<c>battle_board.cpp:1009-1036</c>).
    /// </summary>
    /// <remarks>
    /// 参考拿攻击者在这个格子上的真实站位去试：头尾任一格与目标相邻、并且那一格允许出手
    /// （<see cref="CanAttackFromCell"/>），就算打得着。宽单位因此有两个候选格。
    /// </remarks>
    /// <param name="arena">The arena, for the moat rules.</param>
    /// <param name="attacker">The unit that would strike.</param>
    /// <param name="target">The unit it would hit.</param>
    /// <param name="dst">The cell the attacker would move to first.</param>
    /// <returns>Whether the target can be struck from that cell.</returns>
    public static bool CanAttackTargetFromPosition(BattleArena arena, BattleUnit attacker, BattleUnit target, int dst)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        BattlePosition position = BattlePosition.GetReachable(arena, attacker, dst);

        int[] cells = position.Tail == null
            ? [position.HeadIndex]
            : [position.HeadIndex, position.TailIndex];

        foreach (int cell in cells)
        {
            uint distance = GetDistance(target.HeadIndex, cell);

            if (target.Position.Tail != null)
            {
                distance = Math.Min(distance, GetDistance(target.TailIndex, cell));
            }

            if (distance > 1)
            {
                continue;
            }

            if (!CanAttackFromCell(arena, attacker, cell))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// <c>Board::GetMoveWideIndexes()</c>: the up to four cells a unit's tail may occupy next to its head.
    /// </summary>
    /// <remarks>
    /// The order matters — left, right, then the two diagonals on the side the unit faces — because the caller
    /// picks the first usable one. <paramref name="reflect"/> is the unit's facing: <see langword="true"/> puts the
    /// tail to the left of the head.
    /// </remarks>
    /// <param name="head">The head cell index.</param>
    /// <param name="reflect">Is the unit mirrored?</param>
    /// <returns>The candidates, in reference order.</returns>
    public static int[] GetMoveWideIndexes(int head, bool reflect)
    {
        if (!IsValidIndex(head))
        {
            return [];
        }

        var result = new List<int>(4);

        if (IsValidDirection(head, CellDirection.Left))
        {
            result.Add(GetIndexDirection(head, CellDirection.Left));
        }

        if (IsValidDirection(head, CellDirection.Right))
        {
            result.Add(GetIndexDirection(head, CellDirection.Right));
        }

        CellDirection[] diagonals = reflect
            ? [CellDirection.TopLeft, CellDirection.BottomLeft]
            : [CellDirection.TopRight, CellDirection.BottomRight];

        foreach (CellDirection direction in diagonals)
        {
            if (IsValidDirection(head, direction))
            {
                result.Add(GetIndexDirection(head, direction));
            }
        }

        return [.. result];
    }

    /// <summary>
    /// <c>Board::GetAroundIndexes()</c>: the existing cells around a single cell, clockwise from the top left.
    /// </summary>
    /// <param name="center">The cell index.</param>
    /// <returns>Up to six indexes.</returns>
    public static int[] GetAroundIndexes(int center)
    {
        if (!IsValidIndex(center))
        {
            return [];
        }

        var result = new List<int>(6);

        foreach (CellDirection direction in BattleCellDirections.All)
        {
            if (!IsValidDirection(center, direction))
            {
                continue;
            }

            result.Add(GetIndexDirection(center, direction));
        }

        return [.. result];
    }

    /// <summary>
    /// <c>Board::GetAroundIndexes( const Position &amp; )</c>: the existing cells around a wide unit.
    /// </summary>
    /// <remarks>
    /// A wide unit has a ring of eight cells rather than six, and the ring is walked clockwise from the far end of
    /// the body: when the head index is above the tail index the walk starts at the tail, otherwise at the head.
    /// The two branches are mirror images, which is why the reference writes them out twice.
    /// </remarks>
    /// <param name="head">The head cell index.</param>
    /// <param name="tail">The tail cell index.</param>
    /// <returns>Up to eight indexes, clockwise.</returns>
    public static int[] GetAroundIndexes(int head, int tail)
    {
        if (!IsValidIndex(head) || !IsValidIndex(tail))
        {
            return [];
        }

        if (head == tail)
        {
            return GetAroundIndexes(head);
        }

        // The walk always starts at the cell with the lower index and finishes back on it, so both of the
        // reference's branches produce this one sequence.
        int start = head > tail ? tail : head;
        int end = head > tail ? head : tail;

        var result = new List<int>(8);

        Add(start, CellDirection.TopLeft);
        Add(start, CellDirection.TopRight);
        Add(end, CellDirection.TopRight);
        Add(end, CellDirection.Right);
        Add(end, CellDirection.BottomRight);
        Add(start, CellDirection.BottomRight);
        Add(start, CellDirection.BottomLeft);
        Add(start, CellDirection.Left);

        return [.. result];

        void Add(int index, CellDirection direction)
        {
            if (IsValidDirection(index, direction))
            {
                result.Add(GetIndexDirection(index, direction));
            }
        }
    }

    /// <summary>
    /// <c>Board::GetDistanceIndexes()</c>: every cell exactly within a radius of one cell.
    /// </summary>
    /// <remarks>
    /// The walk is done on axial coordinates, the same ones <see cref="GetDistance"/> measures in: for every
    /// axial step <c>(dq, dr)</c> inside the radius the cell is converted back to offset coordinates and dropped if
    /// it would leave the board. The centre is never included.
    /// </remarks>
    /// <param name="center">The cell index.</param>
    /// <param name="radius">The radius in steps.</param>
    /// <returns>The cells, in the reference's axial walk order.</returns>
    public static int[] GetDistanceIndexes(int center, uint radius)
    {
        if (!IsValidIndex(center))
        {
            return [];
        }

        int centerX = center % WidthInCells;
        int centerY = center / WidthInCells;

        // Axial coordinates.
        int centerQ = centerX - (centerY + centerY % 2) / 2;
        int centerR = centerY;

        var result = new List<int>(checked((int)(3 * radius * (radius + 1) + 1)));

        int intRadius = checked((int)radius);

        for (int dq = -intRadius; dq <= intRadius; ++dq)
        {
            for (int dr = Math.Max(-intRadius, -intRadius - dq); dr <= Math.Min(intRadius, intRadius - dq); ++dr)
            {
                // The centre is not at a distance.
                if (dq == 0 && dr == 0)
                {
                    continue;
                }

                int q = centerQ + dq;
                int r = centerR + dr;

                int x = q + (r + r % 2) / 2;
                int y = r;

                if (x < 0 || x >= WidthInCells || y < 0 || y >= HeightInCells)
                {
                    continue;
                }

                result.Add(y * WidthInCells + x);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// <c>Board::GetDistanceIndexes( const Position &amp; )</c>: every cell within a radius of either half of a wide
    /// unit.
    /// </summary>
    /// <remarks>
    /// The two rings are merged through a sorted set, so the answer is in ascending index order and the unit's own
    /// two cells are dropped — they are at distance zero from themselves.
    /// </remarks>
    /// <param name="head">The head cell index.</param>
    /// <param name="tail">The tail cell index, or the head index again for a single-cell unit.</param>
    /// <param name="radius">The radius in steps.</param>
    /// <returns>The cells, ascending.</returns>
    public static int[] GetDistanceIndexes(int head, int tail, uint radius)
    {
        int[] positionIndexes = [head, tail];

        var boardIndexes = new SortedSet<int>();

        foreach (int positionIndex in positionIndexes)
        {
            if (!IsValidIndex(positionIndex))
            {
                continue;
            }

            foreach (int index in GetDistanceIndexes(positionIndex, radius))
            {
                if (Array.IndexOf(positionIndexes, index) >= 0)
                {
                    continue;
                }

                boardIndexes.Add(index);
            }
        }

        return [.. boardIndexes];
    }

    /// <summary>
    /// The combat description of the moat, <c>Battle::Board::GetMoatInfo()</c> (<c>battle_board.cpp:1038-1044</c>).
    /// </summary>
    /// <remarks>
    /// The castle screen appends this to the keep's description, right after the towers' — the moat interrupts movement
    /// through it rather than merely slowing it down, which is why it gets a paragraph of its own.
    /// </remarks>
    /// <returns>The sentence, with the defense reduction substituted.</returns>
    /// <summary>
    /// The cells around a whole position, <c>Board::GetAroundIndexes( const Position &amp; )</c>
    /// (<c>battle_board.cpp:793-869</c>).
    /// </summary>
    /// <remarks>
    /// 宽单位占两格，邻居是两格邻居的并集；顺序按顺时针走，参考里的顺序会影响 AI 选格的确定性，所以照抄。
    /// </remarks>
    /// <param name="position">The position to surround.</param>
    /// <returns>The neighbouring cells, clockwise.</returns>
    public static List<int> GetAroundIndexes(BattlePosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.Head == null)
        {
            return [];
        }

        if (position.Tail == null)
        {
            return [.. GetAroundIndexes(position.HeadIndex)];
        }

        int headIndex = position.HeadIndex;
        int tailIndex = position.TailIndex;

        if (!IsValidIndex(headIndex) || !IsValidIndex(tailIndex))
        {
            return [];
        }

        List<int> result = new(8);

        if (headIndex > tailIndex)
        {
            AddAround(result, tailIndex, CellDirection.TopLeft);
            AddAround(result, tailIndex, CellDirection.TopRight);
            AddAround(result, headIndex, CellDirection.TopRight);
            AddAround(result, headIndex, CellDirection.Right);
            AddAround(result, headIndex, CellDirection.BottomRight);
            AddAround(result, tailIndex, CellDirection.BottomRight);
            AddAround(result, tailIndex, CellDirection.BottomLeft);
            AddAround(result, tailIndex, CellDirection.Left);
        }
        else if (headIndex < tailIndex)
        {
            AddAround(result, headIndex, CellDirection.TopLeft);
            AddAround(result, headIndex, CellDirection.TopRight);
            AddAround(result, tailIndex, CellDirection.TopRight);
            AddAround(result, tailIndex, CellDirection.Right);
            AddAround(result, tailIndex, CellDirection.BottomRight);
            AddAround(result, headIndex, CellDirection.BottomRight);
            AddAround(result, headIndex, CellDirection.BottomLeft);
            AddAround(result, headIndex, CellDirection.Left);
        }

        return result;

        static void AddAround(List<int> cells, int center, CellDirection direction)
        {
            if (IsValidDirection(center, direction))
            {
                cells.Add(GetIndexDirection(center, direction));
            }
        }
    }

    /// <summary>
    /// Every cell exactly <paramref name="radius"/> steps away from a position,
    /// <c>Board::GetDistanceIndexes( const Position &amp;, uint32_t )</c> (<c>battle_board.cpp:916-935</c>).
    /// </summary>
    /// <remarks>两个格子的环带取并集，再去掉单位自己站的格子，顺序按索引升序。</remarks>
    /// <param name="position">The center position.</param>
    /// <param name="radius">How many steps away.</param>
    /// <returns>The cells of that ring.</returns>
    public static List<int> GetDistanceIndexes(BattlePosition position, uint radius)
    {
        ArgumentNullException.ThrowIfNull(position);

        int[] ownIndexes = position.Tail == null ? [position.HeadIndex] : [position.HeadIndex, position.TailIndex];
        SortedSet<int> cells = [];

        foreach (int own in ownIndexes)
        {
            if (!IsValidIndex(own))
            {
                continue;
            }

            foreach (int index in GetDistanceIndexes(own, radius))
            {
                if (Array.IndexOf(ownIndexes, index) >= 0)
                {
                    continue;
                }

                cells.Add(index);
            }
        }

        return [.. cells];
    }

    /// <summary>
    /// The units still on the field, nearest first, <c>Board::GetNearestTroops()</c>
    /// (<c>battle_board.cpp:193-228</c>).
    /// </summary>
    /// <remarks>
    /// 起点单位自己与黑名单里的单位都不算；每个单位只算一次（只看头格）。距离相同保持遍历顺序——
    /// 参考用的就是 <c>std::sort</c> 的非稳定比较，这里用 <see cref="List{T}.Sort"/> 也一样。
    /// </remarks>
    /// <param name="arena">The arena, to walk its cells.</param>
    /// <param name="startUnit">The unit to measure from.</param>
    /// <param name="blackList">The units to skip.</param>
    /// <returns>The units, nearest first.</returns>
    public static List<BattleUnit> GetNearestTroops(BattleArena arena, BattleUnit startUnit, IReadOnlyList<BattleUnit> blackList)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(startUnit);
        ArgumentNullException.ThrowIfNull(blackList);

        List<(BattleUnit Unit, uint Distance)> foundUnits = [];

        for (int index = 0; index < WidthInCells * HeightInCells; ++index)
        {
            BattleUnit? cellUnit = arena.GetTroopBoard(index);

            if (cellUnit == null || ReferenceEquals(cellUnit, startUnit) || index != cellUnit.HeadIndex)
            {
                continue;
            }

            if (blackList.Contains(cellUnit))
            {
                continue;
            }

            foundUnits.Add((cellUnit, GetDistance(startUnit.Position, cellUnit.Position)));
        }

        foundUnits.Sort(static (first, second) => first.Distance.CompareTo(second.Distance));

        return [.. foundUnits.Select(static found => found.Unit)];
    }

    public static string GetMoatInfo() =>
        "The Moat interrupts any movement through it and reduces the defense skill of troops present in it by " +
        GameStatic.BattleMoatReduceDefense.ToString() + ".";
}
