using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Battle;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Which sequences an attack plays, <see cref="BattleAttackAnimation"/>: the row arithmetic of
/// <c>RedrawActionAttackPart1()</c> (<c>battle_interface.cpp:4158-4174</c>), the second half
/// (<c>:4185-4195</c>) and the wince/kill branch (<c>:4288-4329</c>).
/// </summary>
public sealed class BattleAttackAnimationTests
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
        SetControl(PlayerColor.Blue, PlayerControl.Human);
        SetControl(PlayerColor.Red, PlayerControl.Ai);

        return new BattleArena(
            Army(PlayerColor.Blue, attacker, attackerCount),
            Army(PlayerColor.Red, defender, defenderCount),
            new Pcg32(59));
    }

    private static void SetControl(PlayerColor color, PlayerControl control)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = control;

        Players.Set(color, player);
    }

    /// <summary>Two units of one field, each standing on a cell the caller picks.</summary>
    private static (BattleUnit Attacker, BattleUnit Defender) Place(BattleArena arena, int attackerCell, int defenderCell)
    {
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        attacker.SetPosition(attackerCell);
        defender.SetPosition(defenderCell);

        return (attacker, defender);
    }

    /// <summary>A target on the same row is attacked head on, <c>MELEE_FRONT</c>.</summary>
    [Fact]
    public void AMeleeTargetOnTheSameRowIsHitHeadOn()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 10);
        (BattleUnit attacker, BattleUnit defender) = Place(arena, 30, 32);

        Assert.Equal(
            MonsterAnimationType.MeleeFront,
            BattleAttackAnimation.SelectSwing(attacker, defender, isShooting: false, isDoubleCell: false));
    }

    /// <summary>A target drawn lower is attacked downwards, <c>MELEE_BOT</c> (<c>:4169-4173</c>).</summary>
    [Fact]
    public void AMeleeTargetBelowIsHitDownwards()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 10);
        (BattleUnit attacker, BattleUnit defender) = Place(arena, 30, 41);

        Assert.Equal(
            MonsterAnimationType.MeleeBot,
            BattleAttackAnimation.SelectSwing(attacker, defender, isShooting: false, isDoubleCell: false));
    }

    /// <summary>A target drawn higher is attacked upwards, <c>MELEE_TOP</c>.</summary>
    [Fact]
    public void AMeleeTargetAboveIsHitUpwards()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 10);
        (BattleUnit attacker, BattleUnit defender) = Place(arena, 41, 30);

        Assert.Equal(
            MonsterAnimationType.MeleeTop,
            BattleAttackAnimation.SelectSwing(attacker, defender, isShooting: false, isDoubleCell: false));
    }

    /// <summary>A blow that sweeps two cells swings with <c>RANG_FRONT</c> even in melee, the reference's
    /// <c>isDoubleCell ? RANG_FRONT : MELEE_FRONT</c>.</summary>
    [Fact]
    public void ADoubleCellBlowSwingsWithTheWideSequence()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 10);
        (BattleUnit attacker, BattleUnit defender) = Place(arena, 30, 32);

        Assert.Equal(
            MonsterAnimationType.RangFront,
            BattleAttackAnimation.SelectSwing(attacker, defender, isShooting: false, isDoubleCell: true));
    }

    /// <summary>A shot uses the same row split on the shooting sequences.</summary>
    [Fact]
    public void AShotUsesTheShootingSequences()
    {
        BattleArena arena = Arena(MonsterType.Archer, 5, MonsterType.Peasant, 10);
        (BattleUnit attacker, BattleUnit defender) = Place(arena, 30, 32);
        Assert.Equal(
            MonsterAnimationType.RangFront,
            BattleAttackAnimation.SelectSwing(attacker, defender, isShooting: true, isDoubleCell: false));

        (attacker, defender) = Place(arena, 30, 41);
        Assert.Equal(
            MonsterAnimationType.RangBot,
            BattleAttackAnimation.SelectSwing(attacker, defender, isShooting: true, isDoubleCell: false));

        (attacker, defender) = Place(arena, 41, 30);
        Assert.Equal(
            MonsterAnimationType.RangTop,
            BattleAttackAnimation.SelectSwing(attacker, defender, isShooting: true, isDoubleCell: false));
    }

    /// <summary>The second half of a swing is the next sequence along, <c>++attackStart</c>.</summary>
    [Fact]
    public void TheSecondHalfIsTheNextSequence()
    {
        Assert.Equal(MonsterAnimationType.MeleeFrontEnd, BattleAttackAnimation.SelectSwingEnd(MonsterAnimationType.MeleeFront));
        Assert.Equal(MonsterAnimationType.RangTopEnd, BattleAttackAnimation.SelectSwingEnd(MonsterAnimationType.RangTop));

        // 不是攻击序列时原样返回：参考只对攻击区间里的状态 ++。
        Assert.Equal(MonsterAnimationType.Static, BattleAttackAnimation.SelectSwingEnd(MonsterAnimationType.Static));
    }

    /// <summary>A stack that falls gets the death sequence, a stack that only loses troops the wince.</summary>
    [Fact]
    public void ReactionsFollowTheDamageAndTheLosses()
    {
        BattleArena killed = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 1);
        BattleUnit killer = killed.AttackingForce.First();
        BattleUnit victim = killed.DefendingForce.First();

        killer.SetPosition(
            BattleBoard.GetAroundIndexes(victim.Position).First(index => killed.Board.GetCell(index)?.IsPassable(true) == true));

        killed.ApplyAction(BattleCommand.Attack((int)killer.Uid, (int)victim.Uid, -1, -1));

        BattleDamageTarget dead = killed.LastAttackBlows[0].Targets[0];

        Assert.False(dead.Defender.IsValid);
        Assert.Equal(MonsterAnimationType.Kill, BattleAttackAnimation.SelectReaction(dead));

        BattleArena wince = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 40);
        BattleUnit striker = wince.AttackingForce.First();
        BattleUnit survivor = wince.DefendingForce.First();

        striker.SetPosition(
            BattleBoard.GetAroundIndexes(survivor.Position).First(index => wince.Board.GetCell(index)?.IsPassable(true) == true));

        wince.ApplyAction(BattleCommand.Attack((int)striker.Uid, (int)survivor.Uid, -1, -1));

        BattleDamageTarget hurt = wince.LastAttackBlows[0].Targets[0];

        Assert.True(hurt.Defender.IsValid);
        Assert.True(hurt.Damage > 0);
        Assert.Equal(MonsterAnimationType.Wnce, BattleAttackAnimation.SelectReaction(hurt));
    }

    /// <summary>A single blow only leaves its reactions: its swing was played before the command was applied.</summary>
    [Fact]
    public void ASingleBlowReplaysOnlyItsReactions()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 1);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        attacker.SetPosition(
            BattleBoard.GetAroundIndexes(defender.Position).First(index => arena.Board.GetCell(index)?.IsPassable(true) == true));

        arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1));

        IReadOnlyList<BattleAttackStep> steps = BattleAttackAnimation.CreateReplay(arena.LastAttackBlows);

        BattleAttackStep step = Assert.Single(steps);

        Assert.Same(defender, step.Unit);
        Assert.Equal(MonsterAnimationType.Kill, step.Animation);
    }

    /// <summary>The retaliation plays its own swing before its reaction, <c>BattleProcess()</c> per blow.</summary>
    [Fact]
    public void TheRetaliationSwingIsReplayedToo()
    {
        BattleArena arena = Arena(MonsterType.Swordsman, 5, MonsterType.Peasant, 200);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        attacker.SetPosition(
            BattleBoard.GetAroundIndexes(defender.Position).First(index => arena.Board.GetCell(index)?.IsPassable(true) == true));

        arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1));

        Assert.Equal(2, arena.LastAttackBlows.Count);

        IReadOnlyList<BattleAttackStep> steps = BattleAttackAnimation.CreateReplay(arena.LastAttackBlows);

        // 第一击只剩受击，第二击（反击）是「起手 + 收势 + 受击」。
        Assert.Equal(4, steps.Count);
        Assert.Same(defender, steps[0].Unit);
        Assert.Equal(MonsterAnimationType.Wnce, steps[0].Animation);
        Assert.Same(defender, steps[1].Unit);
        Assert.Equal(MonsterAnimationType.MeleeTop, steps[1].Animation);
        Assert.Equal(MonsterAnimationType.MeleeTopEnd, steps[2].Animation);
        Assert.Same(attacker, steps[3].Unit);
    }
}
