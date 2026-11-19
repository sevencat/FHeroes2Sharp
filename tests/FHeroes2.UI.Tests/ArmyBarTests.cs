using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The bar of <c>src/fheroes2/army/army_bar.cpp</c> as ported by <see cref="ArmyBar"/>: its five 43 pixel slots, the
/// mouse rules of one bar and of a pair of bars, the split questions and the painting.
/// </summary>
public sealed class ArmyBarTests
{
    [Fact]
    public void TheConstructorLaysOutFiveMiniMonsterSlots()
    {
        ArmyBar bar = CreateBar(ArmyWith((MonsterType.Peasant, 1)));

        Assert.Equal(5, bar.Layout.Count);
        Assert.Equal(new Rect(0, 0, 43, 43), bar.Layout.GetItemRect(0));
        Assert.Equal(new Rect(86, 0, 43, 43), bar.Layout.GetItemRect(2));
        Assert.Equal(new Rect(0, 0, 215, 43), bar.Layout.RenderingArea);
    }

    [Fact]
    public void APointIsMappedToTheSlotUnderIt()
    {
        ArmyBar bar = CreateBar(ArmyWith((MonsterType.Peasant, 1)));

        Assert.Equal(0, bar.GetItemIndexAt(new Point(0, 0)));
        Assert.Equal(2, bar.GetItemIndexAt(new Point(90, 10)));
        Assert.Equal(-1, bar.GetItemIndexAt(new Point(215, 10)));
    }

