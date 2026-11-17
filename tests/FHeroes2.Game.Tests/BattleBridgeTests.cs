using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::Bridge</c> (<c>battle_bridge.cpp</c>): who may cross, when it may move, and what it writes
/// into the gate cell.
/// </summary>
public class BattleBridgeTests
{
    private static BattleUnit Spawn(
        BattleBoardCells board,
        MonsterType monster,
        int head,
        PlayerColor color,
        uint uid = 1,
        bool wide = false)
    {
        BattlePosition position = new(board, head, wide, false);

        return new BattleUnit(new Troop(monster, 5), position, false, uid) { Color = color };
    }

    private static (BattleBridge Bridge, BattleBoardCells Board, BattleGraveyard Graveyard) Create()
    {
        BattleBoardCells board = new();
        BattleGraveyard graveyard = new();

        return (new BattleBridge(board, graveyard, PlayerColor.Blue), board, graveyard);
    }

    [Fact]
    public void ANewBridgeIsUpAndCrossableOnlyByTheDefender()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();

        Assert.True(bridge.IsValid);
        Assert.False(bridge.IsDown);
        Assert.False(bridge.IsDestroyed);

        Assert.True(bridge.IsPassable(Spawn(board, MonsterType.Peasant, 40, PlayerColor.Blue)));
        Assert.False(bridge.IsPassable(Spawn(board, MonsterType.Peasant, 20, PlayerColor.Red, 2)));
    }

    [Fact]
    public void ABodyOnTheMoatStopsTheBridge()
    {
        (BattleBridge bridge, BattleBoardCells board, BattleGraveyard graveyard) = Create();

        graveyard.AddUnit(Spawn(board, MonsterType.Peasant, BridgeCell.Moat, PlayerColor.Red));

        Assert.False(bridge.ActionDown());
        Assert.False(bridge.IsPassable(Spawn(board, MonsterType.Peasant, 40, PlayerColor.Blue, 2)));
    }

    [Fact]
    public void ALivingStackInTheGateStopsTheBridge()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();

        Spawn(board, MonsterType.Peasant, BridgeCell.Gates, PlayerColor.Red);

        Assert.False(bridge.ActionDown());
        Assert.False(bridge.AllowUp);
    }

    [Fact]
    public void TheBridgeGoesDownAndUpAgain()
    {
        (BattleBridge bridge, _, _) = Create();

        Assert.True(bridge.ActionDown());
        Assert.True(bridge.IsDown);
        Assert.True(bridge.AllowUp);

        Assert.True(bridge.ActionUp());
        Assert.False(bridge.IsDown);

        // 已经起来了，再放一次也算「什么都没发生」。
        Assert.False(bridge.ActionUp());
    }

    [Fact]
    public void AWreckedBridgeStaysDownForEverybody()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();
        BattleUnit enemy = Spawn(board, MonsterType.Peasant, 20, PlayerColor.Red);

        bridge.SetDestroyed();

        Assert.True(bridge.IsDestroyed);
        Assert.True(bridge.IsDown);
        Assert.True(bridge.IsPassable(enemy));
        Assert.False(bridge.ActionUp());
        Assert.Equal(0, board.GetCell(BridgeCell.Gates)!.Object);
    }

    [Fact]
    public void PassabilityIsWrittenIntoTheGateCell()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();
        BattleUnit defender = Spawn(board, MonsterType.Peasant, 40, PlayerColor.Blue);
        BattleUnit enemy = Spawn(board, MonsterType.Peasant, 20, PlayerColor.Red, 2);

        // 桥还立着：守城方自己能过，敌人不能。
        bridge.SetPassability(defender);
        Assert.Equal(0, board.GetCell(BridgeCell.Gates)!.Object);

        bridge.SetPassability(enemy);
        Assert.Equal(1, board.GetCell(BridgeCell.Gates)!.Object);

        // 桥放下之后谁都过得了。
        bridge.ActionDown();
        bridge.SetPassability(enemy);
        Assert.Equal(0, board.GetCell(BridgeCell.Gates)!.Object);
    }

    [Fact]
    public void TheDefenderGoingForTheGateNeedsTheBridgeDown()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();
        BattleUnit defender = Spawn(board, MonsterType.Peasant, BridgeCell.BelowBridge, PlayerColor.Blue);

        Assert.True(bridge.NeedDown(defender, BridgeCell.Gates));

        // 从桥上（闸门本身）走进闸门不需要动桥。
        Assert.False(bridge.NeedDown(defender, BridgeCell.AfterGates));

        // 远处走来的人不需要——桥不为了路过的兵白放下来。
        BattleUnit far = Spawn(board, MonsterType.Peasant, 10, PlayerColor.Blue, 2);
        Assert.False(bridge.NeedDown(far, BridgeCell.Gates));

        // 敌人本来就过不去，也不动桥。
        BattleUnit enemy = Spawn(board, MonsterType.Peasant, BridgeCell.BelowBridge, PlayerColor.Red, 3);
        Assert.False(bridge.NeedDown(enemy, BridgeCell.Gates));
    }

    [Fact]
    public void AFlierGoingForTheGateNeedsTheBridgeDown()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();
        BattleUnit vampire = Spawn(board, MonsterType.Vampire, 10, PlayerColor.Blue);

        Assert.True(bridge.NeedDown(vampire, BridgeCell.Gates));
        Assert.False(bridge.NeedDown(vampire, BridgeCell.Moat));
    }

    [Fact]
    public void AStepOntoTheMoatNeedsTheBridgeDownUnlessItComesFromTheGate()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();
        BattleUnit defender = Spawn(board, MonsterType.Peasant, BridgeCell.AboveBridge, PlayerColor.Blue);

        Assert.True(bridge.NeedDown(defender, BridgeCell.Moat));

        BattleUnit fromGate = Spawn(board, MonsterType.Peasant, BridgeCell.Gates, PlayerColor.Blue, 2);
        Assert.False(bridge.NeedDown(fromGate, BridgeCell.Moat));
    }

    [Fact]
    public void AWideDefenderHalfOnTheBridgeNeedsItDownToStepInFrontOfTheMoat()
    {
        (BattleBridge bridge, BattleBoardCells board, _) = Create();
        BattleUnit cavalry = Spawn(board, MonsterType.Cavalry, 62, PlayerColor.Blue, wide: true);

        Assert.Equal(BridgeCell.BelowBridge, cavalry.TailIndex);
        Assert.True(bridge.NeedDown(cavalry, BridgeCell.BeforeMoat));

        BattleUnit elsewhere = Spawn(board, MonsterType.Cavalry, 30, PlayerColor.Blue, 2, wide: true);

        Assert.False(bridge.NeedDown(elsewhere, BridgeCell.BeforeMoat));
    }
}

/// <summary>The cell numbers the bridge cares about, so the tests do not spell out bare integers.</summary>
internal static class BridgeCell
{
    public const int AboveBridge = 39;
    public const int BeforeMoat = 48;
    public const int Moat = 49;
    public const int Gates = 50;
    public const int AfterGates = 51;
    public const int BelowBridge = 61;
}
