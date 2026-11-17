using FHeroes2.Game.Battle;

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
    /// Resets the per-battle state, <c>BattlePlanner::battleBegins()</c> (<c>ai_battle.h:93</c>).
    /// </summary>
    /// <remarks>
    /// The reference clears the turn counter and the "turns without deaths" budget that decides when an attacking AI
    /// has to give up.
    /// </remarks>
    public void BattleBegins() =>
        throw new NotImplementedException("BattlePlanner.BattleBegins — ai_battle.cpp 未移植（ADR-161）");

    /// <summary>
    /// Plans the turn of one unit, <c>BattlePlanner::BattleTurn()</c> (<c>ai_battle.h:95</c>).
    /// </summary>
    /// <param name="arena">The arena the battle happens in.</param>
    /// <param name="currentUnit">The unit whose turn it is.</param>
    /// <param name="actions">Receives the commands the unit should perform, in order.</param>
    public void BattleTurn(BattleArena arena, BattleUnit currentUnit, List<BattleCommand> actions) =>
        throw new NotImplementedException("BattlePlanner.BattleTurn — ai_battle.cpp 未移植（ADR-161）");
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
