using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The recruit window, <c>Dialog::RecruitMonster()</c> (<c>dialog_recruit.cpp:242-665</c>).
/// </summary>
/// <remarks>
/// Peasants cost 20 gold and archers 150 (the knight table of <c>MonsterStatistics</c>), so a treasury of 100 gold takes
/// five peasants, and the two units have different maxima - which is what the switch tests check.
/// </remarks>
public sealed class RecruitDialogTests
{
    private const uint PeasantCost = 20;

    [Fact]
    public void OpensAtTheMaximumItCanAfford()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        Assert.Equal(5u, dialog.Maximum);
        Assert.Equal(5u, dialog.Count);
        Assert.True(dialog.IsOkayEnabled);

        // The reference lands on MAX: it is disabled because there is nothing above, MIN is the way back down.
        Assert.Equal(RecruitDialog.MaxTitle, dialog.MaxMinLabel);
        Assert.False(dialog.IsMaxEnabled);
        Assert.True(dialog.IsMinEnabled);
    }

    [Fact]
    public void TheStockCapsTheMaximum()
    {
        RecruitDialog dialog = CreateDialog(gold: 1000, available: 3);

        Assert.Equal(3u, dialog.Maximum);
        Assert.Equal(3u, dialog.Available);
    }

    [Fact]
    public void NothingAffordable_DisablesEverything()
    {
        RecruitDialog dialog = CreateDialog(gold: 19, available: 10);

        Assert.Equal(0u, dialog.Maximum);
        Assert.Equal(0u, dialog.Count);
        Assert.False(dialog.IsOkayEnabled);
        Assert.False(dialog.IsMaxEnabled);
        Assert.False(dialog.IsMinEnabled);
        Assert.Equal(string.Empty, dialog.MaxMinLabel);
    }

    [Fact]
    public void PaymentIsTheUnitCostTimesTheCount()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        Assert.Equal(PeasantCost, (uint)dialog.PaymentPerTroop.Gold);
        Assert.Equal(100, dialog.Payment.Gold);
        Assert.Equal(0, dialog.Remaining.Gold);

        dialog.SetCount(2);

        Assert.Equal(40, dialog.Payment.Gold);
        Assert.Equal(60, dialog.Remaining.Gold);
    }

    [Fact]
    public void IncreaseAndDecreaseStopAtTheEnds()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        Assert.False(dialog.Increase());
        Assert.Equal(5u, dialog.Count);

        Assert.True(dialog.Decrease());
        Assert.Equal(4u, dialog.Count);

        dialog.SetCount(1);

        Assert.True(dialog.Decrease());
        Assert.Equal(0u, dialog.Count);
        Assert.False(dialog.Decrease());
        Assert.False(dialog.IsOkayEnabled);
    }

    [Fact]
    public void TheMiddleOfTheRangeKeepsTheButtonsWhereTheLastExtremeLeftThem()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        dialog.Decrease();

        // Four of five: no label, and MAX stays disabled because nothing has re-enabled it since the window opened.
        Assert.Equal(string.Empty, dialog.MaxMinLabel);
        Assert.False(dialog.IsMaxEnabled);
        Assert.True(dialog.IsMinEnabled);
        Assert.False(dialog.SetToMaximum());
        Assert.Equal(4u, dialog.Count);

        dialog.SetCount(1);

        // One shows MIN and hands the other button over.
        Assert.Equal(RecruitDialog.MinTitle, dialog.MaxMinLabel);
        Assert.False(dialog.IsMinEnabled);
        Assert.True(dialog.IsMaxEnabled);
        Assert.True(dialog.SetToMaximum());
        Assert.Equal(5u, dialog.Count);
        Assert.False(dialog.SetToMaximum());
    }

    [Fact]
    public void SetToMinimumJumpsToOne()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        Assert.True(dialog.SetToMinimum());
        Assert.Equal(1u, dialog.Count);
        Assert.Equal(RecruitDialog.MinTitle, dialog.MaxMinLabel);
        Assert.False(dialog.SetToMinimum());
    }

    [Fact]
    public void SetCountClampsToTheMaximum()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        dialog.SetCount(99);

        Assert.Equal(5u, dialog.Count);
    }

    [Fact]
    public void TypedDigitsAccumulateAndStopAtTheMaximum()
    {
        RecruitDialog dialog = CreateDialog(gold: 1000, available: 20);

        Assert.Equal(20u, dialog.Maximum);

        Assert.True(dialog.TypeDigit('1'));
        Assert.Equal(1u, dialog.Count);

        Assert.True(dialog.TypeDigit('2'));
        Assert.Equal(12u, dialog.Count);

        // "123" is above the maximum, so the third digit is dropped and the count stays twelve.
        Assert.False(dialog.TypeDigit('3'));
        Assert.Equal(12u, dialog.Count);
    }

    [Fact]
    public void ARejectedDigitDoesNotBecomeTheStartOfANewNumber()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 5);

        Assert.True(dialog.TypeDigit('5'));
        Assert.False(dialog.TypeDigit('0'));

        // Typing "5" then "0" must not read as "0".
        Assert.Equal(5u, dialog.Count);
    }

    [Fact]
    public void ClearTypedEmptiesTheCount()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        dialog.ClearTyped();

        // clamp( 0, 0, max ) of the reference.
        Assert.Equal(0u, dialog.Count);
        Assert.False(dialog.IsOkayEnabled);
    }

    [Fact]
    public void TypeDigitRejectsAnythingButADecimalDigit()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TypeDigit('a'));
        Assert.Throws<ArgumentOutOfRangeException>(() => dialog.TypeDigit('-'));
    }

    [Fact]
    public void TheUpgradeChainIsWalkedBothWays()
    {
        RecruitDialog dialog = RecruitDialog.Create(MonsterType.Ranger, 10, CreateKingdom(10_000));

        Assert.True(dialog.CanSwitchMonster);
        Assert.Equal(MonsterType.Ranger, dialog.Monster);

        dialog.SwitchToDowngrade();
        Assert.Equal(MonsterType.Archer, dialog.Monster);

        // Two arrows and two units: the next step down wraps back to the ranger.
        dialog.SwitchToDowngrade();
        Assert.Equal(MonsterType.Ranger, dialog.Monster);

        dialog.SwitchToUpgrade();
        Assert.Equal(MonsterType.Archer, dialog.Monster);
    }

    [Fact]
    public void SwitchingRecomputesTheMaximumAndJumpsToIt()
    {
        RecruitDialog dialog = RecruitDialog.Create(MonsterType.Ranger, 10, CreateKingdom(300));

        // Three hundred gold buys one ranger at 250 and two archers at 150, and ten are in stock.
        Assert.Equal(1u, dialog.Maximum);

        dialog.SwitchToDowngrade();

        Assert.Equal(2u, dialog.Maximum);
        Assert.Equal(2u, dialog.Count);
        Assert.Equal(RecruitDialog.MaxTitle, dialog.MaxMinLabel);
        Assert.Equal(300, dialog.Payment.Gold);
    }

    [Fact]
    public void AUnitWithoutADowngradeShowsNoArrows()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        Assert.False(dialog.CanSwitchMonster);
    }

    [Fact]
    public void TheCallerCanTurnTheArrowsOff()
    {
        Assert.False(RecruitDialog.Create(MonsterType.Archer, 10, CreateKingdom(1000), allowDowngradedMonster: false)
            .CanSwitchMonster);
    }

    [Fact]
    public void TitleAndStockLineReadLikeTheReference()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 7);

        Assert.Equal($"Recruit {MonsterNames.GetMulti(MonsterType.Peasant)}", dialog.Title);
        Assert.Equal("Available: 7", dialog.AvailableText);
        Assert.Equal("Cost per troop:", RecruitDialog.CostPerTroopTitle);
        Assert.Equal("Number to buy:", RecruitDialog.NumberToBuyTitle);
    }

    [Fact]
    public void SelectedIsTheTroopTheReferenceReturns()
    {
        RecruitDialog dialog = CreateDialog(gold: 100, available: 10);

        dialog.SetCount(3);

        Troop troop = dialog.Selected;

        Assert.Equal(MonsterType.Peasant, troop.Monster);
        Assert.Equal(3u, troop.Count);
    }

    [Fact]
    public void TheWindowNeverSpendsTheMoney()
    {
        GameKingdom kingdom = CreateKingdom(100);

        RecruitDialog dialog = RecruitDialog.Create(MonsterType.Peasant, 10, kingdom);

        _ = dialog.Selected;

        // The caller pays; the treasury is only read.
        Assert.Equal(100, kingdom.Treasury.Gold);
    }

    [Fact]
    public void WithoutATreasuryTheStockIsTheMaximum()
    {
        RecruitDialog dialog = RecruitDialog.Create(MonsterType.Peasant, 4, allowDowngradedMonster: false);

        Assert.Equal(4u, dialog.Maximum);
        Assert.Equal(0, dialog.Funds.Gold);
    }

    [Fact]
    public void ImpossibleArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => RecruitDialog.Create(MonsterType.Peasant, 10, (GameKingdom)null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RecruitDialog.Create(MonsterType.Unknown, 10, CreateKingdom(100)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RecruitDialog.Create(MonsterType.Unknown, 10, allowDowngradedMonster: false));
    }

    private static RecruitDialog CreateDialog(int gold, uint available) =>
        RecruitDialog.Create(MonsterType.Peasant, available, CreateKingdom(gold));

    private static GameKingdom CreateKingdom(int gold = 0)
    {
        var kingdom = new GameKingdom();
        kingdom.Treasury.Gold = gold;

        return kingdom;
    }
}
