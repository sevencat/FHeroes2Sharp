using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// What a region is for, <c>Maps::Random_Generator::RegionType</c> (<c>map_random_generator_info.h:226-231</c>).
/// </summary>
public enum RandomMapRegionType : byte
{
    /// <summary>A region a player starts in, <c>STARTING</c>.</summary>
    Starting,

    /// <summary>A region a player expands into, <c>EXPANSION</c>.</summary>
    Expansion,

    /// <summary>A region nobody owns, <c>NEUTRAL</c>.</summary>
    Neutral,
}

/// <summary>
/// One region of the generated map, <c>Maps::Random_Generator::Region</c>
/// (<c>map_random_generator_info.h:233-270</c>, <c>map_random_generator_info.cpp:108-236</c>).
/// </summary>
/// <remarks>
/// <para>
/// 区域靠「从一个中心往外吃格子」长出来：每一步看上一格（<see cref="LastProcessedNode"/>）的八个邻居，落在以中心为圆心、
/// 半径由 <see cref="SizeLimit"/> 推出来的圆外的格子把上一格标成边界，空闲格子吞进来，别人的格子则记成邻居。
/// </para>
/// <para>
/// <b>区域 id 就是它在 <c>mapRegions</c> 列表里的下标</b>：参考在 <see cref="CheckNodeForConnections"/> 里直接
/// <c>mapRegions[node.region]</c>。端口照抄这个约定，调用方必须让 id 与下标一致。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：<see cref="AdjustRegionToFitCastle"/> 在参考里拿 <c>Map_Format::MapFormat</c> 只是为了它的
/// <c>width</c>；端口直接问 <see cref="RandomMapState.Width"/>，免得多引一个格式类型。
/// </para>
/// </remarks>
public sealed class RandomMapRegion
{
    /// <summary>Creates a region around its centre node, <c>Region::Region()</c>.</summary>
    /// <param name="id">The region id, which has to equal its index in the generator's list.</param>
    /// <param name="centerNode">The centre; its region field is set to <paramref name="id"/>.</param>
    /// <param name="playerColor">The colour index the region belongs to, <c>neutralColorIndex</c> for none.</param>
    /// <param name="ground">The ground the region is painted with.</param>
    /// <param name="expectedSize">How many tiles the region aims for, <c>sizeLimit</c>.</param>
    /// <param name="treasure">The value limit of its treasures, <c>treasureLimit</c>.</param>
    /// <param name="type">What the region is for.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expectedSize"/> is not positive.</exception>
    public RandomMapRegion(
        uint id,
        RandomMapNode centerNode,
        int playerColor,
        MapGround ground,
        int expectedSize,
        int treasure,
        RandomMapRegionType type)
    {
        ArgumentNullException.ThrowIfNull(centerNode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedSize);

        Id = id;
        CenterIndex = centerNode.Index;
        SizeLimit = expectedSize;
        ColorIndex = playerColor;
        GroundType = ground;
        TreasureLimit = treasure;
        Type = type;

        centerNode.Region = id;
        Nodes.Add(centerNode);
    }

    /// <summary>The region id, <c>id</c>; it is also the index of the region in the generator's list.</summary>
    public uint Id { get; }

    /// <summary>The tile the region grew from, <c>centerIndex</c>.</summary>
    public int CenterIndex { get; internal set; }

    /// <summary>The regions this one touches, <c>neighbours</c>.</summary>
    public HashSet<uint> Neighbours { get; } = [];

    /// <summary>The tiles the region took, <c>nodes</c>; in the order they were taken.</summary>
    public List<RandomMapNode> Nodes { get; } = [];

    /// <summary>
    /// Where the region joins its neighbours, <c>connections</c>: the neighbour's id to the tile of the join. Ordered
    /// by id like the <c>std::map</c> of the reference.
    /// </summary>
    public SortedDictionary<uint, int> Connections { get; } = [];

    /// <summary>How many tiles the region aims for, <c>sizeLimit</c>.</summary>
    public int SizeLimit { get; }

