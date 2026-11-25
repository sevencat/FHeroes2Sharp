using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Battle;

/// <summary>
/// Hands a unit's turn to whoever plays it, the port's replacement for <c>Interface::HumanTurn()</c>
/// (<c>battle_arena.cpp:513</c>).
/// </summary>
/// <param name="arena">The arena the unit acts in.</param>
/// <param name="unit">The unit whose turn it is.</param>
/// <param name="actions">The list to fill; the arena applies the commands in order.</param>
/// <remarks>
/// 参考的人类回合住在 <c>Interface</c> 里，端口没有界面层，所以把「谁来出这一手」抽成回调。AI 控制的单位不走这里：
/// <see cref="BattleArena.Turns"/> 直接问 <see cref="BattlePlanner"/>。回调给 <see langword="null"/> 时人类控制的单位
/// 也交给 AI——那等价于参考在无界面时的自动战斗，只是端口没有那面「正在自动战斗」的旗子。
/// </remarks>
public delegate void BattleUnitTurnHandler(BattleArena arena, BattleUnit unit, List<BattleCommand> actions);

/// <summary>
/// 回合循环：轮询出手、执行动作、结算胜负，<c>Battle::Arena::Turns()</c> 与
/// <c>Battle::Arena::UnitTurn()</c>（<c>battle_arena.cpp:460-682</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 参考的回合循环与界面长在一起（每个动作播一段动画、每个单位行动前重画顺序条），端口把界面剥离后剩下三段：
/// 回合开始（两军 <c>NewTurn()</c>）、逐单位行动（<see cref="UnitTurn"/>）、收尾结算（<see cref="SettleResult"/>）。
/// </para>
/// <para>
/// <b>轮内分支</b>：投石车（<c>CatapultAction()</c>）与城堡塔（<c>TowerAction()</c>）各有一个「本轮已开火」的旗子，
/// 开火时机与参考一致；顺序条要的 <c>_orderOfUnits</c> 也在这一层维护（见 <see cref="OrderOfUnits"/>），界面只负责画。
/// </para>
/// </remarks>
public sealed partial class BattleArena
{
    /// <summary>Whether the catapult already fired in this round, <c>catapultActed</c> of <c>Turns()</c>.</summary>
    private bool _roundCatapultActed;

    /// <summary>Whether the towers already fired in this round, <c>towersActed</c> of <c>Turns()</c>.</summary>
    private bool _roundTowersActed;

    /// <summary>The stacks that already acted this round, <c>orderHistory</c> of <c>Turns()</c>.</summary>
    private readonly BattleUnits _orderHistory = new();

    /// <summary>The order the bar draws, <c>_orderOfUnits</c> of <c>Interface</c>.</summary>
    private readonly BattleUnits _orderOfUnits = new();

    /// <summary>
    /// The predicted order of the remaining stacks, the <c>_orderOfUnits</c> the turn order bar draws
    /// (<c>battle_interface.cpp:1650</c>).
    /// </summary>
    /// <remarks>
    /// 与参考的一处差别：参考只在界面要的时候（<c>_orderOfUnits</c> 非空）才维护这份列表，端口没有那面旗子，
    /// 一律维护。代价是每次动作多排一遍最多二十个单位，好处是没有界面时也能在测试里断言顺序。
    /// </remarks>
    public IReadOnlyList<BattleUnit> OrderOfUnits => _orderOfUnits;

    /// <summary>
    /// Runs one whole round, <c>Battle::Arena::Turns()</c> (<c>battle_arena.cpp:552-682</c>).
    /// </summary>
    /// <param name="humanTurn">
    /// Who plays the human-controlled units, or <see langword="null"/> to let the AI play them too.
    /// </param>
    /// <remarks>
    /// 参考在循环开始前把两军都 <c>NewTurn()</c> 一遍：再生补血、清掉「本回合已行动」、限时法术时长减一。这两步
    /// 必须在取第一个出手单位**之前**做，否则上一回合的 <c>TR_MOVED</c> 会让全场没人能动。
    /// </remarks>
    public void Turns(BattleUnitTurnHandler? humanTurn = null)
    {
        BeginRound();

        while (BattleValid())
        {
            BattleUnit? next = SelectNextUnit();

            PlayCastleActions();

            if (!BattleValid() || next is null)
            {
                // 塔可能打死了最后一个敌人；也可能场上已经没有人能出手（全部被定身或打光）。
                break;
            }

            UnitTurn(humanTurn);
        }

        EndRound();
    }

