using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The object phase of <c>generateMap()</c>: castles and primary mines, region connectors, the trees on the borders,
/// the secondary mines, the treasure prefabs, the decorations, the guards of the joins and the free pickups —
/// steps 5 to 11 (<c>map_random_generator.cpp:751-957</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这一类的全部内容就是<b>次序</b>。参考把物件分成十一小步，每一步都假设前面几步已经占好了格子：城堡先落，
/// 主矿才不会落在城堡里；连接格先算出来，路面最后才铺；矿与城堡之后才连路，否则路会从它们身上穿过去；
/// 宝物与装饰排在最后，因为它们只要有空地就能长。次序一换，同一张种子就会摇出不同的图。
/// </para>
/// <para>
/// 每一步的实现都在各自的类里（<see cref="RandomMapCastlePhase"/>、<see cref="RandomMapMinePhase"/>、
/// <see cref="RandomMapObjectPhase"/>、<see cref="RandomMapDecorationPhase"/>），这里只负责按参考的顺序调用、
/// 在步与步之间传递那几样东西（经济计数、主矿位置、守卫强度）。
/// </para>
/// </remarks>
public static class RandomMapObjectPasses
{
    /// <summary>
    /// Runs steps 5 to 11 of <c>generateMap()</c> over a laid out terrain.
    /// </summary>
    /// <param name="map">The map that already carries the terrain.</param>
    /// <param name="terrain">The laid out regions and the grid they grew on.</param>
    /// <param name="config">The generator configuration.</param>
    /// <param name="randomGenerator">The generator's random source.</param>
    /// <returns><see langword="false"/> when a player's starting castle could not be placed.</returns>
    /// <remarks>
    /// 步 10 的守卫强度按地图边长分档：小图（36）用 3500 / 6000，其余用 4500 / 7500。参考把「同一个区域类型的
    /// 两个区域之间的接口」看成大道、其余的接口看成小路，大路守得重。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static bool Run(
        Mp2Map map, RandomMapTerrain terrain, RandomMapConfig config, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        RandomMapState state = terrain.State;
        IReadOnlyList<RandomMapRegion> regions = terrain.Regions;
        RandomMapRegionalObjects configuration = RandomMapRegionalObjects.For(config.ResourceDensity);
        RandomMapEconomy economy = new();

        // Step 5: every region gets its castle (or the path out of it), then the starting regions get wood and ore.
        SortedSet<int> primaryMineLocations = [];

        foreach (RandomMapRegion region in regions)
        {
            if (!RandomMapCastlePhase.PlaceRegionCastle(
                    map, state, region, terrain.RegionSizeLimit, config.ResourceDensity, randomGenerator))
            {
                return false;
            }

            primaryMineLocations.UnionWith(
                RandomMapMinePhase.PlacePrimaryMines(map, state, region, economy, config.MonsterStrength, randomGenerator));
        }

        // Step 6: connectors first, then a tree on every border tile.
        ConnectRegions(state, regions);
        DecorateBorders(map, state, regions, randomGenerator);

        // Step 7: mines, then the roads that join the regions.
        foreach (RandomMapRegion region in regions)
        {
            if (region.GroundType == MapGround.Water)
            {
                continue;
            }

            RandomMapMinePhase.FillRegion(
                map, state, region, configuration, terrain.RegionSizeLimit, economy, config.MonsterStrength,
                primaryMineLocations);

            RandomMapMinePhase.ConnectRegion(map, state, region);
        }

        // Step 8: power-ups, then the treasure prefabs chosen by the planner.
        PlacePowerUpsAndTreasures(map, state, regions, configuration, config.MonsterStrength, randomGenerator);

        // Step 9: decorations fill what is left.
        DecorateRegions(map, state, regions, randomGenerator);

        // Step 10: the joins nobody passed through get a guard.
        GuardConnections(map, state, regions, config.MonsterStrength, map.Width == (int)MapSize.Small);

        // Step 11: free resources along the roads.
        PlaceFreePickups(map, state, regions, configuration, randomGenerator);

        // Then the roads are reordered: every tile gets the joint its neighbours need.
        RandomMapRoads.UpdateAll(map, state, randomGenerator);

