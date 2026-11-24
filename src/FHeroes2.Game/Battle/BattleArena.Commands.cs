using System.Diagnostics;

using FHeroes2.Game.Ai;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The commands a player picks by clicking the battlefield, the port's stand-in for the decision part of
/// <c>Interface::HumanBattleTurn()</c> (<c>battle_interface.cpp:3210-...</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的人类回合把「光标是什么」和「点了要下什么命令」写在一起：<c>GetBattleCursor()</c> 决定光标主题，
/// <c>HumanBattleTurn()</c> 在左键落下时按主题拼 <c>BattleCommand</c>。端口没有把光标主题搬进游戏层，
/// 所以这里只留「点哪一格 → 下哪条命令」这一段纯决策：界面负责把像素换成格子下标，
/// 再把这里给出的命令交给 <see cref="PlayCurrentUnitTurn(IReadOnlyList{BattleCommand})"/>。
/// </para>
/// <para>
/// 攻击命令的拼法与 AI 对齐：已经贴身时直接出刀（方向由竞技场自己算），否则先找「绕着目标、这一回合最便宜
/// 能站住的那一格」再算攻击向量。AI 用的是 <c>GetUnitMovementTarget()</c>，但它对「目标格本身站不下」的情形
/// 是断言，而一次点击只能回答「没有命令」——所以这里自己绕一圈邻格，顺序与判据与 AI 相同。
/// 这样人类与电脑点同一格会得到同一条命令，随机流也一致。
/// </para>
/// </remarks>
public sealed partial class BattleArena
{
    /// <summary>
    /// The move a player picks by clicking an empty cell, or <see langword="null"/> when the unit cannot
    /// stand there this turn.
    /// </summary>
    /// <param name="unit">The unit that would move; it must be the current unit's own stack.</param>
    /// <param name="cellIndex">The clicked cell.</param>
    /// <returns>The command, or <see langword="null"/> when the click asks for nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="unit"/> is <see langword="null"/>.</exception>
    public BattleCommand? CreateMoveCommand(BattleUnit unit, int cellIndex)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!unit.IsValid || !BattleBoard.IsValidIndex(cellIndex))
        {
            return null;
        }

        BattlePosition position = BattlePosition.GetPosition(unit, cellIndex);

        if (!position.IsValidForUnit(unit) || !IsPositionReachable(unit, position, isOnCurrentTurn: true))
        {
            return null;
        }

        return BattleCommand.Move((int)unit.Uid, cellIndex);
    }

    /// <summary>
    /// The attack a player picks by clicking an enemy's cell, or <see langword="null"/> when the unit
    /// cannot reach a cell next to it this turn.
    /// </summary>
    /// <param name="attacker">The unit that would attack; it must be the current unit's own stack.</param>
    /// <param name="targetCell">The clicked cell, which has to hold an enemy.</param>
    /// <returns>The command, or <see langword="null"/> when the click asks for nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="attacker"/> is <see langword="null"/>.</exception>
    public BattleCommand? CreateAttackCommand(BattleUnit attacker, int targetCell)
    {
        ArgumentNullException.ThrowIfNull(attacker);

        if (!attacker.IsValid || !BattleBoard.IsValidIndex(targetCell))
        {
            return null;
        }

        if (GetTroopBoard(targetCell) is not { } defender || defender.GetCurrentColor() == attacker.GetCurrentColor())
        {
            return null;
        }

        // Already in contact: the blow needs no walk, and the arena works the direction out itself.
        if (BattleBoard.GetDistance(attacker.Position, defender.Position) == 1)
        {
            return BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, defender.HeadIndex, -1);
        }

        // Otherwise walk next to it: the cheapest cell around the defender this unit can reach now.
        // (The AI asks GetUnitMovementTarget() instead, which asserts when the defender's own cell
        // cannot hold the attacker; a click has to answer "no" instead of asserting.)
        BattlePosition attackPosition = new(_board);
        uint cheapest = uint.MaxValue;

        foreach (int around in BattleBoard.GetAroundIndexes(defender.Position))
        {
            BattlePosition candidate = BattlePosition.GetReachable(this, attacker, around);

            if (!candidate.IsValidForUnit(attacker))
            {
                continue;
            }

            uint cost = CalculateMoveCost(attacker, candidate);

            if (cost < cheapest)
            {
                cheapest = cost;
                attackPosition = candidate;
            }
        }

        if (attackPosition.Head is null)
        {
            return null;
        }

        (int attackTarget, CellDirection direction) =
            BattleTactics.OptimalAttackVector(this, attacker, defender, attackPosition);

        Debug.Assert(BattleBoard.IsValidIndex(attackTarget), "The attack vector must name a cell on the board.");

        int move = attacker.HeadIndex == attackPosition.HeadIndex ? -1 : attackPosition.HeadIndex;

        return BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, move, attackTarget, (int)direction);
    }
}