    /// <summary>
    /// Starts a round, the head of <c>Turns()</c>: <c>StartNewRound()</c>, both armies' <c>NewTurn()</c>
    /// and the once-per-round flags of the castle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 帧驱动的界面不能像参考那样在 <c>Turns()</c> 里阻塞等玩家：它每帧只推进一个单位，所以回合被拆成
    /// <see cref="BeginRound"/> → 若干次 <see cref="SelectNextUnit"/> + <see cref="PlayCastleActions"/> +
    /// <see cref="PlayCurrentUnitTurn(BattleUnitTurnHandler?)"/> → <see cref="EndRound"/>。
    /// <see cref="Turns"/> 本身就是这套调用的一个循环封装，两边的语义完全相同。
    /// </para>
    /// </remarks>
    public void BeginRound()
    {
        StartNewRound();

        _attackingForce.NewTurn();
        _defendingForce.NewTurn();

        _roundCatapultActed = false;
        _roundTowersActed = false;

        // 新一轮从空历史开始：此时还没有人行动，先手看上一轮最后行动的是谁（battle_arena.cpp:565-573）。
        _orderHistory.Clear();

        UpdateOrderOfUnits(GetOppositeColor(_lastActiveUnitArmyColor));
    }

    /// <summary>
    /// Picks the unit that acts next, the <c>_currentUnit = GetCurrentUnit()</c> of <c>Turns()</c>.
    /// </summary>
    /// <returns>The unit, or <see langword="null"/> when nobody can act any more this round.</returns>
    public BattleUnit? SelectNextUnit()
    {
        _currentUnit = GetCurrentUnit();

        // 选中的单位进入本轮历史，顺序条把它画成「已行动」（battle_arena.cpp:583-592）。
        if (_currentUnit != null)
        {
            _orderHistory.Add(_currentUnit);
        }

        UpdateOrderOfUnits(GetOppositeColor(_currentUnit is null ? _lastActiveUnitArmyColor : _currentUnit.Color));

        return _currentUnit;
    }

    /// <summary>
    /// Lets the castle fire once per round: the catapult on the attacker's first turn and the three
    /// towers on the defender's, the block at <c>battle_arena.cpp:560-600</c> inside <c>Turns()</c>.
    /// </summary>
    /// <remarks>
    /// 攻方一个都动不了（全被致盲）时投石车在这一轮末尾开火，守方一个都动不了时塔也一样——所以这一步要在
    /// 「选完下一个单位」之后、并且即使没人能出手也要跑一次。
    /// </remarks>
    public void PlayCastleActions()
    {
        if (_castle == null)
        {
            return;
        }

        if (!_roundCatapultActed && (_currentUnit == null || _currentUnit.GetCurrentColor() == _attackingForce.Color))
        {
            CatapultAction();

            _roundCatapultActed = true;
        }

        if (!_roundTowersActed && (_currentUnit == null || _currentUnit.GetCurrentColor() == _defendingForce.Color))
        {
            FireTower(_towers[1]);
            FireTower(_towers[0]);
            FireTower(_towers[2]);

            _roundTowersActed = true;
        }
    }

    /// <summary>
    /// Lets one tower fire and refreshes the order afterwards, the <c>towerAction</c> lambda of
    /// <c>Turns()</c> (<c>battle_arena.cpp:606-621</c>).
    /// </summary>
    /// <param name="tower">The tower, or <see langword="null"/> when that turret was never built.</param>
    /// <remarks>
    /// 参考只在塔还站着的时候才重排顺序——一座早就塌了的塔不会开火，也不会改变谁在下一位。
    /// </remarks>
    private void FireTower(BattleTower? tower)
    {
        if (tower == null || !tower.IsValid)
        {
            return;
        }

        TowerAction(tower);

        UpdateOrderOfUnits(GetOppositeColor(_currentUnit is null ? _lastActiveUnitArmyColor : _currentUnit.Color));
    }

    /// <summary>
    /// Rebuilds the predicted order, <c>UpdateOrderOfUnits()</c>.
    /// </summary>
    /// <param name="preferredColor">Whose side breaks a speed tie.</param>
    private void UpdateOrderOfUnits(PlayerColor preferredColor)
    {
        BattleUnits order = BattleOrderOfUnits.Predict(
            _attackingForce, _defendingForce, _currentUnit, preferredColor, _orderHistory);

        _orderOfUnits.Clear();

        foreach (BattleUnit unit in order)
        {
            _orderOfUnits.Add(unit);
        }
    }

