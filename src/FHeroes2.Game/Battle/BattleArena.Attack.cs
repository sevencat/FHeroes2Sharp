using System.Diagnostics;

using FHeroes2.Core.Random;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Battle;

/// <summary>
/// One unit caught by a blow and the damage it is about to take,
/// <c>Battle::TargetInfo</c> (<c>battle_action.h:54-60</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考是纯数据：<c>defender</c>、<c>damage</c> 由攻击方算出来，<c>killed</c>/<c>resurrected</c> 由伤害结算回填。
/// 端口把它做成不可变伤害 + 可变杀兵数，因为伤害必须在结算前一次算完（随机数顺序），而杀兵数只能由结算回答。
/// </para>
/// </remarks>
public sealed class BattleDamageTarget
{
    /// <summary>Starts a target with its damage already rolled.</summary>
    /// <param name="defender">The unit that takes the damage.</param>
    /// <param name="damage">How much damage it will take.</param>
    public BattleDamageTarget(BattleUnit defender, uint damage)
    {
        ArgumentNullException.ThrowIfNull(defender);

        Defender = defender;
        Damage = damage;
    }

    /// <summary>The unit that takes the damage, <c>TargetInfo::defender</c>.</summary>
    public BattleUnit Defender { get; }

    /// <summary>How much damage it takes, <c>TargetInfo::damage</c>.</summary>
    public uint Damage { get; }

    /// <summary>How many of its creatures die, <c>TargetInfo::killed</c>; filled in by the damage pass.</summary>
    public uint Killed { get; internal set; }
}

/// <summary>
/// One blow of an attack command and what it hit, the data <c>RedrawActionAttack()</c> needs
/// (<c>battle_interface.cpp:4088-4256</c>).
/// </summary>
/// <remarks>
/// 参考的界面在 <c>BattleProcess()</c> 里被直接调用，所以「谁打谁、射的还是砍的、打到了谁」当场就知道。
/// 端口是一次性结算的，界面事后只能从这份记录里回放，因此这一击必先把这三件事记下来。
/// 反击与双击各是<b>一条</b>记录：参考也是每个 <c>BattleProcess()</c> 各播一遍攻击动画。
/// </remarks>
public sealed class BattleAttackBlow
{
    /// <summary>Records one blow.</summary>
    /// <param name="attacker">The unit that strikes.</param>
    /// <param name="defender">The unit it strikes.</param>
    /// <param name="isShooting">Whether the blow was a shot rather than a melee swing.</param>
    /// <param name="targets">Every unit the blow hit, with the damage and the losses already filled in.</param>
    public BattleAttackBlow(
        BattleUnit attacker, BattleUnit defender, bool isShooting, IReadOnlyList<BattleDamageTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(targets);

        Attacker = attacker;
        Defender = defender;
        IsShooting = isShooting;
        Targets = targets;
    }

    /// <summary>The unit that strikes, <c>RedrawActionAttackPart1()</c>'s <c>attacker</c>.</summary>
    public BattleUnit Attacker { get; }

    /// <summary>The unit it strikes, <c>RedrawActionAttackPart1()</c>'s <c>defender</c>.</summary>
    public BattleUnit Defender { get; }

    /// <summary>Whether the blow was a shot rather than a melee swing.</summary>
    public bool IsShooting { get; }

    /// <summary>Every unit the blow hit, the reference's <c>TargetsInfo</c>.</summary>
    public IReadOnlyList<BattleDamageTarget> Targets { get; }
}

