using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Where a unit stands when it strikes a cell from a chosen side,
/// <see cref="BattleBoard.CalculateAttackPosition"/> (<c>battle_interface.cpp:689-798</c>).
/// </summary>
/// <remarks>
/// 这一对函数（<c>calculateAttackPosition</c> 与 <c>getAttackingCellDirectionForAttackDirection</c>）在参考里服务界面：
/// 光标指着目标格的某一瓣，界面就高亮出「人真正会站到的那一格」。端口把它从界面里搬回棋盘，于是能单测。
/// </remarks>
public class BattleAttackPositionTests
{
    private static GameArmy Army(PlayerColor color, MonsterType monster, int count)
    {
        GameArmy army = new()
        {
            Color = color,
        };

        army.GetTroop(0).Set(monster, (uint)count);

        return army;
    }

    private static BattleArena Field(MonsterType attacker)
    {
        SetControl(PlayerColor.Blue, PlayerControl.Human);
        SetControl(PlayerColor.Red, PlayerControl.Ai);

        return new BattleArena(
            Army(PlayerColor.Blue, attacker, 5), Army(PlayerColor.Red, MonsterType.Peasant, 10), new Pcg32(23));
    }

    /// <summary>The straight sides resolve against the unit's facing: a unit faces right when not reflected.</summary>
    [Fact]
    public void TheStraightTopAndBottomResolveWithTheUnitsFacing()
    {
        BattleArena arena = Field(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();

        CellDirection top = unit.IsReflect ? CellDirection.TopLeft : CellDirection.TopRight;
        CellDirection bottom = unit.IsReflect ? CellDirection.BottomLeft : CellDirection.BottomRight;

        Assert.Equal(top, BattleBoard.GetAttackingCellDirection(unit, AttackDirection.Top));
        Assert.Equal(bottom, BattleBoard.GetAttackingCellDirection(unit, AttackDirection.Bottom));
    }

    /// <summary>The six sides of the ring map to themselves.</summary>
    [Theory]
    [InlineData(AttackDirection.TopLeft, CellDirection.TopLeft)]
    [InlineData(AttackDirection.TopRight, CellDirection.TopRight)]
    [InlineData(AttackDirection.Right, CellDirection.Right)]
    [InlineData(AttackDirection.BottomRight, CellDirection.BottomRight)]
    [InlineData(AttackDirection.BottomLeft, CellDirection.BottomLeft)]
    [InlineData(AttackDirection.Left, CellDirection.Left)]
    public void TheRingSidesMapToThemselves(AttackDirection direction, CellDirection expected)
    {
        BattleArena arena = Field(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();

        Assert.Equal(expected, BattleBoard.GetAttackingCellDirection(unit, direction));
    }

    /// <summary>A single-cell unit simply stands on the neighbour the blow comes from.</summary>
    [Fact]
    public void ASingleCellUnitStandsOnTheNeighbourOfTheBlow()
    {
        BattleArena arena = Field(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();
        const int target = 45;
        int standing = BattleBoard.GetIndexDirection(target, CellDirection.BottomRight);

        unit.SetPosition(standing);

        BattlePosition position = BattleBoard.CalculateAttackPosition(
            arena, unit, target, AttackDirection.BottomRight);

        Assert.Equal(standing, position.HeadIndex);
    }

    /// <summary>The centre and the unknown have no side to come from: the reference looks them up as they are.</summary>
    [Fact]
    public void TheCentreAndTheUnknownHaveNoSide()
    {
        BattleArena arena = Field(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();

        Assert.Equal(CellDirection.Center, BattleBoard.GetAttackingCellDirection(unit, AttackDirection.Center));
        Assert.Equal(CellDirection.Unknown, BattleBoard.GetAttackingCellDirection(unit, AttackDirection.Unknown));
    }

    /// <summary>
    /// A wide unit's tail-side blow still has to land its tail on the neighbour the blow comes from,
    /// so the head may be shifted one more cell towards the tail.
    /// </summary>
    [Fact]
    public void AWideUnitsTailSideBlowCoversTheNeighbourItStrikesFrom()
    {
        BattleArena arena = Field(MonsterType.Cavalry);
        BattleUnit unit = arena.AttackingForce.First();

        Assert.True(unit.IsWide);

        const int target = 45;
        CellDirection side = unit.IsReflect ? CellDirection.TopLeft : CellDirection.TopRight;
        int standing = BattleBoard.GetIndexDirection(target, side);

        unit.SetPosition(standing);

        BattlePosition position = BattleBoard.CalculateAttackPosition(arena, unit, target, AttackDirection.Top);

        if (!position.IsEmpty)
        {
            Assert.Contains(standing, new[] { position.HeadIndex, position.TailIndex });
        }
    }

    /// <summary>An unreachable side gives an empty position rather than an invented one.</summary>
    [Fact]
    public void AnUnreachableSideGivesNoPosition()
    {
        BattleArena arena = Field(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();

        // The bottom right corner while the target sits in the top left: a peasant's speed cannot cross the field.
        unit.SetPosition(98);

        Assert.True(BattleBoard.CalculateAttackPosition(arena, unit, 0, AttackDirection.Right).IsEmpty);
    }

    private static void SetControl(PlayerColor color, PlayerControl control)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = control;

        Players.Set(color, player);
    }
}
