using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Battle;

/// <summary>
/// Which slice of a unit's sprite the moat water hides,
/// <c>Interface::_drawTroopSpriteWithMoatMask()</c> (<c>battle_interface.cpp:2121-2200</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考在这里先把「这一步跨不跨河、跨进哪一格」算清楚，再问那一格的水面矩形，与单位的 sprite 求交；交集就是
/// 要挖掉的那一条。规则有四处细节：飞行中的单位不裁；正在出河（斜向）不裁；从左上斜着进河不裁（水面那格在
/// 身后，遮不到）；对角与在河里时水面要跟着位移走。
/// </para>
/// <para>
/// 计算留在游戏层，绘制留给界面——这样「哪些格要裁、裁到第几行」可以脱离资产单测。
/// </para>
/// </remarks>
public static class BattleMoatMasking
{
    /// <summary>A sprite this short is all water and legs: the reference gives up instead of drawing fragments.</summary>
    public const int ShortestVisibleSprite = 10;

    /// <summary>
    /// The part of a sprite the water covers, or <see langword="null"/> when nothing must be cut away.
    /// </summary>
    /// <param name="board">The battle field's cells.</param>
    /// <param name="unit">The unit whose sprite is being drawn.</param>
    /// <param name="bridge">The drawbridge, or <see langword="null"/> when the battle has none.</param>
    /// <param name="hasBuiltMoat">Whether the castle has a moat at all.</param>
    /// <param name="isFlyingInMotion">Whether this unit is the one currently in the air.</param>
    /// <param name="movementDirection">The direction of the step being drawn; <see cref="CellDirection.Center"/> when standing.</param>
    /// <param name="movementDelta">How far the sprite has already drifted, used to move the water along with it.</param>
    /// <param name="spriteArea">Where the sprite is blitted, before any moat treatment.</param>
    /// <returns>The water rectangle to cut out, already clipped to the sprite.</returns>
    public static Rect? GetMask(
        BattleBoardCells board,
        BattleUnit unit,
        BattleBridge? bridge,
        bool hasBuiltMoat,
        bool isFlyingInMotion,
        CellDirection movementDirection,
        Point movementDelta,
        Rect spriteArea)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(unit);

        // 没有护城河、或者这只正在天上飞：都不裁（:2127-2143）。
        if (!hasBuiltMoat || isFlyingInMotion)
        {
            return null;
        }

        (BattleCell? current, BattleCell? next) = BattleBoard.GetMoatCellsForUnit(board, unit, bridge, movementDirection);

        if (current == null && next == null)
        {
            return null;
        }

        bool isDiagonal = IsDiagonal(movementDirection);
        bool isEntering = current == null;
        bool isExiting = next == null;
        bool isInside = current != null && next != null;

        // 斜着出河不裁（脚已经抬起来了）；从左上斜着进河也不裁（水面在身后）。
        if (isDiagonal && (isExiting || (isEntering && movementDirection == CellDirection.TopRight)))
        {
            return null;
        }

        // 进河看下一格，其余看脚下这格（:2175）。
        BattleCell? maskCell = isEntering ? next : current;

        if (maskCell == null || BattleBoard.GetMoatCellMask(maskCell) is not { } mask)
        {
            return null;
        }

        // 对角步与在河里：水面跟着 sprite 一起漂（:2181-2186）。
        if (isInside || !isDiagonal)
        {
            mask = new Rect(mask.X - movementDelta.X, mask.Y + movementDelta.Y, mask.Width, mask.Height);
        }

        Rect intersection = spriteArea.Intersect(mask);

        if (intersection.IsEmpty)
        {
            return null;
        }

        // 矮小的 sprite 只剩脚泡在水里，参考宁可不画（:2193-2196）。
        return spriteArea.Height - mask.Height < ShortestVisibleSprite ? null : intersection;
    }

    /// <summary>
    /// The three slices a sprite is left with once the water is cut out,
    /// <c>_drawTroopSpriteWithMoatMask()</c>'s four blits minus the one for the band itself (<c>:2188-2200</c>).
    /// </summary>
    /// <param name="Top">Everything above the water, in sprite local coordinates.</param>
    /// <param name="Left">The strip left of the water, level with it.</param>
    /// <param name="Right">The strip right of the water, level with it.</param>
    /// <remarks>
    /// 参考只画这三块，水面之下（脚的位置）不画——这就是「站在河里」的样子。三个矩形都用 sprite 自己的坐标，
    /// 左上角是 <c>(0,0)</c>；两块侧条可能宽度为零，调用方直接交给 blit，空矩形自然什么都不画。
    /// </remarks>
    public readonly record struct SpritePieces(Rect Top, Rect Left, Rect Right);

    /// <summary>Cuts a sprite into the three slices the moat leaves visible.</summary>
    /// <param name="spriteArea">The sprite's rectangle in surface coordinates.</param>
    /// <param name="water">The water band, already clipped to the sprite, in surface coordinates.</param>
    /// <returns>The three source rectangles, in sprite local coordinates.</returns>
    public static SpritePieces GetVisiblePieces(Rect spriteArea, Rect water)
    {
        int topHeight = water.Y - spriteArea.Y;
        int leftWidth = water.X - spriteArea.X;
        int rightX = water.X + water.Width - spriteArea.X;
        int rightWidth = spriteArea.X + spriteArea.Width - (water.X + water.Width);

        return new SpritePieces(
            new Rect(0, 0, spriteArea.Width, topHeight),
            new Rect(0, topHeight, leftWidth, water.Height),
            new Rect(rightX, topHeight, rightWidth, water.Height));
    }

    /// <summary>Whether a step goes sideways as well as along a row, <c>Board::isDiagonal()</c>.</summary>
    /// <param name="direction">The direction of the step.</param>
    /// <returns><see langword="true"/> for the four diagonal directions.</returns>
    private static bool IsDiagonal(CellDirection direction) =>
        direction is CellDirection.TopLeft or CellDirection.TopRight or CellDirection.BottomLeft
            or CellDirection.BottomRight;
}