    /// <summary>
    /// Plays the whole turn of the unit <see cref="SelectNextUnit"/> picked, <c>UnitTurn()</c>.
    /// </summary>
    /// <param name="humanTurn">
    /// Who plays a human-controlled unit, or <see langword="null"/> to let the AI play it.
    /// </param>
    public void PlayCurrentUnitTurn(BattleUnitTurnHandler? humanTurn) => UnitTurn(humanTurn);

    /// <summary>
    /// Plays the turn of the picked unit with commands a human has already chosen, the port's answer to
    /// the blocking <c>Interface::HumanTurn()</c> of the reference (<c>battle_arena.cpp:513</c>).
    /// </summary>
    /// <param name="commands">What the unit does; an empty list counts as "skip".</param>
    /// <remarks>
    /// 参考把「等玩家点」写在 <c>UnitTurn()</c> 里（界面的 <c>HumanTurn()</c> 自己带着事件循环），端口的场景
    /// 是帧驱动的，所以玩家那一手由界面在<em>其他帧</em>收齐命令，再交给这里执行。士气、行动位与随机流的
    /// 记账仍走同一条 <c>UnitTurn()</c>，与参考一致。
    /// </remarks>
    public void PlayCurrentUnitTurn(IReadOnlyList<BattleCommand> commands) => UnitTurn(null, commands);

    /// <summary>
    /// Settles the round, the tail of <c>Turns()</c> (<c>battle_arena.cpp:645-681</c>).
    /// </summary>
    public void EndRound() => SettleResult();


    /// <summary>
    /// The unit that acts next, <c>GetCurrentUnit( force, force, preferredColor )</c>
    /// (<c>battle_arena.cpp:205-217</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的选法：两军各按速度降序排好，各取第一个「还能动」的（速度大于 <see cref="Speed.Standing"/>），谁快谁先；
    /// 同速时**上一回合最后出手的那一方的对手**先。这条同速规则是回合制公平性的关键，所以
    /// <see cref="_lastActiveUnitArmyColor"/> 必须一直在 <see cref="UnitTurn"/> 里更新。
    /// </para>
    /// <para>
    /// 参考这里还会顺手把快照交给界面画顺序条；端口把那份快照做成 <see cref="OrderOfUnits"/>，由界面层取用。
    /// </para>
    /// </remarks>
    /// <returns>The next unit, or <see langword="null"/> when nobody can act any more.</returns>
    public BattleUnit? GetCurrentUnit()
    {
        BattleUnits attackingUnits = _attackingForce.ValidOnly();
        BattleUnits defendingUnits = _defendingForce.ValidOnly();

        attackingUnits.SortFastest();
        defendingUnits.SortFastest();

        BattleUnit? attacker = FindActingUnit(attackingUnits);
        BattleUnit? defender = FindActingUnit(defendingUnits);

        // 参考：preferredColor 是「上一回合最后行动方的对面」，于是「攻方先手」= 对面不是守方。
        bool attackersGoFirst = GetOppositeColor(_lastActiveUnitArmyColor) != _defendingForce.Color;

        if (attacker != null && defender != null)
        {
            if (attacker.GetSpeed() == defender.GetSpeed())
            {
                return attackersGoFirst ? attacker : defender;
            }

            return attacker.GetSpeed() > defender.GetSpeed() ? attacker : defender;
        }

        return attacker ?? defender;
    }

