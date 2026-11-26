using FHeroes2.Core.Random;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The three list helpers the object phase picks tiles with: by node type, evenly spaced, and grouped in
/// rings around the region centre (<c>map_random_generator_helper.cpp:539-658</c>).
/// </summary>
public static class RandomMapTileSelection
{
    /// <summary>The tiles of a region that have a given type, <c>findTilesByType()</c> (<c>:578-587</c>).</summary>
    /// <param name="region">The region.</param>
    /// <param name="type">The node type to collect.</param>
    /// <returns>The indices, in the order the region took them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="region"/> is <see langword="null"/>.</exception>
    public static List<int> FindTilesByType(RandomMapRegion region, RandomMapNodeType type)
    {
        ArgumentNullException.ThrowIfNull(region);

        List<int> result = [];

        foreach (RandomMapNode node in region.Nodes)
        {
            if (node.Type == type)
            {
                result.Add(node.Index);
            }
        }

        return result;
    }

    /// <summary>
    /// The open tiles of a region, grouped in rings around its centre, <c>findOpenTilesSortedJittered()</c>
    /// (<c>:539-576</c>).
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="mapWidth">Width of the map, to turn an index into a column and a row.</param>
    /// <param name="randomGenerator">The generator, used for the jitter and the shuffles.</param>
    /// <returns>The rings, from the centre outwards; every ring is shuffled.</returns>
    /// <remarks>
    /// 「抖动」是给每个格子加一个 0…2 的随机量再分环：同一环里的格子因此不完全等距，环与环之间也会互相渗透，
    /// 铺出来的东西看起来不像同心圆。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="region"/> or <paramref name="randomGenerator"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mapWidth"/> is not positive.</exception>
    public static List<List<int>> FindOpenTilesSortedJittered(
        RandomMapRegion region, int mapWidth, Pcg32 randomGenerator)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(randomGenerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);

        List<List<int>> rings = [];

        if (region.CenterIndex < 0 || region.Nodes.Count == 0)
        {
            return rings;
        }

        int centerX = region.CenterIndex % mapWidth;
        int centerY = region.CenterIndex / mapWidth;

        foreach (RandomMapNode node in region.Nodes)
        {
            if (node.Type != RandomMapNodeType.Open || node.Index < 0)
            {
                continue;
            }

            int dx = (node.Index % mapWidth) - centerX;
            int dy = (node.Index / mapWidth) - centerY;
            double distance = Math.Sqrt((double)(dx * dx) + (dy * dy));
            int ring = (int)Math.Floor(distance) + (int)randomGenerator.NextInclusive(0, 2);

            while (ring >= rings.Count)
            {
                rings.Add([]);
            }

            rings[ring].Add(node.Index);
        }

        foreach (List<int> bucket in rings)
        {
            if (bucket.Count > 0)
            {
                randomGenerator.Shuffle(bucket);
            }
        }

        return rings;
    }

    /// <summary>
    /// <paramref name="pickCount"/> tiles from a list, as far apart as possible, <c>pickEvenlySpacedTiles()</c>
    /// (<c>:589-658</c>).
    /// </summary>
    /// <param name="state">The grid, needed to measure the distance between two tiles.</param>
    /// <param name="candidates">The tiles to choose from.</param>
    /// <param name="pickCount">How many to choose.</param>
    /// <param name="avoidance">Tiles the picks should stay away from, typically the region centre or the primary mines.</param>
    /// <returns>The chosen tiles; fewer than asked for when the list runs out.</returns>
    /// <remarks>
    /// 贪心：先算每个候选格「离被避开那些格子的最近距离」，每次挑剩下的里最远的那一个，再按新挑中的格子刷新其余格子的
    /// 距离。这样同一片地方不会挤在一起，矿与宝物因此散得开。距离用 <see cref="RandomMapState.ApproximateDistance"/>，
    /// 与参考的 <c>Maps::GetApproximateDistance</c> 同一个公式。
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static List<int> PickEvenlySpacedTiles(
        RandomMapState state, IReadOnlyList<int> candidates, int pickCount, IReadOnlyList<int> avoidance)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(avoidance);

        List<int> result = [];

        if (candidates.Count == 0 || pickCount <= 0)
        {
            return result;
        }

        int targetCount = Math.Min(pickCount, candidates.Count);

        (uint Distance, bool Used)[] cache = new (uint, bool)[candidates.Count];

        for (int index = 0; index < candidates.Count; ++index)
        {
            uint minDistance = uint.MaxValue;

            foreach (int avoid in avoidance)
            {
                uint distance = state.ApproximateDistance(candidates[index], avoid);

                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            cache[index] = (minDistance, false);
        }

        for (int placed = 0; placed < targetCount; ++placed)
        {
            int bestIndex = -1;
            uint bestDistance = 0;

            for (int index = 0; index < cache.Length; ++index)
            {
                if (cache[index].Used)
                {
                    continue;
                }

                if (cache[index].Distance > bestDistance)
                {
                    bestDistance = cache[index].Distance;
                    bestIndex = index;
                }
            }

            if (bestIndex < 0)
            {
                break;
            }

            int chosen = candidates[bestIndex];

            cache[bestIndex] = (cache[bestIndex].Distance, true);
            result.Add(chosen);

            for (int index = 0; index < cache.Length; ++index)
            {
                if (cache[index].Used)
                {
                    continue;
                }

                uint distance = state.ApproximateDistance(candidates[index], chosen);

                if (distance < cache[index].Distance)
                {
                    cache[index] = (distance, false);
                }
            }
        }

        return result;
    }
}