    /// <summary>How far the expansion has walked through <see cref="Nodes"/>, <c>lastProcessedNode</c>.</summary>
    public int LastProcessedNode { get; internal set; }

    /// <summary>The colour index of the owner, <c>colorIndex</c>.</summary>
    public int ColorIndex { get; internal set; }

    /// <summary>The ground the region is painted with, <c>groundType</c>.</summary>
    public MapGround GroundType { get; internal set; }

    /// <summary>The value limit of the treasures the region may hold, <c>treasureLimit</c>.</summary>
    public int TreasureLimit { get; internal set; }

    /// <summary>What the region is for, <c>type</c>.</summary>
    public RandomMapRegionType Type { get; internal set; }

    /// <summary>
    /// Grows the region by one node, <c>Region::checkAdjacentTiles()</c>
    /// (<c>map_random_generator_info.cpp:108-148</c>).
    /// </summary>
    /// <param name="state">The grid the region grows on.</param>
    /// <param name="distanceLimit">How far from the centre a tile may be, in tiles.</param>
    /// <param name="randomGenerator">The generator's random source; the diagonal steps are skipped half of the time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="randomGenerator"/> is <see langword="null"/>.</exception>
    public void CheckAdjacentTiles(RandomMapState state, double distanceLimit, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        RandomMapNode previousNode = Nodes[LastProcessedNode];
        Point centre = state.GetPoint(previousNode.Index);

        for (int direction = 0; direction < RandomMapState.DirectionOffsets.Length; ++direction)
        {
            if (Nodes.Count > SizeLimit)
            {
                previousNode.Type = RandomMapNodeType.Border;

                break;
            }

            Point offset = RandomMapState.DirectionOffsets[direction];
            Point newPosition = new(centre.X + offset.X, centre.Y + offset.Y);

            if (!state.IsValidPoint(newPosition))
            {
                continue;
            }

            RandomMapNode newTile = state.GetNodeToUpdate(newPosition);

            if (state.ApproximateDistance(CenterIndex, newTile.Index) > distanceLimit)
            {
                previousNode.Type = RandomMapNodeType.Border;

                continue;
            }

            // 对角方向只走一半的概率，边缘才不会长得像菱形（参考 :133-137）。
            if (direction > 3 && randomGenerator.NextInclusive(0, 1) == 1)
            {
                continue;
            }

            if (newTile.Region == 0 && newTile.Type == RandomMapNodeType.Open)
            {
                newTile.Region = Id;
                Nodes.Add(newTile);
            }
            else if (newTile.Region != Id)
            {
                previousNode.Type = RandomMapNodeType.Border;
                Neighbours.Add(newTile.Region);
            }
        }
    }

    /// <summary>
    /// Walks the nodes taken so far and lets them grow, <c>Region::regionExpansion()</c>
    /// (<c>map_random_generator_info.cpp:150-161</c>).
    /// </summary>
    /// <param name="state">The grid the region grows on.</param>
    /// <param name="randomGenerator">The generator's random source.</param>
    /// <returns><see langword="true"/> when the last walk added at least one tile.</returns>
    /// <remarks>
    /// 只处理进循环时就已经在 <see cref="Nodes"/> 里的节点，新吞进来的留到下一轮——这样每一轮都像一圈年轮。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> or <paramref name="randomGenerator"/> is <see langword="null"/>.</exception>
    public bool RegionExpansion(RandomMapState state, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        int nodesEnd = Nodes.Count;
        double distanceLimit = Math.Sqrt((double)SizeLimit / Math.PI) * 1.85;

        while (LastProcessedNode < nodesEnd)
        {
            CheckAdjacentTiles(state, distanceLimit, randomGenerator);
            ++LastProcessedNode;
        }

        return LastProcessedNode != Nodes.Count;
    }

