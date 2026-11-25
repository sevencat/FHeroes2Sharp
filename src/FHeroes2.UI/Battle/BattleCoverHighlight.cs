using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The cells the pointer's shadow covers, the non-spell half of <c>Interface::RedrawCover()</c>
/// (<c>battle_interface.cpp:2360-2453</c>).
/// </summary>
/// <remarks>
/// <para>
/// 光标除了指示「按下去会发生什么」，还要把「发生在哪儿」摊开：宽单位走去一格时头尾两格都会占住，
/// 攻一刀时人真正站上去的是目标格的某个邻格（不是鼠标指着的格子），双击攻击还要再亮出背对侧的第二个被攻格，
/// 全域攻击则把相邻的敌兵全都圈起来。
/// </para>
/// <para>
/// 与 <see cref="BattleSpellHighlight"/> 一样只算不画：<see cref="BattleFieldPainter"/> 拿它给的格子贴阴影，
/// 于是这一层能对着竞技场单测。
/// </para>
/// </remarks>
public static class BattleCoverHighlight
{
    /// <summary>
    /// The cells to put a shadow under for one hover.
    /// </summary>
    /// <param name="arena">The battle, asked about reachability and who stands where.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <param name="cellIndex">The cell under the pointer.</param>
    /// <param name="cursorTheme">The cursor the hover shows.</param>
    /// <returns>The cells, the standing cell first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arena"/> or <paramref name="currentUnit"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 三支照参考的次序：宽单位的移动/飞行先问「头尾落在哪」，剑光标再问「人站哪、另外还打到谁」，
    /// 其余光标只亮鼠标格。宽单位走不动时退成只亮鼠标格——参考也是在取不到站位时不画。
    /// </remarks>
    public static IReadOnlyList<int> GetCells(BattleArena arena, BattleUnit currentUnit, int cellIndex, CursorTheme cursorTheme)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        if (currentUnit.IsWide && cursorTheme is CursorTheme.CombatMove or CursorTheme.CombatFly)
        {
            BattlePosition position = BattlePosition.GetReachable(arena, currentUnit, cellIndex);

            return position.IsEmpty ? [cellIndex] : [position.HeadIndex, position.TailIndex];
        }

        AttackDirection direction = BattleCursor.AttackDirectionOf(cursorTheme);

        if (direction == AttackDirection.Unknown)
        {
            return [cellIndex];
        }

        List<int> highlighted = [];
        BattlePosition attackPosition = BattleBoard.CalculateAttackPosition(arena, currentUnit, cellIndex, direction);

        if (!attackPosition.IsEmpty)
        {
            highlighted.Add(attackPosition.HeadIndex);

            if (attackPosition.Tail is not null)
            {
                highlighted.Add(attackPosition.TailIndex);
            }
        }

        if (currentUnit.IsDoubleCellAttack)
        {
            CellDirection attackingCellDirection = BattleBoard.GetAttackingCellDirection(currentUnit, direction);

            highlighted.Add(BattleBoard.GetIndexDirection(
                cellIndex,
                BattleBoard.GetReflectDirection(attackingCellDirection)));
        }
        else if (currentUnit.IsAllAdjacentCellsAttack)
        {
            foreach (int nearby in BattleBoard.GetAroundIndexes(attackPosition))
            {
                if (nearby == cellIndex
                    || arena.Board.GetCell(nearby)?.Unit is not { } nearbyUnit
                    || nearbyUnit.GetCurrentColor() == currentUnit.GetCurrentColor())
                {
                    continue;
                }

                highlighted.Add(nearby);
            }
        }

        if (highlighted.Count == 0)
        {
            highlighted.Add(cellIndex);
        }

        return highlighted;
    }
}