/// <summary>
/// 近战与射击：攻击命令的校验、一次打击的完整流程、以及伤害落到多个目标上，
/// <c>battle_action.cpp</c> 的 <c>ApplyActionAttack()</c>（<c>:481-627</c>）、
/// <c>BattleProcess()</c>（<c>:143-276</c>）与 <c>GetTargetsForDamage()</c>/<c>TargetsApplyDamage()</c>
/// （<c>:856-953</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 参考把「打一下」拆成 <c>BattleProcess</c>：先摆朝向，再掷幸运、掷伤害、结算，最后放生物自带的法术。端口保留前四段，
/// **生物自带法术与 <c>PostAttackAction</c> 未接**：前者要 <c>TargetsApplySpell</c>（<c>:955-1179</c>，一整批法术效果），
/// 后者要镜像与催眠的收尾（<c>battle_troop.cpp</c>）。这两处缺口只影响「法师类兵种附带的法术」与「攻击后解除催眠」，
/// 不影响伤害、反击与双击，所以先把主体落地。
/// </para>
/// <para>
/// 参数校验与参考一致：非法的攻击参数在参考里只是打日志并静默返回，端口抛 <see cref="ArgumentException"/>，
/// 理由与 <see cref="ApplyAction"/> 相同——命令错了是编程错误。
/// </para>
/// </remarks>
public sealed partial class BattleArena
{
    /// <summary>
    /// Executes an attack command, <c>Arena::ApplyActionAttack()</c> (<c>battle_action.cpp:481-627</c>).
    /// </summary>
    /// <param name="command">
    /// A command carrying the attacker UID, the defender UID, the cell to walk to (<c>-1</c> to stay put), the cell to
    /// strike (<c>-1</c> to let the arena pick) and the attack direction (<c>-1</c> to let the arena pick).
    /// </param>
    /// <remarks>
    /// <para>
    /// 顺序是固定的：先走到位，再打；对方活着且这一击是近战，才有反击；还活着且是双击兵种，才有第二击。近战与射击
    /// 的差别只体现在方向计算上——射击没有方向（<see cref="CellDirection.Unknown"/>），近战必须从「目标的反方向」打。
    /// </para>
    /// <para>
    /// 最后三个收尾都是一句话，但都有理由：致盲的反击折扣只在这一击的窗口内有效；狂化与催眠只在**本单位主动出手**后
    /// 解除（反击打人时不解除，否则狂化单位互相打两下就都醒了）；死者不转身。
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The command does not describe a legal attack.</exception>
    public void ApplyActionAttack(BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        _attackBlows.Clear();

        uint attackerUid = (uint)command.GetNextValue();
        uint defenderUid = (uint)command.GetNextValue();
        int destination = command.GetNextValue();
        int target = command.GetNextValue();
        int direction = command.GetNextValue();

        BattleUnit? attacker = GetTroopUID(attackerUid);
        BattleUnit? defender = GetTroopUID(defenderUid);

        if (!CheckAttackParameters(attacker, defender, destination, direction, ref target))
        {
            throw new ArgumentException(
                $"The attack of {attackerUid} on {defenderUid} is not legal (cell {destination}, target {target}, "
                + $"direction {direction}).",
                nameof(command));
        }

        MoveUnit(attacker!, destination);

        bool handFighting = BattleUnit.IsHandFighting(attacker!, defender!);
        bool doubleAttack = attacker!.IsDoubleAttack;

        // 致盲的单位仍会还手，只是按致盲的减伤比例打折；这个标记只在这一击期间有效。
        defender!.BlindRetaliation = defender.HasModes(BattleMonsterState.SpellBlind);

        BattleProcess(attacker, defender, target, direction);

        if (defender.IsValid)
        {
            if (handFighting && !attacker.IsIgnoringRetaliation && defender.IsRetaliationAllowed)
            {
                BattleProcess(defender, attacker);

                // 无限反击的兵种不看这个标记，其余单位本回合到此为止。
                defender.SetRetaliationCompleted();
            }

            if (doubleAttack && attacker.IsValid && !attacker.IsImmovable)
            {
                BattleProcess(attacker, defender, target, direction);
            }
        }

        defender.BlindRetaliation = false;

        // 只解除「主动出手」带来的狂化与催眠：反击不解除，见方法注释。
        attacker.RemoveAffection(BattleMonsterState.SpellBerserker | BattleMonsterState.SpellHypnotize);

        if (attacker.IsValid)
        {
            attacker.UpdateDirection();
        }

        if (defender.IsValid)
        {
            defender.UpdateDirection();
        }

        attacker.SetModes(BattleMonsterState.Moved);
    }

    /// <summary>
    /// The blows the last <see cref="ApplyActionAttack"/> landed, in order: the blow itself, then the
    /// retaliation and the second blow of a double attack, if any.
    /// </summary>
    /// <remarks>
    /// 参考的界面在结算过程中被调用，因而不需要这份记录；端口结算一次做完，界面只能事后照它回放。
    /// 记录在每次攻击开头清空，所以它只描述最近一次攻击，不是整场战斗的账。
    /// </remarks>
    public IReadOnlyList<BattleAttackBlow> LastAttackBlows => _attackBlows;

