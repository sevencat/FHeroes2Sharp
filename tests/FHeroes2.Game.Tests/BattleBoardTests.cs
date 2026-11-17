using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the battlefield geometry of <c>battle_board.cpp</c>: the offset hex grid, the direction of one cell from
/// another, the hex distance, the castle and moat tables, and the rings around a unit — all of it computed from
/// the cell index alone, which is why it can be pinned without units.
/// </summary>
public class BattleBoardTests
{
    [Fact]
    public void TheBoardIsElevenByNine()
    {
        Assert.Equal(11, BattleBoard.WidthInCells);
        Assert.Equal(9, BattleBoard.HeightInCells);
        Assert.Equal(99, BattleBoard.SizeInCells);

        Assert.True(BattleBoard.IsValidIndex(0));
        Assert.True(BattleBoard.IsValidIndex(98));
        Assert.False(BattleBoard.IsValidIndex(-1));
        Assert.False(BattleBoard.IsValidIndex(99));
    }

    [Fact]
    public void TheCornerCellKnowsItsThreeNeighbours()
    {
        // Index 0 is the top left corner: no cell above it, so its ring runs right, down right and down left.
        Assert.Equal([1, 12, 11], BattleBoard.GetAroundIndexes(0));

        Assert.Equal(CellDirection.Right, BattleBoard.GetDirection(0, 1));
        Assert.Equal(CellDirection.BottomRight, BattleBoard.GetDirection(0, 12));
        Assert.Equal(CellDirection.BottomLeft, BattleBoard.GetDirection(0, 11));
        Assert.Equal(CellDirection.Center, BattleBoard.GetDirection(0, 0));
        Assert.Equal(CellDirection.Unknown, BattleBoard.GetDirection(0, 2));
    }

