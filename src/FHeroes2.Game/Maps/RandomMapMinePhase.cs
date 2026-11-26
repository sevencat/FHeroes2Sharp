using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The two mine passes of <c>generateMap()</c>: the wood and ore mines every player start gets, and the balanced
/// spreading of the remaining mines over every region, followed by the roads that join the regions
/// (<c>map_random_generator.cpp:806-881</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把矿分两次放：步 5 先给每个<b>玩家起点</b>找一块木矿与一块铁矿（从离中心第 4 环以外的空地开始试，找得到才收），
/// 记进 <c>primaryMineLocations</c>；步 7 再给每个区域按配置补 <c>mineCount</c> 座矿与 <c>goldMineCount</c> 座金矿，
/// 挑格时躲开那些木矿与铁矿。这样玩家的「主矿」不会被无关的宝石矿挤掉。
/// </para>
/// <para>
/// 步 7 的最后一件事是<b>区域连接</b>：每个区域与邻居的接口格标成路，再从那里修一条到最近路面的路。参考特意把它排在
/// 矿与城堡之后——先修路会让路从矿与城堡要占的格子上穿过去。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：参考在步 5 先摆城堡、再摆主矿，城堡会把中心格换成城堡下方两格；端口还没有城堡那一段，
/// 所以 <see cref="PlacePrimaryMines"/> 用的是未经 <see cref="RandomMapRegion.AdjustRegionToFitCastle"/> 调整的中心。
/// 城堡落地之后调用顺序要与参考一致，否则会摇出不同的图。
/// </para>
/// </remarks>
public static class RandomMapMinePhase
{
    /// <summary>A region this big gets the full mine count, <c>regionSizeForSecondaryMines</c> (<c>:60</c>).</summary>
    public const int RegionSizeForSecondaryMines = 250;

    /// <summary>
    /// Only a land this big holds gold mines, <c>regionSizeForGoldMine</c> (<c>:61</c>).
    /// </summary>
    public const int RegionSizeForGoldMine = 350;

    /// <summary>
    /// The first ring a starting player's primary mines may use, the <c>4</c> of the <c>ringIndex</c> loop
    /// (<c>:812</c>).
    /// </summary>
    /// <remarks>
    /// 前四环留给城堡与英雄：主矿要是贴着城堡，玩家一出门就踩到，反而没有扩张的感觉。
    /// </remarks>
    public const int FirstPrimaryMineRing = 4;

    /// <summary>The two resources a player's starting region is guaranteed a mine of, <c>{ WOOD, ORE }</c>.</summary>
    private static readonly Mp2ResourceType[] PrimaryMineResources =
    [
        Mp2ResourceType.Wood,
        Mp2ResourceType.Ore,
    ];

    /// <summary>
    /// Places the wood and ore mine of a player's starting region, the <c>if ( region.type == STARTING )</c> block
    /// of <c>generateMap()</c> (<c>:806-820</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region; only a <see cref="RandomMapRegionType.Starting"/> region gets mines.</param>
    /// <param name="economy">The counter that records what was placed.</param>
    /// <param name="monsterStrength">The player's monster strength setting, which decides the guards.</param>
    /// <param name="randomGenerator">The generator, used for the ring jitter and the shuffles.</param>
    /// <returns>
    /// The tiles the two mines landed on, ordered by index like the <c>std::set</c> of the reference; fewer than two
    /// entries when a ring ran out of usable tiles.
    /// </returns>
    /// <remarks>
    /// 每种资源都从第 <see cref="FirstPrimaryMineRing"/> 环开始一圈一圈往外找，<b>找到第一块就换下一种资源</b>。
    /// 同一环里 <see cref="RandomMapObjectPhase.PlaceMine"/> 会按环内被打乱的顺序逐格试，所以主矿的位置每次都不一样。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static SortedSet<int> PlacePrimaryMines(
        Mp2Map map,
        RandomMapState state,
        RandomMapRegion region,
        RandomMapEconomy economy,
        RandomMapMonsterStrength monsterStrength,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        SortedSet<int> placed = [];

        if (region.Type != RandomMapRegionType.Starting)
        {
            return placed;
        }

        List<List<int>> rings = RandomMapTileSelection.FindOpenTilesSortedJittered(region, state.Width, randomGenerator);

        foreach (Mp2ResourceType resource in PrimaryMineResources)
        {
            for (int ringIndex = FirstPrimaryMineRing; ringIndex < rings.Count; ++ringIndex)
            {
                int mineIndex = RandomMapObjectPhase.PlaceMine(
                    map, state, economy, rings[ringIndex], resource, monsterStrength);

                if (mineIndex != -1)
                {
                    placed.Add(mineIndex);

                    break;
                }
            }
        }