    /// <summary>
    /// Whether an attack command is legal, the <c>checkParameters</c> lambda of <c>ApplyActionAttack()</c>
    /// (<c>battle_action.cpp:483-564</c>).
    /// </summary>
    /// <param name="attacker">The unit that strikes.</param>
    /// <param name="defender">The unit that is struck.</param>
    /// <param name="destination">The cell the attacker walks to, or <c>-1</c> to stay put.</param>
    /// <param name="direction">The attack direction given by the caller, or <c>-1</c> to compute it.</param>
    /// <param name="target">The cell to strike; <c>-1</c> on entry, the computed cell on return.</param>
    /// <returns><see langword="true"/> when the attack may go ahead.</returns>
    /// <remarks>
    /// 射击与近战是两条不同的规则：射击只允许原地不动、朝着目标整堆（宽单位两格都能瞄）、且没有方向；近战必须从目标的
    /// 「反方向格」打过去，也就是说攻击方得站在目标旁边，而且那格不能是护城河之类不许出手的格子。
    /// </remarks>
    private bool CheckAttackParameters(
        BattleUnit? attacker, BattleUnit? defender, int destination, int direction, ref int target)
    {
        if (attacker == null || !attacker.IsValid)
        {
            return false;
        }

        if (defender == null || !defender.IsValid)
        {
            return false;
        }

        if (attacker.HasModes(BattleMonsterState.Moved))
        {
            return false;
        }

        // 被催眠的单位算敌人的颜色，所以这里要问「当前颜色」而不是「本阵颜色」。
        if (attacker.GetCurrentColor() == defender.Color)
        {
            return false;
        }

        if (destination != -1 && !CheckMoveParams(attacker, destination))
        {
            return false;
        }

        if (attacker.IsArchers && !attacker.IsHandFighting())
        {
            // 没被贴住的射手只能原地射。
            if (destination != -1)
            {
                return false;
            }

            if (target < 0)
            {
                target = CalculateAttackTarget(attacker, attacker.Position, defender);
            }

            CellDirection shotDirection = direction < 0
                ? CalculateAttackDirection(attacker, attacker.Position, target)
                : (CellDirection)direction;

            if (!defender.Position.Contains(target))
            {
                return false;
            }

            // 射击不带方向。
            return shotDirection == CellDirection.Unknown;
        }

        BattlePosition attackPosition =
            destination == -1 ? attacker.Position : BattlePosition.GetReachable(this, attacker, destination);

        if (attackPosition.HeadIndex < 0)
        {
            return false;
        }

        if (target < 0)
        {
            target = CalculateAttackTarget(attacker, attackPosition, defender);
        }

        CellDirection meleeDirection = direction < 0
            ? CalculateAttackDirection(attacker, attackPosition, target)
            : (CellDirection)direction;

        if (!defender.Position.Contains(target))
        {
            return false;
        }

        // 近战必须有方向：打不着就是够不着。
        if (meleeDirection == CellDirection.Unknown)
        {
            return false;
        }

        CellDirection reflectDirection = BattleBoard.GetReflectDirection(meleeDirection);

        int attackIndex = BattleBoard.IsValidDirection(target, reflectDirection)
            ? BattleBoard.GetIndexDirection(target, reflectDirection)
            : -1;

        if (!attackPosition.Contains(attackIndex))
        {
            return false;
        }

        // 站得住不等于打得着：护城河里的格子不许出手。
        return BattleBoard.CanAttackFromCell(this, attacker, attackIndex);
    }

