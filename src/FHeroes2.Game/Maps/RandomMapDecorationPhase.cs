using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The two decoration passes of <c>generateMap()</c>: the trees on a region's edge (step 6) and the scenes that fill
/// its empty corners (step 9) — <c>placeBorderObstacle()</c> and <c>placeDecorations()</c>
/// (<c>map_random_generator_helper.cpp:914-934</c>, <c>:1178-1235</c>).
/// </summary>
/// <remarks>
/// <para>
/// 两遍都不需要修路，也不占 <c>ACTION</c>：装饰物只是让地图形状好看，落不落得下都不影响可玩性。所以它们排在最后，
/// 前面的矿、宝物、路都已经占完格子，剩下的空地才是它们的位置。
/// </para>
/// <para>
/// <b>边框树</b>与区域中心的地形装饰不是一回事：前者只长在 <c>BORDER</c> 格上，用 <c>canPlaceBorderObstacle</c>
/// （允许压在已有障碍上，因为树本来就成丛）；后者要一份完整的「景」，用 <c>canPlaceAllObjects</c>，整组要么全放下要么不放。
/// </para>
/// </remarks>
public static class RandomMapDecorationPhase
{
    /// <summary>
    /// How many empty tiles one decoration scene may claim, the <c>300</c> divisor of <c>:1202</c>.
    /// </summary>
    public const int TilesPerDecorationCopy = 300;

    /// <summary>How many extra scenes the picker asks for, the <c>3</c> of <c>:1205</c>.</summary>
    public const int DecorationAttemptFactor = 3;

