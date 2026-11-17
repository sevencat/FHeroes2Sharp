using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The dialog contracts of ADR-163: the value of every <see cref="DialogButton"/> flag and the mapping from a button
/// index to the answer each dialog of <c>dialog.h</c> produces.
/// </summary>
public sealed class DialogModelsTests
{
    [Fact]
    public void TheDialogFlagsMatchTheReference()
    {
        Assert.Equal(0x0000, (int)DialogButton.None);
        Assert.Equal(0x0001, (int)DialogButton.Yes);
        Assert.Equal(0x0002, (int)DialogButton.Ok);
        Assert.Equal(0x0004, (int)DialogButton.No);
        Assert.Equal(0x0008, (int)DialogButton.Cancel);
        Assert.Equal(0x0010, (int)DialogButton.Dismiss);
        Assert.Equal(0x0020, (int)DialogButton.Upgrade);
        Assert.Equal(0x0040, (int)DialogButton.Max);
        Assert.Equal(0x0080, (int)DialogButton.Prev);
        Assert.Equal(0x0100, (int)DialogButton.Next);
        Assert.Equal(0x0200, (int)DialogButton.World);
        Assert.Equal(0x0400, (int)DialogButton.Puzzle);
        Assert.Equal(0x0800, (int)DialogButton.Info);
        Assert.Equal(0x1000, (int)DialogButton.Dig);
    }

    [Fact]
    public void AdventureOptions_OffersDigOnlyWhenEnabled()
    {
        AdventureOptionsDialog enabled = AdventureOptionsDialog.Create(true);
        Assert.True(enabled.Buttons.HasFlag(DialogButton.Dig));
        Assert.True(enabled.Buttons.HasFlag(DialogButton.Cancel));

        AdventureOptionsDialog disabled = AdventureOptionsDialog.Create(false);
        Assert.False(disabled.Buttons.HasFlag(DialogButton.Dig));
        Assert.True(disabled.Buttons.HasFlag(DialogButton.World));
    }

    [Theory]
    [InlineData(0, DialogButton.World)]
    [InlineData(1, DialogButton.Puzzle)]
    [InlineData(2, DialogButton.Info)]
    [InlineData(3, DialogButton.Dig)]
    [InlineData(4, DialogButton.Cancel)]
    [InlineData(9, DialogButton.Cancel)]
    public void AdventureOptions_MapTheButtonToTheAnswer(int index, DialogButton expected)
    {
        Assert.Equal(expected, AdventureOptionsDialog.FromButton(index));
    }

    [Theory]
    [InlineData(0, FileOptionsResult.NewGame)]
    [InlineData(1, FileOptionsResult.LoadGame)]
    [InlineData(3, FileOptionsResult.SaveGame)]
    [InlineData(4, FileOptionsResult.QuickSave)]
    [InlineData(5, FileOptionsResult.Quit)]
    [InlineData(6, FileOptionsResult.Cancel)]
    public void FileOptions_MapTheButtonToTheAnswer(int index, FileOptionsResult expected)
    {
        Assert.Equal(expected, FileOptionsDialog.FromButton(index));
    }

    [Fact]
    public void ArmyInfo_HasButtonsOnlyForTheFourButtonFlags()
    {
        Troop troop = new(MonsterType.Archer, 1);

        Assert.False(ArmyInfoDialog.Create(troop).HasButtons);
        Assert.False(ArmyInfoDialog.Create(troop, DialogButton.Upgrade | DialogButton.Dismiss).HasButtons);
        Assert.True(ArmyInfoDialog.Create(troop, DialogButton.Ok | DialogButton.Upgrade | DialogButton.Dismiss).HasButtons);
        Assert.Equal(DialogButton.Upgrade, ArmyInfoDialog.FromButton(0));
        Assert.Equal(DialogButton.Dismiss, ArmyInfoDialog.FromButton(1));
        Assert.Equal(DialogButton.Cancel, ArmyInfoDialog.FromButton(2));
    }

    [Fact]
    public void BuyBoat_AnswersOkOnlyForTheFirstButton()
    {
        Assert.True(BuyBoatDialog.Create(true).IsPurchaseEnabled);

        Assert.Equal(DialogButton.Ok, BuyBoatDialog.FromButton(0));
        Assert.Equal(DialogButton.Cancel, BuyBoatDialog.FromButton(1));
    }