    /// <summary>
    /// One blow from <paramref name="attacker"/> on <paramref name="defender"/>,
    /// <c>Arena::BattleProcess()</c> (<c>battle_action.cpp:143-276</c>).
    /// </summary>
    /// <param name="attacker">The unit that strikes.</param>
    /// <param name="defender">The unit that is struck (or shot at).</param>
    /// <param name="target">The cell to strike, or <c>-1</c> to compute it.</param>
    /// <param name="direction">The attack direction, or <c>-1</c> to compute it.</param>
    /// <remarks>
    /// <para>
    /// 朝向要先摆好，因为伤害和反击都按朝向算：宽单位如果「反方向格」落在尾巴上，就得翻身；窄单位直接转头。
    /// 射击没有方向（<see cref="CellDirection.Unknown"/>），射手朝目标的格子中心转。
    /// </para>
    /// <para>
    /// 幸运必须在掷伤害**之前**掷：参考就是这么排的，顺序一换，同一个随机种子给出的战斗结果就不同了。
    /// </para>
    /// </remarks>
    private void BattleProcess(BattleUnit attacker, BattleUnit defender, int target = -1, int direction = -1)
    {
        if (target < 0)
        {
            target = CalculateAttackTarget(attacker, attacker.Position, defender);
        }

        CellDirection cellDirection = direction < 0
            ? CalculateAttackDirection(attacker, attacker.Position, target)
            : (CellDirection)direction;

        Debug.Assert(
            BattleUnit.IsHandFighting(attacker, defender)
                ? cellDirection != CellDirection.Unknown
                : cellDirection == CellDirection.Unknown,
            "A melee blow needs a direction, a shot must not have one.");

        if (cellDirection != CellDirection.Unknown)
        {
            if (attacker.IsWide)
            {
                if (!IsDirectionValidForAttack(attacker, target, cellDirection))
                {
                    attacker.SetReflection(!attacker.IsReflect);
                }
            }
            else
            {
                attacker.UpdateDirection(Cell(target).Position);
            }

            // 反击的朝向也提前摆好，否则反击的伤害会按错的相对位置算。
            if (!attacker.IsIgnoringRetaliation && defender.IsRetaliationAllowed)
            {
                int retaliationTarget = CalculateAttackTarget(defender, defender.Position, attacker);
                CellDirection retaliationDirection = CalculateAttackDirection(defender, defender.Position, retaliationTarget);

                if (defender.IsWide)
                {
                    if (!IsDirectionValidForAttack(defender, retaliationTarget, retaliationDirection))
                    {
                        defender.SetReflection(!defender.IsReflect);
                    }
                }
                else
                {
                    defender.UpdateDirection(Cell(retaliationTarget).Position);
                }
            }
        }
        else
        {
            // 射击：宽单位以整格中心为目标，取头格即可。
            attacker.UpdateDirection(Cell(defender.HeadIndex).Position);
        }

        attacker.SetRandomLuck(_randomGenerator);

        List<BattleDamageTarget> attackTargets = GetTargetsForDamage(attacker, defender, target, cellDirection);

        TargetsApplyDamage(attacker, attackTargets);

        // 参考在这一拍就调界面画攻击与受击（battle_action.cpp:198-210）；端口先记账，界面事后回放。
        _attackBlows.Add(
            new BattleAttackBlow(attacker, defender, cellDirection == CellDirection.Unknown, attackTargets));

        // 兵种自带的法术在伤害结算之后：参考先让这一击打死人，再让幸存者吃法术。
        Spell spell = attacker.GetSpellMagic(_randomGenerator);

        if (spell.Type != SpellType.None)
        {
            Debug.Assert(spell.IsSingleTarget() || spell.IsBuiltinOnly(), "A built-in spell must be single target or builtin only.");

            List<BattleSpellTarget> spellTargets = new(attackTargets.Count);

            // 只留下还活着、且法术落得下的目标。
            foreach (BattleDamageTarget attackTarget in attackTargets)
            {
                BattleUnit spellTargetUnit = attackTarget.Defender;

                if (!spellTargetUnit.IsValid || !spellTargetUnit.AllowApplySpell(spell, null))
                {
                    continue;
                }

                spellTargets.Add(new BattleSpellTarget(spellTargetUnit));
            }

            if (spellTargets.Count > 0)
            {
                // 自带法术只打一个：可打多个时按随机序列挑一个。
                if (spellTargets.Count > 1)
                {
                    BattleUnit? selectedUnit = Rand.GetWithGen(spellTargets, _randomGenerator).Defender;

                    spellTargets.RemoveAll(candidate => !ReferenceEquals(candidate.Defender, selectedUnit));
                }

                Debug.Assert(spellTargets.Count == 1, "A built-in spell keeps exactly one target.");

                BattleUnit spellTarget = spellTargets[0].Defender!;

                // 自带的驱散只摘增益，不碰减益——和英雄施放的驱散不同。
                if (spell.Type != SpellType.Dispel || spellTarget.HasModes(BattleMonsterState.GoodMagic))
                {
                    if (spell.Type == SpellType.Dispel)
                    {
                        spellTarget.RemoveAffection(BattleMonsterState.GoodMagic);
                    }
                    else
                    {
                        // 兵种自带法术的强度不看指挥官的技能。
                        TargetsApplySpell(null, spell, spellTargets);
                    }
                }
            }
        }

        attacker.PostAttackAction(defender);
    }