    /// <summary>
    /// Drops trees on the edge of a region, <c>placeBorderObstacle()</c> (<c>:914-934</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="nodeIndex">The <c>BORDER</c> tile to decorate.</param>
    /// <param name="ground">The ground of the region, which decides the trees.</param>
    /// <param name="randomGenerator">The generator, used to shuffle the six kinds of tree.</param>
    /// <returns><see langword="true"/> when a tree landed.</returns>
    /// <remarks>
    /// 六种树打乱后逐个试，第一个放得下的就用——参考只放一棵，边框因此疏密不均，看起来才自然。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="map"/>, <paramref name="state"/> or <paramref name="randomGenerator"/> is <see langword="null"/>.</exception>
    public static bool PlaceBorderObstacle(
        Mp2Map map, RandomMapState state, int nodeIndex, MapGround ground, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        List<int> obstacles = [.. RandomMapDecorations.ObstacleIndicesFor(ground)];

        if (obstacles.Count == 0)
        {
            return false;
        }

        randomGenerator.Shuffle(obstacles);

        Point position = state.GetPoint(nodeIndex);

        foreach (int obstacleId in obstacles)
        {
            if (!RandomMapObjectRules.TryGetObjectInfo(ObjectGroup.LandscapeTrees, obstacleId, out ObjectInfo info))
            {
                continue;
            }

            if (!RandomMapObjectRules.CanPlaceBorderObstacle(state, info, position))
            {
                continue;
            }

            if (!RandomMapObjectWriter.TryWrite(map, position, info))
            {
                continue;
            }

            RandomMapObjectRules.MarkObjectPlacement(state, info, position);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Fills the empty corners of a region with decorative scenes, <c>placeDecorations()</c> (<c>:1178-1235</c>).
    /// </summary>
    /// <param name="map">The map to write into.</param>
    /// <param name="state">The grid.</param>
    /// <param name="region">The region to decorate; its <see cref="RandomMapRegion.GroundType"/> picks tree and mountain variants.</param>
    /// <param name="decorations">The scenes to choose from, usually <see cref="RandomMapDecorations.Common"/> plus the ground's own.</param>
    /// <param name="randomGenerator">The generator, used for the tile picks and the coin tosses.</param>
    /// <returns>How many scenes were placed.</returns>
    /// <remarks>
    /// <para>
    /// <b>只挑挨着东西的空地</b>：四邻全是 <c>OPEN</c> 的格子留着当通路，装饰物都贴在已经落过子的地方周围，
    /// 于是大片的空地上不会忽然冒出一座孤山。
    /// </para>
    /// <para>
    /// <b>每种景有配额</b>：每 300 格空地给每个景一份（至少一份），所以小区域里每种景只出现一次，大区域里会重复几次。
    /// 参考先用 <c>pickEvenlySpacedTiles</c> 把候选格散开，再逐格随机挑一种景——散开保证了同一片地方不会挤满同一种。
    /// </para>
    /// <para>
    /// 附加件（<see cref="RandomMapDecorationSet.Optional"/>）在主体落下之后才掷硬币，所以同一组景每次长得略不一样。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static int PlaceDecorations(
        Mp2Map map,
        RandomMapState state,
        RandomMapRegion region,
        IReadOnlyList<RandomMapDecorationSet> decorations,
        Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(decorations);
        ArgumentNullException.ThrowIfNull(randomGenerator);

        if (decorations.Count == 0)
        {
            return 0;
        }

        List<int> openTiles = RandomMapTileSelection.FindTilesByType(region, RandomMapNodeType.Open);

        openTiles.RemoveAll(index => !TouchesSomethingElse(state, index));

        if (openTiles.Count == 0)
        {
            return 0;
        }

        int copies = Math.Max(1, openTiles.Count / TilesPerDecorationCopy);

        int[] objectCount = new int[decorations.Count];
        Array.Fill(objectCount, copies);

        int possibilities = decorations.Count * copies * DecorationAttemptFactor;

        List<int> tileIndices = RandomMapTileSelection.PickEvenlySpacedTiles(state, openTiles, possibilities, []);

        int placed = 0;

        foreach (int tileIndex in tileIndices)
        {
            int setIndex = randomGenerator.NextInclusive(0, decorations.Count - 1);

            if (objectCount[setIndex] <= 0)
            {
                continue;
            }

            RandomMapDecorationSet scene = decorations[setIndex];
            Point position = state.GetPoint(tileIndex);

            if (!RandomMapObjectRules.CanPlaceAllObjects(state, scene.Obstacles, position, region.GroundType))
            {
                continue;
            }

            foreach (RandomMapObjectPlacement obstacle in scene.Obstacles)
            {
                RandomMapObjectPhase.PlaceSimpleObject(map, state, position, obstacle, region.GroundType);
            }

            foreach (RandomMapObjectPlacement optional in scene.Optional)
            {
                int objectIndex = RandomMapObjectRules.SelectTerrainVariant(
                    optional.Group, optional.ObjectIndex, region.GroundType);

                if (!RandomMapObjectRules.TryGetObjectInfo(optional.Group, objectIndex, out ObjectInfo info))
                {
                    continue;
                }

                Point optionalPosition = new(position.X + optional.Offset.X, position.Y + optional.Offset.Y);

                if (randomGenerator.NextInclusive(0, 1) == 0
                    || !RandomMapObjectRules.CanPlaceBorderObstacle(state, info, optionalPosition))
                {
                    continue;
                }

                RandomMapObjectPhase.PlaceSimpleObject(map, state, position, optional, region.GroundType);
            }

            --objectCount[setIndex];
            ++placed;
        }

        return placed;
    }

    /// <summary>Whether a tile has a neighbour that is not empty, the <c>openSpaceFilter</c> of <c>:1187-1196</c>.</summary>
    /// <param name="state">The grid.</param>
    /// <param name="index">The tile to test.</param>
    /// <returns><see langword="true"/> when at least one of the eight neighbours is taken.</returns>
    private static bool TouchesSomethingElse(RandomMapState state, int index)
    {
        Point point = state.GetPoint(index);

        foreach (Point offset in RandomMapState.DirectionOffsets)
        {
            RandomMapNode neighbour = state.GetNode(new Point(point.X + offset.X, point.Y + offset.Y));

            if (neighbour.Index != -1 && neighbour.Type != RandomMapNodeType.Open)
            {
                return true;
            }
        }

        return false;
    }
}
