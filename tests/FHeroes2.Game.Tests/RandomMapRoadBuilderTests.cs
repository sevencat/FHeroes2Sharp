using FHeroes2.Core.Geometry;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The road search of the object phase, <c>findPathToNearestRoad()</c>
/// (<c>map_random_generator_helper.cpp:420-537</c>).
/// </summary>
public sealed class RandomMapRoadBuilderTests
{
    private const int Size = 36;

    [Fact]
    public void AStraightRouteReachesTheRoad()
    {
        RandomMapState state = Row(10, 5, 15);
        int road = state.IndexAt(new Point(5, 10));
        int start = state.IndexAt(new Point(15, 10));

        state.GetNodeToUpdate(road).Type = RandomMapNodeType.Path;

        List<int> route = RandomMapRoadBuilder.FindPathToNearestRoad(state, 1, start);

        Assert.Equal(11, route.Count);
        Assert.Equal(road, route[0]);
        Assert.Equal(start, route[^1]);
    }

    [Fact]
    public void ARegionWithoutARoadHasNoRoute()
    {
        RandomMapState state = Row(10, 5, 15);

        Assert.Empty(RandomMapRoadBuilder.FindPathToNearestRoad(state, 1, state.IndexAt(new Point(15, 10))));
    }

    [Fact]
    public void TheRoadOfAnotherRegionIsIgnored()
    {
        RandomMapState state = Row(10, 5, 15);
        int road = state.IndexAt(new Point(5, 10));

        state.GetNodeToUpdate(road).Type = RandomMapNodeType.Path;
        state.GetNodeToUpdate(road).Region = 2;

        Assert.Empty(RandomMapRoadBuilder.FindPathToNearestRoad(state, 1, state.IndexAt(new Point(15, 10))));
    }

    [Fact]
    public void AnObstacleCutsTheRoute()
    {
        RandomMapState state = Row(10, 5, 15);

        state.GetNodeToUpdate(new Point(5, 10)).Type = RandomMapNodeType.Path;
        state.GetNodeToUpdate(new Point(10, 10)).Type = RandomMapNodeType.Obstacle;

        Assert.Empty(RandomMapRoadBuilder.FindPathToNearestRoad(state, 1, state.IndexAt(new Point(15, 10))));
    }

    [Fact]
    public void ARouteOutOfAnObjectTileStartsStraight()
    {
        RandomMapState state = Row(9, 5, 15);

        Row(state, 10, 5, 15);
        Row(state, 11, 5, 15);

        int start = state.IndexAt(new Point(15, 10));
        int road = state.IndexAt(new Point(12, 9));

        state.GetNodeToUpdate(start).Type = RandomMapNodeType.Action;
        state.GetNodeToUpdate(road).Type = RandomMapNodeType.Path;

        List<int> route = RandomMapRoadBuilder.FindPathToNearestRoad(state, 1, start);

        // 入口朝下：从可交互物件出发的第一步不许朝上、也不许斜走。
        Assert.NotEmpty(route);

        Point first = state.GetPoint(route[^2]);

        Assert.True(first.X == 15 || first.Y == 10, $"first step {first} is diagonal");
        Assert.True(first.Y >= 10, $"first step {first} goes up");
    }

    [Fact]
    public void TheCostsAreTheOnesOfTheReference()
    {
        Assert.Equal(300U, RandomMapRoadBuilder.Margin);
        Assert.Equal(150U, RandomMapRoadBuilder.DiagonalPenalty);
    }

    /// <summary>A grid whose row <paramref name="y"/> between the two columns belongs to region 1.</summary>
    private static RandomMapState Row(int y, int fromX, int toX)
    {
        RandomMapState state = new(Size, Size);

        Row(state, y, fromX, toX);

        return state;
    }

    private static void Row(RandomMapState state, int y, int fromX, int toX)
    {
        for (int x = fromX; x <= toX; ++x)
        {
            state.GetNodeToUpdate(new Point(x, y)).Region = 1;
        }
    }
}
