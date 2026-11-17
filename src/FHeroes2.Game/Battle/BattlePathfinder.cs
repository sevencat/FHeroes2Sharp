using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Battle;

/// <summary>
/// A key of the walk graph: the head cell and, for a wide unit, the tail cell,
/// <c>Battle::BattleNodeIndex</c> (<c>battle_pathfinding.h:37</c>).
/// </summary>
/// <remarks>
/// 参考是 <c>std::pair&lt;int32_t, int32_t&gt;</c>，配一个把两个值拼成 64 位的哈希器。窄单位的尾巴一律写
/// <c>-1</c>，于是「窄单位站在 30」与「宽单位头 30 尾 ?」永远不会撞键。
/// </remarks>
public readonly struct BattleNodeIndex : IEquatable<BattleNodeIndex>
{
    /// <summary>The value both halves carry when the node does not exist.</summary>
    public const int Invalid = -1;

    /// <summary>Builds the key.</summary>
    /// <param name="head">The head cell index, or <see cref="Invalid"/>.</param>
    /// <param name="tail">The tail cell index, or <see cref="Invalid"/>.</param>
    public BattleNodeIndex(int head, int tail)
    {
        Head = head;
        Tail = tail;
    }

    /// <summary>The head cell index, the reference's <c>index.first</c>.</summary>
    public int Head { get; }

    /// <summary>The tail cell index, the reference's <c>index.second</c>; <c>-1</c> for a one-hex unit.</summary>
    public int Tail { get; }

    /// <summary>The key of a node that does not exist, the reference's <c>{ -1, -1 }</c>.</summary>
    public static BattleNodeIndex None => new(Invalid, Invalid);

    /// <summary>Whether the key names a node at all, i.e. whether the head is set.</summary>
    public bool IsValid => Head != Invalid;

    /// <summary>Value equality, the reference's <c>==</c> on the pair.</summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when both halves match.</returns>
    public bool Equals(BattleNodeIndex other) => Head == other.Head && Tail == other.Tail;

    /// <summary>Value equality.</summary>
    /// <param name="obj">The object to compare to.</param>
    /// <returns><see langword="true"/> for another key with the same halves.</returns>
    public override bool Equals(object? obj) => obj is BattleNodeIndex other && Equals(other);

    /// <summary>
    /// The hash of the pair, in the spirit of the reference's <c>( f &lt;&lt; 32 ) + s</c>.
    /// </summary>
    /// <returns>The hash code.</returns>
    /// <remarks>
    /// 参考专门写了一个哈希器，因为标准库不替 <c>std::pair</c> 生成 <c>std::hash</c>。C# 里
    /// <see cref="HashCode.Combine{T1, T2}"/> 做的正是这件事，两个半区都参与、且与顺序有关。
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(Head, Tail);

    /// <summary>Equality operator.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns><see langword="true"/> when both halves match.</returns>
    public static bool operator ==(BattleNodeIndex left, BattleNodeIndex right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(BattleNodeIndex left, BattleNodeIndex right) => !left.Equals(right);

    /// <summary>Writes the pair for diagnostics, <c>"head,tail"</c>.</summary>
    /// <returns>The textual form.</returns>
    public override string ToString() => $"{Head},{Tail}";
}

/// <summary>
/// One node of the walk graph, <c>Battle::BattleNode</c> (<c>battle_pathfinding.h:52-71</c>).
/// </summary>
/// <remarks>
/// 两个数不是一回事：<see cref="Cost"/> 是走过去要花多少移动力（护城河与飞行都会让它与步数脱钩），
/// <see cref="Distance"/> 是走了几步。宽单位原地转身不算一步，因此两者可以互不相同。
/// </remarks>
public struct BattlePathNode
{
    /// <summary>The node that has not been reached yet, the reference's <c>{ -1, -1 }</c> parent.</summary>
    /// <remarks>
    /// C# 的 <c>default</c> 是全零，而全零的父节点是 <c>( 0, 0 )</c> —— 一个合法的格子。所以「未到达」这个状态必须
    /// 显式构造，不能靠 <c>default</c>。取节点时一律用 <c>out</c> 参数判存在，再按 <see cref="IsReached"/> 判断。
    /// </remarks>
    public static BattlePathNode None => new() { From = BattleNodeIndex.None };

    /// <summary>Where we came from, the reference's <c>_from</c>; <see cref="BattleNodeIndex.None"/> until reached.</summary>
    public BattleNodeIndex From { get; private set; }

    /// <summary>The cost of getting here, the reference's <c>_cost</c>.</summary>
    public uint Cost { get; private set; }

    /// <summary>The number of steps taken, the reference's <c>_distance</c>.</summary>
    public uint Distance { get; private set; }

    /// <summary>Whether the node has been reached at all, the reference's <c>_from != { -1, -1 }</c>.</summary>
    public bool IsReached => From.IsValid;

    /// <summary>Rewrites the three fields, <c>BattleNode::update()</c>.</summary>
    /// <param name="from">Where we came from.</param>
    /// <param name="cost">The cost of getting here.</param>
    /// <param name="distance">The number of steps taken.</param>
    public void Update(BattleNodeIndex from, uint cost, uint distance)
    {
        From = from;
        Cost = cost;
        Distance = distance;
    }
}

/// <summary>
/// The walk graph of one unit on the battlefield, <c>Battle::BattlePathfinder</c>
/// (<c>battle_pathfinding.h:73-123</c>, <c>battle_pathfinding.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把它挂在竞技场上，用 <c>Arena::GetBoard()</c> 与 <c>Arena::GetCastle()</c> 拿棋盘与护城河。端口改成把这两样
/// 从构造参数递进来：棋盘是 <see cref="BattleBoardCells"/>，「有没有护城河」与「桥对这个单位通不通」分别是一个布尔
/// 与一个委托——<c>Bridge::isPassable( unit )</c> 要问单位，所以只能晚绑定。
/// </para>
/// <para>
/// 缓存只在「算它的人」变了时才重建：起点、速度、宽窄、飞行、颜色以及整张棋盘的通行性，任一项变了就重算。原因写在
/// 参考的注释里——棋盘可以在单位自己的回合里被别人改动（例如英雄施法），不重算就会走一条已经不存在的路。
/// </para>
/// <para>
/// 非飞行单位走的是带重入的广度优先：队列按发现顺序取节点，只有发现更便宜的走法才把节点重新排队。这是参考的做法，
/// 不是严格的 Dijkstra；因为护城河惩罚是常数、每步代价非负，结果与 Dijkstra 相同。
/// </para>
/// </remarks>
public sealed class BattlePathfinder
{
    /// <summary>
    /// The cost of stepping into the moat, the reference's <c>MOAT_PENALTY</c> (<c>UINT16_MAX</c>).
    /// </summary>
    /// <remarks>它只是「大到任何速度都走不进去」，不是无穷；参考就用这个值让护城河格在当回合不可达。</remarks>
    public const uint MoatPenalty = ushort.MaxValue;

    private readonly BattleBoardCells _board;
    private readonly bool _isMoatBuilt;
    private readonly Func<BattleUnit, bool> _bridgeIsPassable;

    private readonly Dictionary<BattleNodeIndex, BattlePathNode> _cache = [];
    private readonly bool[] _boardStatus = new bool[BattleBoard.SizeInCells];

    private BattleNodeIndex _pathStart = BattleNodeIndex.None;
    private uint _speed;
    private bool _isWide;
    private bool _isFlying;
    private PlayerColor _color = PlayerColor.None;

    /// <summary>Whether <see cref="_cache"/> has been built at least once.</summary>
    /// <remarks>
    /// 参考没有这个标志：它靠 <c>_pathStart</c> 的 <c>{ -1, -1 }</c> 哨兵保证首次一定重算。若单位恰好不在棋盘上
    /// （头格 -1）、速度 0、窄身、不飞、无颜色，且棋盘全不可通行，那一串参数会和初值完全一致，缓存于是保持为空。
    /// 端口多记一个布尔，把那串巧合挡掉。
    /// </remarks>
    private bool _hasCache;

    /// <summary>Builds a pathfinder over one board.</summary>
    /// <param name="board">The cells of the battlefield.</param>
    /// <param name="isMoatBuilt">Whether the castle has a moat, <c>castle->isBuild( BUILD_MOAT )</c>.</param>
    /// <param name="bridgeIsPassable">
    /// Whether the drawbridge lets a unit through, <c>Bridge::isPassable( unit )</c>. There being no bridge at all is
    /// the same answer as a raised one.
    /// </param>
    public BattlePathfinder(BattleBoardCells board, bool isMoatBuilt, Func<BattleUnit, bool> bridgeIsPassable)
    {
        _board = board;
        _isMoatBuilt = isMoatBuilt;
        _bridgeIsPassable = bridgeIsPassable;
    }

    /// <summary>
    /// Whether the given position is reachable for the given unit, either on the current turn or in principle,
    /// <c>BattlePathfinder::isPositionReachable()</c> (<c>battle_pathfinding.cpp:220-239</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    /// <param name="position">The destination.</param>
    /// <param name="isOnCurrentTurn">Whether the movement has to fit into this turn's speed.</param>
    /// <returns><see langword="true"/> when the path exists.</returns>
    /// <remarks>
    /// 一个不该有人的位置（空位置）是合法的输入，答案永远是「到不了」——参考在开头就返回 <c>false</c>，
    /// 不做任何断言。
    /// </remarks>
    public bool IsPositionReachable(BattleUnit unit, BattlePosition position, bool isOnCurrentTurn)
    {
        if (position.Head == null)
        {
            return false;
        }

        ReEvaluateIfNeeded(unit);

        BattleNodeIndex nodeIndex = new(position.HeadIndex, position.TailIndex);

        if (!_cache.TryGetValue(nodeIndex, out BattlePathNode node))
        {
            return false;
        }

        return (nodeIndex == _pathStart || node.IsReached) && (!isOnCurrentTurn || node.Cost <= _speed);
    }

    /// <summary>
    /// The cost of moving to the given position, <c>BattlePathfinder::getCost()</c>
    /// (<c>battle_pathfinding.cpp:241-257</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    /// <param name="position">A position the caller has already found reachable.</param>
    /// <returns>The cost in movement points; the moat costs <see cref="MoatPenalty"/>.</returns>
    /// <remarks>出发格自己也有一项，代价是 0——它就在缓存里，父节点为空。</remarks>
    public uint GetCost(BattleUnit unit, BattlePosition position)
    {
        Debug.Assert(position.Head != null);

        ReEvaluateIfNeeded(unit);

        BattleNodeIndex nodeIndex = new(position.HeadIndex, position.TailIndex);

        Debug.Assert(_cache.ContainsKey(nodeIndex));

        return _cache[nodeIndex].Cost;
    }

    /// <summary>
    /// The number of steps to the given position, <c>BattlePathfinder::getDistance()</c>
    /// (<c>battle_pathfinding.cpp:259-275</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    /// <param name="position">A position the caller has already found reachable.</param>
    /// <returns>The step count. For flying units it is the straight-line distance.</returns>
    public uint GetDistance(BattleUnit unit, BattlePosition position)
    {
        Debug.Assert(position.Head != null);

        ReEvaluateIfNeeded(unit);

        BattleNodeIndex nodeIndex = new(position.HeadIndex, position.TailIndex);

        Debug.Assert(_cache.ContainsKey(nodeIndex));

        return _cache[nodeIndex].Distance;
    }

    /// <summary>
    /// The path to the given position as far as this turn can carry the unit,
    /// <c>BattlePathfinder::buildPath()</c> (<c>battle_pathfinding.cpp:301-358</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    /// <param name="position">The destination; it may be out of reach this turn.</param>
    /// <returns>The head cell indexes in walking order, destination last; empty when the position is unreachable.</returns>
    /// <remarks>
    /// 目的地在原则上可达、但当回合走不到时，路径止于最后一个走得到的格子。宽单位还有个收尾：最后那半格可能是反的，
    /// 于是补一个尾巴格让单位转过来——那一步不算移动，只是转身。
    /// </remarks>
    public int[] BuildPath(BattleUnit unit, BattlePosition position)
    {
        Debug.Assert(position.Head != null);

        ReEvaluateIfNeeded(unit);

        // 参考按 Speed::INSTANT 预留，那是移动力下限之外的一条经验长度。
        var result = new List<int>((int)Speed.Instant);

        BattleNodeIndex lastReachableNodeIndex = BattleNodeIndex.None;
        BattleNodeIndex nodeIndex = new(position.HeadIndex, position.TailIndex);

        while (_cache.TryGetValue(nodeIndex, out BattlePathNode node))
        {
            if (nodeIndex == _pathStart)
            {
                break;
            }

            Debug.Assert(node.IsReached);

            BattleNodeIndex currentIndex = nodeIndex;
            nodeIndex = node.From;

            // 原则上走得到、当回合走不到：跳过这些步子。
            if (node.Cost > _speed)
            {
                continue;
            }

            if (_isWide && lastReachableNodeIndex == BattleNodeIndex.None)
            {
                Debug.Assert(currentIndex.Head != BattleNodeIndex.Invalid && currentIndex.Tail != BattleNodeIndex.Invalid);

                lastReachableNodeIndex = currentIndex;
            }

            result.Add(currentIndex.Head);
        }

        result.Reverse();

        // 当回合走不到时，宽单位最后走得到的那半格可能和目标是反向的，需要补一步转身。
        if (_isWide && result.Count > 0)
        {
            Debug.Assert(lastReachableNodeIndex.Head != BattleNodeIndex.Invalid && lastReachableNodeIndex.Tail != BattleNodeIndex.Invalid);

            bool isReflect = lastReachableNodeIndex.Head < lastReachableNodeIndex.Tail;

            if (isReflect != position.IsReflect)
            {
                Debug.Assert(!position.Contains(lastReachableNodeIndex.Head) || !position.Contains(lastReachableNodeIndex.Tail));

                result.Add(lastReachableNodeIndex.Tail);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Every cell the unit's head can reach this turn, <c>BattlePathfinder::getAllAvailableMoves()</c>
    /// (<c>battle_pathfinding.cpp:277-299</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    /// <returns>The head cell indexes, ascending and without duplicates.</returns>
    /// <remarks>
    /// 宽单位有若干位置共用同一个头格（尾巴可以左右摆），参考用有序集合按头格去重，于是答案与遍历顺序无关。
    /// 出发格自身不算「可移动到的格子」。
    /// </remarks>
    public int[] GetAllAvailableMoves(BattleUnit unit)
    {
        ReEvaluateIfNeeded(unit);

        var boardIndexes = new SortedSet<int>();

        foreach (KeyValuePair<BattleNodeIndex, BattlePathNode> pair in _cache)
        {
            if (pair.Key == _pathStart || !pair.Value.IsReached || pair.Value.Cost > _speed)
            {
                continue;
            }

            Debug.Assert(pair.Key.Head != BattleNodeIndex.Invalid);

            boardIndexes.Add(pair.Key.Head);
        }

        return [.. boardIndexes];
    }

    /// <summary>
    /// The furthest position this turn that lies on the way to the given one,
    /// <c>BattlePathfinder::getClosestReachablePosition()</c> (<c>battle_pathfinding.cpp:360-415</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    /// <param name="position">The destination; it may be out of reach.</param>
    /// <returns>The furthest reachable position other than the unit's own, or an empty position.</returns>
    public BattlePosition GetClosestReachablePosition(BattleUnit unit, BattlePosition position)
    {
        Debug.Assert(position.Head != null);

        ReEvaluateIfNeeded(unit);

        BattleNodeIndex nodeIndex = new(position.HeadIndex, position.TailIndex);

        while (_cache.TryGetValue(nodeIndex, out BattlePathNode node))
        {
            if (nodeIndex == _pathStart)
            {
                break;
            }

            Debug.Assert(node.IsReached);

            BattleNodeIndex currentIndex = nodeIndex;
            nodeIndex = node.From;

            if (node.Cost > _speed)
            {
                continue;
            }

            BattlePosition result = new(_board);

            if (_isWide)
            {
                Debug.Assert(currentIndex.Head != BattleNodeIndex.Invalid && currentIndex.Tail != BattleNodeIndex.Invalid);

                bool isReflect = currentIndex.Head < currentIndex.Tail;

                result.Set(currentIndex.Head, true, isReflect);

                Debug.Assert(
                    result.Head != null && result.Head.Index == currentIndex.Head &&
                    result.Tail != null && result.Tail.Index == currentIndex.Tail);

                // 当回合走不到时，最后走得到的那半格可能与目标反向，转过来即可。
                if (isReflect != position.IsReflect)
                {
                    Debug.Assert(!position.Contains(currentIndex.Head) || !position.Contains(currentIndex.Tail));

                    result.Swap();
                }
            }
            else
            {
                result.Set(currentIndex.Head, false, false);
            }

            return result;
        }

        return new BattlePosition(_board);
    }

    /// <summary>
    /// Rebuilds the graph when the unit, its speed or the board changed,
    /// <c>BattlePathfinder::reEvaluateIfNeeded()</c> (<c>battle_pathfinding.cpp:45-218</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    private void ReEvaluateIfNeeded(BattleUnit unit)
    {
        Debug.Assert(unit.HeadIndex != BattleNodeIndex.Invalid && (unit.IsWide ? unit.TailIndex != BattleNodeIndex.Invalid : unit.TailIndex == BattleNodeIndex.Invalid));

        // 棋盘的通行性可能在单位自己的回合里因外力改变，所以每次都要重新取一遍。
        var boardStatus = new bool[BattleBoard.SizeInCells];

        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            Debug.Assert(BattleBoard.IsValidIndex(index));

            boardStatus[index] = _board.GetCell(index)!.IsPassable(true);
        }

        BattleNodeIndex pathStart = new(unit.HeadIndex, unit.TailIndex);
        uint speed = unit.GetSpeed();
        bool isWide = unit.IsWide;
        bool isFlying = unit.IsFlying;
        PlayerColor color = unit.Color;

        if (_hasCache &&
            _pathStart == pathStart &&
            _speed == speed &&
            _isWide == isWide &&
            _isFlying == isFlying &&
            _color == color &&
            _boardStatus.AsSpan().SequenceEqual(boardStatus))
        {
            return;
        }

        _pathStart = pathStart;
        _speed = speed;
        _isWide = isWide;
        _isFlying = isFlying;
        _color = color;

        boardStatus.CopyTo(_boardStatus, 0);

        _cache.Clear();
        _cache[_pathStart] = BattlePathNode.None;
        _hasCache = true;

        // 飞行单位能落在任何装得下它的地方。
        if (_isFlying)
        {
            for (int cellIndex = 0; cellIndex < BattleBoard.SizeInCells; ++cellIndex)
            {
                BattlePosition position = BattlePosition.GetPosition(unit, cellIndex);

                if (position.Head == null)
                {
                    continue;
                }

                Debug.Assert(position.IsValidForUnit(unit));

                BattleNodeIndex nodeIndex = new(position.HeadIndex, position.TailIndex);

                if (_cache.ContainsKey(nodeIndex))
                {
                    continue;
                }

                // 宽单位的两个位置可以重叠，实际距离为零；既然动了一步，就按一步算。
                uint distance = Math.Max(BattleBoard.GetDistance(unit.Position, position), 1U);

                // 结构体从字典里取出来是副本，必须先改局部变量再写回。
                BattlePathNode newNode = BattlePathNode.None;
                newNode.Update(_pathStart, 1, distance);
                _cache[nodeIndex] = newNode;
            }

            return;
        }

        // 桥对单位通不通在这一回合内不会变，一次问好过每格问一次。
        bool bridgeIsPassable = _bridgeIsPassable(unit);

        bool IsMoatCell(int cellIndex) => _isMoatBuilt && BattleBoard.IsMoatIndex(cellIndex, unit.IsFlying, bridgeIsPassable);

        // 出发位置里落在护城河上的那一格（没有就是 -1）。
        int pathStartMoatCellIndex = -1;

        if (_isMoatBuilt)
        {
            Debug.Assert(_pathStart.Head != BattleNodeIndex.Invalid);

            if (IsMoatCell(_pathStart.Head))
            {
                pathStartMoatCellIndex = _pathStart.Head;
            }
            else if (_isWide)
            {
                Debug.Assert(_pathStart.Tail != BattleNodeIndex.Invalid);

                if (IsMoatCell(_pathStart.Tail))
                {
                    pathStartMoatCellIndex = _pathStart.Tail;
                }
            }
        }

        var nodesToExplore = new List<BattleNodeIndex>(BattleBoard.SizeInCells * 2) { _pathStart };

        for (int nodesToExploreIndex = 0; nodesToExploreIndex < nodesToExplore.Count; ++nodesToExploreIndex)
        {
            BattleNodeIndex currentNodeIndex = nodesToExplore[nodesToExploreIndex];
            BattlePathNode currentNode = _cache[currentNodeIndex];

            if (_isWide)
            {
                Debug.Assert(currentNodeIndex.Head != BattleNodeIndex.Invalid && currentNodeIndex.Tail != BattleNodeIndex.Invalid);

                int currentHeadCellIndex = currentNodeIndex.Head;
                int currentTailCellIndex = currentNodeIndex.Tail;
                BattleNodeIndex flippedCurrentNodeIndex = new(currentTailCellIndex, currentHeadCellIndex);

                BattleCell currentHeadCell = _board.GetCell(currentHeadCellIndex)!;

                bool isCurrentLeftDirection = BattleCellDirections.IsLeftSide(BattleBoard.GetDirection(currentTailCellIndex, currentHeadCellIndex));

                // 宽单位若本来就踩在护城河格上，那么只要这一步还压着同一格，就不必再挨一次护城河惩罚。
                bool isIgnoreMoat = currentHeadCellIndex == pathStartMoatCellIndex || currentTailCellIndex == pathStartMoatCellIndex;
                bool isInMoat = IsMoatCell(currentHeadCellIndex) || IsMoatCell(currentTailCellIndex);
                uint movementPenalty = !isIgnoreMoat && isInMoat ? MoatPenalty : 1;

                foreach (int headCellIndex in BattleBoard.GetMoveWideIndexes(currentHeadCellIndex, isCurrentLeftDirection))
                {
                    BattleCell cell = _board.GetCell(headCellIndex)!;

                    if (!cell.IsPassableFromAdjacent(unit, currentHeadCell))
                    {
                        continue;
                    }

                    int tailCellIndex = BattleCellDirections.IsLeftSide(BattleBoard.GetDirection(currentHeadCellIndex, headCellIndex))
                        ? headCellIndex + 1
                        : headCellIndex - 1;

                    BattleNodeIndex newNodeIndex = new(headCellIndex, tailCellIndex);

                    if (newNodeIndex == _pathStart)
                    {
                        continue;
                    }

                    // 原地转身不算一步。
                    bool isFlipped = newNodeIndex == flippedCurrentNodeIndex;

                    uint cost = currentNode.Cost + (isFlipped ? 0 : movementPenalty);
                    uint distance = currentNode.Distance + (isFlipped ? 0u : 1u);

                    BattlePathNode newNode = _cache.TryGetValue(newNodeIndex, out BattlePathNode existing) ? existing : BattlePathNode.None;

                    if (!newNode.IsReached || newNode.Cost > cost)
                    {
                        newNode.Update(currentNodeIndex, cost, distance);
                        _cache[newNodeIndex] = newNode;

                        nodesToExplore.Add(newNodeIndex);
                    }
                }
            }
            else
            {
                Debug.Assert(currentNodeIndex.Head != BattleNodeIndex.Invalid && currentNodeIndex.Tail == BattleNodeIndex.Invalid);

                int currentCellIndex = currentNodeIndex.Head;

                BattleCell currentCell = _board.GetCell(currentCellIndex)!;

                // 从护城河里迈出第一步不再挨惩罚。
                bool isIgnoreMoat = currentCellIndex == pathStartMoatCellIndex;
                bool isInMoat = IsMoatCell(currentCellIndex);
                uint movementPenalty = !isIgnoreMoat && isInMoat ? MoatPenalty : 1;

                foreach (int cellIndex in BattleBoard.GetAroundIndexes(currentCellIndex))
                {
                    BattleCell cell = _board.GetCell(cellIndex)!;

                    if (!cell.IsPassableFromAdjacent(unit, currentCell))
                    {
                        continue;
                    }

                    BattleNodeIndex newNodeIndex = new(cellIndex, BattleNodeIndex.Invalid);

                    if (newNodeIndex == _pathStart)
                    {
                        continue;
                    }

                    uint cost = currentNode.Cost + movementPenalty;
                    uint distance = currentNode.Distance + 1;

                    BattlePathNode newNode = _cache.TryGetValue(newNodeIndex, out BattlePathNode existing) ? existing : BattlePathNode.None;

                    if (!newNode.IsReached || newNode.Cost > cost)
                    {
                        newNode.Update(currentNodeIndex, cost, distance);
                        _cache[newNodeIndex] = newNode;

                        nodesToExplore.Add(newNodeIndex);
                    }
                }
            }
        }
    }
}