    [Fact]
    public void SelectCount_ClampsTheAnswer()
    {
        SelectCountDialog dialog = SelectCountDialog.Create("Recruit", 1, 10, 1);

        dialog.Choose(99);
        Assert.Equal(10, dialog.Result);

        dialog.Choose(-5);
        Assert.Equal(1, dialog.Result);

        dialog.Choose(null);
        Assert.Null(dialog.Result);
    }

    [Fact]
    public void SelectCount_RejectsAnEmptyRangeAndANonPositiveStep()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SelectCountDialog.Create("Recruit", 10, 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SelectCountDialog.Create("Recruit", 1, 10, 0));
    }

    [Fact]
    public void InputString_TurnsANegativeLimitIntoNoLimit()
    {
        Assert.Equal(0, InputStringDialog.Create("Name", "Body", -4).CharacterLimit);
        Assert.Equal(12, InputStringDialog.Create("Name", "Body", 12).CharacterLimit);
    }

    [Fact]
    public void ArmySplit_RejectsNegativeInputAndMapsTheAnswer()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ArmySplitDialog.Create(-1, 10, "Archer"));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ArmySplitDialog.Create(1, -1, "Archer"));

        ArmySplitResult ok = ArmySplitDialog.FromButton(0, 5, false);
        Assert.Equal(DialogButton.Ok, ok.Button);
        Assert.Equal(5, ok.RedistributeCount);
        Assert.False(ok.UseFastSplit);

        Assert.Equal(DialogButton.Cancel, ArmySplitDialog.FromButton(1, 5, true).Button);
    }

    [Fact]
    public void Recruit_KeepsOnlyARealStack()
    {
        RecruitDialog dialog = RecruitDialog.Create(MonsterType.Peasant, 20, false);

        dialog.Choose(new Troop(MonsterType.Peasant, 0));
        Assert.Null(dialog.Result);

        Troop hired = new(MonsterType.Peasant, 7);
        dialog.Choose(hired);
        Assert.Same(hired, dialog.Result);
    }

    [Fact]
    public void ArmyJoin_FreeAndPaid()
    {
        Troop troop = new(MonsterType.Archer, 3);

        ArmyJoinDialog free = ArmyJoinDialog.CreateFree(troop);
        Assert.Equal(0u, free.Gold);
        Assert.False(free.IsPaid);
        Assert.Equal(3u, free.JoinCount);

        ArmyJoinDialog paid = ArmyJoinDialog.CreateWithCost(troop, 2, 100);
        Assert.True(paid.IsPaid);
        Assert.Equal(DialogButton.Yes, ArmyJoinDialog.FromButton(0));
        Assert.Equal(DialogButton.No, ArmyJoinDialog.FromButton(1));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ArmyJoinDialog.CreateWithCost(troop, 0, 100));
    }

    [Fact]
    public void LevelUp_RejectsASkillThatWasNotOffered()
    {
        LevelUpDialog single = LevelUpDialog.Create("Maximus", PrimarySkill.Attack, SecondarySkill.Pathfinding, SecondarySkill.Unknown);
        Assert.True(single.IsSingleChoice);

        single.Choose(SecondarySkill.Pathfinding);
        Assert.Equal(SecondarySkill.Pathfinding, single.Result);

        single.Choose(SecondarySkill.Logistics);
        Assert.Null(single.Result);
    }

    [Fact]
    public void GiftResources_KeepsOnlyAResourceIndex()
    {
        GiftResourcesDialog dialog = GiftResourcesDialog.Create(null!);

        dialog.Choose(3);
        Assert.Equal(3, dialog.Result);

        dialog.Choose(7);
        Assert.Null(dialog.Result);

        dialog.Choose(-1);
        Assert.Null(dialog.Result);
    }

    [Fact]
    public void QuickInfo_KeepsTheTargetAndTheAnchor()
    {
        QuickInfoDialog castle = QuickInfoDialog.CreateForCastle(null!, new Rect(1, 2, 3, 4), new Point(5, 6));
        Assert.Equal(QuickInfoTargetKind.Castle, castle.TargetKind);
        Assert.Equal(new Rect(1, 2, 3, 4), castle.IndicationArea);
        Assert.Equal(new Point(5, 6), castle.Position);

        QuickInfoDialog hero = QuickInfoDialog.CreateForHero(null!, true);
        Assert.Equal(QuickInfoTargetKind.Hero, hero.TargetKind);
        Assert.True(hero.IgnoreCustomBottomInfo);
        Assert.Null(hero.IndicationArea);
    }
}
