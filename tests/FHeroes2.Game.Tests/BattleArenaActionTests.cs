using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using Xunit;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the first slice of <c>battle_action.cpp</c> that the port took: the dispatcher and the actions that change the
/// board without the interface — move, skip and morale (<c>battle_action.cpp:278-759</c>).
/// </summary>
public class BattleArenaActionTests
{
    private static GameArmy Army(PlayerColor color, MonsterType monster = MonsterType.Peasant)
    {
        GameArmy army = new()
        {
            Color = color,
        };

        army.GetTroop(0).Set(monster, 10);

        return army;
    }

    private static BattleArena Field(MonsterType attacker = MonsterType.Peasant) =>
        new(Army(PlayerColor.Blue, attacker), Army(PlayerColor.Red), new Pcg32(11));

    private static BattleArena Siege()
    {
        CastleTown castle = new(7, MapRace.Knight);

        foreach (BuildingType building in new[] { BuildingType.Castle, BuildingType.Spec, BuildingType.Moat })
        {
            castle.AddBuildings(building);
        }

        return new BattleArena(Army(PlayerColor.Blue), Army(PlayerColor.Red), castle, new Pcg32(13));
    }

    [Fact]
    public void AMoveWalksTheUnitToTheCellAndSpendsItsTurn()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        Assert.Equal(22, unit.HeadIndex);

        arena.ApplyAction(BattleCommand.Move((int)unit.Uid, 24));

        Assert.Equal(24, unit.HeadIndex);
        Assert.True(unit.HasModes(BattleMonsterState.Moved));

        // 本回合已经动过，再来一条移动命令是编程错误。
        Assert.Throws<ArgumentException>(() => arena.ApplyAction(BattleCommand.Move((int)unit.Uid, 26)));
    }

    [Fact]
    public void AMoveTheAnimationAlreadyWalkedIsOnlyMarkedAsSpent()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        // 界面层先播完行军：BattleFieldPainter 的 MovePlayback 每格 SetPosition，播到终点时模型已经站在 24
        // （battle_interface.cpp:4524-4830）。竞技场再把命令应用一次时只剩「这一回合用掉了」要记。
        unit.SetPosition(24);

        arena.ApplyAction(BattleCommand.Move((int)unit.Uid, 24));

        Assert.Equal(24, unit.HeadIndex);
        Assert.True(unit.HasModes(BattleMonsterState.Moved));
    }

    [Fact]
    public void AUnitCannotMoveOntoItsOwnCell()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        Assert.False(arena.CheckMoveParams(unit, unit.HeadIndex));
    }

    [Fact]
    public void ACellBeyondTheMovementRangeIsNotReachable()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        // 农民只有 2 点移动力：走 8 格要走 8 点。
        Assert.True(BattlePosition.GetReachable(arena, unit, 30).IsEmpty);
        Assert.False(arena.CheckMoveParams(unit, 30));

        // 恰好走满 2 格还是可以的。
        Assert.False(BattlePosition.GetReachable(arena, unit, 24).IsEmpty);
    }

    [Fact]
    public void AWideUnitEndsUpWithItsTailBehindItsHead()
    {
        BattleArena arena = Field(MonsterType.Cavalry);
        BattleUnit unit = arena.AttackingForce.First();

        Assert.True(unit.IsWide);
        Assert.Equal(23, unit.HeadIndex);
        Assert.Equal(22, unit.TailIndex);

        arena.ApplyAction(BattleCommand.Move((int)unit.Uid, 27));

        Assert.Equal(27, unit.HeadIndex);
        Assert.Equal(26, unit.TailIndex);
    }

    [Fact]
    public void ASkipMarksTheUnitAsBothSkippedAndMoved()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        arena.ApplyAction(BattleCommand.Skip((int)unit.Uid));

        Assert.True(unit.HasModes(BattleMonsterState.Skip));
        Assert.True(unit.HasModes(BattleMonsterState.Moved));
        Assert.Equal(22, unit.HeadIndex);
    }

    [Fact]
    public void GoodMoraleGivesTheUnitItsActionBack()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        unit.SetModes(BattleMonsterState.Moved);
        unit.SetModes(BattleMonsterState.MoraleGood);

        arena.ApplyAction(BattleCommand.Morale((int)unit.Uid, 1));

        Assert.False(unit.HasModes(BattleMonsterState.Moved));
        Assert.False(unit.HasModes(BattleMonsterState.MoraleGood));
    }

    [Fact]
    public void BadMoraleTakesTheActionAway()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        unit.SetModes(BattleMonsterState.MoraleBad);

        arena.ApplyAction(BattleCommand.Morale((int)unit.Uid, 0));

        Assert.False(unit.HasModes(BattleMonsterState.MoraleBad));
        Assert.True(unit.HasModes(BattleMonsterState.Moved));
        Assert.Equal(22, unit.HeadIndex);
    }

    [Fact]
    public void AMoraleAnswerTheUnitIsNotWaitingForIsRefused()
    {
        BattleArena arena = Field();
        BattleUnit unit = arena.AttackingForce.First();

        // 没有 MORALE_BAD 却收到坏士气：参考打日志，端口抛。
        Assert.Throws<ArgumentException>(() => arena.ApplyAction(BattleCommand.Morale((int)unit.Uid, 0)));
    }

    [Fact]
    public void ACatapultShotWithoutACastleIsRefused()
    {
        BattleArena arena = Field();

        // 野战没有投石车：参考在 ApplyActionCatapult 里碰到空指针就返回，端口判成调用方的错误。
        Assert.Throws<ArgumentException>(() => arena.ApplyAction(BattleCommand.Catapult()));
    }

    [Fact]
    public void TheBridgeStaysDownWhileAUnitStandsOnTheGate()
    {
        BattleArena arena = Siege();
        BattleUnit unit = arena.AttackingForce.First();

        Assert.NotNull(arena.Bridge);
        Assert.True(arena.Bridge!.ActionDown());

        // 站在桥前，走到闸门格上：48 → 49 → 50 正好两步，农民的移动力够。
        unit.SetPosition(BattleBridge.CellBeforeMoat);

        arena.ApplyAction(BattleCommand.Move((int)unit.Uid, BattleBridge.CellGates));

        Assert.Equal(BattleBridge.CellGates, unit.HeadIndex);

        // 闸门格被占，桥不能升起来——否则单位会被夹在中间。
        Assert.True(arena.Bridge.IsDown);
    }

    [Fact]
    public void TheDefaultFacingFollowsTheSideOfTheArmy()
    {
        BattleArena arena = Field();

        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        // 布阵时守方已经反过来了；UpdateDirection 把它们各自收回到「攻方朝右、守方朝左」。
        defender.SetReflection(false);
        attacker.SetReflection(true);

        attacker.UpdateDirection();
        defender.UpdateDirection();

        Assert.False(attacker.IsReflect);
        Assert.True(defender.IsReflect);
    }
}