        return true;
    }

    /// <summary>
    /// Lets every region look at its border tiles and register the joins, step 6
    /// (<c>map_random_generator.cpp:826-833</c>).
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="regions">Every region; index 0 is the water placeholder.</param>
    /// <remarks>
    /// 参考跳过水面区域（<c>if ( region.groundType == Ground::WATER ) continue;</c>），水面没有路可修。
    /// 节点列表在循环中被读取，而 <see cref="RandomMapRegion.CheckNodeForConnections"/> 只改格子的类型、不改列表。
    /// </remarks>
    private static void ConnectRegions(RandomMapState state, IReadOnlyList<RandomMapRegion> regions)
    {
        List<RandomMapRegion> list = [.. regions];

        foreach (RandomMapRegion region in list)
        {
            if (region.GroundType == MapGround.Water)
            {
                continue;
            }

            foreach (RandomMapNode node in region.Nodes)
            {
                region.CheckNodeForConnections(state, list, node);
            }
        }
    }

    /// <summary>
    /// Drops trees on the border of every region, the second half of step 6 (<c>:835-841</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="regions">Every region.</param>
    /// <param name="randomGenerator">The generator.</param>
    private static void DecorateBorders(
        Mp2Map map, RandomMapState state, IReadOnlyList<RandomMapRegion> regions, Pcg32 randomGenerator)
    {
        foreach (RandomMapRegion region in regions)
        {
            foreach (RandomMapNode node in region.Nodes)
            {
                if (node.Type == RandomMapNodeType.Border)
                {
                    RandomMapDecorationPhase.PlaceBorderObstacle(map, state, node.Index, region.GroundType, randomGenerator);
                }
            }
        }
    }

    /// <summary>
    /// Places the power-up groups and the treasure prefabs of every land region, step 8 (<c>:883-908</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="regions">Every region.</param>
    /// <param name="configuration">The density row that says how many power-ups and treasures a region wants.</param>
    /// <param name="monsterStrength">The player's monster strength setting, which decides the guards.</param>
    /// <param name="randomGenerator">The generator.</param>
    /// <remarks>
    /// 宝物先「规划」再落：<see cref="RandomMapObjectPhase.PlanObjectPlacement"/> 在不改地图的前提下算出哪些格子放得下，
    /// 再从那批格子里按「越散越好」挑 <c>treasureCount</c> 个真正落下去。这样挑位置时整片区域都还是空的，
    /// 不会因为前面的组占了格子而把后面的组挤到角落里。
    /// </remarks>
    private static void PlacePowerUpsAndTreasures(
        Mp2Map map,
        RandomMapState state,
        IReadOnlyList<RandomMapRegion> regions,
        RandomMapRegionalObjects configuration,
        RandomMapMonsterStrength monsterStrength,
        Pcg32 randomGenerator)
    {
        foreach (RandomMapRegion region in regions)
        {
            if (region.GroundType == MapGround.Water)
            {
                continue;
            }

            RandomMapObjectPhase.PlaceObjectSet(
                map, state, region, RandomMapObjectSets.PowerUps, monsterStrength, configuration.PowerUpsCount,
                randomGenerator);

            List<(int Tile, RandomMapObjectSet Set)> plan = RandomMapObjectPhase.PlanObjectPlacement(
                state, region, RandomMapObjectSets.Prefabs, randomGenerator);

            if (plan.Count == 0)
            {
                continue;
            }

            List<int> candidates = [.. plan.Select(entry => entry.Tile)];

            foreach (int tileIndex in RandomMapTileSelection.PickEvenlySpacedTiles(
                         state, candidates, configuration.TreasureCount, [region.CenterIndex]))
            {
                foreach ((int Tile, RandomMapObjectSet Set) entry in plan)
                {
                    if (entry.Tile == tileIndex)
                    {
                        RandomMapObjectPhase.PlaceTreasureGroup(
                            map, state, region, entry.Set, state.GetPoint(tileIndex), monsterStrength, randomGenerator);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fills the corners of every region, step 9 (<c>:910-919</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="regions">Every region.</param>
    /// <param name="randomGenerator">The generator.</param>
    /// <remarks>
    /// 两遍：先放这个地形自己的一套景，再放与地形无关的 <see cref="RandomMapDecorations.Common"/>。参考对水面也查一次
    /// 地形的表（查不到就跳过），但不放公共那一套——水里没有树。
    /// </remarks>
    private static void DecorateRegions(
        Mp2Map map, RandomMapState state, IReadOnlyList<RandomMapRegion> regions, Pcg32 randomGenerator)
    {
        foreach (RandomMapRegion region in regions)
        {
            IReadOnlyList<RandomMapDecorationSet> own = RandomMapDecorations.For(region.GroundType);

            if (own.Count > 0)
            {
                RandomMapDecorationPhase.PlaceDecorations(map, state, region, own, randomGenerator);
            }

            if (region.GroundType != MapGround.Water)
            {
                RandomMapDecorationPhase.PlaceDecorations(map, state, region, RandomMapDecorations.Common, randomGenerator);
            }
        }
    }

    /// <summary>
    /// Guards the joins of a region, step 10 (<c>:921-934</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="regions">Every region; the id of a region is its index here.</param>
    /// <param name="monsterStrength">The player's monster strength setting, which decides the pool.</param>
    /// <param name="isSmallMap">Whether the map is 36 tiles wide, which lowers both value thresholds.</param>
    /// <remarks>
    /// 两档强度按「接口两边是不是同一种区域」分：玩家区与玩家区之间的接口是大路，守得重；其余的接口是小路，守得轻。
    /// 守门的队伍站在接口格上，玩家想过去就得先打——这也是参考的意图（<c>placeMonster( tileIndex, guard )</c>）。
    /// </remarks>
    private static void GuardConnections(
        Mp2Map map,
        RandomMapState state,
        IReadOnlyList<RandomMapRegion> regions,
        RandomMapMonsterStrength monsterStrength,
        bool isSmallMap)
    {
        int weakValue = isSmallMap ? WeakGuardValueSmallMap : WeakGuardValue;
        int strongValue = isSmallMap ? StrongGuardValueSmallMap : StrongGuardValue;

        foreach (RandomMapRegion region in regions)
        {
            foreach (KeyValuePair<uint, int> connection in region.Connections)
            {
                int tileIndex = connection.Value;

                bool sameType = regions[(int)connection.Key].Type == region.Type;

                RandomMapMonsterSelection guard = RandomMapMonsterPools.ByValue(
                    monsterStrength, sameType ? strongValue : weakValue);

                RandomMapObjectPhase.PlaceMonster(map, state, state.GetPoint(tileIndex), guard);
            }
        }
    }

    /// <summary>
    /// Scatters free resources along the roads of a region, step 11 (<c>:936-957</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="regions">Every region.</param>
    /// <param name="configuration">The density row, which says how many pickups a region gets.</param>
    /// <param name="randomGenerator">The generator.</param>
    /// <remarks>
    /// 参考先在路上随机挑一格，再往外挪一格——挪得动就挪，挪不动就留在路上。这样资源都落在路边而不是路中间，
    /// 玩家沿着路走就能顺手捡到。资源本身是「随机资源」那一件物件，捡到才知道是什么。
    /// </remarks>
    private static void PlaceFreePickups(
        Mp2Map map,
        RandomMapState state,
        IReadOnlyList<RandomMapRegion> regions,
        RandomMapRegionalObjects configuration,
        Pcg32 randomGenerator)
    {
        if (!TryRandomResourceIndex(out int objectIndex))
        {
            return;
        }

        foreach (RandomMapRegion region in regions)
        {
            List<int> pathTiles = RandomMapTileSelection.FindTilesByType(region, RandomMapNodeType.Path);

            if (pathTiles.Count == 0)
            {
                continue;
            }

            for (int count = 0; count < configuration.TreasureCount * 2; ++count)
            {
                int tileIndex = Rand.GetWithGen(pathTiles, randomGenerator);
                Direction direction = Rand.GetWithGen(Directions.AllNeighboringDirections, randomGenerator);
                Point point = state.GetPoint(tileIndex);
                Point neighbour = point + OffsetOf(direction);

                if (state.IsValidPoint(neighbour) && state.GetNode(neighbour).Type == RandomMapNodeType.Open)
                {
                    point = neighbour;
                }

                if (!RandomMapObjectWriter.TryWrite(map, point, ObjectGroup.AdventureTreasures, objectIndex))
                {
                    continue;
                }

                RandomMapObjectRules.MarkNodeAsType(state, point, RandomMapNodeType.Action);
            }
        }
    }

    /// <summary>The index of the random resource in <c>ADVENTURE_TREASURES</c>, <c>convertMP2ToObjectInfo( OBJ_RANDOM_RESOURCE )</c>.</summary>
    /// <param name="objectIndex">Its entry in the group.</param>
    /// <returns><see langword="false"/> when the catalogue has no such object.</returns>
    private static bool TryRandomResourceIndex(out int objectIndex)
    {
        IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.AdventureTreasures);

        for (int index = 0; index < objects.Count; ++index)
        {
            if (objects[index].ObjectType == Mp2ObjectType.ActionRandomResource)
            {
                objectIndex = index;

                return true;
            }
        }

        objectIndex = -1;

        return false;
    }

    /// <summary>Tile offset of a direction, <c>Direction::GetDirectionOffset()</c>.</summary>
    /// <param name="direction">One of the eight neighbours.</param>
    /// <returns>The offset from a tile to its neighbour in that direction.</returns>
    private static Point OffsetOf(Direction direction) => direction switch
    {
        Direction.TopLeft => new Point(-1, -1),
        Direction.Top => new Point(0, -1),
        Direction.TopRight => new Point(1, -1),
        Direction.Right => new Point(1, 0),
        Direction.BottomRight => new Point(1, 1),
        Direction.Bottom => new Point(0, 1),
        Direction.BottomLeft => new Point(-1, 1),
        Direction.Left => new Point(-1, 0),
        _ => new Point(0, 0),
    };

    /// <summary>The value of a weak guard on a map wider than 36 tiles, the <c>4500</c> of <c>:924</c>.</summary>
    private const int WeakGuardValue = 4500;

    /// <summary>The value of a strong guard on a map wider than 36 tiles, the <c>7500</c> of <c>:925</c>.</summary>
    private const int StrongGuardValue = 7500;

    /// <summary>The value of a weak guard on a 36 tile map, the <c>3500</c> of <c>:924</c>.</summary>
    private const int WeakGuardValueSmallMap = 3500;

    /// <summary>The value of a strong guard on a 36 tile map, the <c>6000</c> of <c>:925</c>.</summary>
    private const int StrongGuardValueSmallMap = 6000;
}
