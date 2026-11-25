using FHeroes2.Core.Geometry;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Battle;
using Xunit;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The geometry and the slot list of the turn order bar, <see cref="BattleTurnOrderLayout"/>:
/// <c>Battle::TurnOrder::redraw()</c> (<c>battle_interface.cpp:1268-1340</c>).
/// </summary>
public sealed class BattleTurnOrderLayoutTests
{
    /// <summary>One icon is 43 pixels square, <c>turnOrderMonsterIconSize</c>.</summary>
    [Fact]
    public void AnIconIsFortyThreePixelsSquare()
    {
        Assert.Equal(43, BattleTurnOrderLayout.IconSize);
    }

    /// <summary>How many icons fit, <c>min( width / 43, validUnitCount )</c>.</summary>
    [Fact]
    public void TheSlotCountTakesTheSmallerOfTheWidthAndTheStacks()
    {
        Assert.Equal(8, BattleTurnOrderLayout.GetSlotCount(352, 12));
        Assert.Equal(5, BattleTurnOrderLayout.GetSlotCount(352, 5));
        Assert.Equal(0, BattleTurnOrderLayout.GetSlotCount(42, 3));
        Assert.Equal(0, BattleTurnOrderLayout.GetSlotCount(0, 3));
    }

    /// <summary>The bar is as tall as one icon and centered over the battlefield.</summary>
    [Fact]
    public void TheBarIsCenteredOverTheBattlefield()
    {
        Rect area = BattleTurnOrderLayout.GetArea(new Rect(0, 0, 352, 337), 5);

        Assert.Equal(new Rect(68, 0, 215, 43), area);
    }

    /// <summary>The bar starts at the battlefield's own top edge.</summary>
    [Fact]
    public void TheBarHangsFromTheBattlefieldsTopEdge()
    {
        Rect area = BattleTurnOrderLayout.GetArea(new Rect(16, 10, 352, 337), 2);

        Assert.Equal(10, area.Y);
        Assert.Equal(16 + ((352 - 86) / 2), area.X);
        Assert.Equal(86, area.Width);
        Assert.Equal(43, area.Height);
    }

    /// <summary>Without room there is nothing to draw.</summary>
    [Fact]
    public void NoRoomMeansNoSlots()
    {
        Assert.Empty(BattleTurnOrderLayout.SelectSlots([Spawn(moved: false), Spawn(moved: false)], 0));
    }

    /// <summary>With room for everybody the order is kept as is.</summary>
    [Fact]
    public void EnoughRoomDrawsEveryValidStack()
    {
        BattleUnit[] order = [Spawn(moved: true), Spawn(moved: false), Spawn(moved: false)];

        Assert.Equal([0, 1, 2], BattleTurnOrderLayout.SelectSlots(order, 3));
    }

    /// <summary>Stacks that already acted give way first when the bar is full (<c>:1315-1320</c>).</summary>
    [Fact]
    public void StacksThatActedGiveWayWhenTheBarIsFull()
    {
        BattleUnit[] order = [Spawn(moved: true), Spawn(moved: false), Spawn(moved: false), Spawn(moved: false)];

        Assert.Equal([1, 2, 3], BattleTurnOrderLayout.SelectSlots(order, 3));
    }

    /// <summary>Dead stacks are skipped without taking a slot, nor counting towards the 「moved」 budget.</summary>
    [Fact]
    public void DeadStacksAreSkipped()
    {
        BattleUnit dead = Spawn(moved: false);
        dead.ApplyDamage(Spawn(moved: false), 100, out _, out _);

        BattleUnit[] order = [dead, Spawn(moved: false), Spawn(moved: false)];

        Assert.Equal([1, 2], BattleTurnOrderLayout.SelectSlots(order, 2));
    }

    /// <summary>A stack of peasants at a head cell, optionally already acting this round.</summary>
    private static BattleUnit Spawn(bool moved)
    {
        BattleUnit unit = new(new Troop(MonsterType.Peasant, 5),
            new BattlePosition(new BattleBoardCells(), 40, false, false), false, 1);

        if (moved)
        {
            unit.SetModes(BattleMonsterState.Moved);
        }

        return unit;
    }
}