    /// <summary>
    /// Turns a border node into a road end when it joins this region to a neighbour, and into a coast tile when the
    /// neighbour is water, <c>Region::checkNodeForConnections()</c>
    /// (<c>map_random_generator_info.cpp:163-225</c>).
    /// </summary>
    /// <param name="state">The grid the region grows on.</param>
    /// <param name="mapRegions">Every region of the map; the id of a region is its index here.</param>
    /// <param name="node">The border node to look at.</param>
    /// <returns><see langword="true"/> when the node became a <see cref="RandomMapNodeType.Connector"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public bool CheckNodeForConnections(
        RandomMapState state, IList<RandomMapRegion> mapRegions, RandomMapNode node)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(mapRegions);
        ArgumentNullException.ThrowIfNull(node);

        if (node.Type != RandomMapNodeType.Border)
        {
            return false;
        }

        Point position = state.GetPoint(node.Index);

        // A border tile that touches more than one other region is no place for a single road end.
        int distinctNeighbours = 0;
        uint seen = node.Region;

        for (int direction = 0; direction < RandomMapState.DirectionOffsets.Length; ++direction)
        {
            Point offset = RandomMapState.DirectionOffsets[direction];
            RandomMapNode tile = state.GetNode(new Point(position.X + offset.X, position.Y + offset.Y));

            if (tile.Index == -1 || tile.Region == Id)
            {
                continue;
            }

            if (seen != tile.Region)
            {
                seen = tile.Region;
                ++distinctNeighbours;

                if (distinctNeighbours > 1)
                {
                    return false;
                }
            }
        }

        for (int direction = 0; direction < 4; ++direction)
        {
            Point offset = RandomMapState.DirectionOffsets[direction];
            RandomMapNode adjacent = state.GetNodeToUpdate(new Point(position.X + offset.X, position.Y + offset.Y));

            if (adjacent.Index == -1 || adjacent.Region == Id)
            {
                continue;
            }

            if (mapRegions[(int)adjacent.Region].GroundType == MapGround.Water)
            {
                node.Type = RandomMapNodeType.Coast;

                break;
            }

            RandomMapNode twoAway = state.GetNodeToUpdate(
                new Point(position.X + (offset.X * 2), position.Y + (offset.Y * 2)));

            if (twoAway.Index == -1 || twoAway.Type != RandomMapNodeType.Open)
            {
                continue;
            }

            if (!Connections.ContainsKey(adjacent.Region))
            {
                Connections[adjacent.Region] = node.Index;
                mapRegions[(int)adjacent.Region].Connections[Id] = node.Index;

                node.Type = RandomMapNodeType.Connector;
                adjacent.Type = RandomMapNodeType.Connector;
                twoAway.Type = RandomMapNodeType.Connector;

                RandomMapNode stepBack = state.GetNodeToUpdate(new Point(position.X - offset.X, position.Y - offset.Y));

                if (stepBack.Index != -1)
                {
                    stepBack.Type = RandomMapNodeType.Connector;
                }

                break;
            }
        }

        return node.Type == RandomMapNodeType.Connector;
    }

    /// <summary>
    /// Moves the centre so that a castle fits in the region, <c>Region::adjustRegionToFitCastle()</c>
    /// (<c>map_random_generator_info.cpp:227-236</c>).
    /// </summary>
    /// <param name="state">The grid, for its width and its index arithmetic.</param>
    /// <returns>The top left tile of the castle.</returns>
    /// <remarks>
    /// 城堡占 <c>x-1 .. x+2</c>、<c>y .. y+2</c> 一带，所以中心必须离边缘至少四格；中心同时被挪到城堡下方两格
    /// （<c>centerIndex = index( castleX, castleY + 2 )</c>），参考就是这么写的。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public Point AdjustRegionToFitCastle(RandomMapState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Point startingLocation = state.GetPoint(CenterIndex);
        int castleX = Math.Min(Math.Max(startingLocation.X, 4), state.Width - 4);
        int castleY = Math.Min(Math.Max(startingLocation.Y, 4), state.Width - 4);

        CenterIndex = state.IndexAt(new Point(castleX, castleY + 2));

        return new Point(castleX, castleY);
    }
}

