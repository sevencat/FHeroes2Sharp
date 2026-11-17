using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Battle;

/// <summary>
/// One cell of the battlefield, <c>Battle::Cell</c> (<c>battle_cell.h:115-194</c>,
/// <c>battle_cell.cpp:256-359</c>), minus the parts that need a unit.
/// </summary>
/// <remarks>
/// <para>
/// 参考在每个格子里存一个 <c>Unit *</c>，通行性判断都从它出发；占位与 <see cref="IsPassable"/> 已随
/// <see cref="BattleUnit"/> 一起落地，`isPassableFromAdjacent`/`isPassableForUnit` 等桥与寻路批次再补。
/// </para>
/// <para>
/// 偶数列从 <c>area.x + 89</c> 起，奇数行左移半格；行距是 42 像素而不是格高 52，因为六边形竖直方向互相重叠。
/// 七个缓存点是中心加六个角，统一乘以 12 化成整数，三角形容差测试就只用整数运算。
/// </para>
/// </remarks>
public sealed class BattleCell
{
    /// <summary>Cell width in pixels, <c>Cell::widthPx</c>.</summary>
    public const int WidthPx = 44;

    /// <summary>Cell height in pixels, <c>Cell::heightPx</c>; larger than the row step because hexes overlap.</summary>
    public const int HeightPx = 52;

    /// <summary>The straight part of the hexagon's vertical sides, <c>cellHeightVerSide</c>.</summary>
    public const int VerticalSideHeightPx = 32;

    /// <summary>Distance from one row to the next, <c>( heightPx - ( heightPx - cellHeightVerSide ) / 2 )</c>.</summary>
    public const int RowStepPx = HeightPx - ((HeightPx - VerticalSideHeightPx) / 2);

    /// <summary>Horizontal offset of the first column, <c>89</c>.</summary>
    public const int LeftMarginPx = 89;

    /// <summary>Vertical offset of the first row, <c>62</c>.</summary>
    public const int TopMarginPx = 62;

    /// <summary>The factor the cached points are scaled by, the reference's <c>infl</c>.</summary>
    private const int Inflate = 12;

    /// <summary>Centre followed by the six corners in the reference's numbering, all scaled by <see cref="Inflate"/>.</summary>
    private readonly Point[] _coords = new Point[7];

    /// <summary>Creates a cell at <paramref name="index"/> laid out at the origin.</summary>
    /// <param name="index">The cell's index on the board.</param>
    public BattleCell(int index)
    {
        Index = index;
        SetArea(Rect.Empty);
    }

    /// <summary>The cell's index on the board, <c>Cell::GetIndex()</c>.</summary>
    public int Index { get; }

    /// <summary>The rendered rectangle, <c>Cell::GetPos()</c>.</summary>
    public Rect Position { get; private set; }

    /// <summary>The battle object occupying the cell, <c>Cell::GetObject()</c>; <c>0</c> is empty ground.</summary>
    public int Object { get; set; }

    /// <summary>The unit standing on the cell, <c>Cell::GetUnit()</c>.</summary>
    public BattleUnit? Unit { get; private set; }

    /// <summary>
    /// Whether a unit could stand on the cell, <c>Cell::isPassable()</c>.
    /// </summary>
    /// <param name="checkForUnit">Whether an occupied cell counts as blocked.</param>
    /// <returns>True when the cell holds no obstacle and, if asked, no unit.</returns>
    public bool IsPassable(bool checkForUnit) => Object == 0 && (!checkForUnit || Unit == null);

    /// <summary>Places a unit on the cell or clears it, <c>Cell::SetUnit()</c>.</summary>
    /// <param name="unit">The unit, or <see langword="null"/> to clear the cell.</param>
    public void SetUnit(BattleUnit? unit) => Unit = unit;

    /// <summary>One of the seven cached points, scaled; <c>0</c> is the centre.</summary>
    /// <param name="index">The point's number in the reference.</param>
    /// <returns>The scaled point.</returns>
    internal Point GetCoord(int index) => _coords[index];

