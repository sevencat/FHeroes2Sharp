using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>battle_cell.cpp:48-254</c>: where a unit stands, how its two cells move, and where a step would land.
/// </summary>
public class BattlePositionTests
{
    private static BattleBoardCells Board() => new();

    [Fact]
    public void ANarrowUnitOccupiesOnlyItsHead()
    {
        BattlePosition position = new(Board(), head: 40, wide: false, reflect: false);

        Assert.False(position.IsEmpty);
        Assert.Equal(40, position.HeadIndex);
        Assert.Equal(-1, position.TailIndex);
        Assert.False(position.IsReflect);
        Assert.True(position.Contains(40));
        Assert.False(position.Contains(41));
    }

    [Fact]
    public void AnOffBoardIndexLeavesThePositionEmpty()
    {
        BattlePosition position = new(Board(), head: BattleBoard.SizeInCells, wide: true, reflect: false);

        Assert.True(position.IsEmpty);
        Assert.Equal(-1, position.HeadIndex);
    }

    [Fact]
    public void AWideUnitTakesTheTailOnTheSideItFaces()
    {
        BattleCell? left = Board().GetCell(40);
        Assert.NotNull(left);

        BattlePosition reflected = new(Board(), head: 40, wide: true, reflect: true);

        // 朝左时头在低编号一侧，尾巴在右邻格。
        Assert.Equal(41, reflected.TailIndex);
        Assert.True(reflected.IsReflect);

        BattlePosition forward = new(Board(), head: 41, wide: true, reflect: false);

        // 朝右时尾巴在左邻格，头的编号反而更大。
        Assert.Equal(40, forward.TailIndex);
        Assert.False(forward.IsReflect);
    }

    [Fact]
    public void SwappingReversesTheTwoCells()
    {
        BattlePosition position = new(Board(), head: 40, wide: true, reflect: true);

        position.Swap();

        Assert.Equal(41, position.HeadIndex);
        Assert.Equal(40, position.TailIndex);
    }

    [Fact]
    public void TheRectangleCoversBothCells()
    {
        BattleBoardCells board = Board();
        board.SetArea(new Rect(0, 0, 640, 480));

        BattlePosition position = new(board, head: 40, wide: true, reflect: true);
        Rect rect = position.GetRect();

        Assert.Equal(40, position.HeadIndex);
        Assert.Equal(41, position.TailIndex);

        // 两格并排：宽度是两格 88，高度仍是一格 52。
        Assert.Equal(88, rect.Width);
        Assert.Equal(52, rect.Height);
        Assert.Equal(board.GetCell(40)!.Position.X, rect.X);
    }

    [Fact]
    public void AUnitOnlyFitsAPositionOfItsOwnWidth()
    {
        BattleBoardCells board = Board();
        BattleUnit narrow = new(new Troop(MonsterType.Peasant, 10), new BattlePosition(board), false, 1);
        BattleUnit wide = new(new Troop(MonsterType.Cavalry, 10), new BattlePosition(board), false, 2);

        BattlePosition single = new(board, head: 40, wide: false, reflect: false);
        BattlePosition pair = new(board, head: 40, wide: true, reflect: true);

        Assert.True(single.IsValidForUnit(narrow));
        Assert.False(single.IsValidForUnit(wide));
        Assert.True(pair.IsValidForUnit(wide));
        Assert.False(pair.IsValidForUnit(narrow));
        Assert.False(single.IsValidForUnit(null));
    }

    [Fact]
    public void AStepOntoFreeGroundLandsOnTheCell()
    {
        BattleBoardCells board = Board();
        BattleUnit unit = new(new Troop(MonsterType.Peasant, 10), new BattlePosition(board), false, 1);

        BattlePosition target = BattlePosition.GetPosition(unit, 45);

        Assert.Equal(45, target.HeadIndex);
        Assert.Equal(-1, target.TailIndex);
    }

    [Fact]
    public void AStepOntoAnOccupiedCellIsImpossible()
    {
        BattleBoardCells board = Board();
        BattleUnit blocker = new(new Troop(MonsterType.Peasant, 10), new BattlePosition(board), false, 1);
        BattleUnit walker = new(new Troop(MonsterType.Peasant, 10), new BattlePosition(board), false, 2);

        walker.SetPosition(20);
        blocker.SetPosition(45);

        Assert.True(BattlePosition.GetPosition(walker, 45).IsEmpty);

        // 自己的格子永远走得回去，否则原地转个身都会被自己挡住。
        Assert.Equal(20, BattlePosition.GetPosition(walker, 20).HeadIndex);
    }

    [Fact]
    public void AWideUnitTriesTheOtherSideWhenTheFirstTailCellIsBlocked()
    {
        BattleBoardCells board = Board();
        BattleUnit blocker = new(new Troop(MonsterType.Peasant, 10), new BattlePosition(board), false, 1);
        BattleUnit cavalry = new(new Troop(MonsterType.Cavalry, 10), new BattlePosition(board), false, 2);

        // 目标格 45 的右邻 46 被占，左邻 44 空着，于是朝右的骑兵转身落在 44。
        blocker.SetPosition(46);
        BattlePosition target = BattlePosition.GetPosition(cavalry, 45);

        Assert.Equal(45, target.HeadIndex);
        Assert.Equal(44, target.TailIndex);
    }

    [Fact]
    public void AStepOffTheEdgeOfTheBoardIsImpossible()
    {
        BattleBoardCells board = Board();
        BattleUnit unit = new(new Troop(MonsterType.Peasant, 10), new BattlePosition(board), false, 1);

        Assert.True(BattlePosition.GetPosition(unit, -1).IsEmpty);
        Assert.True(BattlePosition.GetPosition(unit, BattleBoard.SizeInCells).IsEmpty);
    }

    [Fact]
    public void TheDistanceBetweenTwoUnitsUsesTheNearestCells()
    {
        BattleBoardCells board = Board();

        BattlePosition single = new(board, head: 40, wide: false, reflect: false);
        BattlePosition pair = new(board, head: 43, wide: true, reflect: true);

        // 40 到 43 与 40 到 44 里取近的那个。
        Assert.Equal(
            Math.Min(BattleBoard.GetDistance(40, 43), BattleBoard.GetDistance(40, 44)),
            BattleBoard.GetDistance(single, pair));

        Assert.Equal(0u, BattleBoard.GetDistance(single, new BattlePosition(board)));
    }

    [Fact]
    public void PositionsSortByCellIndex()
    {
        BattleBoardCells board = Board();

        BattlePosition first = new(board, head: 10, wide: false, reflect: false);
        BattlePosition second = new(board, head: 20, wide: false, reflect: false);

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(second.CompareTo(first) > 0);
        Assert.Equal(0, first.CompareTo(new BattlePosition(board, head: 10, wide: false, reflect: false)));
    }
}
