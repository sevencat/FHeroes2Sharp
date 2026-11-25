using FHeroes2.Core.Geometry;
using FHeroes2.Game.Battle;

namespace FHeroes2.UI.Battle;

/// <summary>
/// How far a moving or flying unit's sprite is drawn away from the cell it still stands on,
/// the offset arithmetic of <c>_drawTroopSprite()</c> (<c>battle_interface.cpp:2069-2110</c>).
/// </summary>
/// <remarks>
/// 参考把这段写在 blit 之前、混在取图与护城河裁剪之间；这里抽成纯函数，好让「对角走一步应该横移多少」
/// 「飞行时第 n 帧停在线段的哪里」直接对着数字断言，而不必先造一套怪物动画数据。
/// </remarks>
public static class BattleTroopDisplacement
{
    /// <summary>
    /// Works out the sprite offset of a moving or flying unit.
    /// </summary>
    /// <param name="unitArea">The unit's current rectangle, <c>unit.GetRectPosition()</c>.</param>
    /// <param name="isFlying">
    /// Whether the unit can fly, <c>isAbilityPresent( FLYING )</c>. While moving, a flying unit glides both
    /// ways; a walker only slides vertically and steps sideways by the frame's <paramref name="frameXOffset"/>.
    /// </param>
    /// <param name="animationLength">How many frames the current sequence has, <c>animation.animationLength()</c>.</param>
    /// <param name="movementProgress">How far along the step the current frame is, <c>animation.movementProgress()</c>.</param>
    /// <param name="frameXOffset">The current frame's BIN horizontal offset, <c>animation.getCurrentFrameXOffset()</c>.</param>
    /// <param name="movingPosition">The point the step heads for, the reference's <c>_movingPos</c>.</param>
    /// <param name="flyingPosition">
    /// The point the current stretch of air left from, the reference's <c>_flyingPos</c>; only a flyer in
    /// the air has one.
    /// </param>
    /// <returns>The offset to add to the sprite's usual drawing point; <see cref="Point.Zero"/> for a unit at rest.</returns>
    public static Point GetOffset(
        Rect unitArea,
        bool isFlying,
        int animationLength,
        double movementProgress,
        int frameXOffset,
        Point? movingPosition,
        Point? flyingPosition)
    {
        if (flyingPosition is { } flying && movingPosition is { } stretchTarget)
        {
            // In the air: interpolate between the two ends of this stretch (battle_interface.cpp:2099-2110).
            int flightX = flying.X - unitArea.X;
            int flightY = flying.Y - unitArea.Y;

            return new Point(
                flightX + (int)((stretchTarget.X - flying.X) * movementProgress),
                flightY + (int)((stretchTarget.Y - flying.Y) * movementProgress));
        }

        if (movingPosition is not { } target || animationLength == 0)
        {
            return Point.Zero;
        }

        int moveX = target.X - unitArea.X;
        int moveY = target.Y - unitArea.Y;

        if (isFlying)
        {
            // A flying creature glides smoothly, diagonally or not (battle_interface.cpp:2084-2088).
            return new Point((int)(movementProgress * moveX), (int)(movementProgress * moveY));
        }

        if (moveY == 0)
        {
            // A horizontal step is drawn by the animation frames themselves.
            return Point.Zero;
        }

        // A diagonal step: the sprite drifts by the frame's own horizontal offset, half of it per side
        // (battle_interface.cpp:2090-2096).
        int driftX = -Math.Sign(moveX) * frameXOffset / 2;

        return new Point(driftX, (int)(movementProgress * moveY));
    }

    /// <summary>The direction of a step and the drift of its sprite, the two other values <c>_drawTroopSprite()</c> needs.</summary>
    /// <param name="Delta">How far the sprite has drifted; only a diagonal step drifts.</param>
    /// <param name="Direction">Which way the step goes, <see cref="CellDirection.Center"/> at rest.</param>
    public readonly record struct Movement(Point Delta, CellDirection Direction);

    /// <summary>
    /// The direction a step goes and the sprite drift the moat mask needs,
    /// <c>battle_interface.cpp:2076-2096</c>'s <c>movementDirection</c> and <c>movementDelta</c>.
    /// </summary>
    /// <param name="unitArea">The unit's current rectangle, <c>unit.GetRectPosition()</c>.</param>
    /// <param name="isFlying">Whether the unit can fly; a flyer glides and never drifts.</param>
    /// <param name="animationLength">How many frames the current sequence has.</param>
    /// <param name="movementProgress">How far along the step the current frame is.</param>
    /// <param name="frameXOffset">The current frame's BIN horizontal offset.</param>
    /// <param name="movingPosition">The point the step heads for, the reference's <c>_movingPos</c>.</param>
    /// <returns>The pair, both zero and centre when the unit stands still.</returns>
    public static Movement GetMovement(
        Rect unitArea,
        bool isFlying,
        int animationLength,
        double movementProgress,
        int frameXOffset,
        Point? movingPosition)
    {
        if (movingPosition is not { } target || animationLength == 0)
        {
            return new Movement(Point.Zero, CellDirection.Center);
        }

        int moveX = target.X - unitArea.X;
        int moveY = target.Y - unitArea.Y;

        CellDirection direction = BattleBoard.GetDirectionFromDelta(moveX, moveY);

        // 飞行单位平滑滑过去，没有「漂移」这回事；水平步也不漂（:2084-2096）。
        if (isFlying || moveY == 0)
        {
            return new Movement(Point.Zero, direction);
        }

        return new Movement(
            new Point(-Math.Sign(moveX) * frameXOffset / 2, (int)(movementProgress * moveY)), direction);
    }
}
