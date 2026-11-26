using FHeroes2.Core.Geometry;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The road builder of the object phase: the cheapest way from a tile to the nearest road of its region,
/// <c>findPathToNearestRoad()</c> (<c>map_random_generator_helper.cpp:420-537</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的择位循环（<c>planObjectPlacement</c> / <c>placeObjectSet</c>）有一条硬规矩：一件东西放下去之后，**必须还能修出一条
/// 到最近的路**，修不出来就整件撤掉。所以这个搜索是「能不能放」的一部分，不是什么收尾美化。
/// </para>
/// <para>
/// 搜索本身是带代价的 BFS（不是严格排序的 Dijkstra，节点按入队顺序处理）：直走
/// <see cref="MapGrounds.DefaultGroundPenalty"/>，斜走一点五倍。找到第一条「从直方向到达」的路之后并不立刻停，而是继续
/// 推进到代价超过它 <see cref="Margin"/> 为止——参考的注释写着这是为了「找到可能更优的路线」。
/// </para>
/// <para>
/// 三条视觉上的经验规则也照抄：从可交互物件出发的路第一步不许斜走、也不许朝上（矿的入口朝下）；斜走的相邻两步不许
/// 拼成急转弯；从 <c>Bottom</c> 过来之后不许横向折返。
/// </para>
/// </remarks>
public static class RandomMapRoadBuilder
{
    /// <summary>
    /// How far past the best road the search keeps looking, <c>roadBuilderMargin</c> — three plain tiles.
    /// </summary>
    public static uint Margin => MapGrounds.DefaultGroundPenalty * 3;

    /// <summary>The cost of a diagonal step, <c>defaultGroundPenalty * 3 / 2</c>.</summary>
    public static uint DiagonalPenalty => MapGrounds.DefaultGroundPenalty * 3 / 2;

