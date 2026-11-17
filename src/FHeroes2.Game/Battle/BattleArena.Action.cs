using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Battle;

/// <summary>
/// 行动执行：把一条 <see cref="BattleCommand"/> 变成战场上的变化，<c>Battle::Arena</c> 的
/// <c>battle_action.cpp</c>（<c>battle_action.cpp:278-759</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>battle_action.cpp</c> 有 1274 行，其中一半是界面动画：每走一格要等一帧、每次攻击要播三段动画。
/// 端口先落地**不带界面的那一半**——移动、跳过、士气、撤退、投降——这些动作改的是格子、模式位与战斗结果，
/// 与画面无关。攻击、法术、塔与投石车的执行要等 <see cref="BattleUnit"/> 的反击判定与
/// <c>TargetsApplyDamage</c> 一起做，见 <see cref="ApplyAction"/> 里显式抛出的那些分支。
/// </para>
/// <para>
/// 错误处理与参考不同：参考对非法参数打日志、在调试构建里 <c>assert( 0 )</c>，发布构建里静默返回。端口一律抛
/// <see cref="ArgumentException"/>——战斗命令只可能来自界面或 AI，参数错了是编程错误，静默吞掉只会把 bug 藏到
/// 很远的结算里。
/// </para>
/// </remarks>
public sealed partial class BattleArena
{
    /// <summary>
    /// Whether a unit could be ordered onto a cell, <c>checkMoveParams()</c>
    /// (<c>battle_action.cpp:122-140</c>).
    /// </summary>
    /// <param name="unit">The unit that would move.</param>
    /// <param name="destination">The cell it is sent to.</param>
    /// <returns>True when the cell is the head of a position the unit can reach this turn.</returns>
    /// <remarks>
    /// 三条否决：单位不存在或已死、目标就是它现在站的头格（「原地移动」不是移动）、可达位置的**头**不在目标格上——
    /// 最后一条排除了宽单位「倒着蹭过去」的解，参考要求调用方指定头格。
    /// </remarks>
    public bool CheckMoveParams(BattleUnit? unit, int destination)
    {
        if (unit == null || !unit.IsValid)
        {
            return false;
        }

        if (unit.HeadIndex == destination)
        {
            return false;
        }

        BattlePosition position = BattlePosition.GetReachable(this, unit, destination);

        return position.HeadIndex == destination;
    }

    /// <summary>
    /// Walks a unit to a cell, <c>Arena::moveUnit()</c> (<c>battle_action.cpp:278-365</c>).
    /// </summary>
    /// <param name="unit">The unit to move.</param>
    /// <param name="destination">The cell its head should end up on.</param>
    /// <remarks>
    /// <para>
    /// 两类移动分开写：飞行单位一步落地（参考把这一步交给动画层，动画层再回调 <c>SetPosition</c>），步行单位按
    /// 寻路器给的路径一格一格挪，途中可能为了「尾巴先过去」而原地转身。
    /// </para>
    /// <para>
    /// 参考只在**有桥**时才真的逐格挪（<c>battle_action.cpp:333-350</c>）：没有界面也没桥的野战里它会原地不动，
    /// 因为实际游戏里这个分支永远有界面接管。端口没有界面，于是把逐格挪提成主路径，桥的升降作为附加动作。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The move is not one the unit can make.</exception>
    public void MoveUnit(BattleUnit unit, int destination)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (destination < 0)
        {
            return;
        }

        RefreshBridgePassability(unit);

        if (!CheckMoveParams(unit, destination))
        {
            throw new ArgumentException("The unit cannot move to that cell this turn.", nameof(destination));
        }

        BattlePosition position = BattlePosition.GetReachable(this, unit, destination);

