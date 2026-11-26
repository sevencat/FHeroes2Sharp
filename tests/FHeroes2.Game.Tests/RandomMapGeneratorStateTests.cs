using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The grid the random map generator lays its regions on and the region itself,
/// <c>Maps::Random_Generator::MapStateManager</c> and <c>Region</c>
/// (<c>map_random_generator_info.cpp:43-236</c>).
/// </summary>
public sealed class RandomMapGeneratorStateTests
{
    private const int Size = 16;

    [Fact]
    public void ANewGridIsOpenAndNumbered()
    {
        RandomMapState state = new(Size, Size);

        Assert.Equal(Size * Size, state.Count);

        for (int index = 0; index < state.Count; ++index)
        {
            RandomMapNode node = state.GetNode(index);

            Assert.Equal(index, node.Index);
            Assert.Equal(0u, node.Region);
            Assert.Equal(RandomMapNodeType.Open, node.Type);
        }
    }

    [Fact]
    public void PointsAndIndexesRoundTrip()
    {
        RandomMapState state = new(Size, Size);

        Assert.Equal(new Point(5, 0), state.GetPoint(5));
        Assert.Equal(new Point(5, 2), state.GetPoint((2 * Size) + 5));
        Assert.Equal((2 * Size) + 5, state.IndexAt(new Point(5, 2)));
        Assert.Equal(-1, state.IndexAt(new Point(-1, 3)));
        Assert.False(state.IsValidPoint(new Point(Size, 0)));
        Assert.False(state.IsValidPoint(new Point(0, -1)));
        Assert.True(state.IsValidPoint(new Point(Size - 1, Size - 1)));
    }

    [Fact]
    public void OutOfBoundsReadsComeBackAsTheBorderSentinel()
    {
        RandomMapState state = new(Size, Size);

        Assert.Equal(-1, state.GetNode(-1).Index);
        Assert.Equal(RandomMapNodeType.Border, state.GetNode(-1).Type);
        Assert.Equal(-1, state.GetNode(state.Count).Index);
        Assert.Equal(-1, state.GetNodeToUpdate(new Point(0, -1)).Index);
    }

    [Fact]
    public void ApproximateDistanceIsTheOctileDistance()
    {
        RandomMapState state = new(Size, Size);

        // (0,0) to (3,4): max 4 + min 3 / 2 = 5.
        Assert.Equal(5u, state.ApproximateDistance(0, (4 * Size) + 3));

        // (1,2) to (4,6): the same shape, one tile in.
        Assert.Equal(5u, state.ApproximateDistance((2 * Size) + 1, (6 * Size) + 4));

        // A straight line has no half steps.
        Assert.Equal(4u, state.ApproximateDistance(0, 4));
    }

    [Fact]
    public void ARolledBackTransactionPutsTheNodesBack()
    {
        RandomMapState state = new(Size, Size);

        using (state.BeginTransaction())
        {
            state.GetNodeToUpdate(10).Region = 3;
            state.GetNodeToUpdate(11).Type = RandomMapNodeType.Obstacle;
        }

        Assert.Equal(0u, state.GetNode(10).Region);
        Assert.Equal(RandomMapNodeType.Open, state.GetNode(11).Type);
    }

    [Fact]
    public void ACommittedTransactionKeepsTheChanges()
    {
        RandomMapState state = new(Size, Size);

        using (RandomMapStateTransaction transaction = state.BeginTransaction())
        {
            state.GetNodeToUpdate(10).Region = 3;
            transaction.Commit();
        }

        state.GetNodeToUpdate(12).Region = 4;

        using (state.BeginTransaction())
        {
            state.GetNodeToUpdate(13).Region = 5;
        }

        Assert.Equal(3u, state.GetNode(10).Region);
        Assert.Equal(4u, state.GetNode(12).Region);
        Assert.Equal(0u, state.GetNode(13).Region);
    }

