using FHeroes2.Game.Ai;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the contract of the AI layer: the data structures of <c>ai_planner.h:65-158</c>, the two helpers of
/// <c>ai_battle.h:55-71</c> and the numbers of <c>ai_planner_internals.h:27-30</c>.
/// </summary>
/// <remarks>
/// The members of the layer that only describe work (<see cref="AiPlanner.KingdomTurn"/> and friends) are not
/// exercised: they throw until their translation unit is ported (ADR-161). <c>ai_common.cpp</c> is ported, so
/// its behaviour is covered by <see cref="AiCommonTests"/> instead.
/// </remarks>
public sealed class AiContractTests
{
    [Fact]
    public void PriorityTaskTypeHasTheValuesOfTheReference()
    {
        Assert.Equal(0, (int)PriorityTaskType.Attack);
        Assert.Equal(1, (int)PriorityTaskType.Defend);
        Assert.Equal(2, (int)PriorityTaskType.Reinforce);
    }

    [Fact]
    public void RegionStatsStartsUnevaluated()
    {
        RegionStats stats = new();

        Assert.False(stats.Evaluated);
        Assert.Equal(-1, stats.HighestThreat);
        Assert.Equal(2, stats.SpellLevel);
        Assert.Equal(0, stats.FriendlyHeroes);
        Assert.Equal(0, stats.FriendlyCastles);
        Assert.Equal(0, stats.EnemyCastles);
        Assert.Equal(0, stats.SafetyFactor);
    }

    [Fact]
    public void BudgetEntryKeepsItsResourceAndClearsTheRest()
    {
        BudgetEntry entry = new(Resource.Gold)
        {
            Missing = 1200,
            Priority = true,
            RecurringCost = true,
        };

        entry.Reset();

        Assert.Equal(Resource.Gold, entry.Resource);
        Assert.Equal(0, entry.Missing);
        Assert.False(entry.Priority);
        Assert.False(entry.RecurringCost);
    }

    [Fact]
    public void EnemyArmyStartsEmptyAndKeepsWhatItWasGiven()
    {
        EnemyArmy empty = new();
        Assert.Equal(-1, empty.Index);
        Assert.Null(empty.Hero);
        Assert.Equal(0.0, empty.Strength);
        Assert.Equal(0u, empty.MovePoints);

        EnemyArmy neutral = new(1234, null, 42.5, 1500);
        Assert.Equal(1234, neutral.Index);
        Assert.Null(neutral.Hero);
        Assert.Equal(42.5, neutral.Strength);
        Assert.Equal(1500u, neutral.MovePoints);
    }

    [Fact]
    public void PriorityTaskStartsAsAnAttackWithoutTargets()
    {
        PriorityTask task = new();

        Assert.Equal(PriorityTaskType.Attack, task.Type);
        Assert.Empty(task.SecondaryTaskTileIndexes);
    }

    [Fact]
    public void PriorityTaskCollectsItsSecondaryTiles()
    {
        PriorityTask task = new(PriorityTaskType.Reinforce, 77);

        Assert.Equal(PriorityTaskType.Reinforce, task.Type);
        Assert.Contains(77, task.SecondaryTaskTileIndexes);

        task.SecondaryTaskTileIndexes.Add(78);
        Assert.Equal(2, task.SecondaryTaskTileIndexes.Count);
    }

    [Fact]
    public void AiCastleRejectsAMissingCastle()
    {
        Assert.Throws<ArgumentNullException>(() => new AiCastle(null!, false, 0, 0));
    }

    [Fact]
    public void UpdateOutcomeKeepsTheSingleBestTarget()
    {
        SpellcastOutcome outcome = new();

        outcome.UpdateOutcome(10.0, 11);
        outcome.UpdateOutcome(5.0, 12);

        Assert.Equal(10.0, outcome.Value);
        Assert.Equal(11, outcome.Cell);
    }

    [Fact]
    public void UpdateOutcomeSumsMassEffects()
    {
        SpellcastOutcome outcome = new();

        outcome.UpdateOutcome(10.0, 11, true);
        outcome.UpdateOutcome(5.0, 12, true);

        Assert.Equal(15.0, outcome.Value);

        // A mass effect never moves the cell: the reference sums the value and leaves the target alone.
        Assert.Equal(-1, outcome.Cell);
    }

    [Fact]
    public void UpdateOutcomeDoesNotTouchTheDestinationCell()
    {
        SpellcastOutcome outcome = new() { DestinationCell = 99 };

        outcome.UpdateOutcome(100.0, 11);

        Assert.Equal(99, outcome.DestinationCell);
    }

    [Fact]
    public void ThePlannersAreSingletons()
    {
        Assert.Same(AiPlanner.Instance, AiPlanner.Instance);
        Assert.Same(BattlePlanner.Instance, BattlePlanner.Instance);
    }

    [Fact]
    public void TheArmyAdvantageThresholdsAreTheNumbersOfTheReference()
    {
        Assert.Equal(0.8, AiPlannerInternals.ArmyAdvantageDesperate);
        Assert.Equal(1.3, AiPlannerInternals.ArmyAdvantageSmall);
        Assert.Equal(1.5, AiPlannerInternals.ArmyAdvantageMedium);
        Assert.Equal(1.8, AiPlannerInternals.ArmyAdvantageLarge);
    }

    [Fact]
    public void BuildIfEnoughFundsRejectsTheMultiplierOutsideTheReferenceRange()
    {
        // The guard runs before anything touches the world, so a null castle is enough to prove it fired.
        Assert.False(AiCommon.BuildIfEnoughFunds(null!, default, 0));
        Assert.False(AiCommon.BuildIfEnoughFunds(null!, default, 100));
    }

    [Fact]
    public void ABattleStartsWithAFreshTurnLimit()
    {
        // The one member of the battle planner that is already ported: it clears the counters that
        // isLimitOfTurnsExceeded() will read, and 50 is the number of the reference (ai_battle.h:139).
        BattlePlanner planner = BattlePlanner.Instance;

        planner.BattleBegins();

        Assert.Equal((0u, 50u, 0u, 0u), planner.TurnLimitCounters);
    }
}