        if (unit.IsFlying)
        {
            unit.UpdateDirection(position.GetRect());

            if (unit.IsReflect != position.IsReflect)
            {
                position.Swap();
            }

            // Lower the bridge if the unit needs to land on it, then raise it again behind the unit.
            if (_bridge != null)
            {
                int destinationTail = unit.IsWide ? position.TailIndex : -1;

                if (_bridge.NeedDown(unit, position.HeadIndex) || (unit.IsWide && _bridge.NeedDown(unit, destinationTail)))
                {
                    _bridge.ActionDown();
                }
            }

            unit.SetPosition(position);

            if (_bridge != null && _bridge.AllowUp)
            {
                _bridge.ActionUp();
            }
        }
        else
        {
            int initialHead = unit.HeadIndex;
            int[] path = GetPath(unit, position);

            if (path.Length == 0)
            {
                throw new ArgumentException("The unit has no path to that cell.", nameof(destination));
            }

            foreach (int cellIndex in path)
            {
                if (_bridge != null && _bridge.NeedDown(unit, cellIndex))
                {
                    _bridge.ActionDown();
                }

                if (unit.IsWide && unit.TailIndex == cellIndex)
                {
                    // Stepping onto its own tail: the unit turns instead of sliding, which is how a wide unit
                    // moves half a cell sideways.
                    unit.SetReflection(!unit.IsReflect);
                }
                else
                {
                    unit.SetPosition(cellIndex);
                }

                if (_bridge != null && _bridge.AllowUp)
                {
                    _bridge.ActionUp();
                }
            }

            // The path names head cells; the last two entries decide the final facing.
            if (unit.IsWide)
            {
                int destinationHead = path[^1];
                int destinationTail = path.Length > 1 ? path[^2] : initialHead;

                position = new BattlePosition(_board);
                position.Set(
                    destinationHead,
                    true,
                    BattleCellDirections.IsRightSide(BattleBoard.GetDirection(destinationHead, destinationTail)));
            }
            else
            {
                position.Set(path[^1], false, unit.IsReflect);
            }

            unit.SetPosition(position);
        }