    [Fact]
    public void ANestedTransactionRollsBackOnlyItsOwnChanges()
    {
        RandomMapState state = new(Size, Size);

        using (RandomMapStateTransaction outer = state.BeginTransaction())
        {
            state.GetNodeToUpdate(20).Region = 7;

            using (state.BeginTransaction())
            {
                state.GetNodeToUpdate(21).Region = 8;
            }

            Assert.Equal(7u, state.GetNode(20).Region);
            Assert.Equal(0u, state.GetNode(21).Region);

            outer.Commit();
        }

        Assert.Equal(7u, state.GetNode(20).Region);
    }

    [Fact]
    public void ARollbackReachesChangesMadeThroughThePointOverload()
    {
        RandomMapState state = new(Size, Size);

        using (state.BeginTransaction())
        {
            state.GetNodeToUpdate(new Point(3, 3)).Region = 9;
        }

        Assert.Equal(0u, state.GetNode(new Point(3, 3)).Region);
    }
}

/// <summary>
/// The region of the random map generator, <c>Maps::Random_Generator::Region</c>
/// (<c>map_random_generator_info.cpp:108-236</c>), and its mine economy.
/// </summary>
public sealed class RandomMapRegionTests
{
    private const int Size = 24;
    private const byte RegionId = 1;

    [Fact]
    public void ARegionTakesItsCentreNode()
    {
        RandomMapState state = new(Size, Size);
        RandomMapNode centre = state.GetNode(new Point(12, 12));

        RandomMapRegion region = Create(state, centre, size: 40);

        Assert.Equal(1u, centre.Region);
        Assert.Equal(centre.Index, region.CenterIndex);
        Assert.Same(centre, Assert.Single(region.Nodes));
        Assert.Equal(40, region.SizeLimit);
        Assert.Empty(region.Neighbours);
        Assert.Empty(region.Connections);
    }

    [Fact]
    public void ASizeThatIsNotPositiveIsRejected()
    {
        RandomMapState state = new(Size, Size);

        Assert.Throws<ArgumentOutOfRangeException>(() => Create(state, state.GetNode(0), size: 0));
    }

    [Fact]
    public void TheExpansionGrowsTheRegionOutwards()
    {
        RandomMapState state = new(Size, Size);
        RandomMapRegion region = Create(state, state.GetNode(new Point(12, 12)), size: 40);
        Pcg32 random = new(1234);

        Expand(region, state, random);

        Assert.True(region.Nodes.Count > 1, $"the region stayed at {region.Nodes.Count} node(s)");
        Assert.True(region.Nodes.Count <= region.SizeLimit + 8, $"{region.Nodes.Count} nodes for a limit of {region.SizeLimit}");
        Assert.All(region.Nodes, node => Assert.Equal(RegionId, node.Region));

        // 半径由 sizeLimit 推出来：sqrt( 40 / pi ) * 1.85 ≈ 6.6，取整后没有节点能超过 7 格。
        uint limit = (uint)(Math.Sqrt(region.SizeLimit / Math.PI) * 1.85);

        Assert.All(region.Nodes, node => Assert.True(
            state.ApproximateDistance(region.CenterIndex, node.Index) <= limit + 1,
            $"node {node.Index} is {state.ApproximateDistance(region.CenterIndex, node.Index)} tiles away"));
    }

    [Fact]
    public void ATileOfAnotherRegionIsANeighbourAndTurnsIntoABorder()
    {
        RandomMapState state = new(Size, Size);
        state.GetNodeToUpdate(new Point(13, 12)).Region = 2;

        RandomMapRegion region = Create(state, state.GetNode(new Point(12, 12)), size: 30);

        Expand(region, state, new Pcg32(7));

        Assert.Contains(2u, region.Neighbours);
        Assert.Contains(region.Nodes, node => node.Type == RandomMapNodeType.Border);
    }