    /// <summary>Places the cell inside the battlefield rectangle, <c>Cell::SetArea()</c>.</summary>
    /// <param name="area">The battlefield's rectangle in window coordinates.</param>
    /// <remarks>参考：<c>battle_cell.cpp:256-277</c>。位置一改就要重建全部七个缓存点。</remarks>
    public void SetArea(Rect area)
    {
        int row = Index / BattleBoard.WidthInCells;
        int column = Index % BattleBoard.WidthInCells;
        int x = area.X + LeftMarginPx - ((row % 2) != 0 ? WidthPx / 2 : 0) + (WidthPx * column);
        int y = area.Y + TopMarginPx + (RowStepPx * row);

        Position = new Rect(x, y, WidthPx, HeightPx);

        int scaledX = Inflate * x;
        int scaledY = Inflate * y;
        int extraY = Inflate * (HeightPx - VerticalSideHeightPx) / 2;

        _coords[0] = new Point(scaledX + (Inflate * WidthPx / 2), scaledY + (Inflate * HeightPx / 2));
        _coords[1] = new Point(scaledX, scaledY + extraY);
        _coords[2] = new Point(_coords[0].X, scaledY);
        _coords[3] = new Point(scaledX + (Inflate * WidthPx), _coords[1].Y);
        _coords[5] = new Point(_coords[0].X, scaledY + (Inflate * HeightPx));
        _coords[4] = new Point(_coords[3].X, _coords[5].Y - extraY);
        _coords[6] = new Point(scaledX, _coords[4].Y);
    }

    /// <summary>Which eighth of the cell a point falls in, <c>Cell::GetTriangleDirection()</c>.</summary>
    /// <param name="destination">A point in window coordinates.</param>
    /// <returns>The direction whose slice holds the point, or <see cref="AttackDirection.Unknown"/> outside.</returns>
    /// <remarks>参考：<c>battle_cell.cpp:279-317</c>。切片按顺时针依次测试，先命中者胜。</remarks>
    public AttackDirection GetTriangleDirection(Point destination)
    {
        Point point = new(Inflate * destination.X, Inflate * destination.Y);

        Point oneHalf = Midpoint(_coords[1], _coords[2]);
        Point twoHalf = Midpoint(_coords[2], _coords[3]);
        Point fourHalf = Midpoint(_coords[4], _coords[5]);
        Point fiveHalf = Midpoint(_coords[5], _coords[6]);

        if (point == _coords[0])
        {
            return AttackDirection.Center;
        }

        if (InTriangle(point, _coords[0], _coords[1], oneHalf))
        {
            return AttackDirection.TopLeft;
        }

        if (InTriangle(point, _coords[0], oneHalf, twoHalf))
        {
            return AttackDirection.Top;
        }

        if (InTriangle(point, _coords[0], twoHalf, _coords[3]))
        {
            return AttackDirection.TopRight;
        }

        if (InTriangle(point, _coords[0], _coords[3], _coords[4]))
        {
            return AttackDirection.Right;
        }

        if (InTriangle(point, _coords[0], _coords[4], fourHalf))
        {
            return AttackDirection.BottomRight;
        }

        if (InTriangle(point, _coords[0], fourHalf, fiveHalf))
        {
            return AttackDirection.Bottom;
        }

        if (InTriangle(point, _coords[0], fiveHalf, _coords[6]))
        {
            return AttackDirection.BottomLeft;
        }

        if (InTriangle(point, _coords[0], _coords[1], _coords[6]))
        {
            return AttackDirection.Left;
        }

        return AttackDirection.Unknown;
    }

    /// <summary>Whether a point is inside the cell's hexagon, <c>Cell::isPositionIncludePoint()</c>.</summary>
    /// <param name="point">A point in window coordinates.</param>
    /// <returns><see langword="true"/> for anything but <see cref="AttackDirection.Unknown"/>.</returns>
    public bool IsPositionIncludePoint(Point point) => GetTriangleDirection(point) != AttackDirection.Unknown;

    /// <summary>The arithmetic midpoint of two points, truncated like the reference's integer division.</summary>
    private static Point Midpoint(Point first, Point second) => new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    /// <summary>
    /// The reference's <c>inABC()</c>: is <paramref name="point"/> on the same side of all three edges?
    /// </summary>
    /// <remarks>
    /// 三个叉积同号（含全零）即在内。点正好落在边上时结果依赖取整方向，参考也不做特殊处理。
    /// </remarks>
    private static bool InTriangle(Point point, Point first, Point second, Point third)
    {
        int a = ((first.X - point.X) * (second.Y - first.Y)) - ((second.X - first.X) * (first.Y - point.Y));
        int b = ((second.X - point.X) * (third.Y - second.Y)) - ((third.X - second.X) * (second.Y - point.Y));
        int c = ((third.X - point.X) * (first.Y - third.Y)) - ((first.X - third.X) * (third.Y - point.Y));

        return (a >= 0 && b >= 0 && c >= 0) || (a < 0 && b < 0 && c < 0);
    }
}