        unit.UpdateDirection();
    }

    /// <summary>
    /// Asks the bridge whether the unit may cross it and writes the answer into the gate cell,
    /// <c>Bridge::SetPassability()</c>.
    /// </summary>
    /// <param name="unit">The unit that is about to act.</param>
    /// <remarks>
    /// 参考在 <c>Turns()</c> 里、每个单位行动之前调它一次（<c>battle_arena.cpp:503-505</c>）：桥的通不通被**烘**进
    /// 闸门格的 <c>objectType</c>，寻路器只读格子、不回头问桥。端口还没有回合循环，于是把这次刷新挂在两个入口上
    /// ——<see cref="ApplyActionMove"/> 与 <see cref="MoveUnit"/>。副作用是给别的单位看到的闸门格状态可能过期，
    /// 但参考里同样如此（它也只按「正在行动的那一支」重算）。
    /// </remarks>
    private void RefreshBridgePassability(BattleUnit unit)
    {
        _bridge?.SetPassability(unit);
    }

    /// <summary>
    /// Executes one battle command, <c>Arena::ApplyAction()</c> (<c>battle_action.cpp:367-410</c>).
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <exception cref="NotSupportedException">
    /// The command belongs to a part of <c>battle_action.cpp</c> the port has not taken yet: attack, spell cast,
    /// tower, catapult, retreat, surrender, auto combat or quick combat.
    /// </exception>
    /// <remarks>
    /// 分发器只认得已经落地的四种命令，其余显式抛出：一条被悄悄吞掉的 <c>ATTACK</c> 会让战斗停在原地而没有任何
    /// 迹象，那种 bug 比一个异常难查得多。
    /// </remarks>
    public void ApplyAction(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        switch (command.Type)
        {
            case BattleCommandType.Move:
                ApplyActionMove(command);
                break;

            case BattleCommandType.Skip:
                ApplyActionSkip(command);
                break;

            case BattleCommandType.Morale:
                ApplyActionMorale(command);
                break;

            default:
                throw new NotSupportedException(
                    $"The '{command.Type}' command is not ported yet; battle_action.cpp's attack, spell, tower, "
                    + "catapult and battle-ending actions are still pending.");
        }
    }

    /// <summary>
    /// Moves a unit, <c>Arena::ApplyActionMove()</c> (<c>battle_action.cpp:629-666</c>).
    /// </summary>
    /// <param name="command">A command carrying the unit UID and the destination cell.</param>
    /// <remarks>
    /// 移动之后打上 <see cref="BattleMonsterState.Moved"/>，这就是「这个单位本回合已经动过」的唯一记法；下一次
    /// 行动时 <see cref="BattleUnit.GetSpeed"/> 会把它算成零移动力。
    /// </remarks>
    /// <exception cref="ArgumentException">The unit is unknown, dead, already moved, or cannot reach the cell.</exception>
    public void ApplyActionMove(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        uint uid = (uint)command.GetNextValue();
        int destination = command.GetNextValue();

        BattleUnit? unit = GetTroopUID(uid);

        if (unit == null || !unit.IsValid)
        {
            throw new ArgumentException("The command names a unit that is not on the battlefield.", nameof(command));
        }

        if (unit.HasModes(BattleMonsterState.Moved))
        {
            throw new ArgumentException("The unit has already acted this turn.", nameof(command));
        }

        RefreshBridgePassability(unit);

        if (!CheckMoveParams(unit, destination))
        {
            throw new ArgumentException("The unit cannot move to that cell this turn.", nameof(command));
        }

        MoveUnit(unit, destination);

        unit.SetModes(BattleMonsterState.Moved);
    }

    /// <summary>
    /// Skips a unit's turn, <c>Arena::ApplyActionSkip()</c> (<c>battle_action.cpp:668-705</c>).
    /// </summary>
    /// <param name="command">A command carrying the unit UID.</param>
    /// <remarks>
    /// 两个模式位一起打：<see cref="BattleMonsterState.Skip"/> 是「它选了跳过」（AI 与统计要区分跳过与没轮到），
    /// <see cref="BattleMonsterState.Moved"/> 是「它本回合不能再动」。
    /// </remarks>
    /// <exception cref="ArgumentException">The unit is unknown, dead, or has already acted.</exception>
    public void ApplyActionSkip(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        uint uid = (uint)command.GetNextValue();
        BattleUnit? unit = GetTroopUID(uid);

        if (unit == null || !unit.IsValid)
        {
            throw new ArgumentException("The command names a unit that is not on the battlefield.", nameof(command));
        }

        if (unit.HasModes(BattleMonsterState.Moved))
        {
            throw new ArgumentException("The unit has already acted this turn.", nameof(command));
        }

        unit.SetModes(BattleMonsterState.Skip);
        unit.SetModes(BattleMonsterState.Moved);
    }

    /// <summary>
    /// Answers the morale question of a unit, <c>Arena::ApplyActionMorale()</c>
    /// (<c>battle_action.cpp:707-759</c>).
    /// </summary>
    /// <param name="command">A command carrying the unit UID and the morale (non-zero for good).</param>
    /// <remarks>
    /// <para>
    /// 好士气是**额外一次行动**：清掉 <see cref="BattleMonsterState.Moved"/> 与 <see cref="BattleMonsterState.MoraleGood"/>，
    /// 于是这个单位本回合还能再动一次，而好士气本身不再触发第二遍。
    /// </para>
    /// <para>
    /// 坏士气是**失去这次行动**：只清 <see cref="BattleMonsterState.MoraleBad"/>，再打上
    /// <see cref="BattleMonsterState.Moved"/>。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The unit is unknown, dead, or the morale answer does not match what the unit is waiting for.
    /// </exception>
    public void ApplyActionMorale(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        uint uid = (uint)command.GetNextValue();
        bool morale = command.GetNextValue() != 0;

        BattleUnit? unit = GetTroopUID(uid);

        if (unit == null || !unit.IsValid)
        {
            throw new ArgumentException("The command names a unit that is not on the battlefield.", nameof(command));
        }

        if (morale)
        {
            // Good morale only makes sense for a unit that has acted and is still flagged as lucky.
            if (!unit.HasAllModes(BattleMonsterState.Moved | BattleMonsterState.MoraleGood))
            {
                throw new ArgumentException("The unit is not waiting on a good morale answer.", nameof(command));
            }

            unit.ResetModes(BattleMonsterState.Moved | BattleMonsterState.MoraleGood);
        }
        else
        {
            if (!unit.HasModes(BattleMonsterState.MoraleBad) || unit.HasModes(BattleMonsterState.Moved))
            {
                throw new ArgumentException("The unit is not waiting on a bad morale answer.", nameof(command));
            }

            unit.ResetModes(BattleMonsterState.MoraleBad);
            unit.SetModes(BattleMonsterState.Moved);
        }
    }
}
