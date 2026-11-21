using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using BuildingType = FHeroes2.Game.Castle.BuildingType;
using CastleTown = FHeroes2.Game.Castle.Castle;
using KingdomType = FHeroes2.Game.Kingdom.Kingdom;
using Speed = FHeroes2.Game.Kingdom.Speed;

namespace FHeroes2.Game.Ai;

/// <summary>
/// A cell plus the unit standing on it, <c>AI::BattleTargetPair</c> (<c>ai_battle.h:41-45</c>).
/// </summary>
/// <remarks>
/// The reference fills this structure through an out parameter (<c>getMeleeBestOutcome()</c>), so it stays mutable
/// in the port as well; <see cref="Cell"/> is <c>-1</c> when nothing was found.
/// </remarks>
public sealed class BattleTargetPair
{
    /// <summary>Cell of the target, <c>-1</c> when there is none.</summary>
    public int Cell { get; set; } = -1;

    /// <summary>The unit standing on that cell, or <see langword="null"/> when there is none.</summary>
    public BattleUnit? Unit { get; set; }
}

/// <summary>
/// The spell the battle AI decided to cast, <c>AI::SpellSelection</c> (<c>ai_battle.h:47-53</c>).
/// </summary>
public sealed class SpellSelection
{
    /// <summary>Id of the spell, <c>-1</c> when no spell is worth casting.</summary>
    public int SpellId { get; set; } = -1;

    /// <summary>Cell the spell is aimed at, <c>-1</c> when there is no target.</summary>
    public int Cell { get; set; } = -1;

    /// <summary>How much the cast is worth.</summary>
    public double Value { get; set; }

    /// <summary>Cell the caster moves to before casting, <c>-1</c> for "cast from where it stands".</summary>
    public int DestinationCell { get; set; } = -1;
}

/// <summary>
/// The best outcome of casting one spell, <c>AI::SpellcastOutcome</c> (<c>ai_battle.h:55-71</c>).
/// </summary>
/// <remarks>
/// <see cref="UpdateOutcome"/> keeps only the single best target, except for mass effects, which accumulate: a mass
/// spell is worth the sum of what it does to every unit it hits. <see cref="DestinationCell"/> is not touched by it
/// at all - the reference sets it directly at the few call sites that need a move first, and that asymmetry is kept.
/// </remarks>
public sealed class SpellcastOutcome
{
    /// <summary>Cell the spell is aimed at, <c>-1</c> when there is no target.</summary>
    public int Cell { get; set; } = -1;

    /// <summary>How much the cast is worth.</summary>
    public double Value { get; set; }

    /// <summary>Cell the caster moves to before casting, <c>-1</c> for "cast from where it stands".</summary>
    public int DestinationCell { get; set; } = -1;

    /// <summary>
    /// Folds one more evaluated target into the outcome, <c>SpellcastOutcome::updateOutcome()</c>
    /// (<c>ai_battle.h:61-70</c>).
    /// </summary>
    /// <param name="potentialValue">What the spell would do to that target.</param>
    /// <param name="targetCell">Cell of that target.</param>
    /// <param name="isMassEffect">Whether the spell hits every unit, in which case values are summed.</param>
    public void UpdateOutcome(double potentialValue, int targetCell, bool isMassEffect = false)
    {
        if (isMassEffect)
        {
            Value += potentialValue;
        }
        else if (potentialValue > Value)
        {
            Value = potentialValue;
            Cell = targetCell;
        }
    }
}

/// <summary>
/// Where a unit under the Berserker spell goes, <c>AI::BerserkerTarget</c> (<c>ai_battle.h:73-81</c>).
/// </summary>
public sealed class BerserkerTarget
{
    /// <summary>The unit to attack or to move towards, or <see langword="null"/> when there is none.</summary>
    public BattleUnit? Unit { get; set; }

    /// <summary>Cell next to the unit to move to, <c>-1</c> when the unit is going to be shot at.</summary>
    public int Cell { get; set; } = -1;

    /// <summary>Whether the unit can be attacked during the current turn.</summary>
    public bool CanAttackThisTurn { get; set; }
}

/// <summary>
/// The battle AI, <c>AI::BattlePlanner</c> (<c>ai_battle.h:87-165</c>, <c>ai_battle.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the contract, not the implementation</b> (ADR-161): the bodies throw until <c>ai_battle.cpp</c> is ported.
/// </para>
/// <para>
/// <b>One signature differs from the header.</b> The reference fills a <c>Battle::Actions</c> queue that the arena
/// drains later; the port has no such queue - <see cref="BattleArena.ApplyAction"/> takes one
/// <see cref="BattleCommand"/> at a time - so <see cref="BattleTurn"/> fills a list instead. The AI still decides a
/// whole turn's worth of commands at once, exactly like the reference.
/// </para>
/// </remarks>
public sealed class BattlePlanner
{
    private BattlePlanner()
    {
    }

    /// <summary>The one battle planner, <c>BattlePlanner::Get()</c>.</summary>
    public static BattlePlanner Instance { get; } = new();

    /// <summary>
    /// After this many turns without a death on either side the attacking AI has to give up,
    /// <c>MAX_TURNS_WITHOUT_DEATHS</c> (<c>ai_battle.h:139</c>).
    /// </summary>
    private const uint MaxTurnsWithoutDeaths = 50;

    /// <summary>
    /// The four turn-limit counters, for the tests that pin <see cref="BattleBegins"/> until
    /// <see cref="IsLimitOfTurnsExceeded"/> reads them (ADR-180).
    /// </summary>
    internal (uint TurnNumber, uint TurnsWithoutDeathsLeft, uint AttackerDead, uint DefenderDead) TurnLimitCounters =>
        (_currentTurnNumber, _numberOfRemainingTurnsWithoutDeaths, _attackerForceTotalNumberOfDeadUnits, _defenderForceTotalNumberOfDeadUnits);

    private uint _currentTurnNumber;

    private uint _numberOfRemainingTurnsWithoutDeaths = MaxTurnsWithoutDeaths;

    private uint _attackerForceTotalNumberOfDeadUnits;

    private uint _defenderForceTotalNumberOfDeadUnits;

    /// <summary>
    /// Resets the per-battle state, <c>BattlePlanner::battleBegins()</c> (<c>ai_battle.h:93</c>).
    /// </summary>
    /// <remarks>
    /// The reference clears the turn counter and the "turns without deaths" budget that decides when an attacking AI
    /// has to give up (<c>ai_battle.cpp</c>); the counts of the dead on both sides go back to zero with them.
    /// </remarks>
    public void BattleBegins()
    {
        _currentTurnNumber = 0;
        _numberOfRemainingTurnsWithoutDeaths = MaxTurnsWithoutDeaths;
        _attackerForceTotalNumberOfDeadUnits = 0;
        _defenderForceTotalNumberOfDeadUnits = 0;
    }

