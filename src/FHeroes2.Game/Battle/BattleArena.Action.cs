using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Battle;

/// <summary>
/// 行动执行：把一条 <see cref="BattleCommand"/> 变成战场上的变化，<c>Battle::Arena</c> 的
/// <c>battle_action.cpp</c>（<c>battle_action.cpp:278-759</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>battle_action.cpp</c> 有 1274 行，其中一半是界面动画：每走一格要等一帧、每次攻击要播三段动画。
/// 端口只落**不带界面的那一半**——移动、跳过、士气、攻击、施法、撤退、投降——这些动作改的是格子、模式位与
/// 战斗结果，与画面无关。塔、投石车与自动/快速战斗还没转，见 <see cref="ApplyAction"/> 里显式抛出的那些分支；
/// 施法在 <c>BattleArena.Spell.cs</c>，近战在 <c>BattleArena.Attack.cs</c>。
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
    /// Lowers the drawbridge when the unit needs it for the cell it is about to step on, part of <c>moveUnit</c>.
    /// </summary>
    /// <param name="unit">The unit about to move.</param>
    /// <param name="cellIndex">The cell it is stepping onto (the head cell for a flyer's landing).</param>
    /// <param name="tailIndex">The tail cell of a wide flyer's landing, or -1 when there is none.</param>
    /// <remarks>
    /// 逐格循环里的桥段（<c>battle_action.cpp:333-350</c>）。参考把它混在 <c>moveUnit</c> 的循环里，端口有两处调用者：
    /// 无界面时是 <see cref="MoveUnit"/> 自己挪格子，有界面时是 <c>BattleFieldPainter</c> 的移动播放（它逐格
    /// <c>SetPosition</c>，走完竞技场只补模式位，见 ADR-245）。所以这段逻辑不能只留在 <see cref="MoveUnit"/> 里，
    /// 否则有观战者的攻城永远不放下吊桥（ADR-246）。
    /// </remarks>
    public void LowerBridgeForStep(BattleUnit unit, int cellIndex, int tailIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (_bridge == null)
        {
            return;
        }

        if (_bridge.NeedDown(unit, cellIndex) || (tailIndex >= 0 && _bridge.NeedDown(unit, tailIndex)))
        {
            _bridge.ActionDown();
        }
    }

    /// <summary>Raises the drawbridge again once a step is done, part of <c>moveUnit</c>.</summary>
    /// <remarks>桥没放下、断了，或者还压在人和尸体上时什么也不做（<c>Bridge::AllowUp()</c>）。</remarks>
    public void RaiseBridgeAfterStep()
    {
        if (_bridge != null && _bridge.AllowUp)
        {
            _bridge.ActionUp();
        }
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
            LowerBridgeForStep(unit, position.HeadIndex, unit.IsWide ? position.TailIndex : -1);

            unit.SetPosition(position);

            RaiseBridgeAfterStep();
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
                LowerBridgeForStep(unit, cellIndex);

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

                RaiseBridgeAfterStep();
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
    /// The command is one <c>battle_action.cpp</c> has but <see cref="BattleCommandType"/> does not: the reference's
    /// dispatcher is a plain <c>switch</c> over all eleven types, so nothing is expected to land here.
    /// </exception>
    /// <remarks>
    /// 分发器只认得已经落地的命令，其余显式抛出：一条被悄悄吞掉的命令会让战斗停在原地而没有任何迹象，那种 bug
    /// 比一个异常难查得多。
    /// </remarks>
    public void ApplyAction(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        switch (command.Type)
        {
            case BattleCommandType.Move:
                ApplyActionMove(command);
                break;

            case BattleCommandType.Attack:
                ApplyActionAttack(command);
                break;

            case BattleCommandType.Spellcast:
                ApplyActionSpellCast(command);
                break;

            case BattleCommandType.Skip:
                ApplyActionSkip(command);
                break;

            case BattleCommandType.Morale:
                ApplyActionMorale(command);
                break;

            case BattleCommandType.Tower:
                ApplyActionTower(command);
                break;

            case BattleCommandType.Catapult:
                ApplyActionCatapult(command);
                break;

            case BattleCommandType.Retreat:
                ApplyActionRetreat();
                break;

            case BattleCommandType.Surrender:
                ApplyActionSurrender();
                break;

            case BattleCommandType.ToggleAutoCombat:
                ApplyActionToggleAutoCombat(command);
                break;

            case BattleCommandType.QuickCombat:
                ApplyActionQuickCombat();
                break;

            default:
                throw new NotSupportedException(
                    $"The '{command.Type}' command is not ported yet; battle_action.cpp's tower, catapult and "
                    + "battle-ending actions are still pending.");
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

        // 界面层可能已经把这一趟走完了：BattleFieldPainter 的 MovePlayback 每播完一格就 SetPosition，播到终点
        // 时单位就站在命令指定的格子上（参考把这套阻塞循环放在 ApplyActionMove 里，见 battle_action.cpp:278-365；
        // 端口拆成「先播、后结算」，见 battle_interface.cpp:4524-4830 的对应段）。
        // 拆开之后「已经在终点」不是非法参数，而是另一半已经做完：补上模式位收工，不要再走一次、也不要再校验一次
        // ——CheckMoveParams 会把「原地移动」判成非法，那正是这里的情形。
        if (unit.HeadIndex == destination)
        {
            unit.SetModes(BattleMonsterState.Moved);

            return;
        }

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

    /// <summary>
    /// Ends the battle with the current side running away, <c>Arena::ApplyActionRetreat()</c>
    /// (<c>battle_action.cpp:761-786</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The side is not allowed to retreat.</exception>
    /// <remarks>
    /// 撤退只写结果标志，不动军队：跑掉的那些兵由战后结算按「撤退」处理（在 <c>BattleLoader</c> 那一侧）。
    /// </remarks>
    public void ApplyActionRetreat()
    {
        PlayerColor currentColor = GetCurrentColor();

        if (!CanRetreatOpponent(currentColor))
        {
            throw new InvalidOperationException("This side is not allowed to retreat.");
        }

        if (AttackingArmyColor == currentColor)
        {
            _battleResult.Attacker = BattleOutcome.Retreat;
        }
        else if (DefendingArmyColor == currentColor)
        {
            _battleResult.Defender = BattleOutcome.Retreat;
        }
        else
        {
            throw new InvalidOperationException("The current colour belongs to neither force.");
        }
    }

    /// <summary>
    /// Ends the battle with the current side buying its way out, <c>Arena::ApplyActionSurrender()</c>
    /// (<c>battle_action.cpp:788-854</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The side may not surrender, or its kingdom cannot pay the ransom.
    /// </exception>
    /// <remarks>
    /// <para>
    /// 赎金按「对面那支军队的幸存者买回价」算（<see cref="BattleForce.GetSurrenderCost"/>），由投降方的王国付给对面王国。
    /// 参考通过全局 <c>world</c> 找王国，端口用 <see cref="World.Current"/>，与 AI 判断「付得起吗」走同一个入口。
    /// </para>
    /// <para>
    /// 注意只动金库，不动军队：跑掉/被赎走的兵怎么处理是战后结算的事。
    /// </para>
    /// </remarks>
    public void ApplyActionSurrender()
    {
        PlayerColor currentColor = GetCurrentColor();

        if (AttackingArmyColor != currentColor && DefendingArmyColor != currentColor)
        {
            throw new InvalidOperationException("The current colour belongs to neither force.");
        }

        if (!CanSurrenderOpponent(currentColor))
        {
            throw new InvalidOperationException("This side is not allowed to surrender.");
        }

        BattleForce surrendering = GetForce(currentColor);
        BattleForce winning = GetForce(GetOppositeColor(currentColor));
        Funds cost = new(Resource.Gold, surrendering.GetSurrenderCost());

        World world = World.Current;

        if (!world.GetKingdom(currentColor).AllowPayment(cost))
        {
            throw new InvalidOperationException("The kingdom cannot pay the ransom.");
        }

        world.GetKingdom(currentColor).OddFundsResource(cost);
        world.GetKingdom(winning.Color).AddFundsResource(cost);

        if (AttackingArmyColor == currentColor)
        {
            _battleResult.Attacker = BattleOutcome.Surrender;
        }
        else
        {
            _battleResult.Defender = BattleOutcome.Surrender;
        }
    }

    /// <summary>
    /// Turns auto combat on or off for one side, <c>Arena::ApplyActionToggleAutoCombat()</c>
    /// (<c>battle_action.cpp:1297-1340</c>).
    /// </summary>
    /// <param name="command">Toggle command: the colour.</param>
    /// <exception cref="ArgumentException">The colour is not in this battle, or its side is already run by the AI.</exception>
    /// <remarks>
    /// 标志按颜色做异或，所以同一条命令连发两次等于没发。AI 自己的一方不许切——它本来就不等玩家。
    /// </remarks>
    public void ApplyActionToggleAutoCombat(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        PlayerColor color = (PlayerColor)command.GetNextValue();

        if (color != AttackingArmyColor && color != DefendingArmyColor)
        {
            throw new ArgumentException("That colour is not in this battle.", nameof(command));
        }

        if (GetForce(color).GetControl() == PlayerControl.Ai)
        {
            throw new ArgumentException("An AI-controlled side cannot toggle auto combat.", nameof(command));
        }

        _autoCombatColors ^= color;
    }

    /// <summary>
    /// Hands the rest of the battle to auto combat, <c>Arena::ApplyActionQuickCombat()</c>
    /// (<c>battle_action.cpp:1342-1370</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Neither side is human, so there is nothing to finish.</exception>
    /// <remarks>
    /// 只对玩家一方生效（AI 一方本来就在自动打）。参考还顺手丢掉 <c>_interface</c>，让剩下的回合不再等动画；
    /// 端口没有界面，这一步对应的是「不再呈现」。
    /// </remarks>
    public void ApplyActionQuickCombat()
    {
        PlayerControl attackingControl = _attackingForce.GetControl();
        PlayerControl defendingControl = _defendingForce.GetControl();

        if (attackingControl != PlayerControl.Human && defendingControl != PlayerControl.Human)
        {
            throw new InvalidOperationException("Neither side is human, so there is nothing to finish.");
        }

        EnableAutoCombatForHumanSides();
    }

    /// <summary>
    /// Hands every human-controlled side to the AI, <c>Arena::_autoCombatColors</c> set for the human sides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两个调用者：<see cref="ApplyActionQuickCombat"/>，以及「没有界面」的战斗——参考在竞技场构造函数里
    /// 直接给人类那一方打上自动战斗（<c>battle_arena.cpp:372-380</c>），端口在 <see cref="BattleLoader"/>
    /// 决定不放映时调这里，效果相同。
    /// </para>
    /// <para>
    /// 只处理人类那一方：AI 那一位本来就不等玩家，把它的颜色也记进来会让
    /// <see cref="AutoCombatInProgress"/> 断言失败。
    /// </para>
    /// </remarks>
    public void EnableAutoCombatForHumanSides()
    {
        if (_attackingForce.GetControl() == PlayerControl.Human)
        {
            _autoCombatColors |= AttackingArmyColor;
        }

        if (_defendingForce.GetControl() == PlayerControl.Human)
        {
            _autoCombatColors |= DefendingArmyColor;
        }
    }
}