    /// <summary>
    /// Lets the unit whose turn it is act until its turn ends, <c>Battle::Arena::UnitTurn()</c>
    /// (<c>battle_arena.cpp:460-545</c>).
    /// </summary>
    /// <param name="humanTurn">Who plays the human-controlled units, or <see langword="null"/>.</param>
    /// <remarks>
    /// <para>
    /// 一个单位的回合可能包含多次「出招」：好士气会把 <see cref="BattleMonsterState.Moved"/> 清掉，于是循环再转一圈、
    /// 再要一套动作。循环的出口有四条：动作执行完、战斗结束、单位已经动不了（速度归零）、或拿到了下一轮的动作。
    /// </para>
    /// <para>
    /// 随机流：参考把每条命令揉进 PCG32 的 stream（<c>Command::updatePCG32Stream</c>），好让「同一串命令在任何机器上
    /// 掷出同样的骰子」。这一步必须在执行动作**之前**做，否则动作内部掷的骰子还用着旧的 stream。
    /// </para>
    /// </remarks>
    private void UnitTurn(BattleUnitTurnHandler? humanTurn, IReadOnlyList<BattleCommand>? preparedCommands = null)
    {
        BattleUnit unit = _currentUnit!;

        Debug.Assert(unit.IsValid, "The unit whose turn it is must be on the battlefield.");

        // 参考只给「受士气影响」的兵种掷士气，未受影响的连骰子都不掷——否则随机序列会错位。
        if (MonsterAbilities.IsAffectedByMorale(unit.Monster))
        {
            unit.SetRandomMorale(_randomGenerator);
        }

        Debug.Assert(
            !unit.HasAllModes(BattleMonsterState.MoraleGood | BattleMonsterState.MoraleBad),
            "A unit cannot start its turn with both morale flags.");

        bool endOfTurn = false;

        while (!endOfTurn)
        {
            List<BattleCommand> actions = [];

            if (unit.GetSpeed() == Speed.Standing)
            {
                // 死亡、被定身或已经行动完：参考把这一轮记成「已行动」，不再问任何人的意见。
                unit.SetModes(BattleMonsterState.Moved);

                endOfTurn = true;
            }
            else if (unit.HasModes(BattleMonsterState.MoraleBad))
            {
                actions.Add(BattleCommand.Morale((int)unit.Uid, 0));
            }
            else
            {
                // 这个单位确定会做至少一次完整动作：记下它是谁，同速时的先手判据靠它。
                _lastActiveUnitArmyColor = unit.Color;

                // 桥能不能过被烘进闸门格，寻路器只读格子。
                _bridge?.SetPassability(unit);

                // AI 控制的一方本来就交给规划器；人类那一方可能已经把战斗托管给电脑（自动战斗）。
                if (preparedCommands is { Count: > 0 })
                {
                    // 玩家已经在别的帧里点好了这一手：直接执行，不再问任何人。
                    actions.AddRange(preparedCommands);

                    preparedCommands = null;
                }
                else if (humanTurn != null && unit.GetCurrentControl().IsHuman() && !IsAutoCombat(unit.GetCurrentColor()))
                {
                    humanTurn(this, unit, actions);
                }
                else
                {
                    BattlePlanner.Instance.BattleTurn(this, unit, actions);
                }

                if (actions.Count == 0)
                {
                    // 没人肯出手也不能让循环空转：参考里界面保证至少给一条命令，端口兜成跳过。
                    Debug.Assert(false, "A turn controller must give an acting unit at least one command.");

                    actions.Add(BattleCommand.Skip((int)unit.Uid));
                }
            }

            ulong stream = _randomGenerator.Stream;

            foreach (BattleCommand command in actions)
            {
                stream = command.UpdatePcg32Stream(stream);
            }

            _randomGenerator.SetStream(stream);

            while (actions.Count > 0)
            {
                BattleCommand command = actions[0];
                bool isAttack = command.Type == BattleCommandType.Attack;

                // 行军与挥击要在结算之前播完，受击与死亡在结算之后——参考把这两段夹在 ApplyAction*() 两侧
                // （battle_action.cpp:198-210）。实现方在这里阻塞到动画结束，所以不会影响规则。
                Animator?.BeforeAction(unit, command);

                ApplyAction(command);
                actions.RemoveAt(0);

                RemoveDeadUnits();

                // 这条动作可能打死人或改别人的速度，顺序条要跟着变（battle_arena.cpp:527-530）。
                UpdateOrderOfUnits(GetOppositeColor(unit.Color));

                Animator?.AfterAction(isAttack ? LastAttackBlows : Array.Empty<BattleAttackBlow>());

                if (!BattleValid())
                {
                    endOfTurn = true;

                    break;
                }

                // 好士气是一次额外行动：本回合已经动过、士气仍标着好，就再问一次要不要动。
                if (unit.HasAllModes(BattleMonsterState.Moved | BattleMonsterState.MoraleGood)
                    && !unit.HasModes(BattleMonsterState.Skip)
                    && unit.GetSpeed(false, true) > Speed.Standing)
                {
                    actions.Add(BattleCommand.Morale((int)unit.Uid, 1));
                }
            }

            // 循环的退出靠开头那条 `GetSpeed() == Speed.Standing`：动过的单位速度归零，下一圈就把 `endOfTurn` 置上。
            // 这里<b>不能</b>再补一条「没动过就判它动过」的兜底——好士气会清掉 `Moved` 换来一次额外行动
            // （`ApplyActionMorale(1)` 干的正是这件事），那条兜底会把额外行动当场吃掉，
            // 于是「士气高涨、却又什么都没做」的单位把回合空转掉（参考 battle_arena.cpp:474-544 没有这一步）。
        }
    }