    /// <summary>
    /// Whether a wide unit strikes from its head rather than its tail,
    /// the <c>directionIsValidForAttack</c> lambda of <c>BattleProcess()</c> (<c>battle_action.cpp:156-164</c>).
    /// </summary>
    /// <param name="attackingUnit">The wide unit.</param>
    /// <param name="attackTarget">The cell it strikes.</param>
    /// <param name="attackDirection">The direction of the blow.</param>
    /// <returns>True when the blow comes from the unit's head cell.</returns>
    private static bool IsDirectionValidForAttack(BattleUnit attackingUnit, int attackTarget, CellDirection attackDirection)
    {
        Debug.Assert(attackingUnit.IsWide, "Only a wide unit has two cells to strike from.");

        int attackSource = BattleBoard.GetIndexDirection(attackTarget, BattleBoard.GetReflectDirection(attackDirection));

        return attackSource == attackingUnit.HeadIndex;
    }

    /// <summary>
    /// Which cell of the defender a wide unit or a shot lands on,
    /// <c>calculateAttackTarget()</c> (<c>battle_action.cpp:66-99</c>).
    /// </summary>
    /// <param name="attackingUnit">The unit that strikes.</param>
    /// <param name="attackPosition">Where it strikes from.</param>
    /// <param name="defendingUnit">The unit that is struck.</param>
    /// <returns>The head cell of the defender when it cannot be reached, a neighbouring cell otherwise.</returns>
    /// <remarks>
    /// 宽单位有两格可以出手，所以先问头格够不够得着目标的两格，再问尾巴；都够不着就当作射击，瞄头格。
    /// </remarks>
    private int CalculateAttackTarget(BattleUnit attackingUnit, BattlePosition attackPosition, BattleUnit defendingUnit)
    {
        int headIndex = attackPosition.HeadIndex;
        int tailIndex = attackPosition.TailIndex;

        if (BattleBoard.CanAttackFromCell(this, attackingUnit, headIndex))
        {
            if (BattleBoard.IsNearIndexes(headIndex, defendingUnit.HeadIndex))
            {
                return defendingUnit.HeadIndex;
            }

            if (defendingUnit.IsWide && BattleBoard.IsNearIndexes(headIndex, defendingUnit.TailIndex))
            {
                return defendingUnit.TailIndex;
            }
        }

        if (BattleBoard.CanAttackFromCell(this, attackingUnit, tailIndex))
        {
            if (BattleBoard.IsNearIndexes(tailIndex, defendingUnit.HeadIndex))
            {
                return defendingUnit.HeadIndex;
            }

            if (defendingUnit.IsWide && BattleBoard.IsNearIndexes(tailIndex, defendingUnit.TailIndex))
            {
                return defendingUnit.TailIndex;
            }
        }

        return defendingUnit.HeadIndex;
    }

    /// <summary>
    /// Which way the blow goes, <c>calculateAttackDirection()</c> (<c>battle_action.cpp:101-120</c>).
    /// </summary>
    /// <param name="attackingUnit">The unit that strikes.</param>
    /// <param name="attackPosition">Where it strikes from.</param>
    /// <param name="attackTargetIndex">The cell it strikes.</param>
    /// <returns>The direction, or <see cref="CellDirection.Unknown"/> for a shot.</returns>
    private CellDirection CalculateAttackDirection(
        BattleUnit attackingUnit, BattlePosition attackPosition, int attackTargetIndex)
    {
        int headIndex = attackPosition.HeadIndex;
        int tailIndex = attackPosition.TailIndex;

        if (BattleBoard.CanAttackFromCell(this, attackingUnit, headIndex)
            && BattleBoard.IsNearIndexes(headIndex, attackTargetIndex))
        {
            return BattleBoard.GetDirection(headIndex, attackTargetIndex);
        }

        if (BattleBoard.CanAttackFromCell(this, attackingUnit, tailIndex)
            && BattleBoard.IsNearIndexes(tailIndex, attackTargetIndex))
        {
            return BattleBoard.GetDirection(tailIndex, attackTargetIndex);
        }

        return CellDirection.Unknown;
    }

