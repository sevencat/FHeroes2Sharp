using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The best blow a melee unit can land from a cell, <c>MeleeAttackOutcome</c> (<c>ai_battle.cpp:71-87</c>).
/// </summary>
/// <remarks>
/// 参考用 <see cref="Create"/> 造一个「什么都没找到」的初值（<c>lowest()</c>），再用
/// <see cref="BattleTactics.IsOutcomeImproved"/> 逐个比较。
/// </remarks>
internal sealed class MeleeAttackOutcome
{
    /// <summary>The cell the blow would be struck from; -1 when there is none.</summary>
    public int FromIndex { get; set; } = -1;

    /// <summary>How much the blow itself is worth.</summary>
    public double AttackValue { get; set; } = double.MinValue;

    /// <summary>How much the cell itself is worth (the position value of the attacker's turn).</summary>
    public double PositionValue { get; set; } = double.MinValue;

    /// <summary>Whether the unit can strike right away, without waiting for the next turn.</summary>
    public bool CanAttackImmediately { get; set; }

    /// <summary>The value of «nothing found», <c>MeleeAttackOutcome::lowest()</c>.</summary>
    /// <returns>An outcome worse than any real one.</returns>
    public static MeleeAttackOutcome Create() => new();
}

/// <summary>
/// A cell plus its distance to something, <c>CellDistanceInfo</c> (<c>ai_battle.cpp:471-477</c>).
/// </summary>
/// <remarks>参考用 <c>idx = -1, dist = UINT32_MAX</c> 表示「一个都没有」。</remarks>
internal readonly struct CellDistanceInfo
{
    /// <summary>Creates the pair.</summary>
    /// <param name="index">The cell.</param>
    /// <param name="distance">How far it is.</param>
    public CellDistanceInfo(int index, uint distance)
    {
        Index = index;
        Distance = distance;
    }

    /// <summary>The cell.</summary>
    public int Index { get; }

    /// <summary>How far it is.</summary>
    public uint Distance { get; }

    /// <summary>«No cell at all», <c>idx = -1, dist = UINT32_MAX</c>.</summary>
    public static CellDistanceInfo None => new(-1, uint.MaxValue);
}

/// <summary>
/// Orders positions like the reference's <c>operator&lt;</c>, so a map of positions walks in the same order.
/// </summary>
internal sealed class BattlePositionOrder : IComparer<BattlePosition>
{
    /// <summary>The only instance.</summary>
    public static BattlePositionOrder Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(BattlePosition? first, BattlePosition? second) => first!.CompareTo(second!);
}

/// <summary>
/// Orders positions for a set, i.e. by the same rule as <see cref="BattlePositionOrder"/>.
/// </summary>
internal sealed class BattlePositionEquality : IEqualityComparer<BattlePosition>
{
    /// <summary>The only instance.</summary>
    public static BattlePositionEquality Instance { get; } = new();

    /// <inheritdoc/>
    public bool Equals(BattlePosition? first, BattlePosition? second) =>
        ReferenceEquals(first, second) || (first != null && second != null && first.CompareTo(second) == 0);

    /// <inheritdoc/>
    public int GetHashCode(BattlePosition position) =>
        HashCode.Combine(position.HeadIndex, position.TailIndex);
}

/// <summary>
/// The anonymous helpers of <c>ai_battle.cpp</c> minus the spell-casting ones
/// (<c>ai_battle.cpp:71-656</c>), plus <c>AI::getBerserkerTarget()</c>.
/// </summary>
/// <remarks>
/// 这些函数原本是 <c>ai_battle.cpp</c> 匿名命名空间里的自由函数，只被 <c>BattlePlanner</c> 的成员与
/// <c>AI::</c> 的两个入口调用。端口把它们收进一个 <see langword="internal"/> 静态类，签名里显式带
/// <see cref="BattleArena"/>，不碰全局状态。
/// </remarks>
internal static class BattleTactics
{
    /// <summary>
    /// The cells right under the castle walls, <c>cellsUnderWallsIndexes</c> (<c>ai_battle.cpp:73</c>).
    /// </summary>
    public static readonly int[] CellsUnderWallsIndexes = [7, 28, 49, 72, 95];

