using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Editor;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The placement steps of the object phase that work on one anchor tile: a single object, a whole prefab
/// with its rolled prizes and its guard — <c>placeSimpleObject()</c>, <c>placeValidTreasures()</c> and
/// <c>placeMonster()</c> (<c>map_random_generator_helper.cpp:936-1112</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这三段是「判定 → 落盘 → 占格」的粘合层：判定在 <see cref="RandomMapObjectRules"/>，落盘在
/// <see cref="RandomMapObjectWriter"/>，占格又回到规则类。它们自己不含任何地图知识，只按参考的次序调用。
/// </para>
/// <para>
/// 参考的 <c>placeObjectSet</c>/<c>planObjectPlacement</c> 还要求「先能修一条到最近的路」才算放过关（找不到路就整组跳过），
/// 那是修路那一段的事；这里提供的是它们脚下的三层，修路接上之后直接复用。
/// </para>
/// </remarks>
public static class RandomMapObjectPhase
{
    /// <summary>
    /// Writes one object of a prefab at its offset from <paramref name="anchor"/>,
    /// <c>placeSimpleObject()</c> (<c>:958-973</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid, whose tiles get marked once the object is written.</param>
    /// <param name="anchor">Anchor tile of the prefab.</param>
    /// <param name="placement">The object and its offset from the anchor.</param>
    /// <param name="ground">The ground of the region, which decides the tree and mountain variants.</param>
    /// <returns><see langword="true"/> when the object was written and marked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="state"/> is <see langword="null"/>.</exception>
    public static bool PlaceSimpleObject(
        Mp2Map map,
        RandomMapState state,
        in Point anchor,
        RandomMapObjectPlacement placement,
        MapGround ground)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);

        Point position = new(anchor.X + placement.Offset.X, anchor.Y + placement.Offset.Y);
        int objectIndex = RandomMapObjectRules.SelectTerrainVariant(placement.Group, placement.ObjectIndex, ground);

        if (!RandomMapObjectRules.TryGetObjectInfo(placement.Group, objectIndex, out ObjectInfo info))
        {
            return false;
        }

        if (!RandomMapObjectWriter.TryWrite(map, position, info))
        {
            return false;
        }

        RandomMapObjectRules.MarkObjectPlacement(state, info, position);

        return true;
    }

    /// <summary>
    /// Writes the guard of an object, <c>placeMonster()</c> (<c>:936-956</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="position">The tile the guard stands on.</param>
    /// <param name="selection">The stack to place, <see cref="RandomMapMonsterSelection.None"/> for no guard.</param>
    /// <returns><see langword="true"/> when a guard was written.</returns>
    /// <remarks>
    /// 怪物格直接标 <c>ACTION</c>（而不是 <see cref="RandomMapObjectRules.MarkObjectPlacement"/> 那套「占满地面层」）：
    /// 与参考一致，怪物只占一格，玩家要站到它前面才能打。参考随后还会把「可转成的兵种」名单写进 fh2 存档专用的
    /// <c>monsterMetadata</c>，MP2 文件没有这个字段，读图时按档位随机，所以这里不写。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="state"/> is <see langword="null"/>.</exception>
    public static bool PlaceMonster(
        Mp2Map map, RandomMapState state, in Point position, RandomMapMonsterSelection selection)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);

        if (selection.IsEmpty)
        {
            return false;
        }

        if (!TryMonsterCatalogueIndex(selection.MonsterId, out int objectIndex))
        {
            return false;
        }

        if (!RandomMapObjectWriter.TryWrite(map, position, ObjectGroup.Monsters, objectIndex))
        {
            return false;
        }

        RandomMapObjectRules.MarkNodeAsType(state, position, RandomMapNodeType.Action);

        return true;
    }

    /// <summary>
    /// The entry of <c>ROADS</c> a route gets before <see cref="RandomMapRoads.UpdateAll"/> reorders them, the
    /// <c>2</c> of <c>forceTempRoadOnTile()</c> (<c>:758</c>).
    /// </summary>
    public const int TemporaryRoadIndex = 2;

    /// <summary>
    /// Lays a temporary road on one tile of a route, <c>forceTempRoadOnTile()</c>
    /// (<c>map_random_generator_helper.cpp:729-757</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="index">The tile of the route.</param>
    /// <returns><see langword="false"/> when the tile holds an obstacle and gets no road.</returns>
    /// <remarks>
    /// <para>
    /// 「临时」是因为这些格子只是为了让择位时的路由成立：真正的路面最后会由
    /// <see cref="RandomMapRoads.UpdateAll"/> 一次性重排，那时才按相邻格算出正确的直/弯/三叉。这里先把
    /// <c>ROADS</c> 组的第 <see cref="TemporaryRoadIndex"/> 号放上去，够用即可。
    /// </para>
    /// <para>
    /// 地面图里自带装饰（草堆、石头）时先换成一张干净的：路是贴在地面上的，装饰会从路面下面露出来。
    /// 参考用的是 <c>Ground::getRandomTerrainImageIndex( ground, false )</c>（全局随机）；端口按 ADR-274 的规矩改用
    /// <see cref="TerrainTransitions.PlainImageIndex"/> 的确定性变体，让同一个种子生成同一张图。
    /// </para>
    /// <para>
    /// 落了路就把 UID 记进 <see cref="RandomMapState.Roads"/>：重排时按它撤旧件、按它写新件。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a tile of the grid.</exception>
    public static bool ForceTempRoadOnTile(Mp2Map map, RandomMapState state, int index)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);

        if (index < 0 || index >= state.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The tile is not part of the grid.");
        }

        if (state.GetNode(index).Type == RandomMapNodeType.Obstacle)
        {
            // 路不该从石头里穿过去。
            return false;
        }

        RandomMapObjectRules.MarkNodeIndexAsType(state, index, RandomMapNodeType.Path);

        Point point = state.GetPoint(index);
        Mp2TileInfo tile = map.Tile(point.X, point.Y);

        if (HasRoad(map, tile))
        {
            return true;
        }

        if (MapGrounds.ContainsEmbeddedObjects(tile.TerrainImageIndex))
        {
            MapGround ground = MapGrounds.FromTerrainImageIndex(tile.TerrainImageIndex);

            map.SetTile(point.X, point.Y, tile with
            {
                TerrainImageIndex = TerrainTransitions.PlainImageIndex(ground, point),
            });
        }

        uint uid = map.TakeObjectUid();

        if (!RandomMapObjectWriter.TryWriteRoad(map, point, TemporaryRoadIndex, uid))
        {
            return false;
        }

        state.Roads[index] = new RandomMapRoadPlacement(uid, TemporaryRoadIndex);

        return true;
    }

    /// <summary>
    /// Writes a whole prefab: its obstacles, its prizes and its guard, <c>placeValidTreasures()</c>
    /// (<c>:1080-1112</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region the prefab lands in; its treasure budget shrinks by what was placed.</param>
    /// <param name="set">The prefab.</param>
    /// <param name="anchor">Anchor tile of the prefab.</param>
    /// <param name="monsterStrength">The player's monster strength setting, which decides the guard.</param>
    /// <param name="randomGenerator">The generator, used to roll the prizes.</param>
    /// <returns>What the group cost, i.e. the amount taken off <see cref="RandomMapRegion.TreasureLimit"/>.</returns>
    /// <remarks>
    /// <para>
    /// 两个奖品走两条路：<c>ADVENTURE_POWER_UPS</c> 是预制里点名的能力点，原样放；其余（宝箱位）按当前预算现摇一个宝物，
    /// 摇出来的东西占用预制给的那个格子。每摇一个，预算就减掉已经放下的价值，所以越往后摇到的越便宜——
    /// <see cref="RandomMapObjectValues.MinimumTreasureValue"/> 是地板，允许总价超出区域预算（参考的注释写着这是故意
    /// 的：「It is possible to go into the negatives」）。
    /// </para>
    /// <para>
    /// 守卫按**这一组放下的总价值**挑强度，不是按区域预算：一组四件便宜宝物也能引来一支小队伍。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static int PlaceTreasureGroup(
        Mp2Map map,
        RandomMapState state,
        RandomMapRegion region,
        RandomMapObjectSet set,
        in Point anchor,
        RandomMapMonsterStrength monsterStrength,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        foreach (RandomMapObjectPlacement obstacle in set.Obstacles)
        {
            PlaceSimpleObject(map, state, anchor, obstacle, region.GroundType);
        }

        int groupValueLimit = Math.Min(region.TreasureLimit, RandomMapObjectValues.MaximumTreasureGroupValue);
        int groupValue = 0;

        foreach (RandomMapObjectPlacement valuable in set.Valuables)
        {
            if (valuable.Group == ObjectGroup.AdventurePowerUps)
            {
                if (PlaceSimpleObject(map, state, anchor, valuable, region.GroundType))
                {
                    groupValue += ValueOf(valuable, region.GroundType);
                }

                continue;
            }

            int valueLimit = Math.Max(RandomMapObjectValues.MinimumTreasureValue, groupValueLimit - groupValue);

            if (!RandomMapTreasureSelection.TryPick(valueLimit, randomGenerator, out RandomMapObjectPlacement picked))
            {
                continue;
            }

            RandomMapObjectPlacement atOffset = picked with { Offset = valuable.Offset };

            if (PlaceSimpleObject(map, state, anchor, atOffset, region.GroundType))
            {
                groupValue += ValueOf(atOffset, region.GroundType);
            }
        }

        region.TreasureLimit -= groupValue;

        PlaceMonster(map, state, anchor, RandomMapMonsterPools.ByValue(monsterStrength, groupValue));

        return groupValue;
    }

    /// <summary>How many tiles one selection pass may try, <c>maxPlacementAttempts</c> (<c>:65</c>).</summary>
    public const int MaxPlacementAttempts = 99;

    /// <summary>
    /// Chooses where treasure groups would go without touching the map, <c>planObjectPlacement()</c>
    /// (<c>:1014-1078</c>).
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region being filled; its treasure budget limits the plan.</param>
    /// <param name="sets">The prefabs to choose from.</param>
    /// <param name="randomGenerator">The generator, used for the shuffles.</param>
    /// <returns>The anchor tile of every prefab that fits, with the prefab.</returns>
    /// <remarks>
    /// <para>
    /// <b>什么也不改</b>：外层事务在返回时把所有标记撤回，内层事务只在「这一组确实成立」时提交。这正是参考的
    /// <c>planObjectPlacement</c> 用两个 <c>MapStateTransaction</c> 干的事——规划阶段唯一的产物是那个 <c>vector</c>。
    /// </para>
    /// <para>
    /// 「成立」比「放得下」多一条：<see cref="RandomMapRoadBuilder.FindPathToNearestRoad"/> 得能修出一条到最近的路，
    /// 修不出来就整组跳过。预算按预制里点名的宝物编号扣（不是现摇出来的那件）。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static List<(int Tile, RandomMapObjectSet Set)> PlanObjectPlacement(
        RandomMapState state,
        RandomMapRegion region,
        IReadOnlyList<RandomMapObjectSet> sets,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        List<(int Tile, RandomMapObjectSet Set)> planned = [];

        if (region.TreasureLimit <= 0 || sets.Count == 0)
        {
            return planned;
        }

        using RandomMapStateTransaction revert = state.BeginTransaction();

        List<RandomMapObjectSet> prefabs = [.. sets];
        randomGenerator.Shuffle(prefabs);

        int treasureLimit = region.TreasureLimit;

        List<int> openTiles = [];

        foreach (RandomMapNode node in region.Nodes)
        {
            if (node.Type == RandomMapNodeType.Open && node.Index >= 0)
            {
                openTiles.Add(node.Index);
            }
        }

        randomGenerator.Shuffle(openTiles);

        int attempts = 0;

        foreach (int nodeIndex in openTiles)
        {
            if (attempts >= MaxPlacementAttempts || treasureLimit <= 0)
            {
                break;
            }

            ++attempts;

            Point mapPoint = state.GetPoint(nodeIndex);

            foreach (RandomMapObjectSet prefab in prefabs)
            {
                if (!RandomMapObjectRules.CanFitObjectSet(state, prefab, mapPoint, region.GroundType))
                {
                    continue;
                }

                using RandomMapStateTransaction attempt = state.BeginTransaction();

                foreach (RandomMapObjectPlacement obstacle in prefab.Obstacles)
                {
                    MarkPlacement(state, obstacle, mapPoint, region.GroundType);
                }

                List<int> route = RandomMapRoadBuilder.FindPathToNearestRoad(state, region.Id, nodeIndex);

                if (route.Count == 0)
                {
                    // using 结束时撤回这一组留下的标记。
                    continue;
                }

                foreach (int step in route)
                {
                    RandomMapObjectRules.MarkNodeIndexAsType(state, step, RandomMapNodeType.Path);
                }

                foreach (RandomMapObjectPlacement valuable in prefab.Valuables)
                {
                    MarkPlacement(state, valuable, mapPoint, region.GroundType);
                    treasureLimit -= ValueOf(valuable, region.GroundType);
                }

                attempt.Commit();

                planned.Add((nodeIndex, prefab));

                break;
            }
        }

        return planned;
    }

    /// <summary>
    /// Drops treasure groups into a region one by one, <c>placeObjectSet()</c> (<c>:1114-1176</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region being filled; its treasure budget shrinks as groups land.</param>
    /// <param name="sets">The prefabs to choose from.</param>
    /// <param name="monsterStrength">The player's monster strength setting, which decides the guards.</param>
    /// <param name="expectedCount">How many groups the region should get.</param>
    /// <param name="randomGenerator">The generator.</param>
    /// <returns>How many groups were placed.</returns>
    /// <remarks>
    /// 与 <see cref="PlanObjectPlacement"/> 的区别是**它真的落盘**：每个被接受的组先把路铺好
    /// （<see cref="ForceTempRoadOnTile"/>），再让 <see cref="PlaceTreasureGroup"/> 把障碍、奖品与守卫写进地图。
    /// 标记那一段仍然包在事务里，因为「找不到路就整组撤掉」的规矩在这里同样成立。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static int PlaceObjectSet(
        Mp2Map map,
        RandomMapState state,
        RandomMapRegion region,
        IReadOnlyList<RandomMapObjectSet> sets,
        RandomMapMonsterStrength monsterStrength,
        int expectedCount,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        int placed = 0;

        if (region.Nodes.Count == 0 || sets.Count == 0)
        {
            return placed;
        }

        List<RandomMapObjectSet> prefabs = [.. sets];

        for (int attempt = 0; attempt < MaxPlacementAttempts; ++attempt)
        {
            if (placed == expectedCount || region.TreasureLimit <= 0)
            {
                break;
            }

            RandomMapNode node = Rand.GetWithGen(region.Nodes, randomGenerator);

            randomGenerator.Shuffle(prefabs);

            foreach (RandomMapObjectSet prefab in prefabs)
            {
                Point anchor = state.GetPoint(node.Index);

                if (!RandomMapObjectRules.CanFitObjectSet(state, prefab, anchor, region.GroundType))
                {
                    continue;
                }

                using RandomMapStateTransaction marks = state.BeginTransaction();

                foreach (RandomMapObjectPlacement obstacle in prefab.Obstacles)
                {
                    MarkPlacement(state, obstacle, anchor, region.GroundType);
                }

                List<int> route = RandomMapRoadBuilder.FindPathToNearestRoad(state, region.Id, node.Index);

                if (route.Count == 0)
                {
                    continue;
                }

                foreach (int step in route)
                {
                    ForceTempRoadOnTile(map, state, step);
                }

                marks.Commit();

                PlaceTreasureGroup(map, state, region, prefab, anchor, monsterStrength, randomGenerator);

                ++placed;

                break;
            }
        }

        return placed;
    }

    /// <summary>Marks one object of a prefab on the grid, resolving its terrain variant first.</summary>
    /// <param name="state">The grid.</param>
    /// <param name="placement">The object and its offset from the anchor.</param>
    /// <param name="anchor">Anchor tile of the prefab.</param>
    /// <param name="ground">The ground of the region.</param>
    private static void MarkPlacement(
        RandomMapState state, RandomMapObjectPlacement placement, in Point anchor, MapGround ground)
    {
        int objectIndex = RandomMapObjectRules.SelectTerrainVariant(placement.Group, placement.ObjectIndex, ground);

        if (!RandomMapObjectRules.TryGetObjectInfo(placement.Group, objectIndex, out ObjectInfo info))
        {
            return;
        }

        RandomMapObjectRules.MarkObjectPlacement(
            state, info, new Point(anchor.X + placement.Offset.X, anchor.Y + placement.Offset.Y));
    }

    /// <summary>
    /// Places one object that has an entrance and lays the road to it, <c>placeActionObject()</c>
    /// (<c>:797-822</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="position">The tile the object stands on.</param>
    /// <param name="group">Which catalogue.</param>
    /// <param name="objectIndex">Which entry.</param>
    /// <returns><see langword="true"/> when the object and the road were written.</returns>
    /// <remarks>
    /// 与 <see cref="PlaceSimpleObject"/> 的差别：这里要求**先**能修出一条路，才允许把对象写进地图；这类对象自己带入口
    /// （矿、城堡），所以它和路是一起出现的。标记包在事务里，修不出路就整件撤掉。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="state"/> is <see langword="null"/>.</exception>
    public static bool PlaceActionObject(
        Mp2Map map, RandomMapState state, in Point position, ObjectGroup group, int objectIndex)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);

        if (!RandomMapObjectRules.TryGetObjectInfo(group, objectIndex, out ObjectInfo info))
        {
            return false;
        }

        if (!RandomMapObjectRules.CanPlaceObject(state, info, position))
        {
            return false;
        }

        using RandomMapStateTransaction marks = state.BeginTransaction();

        RandomMapObjectRules.MarkObjectPlacement(state, info, position);

        int tileIndex = state.IndexAt(position);
        List<int> route = RandomMapRoadBuilder.FindPathToNearestRoad(
            state, state.GetNode(tileIndex).Region, tileIndex);

        if (route.Count == 0)
        {
            return false;
        }

        if (!RandomMapObjectWriter.TryWrite(map, position, info))
        {
            return false;
        }

        foreach (int step in route)
        {
            ForceTempRoadOnTile(map, state, step);
        }

        marks.Commit();

        return true;
    }

    /// <summary>
    /// The tiles a single object could go to and still get a road, <c>findTilesForPlacement()</c>
    /// (<c>:975-1012</c>).
    /// </summary>
    /// <param name="state">The grid.</param>
    /// <param name="regionId">The region the road has to stay in.</param>
    /// <param name="nodes">The tiles to test, usually the <c>OPEN</c> tiles of the region.</param>
    /// <param name="info">The object.</param>
    /// <returns>The usable tiles, at most <see cref="MaxPlacementAttempts"/> of them.</returns>
    /// <remarks>与 <see cref="PlanObjectPlacement"/> 一样，函数返回时网格恢复原状。</remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static List<int> FindTilesForPlacement(
        RandomMapState state, uint regionId, IReadOnlyList<int> nodes, ObjectInfo info)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(info);

        List<int> options = [];

        if (info.IsEmpty)
        {
            return options;
        }

        using RandomMapStateTransaction revert = state.BeginTransaction();

        foreach (int nodeIndex in nodes)
        {
            Point mapPoint = state.GetPoint(nodeIndex);

            if (!RandomMapObjectRules.CanPlaceObject(state, info, mapPoint))
            {
                continue;
            }

            using RandomMapStateTransaction attempt = state.BeginTransaction();

            RandomMapObjectRules.MarkObjectPlacement(state, info, mapPoint);

            List<int> route = RandomMapRoadBuilder.FindPathToNearestRoad(state, regionId, nodeIndex);

            if (route.Count == 0)
            {
                continue;
            }

            foreach (int step in route)
            {
                RandomMapObjectRules.MarkNodeIndexAsType(state, step, RandomMapNodeType.Path);
            }

            attempt.Commit();

            options.Add(nodeIndex);

            if (options.Count > MaxPlacementAttempts)
            {
                break;
            }
        }

        return options;
    }

    /// <summary>
    /// Places one mine of a resource, <c>placeMine()</c> (<c>:897-912</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="economy">The counter that keeps the resources balanced.</param>
    /// <param name="tileOptions">The tiles to try, in order.</param>
    /// <param name="resource">What the mine produces.</param>
    /// <param name="monsterStrength">The player's monster strength setting, which decides the guard.</param>
    /// <returns>The tile the mine landed on, <c>-1</c> when none of the options worked.</returns>
    /// <remarks>
    /// <para>
    /// 矿的图形按**产出资源与地面**挑（<see cref="RandomMapMines.ObjectInfoIndex"/>：木/铁是锯木厂、四种宝石是矿、
    /// 金是废弃矿、水银是炼金实验室），所以同一座「矿」在雪地上是另一张图。
    /// </para>
    /// <para>
    /// 守卫站在矿的正下方一格（<c>Direction::BOTTOM</c>）：矿要玩家走到跟前才占领，守门的队伍得挡在入口。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static int PlaceMine(
        Mp2Map map,
        RandomMapState state,
        RandomMapEconomy economy,
        IReadOnlyList<int> tileOptions,
        Mp2ResourceType resource,
        RandomMapMonsterStrength monsterStrength)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(tileOptions);

        foreach (int nodeIndex in tileOptions)
        {
            Point point = state.GetPoint(nodeIndex);
            MapGround ground = MapGrounds.FromTerrainImageIndex(map.Tile(point.X, point.Y).TerrainImageIndex);
            int mineType = RandomMapMines.ObjectInfoIndex(resource, ground);

            if (!PlaceActionObject(map, state, point, ObjectGroup.AdventureMines, mineType))
            {
                continue;
            }

            economy.IncreaseMineCount(resource);

            int mineValue = RandomMapObjectValues.GoldValue(RandomMapEconomy.MineObjectType(resource));

            if (point.Y + 1 < state.Height)
            {
                PlaceMonster(
                    map,
                    state,
                    new Point(point.X, point.Y + 1),
                    RandomMapMonsterPools.ByValue(monsterStrength, mineValue));
            }

            return nodeIndex;
        }

        return -1;
    }

    /// <summary>Whether a tile already carries a road, the <c>Maps::doesContainRoad()</c> test.</summary>
    /// <param name="map">The map, needed for the add-on chain.</param>
    /// <param name="tile">The tile to test.</param>
    /// <returns><see langword="true"/> when the tile draws a road on any layer.</returns>
    private static bool HasRoad(Mp2Map map, Mp2TileInfo tile)
    {
        if (tile.GroundIcnType == Mp2ObjectIcnType.Road || tile.TopIcnType == Mp2ObjectIcnType.Road)
        {
            return true;
        }

        foreach (int index in map.EnumerateAddonChain(tile))
        {
            Mp2AddonInfo addon = map.Addons[index];

            if (addon.GroundIcnType == Mp2ObjectIcnType.Road || addon.TopIcnType == Mp2ObjectIcnType.Road)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What one placement is worth, <c>getObjectGoldValue( groupType, objectIndex )</c>.</summary>
    /// <param name="placement">The object, still with the prefab's terrain independent index.</param>
    /// <param name="ground">The ground of the region.</param>
    /// <returns>Its gold value, <c>0</c> for an object the value table does not price.</returns>
    private static int ValueOf(RandomMapObjectPlacement placement, MapGround ground)
    {
        int objectIndex = RandomMapObjectRules.SelectTerrainVariant(placement.Group, placement.ObjectIndex, ground);

        return RandomMapObjectRules.TryGetObjectInfo(placement.Group, objectIndex, out ObjectInfo info)
            ? RandomMapObjectValues.GoldValue(info.ObjectType)
            : 0;
    }

    /// <summary>
    /// The index of a creature inside the <c>MONSTERS</c> group, the reverse of the reference's
    /// <c>Monster( id ).GetSpriteIndex()</c>.
    /// </summary>
    /// <param name="monster">The creature, or one of the four random level placeholders.</param>
    /// <param name="objectIndex">Its entry in <see cref="ObjectGroup.Monsters"/>.</param>
    /// <returns><see langword="true"/> when the catalogue has the creature.</returns>
    /// <remarks>
    /// 物件目录的怪物表按精灵顺序排，每一条把自己的编号写在 <see cref="ObjectInfo.Metadata"/> 的第 0 槽（与参考的
    /// <c>GetSpriteIndex()</c> 是同一个顺序）。四个「随机档位」也是表里的真实条目，所以这里不需要特判。
    /// </remarks>
    private static bool TryMonsterCatalogueIndex(MonsterType monster, out int objectIndex)
    {
        IReadOnlyList<ObjectInfo> objects = MapObjectCatalog.GetObjectsByGroup(ObjectGroup.Monsters);

        for (int index = 0; index < objects.Count; ++index)
        {
            if (objects[index].Metadata[0] == (uint)monster)
            {
                objectIndex = index;

                return true;
            }
        }

        GameLog.Warn($"The random map generator has no sprite for the creature {monster}.");

        objectIndex = -1;

        return false;
    }

}