    /// <summary>
    /// The route from <paramref name="start"/> to the nearest road of its region, <c>findPathToNearestRoad()</c>.
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="regionId">The region the road has to stay in.</param>
    /// <param name="start">The tile the road would start from.</param>
    /// <returns>
    /// The tiles of the route, the road end first and <paramref name="start"/> last; empty when no road is reachable.
    /// </returns>
    /// <remarks>
    /// 返回的那一格「路」也会出现在结果里，它本来就是 <c>PATH</c>；调用方把整串都标成 <c>PATH</c> 只是幂等操作。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is not a tile of the grid.</exception>
    public static List<int> FindPathToNearestRoad(RandomMapState state, uint regionId, int start)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (start < 0 || start >= state.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "The tile is not part of the grid.");
        }

        int[] from = new int[state.Count];
        uint[] cost = new uint[state.Count];
        Direction[] directions = new Direction[state.Count];

        Array.Fill(from, -1);

        List<int> result = [];
        List<int> nodesToExplore = [start];

        from[start] = start;

        bool fromActionTile = state.GetNode(start).Type == RandomMapNodeType.Action;

        int bestRoadIndex = -1;
        uint bestRoadCost = uint.MaxValue;

        for (int processed = 0; processed < nodesToExplore.Count; ++processed)
        {
            int currentIndex = nodesToExplore[processed];

            // 找到第一条路之后不立刻收工：再往后看一个「幅度」，也许有更划算的走法。
            if (bestRoadIndex != -1 && cost[currentIndex] > bestRoadCost + Margin)
            {
                break;
            }

            bool comeFromStraight = !Directions.IsDiagonal(directions[currentIndex]);

            if (comeFromStraight
                && currentIndex != start
                && cost[currentIndex] < bestRoadCost
                && state.GetNode(currentIndex).Type == RandomMapNodeType.Path)
            {
                bestRoadIndex = currentIndex;
                bestRoadCost = cost[currentIndex];
            }

            Explore(state, regionId, start, currentIndex, fromActionTile, from, cost, directions, nodesToExplore);
        }

        return WalkBack(state, from, directions, bestRoadIndex);
    }

    /// <summary>Expands one tile of the search frontier.</summary>
    /// <param name="state">The grid.</param>
    /// <param name="regionId">The region the road has to stay in.</param>
    /// <param name="start">The tile the road starts from.</param>
    /// <param name="currentIndex">The tile being expanded.</param>
    /// <param name="fromActionTile">Whether the road starts on a tile reserved for an object.</param>
    /// <param name="from">For every tile, the tile it was reached from.</param>
    /// <param name="cost">For every tile, the cost to reach it.</param>
    /// <param name="directions">For every tile, the direction it was reached from.</param>
    /// <param name="nodesToExplore">The frontier, in the order the tiles were queued.</param>
    private static void Explore(
        RandomMapState state,
        uint regionId,
        int start,
        int currentIndex,
        bool fromActionTile,
        int[] from,
        uint[] cost,
        Direction[] directions,
        List<int> nodesToExplore)
    {
        foreach (Direction direction in Directions.AllNeighboringDirections)
        {
            if (!TryGetNeighbour(state, currentIndex, direction, out int nextIndex))
            {
                continue;
            }

            bool isDiagonal = Directions.IsDiagonal(direction);

            // 可交互物件的入口朝下：从它出发的第一步不许斜走、也不许朝上。
            if (fromActionTile && currentIndex == start && (isDiagonal || direction == Direction.Top))
            {
                continue;
            }

            // 斜走的两步不许拼成急转弯。
            if (isDiagonal && from[currentIndex] != -1)
            {
                Direction previousDirection = directions[from[currentIndex]];

                if (previousDirection != direction && Directions.IsDiagonal(previousDirection))
                {
                    continue;
                }

                if (previousDirection == Direction.Bottom
                    && directions[currentIndex] is Direction.Left or Direction.Right)
                {
                    continue;
                }
            }

            if (nextIndex == start)
            {
                continue;
            }

            RandomMapNode other = state.GetNode(nextIndex);

            if (other.Region != regionId)
            {
                continue;
            }

            if (other.Type is not (RandomMapNodeType.Open
                or RandomMapNodeType.Path
                or RandomMapNodeType.Connector))
            {
                continue;
            }

            uint movementCost = cost[currentIndex] + (isDiagonal ? DiagonalPenalty : MapGrounds.DefaultGroundPenalty);

            if (from[nextIndex] == -1 || cost[nextIndex] > movementCost)
            {
                from[nextIndex] = currentIndex;
                cost[nextIndex] = movementCost;
                directions[nextIndex] = direction;

                nodesToExplore.Add(nextIndex);
            }
        }
    }

    /// <summary>Turns the search tree into the route, road end first and start last.</summary>
    /// <param name="state">The grid.</param>
    /// <param name="from">For every tile, the tile it was reached from.</param>
    /// <param name="directions">For every tile, the direction it was reached from.</param>
    /// <param name="bestRoadIndex">The road tile the search settled on, <c>-1</c> when none was reachable.</param>
    /// <returns>The route, empty when there is no road.</returns>
    private static List<int> WalkBack(RandomMapState state, int[] from, Direction[] directions, int bestRoadIndex)
    {
        List<int> result = [];

        if (bestRoadIndex == -1)
        {
            return result;
        }

        int currentStep = bestRoadIndex;

        while (true)
        {
            // 斜着接上路面时少一张过渡图，补一格横向的过渡路（参考的注释写的是「补偿缺失的精灵」）。
            if (result.Count == 1)
            {
                Direction step = directions[currentStep];

                if (step == Direction.BottomRight && TryGetNeighbour(state, currentStep, Direction.Left, out int filler))
                {
                    result.Add(filler);
                }
                else if (step == Direction.BottomLeft && TryGetNeighbour(state, currentStep, Direction.Right, out filler))
                {
                    result.Add(filler);
                }
            }

            result.Add(currentStep);

            if (currentStep == from[currentStep])
            {
                break;
            }

            currentStep = from[currentStep];
        }

        return result;
    }

    /// <summary>The neighbour of a tile in a direction, if it is inside the map.</summary>
    /// <param name="state">The grid.</param>
    /// <param name="index">The tile.</param>
    /// <param name="direction">One of the eight directions.</param>
    /// <param name="neighbour">The neighbouring tile.</param>
    /// <returns><see langword="true"/> when the neighbour is inside the map.</returns>
    private static bool TryGetNeighbour(RandomMapState state, int index, Direction direction, out int neighbour)
    {
        (int dx, int dy) = direction switch
        {
            Direction.TopLeft => (-1, -1),
            Direction.Top => (0, -1),
            Direction.TopRight => (1, -1),
            Direction.Right => (1, 0),
            Direction.BottomRight => (1, 1),
            Direction.Bottom => (0, 1),
            Direction.BottomLeft => (-1, 1),
            Direction.Left => (-1, 0),
            _ => (0, 0),
        };

        int x = (index % state.Width) + dx;
        int y = (index / state.Width) + dy;

        if (x < 0 || y < 0 || x >= state.Width || y >= state.Height)
        {
            neighbour = -1;

            return false;
        }

        neighbour = state.IndexAt(new Point(x, y));

        return true;
    }

}