/// <summary>
/// Keeps the mines of a generated map balanced, <c>Maps::Random_Generator::MapEconomy</c>
/// (<c>map_random_generator_info.h:272-281</c>, <c>map_random_generator_info.cpp:91-106</c>).
/// </summary>
/// <remarks>
/// 参考数的七种矿里金矿只计数、从不被选：<c>pickNextMineResource()</c> 在另外六种里挑当前最少的那一种，
/// 于是每摆一座矿就把它记上一笔，下一座自然轮到别的资源。
/// </remarks>
public sealed class RandomMapEconomy
{
    /// <summary>The order the reference picks in; a tie goes to the earlier entry, like <c>std::min_element</c>.</summary>
    private static readonly Mp2ResourceType[] Rotation =
    [
        Mp2ResourceType.Wood,
        Mp2ResourceType.Ore,
        Mp2ResourceType.Crystal,
        Mp2ResourceType.Sulfur,
        Mp2ResourceType.Gems,
        Mp2ResourceType.Mercury,
    ];

    private readonly Dictionary<Mp2ResourceType, uint> _minesCount = new()
    {
        [Mp2ResourceType.Wood] = 0,
        [Mp2ResourceType.Ore] = 0,
        [Mp2ResourceType.Crystal] = 0,
        [Mp2ResourceType.Sulfur] = 0,
        [Mp2ResourceType.Gems] = 0,
        [Mp2ResourceType.Mercury] = 0,
        [Mp2ResourceType.Gold] = 0,
    };

    /// <summary>Records one more mine of a resource, <c>increaseMineCount()</c>.</summary>
    /// <param name="resource">The resource of the mine.</param>
    /// <exception cref="ArgumentOutOfRangeException">The resource is not one the economy counts.</exception>
    public void IncreaseMineCount(Mp2ResourceType resource)
    {
        if (!_minesCount.ContainsKey(resource))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resource), resource, "The economy only counts the seven mineable resources.");
        }

        ++_minesCount[resource];
    }

    /// <summary>The least used resource of the six that rotate, <c>pickNextMineResource()</c>.</summary>
    /// <returns>The resource the next mine should produce.</returns>
    public Mp2ResourceType PickNextMineResource()
    {
        Mp2ResourceType best = Rotation[0];

        foreach (Mp2ResourceType resource in Rotation)
        {
            if (_minesCount[resource] < _minesCount[best])
            {
                best = resource;
            }
        }

        return best;
    }

    /// <summary>How many mines of a resource were placed, for the generator's checks and for the tests.</summary>
    /// <param name="resource">The resource to ask about.</param>
    /// <returns>The count, <c>0</c> for one the economy does not count.</returns>
    public uint MineCount(Mp2ResourceType resource) => _minesCount.GetValueOrDefault(resource);

    /// <summary>
    /// Which map object stands for a mine of a resource, <c>getFakeMP2MineType()</c>
    /// (<c>map_random_generator_helper.cpp:188-207</c>).
    /// </summary>
    /// <param name="resource">The resource the mine produces.</param>
    /// <returns>
    /// <c>Sawmill</c> for wood and ore, <c>Mine</c> for the four gems, <c>AbandonedMine</c> for gold and
    /// <see cref="Mp2ObjectType.None"/> for anything else (the reference asserts on that case).
    /// </returns>
    /// <remarks>
    /// 原版只有三种矿的图形：锯木厂（木、铁）、矿（四种宝石）、废弃矿（金）。落子时按产出资源挑一个「长得像」的。
    /// </remarks>
    public static Mp2ObjectType MineObjectType(Mp2ResourceType resource) => resource switch
    {
        Mp2ResourceType.Wood or Mp2ResourceType.Ore => Mp2ObjectType.Sawmill,
        Mp2ResourceType.Sulfur or Mp2ResourceType.Crystal or Mp2ResourceType.Gems or Mp2ResourceType.Mercury =>
            Mp2ObjectType.Mine,
        Mp2ResourceType.Gold => Mp2ObjectType.AbandonedMine,
        _ => Mp2ObjectType.None,
    };
}
