using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the ring arithmetic of <c>battle_cell.h</c>: the directions are single bits so that
/// <c>operator++</c> can double, and the two rings — six movement directions and eight attack directions —
/// have different lengths.
/// </summary>
public class BattleCellDirectionTests
{
    [Fact]
    public void TheMovementRingDoubles()
    {
        Assert.Equal(CellDirection.TopRight, BattleCellDirections.Next(CellDirection.TopLeft));
        Assert.Equal(CellDirection.Right, BattleCellDirections.Next(CellDirection.TopRight));
        Assert.Equal(CellDirection.BottomRight, BattleCellDirections.Next(CellDirection.Right));
        Assert.Equal(CellDirection.BottomLeft, BattleCellDirections.Next(CellDirection.BottomRight));
        Assert.Equal(CellDirection.Left, BattleCellDirections.Next(CellDirection.BottomLeft));

        // The ring closes on the centre, which is the only value at or above it.
        Assert.Equal(CellDirection.Center, BattleCellDirections.Next(CellDirection.Left));
        Assert.Equal(CellDirection.TopLeft, BattleCellDirections.Next(CellDirection.Center));
    }

    [Fact]
    public void TheMovementRingRunsBackwardsToo()
    {
        foreach (CellDirection direction in BattleCellDirections.All)
        {
            Assert.Equal(direction, BattleCellDirections.Previous(BattleCellDirections.Next(direction)));
            Assert.Equal(direction, BattleCellDirections.Next(BattleCellDirections.Previous(direction)));
        }

        Assert.Equal(CellDirection.Center, BattleCellDirections.Previous(CellDirection.TopLeft));
    }

    [Fact]
    public void TheAllArrayIsTheRingInOrder()
    {
        CellDirection direction = CellDirection.TopLeft;

        foreach (CellDirection expected in BattleCellDirections.All)
        {
            Assert.Equal(expected, direction);
            direction = BattleCellDirections.Next(direction);
        }

        Assert.Equal(CellDirection.Center, direction);
    }

    [Fact]
    public void TheAttackRingHasEightDirections()
    {
        AttackDirection direction = AttackDirection.TopLeft;

        foreach (AttackDirection expected in BattleCellDirections.AllAttackDirections)
        {
            Assert.Equal(expected, direction);
            direction = BattleCellDirections.Next(direction);
        }

        Assert.Equal(AttackDirection.Center, direction);
        Assert.Equal(8, BattleCellDirections.AllAttackDirections.Length);
    }

    [Fact]
    public void TheAttackRingRunsBackwardsToo()
    {
        foreach (AttackDirection direction in BattleCellDirections.AllAttackDirections)
        {
            Assert.Equal(direction, BattleCellDirections.Previous(BattleCellDirections.Next(direction)));
        }

        Assert.Equal(AttackDirection.Center, BattleCellDirections.Previous(AttackDirection.TopLeft));
        Assert.Equal(AttackDirection.TopLeft, BattleCellDirections.Next(AttackDirection.Center));
    }

    [Fact]
    public void TheSideMasksHoldThreeNeighboursEach()
    {
        Assert.True(BattleCellDirections.IsLeftSide(CellDirection.TopLeft));
        Assert.True(BattleCellDirections.IsLeftSide(CellDirection.Left));
        Assert.True(BattleCellDirections.IsLeftSide(CellDirection.BottomLeft));
        Assert.False(BattleCellDirections.IsLeftSide(CellDirection.TopRight));
        Assert.False(BattleCellDirections.IsLeftSide(CellDirection.Center));
        Assert.False(BattleCellDirections.IsLeftSide(CellDirection.Unknown));

        Assert.True(BattleCellDirections.IsRightSide(CellDirection.TopRight));
        Assert.True(BattleCellDirections.IsRightSide(CellDirection.Right));
        Assert.True(BattleCellDirections.IsRightSide(CellDirection.BottomRight));
        Assert.False(BattleCellDirections.IsRightSide(CellDirection.Left));
        Assert.False(BattleCellDirections.IsRightSide(CellDirection.Unknown));

        Assert.Equal(CellDirection.LeftSide, CellDirection.TopLeft | CellDirection.Left | CellDirection.BottomLeft);
        Assert.Equal(CellDirection.RightSide, CellDirection.TopRight | CellDirection.Right | CellDirection.BottomRight);
    }

    [Fact]
    public void TheAttackDirectionSkipsTheCentreAndTheMasks()
    {
        Assert.Equal(AttackDirection.TopLeft, BattleCellDirections.AsAttackDirection(CellDirection.TopLeft));
        Assert.Equal(AttackDirection.TopRight, BattleCellDirections.AsAttackDirection(CellDirection.TopRight));
        Assert.Equal(AttackDirection.Right, BattleCellDirections.AsAttackDirection(CellDirection.Right));
        Assert.Equal(AttackDirection.BottomRight, BattleCellDirections.AsAttackDirection(CellDirection.BottomRight));
        Assert.Equal(AttackDirection.BottomLeft, BattleCellDirections.AsAttackDirection(CellDirection.BottomLeft));
        Assert.Equal(AttackDirection.Left, BattleCellDirections.AsAttackDirection(CellDirection.Left));

        Assert.Equal(AttackDirection.Unknown, BattleCellDirections.AsAttackDirection(CellDirection.Center));
        Assert.Equal(AttackDirection.Unknown, BattleCellDirections.AsAttackDirection(CellDirection.Unknown));
        Assert.Equal(AttackDirection.Unknown, BattleCellDirections.AsAttackDirection(CellDirection.LeftSide));
    }
}