    [Fact]
    public void AClickSelectsAStackAndSaysSo()
    {
        ArmyBar bar = CreateBar(ArmyWith((MonsterType.Peasant, 10)));

        ArmyBarEvent result = bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));

        Assert.True(result.Handled);
        Assert.Equal(0, bar.SelectedIndex);
        Assert.Equal("Select peasant", bar.Message);
    }

    [Fact]
    public void ASecondClickOnAnotherStackOfTheSameKindMergesIt()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10), (MonsterType.Peasant, 5));
        ArmyBar bar = CreateBar(army);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));
        bar.ProcessEvents(Click(50, 5, MouseButtonType.Left));

        Assert.True(army.GetTroop(0).IsEmpty);
        Assert.Equal(15u, army.GetTroop(1).Count);
        Assert.False(bar.IsSelected);
    }

    [Fact]
    public void ASecondClickOnAnotherKindSwapsTheStacks()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10), (MonsterType.Archer, 5));
        ArmyBar bar = CreateBar(army);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));
        bar.ProcessEvents(Click(50, 5, MouseButtonType.Left));

        Assert.Equal(MonsterType.Archer, army.GetTroop(0).Monster);
        Assert.Equal(5u, army.GetTroop(0).Count);
        Assert.Equal(MonsterType.Peasant, army.GetTroop(1).Monster);
        Assert.Equal(10u, army.GetTroop(1).Count);
    }

    [Fact]
    public void ARightClickOnAStackOfAChangableBarDismissesIt()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10));
        ArmyBar bar = CreateBar(army, canChange: true);

        ArmyBarEvent result = bar.ProcessEvents(Press(5, 5, MouseButtonType.Right));

        Assert.True(result.Handled);
        Assert.Null(result.Info);
        Assert.True(army.GetTroop(0).IsEmpty);
    }

    [Fact]
    public void ARightClickOnAReadOnlyStackOpensTheInformationWindow()
    {
        ArmyBar bar = CreateBar(ArmyWith((MonsterType.Peasant, 10)), readOnly: true);

        ArmyBarEvent result = bar.ProcessEvents(Press(5, 5, MouseButtonType.Right));

        Assert.True(result.Handled);
        Assert.NotNull(result.Info);
    }

    [Fact]
    public void ASecondClickOnTheSameStackOpensTheInformationWindow()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10));
        ArmyBar bar = CreateBar(army);

        Assert.Null(bar.ProcessEvents(Click(5, 5, MouseButtonType.Left)).Info);

        ArmyBarEvent result = bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));

        Assert.NotNull(result.Info);
        Assert.False(bar.IsSelected);
        Assert.Equal(10u, army.GetTroop(0).Count);
    }

    [Fact]
    public void TheSplitKeyOnAnEmptySlotAsksHowToSplitTheStack()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10));
        ArmyBar bar = CreateBar(army);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));

        ArmyBarEvent result = bar.ProcessEvents(Click(50, 5, MouseButtonType.Left), ArmyBarHotKey.SplitStackByHalf);

        ArmyBarSplit split = Assert.IsType<ArmyBarSplit>(result.Split);
        Assert.False(split.SameTroopType);
        Assert.Equal(10u, split.OverallCount);
        Assert.Equal(5, split.Slots);
        Assert.Equal(10, split.MaxCount);
        Assert.Equal(5, split.SuggestedCount);

        Assert.True(ArmyBar.ApplySplit(split, split.Slots, useFastSplit: true));

        for (int i = 0; i < ArmyStack.MaximumTroopCount; ++i)
        {
            Assert.Equal(MonsterType.Peasant, army.GetTroop(i).Monster);
            Assert.Equal(2u, army.GetTroop(i).Count);
        }
    }

    [Fact]
    public void ARightClickOnAStackOfTheSameKindAsksHowMuchToRedistribute()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10), (MonsterType.Peasant, 4));
        ArmyBar bar = CreateBar(army);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));

        ArmyBarEvent result = bar.ProcessEvents(Click(50, 5, MouseButtonType.Right));

        ArmyBarSplit split = Assert.IsType<ArmyBarSplit>(result.Split);
        Assert.True(split.SameTroopType);
        Assert.Equal(14u, split.OverallCount);
        Assert.Equal(1, split.SuggestedCount);
        Assert.False(split.UseFastSplit);

        Assert.True(ArmyBar.ApplySplit(split, 2, useFastSplit: false));

        Assert.Equal(9u, army.GetTroop(0).Count);
        Assert.Equal(5u, army.GetTroop(1).Count);
        Assert.False(bar.IsSelected);
    }

    [Fact]
    public void ARightClickOnAStackOfAnotherKindDoesNotRedistribute()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10), (MonsterType.Archer, 4));
        ArmyBar bar = CreateBar(army);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));

        ArmyBarEvent result = bar.ProcessEvents(Click(50, 5, MouseButtonType.Right));

        Assert.Null(result.Split);
        Assert.Equal(10u, army.GetTroop(0).Count);
        Assert.Equal(4u, army.GetTroop(1).Count);
    }

    [Fact]
    public void AStackDraggedFromAnotherBarIsMergedIntoThisOne()
    {
        ArmyStack source = ArmyWith((MonsterType.Peasant, 3));
        ArmyStack target = ArmyWith((MonsterType.Peasant, 7));

        ArmyBar sourceBar = CreateBar(source);
        ArmyBar targetBar = CreateBar(target);

        // The two bars sit in different places on the screen, as they do in the reference.
        sourceBar.Layout.SetRenderingOffset(new Point(0, 100));

        sourceBar.ProcessEvents(Click(5, 105, MouseButtonType.Left));
        Assert.Equal(0, sourceBar.SelectedIndex);

        // The press and the release are two frames of the same event, as the reference sees them.
        Point position = new(5, 5);
        LocalEvent events = new(() => 0);
        events.OnMouseMotion(position);
        events.OnMouseButton(true, MouseButtonType.Left, position);
        targetBar.ProcessEvents(events, sourceBar);
        events.OnMouseButton(false, MouseButtonType.Left, position);

        ArmyBarEvent result = targetBar.ProcessEvents(events, sourceBar);

        Assert.True(result.Handled);
        Assert.Equal(10u, target.GetTroop(0).Count);
        Assert.True(source.GetTroop(0).IsEmpty);
        Assert.False(sourceBar.IsSelected);
    }

    [Fact]
    public void AReadOnlyBarShowsTheStackButChangesNothing()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10), (MonsterType.Archer, 5));
        ArmyBar bar = CreateBar(army, readOnly: true);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));
        bar.ProcessEvents(Click(50, 5, MouseButtonType.Left));

        Assert.Equal(MonsterType.Peasant, army.GetTroop(0).Monster);
        Assert.Equal(10u, army.GetTroop(0).Count);
        Assert.Equal(MonsterType.Archer, army.GetTroop(1).Monster);
    }

    [Fact]
    public void ABarWithoutAnArmyHandlesNothing()
    {
        ArmyBar bar = new(null, miniSprites: true, readOnly: false);
        bar.Layout.SetTableSize(new Size(5, 1));

        Assert.Equal(0, bar.ItemsCount);
        Assert.False(bar.ProcessEvents(Click(5, 5, MouseButtonType.Left)).Handled);
        Assert.True(bar.GetTroop(0) is null);
    }

    [Fact]
    public void TheMiniBackgroundIsPaintedIntoEverySlot()
    {
        ArmyBar bar = CreateBar(ArmyWith((MonsterType.Peasant, 10)));
        bar.SetBackground(new Size(43, 43), fillColor: 10, borderColor: 20);

        PixelBuffer output = new(215, 43);
        bar.Redraw(output);

        Assert.Equal(20, output[0, 0]);
        Assert.Equal(10, output[5, 5]);
        Assert.Equal(20, output[44, 0]);
        Assert.Equal(10, output[50, 5]);
    }

    [Fact]
    public void TheSelectedStackIsFramed()
    {
        ArmyBar bar = CreateBar(ArmyWith((MonsterType.Peasant, 10)));
        bar.SetBackground(new Size(43, 43), fillColor: 10, borderColor: 20);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));

        PixelBuffer output = new(215, 43);
        bar.Redraw(output);

        Assert.Equal(ArmyBar.CursorColor, output[1, 0]);
        Assert.Equal(ArmyBar.CursorColor, output[0, 20]);
        Assert.Equal(ArmyBar.CursorColor, output[42, 42]);

        // The second slot shows its own background and no frame.
        Assert.Equal(20, output[43, 1]);
        Assert.Equal(10, output[44, 1]);
    }

    [Fact]
    public void AMiniPortraitIsDrawnStandingOnTheSlotFloor()
    {
        ArmyBar bar = CreateBar(ArmyWith((MonsterType.Peasant, 10)));

        ArmyBarSpriteSet sprites = new();
        Sprite portrait = new(32, 32);
        ImageOps.FillSprite(portrait, portrait.Image.Bounds, 7);
        sprites.MiniMonsters[MonsterType.Peasant] = portrait;

        PixelBuffer output = new(215, 43);
        bar.Redraw(output, sprites);

        // Centred horizontally, one pixel above the floor: x = 5 .. 36, y = 10 .. 41.
        Assert.Equal(7, output[20, 20]);
        Assert.Equal(7, output[6, 41]);
        Assert.Equal(0, output[20, 8]);
    }

    [Fact]
    public void ApplySplitRefusesAnAnswerOutsideTheDialogRange()
    {
        ArmyStack army = ArmyWith((MonsterType.Peasant, 10), (MonsterType.Peasant, 2));
        ArmyBar bar = CreateBar(army);

        bar.ProcessEvents(Click(5, 5, MouseButtonType.Left));
        ArmyBarSplit split = Assert.IsType<ArmyBarSplit>(
            bar.ProcessEvents(Click(50, 5, MouseButtonType.Right)).Split);

        Assert.False(ArmyBar.ApplySplit(split, 1, useFastSplit: true));
        Assert.False(ArmyBar.ApplySplit(split, 7, useFastSplit: true));
    }

    [Fact]
    public void TheInformationWindowAnswerDismissesOrUpgradesTheStack()
    {
        Troop troop = new(MonsterType.Peasant, 10);

        ArmyBar.ApplyInfoAnswer(troop, DialogButton.Dismiss);
        Assert.True(troop.IsEmpty);

        Troop archer = new(MonsterType.Archer, 5);
        Funds? paid = null;

        ArmyBar.ApplyInfoAnswer(archer, DialogButton.Upgrade, funds => paid = funds);

        Assert.NotNull(paid);
        Assert.Equal(MonsterType.Ranger, archer.Monster);
        Assert.Equal(5u, archer.Count);
    }

    private static ArmyStack ArmyWith(params (MonsterType Monster, uint Count)[] stacks)
    {
        ArmyStack army = new();

        foreach ((MonsterType monster, uint count) in stacks)
        {
            Assert.True(army.JoinTroop(monster, count));
        }

        return army;
    }

    private static ArmyBar CreateBar(ArmyStack army, bool readOnly = false, bool canChange = false,
        bool saveLastTroop = true)
    {
        ArmyBar bar = new(army, miniSprites: true, readOnly, canChange, saveLastTroop);
        bar.Layout.SetTableSize(new Size(5, 1));

        return bar;
    }

    private static LocalEvent Click(int x, int y, MouseButtonType button)
    {
        Point position = new(x, y);
        LocalEvent events = new(() => 0);

        events.OnMouseMotion(position);
        events.OnMouseButton(true, button, position);
        events.OnMouseButton(false, button, position);

        return events;
    }

    /// <summary>A frame in which the button is still down, which is what the "hold" handlers answer.</summary>
    private static LocalEvent Press(int x, int y, MouseButtonType button)
    {
        Point position = new(x, y);
        LocalEvent events = new(() => 0);

        events.OnMouseMotion(position);
        events.OnMouseButton(true, button, position);

        return events;
    }
}