    /// <summary>
    /// Plans the turn of one unit, <c>BattlePlanner::BattleTurn()</c> (<c>ai_battle.h:95</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <param name="actions">Receives the commands the unit should perform, in order.</param>
    public void BattleTurn(BattleArena arena, BattleUnit currentUnit, List<BattleCommand> actions)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);
        ArgumentNullException.ThrowIfNull(actions);

        if (IsLimitOfTurnsExceeded(arena, actions))
        {
            return;
        }

        actions.AddRange(PlanUnitTurn(arena, currentUnit));
    }

    // -------------------------------------------------------------------------------------------------------------
    // 私有半边（ai_battle.h:97-165）。
    // -------------------------------------------------------------------------------------------------------------

    /// <summary>The colors of the current turn, <c>_myColor</c> (<c>ai_battle.h:141</c>).</summary>
    private PlayerColor _myColor;

    /// <summary>The commander of the current side, <c>_commander</c> (<c>ai_battle.h:143</c>).</summary>
    private IArmyCommander? _commander;

    /// <summary>Total strength of the current side, <c>_myArmyStrength</c> (<c>ai_battle.h:152</c>).</summary>
    private double _myArmyStrength;

    /// <summary>Total strength of the other side, <c>_enemyArmyStrength</c> (<c>ai_battle.h:153</c>).</summary>
    private double _enemyArmyStrength;

    /// <summary>Strength of the current side's shooters (with towers and spells), <c>_myShootersStrength</c>.</summary>
    private double _myShootersStrength;

    /// <summary>Strength of the other side's shooters (with towers and spells), <c>_enemyShootersStrength</c>.</summary>
    private double _enemyShootersStrength;

    /// <summary>Strength of the current side's shooter units only, <c>_myRangedUnitsOnly</c>.</summary>
    private double _myRangedUnitsOnly;

    /// <summary>Strength of the other side's shooter units only, <c>_enemyRangedUnitsOnly</c>.</summary>
    private double _enemyRangedUnitsOnly;

    /// <summary>Strength-weighted average speed of the current side, <c>_myArmyAverageSpeed</c>.</summary>
    private double _myArmyAverageSpeed;

    /// <summary>Strength-weighted average speed of the other side, <c>_enemyAverageSpeed</c>.</summary>
    private double _enemyAverageSpeed;

    /// <summary>Strategic value of the other side's spells, <c>_enemySpellStrength</c>.</summary>
    private double _enemySpellStrength;

    /// <summary>Whether the current side storms a castle, <c>_attackingCastle</c>.</summary>
    private bool _attackingCastle;

    /// <summary>Whether the current side holds a castle, <c>_defendingCastle</c>.</summary>
    private bool _defendingCastle;

    /// <summary>Whether the current side should consider running away, <c>_considerRetreat</c>.</summary>
    private bool _considerRetreat;

    /// <summary>Whether the current side should hold its ground, <c>_defensiveTactics</c>.</summary>
    private bool _defensiveTactics;

    /// <summary>Whether the current side should advance carefully, <c>_cautiousOffensive</c>.</summary>
    private bool _cautiousOffensive;

    /// <summary>Whether exposing several units to one blow must be avoided, <c>_avoidStackingUnits</c>.</summary>
    private bool _avoidStackingUnits;

    /// <summary>
    /// Adds the command that ends an auto battle which dragged on, <c>BattlePlanner::isLimitOfTurnsExceeded()</c>
    /// (<c>ai_battle.cpp:683-740</c>).
    /// </summary>
    /// <remarks>
    /// 只查进攻方：双方连续 <see cref="MaxTurnsWithoutDeaths"/> 个回合没有死人就叫停——自动战斗时关掉自动战斗，
    /// 否则让英雄撤退。计数器在新回合且无死亡时递减，一旦有人死掉就重新记满。
    /// </remarks>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="actions">Receives the command, when the limit is exceeded.</param>
    /// <returns>Whether the battle has to stop.</returns>
    private bool IsLimitOfTurnsExceeded(BattleArena arena, List<BattleCommand> actions)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(actions);

        PlayerColor currentColor = arena.GetCurrentColor();

        if (currentColor != arena.AttackingArmyColor)
        {
            return false;
        }

        uint currentTurnNumber = arena.TurnNumber;
        Debug.Assert(currentTurnNumber > 0, "The turn number starts at one.");

        if (currentTurnNumber > _currentTurnNumber && _numberOfRemainingTurnsWithoutDeaths > 0)
        {
            uint previousAttackerDead = _attackerForceTotalNumberOfDeadUnits;
            uint previousDefenderDead = _defenderForceTotalNumberOfDeadUnits;

            uint currentAttackerDead = arena.AttackingForce.GetTotalNumberOfDeadUnits();
            uint currentDefenderDead = arena.DefendingForce.GetTotalNumberOfDeadUnits();

            if (_currentTurnNumber == 0
                || currentTurnNumber - _currentTurnNumber != 1
                || previousAttackerDead != currentAttackerDead
                || previousDefenderDead != currentDefenderDead)
            {
                _numberOfRemainingTurnsWithoutDeaths = MaxTurnsWithoutDeaths;
            }
            else
            {
                _numberOfRemainingTurnsWithoutDeaths -= 1;
            }

            _attackerForceTotalNumberOfDeadUnits = currentAttackerDead;
            _defenderForceTotalNumberOfDeadUnits = currentDefenderDead;

            _currentTurnNumber = currentTurnNumber;
        }

        if (_numberOfRemainingTurnsWithoutDeaths != 0)
        {
            return false;
        }

        if (arena.AutoCombatInProgress && arena.CanToggleAutoCombat)
        {
            actions.Add(BattleCommand.ToggleAutoCombat((int)currentColor));
        }
        else
        {
            Debug.Assert(arena.CanRetreatOpponent(currentColor) && arena.GetCurrentCommander() != null,
                "Only a side that may retreat is told to.");

            actions.Add(BattleCommand.Retreat());
        }

        return true;
    }

    /// <summary>
    /// Decides the whole turn of one unit, <c>BattlePlanner::planUnitTurn()</c> (<c>ai_battle.cpp:742-1000</c>).
    /// </summary>
    /// <remarks>
    /// 五步：狂暴单位另走一路 → 分析形势 → 考虑撤退/投降 → 施法 → 射手或近战的决策树；全程没动作就跳过。
    /// </remarks>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <returns>The commands the unit should perform, in order.</returns>
    private List<BattleCommand> PlanUnitTurn(BattleArena arena, BattleUnit currentUnit)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        if (currentUnit.HasModes(BattleMonsterState.SpellBerserker))
        {
            return BattleTactics.BerserkTurn(arena, currentUnit);
        }

        List<BattleCommand> actions = [];

        AnalyzeBattleState(arena, currentUnit);

        if (_commander is Hero actualHero)
        {
            switch (GetRetreatOrSurrenderOutcome(arena, actualHero))
            {
            case FHeroes2.Game.Ai.BattlePlanner.Outcome.Retreat:
                AppendFarewellSpellcast(arena, currentUnit, actions);
                actions.Add(BattleCommand.Retreat());

                return actions;
            case FHeroes2.Game.Ai.BattlePlanner.Outcome.Surrender:
                AppendFarewellSpellcast(arena, currentUnit, actions);
                actions.Add(BattleCommand.Surrender());

                return actions;
            default:
                break;
            }
        }

        if (BattleTactics.IsCommanderCanSpellcast(arena, _commander))
        {
            SpellSelection bestSpell = SelectBestSpell(arena, currentUnit, false);

            if (bestSpell.SpellId != -1)
            {
                actions.Add(AppendSpellcast(bestSpell));

                return actions;
            }
        }

        int actionsSize = actions.Count;

        if (currentUnit.IsArchers)
        {
            actions.AddRange(ArcherDecision(arena, currentUnit));
        }
        else
        {
            BattleTargetPair target = _defensiveTactics
                ? MeleeUnitDefense(arena, currentUnit)
                : MeleeUnitOffense(arena, currentUnit);

            if (target.Cell != -1)
            {
                int moveTargetIdx = BattleTactics.GetUnitMovementTarget(arena, currentUnit, target.Cell);

                if (target.Unit != null)
                {
                    BattlePosition attackPos = BattlePosition.GetReachable(arena, currentUnit, moveTargetIdx);
                    Debug.Assert(attackPos.IsValidForUnit(currentUnit), "The cell must fit the attacker.");

                    (int attackTargetIdx, CellDirection attackDirection) =
                        BattleTactics.OptimalAttackVector(arena, currentUnit, target.Unit, attackPos);

                    actions.Add(BattleCommand.Attack(
                        (int)currentUnit.Uid,
                        (int)target.Unit.Uid,
                        currentUnit.HeadIndex == moveTargetIdx ? -1 : moveTargetIdx,
                        attackTargetIdx,
                        (int)attackDirection));
                }
                else if (currentUnit.HeadIndex != moveTargetIdx)
                {
                    actions.Add(BattleCommand.Move((int)currentUnit.Uid, moveTargetIdx));
                }
            }
        }

        if (actions.Count == actionsSize)
        {
            actions.Add(BattleCommand.Skip((int)currentUnit.Uid));
        }

        return actions;
    }

    /// <summary>
    /// Sizes up both armies at the start of a turn, <c>BattlePlanner::analyzeBattleState()</c>
    /// (<c>ai_battle.cpp:1002-1223</c>).
    /// </summary>
    /// <remarks>
    /// 先清空上回合的结论，再分别累计两边的兵力、射手力量、按兵力加权的平均速度；随后算城堡攻防的塔与射箭惩罚、
    /// 指挥官的施法价值，最后定下「守/攻/谨慎进攻」三种战术之一。
    /// </remarks>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    private void AnalyzeBattleState(BattleArena arena, BattleUnit currentUnit)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        _myColor = currentUnit.GetCurrentColor();
        _commander = arena.GetCommander(_myColor);

        BattleForce friendlyForce = arena.GetForce(_myColor);
        BattleForce enemyForce = arena.GetEnemyForce(_myColor);

        _myArmyStrength = 0;
        _enemyArmyStrength = 0;
        _myShootersStrength = 0;
        _enemyShootersStrength = 0;
        _myRangedUnitsOnly = 0;
        _enemyRangedUnitsOnly = 0;
        _myArmyAverageSpeed = 0;
        _enemyAverageSpeed = 0;
        _enemySpellStrength = 0;
        _attackingCastle = false;
        _defendingCastle = false;
        _considerRetreat = false;
        _defensiveTactics = false;
        _cautiousOffensive = false;
        _avoidStackingUnits = false;

        if (enemyForce.Count == 0)
        {
            return;
        }

        double sumEnemyStrength = 0;
        double areaAttackThreat = 0;

        foreach (BattleUnit unit in enemyForce)
        {
            if (!unit.IsValid)
            {
                continue;
            }

            double unitStrength = unit.Troop.GetStrength();

            _enemyArmyStrength += unitStrength;

            if (unit.IsArchers && !unit.IsImmovable)
            {
                _enemyRangedUnitsOnly += unitStrength;

                if (MonsterAbilities.IsPresent(unit.Monster, MonsterAbility.AreaShot) && !unit.IsHandFighting())
                {
                    areaAttackThreat += unitStrength;
                }
            }

            _enemyAverageSpeed += unit.GetSpeed(false, true) * unitStrength;
            sumEnemyStrength += unitStrength;
        }

        _enemyShootersStrength = _enemyRangedUnitsOnly;

        if (sumEnemyStrength > 0)
        {
            _enemyAverageSpeed /= sumEnemyStrength;
        }

        if (areaAttackThreat / sumEnemyStrength > 0.1)
        {
            _avoidStackingUnits = true;
        }

        uint initialUnitCount = 0;
        double sumArmyStrength = 0;

        foreach (BattleUnit unit in friendlyForce)
        {
            // 参考故意不查 isValid()：这里要把已经打光的部队也算进来。
            uint count = unit.Count;
            uint dead = unit.Dead;

            if (count > 0 || dead > 0)
            {
                ++initialUnitCount;
            }

            double unitStrength = unit.Troop.GetStrength();

            _myArmyAverageSpeed += unit.GetSpeed(false, true) * unitStrength;
            sumArmyStrength += unitStrength;

            if (count == 0 && dead > 0)
            {
                _considerRetreat = true;

                continue;
            }

            _myArmyStrength += unitStrength;

            if (unit.IsArchers && !unit.IsImmovable)
            {
                _myRangedUnitsOnly += unitStrength;
            }
        }

        _myShootersStrength = _myRangedUnitsOnly;

        if (sumArmyStrength > 0)
        {
            _myArmyAverageSpeed /= sumArmyStrength;
        }

        _considerRetreat = _considerRetreat || initialUnitCount < 4;

        if (arena.IsAnyTowerPresent && arena.Castle is { } castle)
        {
            bool attackerIgnoresCover = AttackerIgnoresCover(arena);

            double towerStrength = GetTowerStrength(arena.GetTower(BattleTowerType.Center))
                                   + GetTowerStrength(arena.GetTower(BattleTowerType.Left))
                                   + GetTowerStrength(arena.GetTower(BattleTowerType.Right));

            Debug.Assert(towerStrength >= 0, "Tower strengths are never negative.");

            if (_myColor == castle.Color)
            {
                _defendingCastle = true;
                _myShootersStrength += towerStrength;

                if (!attackerIgnoresCover)
                {
                    _enemyShootersStrength /= 1 + (GetCastleWallRangedPenalty() / 100.0);
                }
            }
            else
            {
                _attackingCastle = true;
                _enemyShootersStrength += towerStrength;

                if (!attackerIgnoresCover)
                {
                    _myShootersStrength /= 1 + (GetCastleWallRangedPenalty() / 100.0);
                }
            }
        }

        if (_commander != null && _myShootersStrength > 1)
        {
            _myShootersStrength += BattleTactics.CommanderMaximumSpellDamageValue(_commander);
        }

        if (arena.GetEnemyCommander(_myColor) is { } enemyCommander)
        {
            _enemySpellStrength = enemyCommander.GetMagicStrategicValue(_myArmyStrength);
            _enemyShootersStrength += BattleTactics.CommanderMaximumSpellDamageValue(enemyCommander);
        }

        Debug.Assert(_myArmyStrength > 0 && _enemyArmyStrength > 0, "Both sides have troops.");

        double myArcherRatio = _myShootersStrength / _myArmyStrength;
        double enemyArcherRatio = _enemyShootersStrength / _enemyArmyStrength;

        _defensiveTactics = IsDefensiveTacticsApplicable(
            currentUnit,
            myArcherRatio,
            enemyArcherRatio);

        _cautiousOffensive = enemyArcherRatio < 0.15;
    }

    /// <summary>
    /// Whether the current side should hold its ground, the lambda of
    /// <c>analyzeBattleState()</c> (<c>ai_battle.cpp:1177-1212</c>).
    /// </summary>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <param name="myArcherRatio">The share of the current side's strength that shoots.</param>
    /// <param name="enemyArcherRatio">The share of the other side's strength that shoots.</param>
    /// <returns>Whether to fight defensively.</returns>
    private bool IsDefensiveTacticsApplicable(BattleUnit currentUnit, double myArcherRatio, double enemyArcherRatio)
    {
        // 已经杀进敌方半场，就继续进攻。
        if (!IsPositionLocatedInDefendedArea(currentUnit, currentUnit.Position))
        {
            return false;
        }

        double overPowerRatio = currentUnit.IsFlying ? 6 : 10;

        if (_myArmyStrength > _enemyArmyStrength * overPowerRatio)
        {
            return false;
        }

        if (_myShootersStrength < _enemyShootersStrength)
        {
            return false;
        }

        if (_defendingCastle)
        {
            return true;
        }

        if (myArcherRatio < 0.15)
        {
            return false;
        }

        if (enemyArcherRatio > 0.66)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the attacking commander ignores the castle wall's shooting penalty,
    /// the <c>attackerIgnoresCover</c> lambda of <c>analyzeBattleState()</c> (<c>ai_battle.cpp:1117-1130</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <returns>Whether the wall gives the defenders no protection.</returns>
    private static bool AttackerIgnoresCover(BattleArena arena)
    {
        IArmyCommander? commander = arena.GetCommander(arena.AttackingArmyColor);
        Debug.Assert(commander != null, "The attacking side always has a commander.");

        if (commander.BagArtifacts.IsBonusPresent(FHeroes2.Game.Artifacts.ArtifactBonusType.NoShootingPenalty))
        {
            return true;
        }

        return commander.GetLevelSkill(SecondarySkill.Archery) != SkillLevel.None;
    }

    /// <summary>
    /// What a tower adds to the defence, the <c>getTowerStrength</c> lambda of <c>analyzeBattleState()</c>.
    /// </summary>
    /// <param name="tower">The tower, or null for none.</param>
    /// <returns>The tower's strength, zero for an absent or ruined tower.</returns>
    private static double GetTowerStrength(BattleTower? tower) =>
        tower != null && tower.IsValid ? tower.Unit.GetStrengthWithBonus(0, 0) : 0;

    /// <summary>
    /// What the castle wall takes away from enemy shooting, <c>GameStatic::getCastleWallRangedPenalty()</c>.
    /// </summary>
    /// <returns>The penalty in percent.</returns>
    private static int GetCastleWallRangedPenalty() => GameStatic.CastleWallRangedPenalty;

    /// <summary>The outcome of the retreat/surrender analysis, the local enum of <c>planUnitTurn()</c>.</summary>
    private enum Outcome
    {
        /// <summary>Keep fighting.</summary>
        ContinueBattle,

        /// <summary>Run away.</summary>
        Retreat,

        /// <summary>Give up.</summary>
        Surrender,
    }

    /// <summary>
    /// Whether the hero should run away or give up, the lambda of <c>planUnitTurn()</c>
    /// (<c>ai_battle.cpp:765-876</c>).
    /// </summary>
    /// <remarks>
    /// 只有「兵力明显劣势」才考虑；有价值的神器必须保住（能投降就投降、不能就撤退），英雄能被重新雇回来
    /// 并且等级够高时才值得保存，否则不如拼到底。
    /// </remarks>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="hero">The hero of the current side.</param>
    /// <returns>What to do.</returns>
    private Outcome GetRetreatOrSurrenderOutcome(BattleArena arena, Hero hero)
    {
        if (!_considerRetreat)
        {
            return Outcome.ContinueBattle;
        }

        // 人类控制的英雄由玩家自己决定，AI 不替他跑。
        if (hero.GetControl() != PlayerControl.Ai)
        {
            return Outcome.ContinueBattle;
        }

        GameDifficulty gameDifficulty = Settings.Get().GameDifficulty;

        if (_myArmyStrength * Difficulty.GetArmyStrengthRatioForAiRetreat(gameDifficulty) >= _enemyArmyStrength)
        {
            return Outcome.ContinueBattle;
        }

        bool hasValuableArtifacts = hero.BagArtifacts.Slots.Any(static artifact =>
            artifact != ArtifactType.Unknown
            && ArtifactTable.Get(artifact).Bonuses.Any(static bonus => bonus.Type != ArtifactBonusType.None));

        // 参考问英雄的王国（Heroes::GetKingdom()）；端口没有反向指针（ADR-120），从世界按颜色取。
        KingdomType kingdom = World.Current.GetKingdom(hero.Color);

        bool isAbleToSurrender = arena.CanSurrenderOpponent(_myColor)
            && kingdom.AllowPayment(new Funds(Resource.Gold, arena.GetForce(_myColor).GetSurrenderCost()));

        bool isPossibleToRehire = IsPossibleToRehire(hero, kingdom);

        const int minHeroTotalPrimarySkillLevelForRetreat = 10;

        if (!arena.CanRetreatOpponent(_myColor))
        {
            if (!isAbleToSurrender)
            {
                return Outcome.ContinueBattle;
            }

            if (hasValuableArtifacts)
            {
                return Outcome.Surrender;
            }

            if (!isPossibleToRehire)
            {
                return Outcome.ContinueBattle;
            }

            return hero.GetTotalPrimarySkillLevel() >= minHeroTotalPrimarySkillLevelForRetreat
                ? Outcome.Surrender
                : Outcome.ContinueBattle;
        }

        if (hasValuableArtifacts)
        {
            return Outcome.Retreat;
        }

        if (!isPossibleToRehire)
        {
            return Outcome.ContinueBattle;
        }

        return hero.GetTotalPrimarySkillLevel() >= minHeroTotalPrimarySkillLevelForRetreat
            ? Outcome.Retreat
            : Outcome.ContinueBattle;
    }

    /// <summary>
    /// Whether the hero could be hired again after the battle, the <c>isPossibleToReHire</c> lambda of
    /// <c>planUnitTurn()</c> (<c>ai_battle.cpp:803-829</c>).
    /// </summary>
    /// <param name="hero">The hero.</param>
    /// <param name="kingdom">The hero's kingdom.</param>
    /// <returns>Whether the hero is not the last hope of the kingdom.</returns>
    private static bool IsPossibleToRehire(Hero hero, KingdomType kingdom)
    {
        // 还有别的英雄：哪怕一个城都没有，别的英雄也能去打下一个。
        if (kingdom.Heroes.Count > 1)
        {
            return true;
        }

        Debug.Assert(kingdom.Heroes.Count == 1 && kingdom.Heroes[0] == hero, "The hero is the last one.");

        if (kingdom.Castles.Count == 0)
        {
            return false;
        }

        // 参考是 Heroes::inCastle()：站在那里就是城堡入口格。
        CastleTown? castle = World.Current.GetCastleEntrance(hero.MapIndex);

        return !(castle != null && kingdom.Castles.Count == 1);
    }

    /// <summary>
    /// Casts one last spell before running away, the <c>farewellSpellcast</c> lambda of <c>planUnitTurn()</c>
    /// (<c>ai_battle.cpp:878-898</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <param name="actions">Receives the command.</param>
    private void AppendFarewellSpellcast(BattleArena arena, BattleUnit currentUnit, List<BattleCommand> actions)
    {
        if (!BattleTactics.IsCommanderCanSpellcast(arena, _commander))
        {
            return;
        }

        SpellSelection bestSpell = SelectBestSpell(arena, currentUnit, true);

        if (bestSpell.SpellId == -1)
        {
            return;
        }

        actions.Add(AppendSpellcast(bestSpell));
    }

    /// <summary>
    /// Turns a spell choice into the command, the two branches of <c>planUnitTurn()</c>.
    /// </summary>
    /// <param name="selection">The chosen spell.</param>
    /// <returns>The command.</returns>
    private static BattleCommand AppendSpellcast(SpellSelection selection) =>
        selection.DestinationCell >= 0
            ? BattleCommand.SpellCast((SpellType)selection.SpellId, selection.Cell, selection.DestinationCell)
            : BattleCommand.SpellCast((SpellType)selection.SpellId, selection.Cell);

    /// <summary>
    /// What a shooter does on its turn, <c>BattlePlanner::archerDecision()</c> (<c>ai_battle.h:108</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The shooter whose turn it is.</param>
    /// <returns>The commands the shooter should perform.</returns>
    /// <remarks>
    /// 三步走：先看自己站的格子是否被敌人威胁、能不能退到更安全的地方；退不掉就贴脸近战；
    /// 还能射就按威胁值挑目标（有「云攻击」的单位改成按波及范围的威胁值之和挑，并回避误伤友军）。
    /// </remarks>
    private List<BattleCommand> ArcherDecision(BattleArena arena, BattleUnit currentUnit)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        List<BattleCommand> actions = [];

        // 当前单位可能被催眠了，敌方队列里会有它自己。
        BattleUnits enemies = arena.GetEnemyForce(_myColor).ValidExcept(currentUnit);

        int retreatPositionIndex = EvaluateArcherRetreatPosition(arena, currentUnit, enemies);

        if (retreatPositionIndex != -1)
        {
            // 移动命令给的是头格要去的位置，而候选格子是「头格能站的格子」。
            int moveTargetIndex = BattleTactics.GetUnitMovementTarget(arena, currentUnit, retreatPositionIndex);

            Debug.Assert(currentUnit.HeadIndex != moveTargetIndex, "Retreating to the cell the unit already stands on is pointless.");

            if (currentUnit.HeadIndex != moveTargetIndex)
            {
                actions.Add(BattleCommand.Move((int)currentUnit.Uid, moveTargetIndex));
            }

            return actions;
        }

        if (currentUnit.IsHandFighting())
        {
            // 被贴住的射手只能近战：打「我方伤害 − 反击伤害」最大的那个。
            BattleUnit? meleeTarget = null;
            int bestOutcome = int.MinValue;

            foreach (BattleUnit enemy in enemies)
            {
                if (BattleBoard.GetDistance(currentUnit.Position, enemy.Position) != 1)
                {
                    continue;
                }

                uint archerMeleeDamage = currentUnit.GetPotentialDamage(enemy);
                uint retaliatoryDamage = enemy.EstimateRetaliatoryDamage(archerMeleeDamage);
                int damageDifference = (int)archerMeleeDamage - (int)retaliatoryDamage;

                if (bestOutcome < damageDifference)
                {
                    bestOutcome = damageDifference;
                    meleeTarget = enemy;
                }
            }

            if (meleeTarget != null)
            {
                actions.Add(BattleCommand.Attack((int)currentUnit.Uid, (int)meleeTarget.Uid, -1, -1, -1));
            }

            return actions;
        }

        {
            // 能射。最高优先级目标的威胁值最大。
            BattleUnit? target = null;
            int targetCell = -1;
            double highestPriority = double.MinValue;

            bool isAreaShotAbilityPresent = MonsterAbilities.IsPresent(currentUnit.Monster, MonsterAbility.AreaShot);

            foreach (BattleUnit enemy in enemies)
            {
                void UpdateBestTarget(double priority, int targetIndex)
                {
                    if (highestPriority >= priority)
                    {
                        return;
                    }

                    highestPriority = priority;
                    targetCell = targetIndex;
                    target = enemy;
                }

                if (isAreaShotAbilityPresent)
                {
                    double CalculateAreaShotAttackPriority(int targetIndex, out bool isDangerousMove)
                    {
                        double result = 0;

                        // 用头格下标记集合而不是指针：浮点相加的顺序会影响结果，顺序必须确定。
                        SortedSet<int> affectedUnitsIndexes = [enemy.HeadIndex];

                        foreach (int cellIndex in BattleBoard.GetAroundIndexes(targetIndex))
                        {
                            BattleUnit? unit = arena.GetTroopBoard(cellIndex);

                            if (unit == null)
                            {
                                continue;
                            }

                            affectedUnitsIndexes.Add(unit.HeadIndex);
                        }

                        double enemyDamageHitPoints = 0;
                        double friendDamageHitPoints = 0;

                        bool isExtraLogicAllowed = Difficulty.IsBasicAiBattleLogicApplicable(
                            Settings.Get().GameDifficulty,
                            currentUnit.GetControl() == PlayerControl.Human);

                        foreach (int unitIndex in affectedUnitsIndexes)
                        {
                            BattleUnit? unit = arena.GetTroopBoard(unitIndex);
                            Debug.Assert(unit != null, "Every index in the set belongs to a unit on the board.");

                            if (isExtraLogicAllowed)
                            {
                                uint damageHitPoints = Math.Min(unit.HitPoints, currentUnit.GetPotentialDamage(unit));

                                if (currentUnit.Color == unit.GetCurrentColor())
                                {
                                    friendDamageHitPoints += damageHitPoints;
                                }
                                else
                                {
                                    enemyDamageHitPoints += damageHitPoints;
                                }
                            }

                            result += unit.EvaluateThreatForUnit(currentUnit);
                        }

                        // 打掉的友军是敌军的 3 倍及以上就不打。
                        isDangerousMove = isExtraLogicAllowed && friendDamageHitPoints >= 3 * enemyDamageHitPoints;

                        return result;
                    }

                    int enemyHeadIndex = enemy.HeadIndex;
                    Debug.Assert(enemyHeadIndex != -1, "A unit on the board has a head cell.");

                    double priority = CalculateAreaShotAttackPriority(enemyHeadIndex, out bool isDangerousMove);

                    if (!isDangerousMove)
                    {
                        UpdateBestTarget(priority, enemyHeadIndex);
                    }

                    if (enemy.IsWide)
                    {
                        int enemyTailIndex = enemy.TailIndex;
                        Debug.Assert(enemyTailIndex != -1, "A wide unit has a tail cell.");

                        priority = CalculateAreaShotAttackPriority(enemyTailIndex, out isDangerousMove);

                        if (!isDangerousMove)
                        {
                            UpdateBestTarget(priority, enemyTailIndex);
                        }
                    }

                    continue;
                }

                UpdateBestTarget(enemy.EvaluateThreatForUnit(currentUnit), -1);
            }

            if (target != null)
            {
                actions.Add(BattleCommand.Attack((int)currentUnit.Uid, (int)target.Uid, -1, targetCell, -1));
            }
        }

        return actions;
    }

    /// <summary>
    /// The safest cell a shooter could fall back to, the <c>retreatPositionIndex</c> lambda of
    /// <c>archerDecision()</c> (<c>ai_battle.cpp:1236-1435</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The shooter.</param>
    /// <param name="enemies">The units that could hurt it.</param>
    /// <returns>The head cell to retreat to, <c>-1</c> when the shooter should stay put.</returns>
    private static int EvaluateArcherRetreatPosition(BattleArena arena, BattleUnit currentUnit, BattleUnits enemies)
    {
        // 对飞行单位，退到哪儿都躲不开。
        foreach (BattleUnit enemy in enemies)
        {
            if (enemy.IsFlying)
            {
                return -1;
            }
        }

        // 「本回合能站的格子」= 当前位置 + 所有可达格。
        SortedDictionary<BattlePosition, ArcherPositionCharacteristics> potentialPositions = new(BattlePositionOrder.Instance);

        potentialPositions.TryAdd(currentUnit.Position, new ArcherPositionCharacteristics());

        foreach (int index in arena.GetAllAvailableMoves(currentUnit))
        {
            potentialPositions.TryAdd(BattlePosition.GetReachable(arena, currentUnit, index), new ArcherPositionCharacteristics());
        }

        // 评估前先把当前单位从棋盘上摘掉：它自己会挡住敌人通往候选格的路。
        BattleCell? headCell = currentUnit.Position.Head;
        BattleCell? tailCell = currentUnit.Position.Tail;

        headCell?.SetUnit(null);
        tailCell?.SetUnit(null);

        try
        {
            foreach (BattleUnit enemy in enemies)
            {
                foreach (BattlePosition position in potentialPositions.Keys)
                {
                    ArcherPositionCharacteristics characteristics = potentialPositions[position];

                    bool isPositionUnderEnemyThreat;

                    uint distanceToEnemy = BattleBoard.GetDistance(position, enemy.Position);
                    Debug.Assert(distanceToEnemy > 0, "The shooter and the enemy never share a cell.");

                    if (distanceToEnemy == 1)
                    {
                        // 与敌人相邻（哪怕是站着不动的）都算受威胁：射手在那里射不出来。
                        isPositionUnderEnemyThreat = true;
                    }
                    else if (enemy.IsArchers && !enemy.IsHandFighting())
                    {
                        // 没被贴住的射手一般威胁任何位置，但这里只按「近战威胁」算。
                        isPositionUnderEnemyThreat = false;
                    }
                    else
                    {
                        // 不考虑敌人走好运的额外移动。
                        isPositionUnderEnemyThreat = BattleTactics.IsUnitAbleToApproachPosition(enemy, position);
                    }

                    if (isPositionUnderEnemyThreat)
                    {
                        characteristics.ThreateningEnemyIndexes.Add(enemy.HeadIndex);
                    }

                    characteristics.DistanceToNearestEnemy = Math.Min(characteristics.DistanceToNearestEnemy, distanceToEnemy);
                }
            }
        }
        finally
        {
            headCell?.SetUnit(currentUnit);
            tailCell?.SetUnit(currentUnit);
        }

        {
            ArcherPositionCharacteristics currentCharacteristics = potentialPositions[currentUnit.Position];

            // 当前位置不受威胁，什么都不用做。
            if (currentCharacteristics.ThreateningEnemyIndexes.Count == 0)
            {
                return -1;
            }

            uint currentUnitSpeed = currentUnit.GetSpeed();
            Debug.Assert(currentUnitSpeed > Speed.Standing, "A shooter on the board can move.");

            // 当前位置危险，看看有没有退的必要：所有威胁者都得明显更慢。
            bool isItWorthTryingToRetreat = true;

            foreach (int enemyIndex in currentCharacteristics.ThreateningEnemyIndexes)
            {
                BattleUnit? enemy = arena.GetTroopBoard(enemyIndex);
                Debug.Assert(enemy != null && !enemy.IsFlying, "Only ground units threaten a retreating shooter.");

                // 也考虑下一回合——即便这个单位本回合已经动过。
                uint enemySpeed = enemy.GetSpeed(false, true);

                if (enemySpeed == Speed.Standing)
                {
                    // 站着不动的敌人随时都能躲开。
                    continue;
                }

                if (!(enemySpeed + 2 < currentUnitSpeed))
                {
                    isItWorthTryingToRetreat = false;

                    break;
                }
            }

            if (!isItWorthTryingToRetreat)
            {
                return -1;
            }
        }

        // 排除威胁者之后，只考虑完全安全的格子：离敌人越远越好，离战场中心越近越好（角落容易被堵）。
        int safestIndex = -1;
        uint safestDistanceToNearestEnemy = 0;
        double safestInverseDistanceToCenter = 0;

        foreach ((BattlePosition position, ArcherPositionCharacteristics characteristics) in potentialPositions)
        {
            Debug.Assert(position.Head != null && characteristics.DistanceToNearestEnemy > 0, "Candidate positions are usable cells.");

            if (characteristics.ThreateningEnemyIndexes.Count != 0)
            {
                continue;
            }

            int index = position.HeadIndex;
            uint distanceToBattlefieldCenter = BattleBoard.GetDistance(index, BattleBoard.SizeInCells / 2);

            double inverseDistanceToCenter = distanceToBattlefieldCenter == 0 ? 1.0 : 1.0 / distanceToBattlefieldCenter;

            bool isBetter = safestIndex == -1
                || characteristics.DistanceToNearestEnemy > safestDistanceToNearestEnemy
                || (characteristics.DistanceToNearestEnemy == safestDistanceToNearestEnemy
                    && inverseDistanceToCenter > safestInverseDistanceToCenter);

            if (isBetter)
            {
                safestIndex = index;
                safestDistanceToNearestEnemy = characteristics.DistanceToNearestEnemy;
                safestInverseDistanceToCenter = inverseDistanceToCenter;
            }
        }

        return safestIndex;
    }

    /// <summary>
    /// How safe a cell is, the local <c>PositionCharacteristics</c> of <c>archerDecision()</c>.
    /// </summary>
    private sealed class ArcherPositionCharacteristics
    {
        /// <summary>The head cells of the enemies that could reach the cell.</summary>
        public SortedSet<int> ThreateningEnemyIndexes { get; } = [];

        /// <summary>The distance to the nearest enemy; <see cref="uint.MaxValue"/> until the first enemy is measured.</summary>
        public uint DistanceToNearestEnemy { get; set; } = uint.MaxValue;
    }

    /// <summary>
    /// Whom a melee unit attacks when it is winning, <c>BattlePlanner::meleeUnitOffense()</c>
    /// (<c>ai_battle.h:110</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <returns>The best target, <see cref="BattleTargetPair.Cell"/> is <c>-1</c> when there is none.</returns>
    /// <remarks>
    /// 1) 本回合就能打的目标里挑最划算的；2) 打不着就朝「威胁/距离」最大的敌人走过去，先挑躲不开的；
    /// 3) 攻城时还没目标就朝城墙根下最近的一格走。
    /// </remarks>
    private BattleTargetPair MeleeUnitOffense(BattleArena arena, BattleUnit currentUnit)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        // 当前单位可能被催眠了，敌方队列里会有它自己。
        BattleUnits enemies = arena.GetEnemyForce(_myColor).ValidExcept(currentUnit);

        BattleTargetPair target = new();

        // 1. 能立刻打到的目标里挑最好的。
        {
            GetMeleeBestOutcome(arena, currentUnit, enemies, target);

            if (target.Unit != null)
            {
                return target;
            }
        }

        // 2. 打不着：按「威胁 / 到目标的距离」挑一个，朝它走。
        {
            void ChooseDistantTarget(Func<BattleUnit, bool> enemyPredicate)
            {
                double maxPriority = double.MinValue;

                foreach (BattleUnit enemy in enemies)
                {
                    CellDistanceInfo nearestCellInfo = BattleTactics.FindNearestCellNextToUnit(currentUnit, enemy);

                    if (nearestCellInfo.Index == -1)
                    {
                        continue;
                    }

                    if (!enemyPredicate(enemy))
                    {
                        continue;
                    }

                    Debug.Assert(nearestCellInfo.Distance > 0, "An adjacent enemy would have been attacked already.");

                    double priority = enemy.EvaluateThreatForUnit(currentUnit) / nearestCellInfo.Distance;

                    // 参考用的是「小于才跳过」：相等时仍然覆盖，于是后遍历到的敌人胜出。
                    if (priority < maxPriority)
                    {
                        continue;
                    }

                    maxPriority = priority;

                    BattlePosition pos = BattlePosition.GetPosition(currentUnit, nearestCellInfo.Index);
                    Debug.Assert(pos.IsValidForUnit(currentUnit), "The cell was chosen as a usable neighbour.");

                    int[] path = arena.GetPath(currentUnit, pos);
                    Debug.Assert(path.Length > 0, "The destination is reachable, so the path is not empty.");

                    if (IsMoatCellForUnit(arena, path[^1], currentUnit))
                    {
                        // 要冲过护城河：直接踩进去，下回合行动更自由。
                        target.Cell = path[^1];
                    }
                    else if (_cautiousOffensive)
                    {
                        target.Cell = BattleTactics.FindOptimalPositionForSubsequentAttack(arena, path, currentUnit, enemies);
                    }
                    else
                    {
                        target.Cell = path[^1];
                    }
                }
            }

            // 先挑「躲不开交手」的敌人。
            ChooseDistantTarget(enemy =>
            {
                Debug.Assert(!currentUnit.IsFlying, "A flyer would have attacked in step 1.");

                // 射手永远要优先压制，哪怕它跑得快。
                if (enemy.IsArchers)
                {
                    return true;
                }

                uint enemySpeed = enemy.GetSpeed(false, true);

                if (enemySpeed == Speed.Standing)
                {
                    return true;
                }

                // 飞行单位哪怕更慢也能绕场躲开。
                if (enemy.IsFlying)
                {
                    return false;
                }

                return enemySpeed < currentUnit.GetSpeed(false, true);
            });

            if (target.Cell != -1)
            {
                return target;
            }

            // 没有躲不开的，就挑「原则上够得着」的。
            ChooseDistantTarget(static _ => true);

            if (target.Cell != -1)
            {
                return target;
            }
        }

        // 3. 攻城：朝城墙根下最近的一格走。
        if (_attackingCastle)
        {
            uint shortestDistance = uint.MaxValue;

            foreach (int cellIndex in BattleTactics.CellsUnderWallsIndexes)
            {
                BattlePosition pos = BattlePosition.GetPosition(currentUnit, cellIndex);

                if (!arena.IsPositionReachable(currentUnit, pos, false))
                {
                    continue;
                }

                uint moveDistance = arena.CalculateMoveDistance(currentUnit, pos);

                if (target.Cell == -1 || moveDistance < shortestDistance)
                {
                    shortestDistance = moveDistance;
                    target.Cell = cellIndex;
                }
            }
        }

        return target;
    }

    /// <summary>
    /// Whether stepping onto a cell would put the unit in a moat, the <c>isMoatBuilt &amp;&amp; isMoatIndex()</c> test of
    /// <c>meleeUnitOffense()</c> (<c>ai_battle.cpp:1694-1696</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="index">The cell to test.</param>
    /// <param name="unit">The unit that would step there.</param>
    /// <returns>Whether the cell is moat water for that unit.</returns>
    private static bool IsMoatCellForUnit(BattleArena arena, int index, BattleUnit unit)
    {
        if (arena.Castle is not { } castle || !castle.IsBuild(BuildingType.Moat))
        {
            return false;
        }

        bool bridgeIsPassable = arena.Bridge is { } bridge && bridge.IsPassable(unit);

        return BattleBoard.IsMoatIndex(index, unit.IsFlying, bridgeIsPassable);
    }

    /// <summary>
    /// Whom a melee unit attacks when it is losing, <c>BattlePlanner::meleeUnitDefense()</c>
    /// (<c>ai_battle.h:111</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <returns>The best target, <see cref="BattleTargetPair.Cell"/> is <c>-1</c> when there is none.</returns>
    /// <remarks>
    /// 守方逻辑分两步：1) 优先把敌人从自家射手身上赶开（自己的兵被贴身时优先救它）；
    /// 2) 退而求其次，占据「在自家阵地里」的格子，让敌人来攻而不是自己冲出去。
    /// </remarks>
    private BattleTargetPair MeleeUnitDefense(BattleArena arena, BattleUnit currentUnit)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        BattleTargetPair target = new();

        SortedDictionary<BattlePosition, double> valuesOfAttackPositions =
            BattleTactics.EvaluatePotentialAttackPositions(arena, currentUnit);

        BattleUnits friendly = arena.GetForce(_myColor).ValidExcept(currentUnit);
        BattleUnits enemies = arena.GetEnemyForce(_myColor).ValidExcept(currentUnit);

        double allEnemiesThreat = 0;

        foreach (BattleUnit unit in enemies)
        {
            allEnemiesThreat += unit.EvaluateThreatForUnit(currentUnit);
        }

        // 1. 先看有没有敌人能立刻打到，记下来给后面的判断用。
        if (currentUnit.Army?.Color == _myColor)
        {
            bool isAnyEnemyCanBeAttackedImmediately = false;

            foreach (BattleUnit enemy in enemies)
            {
                MeleeAttackOutcome outcome = BattleTactics.BestAttackOutcome(
                    arena, currentUnit, enemy, valuesOfAttackPositions, allEnemiesThreat);

                if (outcome.CanAttackImmediately)
                {
                    isAnyEnemyCanBeAttackedImmediately = true;

                    break;
                }
            }

            // 离自家射手越远越不值当：每格扣这么多分。
            double defenseDistanceModifier = _myRangedUnitsOnly / 15.0;
            double bestArcherValue = double.MinValue;

            foreach (BattleUnit frnd in friendly)
            {
                if (!frnd.IsArchers)
                {
                    continue;
                }

                CellDistanceInfo bestCoverCellInfo = FindBestCoverCell(arena, currentUnit, frnd, _avoidStackingUnits);

                // 贴住自家射手的敌人：能挡住就挡。
                List<BattleUnit> adjacentEnemies = [];

                foreach (BattleUnit enemy in enemies)
                {
                    if (BattleBoard.GetDistance(frnd.Position, enemy.Position) != 1)
                    {
                        continue;
                    }

                    adjacentEnemies.Add(enemy);
                }

                // 既没位置护着它、也没敌人贴着它，这个射手不用管。
                if (bestCoverCellInfo.Index == -1 && adjacentEnemies.Count == 0)
                {
                    continue;
                }

                (bool isReachable, uint distance) = (bestCoverCellInfo.Index != -1, bestCoverCellInfo.Distance);

                foreach (BattleUnit enemy in adjacentEnemies)
                {
                    CellDistanceInfo nearestToEnemyCellInfo = BattleTactics.FindNearestCellNextToUnit(currentUnit, enemy);

                    if (nearestToEnemyCellInfo.Index == -1)
                    {
                        continue;
                    }

                    if (!isReachable || nearestToEnemyCellInfo.Distance < distance)
                    {
                        isReachable = true;
                        distance = nearestToEnemyCellInfo.Distance;
                    }
                }

                if (!isReachable)
                {
                    continue;
                }

                // 本回合就能打到敌人时，不为太远的射手分心——除非自己会飞。
                if (isAnyEnemyCanBeAttackedImmediately && !currentUnit.IsFlying && distance > currentUnit.GetSpeed() * 2)
                {
                    continue;
                }

                double archerValue = frnd.GetStrengthWithBonus(0, 0) - distance * defenseDistanceModifier;

                if (archerValue < bestArcherValue)
                {
                    continue;
                }

                bestArcherValue = archerValue;
                target.Cell = bestCoverCellInfo.Index;
                target.Unit = null;

                // 有敌人贴着这个射手：顺手打它一下，比单纯站位划算。
                {
                    MeleeAttackOutcome bestOutcome = MeleeAttackOutcome.Create();

                    foreach (BattleUnit enemy in adjacentEnemies)
                    {
                        MeleeAttackOutcome outcome = BattleTactics.BestAttackOutcome(
                            arena, currentUnit, enemy, valuesOfAttackPositions, allEnemiesThreat);

                        if (BattleTactics.IsOutcomeImproved(outcome, bestOutcome))
                        {
                            bestOutcome = outcome;

                            target.Cell = outcome.FromIndex;
                            target.Unit = outcome.CanAttackImmediately ? enemy : null;
                        }
                    }
                }

                Debug.Assert(BattleBoard.IsValidIndex(target.Cell), "Cover or an attackable enemy always yields a cell.");

                if (target.Unit != null)
                {
                    continue;
                }

                // 不打这个敌人也行，但得确保过后能打到：自己无视反击，或者对方是「云攻击」。
                if (!MonsterAbilities.IsPresent(currentUnit.Monster, MonsterAbility.NoEnemyRetaliation)
                    && !MonsterAbilities.IsPresent(frnd.Monster, MonsterAbility.AreaShot))
                {
                    continue;
                }

                // 站到射手身边，看能不能顺手打一下下面这些敌人。
                {
                    double bestAttackValue = 0;

                    foreach (BattleUnit enemy in enemies)
                    {
                        if (!BattleBoard.CanAttackTargetFromPosition(arena, currentUnit, enemy, target.Cell))
                        {
                            continue;
                        }

                        BattlePosition pos = BattlePosition.GetReachable(arena, currentUnit, target.Cell);
                        double attackValue = BattleTactics.OptimalAttackValue(arena, currentUnit, enemy, pos, allEnemiesThreat);

                        if (bestAttackValue < attackValue)
                        {
                            bestAttackValue = attackValue;
                            target.Unit = enemy;
                        }
                    }
                }
            }

            if (target.Cell != -1)
            {
                return target;
            }
        }

        // 2. 没有射手要救：退回自家阵地里最好的格子。
        {
            MeleeAttackOutcome bestOutcome = MeleeAttackOutcome.Create();

            foreach (BattleUnit enemy in enemies)
            {
                MeleeAttackOutcome outcome = BattleTactics.BestAttackOutcome(
                    arena, currentUnit, enemy, valuesOfAttackPositions, allEnemiesThreat,
                    pos => IsPositionLocatedInDefendedArea(currentUnit, pos));

                if (!BattleBoard.IsValidIndex(outcome.FromIndex))
                {
                    continue;
                }

                if (BattleTactics.IsOutcomeImproved(outcome, bestOutcome))
                {
                    bestOutcome = outcome;

                    target.Cell = outcome.FromIndex;
                    target.Unit = outcome.CanAttackImmediately ? enemy : null;
                }
            }
        }

        return target;
    }

    /// <summary>
    /// The nearest cell from which the unit could cover a friendly shooter, the local <c>findBestCoverCellInfo</c> lambda of
    /// <c>meleeUnitDefense()</c> (<c>ai_battle.cpp:1794-1884</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「挡住」的语义是：站在射手与敌人之间、贴着射手。先按首选方向从贴着射手的一圈里挑，逐个试探到第一处能站的位置。
    /// </para>
    /// <para>
    /// <paramref name="avoidStackingUnits"/> 为真时再往外挪一格，免得守卫与射手挤成一排。
    /// </para>
    /// </remarks>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit looking for a spot.</param>
    /// <param name="frnd">The shooter to cover.</param>
    /// <param name="avoidStackingUnits">Whether to keep one cell of breathing room.</param>
    /// <returns>The cell and the distance to it, or <see cref="CellDistanceInfo.None"/>.</returns>
    private static CellDistanceInfo FindBestCoverCell(BattleArena arena, BattleUnit currentUnit, BattleUnit frnd, bool avoidStackingUnits)
    {
        List<int> nearbyIndexes = [];

        // 首选的「挡在前面」方向：宽单位要挡在射手侧面，否则挡在射手正前方。
        bool preferToCoverFromTheSide = currentUnit.IsWide
            && (frnd.IsWide
                || (frnd.IsReflect
                    ? (frnd.HeadIndex / BattleBoard.WidthInCells) % 2 == 1
                    : (frnd.HeadIndex / BattleBoard.WidthInCells) % 2 == 0));

        CellDirection[] priorityDirections;

        if (preferToCoverFromTheSide)
        {
            priorityDirections = frnd.IsReflect
                ? [CellDirection.TopLeft, CellDirection.BottomLeft, CellDirection.Left,
                   CellDirection.TopRight, CellDirection.BottomRight, CellDirection.Right]
                : [CellDirection.TopRight, CellDirection.BottomRight, CellDirection.Right,
                   CellDirection.TopLeft, CellDirection.BottomLeft, CellDirection.Left];
        }
        else
        {
            priorityDirections = frnd.IsReflect
                ? [CellDirection.Left, CellDirection.TopLeft, CellDirection.BottomLeft,
                   CellDirection.TopRight, CellDirection.BottomRight, CellDirection.Right]
                : [CellDirection.Right, CellDirection.TopRight, CellDirection.BottomRight,
                   CellDirection.TopLeft, CellDirection.BottomLeft, CellDirection.Left];
        }

        foreach (int idx in new[] { frnd.HeadIndex, frnd.TailIndex })
        {
            if (!BattleBoard.IsValidIndex(idx))
            {
                continue;
            }

            foreach (CellDirection dir in priorityDirections)
            {
                if (!BattleBoard.IsValidDirection(idx, dir))
                {
                    continue;
                }

                int nearbyIndex = BattleBoard.GetIndexDirection(idx, dir);

                if (avoidStackingUnits)
                {
                    if (!BattleBoard.IsValidDirection(nearbyIndex, dir))
                    {
                        continue;
                    }

                    nearbyIndex = BattleBoard.GetIndexDirection(nearbyIndex, dir);

                    if (currentUnit.IsWide)
                    {
                        if (dir == (frnd.IsReflect ? CellDirection.Left : CellDirection.Right))
                        {
                            // 宽单位再往外挪一格。
                            if (!BattleBoard.IsValidDirection(nearbyIndex, dir))
                            {
                                continue;
                            }

                            nearbyIndex = BattleBoard.GetIndexDirection(nearbyIndex, dir);
                        }
                        else if (dir == (frnd.IsReflect ? CellDirection.Right : CellDirection.Left))
                        {
                            // 或者挪到射手身后相邻的位置。
                            BattlePosition pos = BattlePosition.GetPosition(currentUnit, nearbyIndex);

                            if (pos.Head == null)
                            {
                                continue;
                            }

                            if (BattleBoard.GetDistance(pos, frnd.Position) == 1)
                            {
                                continue;
                            }
                        }
                    }
                }

                if (nearbyIndexes.Contains(nearbyIndex))
                {
                    continue;
                }

                nearbyIndexes.Add(nearbyIndex);
            }
        }

        foreach (int idx in nearbyIndexes)
        {
            BattlePosition pos = BattlePosition.GetPosition(currentUnit, idx);

            if (pos.Head == null)
            {
                continue;
            }

            if (!arena.IsPositionReachable(currentUnit, pos, false))
            {
                continue;
            }

            return new CellDistanceInfo(idx, arena.CalculateMoveDistance(currentUnit, pos));
        }

        return CellDistanceInfo.None;
    }

    /// <summary>
    /// Whether a cell is defended by the unit's own side, <c>BattlePlanner::isPositionLocatedInDefendedArea()</c>
    /// (<c>ai_battle.h:113</c>).
    /// </summary>
    /// <param name="currentUnit">The unit that would move there.</param>
    /// <param name="pos">The cell to check.</param>
    /// <returns>Whether a friendly unit protects that cell.</returns>
    /// <remarks>
    /// 守方的「阵地」可以选两种：城堡战里是城墙围起来的格子，野地里是己方这半边。
    /// </remarks>
    private bool IsPositionLocatedInDefendedArea(BattleUnit currentUnit, BattlePosition pos)
    {
        Debug.Assert(pos.IsReflect == currentUnit.Position.IsReflect && pos.Head != null, "The candidate cell faces the same way.");

        // 被催眠的单位方向会反过来。
        bool reflect = currentUnit.Army?.Color != _myColor ? !currentUnit.IsReflect : currentUnit.IsReflect;

        bool CheckIndex(int index) => _defendingCastle
            ? BattleBoard.IsCastleIndex(index)
            : BattleBoard.GetDistanceFromBoardEdgeAlongXAxis(index, reflect) <= BattleBoard.WidthInCells / 2;

        if (!CheckIndex(pos.HeadIndex))
        {
            return false;
        }

        if (pos.Tail != null && !CheckIndex(pos.TailIndex))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// The spell worth casting this turn, <c>BattlePlanner::selectBestSpell()</c> (<c>ai_battle.h:115</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The caster.</param>
    /// <param name="retreating">Whether the caster's side is trying to get away.</param>
    /// <returns>The spell and its target, <see cref="SpellSelection.SpellId"/> is <c>-1</c> when none is worth it.</returns>
    /// <remarks>
    /// 逐个法术估价值，再按法力消耗折算：一级法术是基准（1:1），高级法术开平方折叠，撤退时不做折算。
    /// 另外还有一个「不值得就不放」的下限——两军相当时约等于单个单位强度的 20%。
    /// </remarks>
    private SpellSelection SelectBestSpell(BattleArena arena, BattleUnit currentUnit, bool retreating)
    {
        SpellSelection bestSpell = new();

        if (_commander == null)
        {
            Debug.Assert(false, "Only a side with a commander can cast spells.");

            return bestSpell;
        }

        SpellStorage allSpells = _commander.GetAllSpells();

        BattleUnits friendly = arena.GetForce(_myColor).ValidOnly();
        BattleUnits enemies = arena.GetEnemyForce(_myColor).ValidOnly();

        BattleUnits trueFriendly = arena.GetForce(_myColor).ValidOnOwnSide();
        BattleUnits trueEnemies = arena.GetEnemyForce(_myColor).ValidOnOwnSide();

        // 已经花掉一半以上法力、或自家兵力占优时，英雄该省着点用。两军相当时阈值 0.04（= 单个单位的 20%）。
        double spellValueThreshold = _myArmyStrength * _myArmyStrength / _enemyArmyStrength * 0.04;

        if (_enemyShootersStrength / _enemyArmyStrength > 0.5)
        {
            spellValueThreshold *= 0.5;
        }

        if (_commander.SpellPoints * 2 < _commander.GetMaxSpellPoints())
        {
            spellValueThreshold *= 2;
        }

        void CheckSelectBestSpell(Spell spell, SpellcastOutcome outcome)
        {
            // 按法力消耗折算价值：除以 3 让一级法术成为基准（1:1），开平方让高级法术不是线性关系。
            double spellPointValue = retreating
                ? outcome.Value
                : outcome.Value / Math.Sqrt(spell.SpellPoints(_commander.BagArtifacts) / 3.0);

            // 撤退与复活法术不看阈值：前者是保命，后者是止损。
            bool ignoreThreshold = retreating || spell.IsResurrect();

            if (spellPointValue > bestSpell.Value && (ignoreThreshold || spellPointValue > spellValueThreshold))
            {
                bestSpell.SpellId = (int)spell.Type;
                bestSpell.Cell = outcome.Cell;
                bestSpell.Value = spellPointValue;
                bestSpell.DestinationCell = outcome.DestinationCell;
            }
        }

        foreach (Spell spell in allSpells)
        {
            if (!spell.IsCombat() || arena.IsDisableCastSpell(spell) || !_commander.CanCastSpell(spell)
                || (retreating && !spell.IsDamage()))
            {
                continue;
            }

            if (spell.IsDamage())
            {
                CheckSelectBestSpell(spell, SpellDamageValue(spell, arena, currentUnit, friendly, enemies, retreating));
            }
            else if (spell.IsEffectDispel())
            {
                CheckSelectBestSpell(spell, SpellDispelValue(spell, friendly, enemies, arena));
            }
            else if (spell.IsSummon())
            {
                CheckSelectBestSpell(spell, SpellSummonValue(spell, arena, _commander.GetColor()));
            }
            else if (spell.IsResurrect())
            {
                CheckSelectBestSpell(spell, SpellResurrectValue(spell, arena));
            }
            else if (spell.Type == SpellType.DragonSlayer)
            {
                CheckSelectBestSpell(spell, SpellDragonSlayerValue(spell, friendly, enemies));
            }
            else if (spell.Type == SpellType.Teleport)
            {
                CheckSelectBestSpell(spell, SpellTeleportValue(arena, spell, currentUnit, trueEnemies));
            }
            else if (spell.Type == SpellType.Earthquake)
            {
                CheckSelectBestSpell(spell, SpellEarthquakeValue(arena, spell, friendly));
            }
            else if (spell.IsApplyToFriends())
            {
                CheckSelectBestSpell(spell, SpellEffectValue(spell, trueFriendly, enemies, arena));
            }
            else if (spell.IsApplyToEnemies())
            {
                CheckSelectBestSpell(spell, SpellEffectValue(spell, trueEnemies, enemies, arena));
            }
        }

        return bestSpell;
    }

    /// <summary>
    /// What a damaging spell would achieve, <c>BattlePlanner::spellDamageValue()</c> (<c>ai_battle.h:117</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The caster.</param>
    /// <param name="friendly">The caster's side.</param>
    /// <param name="enemies">The other side.</param>
    /// <param name="retreating">Whether the caster's side is trying to get away.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellDamageValue(Spell spell, BattleArena arena, BattleUnit currentUnit, BattleUnits friendly, BattleUnits enemies, bool retreating)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);
        ArgumentNullException.ThrowIfNull(friendly);
        ArgumentNullException.ThrowIfNull(enemies);

        SpellcastOutcome bestOutcome = new();

        if (!spell.IsDamage())
        {
            return bestOutcome;
        }

        uint spellDamage = SpellInfo.GetDamage(spell, (uint)_commander!.GetPower(), _commander.BagArtifacts);

        double DamageHeuristic(BattleUnit unit, double armyStrength, double armySpeed)
        {
            uint damage = spellDamage * (100 - unit.GetMagicResist(spell, _commander)) / 100;

            // 完全免疫：打不死人、不掉强度，被定住的单位也不会被吵醒。
            if (damage == 0)
            {
                return 0;
            }

            // 撤退时不在乎「削掉一点」，只在乎真能杀掉几个。
            if (retreating)
            {
                // 反正战斗结束就消失，杀镜像没有意义。
                if (unit.HasModes(BattleMonsterState.CapMirrorImage))
                {
                    return 0;
                }

                return MonsterStatistics.GetMonsterStrength(unit.Monster) * unit.HowManyWillBeKilled(damage);
            }

            // 能整队打掉时，用整队强度加上「清空一堆」的奖励。
            uint hitPoints = unit.HasModes(BattleMonsterState.CapMirrorImage) ? 1 : unit.HitPoints;

            if (damage >= hitPoints)
            {
                double overallStrength = unit.GetStrengthWithBonus(0, 0);
                double bonus = unit.GetSpeed() > armySpeed ? 0.07 : 0.035;

                if (unit.HasModes(BattleMonsterState.CapMirrorOwner))
                {
                    // 这堆有镜像：镜像也一并算进收益。
                    foreach (BattleUnit enemyUnit in enemies)
                    {
                        if (enemyUnit.HasModes(BattleMonsterState.CapMirrorImage) && enemyUnit.MirrorUnit == unit)
                        {
                            overallStrength += enemyUnit.GetStrengthWithBonus(0, 0);
                        }
                    }
                }

                return overallStrength + armyStrength * bonus;
            }

            // 否则按「削掉了这一堆的百分之多少」算。
            double unitPercentageLost = Math.Min(damage / (double)hitPoints, 1.0);

            // 打醒被定住的单位要扣分：只打掉三成，剩下七成就是代价。
            if (unit.IsImmovable)
            {
                unitPercentageLost += unitPercentageLost - 1.0;
            }

            return unitPercentageLost * unit.GetStrengthWithBonus(0, 0);
        }

        if (spell.IsSingleTarget())
        {
            foreach (BattleUnit enemy in enemies)
            {
                bestOutcome.UpdateOutcome(DamageHeuristic(enemy, _enemyArmyStrength, _enemyAverageSpeed), enemy.HeadIndex);
            }
        }
        else if (spell.IsApplyWithoutFocusObject())
        {
            double spellHeuristic = 0;

            foreach (BattleUnit enemy in enemies)
            {
                spellHeuristic += DamageHeuristic(enemy, _enemyArmyStrength, _enemyAverageSpeed);
            }

            foreach (BattleUnit unit in friendly)
            {
                double valueLost = DamageHeuristic(unit, _myArmyStrength, _myArmyAverageSpeed);

                // 撤退时会把自己也打死：这个法术不能用，直接放弃。
                if (retreating && unit.Uid == currentUnit.Uid
                    && Math.Abs(valueLost - unit.GetStrengthWithBonus(0, 0)) < 0.001)
                {
                    return bestOutcome;
                }

                spellHeuristic -= valueLost;
            }

            bestOutcome.UpdateOutcome(spellHeuristic, -1);
        }
        else
        {
            // 范围法术（如火球）：对每个可能的落点估一次。
            void AreaOfEffectCheck(List<BattleSpellTarget> targets, int index)
            {
                double spellHeuristic = 0;

                foreach (BattleSpellTarget target in targets)
                {
                    BattleUnit defender = target.Defender!;

                    if (defender.GetCurrentColor() == _myColor)
                    {
                        double valueLost = DamageHeuristic(defender, _myArmyStrength, _myArmyAverageSpeed);

                        // 撤退时会把自己也打死：这个落点不要。
                        if (retreating && defender.Uid == currentUnit.Uid
                            && Math.Abs(valueLost - defender.GetStrengthWithBonus(0, 0)) < 0.001)
                        {
                            return;
                        }

                        spellHeuristic -= valueLost;
                    }
                    else
                    {
                        spellHeuristic += DamageHeuristic(defender, _enemyArmyStrength, _enemyAverageSpeed);
                    }
                }

                bestOutcome.UpdateOutcome(spellHeuristic, index);
            }

            if (spell.Type == SpellType.ChainLightning)
            {
                // 链闪电只从「被允许施法的敌人」起跳，否则算出来的链子根本不会发生。
                foreach (BattleUnit enemy in enemies)
                {
                    if (!enemy.AllowApplySpell(spell, _commander))
                    {
                        continue;
                    }

                    int index = enemy.HeadIndex;
                    AreaOfEffectCheck(arena.GetTargetsForSpell(_commander, spell, index), index);
                }
            }
            else
            {
                for (int index = 0; index < BattleBoard.SizeInCells; ++index)
                {
                    AreaOfEffectCheck(arena.GetTargetsForSpell(_commander, spell, index), index);
                }
            }
        }

        return bestOutcome;
    }

    /// <summary>
    /// What dispelling the enemy's spells would achieve, <c>BattlePlanner::spellDispelValue()</c>
    /// (<c>ai_battle.h:119</c>).
    /// </summary>
    /// <param name="spell">The dispel spell.</param>
    /// <param name="friendly">The caster's side.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellDispelValue(Spell spell, BattleUnits friendly, BattleUnits enemies, BattleArena arena)
    {
        ArgumentNullException.ThrowIfNull(friendly);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(arena);

        bool isMassSpell = spell.IsMassActions();
        bool isDispel = spell.Type is SpellType.Dispel or SpellType.MassDispel;

        SpellcastOutcome bestOutcome = new();

        foreach (BattleUnit unit in friendly)
        {
            if (!unit.HasModes(BattleMonsterState.Magic))
            {
                continue;
            }

            double unitValue = 0;

            foreach (Spell spellOnFriend in unit.GetCurrentSpellEffects())
            {
                double effectValue = SpellEffectValue(spellOnFriend, unit, enemies, false, true, arena);

                if (spellOnFriend.IsApplyToEnemies())
                {
                    // 自己身上挂着的坏法术：驱散掉是赚的。
                    unitValue += effectValue;
                }
                else if (isDispel && spellOnFriend.IsApplyToFriends())
                {
                    // 自己身上的好法术：驱散掉是亏的。
                    unitValue -= effectValue;
                }
            }

            bestOutcome.UpdateOutcome(unitValue, unit.HeadIndex, isMassSpell);
        }

        if (isDispel)
        {
            bool enemyLastUnit = enemies.Count == 1;

            foreach (BattleUnit unit in enemies)
            {
                if (!unit.HasModes(BattleMonsterState.Magic))
                {
                    continue;
                }

                double unitValue = 0;

                foreach (Spell spellOnEnemy in unit.GetCurrentSpellEffects())
                {
                    double effectValue = SpellEffectValue(spellOnEnemy, unit, enemies, enemyLastUnit, true, arena);

                    // 敌人身上的好法术被驱散是赚的，坏法术被驱散是亏的。
                    unitValue += spellOnEnemy.IsApplyToFriends() ? effectValue : -effectValue;
                }

                bestOutcome.UpdateOutcome(unitValue, unit.HeadIndex, isMassSpell);
            }
        }

        return bestOutcome;
    }

    /// <summary>
    /// What resurrecting the fallen would achieve, <c>BattlePlanner::spellResurrectValue()</c>
    /// (<c>ai_battle.h:120</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellResurrectValue(Spell spell, BattleArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);

        uint hpRestored = SpellInfo.GetResurrectPoints(spell, (uint)_commander!.GetPower(), _commander.BagArtifacts);

        SpellcastOutcome bestOutcome = new();

        void UpdateBestOutcome(BattleUnit unit)
        {
            uint missingHitPoints = unit.MissingHitPoints;
            missingHitPoints = missingHitPoints < hpRestored ? missingHitPoints : hpRestored;

            // 复活的价值按「补回来多少个兵」算：补的血量除以单个兵的血量，再乘单体强度。
            double spellValue = missingHitPoints * MonsterStatistics.GetMonsterStrength(unit.Monster)
                                / MonsterStatistics.GetBattleStats(unit.Monster).HitPoints;

            // 已经赢定的战斗：永久的复活（不只是把这一仗救回来）值双倍。
            if (_myArmyStrength > _enemyArmyStrength && spell.Type != SpellType.Resurrect)
            {
                spellValue *= 2;
            }

            bestOutcome.UpdateOutcome(spellValue, unit.HeadIndex);
        }

        // 先看还活着的堆。
        foreach (BattleUnit unit in arena.GetForce(_myColor))
        {
            if (!unit.IsValid || !unit.AllowApplySpell(spell, _commander))
            {
                continue;
            }

            UpdateBestOutcome(unit);
        }

        // 再看坟场里的。
        foreach (int index in arena.Graveyard.GetOccupiedCells())
        {
            if (!arena.IsAbleToResurrectFromGraveyard(index, spell))
            {
                continue;
            }

            BattleUnit? unit = arena.GetLastResurrectableUnitFromGraveyard(index, spell);
            Debug.Assert(unit != null && !unit.IsValid, "A resurrectable cell holds a fallen unit.");

            UpdateBestOutcome(unit);
        }

        return bestOutcome;
    }

    /// <summary>
    /// What summoning new troops would achieve, <c>BattlePlanner::spellSummonValue()</c> (<c>ai_battle.h:121</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="heroColor">The color of the caster's hero.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellSummonValue(Spell spell, BattleArena arena, PlayerColor heroColor)
    {
        ArgumentNullException.ThrowIfNull(arena);

        SpellcastOutcome bestOutcome = new();

        if (!spell.IsSummon())
        {
            return bestOutcome;
        }

        if (arena.GetFreePositionNearHero(heroColor) < 0)
        {
            return bestOutcome;
        }

        // 参考直接拿法术 ID 当怪物 ID 用：44 到 47 正好是四个召唤法术对应的元素。
        Troop summon = new(
            (MonsterType)(int)spell.Type,
            SpellInfo.GetSummonMonsterCount(spell, (uint)_commander!.GetPower(), _commander.BagArtifacts));

        bestOutcome.Value = summon.GetStrengthWithBonus(_commander.GetAttack(), _commander.GetDefense());

        // 已经大优时，多一支兵没那么值钱。
        if (_myArmyStrength > _enemyArmyStrength * 2)
        {
            bestOutcome.Value /= 2;
        }

        return bestOutcome;
    }

    /// <summary>
    /// What Dragon Slayer would achieve, <c>BattlePlanner::spellDragonSlayerValue()</c> (<c>ai_battle.h:122</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="friendly">The caster's side.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellDragonSlayerValue(Spell spell, BattleUnits friendly, BattleUnits enemies)
    {
        ArgumentNullException.ThrowIfNull(friendly);
        ArgumentNullException.ThrowIfNull(enemies);

        double enemyArmyStrength = 0;
        double dragonsStrength = 0;
        int numOfSlotsWithEnemyDragons = 0;

        foreach (BattleUnit unit in enemies)
        {
            double strength = unit.GetStrengthWithBonus(0, 0);

            if (MonsterAbilities.IsDragons(unit.Monster))
            {
                ++numOfSlotsWithEnemyDragons;
                dragonsStrength += strength;
            }

            enemyArmyStrength += strength;
        }

        SpellcastOutcome bestOutcome = new();

        if (numOfSlotsWithEnemyDragons == 0)
        {
            // 对面一条龙都没有，这个法术白放。
            return bestOutcome;
        }

        // 借鲜血欲望的估值：两者都是加攻击，但屠龙只对龙加倍，所以按龙占对面兵力的比例折算。
        double bloodlustAttackBonus = new Spell(SpellType.BloodLust).ExtraValue();
        double dragonSlayerAttackBonus = spell.ExtraValue();

        double dragonSlayerRatio = 0.1 * dragonSlayerAttackBonus / bloodlustAttackBonus
                                   * dragonsStrength / enemyArmyStrength;

        foreach (BattleUnit unit in friendly)
        {
            if (IsSpellcastUselessForUnit(unit, enemies, spell))
            {
                continue;
            }

            double unitValue = unit.GetStrengthWithBonus(0, 0) * dragonSlayerRatio * SpellDurationMultiplier(unit);
            bestOutcome.UpdateOutcome(unitValue, unit.HeadIndex);
        }

        return bestOutcome;
    }

    /// <summary>
    /// What teleporting a unit would achieve, <c>BattlePlanner::spellTeleportValue()</c> (<c>ai_battle.h:123</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="currentUnit">The caster.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellTeleportValue(BattleArena arena, Spell spell, BattleUnit currentUnit, BattleUnits enemies)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);
        ArgumentNullException.ThrowIfNull(enemies);

        // 被催眠的单位交给别的逻辑处理。
        if (currentUnit.HasModes(BattleMonsterState.SpellHypnotize))
        {
            return new SpellcastOutcome();
        }

        // 防守时不用传送。
        if (_defensiveTactics)
        {
            return new SpellcastOutcome();
        }

        if (IsSpellcastUselessForUnit(currentUnit, enemies, spell))
        {
            return new SpellcastOutcome();
        }

        // 会飞的本来就能去任何格子。
        if (currentUnit.IsFlying)
        {
            return new SpellcastOutcome();
        }

        // 射手不需要传送。
        if (currentUnit.IsArchers)
        {
            return new SpellcastOutcome();
        }

        int currentPosition = currentUnit.HeadIndex;

        if (GetMeleeBestOutcome(arena, currentUnit, enemies, new BattleTargetPair()) > 0.1)
        {
            // 现在就能打到人，不必传送。
            return new SpellcastOutcome();
        }

        BattleUnit? temporaryUnit = null;

        foreach (BattleUnit unit in arena.GetForce(_myColor).ValidOnly())
        {
            if (ReferenceEquals(unit, currentUnit))
            {
                temporaryUnit = unit;

                break;
            }
        }

        if (temporaryUnit == null)
        {
            Debug.Assert(false, "The caster belongs to its own side.");

            return new SpellcastOutcome();
        }

        BattleTargetPair bestTarget = new();

        double bestDamage;

        // 临时给它传送能力，再按「能去任何格子」重算一遍；异常路径也要撤掉。
        temporaryUnit.SetModes(BattleMonsterState.TeleportAbility);

        try
        {
            bestDamage = GetMeleeBestOutcome(arena, currentUnit, enemies, bestTarget);
        }
        finally
        {
            temporaryUnit.ResetModes(BattleMonsterState.TeleportAbility);
        }

        if (bestDamage < 0.1)
        {
            // 传送过去也够不着人。
            return new SpellcastOutcome();
        }

        return new SpellcastOutcome
        {
            Cell = currentPosition,
            Value = currentUnit.GetStrengthWithBonus(0, 0) * 0.1,
            DestinationCell = bestTarget.Cell
        };
    }

    /// <summary>
    /// What an earthquake would achieve, <c>BattlePlanner::spellEarthquakeValue()</c> (<c>ai_battle.h:124</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="friendly">The caster's side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellEarthquakeValue(BattleArena arena, Spell spell, BattleUnits friendly)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(friendly);

        // 不是在攻城，地震毫无意义。
        if (!_attackingCastle)
        {
            return new SpellcastOutcome();
        }

        // 全是飞行或射手时我们不冲城墙，城墙对我们就没价值。
        int meleeUnits = 0;
        double meleeStrength = 0;

        foreach (BattleUnit unit in friendly)
        {
            if (!unit.IsFlying && !unit.IsArchers)
            {
                ++meleeUnits;
                meleeStrength += unit.GetStrengthWithBonus(0, 0);
            }
        }

        if (meleeUnits == 0)
        {
            return new SpellcastOutcome();
        }

        int totalTargets = 0;
        int targetsToDestroy = 0;

        foreach (CastleDefenseStructure target in BattleArena.GetEarthQuakeSpellTargets())
        {
            if (target is CastleDefenseStructure.TopBridgeTower or CastleDefenseStructure.BottomBridgeTower)
            {
                // 纯装饰建筑，没有价值。
                continue;
            }

            int targetValue = arena.GetCastleDefenseStructureCondition(target, SiegeWeaponType.EarthquakeSpell);

            ++totalTargets;

            if (targetValue > 0)
            {
                ++targetsToDestroy;
            }
        }

        (uint minDamage, uint maxDamage) = BattleArena.GetEarthquakeDamageRange(_commander!);

        double enemyShooterRatio = _enemyShootersStrength / _enemyArmyStrength;
        double targetRatio = targetsToDestroy * 1.0 / totalTargets;
        double averageDamage = (maxDamage - minDamage) / 2.0;
        double meleeRatio = meleeStrength / _myArmyStrength;

        return new SpellcastOutcome
        {
            Cell = 0,
            Value = meleeUnits * meleeStrength * meleeRatio * targetRatio * averageDamage * enemyShooterRatio * 0.2
        };
    }

    /// <summary>
    /// What a spell would do to the units it can reach, <c>BattlePlanner::spellEffectValue()</c>
    /// (<c>ai_battle.h:125</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="targets">The units the spell can reach.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome over all targets.</returns>
    private SpellcastOutcome SpellEffectValue(Spell spell, BattleUnits targets, BattleUnits enemies, BattleArena arena)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(arena);

        bool isSingleTargetLeft = targets.Count == 1;
        bool isMassSpell = spell.IsMassActions();

        SpellcastOutcome bestOutcome = new();

        foreach (BattleUnit unit in targets)
        {
            bestOutcome.UpdateOutcome(
                SpellEffectValue(spell, unit, enemies, isSingleTargetLeft, false, arena), unit.HeadIndex, isMassSpell);
        }

        return bestOutcome;
    }

    /// <summary>
    /// What a spell would do to one unit, <c>BattlePlanner::spellEffectValue()</c> (<c>ai_battle.h:127</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="target">The unit it would hit.</param>
    /// <param name="enemies">The other side.</param>
    /// <param name="targetIsLast">Whether this is the last unit of its side.</param>
    /// <param name="forDispel">Whether the value is asked for a dispel, which only counts the harmful effects.</param>
    /// <returns>How much the spell is worth against that unit.</returns>
    private double SpellEffectValue(
        Spell spell, BattleUnit target, BattleUnits enemies, bool targetIsLast, bool forDispel, BattleArena arena)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(arena);

        // 先过一道「这个法术对它有没有意义」；评估驱散时跳过，因为要算的是被驱散掉的法术本身。
        if (!forDispel && (IsSpellcastUselessForUnit(target, enemies, spell) || !target.AllowApplySpell(spell, _commander)))
        {
            return 0;
        }

        uint targetDamageMin = (uint)MonsterStatistics.GetBattleStats(target.Monster).DamageMin;
        uint targetDamageMax = (uint)MonsterStatistics.GetBattleStats(target.Monster).DamageMax;

        double ratio;

        switch (spell.Type)
        {
            case SpellType.Slow:
            case SpellType.MassSlow:
                ratio = GetSpellSlowRatio(target);

                break;

            case SpellType.Blind:
                if (targetIsLast)
                {
                    // 对最后一堆下失明要谨慎：它会还手，五成伤害不算少。
                    if (MonsterAbilities.IsPresent(target.Monster, MonsterAbility.UnlimitedRetaliation)
                        || !target.IsRetaliationAllowed)
                    {
                        return 0;
                    }

                    ratio = 0.4;
                }
                else
                {
                    ratio = 0.8;
                }

                break;

            case SpellType.Curse:
            case SpellType.MassCurse:
                if (targetDamageMax == targetDamageMin)
                {
                    // 伤害上下限一样，诅咒无从发挥。
                    return 0;
                }

                ratio = 0.15;

                break;

            case SpellType.Berserker:
            {
                if (targetIsLast)
                {
                    // 对面只剩一堆时没意义：它本来就只能打我们。
                    return 0;
                }

                BerserkerTarget berserkerTarget = AiBattle.GetBerserkerTarget(arena, target);

                if (berserkerTarget.Unit != null && berserkerTarget.Unit.GetCurrentOrArmyColor() == _myColor)
                {
                    // 它本来就要冲我们的人，狂暴没改变什么。
                    return 0;
                }

                ratio = 0.85;

                break;
            }

            case SpellType.Paralyze:
                if (targetIsLast)
                {
                    if (MonsterAbilities.IsPresent(target.Monster, MonsterAbility.UnlimitedRetaliation)
                        || !target.IsRetaliationAllowed)
                    {
                        return 0;
                    }

                    if (SpellDurationMultiplier(target) < 1)
                    {
                        return 0;
                    }

                    ratio = 0.5;
                }
                else
                {
                    ratio = 0.85;
                }

                break;

            case SpellType.Hypnotize:
                ratio = 1.5;

                break;

            case SpellType.DisruptingRay:
                ratio = GetSpellDisruptingRayRatio(target);

                break;

            case SpellType.Haste:
            case SpellType.MassHaste:
                ratio = GetSpellHasteRatio(target);

                break;

            case SpellType.BloodLust:
                ratio = 0.1;

                break;

            case SpellType.Bless:
            case SpellType.MassBless:
                if (targetDamageMax == targetDamageMin)
                {
                    // 伤害已经固定在上限，祝福无从发挥。
                    return 0;
                }

                ratio = 0.15;

                break;

            case SpellType.StoneSkin:
                ratio = 0.1;

                break;

            case SpellType.SteelSkin:
                ratio = 0.2;

                break;

            // 下面几个的价值取决于具体局面，ratio 在下面再定。
            case SpellType.AntiMagic:
            case SpellType.MirrorImage:
            case SpellType.Shield:
            case SpellType.MassShield:
                ratio = 0;

                break;

            default:
                return 0;
        }

        if (target.HasModes(BattleMonsterState.SpellBless) && spell.Type is SpellType.Curse or SpellType.MassCurse)
        {
            // 抵消掉已有的祝福，价值翻倍。
            ratio *= 2;
        }
        else if (target.HasModes(BattleMonsterState.SpellCurse) && spell.Type is SpellType.Bless or SpellType.MassBless)
        {
            ratio *= 2;
        }
        else if (spell.Type == SpellType.AntiMagic
                 && !target.HasModes(BattleMonsterState.GoodMagic)
                 && _enemySpellStrength > 200.0)
        {
            double ratioLimit = 0.9;

            foreach (Spell otherSpell in _commander!.GetAllSpells())
            {
                if (otherSpell.IsResurrect() && _commander.CanCastSpell(otherSpell)
                    && target.AllowApplySpell(otherSpell, _commander))
                {
                    // 将来还能复活它：抗魔的价值要留出余地。
                    ratioLimit = 0.35;

                    break;
                }
            }

            // 把 0...5000 的敌方法术强度折成 0.0~0.9 的比例，再夹上限。
            ratio = Math.Min(_enemySpellStrength / 200.0 * 0.036, ratioLimit);

            // 敌人英雄比它的军队还强，可能打完就跑：抗魔更值钱。
            if (_enemySpellStrength > _enemyArmyStrength)
            {
                ratio *= 1.5;
            }

            if (target.HasModes(BattleMonsterState.BadMagic))
            {
                ratio *= 2;
            }
        }
        else if (spell.Type == SpellType.MirrorImage)
        {
            ratio = target.IsArchers ? 1.0 : target.IsFlying ? 0.55 : 0.33;

            // 慢的单位可能还没轮到就被打掉了。
            if (target.GetSpeed() < _enemyAverageSpeed)
            {
                ratio /= 5;
            }
        }
        else if (spell.Type == SpellType.Berserker && !target.IsArchers)
        {
            ratio /= BattleTactics.ReduceEffectivenessByDistance(target);
        }
        else if (spell.Type is SpellType.Shield or SpellType.MassShield)
        {
            ratio = _enemyRangedUnitsOnly / _enemyArmyStrength * 0.3;

            if (target.IsArchers)
            {
                ratio *= 1.25;
            }
        }

        return target.GetStrengthWithBonus(0, 0) * ratio * SpellDurationMultiplier(target);
    }

    /// <summary>
    /// How much a Disrupting Ray is worth against a unit, <c>BattlePlanner::getSpellDisruptingRayRatio()</c>
    /// (<c>ai_battle.h:128</c>).
    /// </summary>
    /// <param name="target">The unit the spell would hit.</param>
    /// <returns>The ratio the spell's value is multiplied by.</returns>
    private double GetSpellDisruptingRayRatio(BattleUnit target)
    {
        ArgumentNullException.ThrowIfNull(target);

        uint targetDefense = target.GetDefense();

        if (targetDefense <= 1)
        {
            // 防御已经见底，这个法术没用。
            return 0;
        }

        double ratio = 0.2;

        uint spellValue = new Spell(SpellType.DisruptingRay).ExtraValue();

        if (targetDefense <= spellValue)
        {
            // 减不满：按实际能减掉的比例折算。
            Debug.Assert(spellValue > 0, "Disrupting Ray lowers defense by a positive amount.");

            if (spellValue == 0)
            {
                return 0;
            }

            double actualDefenseChange = targetDefense - 1.0;
            ratio *= actualDefenseChange / spellValue;
        }

        double targetStrength = target.GetStrengthWithBonus(0, 0);

        // 自家兵力远不如这一堆时，降低比例，让直接伤害法术更优先。
        if (_myArmyStrength < targetStrength)
        {
            ratio *= _myArmyStrength / targetStrength;
        }

        return ratio;
    }

    /// <summary>
    /// How much a Slow is worth against a unit, <c>BattlePlanner::getSpellSlowRatio()</c> (<c>ai_battle.h:129</c>).
    /// </summary>
    /// <param name="target">The unit the spell would hit.</param>
    /// <returns>The ratio the spell's value is multiplied by.</returns>
    private double GetSpellSlowRatio(BattleUnit target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsArchers || _attackingCastle)
        {
            // 对射手或守城部队减速没意义。
            return 0.01;
        }

        uint currentSpeed = target.GetSpeed(false, true);
        uint newSpeed = Speed.GetSlowSpeedFromSpell(currentSpeed);
        uint lostSpeed = currentSpeed - newSpeed;

        double ratio = 0.1 * lostSpeed;

        if (currentSpeed < _myArmyAverageSpeed)
        {
            // 目标本来就比我们全军慢，减速用处不大。
            ratio /= 2;
        }

        if (target.HasModes(BattleMonsterState.SpellHaste))
        {
            // 抵消加速，价值翻倍。
            ratio *= 2;
        }
        else if (!target.IsFlying)
        {
            ratio /= BattleTactics.ReduceEffectivenessByDistance(target);
        }

        return ratio;
    }

    /// <summary>
    /// How much a Haste is worth for a unit, <c>BattlePlanner::getSpellHasteRatio()</c> (<c>ai_battle.h:130</c>).
    /// </summary>
    /// <param name="target">The unit the spell would help.</param>
    /// <returns>The ratio the spell's value is multiplied by.</returns>
    private double GetSpellHasteRatio(BattleUnit target)
    {
        ArgumentNullException.ThrowIfNull(target);

        uint currentSpeed = target.GetSpeed(false, true);
        uint newSpeed = Speed.GetHasteSpeedFromSpell(currentSpeed);
        uint gainedSpeed = newSpeed - currentSpeed;

        double ratio = 0.05 * gainedSpeed;

        if (currentSpeed < _enemyAverageSpeed)
        {
            // 目标比敌人全军还慢，加速非常有价值。
            ratio *= 2;
        }

        if (target.HasModes(BattleMonsterState.SpellSlow))
        {
            // 抵消减速，价值翻倍。
            ratio *= 2;
        }
        else if (target.IsArchers || _defensiveTactics)
        {
            // 不需要移动时，加速没那么值钱。
            ratio /= 2;
        }

        return ratio;
    }

    /// <summary>
    /// How long a spell lasts on a unit, <c>BattlePlanner::spellDurationMultiplier()</c> (<c>ai_battle.h:131</c>).
    /// </summary>
    /// <param name="target">The unit the spell would hit.</param>
    /// <returns>The multiplier of the spell's duration.</returns>
    private int SpellDurationMultiplier(BattleUnit target)
    {
        ArgumentNullException.ThrowIfNull(target);

        int duration = GetSpellPower(_commander!);

        // 参考：只持续一回合、而目标这回合已经动过，等于没生效。
        if (duration < 2 && target.HasModes(BattleMonsterState.Moved))
        {
            return 0;
        }

        return 1;
    }

    /// <summary>
    /// The caster's effective spell power, <c>getSpellPower()</c> (<c>ai_battle_spell.cpp:63-68</c>).
    /// </summary>
    /// <remarks>英雄的力量加上宝物给的法术持续时间加成。</remarks>
    /// <param name="hero">The caster.</param>
    /// <returns>The spell power used for durations.</returns>
    private static int GetSpellPower(IArmyCommander hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        return hero.GetPower()
               + hero.BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.EveryCombatSpellDuration);
    }

    /// <summary>
    /// Whether casting a spell on a unit is a waste, <c>BattlePlanner::isSpellcastUselessForUnit()</c>
    /// (<c>ai_battle.h:133</c>).
    /// </summary>
    /// <param name="unit">The unit the spell would hit.</param>
    /// <param name="enemies">The other side.</param>
    /// <param name="spell">The spell to evaluate.</param>
    /// <returns>Whether the spell changes nothing.</returns>
    private bool IsSpellcastUselessForUnit(BattleUnit unit, BattleUnits enemies, Spell spell)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(enemies);

        // 已经动不了的单位，除了抗魔什么都不用加。
        if (unit.IsImmovable && spell.Type != SpellType.AntiMagic)
        {
            return true;
        }

        switch (spell.Type)
        {
            case SpellType.Bless:
            case SpellType.MassBless:
                return unit.HasModes(BattleMonsterState.SpellBless);

            case SpellType.BloodLust:
                return unit.HasModes(BattleMonsterState.SpellBloodlust);

            case SpellType.Curse:
            case SpellType.MassCurse:
                return unit.HasModes(BattleMonsterState.SpellCurse);

            case SpellType.Haste:
            case SpellType.MassHaste:
                return unit.HasModes(BattleMonsterState.SpellHaste) || unit.GetSpeed() == Speed.Instant;

            case SpellType.Shield:
            case SpellType.MassShield:
                // 只持续一回合、而所有射手都已经动过，这个法术就白放了。
                if (GetSpellPower(_commander!) == 1)
                {
                    int activeShooters = 0;

                    foreach (BattleUnit enemy in enemies)
                    {
                        if (enemy.IsArchers && !enemy.IsImmovable && !enemy.HasModes(BattleMonsterState.Moved))
                        {
                            ++activeShooters;
                        }
                    }

                    if (activeShooters == 0)
                    {
                        return true;
                    }
                }

                return unit.HasModes(BattleMonsterState.SpellShield);

            case SpellType.Slow:
            case SpellType.MassSlow:
                return unit.HasModes(BattleMonsterState.SpellSlow) || unit.GetSpeed() == Speed.Crawling;

            case SpellType.StoneSkin:
            case SpellType.SteelSkin:
                return unit.HasModes(BattleMonsterState.SpellStoneSkin | BattleMonsterState.SpellSteelSkin);

            case SpellType.Blind:
            case SpellType.Paralyze:
            case SpellType.Petrify:
                return unit.HasModes(
                    BattleMonsterState.SpellBlind | BattleMonsterState.SpellParalyze | BattleMonsterState.SpellStone);

            case SpellType.DragonSlayer:
                return unit.HasModes(BattleMonsterState.SpellDragonSlayer);

            case SpellType.AntiMagic:
                return unit.HasModes(BattleMonsterState.SpellAntiMagic);

            case SpellType.Berserker:
                return unit.HasModes(BattleMonsterState.SpellBerserker);

            case SpellType.Hypnotize:
                return unit.HasModes(BattleMonsterState.SpellHypnotize);

            case SpellType.MirrorImage:
                return unit.HasModes(BattleMonsterState.CapMirrorOwner);

            case SpellType.DisruptingRay:
                return unit.GetDefense() <= 1;

            default:
                break;
        }

        return false;
    }

    /// <summary>
    /// The best melee outcome over all enemies, <c>BattlePlanner::getMeleeBestOutcome()</c>
    /// (<c>ai_battle.h:135</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit that would attack.</param>
    /// <param name="enemies">The units it could attack.</param>
    /// <param name="bestTarget">Receives the cell and the unit of the best attack.</param>
    /// <returns>How much the best attack is worth.</returns>
    private static double GetMeleeBestOutcome(BattleArena arena, BattleUnit currentUnit, BattleUnits enemies, BattleTargetPair bestTarget)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(bestTarget);

        SortedDictionary<BattlePosition, double> valuesOfAttackPositions =
            BattleTactics.EvaluatePotentialAttackPositions(arena, currentUnit);

        MeleeAttackOutcome bestOutcome = MeleeAttackOutcome.Create();

        double allEnemiesThreat = 0;

        foreach (BattleUnit unit in enemies)
        {
            allEnemiesThreat += unit.EvaluateThreatForUnit(currentUnit);
        }

        foreach (BattleUnit enemy in enemies)
        {
            MeleeAttackOutcome outcome = BattleTactics.BestAttackOutcome(
                arena, currentUnit, enemy, valuesOfAttackPositions, allEnemiesThreat);

            // 只能在「本回合就能打」的候选里挑；走过去下回合再打的不算。
            if (!outcome.CanAttackImmediately)
            {
                continue;
            }

            if (BattleTactics.IsOutcomeImproved(outcome, bestOutcome))
            {
                bestOutcome = outcome;

                bestTarget.Cell = outcome.FromIndex;
                bestTarget.Unit = enemy;
            }
        }

        return bestOutcome.AttackValue;
    }
}