    [Fact]
    public void EveryNeighbourIsOneStepAwayAndTheDirectionIsReciprocal()
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            foreach (CellDirection direction in BattleCellDirections.All)
            {
                if (!BattleBoard.IsValidDirection(index, direction))
                {
                    continue;
                }

                int neighbour = BattleBoard.GetIndexDirection(index, direction);

                Assert.InRange(neighbour, 0, BattleBoard.SizeInCells - 1);
                Assert.Equal(1u, BattleBoard.GetDistance(index, neighbour));
                Assert.Equal(direction, BattleBoard.GetDirection(index, neighbour));
                Assert.Equal(
                    BattleBoard.GetReflectDirection(direction),
                    BattleBoard.GetDirection(neighbour, index));
            }
        }
    }

    [Fact]
    public void TheRowsLoseTheirDiagonalsTowardsTheShift()
    {
        // Row 1 is one of the rows pushed half a cell to the right, so its leftmost cell has no top left
        // neighbour, but does reach the right one of row 0.
        Assert.False(BattleBoard.IsValidDirection(11, CellDirection.TopLeft));
        Assert.True(BattleBoard.IsValidDirection(11, CellDirection.TopRight));
        Assert.Equal(0, BattleBoard.GetIndexDirection(11, CellDirection.TopRight));

        // Row 4 is not, so its leftmost cell has a top left neighbour and no top right one.
        Assert.True(BattleBoard.IsValidDirection(44, CellDirection.TopLeft));
        Assert.Equal(33, BattleBoard.GetIndexDirection(44, CellDirection.TopLeft));
        Assert.Equal(34, BattleBoard.GetIndexDirection(44, CellDirection.TopRight));
    }

    [Fact]
    public void TheCentreIsAlwaysAValidDirection()
    {
        foreach (int index in new[] { 0, 5, 49, 98 })
        {
            Assert.True(BattleBoard.IsValidDirection(index, CellDirection.Center));
            Assert.Equal(index, BattleBoard.GetIndexDirection(index, CellDirection.Center));
        }

        Assert.False(BattleBoard.IsValidDirection(-1, CellDirection.Center));
        Assert.Equal(-1, BattleBoard.GetIndexDirection(-1, CellDirection.Right));
    }

    [Fact]
    public void TheDistanceCountsHexSteps()
    {
        Assert.Equal(0u, BattleBoard.GetDistance(49, 49));
        Assert.Equal(1u, BattleBoard.GetDistance(0, 1));
        Assert.Equal(1u, BattleBoard.GetDistance(0, 12));
        Assert.Equal(10u, BattleBoard.GetDistance(0, 10));
        Assert.Equal(7u, BattleBoard.GetDistance(0, 49));
        Assert.Equal(14u, BattleBoard.GetDistance(0, 98));

        // The distance is symmetric.
        Assert.Equal(BattleBoard.GetDistance(10, 0), BattleBoard.GetDistance(0, 10));
    }

    [Fact]
    public void AnIndexOffTheBoardHasNoDistance()
    {
        Assert.Equal(0u, BattleBoard.GetDistance(0, -1));
        Assert.Equal(0u, BattleBoard.GetDistance(99, 0));
    }

    [Fact]
    public void TheEdgeDistanceCountsFromOne()
    {
        Assert.Equal(1u, BattleBoard.GetDistanceFromBoardEdgeAlongXAxis(0, fromRightEdge: false));
        Assert.Equal(11u, BattleBoard.GetDistanceFromBoardEdgeAlongXAxis(10, fromRightEdge: false));
        Assert.Equal(11u, BattleBoard.GetDistanceFromBoardEdgeAlongXAxis(0, fromRightEdge: true));
        Assert.Equal(1u, BattleBoard.GetDistanceFromBoardEdgeAlongXAxis(10, fromRightEdge: true));

        Assert.Throws<ArgumentOutOfRangeException>(() => BattleBoard.GetDistanceFromBoardEdgeAlongXAxis(99, fromRightEdge: false));
    }

    [Fact]
    public void TheReflectedDirectionIsTheOppositeOne()
    {
        Assert.Equal(CellDirection.BottomRight, BattleBoard.GetReflectDirection(CellDirection.TopLeft));
        Assert.Equal(CellDirection.BottomLeft, BattleBoard.GetReflectDirection(CellDirection.TopRight));
        Assert.Equal(CellDirection.Left, BattleBoard.GetReflectDirection(CellDirection.Right));
        Assert.Equal(CellDirection.Right, BattleBoard.GetReflectDirection(CellDirection.Left));
        Assert.Equal(CellDirection.TopRight, BattleBoard.GetReflectDirection(CellDirection.BottomLeft));
        Assert.Equal(CellDirection.TopLeft, BattleBoard.GetReflectDirection(CellDirection.BottomRight));

        Assert.Equal(CellDirection.Unknown, BattleBoard.GetReflectDirection(CellDirection.Center));
        Assert.Equal(CellDirection.Unknown, BattleBoard.GetReflectDirection(CellDirection.LeftSide));
    }

    [Fact]
    public void TheDirectionFromADeltaPrefersTheDiagonal()
    {
        Assert.Equal(CellDirection.Center, BattleBoard.GetDirectionFromDelta(0, 0));
        Assert.Equal(CellDirection.TopLeft, BattleBoard.GetDirectionFromDelta(-1, -1));
        Assert.Equal(CellDirection.TopRight, BattleBoard.GetDirectionFromDelta(1, -1));
        Assert.Equal(CellDirection.BottomLeft, BattleBoard.GetDirectionFromDelta(-1, 1));
        Assert.Equal(CellDirection.BottomRight, BattleBoard.GetDirectionFromDelta(1, 1));
        Assert.Equal(CellDirection.Left, BattleBoard.GetDirectionFromDelta(-3, 0));
        Assert.Equal(CellDirection.Right, BattleBoard.GetDirectionFromDelta(3, 0));
    }

    [Fact]
    public void TheCastleAndTheFieldSplitTheBoard()
    {
        Assert.True(BattleBoard.IsCastleIndex(8));
        Assert.True(BattleBoard.IsCastleIndex(10));
        Assert.True(BattleBoard.IsCastleIndex(52));
        Assert.False(BattleBoard.IsCastleIndex(0));
        Assert.False(BattleBoard.IsCastleIndex(24));
        Assert.False(BattleBoard.IsCastleIndex(BattleBoard.BridgeMoatIndex));

        Assert.True(BattleBoard.IsOutOfWallsIndex(0));
        Assert.True(BattleBoard.IsOutOfWallsIndex(BattleBoard.BridgeMoatIndex));
        Assert.False(BattleBoard.IsOutOfWallsIndex(9));
        Assert.False(BattleBoard.IsOutOfWallsIndex(52));

        // The two tables touch at the last cell of every open row: the reference lists both, so those four cells
        // answer true to both questions. The port keeps that as it is.
        foreach (int index in new[] { 8, 29, 73, 96 })
        {
            Assert.True(BattleBoard.IsCastleIndex(index), $"cell {index} should be inside the castle");
            Assert.True(BattleBoard.IsOutOfWallsIndex(index), $"cell {index} should be outside the walls");
        }

        // Every other cell is on exactly one side of the wall.
        int covered = 0;

        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            bool castle = BattleBoard.IsCastleIndex(index);
            bool field = BattleBoard.IsOutOfWallsIndex(index);

            Assert.True(castle || field, $"cell {index} is on neither side of the wall");

            if (castle && field)
            {
                ++covered;
            }
        }

        Assert.Equal(4, covered);
    }

    [Fact]
    public void TheMoatIsEightCellsAndTheBridge()
    {
        foreach (int index in new[] { 7, 18, 28, 39, 61, 72, 84, 95 })
        {
            Assert.True(BattleBoard.IsMoatIndex(index, unitIsFlying: true, bridgeIsPassable: true));
        }

        // The bridge cell only hurts a unit that flies over it or faces a bridge it cannot cross.
        Assert.True(BattleBoard.IsMoatIndex(BattleBoard.BridgeMoatIndex, unitIsFlying: false, bridgeIsPassable: false));
        Assert.True(BattleBoard.IsMoatIndex(BattleBoard.BridgeMoatIndex, unitIsFlying: true, bridgeIsPassable: true));
        Assert.False(BattleBoard.IsMoatIndex(BattleBoard.BridgeMoatIndex, unitIsFlying: false, bridgeIsPassable: true));

        Assert.False(BattleBoard.IsMoatIndex(50, unitIsFlying: false, bridgeIsPassable: false));
        Assert.False(BattleBoard.IsMoatIndex(0, unitIsFlying: false, bridgeIsPassable: false));
    }

    [Fact]
    public void TheWideUnitIsAlwaysBuiltTowardsOneSide()
    {
        // Left, right and then the two diagonals on the facing side: at the corner only the right hand ones exist.
        Assert.Equal([1, 12], BattleBoard.GetMoveWideIndexes(0, reflect: false));
        Assert.Equal([1, 11], BattleBoard.GetMoveWideIndexes(0, reflect: true));
        Assert.Empty(BattleBoard.GetMoveWideIndexes(-1, reflect: false));
    }

    [Fact]
    public void TheRingAroundAWideUnitHasEightCells()
    {
        // A wide unit with its head at 45 and its tail at 46, in open field.
        int[] around = BattleBoard.GetAroundIndexes(45, 46);

        Assert.Equal(8, around.Length);
        Assert.Equal([34, 35, 36, 47, 58, 57, 56, 44], around);

        // The same body read the other way round yields the same ring in the same order.
        Assert.Equal(around, BattleBoard.GetAroundIndexes(46, 45));

        // Every cell of the ring is one step from one of the two halves, and neither half is in it.
        foreach (int index in around)
        {
            Assert.DoesNotContain(index, new[] { 45, 46 });
            Assert.Equal(
                1u,
                Math.Min(BattleBoard.GetDistance(45, index), BattleBoard.GetDistance(46, index)));
        }
    }

    [Fact]
    public void ACornerLeavesAWideUnitOnlyFourCellsToTurn()
    {
        Assert.Equal([2, 13, 12, 11], BattleBoard.GetAroundIndexes(0, 1));
    }

    [Fact]
    public void TheDistanceRingAtOneMatchesTheAroundRing()
    {
        foreach (int center in new[] { 0, 1, 11, 33, 45, 49, 87, 98 })
        {
            Assert.Equal(
                BattleBoard.GetAroundIndexes(center).Order(),
                BattleBoard.GetDistanceIndexes(center, 1).Order());
        }

        Assert.Empty(BattleBoard.GetDistanceIndexes(-1, 1));
    }

    [Fact]
    public void TheRingIsEverythingWithinTheRadius()
    {
        // The reference calls it a distance ring but fills it with every cell within the radius, not just the cells
        // at exactly that distance — the ones of the smaller ring come along.
        const int center = 45;

        int[] ring = BattleBoard.GetDistanceIndexes(center, 2);

        Assert.Equal(17, ring.Length);
        Assert.DoesNotContain(center, ring);
        Assert.Contains(44, ring);
        Assert.Contains(36, ring);

        foreach (int index in ring)
        {
            Assert.InRange(BattleBoard.GetDistance(center, index), 1u, 2u);
        }
    }

    [Fact]
    public void TheRingOfAWideUnitSkipsTheUnitItself()
    {
        int[] ring = BattleBoard.GetDistanceIndexes(45, 46, 1);

        // The union of both rings without the unit's own two cells, in ascending order.
        Assert.Equal([34, 35, 36, 44, 47, 56, 57, 58], ring);
    }

    [Fact]
    public void OnlyNeighbouringIndexesAreNear()
    {
        Assert.True(BattleBoard.IsNearIndexes(0, 1));
        Assert.True(BattleBoard.IsNearIndexes(45, 44));
        Assert.False(BattleBoard.IsNearIndexes(0, 0));
        Assert.False(BattleBoard.IsNearIndexes(0, 2));
        Assert.False(BattleBoard.IsNearIndexes(-1, 0));
    }
}
