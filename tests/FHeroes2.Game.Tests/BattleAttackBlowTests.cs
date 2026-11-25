using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The record of the last attack, <see cref="BattleArena.LastAttackBlows"/>: what the screen needs to draw
/// the swing, the wince and the death it did not get to see (<c>battle_interface.cpp:4088-4439</c>).
/// </summary>
public class BattleAttackBlowTests
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

    private static BattleArena Arena(MonsterType attacker, int attackerCount, MonsterType defender, int defenderCount)
    {
        SetControl(PlayerColor.Blue);
        SetControl(PlayerColor.Red);

        return new BattleArena(
            Army(PlayerColor.Blue, attacker, attackerCount),
            Army(PlayerColor.Red, defender, defenderCount),
            new Pcg32(53));
    }

    private static void SetControl(PlayerColor color)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = PlayerControl.Human;

        Players.Set(color, player);
    }

    /// <summary>Puts the attacker on the first free cell around the defender, the way a walked-in attack ends up.</summary>
    private static void PlaceBeside(BattleArena arena, BattleUnit attacker, BattleUnit defender) =>
        attacker.SetPosition(
            BattleBoard.GetAroundIndexes(defender.Position)
                .First(index => arena.Board.GetCell(index)?.IsPassable(true) == true));

    /// <summary>One melee blow records the pair, the flag and the one stack it hit.</summary>
    [Fact]
    public void AMeleeBlowIsRecorded()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 40);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        PlaceBeside(arena, attacker, defender);

        arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1));

        Assert.NotEmpty(arena.LastAttackBlows);

        BattleAttackBlow blow = arena.LastAttackBlows[0];

        Assert.Same(attacker, blow.Attacker);
        Assert.Same(defender, blow.Defender);
        Assert.False(blow.IsShooting);
        Assert.Single(blow.Targets);
        Assert.Same(defender, blow.Targets[0].Defender);
        Assert.True(blow.Targets[0].Damage > 0);
    }

    /// <summary>A shot is flagged as one: the screen plays the shooting sequences for it (<c>battle_interface.cpp:4102-4119</c>).</summary>
    [Fact]
    public void AShotIsFlaggedAsOne()
    {
        BattleArena arena = Arena(MonsterType.Archer, 5, MonsterType.Peasant, 10);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1));

        BattleAttackBlow blow = Assert.Single(arena.LastAttackBlows);

        Assert.True(blow.IsShooting);
    }

    /// <summary>The retaliation is a blow of its own, with the roles swapped (<c>battle_action.cpp:230-250</c>).</summary>
    [Fact]
    public void TheRetaliationIsRecordedAsItsOwnBlow()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 200);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        PlaceBeside(arena, attacker, defender);

        arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1));

        Assert.Equal(2, arena.LastAttackBlows.Count);

        BattleAttackBlow retaliation = arena.LastAttackBlows[1];

        Assert.Same(defender, retaliation.Attacker);
        Assert.Same(attacker, retaliation.Defender);
        Assert.False(retaliation.IsShooting);
    }

    /// <summary>A shot draws no retaliation: the target is not hand-fighting (<c>Unit::isHandFighting()</c>).</summary>
    [Fact]
    public void AShotDrawsNoRetaliation()
    {
        BattleArena arena = Arena(MonsterType.Archer, 5, MonsterType.Peasant, 10);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1));

        Assert.Single(arena.LastAttackBlows);
    }

    /// <summary>The record describes the last attack only, not the whole battle.</summary>
    [Fact]
    public void TheRecordIsClearedByTheNextAttack()
    {
        SetControl(PlayerColor.Blue);
        SetControl(PlayerColor.Red);

        GameArmy blue = Army(PlayerColor.Blue, MonsterType.Swordsman, 5);
        blue.GetTroop(1).Set(MonsterType.Swordsman, 5);

        BattleArena arena = new(blue, Army(PlayerColor.Red, MonsterType.Peasant, 200), new Pcg32(53));

        BattleUnit firstAttacker = arena.AttackingForce.First();
        BattleUnit secondAttacker = arena.AttackingForce.Last();
        BattleUnit defender = arena.DefendingForce.First();

        PlaceBeside(arena, firstAttacker, defender);

        arena.ApplyAction(BattleCommand.Attack((int)firstAttacker.Uid, (int)defender.Uid, -1, -1));

        int blows = arena.LastAttackBlows.Count;

        Assert.True(blows > 0);

        // 另一支部队再打一次：记录不该把上一击也攒着。
        secondAttacker.SetPosition(
            BattleBoard.GetAroundIndexes(defender.Position)
                .First(index => arena.Board.GetCell(index)?.IsPassable(true) == true));

        arena.ApplyAction(BattleCommand.Attack((int)secondAttacker.Uid, (int)defender.Uid, -1, -1));

        // 第二击的记录从它自己开始，没有把第一击攒进来（守方已经还过手，所以这次可能只有一击）。
        Assert.Same(secondAttacker, arena.LastAttackBlows[0].Attacker);
        Assert.InRange(arena.LastAttackBlows.Count, 1, blows);
    }
}
