using FHeroes2.Core.Geometry;
using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the geometry of <c>battle_cell.cpp</c>: where a cell is drawn, and which eighth of its hexagon a point
/// falls in. The unit-related members of <c>Battle::Cell</c> come with <c>battle_troop</c> and are not pinned here.
/// </summary>
public class BattleCellTests
{
    [Fact]
    public void ACellIsFortyFourByFiftyTwoPixels()
    {
        Assert.Equal(44, BattleCell.WidthPx);
        Assert.Equal(52, BattleCell.HeightPx);
        Assert.Equal(42, BattleCell.RowStepPx);
        Assert.Equal(32, BattleCell.VerticalSideHeightPx);
    }

    [Fact]
    public void TheFirstCellSitsAtTheTopLeftMargin()
    {
        BattleCell cell = new(0);

        Assert.Equal(0, cell.Index);
        Assert.Equal(new Rect(89, 62, 44, 52), cell.Position);
        Assert.Equal(0, cell.Object);
    }

    [Fact]
    public void OddRowsAreShiftedHalfACellToTheLeft()
    {
        // 第 1 行第 0 列：左移 22 像素，下移 42 像素（不是整格高 52，六边形竖直方向重叠）。
        BattleCell cell = new(BattleBoard.WidthInCells);

        Assert.Equal(new Rect(67, 104, 44, 52), cell.Position);

        // 第 1 行第 1 列回到偶数行的起点再右移一格。
        Assert.Equal(new Rect(111, 104, 44, 52), new BattleCell(BattleBoard.WidthInCells + 1).Position);
    }

    [Fact]
    public void TheAreaMovesTheWholeGrid()
    {
        BattleCell cell = new(0);
        cell.SetArea(new Rect(10, 20, 640, 480));

        Assert.Equal(new Rect(99, 82, 44, 52), cell.Position);

        // 第 2 行第 2 列：右移两格、下移两行，偶数行不左移。
        BattleCell lower = new((BattleBoard.WidthInCells * 2) + 2);
        lower.SetArea(new Rect(10, 20, 640, 480));

        Assert.Equal(new Rect(99 + (44 * 2), 82 + (42 * 2), 44, 52), lower.Position);
    }

    [Fact]
    public void TheCentreOfEveryCellIsTheCentreSlice()
    {
        BattleCell cell = new(0);

        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            BattleCell current = new(index);
            Point centre = current.Position.Center;

            Assert.Equal(AttackDirection.Center, current.GetTriangleDirection(centre));
            Assert.True(current.IsPositionIncludePoint(centre));
        }

        // 参考只把「正好落在中心点」算作 CENTER。
        Assert.NotEqual(AttackDirection.Center, cell.GetTriangleDirection(new Point(90, 63)));
    }

    [Fact]
    public void TheHexagonIsCutIntoEightSlices()
    {
        BattleCell cell = new(0);
        Rect position = cell.Position;

        Point At(int dx, int dy) => new(position.X + dx, position.Y + dy);

        Assert.Equal(AttackDirection.Top, cell.GetTriangleDirection(At(22, 12)));
        Assert.Equal(AttackDirection.TopLeft, cell.GetTriangleDirection(At(7, 12)));
        Assert.Equal(AttackDirection.TopRight, cell.GetTriangleDirection(At(37, 12)));
        Assert.Equal(AttackDirection.Right, cell.GetTriangleDirection(At(40, 26)));
        Assert.Equal(AttackDirection.BottomRight, cell.GetTriangleDirection(At(37, 40)));
        Assert.Equal(AttackDirection.Bottom, cell.GetTriangleDirection(At(22, 40)));
        Assert.Equal(AttackDirection.BottomLeft, cell.GetTriangleDirection(At(7, 40)));
        Assert.Equal(AttackDirection.Left, cell.GetTriangleDirection(At(4, 26)));
    }

    [Fact]
    public void APointOutsideTheHexagonBelongsToNoSlice()
    {
        BattleCell cell = new(0);

        Assert.Equal(AttackDirection.Unknown, cell.GetTriangleDirection(new Point(0, 0)));
        Assert.Equal(AttackDirection.Unknown, cell.GetTriangleDirection(new Point(600, 600)));
        Assert.False(cell.IsPositionIncludePoint(new Point(0, 0)));

        // 正下方一格之外就已经在六边形外：格高 52，但六边形竖直只占 32 像素。
        Assert.Equal(AttackDirection.Unknown, cell.GetTriangleDirection(new Point(cell.Position.X + 22, cell.Position.Y + 51)));
    }

    [Fact]
    public void TheObjectNumberIsPlainState()
    {
        BattleCell cell = new(5)
        {
            Object = 17,
        };

        Assert.Equal(5, cell.Index);
        Assert.Equal(17, cell.Object);
    }
}
