using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::Result</c> (<c>battle.h:43-66</c>) and the precedence of <c>getBattleResult()</c>
/// (<c>battle_main.cpp:163-179</c>).
/// </summary>
public class BattleResultTests
{
    [Fact]
    public void TheDefaultResultDecidesNothing()
    {
        var result = new BattleResult();

        Assert.False(result.IsAttackerWin);
        Assert.False(result.IsDefenderWin);
        Assert.Equal(BattleOutcome.None, result.GetAttackerOutcome());
        Assert.Equal(BattleOutcome.None, result.GetDefenderOutcome());
    }

    [Fact]
    public void TheWinBitIsTheOnlyWayToWin()
    {
        var result = new BattleResult { Attacker = BattleOutcome.Wins, Defender = BattleOutcome.Loss };

        Assert.True(result.IsAttackerWin);
        Assert.False(result.IsDefenderWin);
        Assert.Equal(BattleOutcome.Wins, result.GetAttackerOutcome());
        Assert.Equal(BattleOutcome.Loss, result.GetDefenderOutcome());
    }

    [Fact]
    public void SurrenderBeatsRetreatAndLossAndWinning()
    {
        Assert.Equal(
            BattleOutcome.Surrender,
            BattleResult.GetOutcome(BattleOutcome.Surrender | BattleOutcome.Retreat | BattleOutcome.Loss));

        Assert.Equal(
            BattleOutcome.Surrender,
            BattleResult.GetOutcome(BattleOutcome.Surrender | BattleOutcome.Wins));
    }

    [Fact]
    public void RetreatBeatsLossAndWinning()
    {
        Assert.Equal(
            BattleOutcome.Retreat,
            BattleResult.GetOutcome(BattleOutcome.Retreat | BattleOutcome.Loss));

        Assert.Equal(
            BattleOutcome.Retreat,
            BattleResult.GetOutcome(BattleOutcome.Retreat | BattleOutcome.Wins));
    }

    [Fact]
    public void AnyLossReasonBeatsTheWinBit()
    {
        Assert.Equal(BattleOutcome.Loss, BattleResult.GetOutcome(BattleOutcome.Loss | BattleOutcome.Wins));
        Assert.Equal(BattleOutcome.Wins, BattleResult.GetOutcome(BattleOutcome.Wins));
    }

    [Fact]
    public void TheNumbersAreCarriedThrough()
    {
        var result = new BattleResult
        {
            Attacker = BattleOutcome.Wins,
            Defender = BattleOutcome.Loss,
            AttackerExperience = 1200,
            DefenderExperience = 0,
            NumOfDeadUnitsForNecromancy = 43,
        };

        Assert.Equal(1200u, result.AttackerExperience);
        Assert.Equal(0u, result.DefenderExperience);
        Assert.Equal(43u, result.NumOfDeadUnitsForNecromancy);
    }

    [Fact]
    public void TheOutcomeValuesAreTheReferences()
    {
        Assert.Equal(0x01u, (uint)BattleOutcome.Loss);
        Assert.Equal(0x02u, (uint)BattleOutcome.Retreat);
        Assert.Equal(0x04u, (uint)BattleOutcome.Surrender);
        Assert.Equal(0x80u, (uint)BattleOutcome.Wins);
    }
}