    [Fact]
    public void TheConnectionTurnsBorderNodesIntoARoadEnd()
    {
        RandomMapState state = new(Size, Size);
        (RandomMapRegion first, RandomMapRegion second, RandomMapNode border) = TwoAdjacentRegions(state, MapGround.Grass);

        List<RandomMapRegion> regions =
        [
            new RandomMapRegion(0, state.GetNode(0), 0, MapGround.Water, 1, 0, RandomMapRegionType.Neutral),
            first,
            second,
        ];

        Assert.True(first.CheckNodeForConnections(state, regions, border));

        int join = state.IndexAt(new Point(7, 8));

        Assert.Equal(RandomMapNodeType.Connector, border.Type);
        Assert.Equal(join, first.Connections[2]);
        Assert.Equal(join, second.Connections[1]);
        Assert.Equal(RandomMapNodeType.Connector, state.GetNode(new Point(8, 8)).Type);
        Assert.Equal(RandomMapNodeType.Connector, state.GetNode(new Point(9, 8)).Type);
        Assert.Equal(RandomMapNodeType.Connector, state.GetNode(new Point(6, 8)).Type);
    }

    [Fact]
    public void AWaterNeighbourMakesTheBorderACoast()
    {
        RandomMapState state = new(Size, Size);
        (RandomMapRegion first, RandomMapRegion second, RandomMapNode border) = TwoAdjacentRegions(state, MapGround.Water);

        List<RandomMapRegion> regions =
        [
            new RandomMapRegion(0, state.GetNode(0), 0, MapGround.Grass, 1, 0, RandomMapRegionType.Neutral),
            first,
            second,
        ];

        Assert.False(first.CheckNodeForConnections(state, regions, border));
        Assert.Equal(RandomMapNodeType.Coast, border.Type);
        Assert.Empty(first.Connections);
    }

    [Fact]
    public void ANonBorderNodeIsLeftAlone()
    {
        RandomMapState state = new(Size, Size);
        (RandomMapRegion first, RandomMapRegion second, RandomMapNode border) = TwoAdjacentRegions(state, MapGround.Grass);

        border.Type = RandomMapNodeType.Open;

        List<RandomMapRegion> regions =
        [
            new RandomMapRegion(0, state.GetNode(0), 0, MapGround.Water, 1, 0, RandomMapRegionType.Neutral),
            first,
            second,
        ];

        Assert.False(first.CheckNodeForConnections(state, regions, border));
        Assert.Empty(first.Connections);
    }

    [Fact]
    public void CastleAdjustmentKeepsFourTilesFromTheEdge()
    {
        RandomMapState state = new(16, 16);
        RandomMapRegion corner = Create(state, state.GetNode(new Point(1, 1)), size: 10);

        Assert.Equal(new Point(4, 4), corner.AdjustRegionToFitCastle(state));
        Assert.Equal(state.IndexAt(new Point(4, 6)), corner.CenterIndex);

        RandomMapRegion far = new(2, state.GetNode(new Point(14, 14)), 0, MapGround.Grass, 10, 0, RandomMapRegionType.Starting);

        Assert.Equal(new Point(12, 12), far.AdjustRegionToFitCastle(state));
        Assert.Equal(state.IndexAt(new Point(12, 14)), far.CenterIndex);
    }

    private static RandomMapRegion Create(RandomMapState state, RandomMapNode centre, int size) =>
        new(RegionId, centre, playerColor: 0, MapGround.Grass, size, treasure: 0, RandomMapRegionType.Starting);

    private static void Expand(RandomMapRegion region, RandomMapState state, Pcg32 random)
    {
        // 参考没有轮数上限：区域长到 sizeLimit 之后每一轮都不再添加，循环自然结束。这里加一道护栏防死循环。
        for (int round = 0; round < 500 && region.RegionExpansion(state, random); ++round)
        {
        }
    }