/// <summary>
/// The helpers of <c>ai_battle.cpp</c> that are not members of <see cref="BattlePlanner"/>.
/// </summary>
public static class AiBattle
{
    /// <summary>
    /// Where a unit under the Berserker spell goes, <c>AI::getBerserkerTarget()</c> (<c>ai_battle.h:83-85</c>).
    /// </summary>
    /// <remarks>
    /// The unit does not have to be under the spell yet, so the same function serves both to play a berserk unit's
    /// turn and to predict what casting the spell would achieve.
    /// </remarks>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="unit">The unit to evaluate.</param>
    /// <returns>What the unit would go for.</returns>
    public static BerserkerTarget GetBerserkerTarget(BattleArena arena, BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(unit);

        List<BattleUnit> nearestUnits = BattleBoard.GetNearestTroops(arena, unit, []);
        Debug.Assert(nearestUnits.Count > 0, "There is always someone else left on the battlefield.");

        // 被狂暴的单位没有盟友，任何相邻单位都会逼它近战。这里不能用 IsHandFighting()：
        // 单位可能还没有中狂暴，那时它会忽略自己颜色的相邻单位。
        int[] aroundIndexes = unit.IsWide
            ? BattleBoard.GetAroundIndexes(unit.HeadIndex, unit.TailIndex)
            : BattleBoard.GetAroundIndexes(unit.HeadIndex);

        bool isNextToAnyUnit = false;

        foreach (int index in aroundIndexes)
        {
            if (arena.GetTroopBoard(index) != null)
            {
                isNextToAnyUnit = true;

                break;
            }
        }

        // 狂暴的射手身边没人，就射最近的那个。
        if (unit.IsArchers && !isNextToAnyUnit)
        {
            Debug.Assert(nearestUnits[0] != null, "The nearest unit exists.");

            return new BerserkerTarget { Unit = nearestUnits[0], Cell = -1, CanAttackThisTurn = true };
        }

        // 先找本回合就能打到的。
        foreach (BattleUnit nearbyUnit in nearestUnits)
        {
            CellDistanceInfo nearestCellInfo = BattleTactics.FindNearestCellNextToUnit(unit, nearbyUnit);

            if (nearestCellInfo.Index == -1)
            {
                continue;
            }

            if (!BattleBoard.CanAttackTargetFromPosition(arena, unit, nearbyUnit, nearestCellInfo.Index))
            {
                continue;
            }

            return new BerserkerTarget { Unit = nearbyUnit, Cell = nearestCellInfo.Index, CanAttackThisTurn = true };
        }

        // 本回合打不到：挑最近的，下回合再说。
        foreach (BattleUnit nearbyUnit in nearestUnits)
        {
            CellDistanceInfo nearestCellInfo = BattleTactics.FindNearestCellNextToUnit(unit, nearbyUnit);

            if (nearestCellInfo.Index == -1)
            {
                continue;
            }

            return new BerserkerTarget { Unit = nearbyUnit, Cell = nearestCellInfo.Index, CanAttackThisTurn = false };
        }

        return new BerserkerTarget();
    }
}
