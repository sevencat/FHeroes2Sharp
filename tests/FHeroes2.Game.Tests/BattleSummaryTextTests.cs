using FHeroes2.Assets.Icn;
using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The strings and the backdrop animations of the battle result window, <c>Battle::GetSummaryParams()</c>
/// (<c>battle_dialogs.cpp:416-471</c>).
/// </summary>
public class BattleSummaryTextTests
{
    [Fact]
    public void AVictoryOverAWipedOutEnemyIsAGloriousVictory()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Wins, BattleOutcome.Loss, "Ariel", 1200, 0);

        Assert.Equal("A glorious victory!", text.Title);
        Assert.Equal(string.Empty, text.SurrenderText);
        Assert.Equal("For valor in combat, Ariel receives 1200 experience.", text.OutcomeText);
        Assert.Equal([new BattleSummaryAnimationStep(IcnType.Wincmbt, true)], text.Animation);
    }

    [Fact]
    public void TheEnemyRunningAwayIsNoRansom()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Wins, BattleOutcome.Retreat, "Ariel", 800, 0);

        Assert.Equal("The enemy has fled!", text.Title);
        Assert.Equal(string.Empty, text.SurrenderText);
    }

    [Fact]
    public void ASurrenderCostsTheEnemyGoldAndSaysSo()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Wins,
            BattleOutcome.Loss | BattleOutcome.Surrender, "Ariel", 300, 2500);

        Assert.Equal("The enemy has surrendered!", text.Title);
        Assert.Equal("Their cowardice costs them 2500 gold.", text.SurrenderText);
        Assert.Equal("For valor in combat, Ariel receives 300 experience.", text.OutcomeText);
    }

    [Fact]
    public void ACaptainWinsWithoutAnExperienceLine()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Wins, BattleOutcome.Loss, null, 900, 0);

        Assert.Equal("A glorious victory!", text.Title);
        Assert.Equal(string.Empty, text.OutcomeText);
    }

    [Fact]
    public void FleeingPlaysTheThreeChunksOnce()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Retreat, BattleOutcome.Wins, "Ariel", 0, 0);

        Assert.Equal("The cowardly Ariel flees from battle.", text.Title);
        Assert.Equal(
            [
                new BattleSummaryAnimationStep(IcnType.Cmbtfle1, false),
                new BattleSummaryAnimationStep(IcnType.Cmbtfle2, false),
                new BattleSummaryAnimationStep(IcnType.Cmbtfle3, false),
            ],
            text.Animation);
    }

    [Fact]
    public void SurrenderingLoopsTheShame()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Surrender, BattleOutcome.Wins, "Ariel", 0, 0);

        Assert.Equal("Ariel surrenders to the enemy, and departs in shame.", text.Title);
        Assert.Equal([new BattleSummaryAnimationStep(IcnType.Cmbtsurr, true)], text.Animation);
    }

    [Fact]
    public void ALossPlaysTwoChunksThenLoopsTheThird()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Loss, BattleOutcome.Wins, "Ariel", 0, 0);

        Assert.Equal("Your forces suffer a bitter defeat, and Ariel abandons your cause.", text.Title);
        Assert.Equal(
            [
                new BattleSummaryAnimationStep(IcnType.Cmbtlos1, false),
                new BattleSummaryAnimationStep(IcnType.Cmbtlos2, false),
                new BattleSummaryAnimationStep(IcnType.Cmbtlos3, true),
            ],
            text.Animation);
    }

    [Fact]
    public void ALostCaptainHasNoNameToDrop()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Loss, BattleOutcome.Wins, null, 0, 0);

        Assert.Equal("Your forces suffer a bitter defeat.", text.Title);
    }

    [Fact]
    public void LosingNeverEarnsExperience()
    {
        BattleSummaryText text = BattleSummaryText.Create(BattleOutcome.Loss, BattleOutcome.Wins, "Ariel", 500, 0);

        Assert.Equal(string.Empty, text.OutcomeText);
        Assert.Equal(string.Empty, text.SurrenderText);
    }
}
