using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The tile lists the object phase picks from, <c>findTilesByType()</c>, <c>findOpenTilesSortedJittered()</c>
/// and <c>pickEvenlySpacedTiles()</c> (<c>map_random_generator_helper.cpp:539-658</c>).
/// </summary>
public sealed class RandomMapTileSelectionTests
{
    private const int Size = 36;

    [Fact]
    public void FindTilesByTypeKeepsTheRegionsOrder()
    {
        RandomMapState state = new(Size, Size);
        RandomMapRegion region = RegionAround(state, new Point(10, 10));

        Assert.Equal([state.IndexAt(new Point(10, 10))], RandomMapTileSelection.FindTilesByType(region, RandomMapNodeType.Path));
        Assert.Equal(8, RandomMapTileSelection.FindTilesByType(region, RandomMapNodeType.Open).Count);
    }

    [Fact]
    public void EvenlySpacedPicksStartWithTheFarthest()
    {
        RandomMapState state = new(Size, Size);
        List<int> candidates = [10, 11, 12, 20, 30];
        List<int> picks = RandomMapTileSelection.PickEvenlySpacedTiles(state, candidates, 2, [10]);

        // 离被避开的 10 最远的是 30，其次是 11（剩下的里离 30 与 10 都远的那一个）。
        Assert.Equal(30, picks[0]);
        Assert.Equal(2, picks.Count);
        Assert.DoesNotContain(10, picks);
    }

    [Fact]
    public void AskingForMoreThanThereIsReturnsEverything()
    {
        RandomMapState state = new(Size, Size);
        List<int> picks = RandomMapTileSelection.PickEvenlySpacedTiles(state, [1, 2, 3], 10, []);

        Assert.Equal(3, picks.Count);
    }

    [Fact]
    public void TheRingsHoldEveryOpenTileExactlyOnce()
    {
        RandomMapState state = new(Size, Size);
        RandomMapRegion region = RegionAround(state, new Point(10, 10));

        List<List<int>> rings = RandomMapTileSelection.FindOpenTilesSortedJittered(region, Size, new Pcg32(seed: 2026));

        List<int> flat = [.. rings.SelectMany(ring => ring)];
        List<int> expected = RandomMapTileSelection.FindTilesByType(region, RandomMapNodeType.Open);

        Assert.Equal(expected.Count, flat.Count);
        Assert.Equal(expected.Order(), flat.Order());
    }

    [Fact]
    public void TheJitterKeepsATileWithinThreeRingsOfItsDistance()
    {
        RandomMapState state = new(Size, Size);
        Point centre = new(10, 10);
        Point near = new(14, 10);
        RandomMapRegion region = new(1, state.GetNode(centre), 0, MapGround.Grass, 9, 0, RandomMapRegionType.Neutral);

        region.Nodes.Add(state.GetNode(near));

        List<List<int>> rings = RandomMapTileSelection.FindOpenTilesSortedJittered(region, Size, new Pcg32(seed: 7));
        int nearRing = rings.FindIndex(ring => ring.Contains(state.IndexAt(near)));

        // 距离 4 的格子加 0…2 的抖动，落在第 4 到第 6 环。
        Assert.InRange(nearRing, 4, 6);
    }

    /// <summary>A region of nine tiles around a centre, one of them a road.</summary>
    private static RandomMapRegion RegionAround(RandomMapState state, Point centre)
    {
        RandomMapRegion region = new(1, state.GetNode(centre), 0, MapGround.Grass, 9, 0, RandomMapRegionType.Neutral);

        for (int y = centre.Y - 1; y <= centre.Y + 1; ++y)
        {
            for (int x = centre.X - 1; x <= centre.X + 1; ++x)
            {
                RandomMapNode node = state.GetNode(new Point(x, y));

                if (node != region.Nodes[0])
                {
                    region.Nodes.Add(node);
                }
            }
        }

        region.Nodes[0].Type = RandomMapNodeType.Path;

        return region;
    }
}