    /// <summary>
    /// Builds two three-by-three regions that face each other across the border tile <c>(7,8)</c>, the layout
    /// <see cref="RandomMapRegion.CheckNodeForConnections"/> needs: one other region to the east, the rest own tiles.
    /// </summary>
    /// <param name="state">The grid to paint.</param>
    /// <param name="secondGround">Ground of the region to the east, water makes a coast instead of a road.</param>
    /// <returns>The two regions and the border tile between them.</returns>
    private static (RandomMapRegion First, RandomMapRegion Second, RandomMapNode Border) TwoAdjacentRegions(
        RandomMapState state, MapGround secondGround)
    {
        for (int y = 7; y <= 9; ++y)
        {
            for (int x = 4; x <= 6; ++x)
            {
                state.GetNodeToUpdate(new Point(x, y)).Region = 1;
            }

            for (int x = 8; x <= 10; ++x)
            {
                state.GetNodeToUpdate(new Point(x, y)).Region = 2;
            }
        }

        state.GetNodeToUpdate(new Point(7, 7)).Region = 1;
        state.GetNodeToUpdate(new Point(7, 9)).Region = 1;

        RandomMapNode border = state.GetNodeToUpdate(new Point(7, 8));
        border.Region = 1;
        border.Type = RandomMapNodeType.Border;

        RandomMapRegion first = new(1, state.GetNode(new Point(5, 8)), 0, MapGround.Grass, 9, 0, RandomMapRegionType.Starting);
        RandomMapRegion second = new(2, state.GetNode(new Point(9, 8)), 0, secondGround, 9, 0, RandomMapRegionType.Expansion);

        return (first, second, border);
    }
}

/// <summary>
/// The mine economy of the random map generator, <c>Maps::Random_Generator::MapEconomy</c>
/// (<c>map_random_generator_info.cpp:91-106</c>).
/// </summary>
public sealed class RandomMapEconomyTests
{
    [Fact]
    public void TheNextMineIsTheLeastUsedOfTheSixThatRotate()
    {
        RandomMapEconomy economy = new();

        Assert.Equal(Mp2ResourceType.Wood, economy.PickNextMineResource());

        economy.IncreaseMineCount(Mp2ResourceType.Wood);

        Assert.Equal(Mp2ResourceType.Ore, economy.PickNextMineResource());

        economy.IncreaseMineCount(Mp2ResourceType.Ore);

        Assert.Equal(Mp2ResourceType.Crystal, economy.PickNextMineResource());

        economy.IncreaseMineCount(Mp2ResourceType.Crystal);

        Assert.Equal(Mp2ResourceType.Sulfur, economy.PickNextMineResource());

        economy.IncreaseMineCount(Mp2ResourceType.Sulfur);

        Assert.Equal(Mp2ResourceType.Gems, economy.PickNextMineResource());

        economy.IncreaseMineCount(Mp2ResourceType.Gems);

        Assert.Equal(Mp2ResourceType.Mercury, economy.PickNextMineResource());

        economy.IncreaseMineCount(Mp2ResourceType.Mercury);

        // 六种都摆过一座之后回到第一个。
        Assert.Equal(Mp2ResourceType.Wood, economy.PickNextMineResource());
    }

    [Fact]
    public void GoldIsCountedButNeverPicked()
    {
        RandomMapEconomy economy = new();

        for (int mine = 0; mine < 12; ++mine)
        {
            Mp2ResourceType resource = economy.PickNextMineResource();

            Assert.NotEqual(Mp2ResourceType.Gold, resource);

            economy.IncreaseMineCount(resource);
        }

        economy.IncreaseMineCount(Mp2ResourceType.Gold);

        Assert.Equal(1u, economy.MineCount(Mp2ResourceType.Gold));
        Assert.NotEqual(Mp2ResourceType.Gold, economy.PickNextMineResource());
    }

    [Fact]
    public void AResourceTheEconomyDoesNotCountIsRejected()
    {
        RandomMapEconomy economy = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => economy.IncreaseMineCount(Mp2ResourceType.None));
        Assert.Equal(0u, economy.MineCount(Mp2ResourceType.None));
    }
}
