using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the hero's route, <c>Route::Path</c> (<c>route.h:120-215</c>, <c>route.cpp:321-414</c>).
/// </summary>
public sealed class HeroPathTests
{
    private const int MapSize = 100;

    [Fact]
    public void ANewRouteIsEmptyAndHidden()
    {
        HeroPath path = new();

        Assert.True(path.IsEmpty);
        Assert.Equal(0, path.Count);
        Assert.False(path.IsShow);
    }

    [Fact]
    public void AnEmptyRouteHasNoFrontAndNoDestination()
    {
        HeroPath path = new();

        Assert.Equal(-1, path.FrontIndex);
        Assert.Equal(-1, path.FrontFrom);
        Assert.Equal(-1, path.DestinationIndex);
        Assert.Equal(Direction.Unknown, path.FrontDirection);
        Assert.Equal(Direction.Unknown, path.NextStepDirection);
        Assert.Equal(0u, path.FrontPenalty);
        Assert.Empty(path.Steps);
    }

    [Fact]
    public void SetPathReplacesWhateverWasThere()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.BottomRight, 150), Step(2, 1, Direction.BottomRight, 150)]);
        Assert.Equal(2, path.Count);

        path.SetPath([Step(1, 0, Direction.BottomRight, 150)]);
        Assert.Equal(1, path.Count);
        Assert.Equal(1, path.DestinationIndex);
    }

    [Fact]
    public void ShowAndHideFollowTheFlag()
    {
        HeroPath path = new();

        path.Show();
        Assert.True(path.IsShow);

        path.Hide();
        Assert.False(path.IsShow);
    }

    [Fact]
    public void TheFrontIsTheFirstStepAndTheDestinationTheLast()
    {
        HeroPath path = new();

        path.SetPath([Step(5, 0, Direction.BottomRight, 150), Step(6, 5, Direction.Right, 100), Step(7, 6, Direction.Right, 100)]);

        Assert.Equal(5, path.FrontIndex);
        Assert.Equal(0, path.FrontFrom);
        Assert.Equal(Direction.BottomRight, path.FrontDirection);
        Assert.Equal(150u, path.FrontPenalty);
        Assert.Equal(Direction.Right, path.NextStepDirection);
        Assert.Equal(7, path.DestinationIndex);
    }

    [Fact]
    public void TheNextStepDirectionNeedsTwoSteps()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100)]);

        Assert.Equal(Direction.Unknown, path.NextStepDirection);
    }

    [Fact]
    public void PopFrontWalksTheRoute()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100), Step(2, 1, Direction.Right, 100)]);

        path.PopFront();

        Assert.Equal(1, path.Count);
        Assert.Equal(2, path.FrontIndex);
        Assert.Equal(1, path.FrontFrom);

        path.PopFront();

        Assert.True(path.IsEmpty);
        Assert.Equal(-1, path.DestinationIndex);
    }

    [Fact]
    public void PopFrontOnAnEmptyRouteDoesNothing()
    {
        HeroPath path = new();

        path.PopFront();

        Assert.True(path.IsEmpty);
    }

    [Fact]
    public void TruncateKeepsOnlyTheFrontStep()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100), Step(2, 1, Direction.Right, 100), Step(3, 2, Direction.Right, 100)]);

        path.Truncate();

        Assert.Equal(1, path.Count);
        Assert.Equal(1, path.DestinationIndex);
    }

    [Fact]
    public void TruncateOnAnEmptyRouteDoesNothing()
    {
        HeroPath path = new();

        path.Truncate();

        Assert.True(path.IsEmpty);
    }

    [Fact]
    public void ResetEmptiesTheRoute()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100)]);
        path.Reset();

        Assert.True(path.IsEmpty);
    }

    [Fact]
    public void AStepWithADirectionOnTheMapIsAValidMovementStep()
    {
        HeroPath path = new();

        path.SetPath([Step(9, 0, Direction.Right, 100)]);

        Assert.True(path.IsValidForMovement(MapSize));
        Assert.False(path.IsValidForTeleportation(MapSize));
    }

    [Fact]
    public void AStepWithoutADirectionIsATeleport()
    {
        HeroPath path = new();

        path.SetPath([Step(9, 0, Direction.Unknown, 100)]);

        Assert.False(path.IsValidForMovement(MapSize));
        Assert.True(path.IsValidForTeleportation(MapSize));
    }

    [Fact]
    public void AStepOutsideTheMapIsNotValid()
    {
        HeroPath path = new();

        path.SetPath([Step(MapSize, 0, Direction.Right, 100)]);

        Assert.False(path.IsValidForMovement(MapSize));
        Assert.False(path.IsValidForTeleportation(MapSize));
    }

    [Fact]
    public void AnEmptyRouteIsNeitherMovementNorTeleportation()
    {
        HeroPath path = new();

        Assert.False(path.IsValidForMovement(MapSize));
        Assert.False(path.IsValidForTeleportation(MapSize));
    }

    [Fact]
    public void HasAllowedStepsAsksForTheFrontStepOnly()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100), Step(2, 1, Direction.Right, 100)]);

        Assert.True(path.HasAllowedSteps(100));
        Assert.False(path.HasAllowedSteps(99));
    }

    [Fact]
    public void GetAllowedStepsCountsTheAffordablePrefix()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100), Step(2, 1, Direction.Right, 100), Step(3, 2, Direction.Right, 100)]);

        Assert.Equal(0, path.GetAllowedSteps(0));
        Assert.Equal(0, path.GetAllowedSteps(50));
        Assert.Equal(1, path.GetAllowedSteps(150));
        Assert.Equal(2, path.GetAllowedSteps(250));
        Assert.Equal(3, path.GetAllowedSteps(300));
        Assert.Equal(3, path.GetAllowedSteps(1000));
    }

    [Fact]
    public void CalculatePenaltyAddsUpTheSteps()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100), Step(2, 1, Direction.BottomRight, 150)]);

        Assert.Equal(250u, path.CalculatePenalty());
    }

    [Fact]
    public void CalculatePenaltyIsZeroForAnEmptyRoute()
    {
        Assert.Equal(0u, HeroPath.CalculatePenalty([]));
    }

    [Fact]
    public void AStepCanOnlyBeWalkedFromWhereItStarts()
    {
        HeroPath path = new();

        path.SetPath([Step(1, 0, Direction.Right, 100)]);

        Assert.True(path.IsWalkableFrom(0, 100));
        Assert.False(path.IsWalkableFrom(1, 100));
        Assert.False(path.IsWalkableFrom(0, 99));
    }

    [Fact]
    public void ARouteFromThePathfinderWalksToTheDestination()
    {
        World world = new();
        world.LoadMap(SyntheticMap.Create(4, 4, _ => SyntheticMap.BlankTile() with { TerrainImageIndex = 68 }));

        for (int index = 0; index < world.Size; ++index)
        {
            world.GetTile(index).ClearFog(PlayerColor.Blue);
        }

        PlayerWorldPathfinder pathfinder = world.Pathfinder;
        pathfinder.ReEvaluateIfNeeded(0, PlayerColor.Blue, 1000, SkillLevel.None, 1000);

        HeroPath path = new();
        path.SetPath(pathfinder.BuildPath(3));

        Assert.Equal(3, path.Count);
        Assert.Equal(300u, path.CalculatePenalty());

        while (!path.IsEmpty)
        {
            Assert.True(path.IsValidForMovement(world.Size));
            path.PopFront();
        }

        Assert.Equal(-1, path.DestinationIndex);
    }

    [Theory]
    [InlineData(100, 25)]
    [InlineData(125, 49)]
    [InlineData(150, 73)]
    [InlineData(175, 97)]
    [InlineData(200, 121)]
    [InlineData(99, 1)]
    [InlineData(300, 1)]
    public void TheArrowOfAStepPicksTheBlockOfItsCost(int cost, int blockStart)
    {
        // A straight arrow inside each block: the "from" and the "to" are both the top direction.
        Assert.Equal(blockStart + 8, HeroPath.GetIndexSprite(Direction.Top, Direction.Top, cost));
    }

    [Theory]
    [InlineData(Direction.Top, Direction.TopRight, 17)]
    [InlineData(Direction.Top, Direction.BottomLeft, 5)]
    [InlineData(Direction.TopRight, Direction.Top, 0)]
    [InlineData(Direction.Right, Direction.BottomLeft, 21)]
    [InlineData(Direction.BottomRight, Direction.Left, 22)]
    [InlineData(Direction.Bottom, Direction.TopLeft, 16)]
    [InlineData(Direction.BottomLeft, Direction.BottomLeft, 13)]
    [InlineData(Direction.Left, Direction.Left, 14)]
    [InlineData(Direction.TopLeft, Direction.TopLeft, 15)]
    public void TheArrowFollowsThePairOfDirections(Direction from, Direction to, int offset)
    {
        Assert.Equal(1 + offset, HeroPath.GetIndexSprite(from, to, 100) - 24);
    }

    [Theory]
    [InlineData(Direction.Top, Direction.Bottom)]
    [InlineData(Direction.TopRight, Direction.BottomLeft)]
    [InlineData(Direction.Bottom, Direction.Top)]
    [InlineData(Direction.Unknown, Direction.Top)]
    [InlineData(Direction.Top, Direction.Unknown)]
    public void AnArrowBackAtThePreviousStepIsNotDrawn(Direction from, Direction to)
    {
        Assert.Equal(0, HeroPath.GetIndexSprite(from, to, 100));
    }

    private static RouteStep Step(int index, int from, Direction direction, uint penalty) =>
        new(index, from, direction, penalty);
}