    /// <summary>
    /// Every unit a single blow hits, <c>Arena::GetTargetsForDamage()</c> (<c>battle_action.cpp:869-953</c>).
    /// </summary>
    /// <param name="attacker">The unit that strikes.</param>
    /// <param name="defender">The unit aimed at.</param>
    /// <param name="destination">The cell actually struck.</param>
    /// <param name="direction">The direction of the blow, or <see cref="CellDirection.Unknown"/> for a shot.</param>
    /// <returns>The aimed unit first, then whatever the blow spills onto.</returns>
    /// <remarks>
    /// <para>
    /// 三种溅射互斥，顺序即优先级：双格近战（打在目标背后那一格）、全相邻近战（打自己身边所有敌人）、
    /// 范围射击（巫妖的云，打目标周围一圈）。**神灯的「减半」未接**——它要读能力条目里的百分比，等
    /// <see cref="MonsterAbilities"/> 暴露那条数据再补，见类注释。
    /// </para>
    /// <para>
    /// 伤害在这里一次全掷完，之后不再掷骰：结算阶段只扣血、数死人。这样随机序列与参考一致。
    /// </para>
    /// </remarks>
    private List<BattleDamageTarget> GetTargetsForDamage(
        BattleUnit attacker, BattleUnit defender, int destination, CellDirection direction)
    {
        Debug.Assert(
            defender.HeadIndex == destination || defender.TailIndex == destination,
            "The struck cell belongs to the defending unit.");

        List<BattleDamageTarget> targets = [new BattleDamageTarget(defender, attacker.GetDamage(defender, _randomGenerator))];

        HashSet<BattleUnit> consideredTargets = [defender];

        if (attacker.IsDoubleCellAttack)
        {
            BattleUnit? spilled = _board.GetCell(destination, direction)?.Unit;

            if (spilled != null && consideredTargets.Add(spilled))
            {
                targets.Add(new BattleDamageTarget(spilled, attacker.GetDamage(spilled, _randomGenerator)));
            }
        }
        else if (attacker.IsAllAdjacentCellsAttack)
        {
            foreach (int nearbyIndex in BattleBoard.GetAroundIndexes(attacker.Position))
            {
                BattleUnit? enemy = _board.GetCell(nearbyIndex)?.Unit;

                if (enemy != null && enemy.GetCurrentColor() != attacker.GetCurrentColor()
                    && consideredTargets.Add(enemy))
                {
                    targets.Add(new BattleDamageTarget(enemy, attacker.GetDamage(enemy, _randomGenerator)));
                }
            }
        }
        else if (MonsterAbilities.IsPresent(attacker.Monster, MonsterAbility.AreaShot)
                 && !BattleUnit.IsHandFighting(attacker, defender))
        {
            foreach (int nearbyIndex in BattleBoard.GetAroundIndexes(destination))
            {
                BattleUnit? enemy = _board.GetCell(nearbyIndex)?.Unit;

                if (enemy != null && consideredTargets.Add(enemy))
                {
                    targets.Add(new BattleDamageTarget(enemy, attacker.GetDamage(enemy, _randomGenerator)));
                }
            }
        }

        return targets;
    }

    /// <summary>
    /// Applies a blow's rolled damage to every unit it caught,
    /// <c>Arena::TargetsApplyDamage()</c> (<c>battle_action.cpp:856-867</c>).
    /// </summary>
    /// <param name="attacker">The unit that struck.</param>
    /// <param name="targets">The units and the damage they take.</param>
    /// <returns>How many creatures were restored by life drain, summed over the targets.</returns>
    private uint TargetsApplyDamage(BattleUnit attacker, List<BattleDamageTarget> targets)
    {
        uint resurrected = 0;

        foreach (BattleDamageTarget target in targets)
        {
            target.Defender.ApplyDamage(attacker, target.Damage, out uint killed, out uint restored);

            target.Killed = killed;
            resurrected += restored;
        }

        return resurrected;
    }
}