        return placed;
    }

    /// <summary>
    /// Fills one region with its secondary and gold mines, the start of step 7 of <c>generateMap()</c>
    /// (<c>:843-871</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region to fill; water regions get nothing.</param>
    /// <param name="configuration">The density row that says how many mines a region wants.</param>
    /// <param name="regionSizeLimit">The generator's region size, which decides whether a region gets the full count.</param>
    /// <param name="economy">The counter that spreads the resources and records the placement.</param>
    /// <param name="monsterStrength">The player's monster strength setting, which decides the guards.</param>
    /// <param name="primaryMineLocations">The tiles of the players' wood and ore mines, which the picks avoid.</param>
    /// <returns>How many mines were placed, gold ones included.</returns>
    /// <remarks>
    /// <para>
    /// 「小区域只给一座矿，中立区不管大小都给满」是参考的原话：中立区是玩家要去抢的地方，太小也得有东西可抢；
    /// 而玩家自己的副矿按面积给，免得小图上一步一矿。
    /// </para>
    /// <para>
    /// 候选格是「能放下金矿并且修得出路」的空地（<see cref="RandomMapObjectPhase.FindTilesForPlacement"/>），
    /// 再按「越散越好」挑 <c>mineCount * 3</c> 个——留出三倍余量是因为有些格子随后会被前面落的矿占掉。
    /// 资源由 <see cref="RandomMapEconomy.PickNextMineResource"/> 轮流挑当前最少的那种，金矿最后单独补。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static int FillRegion(
        Mp2Map map,
        RandomMapState state,
        RandomMapRegion region,
        RandomMapRegionalObjects configuration,
        int regionSizeLimit,
        RandomMapEconomy economy,
        RandomMapMonsterStrength monsterStrength,
        IReadOnlySet<int> primaryMineLocations)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(primaryMineLocations);

        if (region.GroundType == MapGround.Water)
        {
            return 0;
        }

        // 参考拿草地上的金矿当「矿占多大」的样板：所有矿都是同一个尺寸，拿哪一张图问都一样。
        if (!RandomMapObjectRules.TryGetObjectInfo(
                ObjectGroup.AdventureMines, RandomMapMines.ObjectInfoIndex(Mp2ResourceType.Gold, MapGround.Grass),
                out ObjectInfo mineInfo))
        {
            return 0;
        }

        List<int> options = RandomMapObjectPhase.FindTilesForPlacement(
            state, region.Id, RandomMapTileSelection.FindTilesByType(region, RandomMapNodeType.Open), mineInfo);

        if (options.Count == 0)
        {
            return 0;
        }

        int secondaryMineCount = regionSizeLimit > RegionSizeForSecondaryMines
                                 || region.Type == RandomMapRegionType.Neutral
            ? configuration.MineCount
            : 1;

        List<int> avoidance = [.. primaryMineLocations];
        avoidance.Sort();

        options = RandomMapTileSelection.PickEvenlySpacedTiles(
            state, options, secondaryMineCount * 3, avoidance);

        int placed = 0;

        for (int index = 0; index < secondaryMineCount; ++index)
        {
            Mp2ResourceType resource = economy.PickNextMineResource();

            if (RandomMapObjectPhase.PlaceMine(map, state, economy, options, resource, monsterStrength) != -1)
            {
                ++placed;
            }
        }

        if (regionSizeLimit > RegionSizeForGoldMine)
        {
            for (int index = 0; index < configuration.GoldMineCount; ++index)
            {
                if (RandomMapObjectPhase.PlaceMine(
                        map, state, economy, options, Mp2ResourceType.Gold, monsterStrength) != -1)
                {
                    ++placed;
                }
            }
        }

        return placed;
    }

    /// <summary>
    /// Marks a region's connection tiles as roads and builds the road back to the existing network, the tail of
    /// step 7 (<c>:873-880</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region whose <see cref="RandomMapRegion.Connections"/> are built.</param>
    /// <returns>How many connection tiles got a road to the network.</returns>
    /// <remarks>
    /// 参考直接给接口格赋 <c>PATH</c>（绕开「边界不被覆盖」的那条规矩），因为
    /// <see cref="RandomMapRegion.CheckNodeForConnections"/> 早就把它从 <c>BORDER</c> 提升成了 <c>CONNECTOR</c>，
    /// 而 <see cref="RandomMapObjectRules.MarkNodeIndexAsType"/> 允许 <c>CONNECTOR</c> 变成 <c>PATH</c>。
    /// 修不出路时接口格仍留在 <c>PATH</c> 上——参考也这样，那条路等后续的矿与宝物从它旁边过时会被接上。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="state"/> is <see langword="null"/>.</exception>
    public static int ConnectRegion(Mp2Map map, RandomMapState state, RandomMapRegion region)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);

        int connected = 0;

        foreach (KeyValuePair<uint, int> connection in region.Connections)
        {
            int tileIndex = connection.Value;

            RandomMapObjectRules.MarkNodeIndexAsType(state, tileIndex, RandomMapNodeType.Path);

            List<int> path = RandomMapRoadBuilder.FindPathToNearestRoad(state, region.Id, tileIndex);

            if (path.Count == 0)
            {
                continue;
            }

            foreach (int step in path)
            {
                RandomMapObjectPhase.ForceTempRoadOnTile(map, state, step);
            }

            ++connected;
        }

        return connected;
    }
}
