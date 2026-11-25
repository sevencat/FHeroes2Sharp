using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Battle;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// What a march will draw, <see cref="BattleMoveAnimation"/>: the steps of <c>RedrawActionMove()</c> and
/// <c>RedrawActionFly()</c> (<c>battle_interface.cpp:4524-4830</c>).
/// </summary>
public sealed class BattleMoveAnimationTests
{
    private static GameArmy Army(PlayerColor color, MonsterType monster, int count)
    {
        GameArmy army = new()
        {
            Color = color,
        };

        army.GetTroop(0).Set(monster, (uint)count);

        return army;
    }

    private static BattleArena Arena(MonsterType attacker)
    {
        SetControl(PlayerColor.Blue, PlayerControl.Human);
        SetControl(PlayerColor.Red, PlayerControl.Ai);

        return new BattleArena(
            Army(PlayerColor.Blue, attacker, 5), Army(PlayerColor.Red, MonsterType.Peasant, 10), new Pcg32(31));
    }

    private static void SetControl(PlayerColor color, PlayerControl control)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = control;

        Players.Set(color, player);
    }

    /// <summary>The length of the walk to a cell, or zero when it is out of reach.</summary>
    private static int PathLength(BattleArena arena, BattleUnit unit, int index) =>
        arena.CreateMoveCommand(unit, index) is null
            ? 0
            : arena.GetPath(unit, BattlePosition.GetReachable(arena, unit, index)).Length;

    /// <summary>A free cell the unit can walk onto this turn.</summary>
    private static int ReachableEmptyCell(BattleArena arena, BattleUnit unit)
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (arena.Board.GetCell(index) is { Unit: null } && arena.CreateMoveCommand(unit, index) is not null)
            {
                return index;
            }
        }

        throw new InvalidOperationException("The unit can reach no empty cell.");
    }

    /// <summary>A cell the unit reaches in exactly the given number of steps.</summary>
    private static int CellAtPathLength(BattleArena arena, BattleUnit unit, int length)
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (arena.Board.GetCell(index) is { Unit: null } && PathLength(arena, unit, index) == length)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"No empty cell is {length} steps away.");
    }

    /// <summary>Standing on the destination is not a march.</summary>
    [Fact]
    public void TheCellTheUnitStandsOnHasNothingToDraw()
    {
        BattleArena arena = Arena(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();

        Assert.Null(BattleMoveAnimation.Create(arena, unit, unit.HeadIndex, flightStep: 16));
    }

    /// <summary>One cell is the quick step, not a start-and-ending pair (<c>battle_interface.cpp:4464-4471</c>).</summary>
    [Fact]
    public void OneCellIsASingleQuickStep()
    {
        BattleArena arena = Arena(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();
        int destination = CellAtPathLength(arena, unit, 1);

        BattleMoveAnimation plan = BattleMoveAnimation.Create(arena, unit, destination, flightStep: 16)!;

        BattleMoveStep step = Assert.Single(plan.Steps);
        Assert.Equal(MonsterAnimationType.MoveQuick, step.Animation);
        Assert.Equal(destination, step.CellIndex);
        Assert.False(step.IsTurnOnly);
        Assert.False(plan.IsFlight);
    }

    /// <summary>A longer walk starts, carries on, and ends with the matching sequences.</summary>
    [Fact]
    public void ALongerWalkStartsCarriesOnAndEnds()
    {
        BattleArena arena = Arena(MonsterType.Wolf);
        BattleUnit unit = arena.AttackingForce.First();
        int destination = CellAtPathLength(arena, unit, 3);

        BattleMoveAnimation plan = BattleMoveAnimation.Create(arena, unit, destination, flightStep: 16)!;

        Assert.Equal(3, plan.Steps.Count);
        Assert.Equal(MonsterAnimationType.MoveStart, plan.Steps[0].Animation);
        Assert.Equal(MonsterAnimationType.Moving, plan.Steps[1].Animation);
        Assert.Equal(MonsterAnimationType.MoveEnd, plan.Steps[2].Animation);
        Assert.Equal(destination, plan.Destination);
    }

    /// <summary>Each step heads for the sprite point of its own cell.</summary>
    [Fact]
    public void EachStepAimsAtItsOwnCell()
    {
        BattleArena arena = Arena(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();
        int destination = CellAtPathLength(arena, unit, 2);

        BattleMoveAnimation plan = BattleMoveAnimation.Create(arena, unit, destination, flightStep: 16)!;

        foreach (BattleMoveStep step in plan.Steps)
        {
            Rect cell = arena.Board.GetCell(step.CellIndex)!.Position;

            Assert.Equal(new Point(cell.X, cell.Y), step.MovingPosition);
            Assert.Null(step.FlyingPosition);
        }
    }

    /// <summary>Planning is presentation only: the unit and the board are left alone.</summary>
    [Fact]
    public void PlanningDoesNotMoveTheUnit()
    {
        BattleArena arena = Arena(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();
        int head = unit.HeadIndex;
        int destination = CellAtPathLength(arena, unit, 2);

        _ = BattleMoveAnimation.Create(arena, unit, destination, flightStep: 16);

        Assert.Equal(head, unit.HeadIndex);
        Assert.Same(unit, arena.Board.GetCell(head)!.Unit);
        Assert.Null(arena.Board.GetCell(destination)!.Unit);
    }

    /// <summary>A flyer lifts off, crosses the field in stretches, and lands (<c>battle_interface.cpp:4715-4830</c>).</summary>
    [Fact]
    public void AFlyerLiftsOffFliesAndLands()
    {
        BattleArena arena = Arena(MonsterType.Griffin);
        BattleUnit unit = arena.AttackingForce.First();
        int destination = ReachableEmptyCell(arena, unit);

        BattleMoveAnimation plan = BattleMoveAnimation.Create(arena, unit, destination, flightStep: 16)!;

        Assert.True(plan.IsFlight);
        Assert.Equal(MonsterAnimationType.FlyUp, plan.Steps[0].Animation);
        Assert.Null(plan.Steps[0].FlyingPosition);
        Assert.Equal(MonsterAnimationType.FlyLand, plan.Steps[^1].Animation);

        // Every step of a flight aims at the landing cell, the reference's single dst.
        Assert.All(plan.Steps, step => Assert.Equal(destination, step.CellIndex));

        // The stretches between the two ends are the only ones with a point behind them.
        Assert.All(plan.Steps.Skip(1).Take(plan.Steps.Count - 2), step =>
        {
            Assert.Equal(MonsterAnimationType.Moving, step.Animation);
            Assert.NotNull(step.FlyingPosition);
        });
    }

    /// <summary>
    /// The straight line is cut into stretches of the flight speed, ends included
    /// (<c>math_tools.cpp:47-74</c>).
    /// </summary>
    [Fact]
    public void TheLineIsCutIntoStretchesOfTheFlightSpeed()
    {
        IReadOnlyList<Point> points = BattleMoveAnimation.GetLinePoints(new Point(0, 0), new Point(100, 0), 16);

        Assert.Equal(8, points.Count);
        Assert.Equal(new Point(0, 0), points[0]);
        Assert.Equal(new Point(100, 0), points[^1]);
    }

    /// <summary>A line shorter than two stretches is just its two ends.</summary>
    [Fact]
    public void AShortLineIsItsTwoEnds()
    {
        IReadOnlyList<Point> points = BattleMoveAnimation.GetLinePoints(new Point(5, 5), new Point(10, 5), 100);

        Assert.Equal(2, points.Count);
        Assert.Equal(new Point(5, 5), points[0]);
        Assert.Equal(new Point(10, 5), points[1]);
    }
}