    /// <summary>
    /// Whether the new pair of values beats the old one, <c>ValueHasImproved()</c> (<c>ai_battle.cpp:117-133</c>).
    /// </summary>
    /// <remarks>
    /// 次值只有在主值几乎相同时才比较——参考用 0.001 的容差，避免浮点噪声让「看起来一样好」的两个格子互相压过。
    /// </remarks>
    /// <param name="newPrimary">The new primary value.</param>
    /// <param name="previousPrimary">The previous primary value.</param>
    /// <param name="newSecondary">The new secondary value.</param>
    /// <param name="previousSecondary">The previous secondary value.</param>
    /// <returns>Whether the new pair is better.</returns>
    public static bool ValueHasImproved(double newPrimary, double previousPrimary, double newSecondary, double previousSecondary) =>
        previousPrimary < newPrimary ||
        (previousSecondary < newSecondary && Math.Abs(previousPrimary - newPrimary) < 0.001);

    /// <summary>
    /// How much a spell's value shrinks because the unit has already crossed the battlefield,
    /// <c>ReduceEffectivenessByDistance()</c> (<c>ai_battle_spell.cpp:54-61</c>).
    /// </summary>
    /// <remarks>
    /// 参考直接返回「离它要冲过去的那条边还有几格」，当除数用：越靠近对面，减速、狂暴之类的价值越低。
    /// </remarks>
    /// <param name="unit">The unit the spell would hit.</param>
    /// <returns>The number of columns between the unit and the far edge.</returns>
    public static uint ReduceEffectivenessByDistance(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        uint result = BattleBoard.GetDistanceFromBoardEdgeAlongXAxis(unit.HeadIndex, unit.IsReflect);
        Debug.Assert(result > 0, "A unit on the board is at least one column away from the edge.");

        return result;
    }

    /// <summary>
    /// Whether one blow beats another, <c>IsOutcomeImproved()</c> (<c>ai_battle.cpp:105-115</c>).
    /// </summary>
    /// <remarks>攻击值优先；攻击值相同时看格子值。</remarks>
    /// <param name="newOutcome">The candidate.</param>
    /// <param name="previousOutcome">The best so far.</param>
    /// <returns>Whether the candidate is better.</returns>
    public static bool IsOutcomeImproved(MeleeAttackOutcome newOutcome, MeleeAttackOutcome previousOutcome) =>
        ValueHasImproved(newOutcome.AttackValue, previousOutcome.AttackValue, newOutcome.PositionValue, previousOutcome.PositionValue);

    /// <summary>
    /// What the second half of a two-cell blow is worth, <c>doubleCellAttackValue()</c>
    /// (<c>ai_battle.cpp:135-172</c>).
    /// </summary>
    /// <remarks>
    /// 双格近战会顺带打到主目标旁边的那一格。参考从主目标往回退一格取「第二个目标」，取不到就只值主目标的一半；
    /// 取到友军则倒扣一半的友军威胁（避免误伤），取到敌军就加上它的威胁。
    /// </remarks>
    /// <param name="arena">The arena, to look the cell up.</param>
    /// <param name="attacker">The unit that would strike.</param>
    /// <param name="target">The main target.</param>
    /// <param name="from">The cell the attacker stands on.</param>
    /// <param name="targetCell">The cell of the main target.</param>
    /// <returns>The value of the second blow.</returns>
    public static double DoubleCellAttackValue(BattleArena arena, BattleUnit attacker, BattleUnit target, int from, int targetCell)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        CellDirection direction = BattleBoard.GetDirection(from, targetCell);

        if (direction == CellDirection.Unknown || direction == CellDirection.Center)
        {
            return 0;
        }

        BattleCell? secondCell = arena.Board.GetCell(targetCell, BattleBoard.GetReflectDirection(direction));

