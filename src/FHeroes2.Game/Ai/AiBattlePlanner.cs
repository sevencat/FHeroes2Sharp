using FHeroes2.Assets.Maps;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Spells;

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
    public void BattleTurn(BattleArena arena, BattleUnit currentUnit, List<BattleCommand> actions) =>
        throw new NotImplementedException("BattlePlanner.BattleTurn — ai_battle.cpp 未移植（ADR-161）");

    // -------------------------------------------------------------------------------------------------------------
    // 私有半边（ai_battle.h:97-165）：签名照抄参考，正文留到移植 ai_battle.cpp / ai_battle_spell.cpp 时再写（ADR-180）。
    // 一次战斗内有效的分析数据（_commander、_myArmyStrength 等）要等 analyzeBattleState 落地，届时一起加。
    // -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds the command that ends an auto battle which dragged on, <c>BattlePlanner::isLimitOfTurnsExceeded()</c>
    /// (<c>ai_battle.h:102</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="actions">Receives the command, when the limit is exceeded.</param>
    /// <returns>Whether the battle has to stop.</returns>
    private bool IsLimitOfTurnsExceeded(BattleArena arena, List<BattleCommand> actions) =>
        throw new NotImplementedException("BattlePlanner.IsLimitOfTurnsExceeded — ai_battle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Decides the whole turn of one unit, <c>BattlePlanner::planUnitTurn()</c> (<c>ai_battle.h:104</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <returns>The commands the unit should perform, in order.</returns>
    private List<BattleCommand> PlanUnitTurn(BattleArena arena, BattleUnit currentUnit) =>
        throw new NotImplementedException("BattlePlanner.PlanUnitTurn — ai_battle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Sizes up both armies at the start of a turn, <c>BattlePlanner::analyzeBattleState()</c>
    /// (<c>ai_battle.h:106</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    private void AnalyzeBattleState(BattleArena arena, BattleUnit currentUnit) =>
        throw new NotImplementedException("BattlePlanner.AnalyzeBattleState — ai_battle.cpp 未移植（ADR-180）");

    /// <summary>
    /// What a shooter does on its turn, <c>BattlePlanner::archerDecision()</c> (<c>ai_battle.h:108</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The shooter whose turn it is.</param>
    /// <returns>The commands the shooter should perform.</returns>
    private List<BattleCommand> ArcherDecision(BattleArena arena, BattleUnit currentUnit) =>
        throw new NotImplementedException("BattlePlanner.ArcherDecision — ai_battle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Whom a melee unit attacks when it is winning, <c>BattlePlanner::meleeUnitOffense()</c>
    /// (<c>ai_battle.h:110</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <returns>The best target, <see cref="BattleTargetPair.Cell"/> is <c>-1</c> when there is none.</returns>
    private BattleTargetPair MeleeUnitOffense(BattleArena arena, BattleUnit currentUnit) =>
        throw new NotImplementedException("BattlePlanner.MeleeUnitOffense — ai_battle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Whom a melee unit attacks when it is losing, <c>BattlePlanner::meleeUnitDefense()</c>
    /// (<c>ai_battle.h:111</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <returns>The best target, <see cref="BattleTargetPair.Cell"/> is <c>-1</c> when there is none.</returns>
    private BattleTargetPair MeleeUnitDefense(BattleArena arena, BattleUnit currentUnit) =>
        throw new NotImplementedException("BattlePlanner.MeleeUnitDefense — ai_battle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Whether a cell is defended by the unit's own side, <c>BattlePlanner::isPositionLocatedInDefendedArea()</c>
    /// (<c>ai_battle.h:113</c>).
    /// </summary>
    /// <param name="currentUnit">The unit that would move there.</param>
    /// <param name="pos">The cell to check.</param>
    /// <returns>Whether a friendly unit protects that cell.</returns>
    private bool IsPositionLocatedInDefendedArea(BattleUnit currentUnit, BattlePosition pos) =>
        throw new NotImplementedException("BattlePlanner.IsPositionLocatedInDefendedArea — ai_battle.cpp 未移植（ADR-180）");

    /// <summary>
    /// The spell worth casting this turn, <c>BattlePlanner::selectBestSpell()</c> (<c>ai_battle.h:115</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The caster.</param>
    /// <param name="retreating">Whether the caster's side is trying to get away.</param>
    /// <returns>The spell and its target, <see cref="SpellSelection.SpellId"/> is <c>-1</c> when none is worth it.</returns>
    private SpellSelection SelectBestSpell(BattleArena arena, BattleUnit currentUnit, bool retreating) =>
        throw new NotImplementedException("BattlePlanner.SelectBestSpell — ai_battle_spell.cpp 未移植（ADR-180）");

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
    private SpellcastOutcome SpellDamageValue(Spell spell, BattleArena arena, BattleUnit currentUnit, BattleUnits friendly, BattleUnits enemies, bool retreating) =>
        throw new NotImplementedException("BattlePlanner.SpellDamageValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What dispelling the enemy's spells would achieve, <c>BattlePlanner::spellDispelValue()</c>
    /// (<c>ai_battle.h:119</c>).
    /// </summary>
    /// <param name="spell">The dispel spell.</param>
    /// <param name="friendly">The caster's side.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellDispelValue(Spell spell, BattleUnits friendly, BattleUnits enemies) =>
        throw new NotImplementedException("BattlePlanner.SpellDispelValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What resurrecting the fallen would achieve, <c>BattlePlanner::spellResurrectValue()</c>
    /// (<c>ai_battle.h:120</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellResurrectValue(Spell spell, BattleArena arena) =>
        throw new NotImplementedException("BattlePlanner.SpellResurrectValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What summoning new troops would achieve, <c>BattlePlanner::spellSummonValue()</c> (<c>ai_battle.h:121</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="heroColor">The color of the caster's hero.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellSummonValue(Spell spell, BattleArena arena, PlayerColor heroColor) =>
        throw new NotImplementedException("BattlePlanner.SpellSummonValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What Dragon Slayer would achieve, <c>BattlePlanner::spellDragonSlayerValue()</c> (<c>ai_battle.h:122</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="friendly">The caster's side.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellDragonSlayerValue(Spell spell, BattleUnits friendly, BattleUnits enemies) =>
        throw new NotImplementedException("BattlePlanner.SpellDragonSlayerValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What teleporting a unit would achieve, <c>BattlePlanner::spellTeleportValue()</c> (<c>ai_battle.h:123</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="currentUnit">The caster.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellTeleportValue(BattleArena arena, Spell spell, BattleUnit currentUnit, BattleUnits enemies) =>
        throw new NotImplementedException("BattlePlanner.SpellTeleportValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What an earthquake would achieve, <c>BattlePlanner::spellEarthquakeValue()</c> (<c>ai_battle.h:124</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="friendly">The caster's side.</param>
    /// <returns>The best outcome of the cast.</returns>
    private SpellcastOutcome SpellEarthquakeValue(BattleArena arena, Spell spell, BattleUnits friendly) =>
        throw new NotImplementedException("BattlePlanner.SpellEarthquakeValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What a spell would do to the units it can reach, <c>BattlePlanner::spellEffectValue()</c>
    /// (<c>ai_battle.h:125</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="targets">The units the spell can reach.</param>
    /// <param name="enemies">The other side.</param>
    /// <returns>The best outcome over all targets.</returns>
    private SpellcastOutcome SpellEffectValue(Spell spell, BattleUnits targets, BattleUnits enemies) =>
        throw new NotImplementedException("BattlePlanner.SpellEffectValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// What a spell would do to one unit, <c>BattlePlanner::spellEffectValue()</c> (<c>ai_battle.h:127</c>).
    /// </summary>
    /// <param name="spell">The spell to evaluate.</param>
    /// <param name="target">The unit it would hit.</param>
    /// <param name="enemies">The other side.</param>
    /// <param name="targetIsLast">Whether this is the last unit of its side.</param>
    /// <param name="forDispel">Whether the value is asked for a dispel, which only counts the harmful effects.</param>
    /// <returns>How much the spell is worth against that unit.</returns>
    private double SpellEffectValue(Spell spell, BattleUnit target, BattleUnits enemies, bool targetIsLast, bool forDispel) =>
        throw new NotImplementedException("BattlePlanner.SpellEffectValue — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// How much a Disrupting Ray is worth against a unit, <c>BattlePlanner::getSpellDisruptingRayRatio()</c>
    /// (<c>ai_battle.h:128</c>).
    /// </summary>
    /// <param name="target">The unit the spell would hit.</param>
    /// <returns>The ratio the spell's value is multiplied by.</returns>
    private double GetSpellDisruptingRayRatio(BattleUnit target) =>
        throw new NotImplementedException("BattlePlanner.GetSpellDisruptingRayRatio — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// How much a Slow is worth against a unit, <c>BattlePlanner::getSpellSlowRatio()</c> (<c>ai_battle.h:129</c>).
    /// </summary>
    /// <param name="target">The unit the spell would hit.</param>
    /// <returns>The ratio the spell's value is multiplied by.</returns>
    private double GetSpellSlowRatio(BattleUnit target) =>
        throw new NotImplementedException("BattlePlanner.GetSpellSlowRatio — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// How much a Haste is worth for a unit, <c>BattlePlanner::getSpellHasteRatio()</c> (<c>ai_battle.h:130</c>).
    /// </summary>
    /// <param name="target">The unit the spell would help.</param>
    /// <returns>The ratio the spell's value is multiplied by.</returns>
    private double GetSpellHasteRatio(BattleUnit target) =>
        throw new NotImplementedException("BattlePlanner.GetSpellHasteRatio — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// How long a spell lasts on a unit, <c>BattlePlanner::spellDurationMultiplier()</c> (<c>ai_battle.h:131</c>).
    /// </summary>
    /// <param name="target">The unit the spell would hit.</param>
    /// <returns>The multiplier of the spell's duration.</returns>
    private int SpellDurationMultiplier(BattleUnit target) =>
        throw new NotImplementedException("BattlePlanner.SpellDurationMultiplier — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// Whether casting a spell on a unit is a waste, <c>BattlePlanner::isSpellcastUselessForUnit()</c>
    /// (<c>ai_battle.h:133</c>).
    /// </summary>
    /// <param name="unit">The unit the spell would hit.</param>
    /// <param name="enemies">The other side.</param>
    /// <param name="spell">The spell to evaluate.</param>
    /// <returns>Whether the spell changes nothing.</returns>
    private bool IsSpellcastUselessForUnit(BattleUnit unit, BattleUnits enemies, Spell spell) =>
        throw new NotImplementedException("BattlePlanner.IsSpellcastUselessForUnit — ai_battle_spell.cpp 未移植（ADR-180）");

    /// <summary>
    /// The best melee outcome over all enemies, <c>BattlePlanner::getMeleeBestOutcome()</c>
    /// (<c>ai_battle.h:135</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit that would attack.</param>
    /// <param name="enemies">The units it could attack.</param>
    /// <param name="bestTarget">Receives the cell and the unit of the best attack.</param>
    /// <returns>How much the best attack is worth.</returns>
    private static double GetMeleeBestOutcome(BattleArena arena, BattleUnit currentUnit, BattleUnits enemies, BattleTargetPair bestTarget) =>
        throw new NotImplementedException("BattlePlanner.GetMeleeBestOutcome — ai_battle.cpp 未移植（ADR-180）");
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
    public static BerserkerTarget GetBerserkerTarget(BattleArena arena, BattleUnit unit) =>
        throw new NotImplementedException("AiBattle.GetBerserkerTarget — ai_battle.cpp 未移植（ADR-161）");
}