    /// <summary>
    /// Takes the fallen off the board, <c>Board::removeDeadUnits()</c> (<c>battle_board.cpp</c>).
    /// </summary>
    /// <remarks>
    /// 参考在每条动作执行后、以及每轮循环的开头都调它：把血量归零的单位交给 <c>PostKilledAction()</c>（登记坟场、
    /// 交还格子、解开镜像引用）。**顺序不能反**——坟场按格子下标归档，必须先登记再交还。
    /// </remarks>
    public void RemoveDeadUnits()
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            BattleUnit? unit = _board.GetCell(index)?.Unit;

            if (unit != null && !unit.IsValid)
            {
                unit.PostKilledAction();
            }
        }
    }

    /// <summary>
    /// Reduces the fight to a result, the tail of <c>Battle::Arena::Turns()</c> (<c>battle_arena.cpp:645-681</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判负的三条来源合成一个 <c>RESULT_LOSS</c> 位：场上没人了、撤退、投降。判负之后对面是「赢」还是「也算输」取决于
    /// **原始军队**是否还有人（<see cref="BattleForce.IsValid(bool)"/> 传 <see langword="false"/>）：也就是说，
    /// 靠召唤物撑着的军队不算「还存在」。
    /// </para>
    /// <para>
    /// 经验分三段加：双方按对方伤亡算经验；再把「击溃了一名英雄（不是被他跑掉）」的 500 点给对面；攻城时攻方再加 500。
    /// 招魂只数**输家**的死人。
    /// </para>
    /// </remarks>
    private void SettleResult()
    {
        if (!_attackingForce.IsValid()
            || (Result.Attacker & (BattleOutcome.Retreat | BattleOutcome.Surrender)) != 0)
        {
            Result.Attacker |= BattleOutcome.Loss;
            Result.Defender = _defendingForce.IsValid(false) ? BattleOutcome.Wins : BattleOutcome.Loss;
        }
        else if (!_defendingForce.IsValid()
                 || (Result.Defender & (BattleOutcome.Retreat | BattleOutcome.Surrender)) != 0)
        {
            Result.Defender |= BattleOutcome.Loss;
            Result.Attacker = _attackingForce.IsValid(false) ? BattleOutcome.Wins : BattleOutcome.Loss;
        }

        if (Result.Attacker == BattleOutcome.None && Result.Defender == BattleOutcome.None)
        {
            return;
        }

        Result.AttackerExperience = _defendingForce.CalculateExperienceBasedOnLosses();
        Result.DefenderExperience = _attackingForce.CalculateExperienceBasedOnLosses();

        IArmyCommander? attackingCommander = _attackingForce.Commander;
        IArmyCommander? defendingCommander = _defendingForce.Commander;

        // 对面是英雄、而且没跑掉（跑掉了就不给这份战功）。
        if (attackingCommander != null && attackingCommander.IsHeroes
            && (Result.Attacker & (BattleOutcome.Retreat | BattleOutcome.Surrender)) == 0)
        {
            Result.DefenderExperience += 500;
        }

        if (defendingCommander != null && defendingCommander.IsHeroes
            && (Result.Defender & (BattleOutcome.Retreat | BattleOutcome.Surrender)) == 0)
        {
            Result.AttackerExperience += 500;
        }

        // 攻下城池的额外战功。
        if (IsTown)
        {
            Result.AttackerExperience += 500;
        }

        BattleForce? losingForce = (Result.Attacker & BattleOutcome.Loss) != 0 ? _attackingForce
            : (Result.Defender & BattleOutcome.Loss) != 0 ? _defendingForce
            : null;

        Result.NumOfDeadUnitsForNecromancy = losingForce?.CalculateNumberOfDeadUnitsForNecromancy() ?? 0;
    }

    /// <summary>
    /// The first stack of a side that can still act, the <c>unitFilter</c> of <c>GetCurrentUnit()</c>.
    /// </summary>
    /// <param name="units">One side's stacks, already sorted fastest first.</param>
    /// <returns>The first stack whose speed is above <see cref="Speed.Standing"/>, or <see langword="null"/>.</returns>
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