        if (secondCell?.Unit is not { } secondTarget)
        {
            return 0.5 * target.EvaluateThreatForUnit(attacker);
        }

        return secondTarget.Color == attacker.GetCurrentColor()
            ? -0.5 * secondTarget.EvaluateThreatForUnit(attacker)
            : secondTarget.EvaluateThreatForUnit(attacker);
    }

    /// <summary>
    /// Where a two-cell blow would land, <c>optimalAttackVector()</c> (<c>ai_battle.cpp:174-228</c>).
    /// </summary>
    /// <remarks>
    /// 宽单位有两个站位：先用当前位置去试（有 90% 的把握主目标就挨着某条边），不行再把位置翻过来试。
    /// 空结果用 <c>(-1, Unknown)</c> 表示。
    /// </remarks>
    /// <param name="arena">The arena, to test the cells.</param>
    /// <param name="attacker">The unit that would strike.</param>
    /// <param name="target">The main target.</param>
    /// <param name="attackPos">Where the attacker would stand.</param>
    /// <returns>The target cell and the direction of the blow.</returns>
    public static (int Index, CellDirection Direction) OptimalAttackVector(BattleArena arena, BattleUnit attacker, BattleUnit target, BattlePosition attackPos)
    {
        if (attacker.IsWide)
        {
            Debug.Assert(attackPos.Tail != null, "A wide unit always has a tail.");

            CellDirection firstDirection = BattleBoard.GetDirection(attackPos.HeadIndex, target.HeadIndex);

            if (firstDirection != CellDirection.Unknown && firstDirection != CellDirection.Center)
            {
                int cell = BattleBoard.GetIndexDirection(attackPos.HeadIndex, firstDirection);

                if (arena.GetTroopBoard(cell) == target)
                {
                    return (cell, firstDirection);
                }
            }

            CellDirection secondDirection = BattleBoard.GetDirection(attackPos.TailIndex, target.HeadIndex);

            if (secondDirection != CellDirection.Unknown && secondDirection != CellDirection.Center)
            {
                int cell = BattleBoard.GetIndexDirection(attackPos.TailIndex, secondDirection);

                if (arena.GetTroopBoard(cell) == target)
                {
                    return (cell, secondDirection);
                }
            }

            return (-1, CellDirection.Unknown);
        }

        CellDirection direction = BattleBoard.GetDirection(attackPos.HeadIndex, target.HeadIndex);

        if (direction == CellDirection.Unknown || direction == CellDirection.Center)
        {
            return (-1, CellDirection.Unknown);
        }

        return (target.HeadIndex, direction);
    }

    /// <summary>
    /// What a blow made from a given cell is worth, <c>optimalAttackValue()</c>
    /// (<c>ai_battle.cpp:230-317</c>).
    /// </summary>
    /// <remarks>
    /// 全相邻攻击把周围所有敌人（含友军，威胁为负）加总；普通攻击先取主目标的威胁，双格攻击再加第二击，
    /// 噬魂/吸血再按难度与战场形势折算「这一击省下的复活点数」。困难难度以下与人类操作的单位只算伤害。
    /// </remarks>
    /// <param name="arena">The arena, to look the board up.</param>
    /// <param name="attacker">The unit that would strike.</param>
    /// <param name="target">The main target.</param>
    /// <param name="attackPos">Where the attacker would stand.</param>
    /// <param name="allEnemiesThreat">The summed threat of every enemy of the attacker.</param>
    /// <returns>The value of the blow.</returns>
    public static double OptimalAttackValue(BattleArena arena, BattleUnit attacker, BattleUnit target, BattlePosition attackPos, double allEnemiesThreat)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attackPos);

        Debug.Assert(attackPos.IsValidForUnit(attacker), "The cell must fit the attacker.");

        if (attacker.IsAllAdjacentCellsAttack)
        {
            HashSet<BattleUnit> unitsUnderAttack = [];

            foreach (int index in BattleBoard.GetAroundIndexes(attackPos))
            {
                BattleUnit? unit = arena.GetTroopBoard(index);

                if (unit == null || ReferenceEquals(unit, attacker) || unit.Color == attacker.GetCurrentColor())
                {
                    continue;
                }

                unitsUnderAttack.Add(unit);
            }

            double sum = 0;

            foreach (BattleUnit unit in unitsUnderAttack)
            {
                sum += unit.EvaluateThreatForUnit(attacker);
            }

            return sum;
        }

        double attackValue = target.EvaluateThreatForUnit(attacker);

        if (attacker.IsDoubleCellAttack && BattleBoard.CanAttackTargetFromPosition(arena, attacker, target, attackPos.HeadIndex))
        {
            (int attackTargetIdx, CellDirection attackDirection) = OptimalAttackVector(arena, attacker, target, attackPos);

            Debug.Assert(attackTargetIdx != -1 && attackDirection != CellDirection.Unknown,
                "A two-cell blow that can be struck always has a vector.");

            attackValue += DoubleCellAttackValue(
                arena,
                attacker,
                target,
                BattleBoard.GetIndexDirection(attackTargetIdx, BattleBoard.GetReflectDirection(attackDirection)),
                attackTargetIdx);
        }

        if (!MonsterAbilities.IsPresent(attacker.Monster, MonsterAbility.SoulEater)
            && !MonsterAbilities.IsPresent(attacker.Monster, MonsterAbility.HpDrain))
        {
            return attackValue;
        }

        bool isBasicLogic = Difficulty.IsBasicAiBattleLogicApplicable(
            Settings.Get().GameDifficulty,
            attacker.GetControl() != PlayerControl.Ai);

        if (isBasicLogic)
        {
            return attackValue;
        }

        {
            uint futureKilled = target.HowManyWillBeKilled(attacker.GetPotentialDamage(target));
            Debug.Assert(futureKilled <= target.Count, "A blow cannot kill more creatures than the stack has.");

            uint resurrectPoints;

            if (MonsterAbilities.IsPresent(attacker.Monster, MonsterAbility.SoulEater))
            {
                resurrectPoints = futureKilled * (uint)MonsterStatistics.GetBattleStats(attacker.Monster).HitPoints;
            }
            else
            {
                resurrectPoints = futureKilled * (uint)MonsterStatistics.GetBattleStats(target.Monster).HitPoints;
                resurrectPoints = Math.Min(resurrectPoints, attacker.MissingHitPoints);
            }

            Debug.Assert(resurrectPoints <= attacker.HitPoints, "A blow cannot heal more than the stack could hold.");

            // 参考的经验值：一个「有威胁的目标」平均值 1.5 个兵的血量，敌方每回合平均 2 个单位出手，战斗约 5 回合。
            const double averageNumberOfPowerfulTargets = 1.5;
            attackValue += resurrectPoints / averageNumberOfPowerfulTargets;

            const double averageNumberOfAttackers = 2.0;
            const double averageBattleTurns = 5.0;

            double powerRatio = 1
                + Math.Max(resurrectPoints - (allEnemiesThreat / averageNumberOfAttackers), 0.0)
                  / (attacker.HitPoints * 0.5);
            Debug.Assert(powerRatio > 0, "The ratio is a positive number.");

            double killTimeRatio = 1 / powerRatio;
            attackValue += allEnemiesThreat / averageNumberOfAttackers * averageBattleTurns * (1 - killTimeRatio);
        }

        return attackValue;
    }

    /// <summary>
    /// The value of every cell the attacker could strike from this turn,
    /// <c>evaluatePotentialAttackPositions()</c> (<c>ai_battle.cpp:319-397</c>).
    /// </summary>
    /// <remarks>
    /// 先按「非射手在前」排敌人，再看每个敌人周围一圈（宽单位看两圈）里哪些格子与它相邻、能站、能打；
    /// 一格能打多个目标时：多格攻击取第一个目标，射手取和，其余取最大值。
    /// </remarks>
    /// <param name="arena">The arena.</param>
    /// <param name="attacker">The unit that would strike.</param>
    /// <returns>The value of each usable cell, ordered like the reference's map.</returns>
    public static SortedDictionary<BattlePosition, double> EvaluatePotentialAttackPositions(BattleArena arena, BattleUnit attacker)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(attacker);

        BattleUnits enemies = arena.GetEnemyForce(attacker.GetCurrentColor()).ValidExcept(attacker);

        // 参考的比较器：非射手排在射手前面（射手更该被优先摘掉）。
        List<BattleUnit> orderedEnemies = [.. enemies];
        orderedEnemies.Sort(static (first, second) =>
        {
            bool firstBeforeSecond = !first.IsArchers && second.IsArchers;
            bool secondBeforeFirst = first.IsArchers && !second.IsArchers;

            return firstBeforeSecond ? -1 : (secondBeforeFirst ? 1 : 0);
        });

        double allEnemiesThreat = 0;

        foreach (BattleUnit enemy in orderedEnemies)
        {
            allEnemiesThreat += enemy.EvaluateThreatForUnit(attacker);
        }

        SortedDictionary<BattlePosition, double> result = new(BattlePositionOrder.Instance);

        foreach (BattleUnit enemyUnit in orderedEnemies)
        {
            Debug.Assert(enemyUnit.IsValid, "Only valid units are considered.");

            uint radius = attacker.IsWide ? 2u : 1u;
            HashSet<BattlePosition> processedPositions = new(BattlePositionEquality.Instance);

            foreach (int index in BattleBoard.GetDistanceIndexes(enemyUnit.Position, radius))
            {
                BattlePosition pos = BattlePosition.GetPosition(attacker, index);

                if (pos.Head == null)
                {
                    continue;
                }

                Debug.Assert(pos.IsValidForUnit(attacker), "The cell must fit the attacker.");
                Debug.Assert(BattleBoard.GetDistance(pos, enemyUnit.Position) > 0, "Only adjacent cells are considered.");

                if (BattleBoard.GetDistance(pos, enemyUnit.Position) != 1)
                {
                    continue;
                }

                if (!arena.IsPositionReachable(attacker, pos, false))
                {
                    continue;
                }

                if (!processedPositions.Add(pos))
                {
                    continue;
                }

                double attackValue = OptimalAttackValue(arena, attacker, enemyUnit, pos, allEnemiesThreat);

                if (!result.TryGetValue(pos, out double existing))
                {
                    result[pos] = attackValue;

                    continue;
                }

                if (attacker.IsAllAdjacentCellsAttack)
                {
                    // 多格攻击不看单个目标：这一格的分数已经在第一目标处算全了。
                }
                else if (enemyUnit.IsArchers)
                {
                    result[pos] = existing + attackValue;
                }
                else
                {
                    result[pos] = Math.Max(attackValue, existing);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Whether a unit could walk up to a cell in a single turn, <c>isUnitAbleToApproachPosition()</c>
    /// (<c>ai_battle.cpp:399-423</c>).
    /// </summary>
    /// <param name="unit">The unit that would walk.</param>
    /// <param name="pos">The cell it would reach.</param>
    /// <returns>Whether the cell is within one turn of movement.</returns>
    public static bool IsUnitAbleToApproachPosition(BattleUnit unit, BattlePosition pos)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(pos);

        if (pos.Head == null)
        {
            return false;
        }

        uint speed = unit.GetSpeed(false, true);

        if (speed == 0)
        {
            return false;
        }

        if (!pos.IsValidForUnit(unit))
        {
            return false;
        }

        uint distance = pos.Tail == null
            ? BattleBoard.GetDistance(unit.HeadIndex, pos.HeadIndex)
            : BattleBoard.GetDistance(unit.Position, pos);

        return distance <= speed;
    }

    /// <summary>
    /// The best blow the unit can land this turn, <c>bestAttackOutcome()</c>
    /// (<c>ai_battle.cpp:425-477</c>).
    /// </summary>
    /// <remarks>
    /// 只试与目标相邻的格子，按「离攻击者当前位置由近到远」排序；同格候选的攻击值大者胜。能否立刻出手不是筛选条件，
    /// 只记在 <see cref="MeleeAttackOutcome.CanAttackImmediately"/> 里交给调用方判断。
    /// </remarks>
    /// <param name="arena">The arena.</param>
    /// <param name="attacker">The unit that would strike.</param>
    /// <param name="defender">The unit it would hit.</param>
    /// <param name="valuesOfAttackPositions">The value of each cell, from <see cref="EvaluatePotentialAttackPositions"/>.</param>
    /// <param name="allEnemiesThreat">The summed threat of every enemy of the attacker.</param>
    /// <param name="cellFilter">An optional filter applied to the candidate cells.</param>
    /// <returns>The best blow, or «nothing found».</returns>
    public static MeleeAttackOutcome BestAttackOutcome(
        BattleArena arena,
        BattleUnit attacker,
        BattleUnit defender,
        SortedDictionary<BattlePosition, double> valuesOfAttackPositions,
        double allEnemiesThreat,
        Func<BattlePosition, bool>? cellFilter = null)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(valuesOfAttackPositions);

        MeleeAttackOutcome bestOutcome = MeleeAttackOutcome.Create();

        List<BattlePosition> aroundDefender = [];

        foreach (BattlePosition pos in valuesOfAttackPositions.Keys)
        {
            uint distance = BattleBoard.GetDistance(pos, defender.Position);
            Debug.Assert(distance > 0, "The attacker and the defender never share a cell.");

            if (distance != 1)
            {
                continue;
            }

            aroundDefender.Add(pos);
        }

        // 先试离攻击者当前位置最近的格子。
        aroundDefender.Sort((first, second) =>
            BattleBoard.GetDistance(attacker.Position, first).CompareTo(BattleBoard.GetDistance(attacker.Position, second)));

        foreach (BattlePosition pos in aroundDefender)
        {
            Debug.Assert(pos.Head != null, "A candidate cell has a head.");

            if (cellFilter != null && !cellFilter(pos))
            {
                continue;
            }

            int posHeadIndex = pos.HeadIndex;

            Debug.Assert(valuesOfAttackPositions.ContainsKey(pos), "The cell came from the map.");

            MeleeAttackOutcome current = new()
            {
                FromIndex = posHeadIndex,
                AttackValue = OptimalAttackValue(arena, attacker, defender, pos, allEnemiesThreat),
                PositionValue = valuesOfAttackPositions[pos],
                CanAttackImmediately = BattleBoard.CanAttackTargetFromPosition(arena, attacker, defender, posHeadIndex),
            };

            if (IsOutcomeImproved(current, bestOutcome))
            {
                bestOutcome = current;
            }
        }

        return bestOutcome;
    }

    /// <summary>
    /// Where to stop on the way to a unit so that the next turn can strike it,
    /// <c>findOptimalPositionForSubsequentAttack()</c> (<c>ai_battle.cpp:483-551</c>).
    /// </summary>
    /// <remarks>
    /// 沿着路径走：宽单位走到自己尾格时只需翻个身；每走一步就算「哪些近战敌人下一回合够得到这一步」，
    /// 取威胁总和最小的那一步。威胁相同时取靠前的一步（差值在 0.001 内即算相同）。
    /// </remarks>
    /// <param name="arena">The arena.</param>
    /// <param name="path">The cells to walk through.</param>
    /// <param name="currentUnit">The unit that would walk.</param>
    /// <param name="enemies">The other side, as seen by the unit.</param>
    /// <returns>The cell to stop at; -1 when there is none.</returns>
    public static int FindOptimalPositionForSubsequentAttack(BattleArena arena, IReadOnlyList<int> path, BattleUnit currentUnit, BattleUnits enemies)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(currentUnit);
        ArgumentNullException.ThrowIfNull(enemies);

        List<(BattlePosition Position, double Threat)> pathSteps = [];

        {
            BattlePosition stepPos = new(currentUnit.Position.Board);
            stepPos.CopyFrom(currentUnit.Position);

            foreach (int stepIdx in path)
            {
                if (currentUnit.IsWide)
                {
                    Debug.Assert(stepPos.Tail != null, "A wide unit always has a tail.");

                    if (stepIdx == stepPos.TailIndex)
                    {
                        stepPos.Set(stepIdx, currentUnit.IsWide, !stepPos.IsReflect);

                        continue;
                    }
                }

                stepPos.Set(stepIdx, currentUnit.IsWide, stepPos.IsReflect);
                Debug.Assert(arena.IsPositionReachable(currentUnit, stepPos, true), "The step belongs to the path.");

                BattlePosition snapshot = new(stepPos.Board, stepPos.HeadIndex, stepPos.Tail != null, stepPos.IsReflect);
                pathSteps.Add((snapshot, 0));
            }
        }

        // 飞行与射手够不到路上经过的格子。
        foreach (BattleUnit enemy in enemies)
        {
            if (enemy.IsFlying || (enemy.IsArchers && !enemy.IsHandFighting()))
            {
                continue;
            }

            for (int i = 0; i < pathSteps.Count; i++)
            {
                if (!IsUnitAbleToApproachPosition(enemy, pathSteps[i].Position))
                {
                    continue;
                }

                pathSteps[i] = (pathSteps[i].Position, pathSteps[i].Threat + enemy.EvaluateThreatForUnit(currentUnit));
            }
        }

        double lowestThreatLevel = double.MaxValue;
        int targetIdx = -1;

        for (int i = 0; i < pathSteps.Count; i++)
        {
            double stepThreatLevel = pathSteps[i].Threat;

            if (stepThreatLevel < lowestThreatLevel || Math.Abs(stepThreatLevel - lowestThreatLevel) < 0.001)
            {
                lowestThreatLevel = stepThreatLevel;
                targetIdx = pathSteps[i].Position.HeadIndex;
            }
        }

        return targetIdx;
    }

    /// <summary>
    /// The closest cell from which a unit could strike another, <c>findNearestCellNextToUnit()</c>
    /// (<c>ai_battle.cpp:553-589</c>).
    /// </summary>
    /// <param name="currentUnit">The unit that would strike.</param>
    /// <param name="target">The unit it would hit.</param>
    /// <returns>The closest such cell, or «no cell at all».</returns>
    public static CellDistanceInfo FindNearestCellNextToUnit(BattleUnit currentUnit, BattleUnit target)
    {
        ArgumentNullException.ThrowIfNull(currentUnit);
        ArgumentNullException.ThrowIfNull(target);

        uint radius = currentUnit.IsWide ? 2u : 1u;

        CellDistanceInfo found = CellDistanceInfo.None;

        foreach (int index in BattleBoard.GetDistanceIndexes(target.Position, radius))
        {
            uint distance = BattleBoard.GetDistance(currentUnit.HeadIndex, index);

            if (distance < found.Distance)
            {
                found = new CellDistanceInfo(index, distance);
            }
        }

        return found;
    }

    /// <summary>
    /// The cell a unit should move to when heading for a destination, <c>getUnitMovementTarget()</c>
    /// (<c>ai_battle.cpp:591-611</c>).
    /// </summary>
    /// <param name="arena">The arena.</param>
    /// <param name="currentUnit">The unit that would move.</param>
    /// <param name="index">The destination.</param>
    /// <returns>The cell it should actually move to this turn.</returns>
    public static int GetUnitMovementTarget(BattleArena arena, BattleUnit currentUnit, int index)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        BattlePosition dstPos = BattlePosition.GetReachable(arena, currentUnit, index);

        Debug.Assert(dstPos.Head != null, "The destination belongs to the battlefield.");

        BattlePosition currentPos = currentUnit.Position;

        if (currentPos.Contains(index) || !dstPos.IsEmpty)
        {
            Debug.Assert(arena.IsPositionReachable(currentUnit, dstPos, true),
                "The movement target must be reachable in one turn.");
        }

        BattlePosition endPos = arena.GetClosestReachablePosition(currentUnit, dstPos);
        Debug.Assert(endPos.Head != null, "The unit always has somewhere to stand.");

        return endPos.HeadIndex;
    }

    /// <summary>
    /// Whether the commander casts spells on its own, <c>isCommanderCanSpellcast()</c>
    /// (<c>ai_battle.cpp:613-617</c>).
    /// </summary>
    /// <param name="arena">The arena, for the «spells are switched off» switch.</param>
    /// <param name="commander">The commander, or null for none.</param>
    /// <returns>Whether the commander can cast.</returns>
    public static bool IsCommanderCanSpellcast(BattleArena arena, IArmyCommander? commander)
    {
        ArgumentNullException.ThrowIfNull(arena);

        return commander != null
            && (commander.GetControl() != PlayerControl.Human || Settings.Get().IsBattleAutoSpellcast)
            && commander.HasSpellBook
            && (commander.Modes & HeroModes.Spellcasted) == 0
            && !arena.IsSpellcastDisabled;
    }

    /// <summary>
    /// The most any of a commander's spells could do, <c>commanderMaximumSpellDamageValue()</c>
    /// (<c>ai_battle.cpp:537-555</c>).
    /// </summary>
    /// <remarks>
    /// 只看「战斗用、有伤害、付得起法力」的法术，取单次伤害的最大值——参考既不看目标也不乘数量。
    /// </remarks>
    /// <param name="commander">The commander.</param>
    /// <returns>The largest damage, zero when there is no such spell.</returns>
    public static double CommanderMaximumSpellDamageValue(IArmyCommander commander)
    {
        ArgumentNullException.ThrowIfNull(commander);

        double bestValue = 0;

        foreach (Spell spell in commander.GetAllSpells())
        {
            if (!spell.IsCombat() || !spell.IsDamage())
            {
                continue;
            }

            if (commander.SpellPoints < spell.SpellPoints())
            {
                continue;
            }

            bestValue = Math.Max(bestValue, SpellInfo.GetDamage(spell, (uint)commander.GetPower(), commander.BagArtifacts));
        }

        return bestValue;
    }

    /// <summary>
    /// The turn of a berserk unit, <c>berserkTurn()</c> (<c>ai_battle.cpp:638-685</c>).
    /// </summary>
    /// <remarks>
    /// 狂暴单位必须先打最近的人；没得打就把「最近」那个当作目标占位，能打就打、不能打就朝它走。
    /// </remarks>
    /// <param name="arena">The arena.</param>
    /// <param name="currentUnit">The unit with the berserk spell on it.</param>
    /// <returns>The commands to run this turn.</returns>
    public static List<BattleCommand> BerserkTurn(BattleArena arena, BattleUnit currentUnit)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        Debug.Assert(currentUnit.HasModes(BattleMonsterState.SpellBerserker), "The unit is berserk.");

        List<BattleCommand> actions = [];

        int uid = (int)currentUnit.Uid;

        BerserkerTarget target = AiBattle.GetBerserkerTarget(arena, currentUnit);

        if (target.Unit == null)
        {
            actions.Add(BattleCommand.Skip(uid));

            return actions;
        }

        if (target.Cell == -1)
        {
            actions.Add(BattleCommand.Attack(uid, (int)target.Unit.Uid, -1, -1, -1));

            return actions;
        }

        int moveTargetIdx = GetUnitMovementTarget(arena, currentUnit, target.Cell);

        if (target.CanAttackThisTurn)
        {
            actions.Add(BattleCommand.Attack(
                uid,
                (int)target.Unit.Uid,
                currentUnit.HeadIndex == moveTargetIdx ? -1 : moveTargetIdx,
                -1,
                -1));
        }
        else
        {
            actions.Add(BattleCommand.Move(uid, moveTargetIdx));
        }

        return actions;
    }
}
