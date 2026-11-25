using FHeroes2.Assets.Maps;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The predicted action order of the remaining stacks, <c>Battle::UpdateOrderOfUnits()</c>
/// (<c>battle_arena.cpp:223-250</c>).
/// </summary>
/// <remarks>
/// <para>
/// 顺序条画的不是「本轮的原始队列」，而是「已经行动过的」加上「接下来会行动的」：历史按行动先后原样抄在最前面，
/// 后面用 <c>GetCurrentUnit( ..., ordersMode = true )</c> 反复取下一个可以行动的单位，每取一个就把它从那一边的
/// 待办里删掉，于是同速时的先手判定会像真的打下去一样左右交替。当前正在行动的单位不进列表——它已经不在「接下来」
/// 里了（<c>:242-244</c>）。
/// </para>
/// <para>
/// 计算与绘制分开：这里只认军队与单位，不认任何精灵。界面层拿它的输出按图标宽度裁剪后画出来。
/// </para>
/// </remarks>
public static class BattleOrderOfUnits
{
    /// <summary>
    /// Rebuilds the order list, <c>UpdateOrderOfUnits()</c>.
    /// </summary>
    /// <param name="attackingForce">The attacking army, <c>attackingArmy</c>.</param>
    /// <param name="defendingForce">The defending army, <c>defendingArmy</c>.</param>
    /// <param name="currentUnit">The stack acting right now, or <see langword="null"/> between turns.</param>
    /// <param name="preferredColor">
    /// Whose side breaks a speed tie, <c>preferredColor</c>; normally the opposite of the last army that acted.
    /// </param>
    /// <param name="orderHistory">The stacks that already acted this round, fastest-first within the round.</param>
    /// <returns>The history, then every stack still able to act, in the order they will.</returns>
    /// <exception cref="ArgumentNullException">A force, or the history, is <see langword="null"/>.</exception>
    public static BattleUnits Predict(BattleForce attackingForce, BattleForce defendingForce, BattleUnit? currentUnit,
        PlayerColor preferredColor, BattleUnits orderHistory)
    {
        ArgumentNullException.ThrowIfNull(attackingForce);
        ArgumentNullException.ThrowIfNull(defendingForce);
        ArgumentNullException.ThrowIfNull(orderHistory);

        BattleUnits order = new();

        foreach (BattleUnit unit in orderHistory)
        {
            order.Add(unit);
        }

        BattleUnits attackingUnits = attackingForce.ValidOnly();
        BattleUnits defendingUnits = defendingForce.ValidOnly();

        attackingUnits.SortFastest();
        defendingUnits.SortFastest();

        while (true)
        {
            BattleUnit? unit = TakeNext(attackingUnits, defendingUnits, preferredColor != defendingForce.Color);

            if (unit is null)
            {
                break;
            }

            if (ReferenceEquals(unit, currentUnit))
            {
                continue;
            }

            // 下一对同速单位的先手，看刚排进来的这个是谁的：对面先手。
            preferredColor = unit.Color == attackingForce.Color ? defendingForce.Color : attackingForce.Color;

            order.Add(unit);
        }

        return order;
    }

    /// <summary>
    /// The next stack to act, the <c>ordersMode</c> overload of <c>GetCurrentUnit()</c> (<c>battle_arena.cpp:169-203</c>).
    /// </summary>
    /// <param name="attackingUnits">The attacker's stacks, fastest first; the chosen one is removed.</param>
    /// <param name="defendingUnits">The defender's stacks, fastest first; the chosen one is removed.</param>
    /// <param name="attackersGoFirst">Whose side wins a speed tie.</param>
    /// <returns>The next stack, or <see langword="null"/> when neither side has one left.</returns>
    /// <remarks>
    /// 两边都取「第一个还能动的」再比速度；同速按 <paramref name="attackersGoFirst"/> 挑。取中之后只从它自己那一边的
    /// 列表里删掉一个（参考同样只删一个——速度为零的单位留在列表里，反正下一次取不到它）。
    /// </remarks>
    private static BattleUnit? TakeNext(BattleUnits attackingUnits, BattleUnits defendingUnits, bool attackersGoFirst)
    {
        BattleUnit? attacker = FindActingUnit(attackingUnits);
        BattleUnit? defender = FindActingUnit(defendingUnits);

        BattleUnit? result;

        if (attacker != null && defender != null)
        {
            if (attacker.GetSpeed() == defender.GetSpeed())
            {
                result = attackersGoFirst ? attacker : defender;
            }
            else
            {
                result = attacker.GetSpeed() > defender.GetSpeed() ? attacker : defender;
            }
        }
        else
        {
            result = attacker ?? defender;
        }

        if (result != null)
        {
            if (!attackingUnits.Remove(result))
            {
                defendingUnits.Remove(result);
            }
        }

        return result;
    }

    /// <summary>The first stack of a side whose speed is above <see cref="Speed.Standing"/>.</summary>
    /// <param name="units">One side's stacks, fastest first.</param>
    /// <returns>The stack, or <see langword="null"/> when that side is done for the round.</returns>
    private static BattleUnit? FindActingUnit(BattleUnits units)
    {
        foreach (BattleUnit unit in units)
        {
            if (unit.GetSpeed() > Speed.Standing)
            {
                return unit;
            }
        }

        return null;
    }
}
