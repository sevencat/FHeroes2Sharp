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
/// Covers the turn loop: who acts next, what a turn does to the board, and what the arena settles to
/// (<c>battle_arena.cpp:460-682</c>).
/// </summary>
/// <remarks>
/// The AI of a turn is <see cref="FHeroes2.Game.Ai.BattlePlanner"/>, a process-wide singleton, so this class shares the
/// <see cref="PlannerSingletonCollection"/> collection with <see cref="AiContractTests"/>, which pins that singleton's
/// reset behaviour.
/// </remarks>
[Collection(PlannerSingletonCollection)]
public class BattleArenaTurnTests
{
    /// <summary>The xUnit collection that keeps every reader of the battle planner's singleton state serial.</summary>
    public const string PlannerSingletonCollection = "BattlePlannerSingleton";
    private static GameArmy Army(PlayerColor color, MonsterType monster = MonsterType.Peasant, int count = 10)
    {
        GameArmy army = new()
        {
            Color = color,
        };

        army.GetTroop(0).Set(monster, (uint)count);

        return army;
    }

    /// <summary>A field with one stack per side, both played by a human so the test's handler sees every turn.</summary>
    private static BattleArena HumanField(MonsterType attacker, MonsterType defender, uint seed)
    {
        SetHuman(PlayerColor.Blue);
        SetHuman(PlayerColor.Red);

        return new BattleArena(Army(PlayerColor.Blue, attacker), Army(PlayerColor.Red, defender), new Pcg32(seed));
    }

    private static void SetHuman(PlayerColor color)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = PlayerControl.Human;

        Players.Set(color, player);
    }

    [Fact]
    public void TheFastestStackActsFirst()
    {
        BattleArena arena = new(
            Army(PlayerColor.Blue, MonsterType.Cavalry), Army(PlayerColor.Red), new Pcg32(17));

        BattleUnit? current = arena.GetCurrentUnit();

        Assert.NotNull(current);
        Assert.Equal(MonsterType.Cavalry, current!.Monster);
        Assert.Equal(arena.AttackingForce.Color, current.Color);
    }

    [Fact]
    public void EqualSpeedGivesTheFirstBlowToTheAttacker()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(19));

        // 双方都是农民，速度相同；战斗刚开始时上一回合没有「最后行动方」，所以攻方先手。
        Assert.Same(arena.AttackingForce.First(), arena.GetCurrentUnit());
    }

    [Fact]
    public void AStackThatAlreadyActedWantsTheOtherSide()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(23));

        BattleUnit attacker = arena.AttackingForce.First();
        attacker.SetModes(BattleMonsterState.Moved);

        Assert.Same(arena.DefendingForce.First(), arena.GetCurrentUnit());
    }

    [Fact]
    public void NobodyActsWhenEveryStackIsImmovable()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(29));

        arena.AttackingForce.First().SetModes(BattleMonsterState.Moved);
        arena.DefendingForce.First().SetModes(BattleMonsterState.Moved);

        Assert.Null(arena.GetCurrentUnit());
    }

    [Fact]
    public void TheDeadLeaveTheBoard()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(31));

        BattleUnit defender = arena.DefendingForce.First();
        defender.ApplyDamage(arena.AttackingForce.First(), 1000, out _, out _);

        Assert.False(defender.IsValid);
        Assert.NotNull(arena.Board.GetCell(defender.HeadIndex)?.Unit);

        arena.RemoveDeadUnits();

        Assert.Null(arena.Board.GetCell(defender.HeadIndex)?.Unit);
        Assert.False(arena.DefendingForce.IsValid());
    }

    [Fact]
    public void ABattleOfShootersRunsToTheEndAndSettlesAWinner()
    {
        BattleArena arena = HumanField(MonsterType.Archer, MonsterType.Peasant, 37);

        arena.Turns((arena, unit, actions) =>
        {
            BattleUnit? enemy = arena.GetForce(unit.Color == PlayerColor.Blue ? PlayerColor.Red : PlayerColor.Blue)
                .ValidOnly()
                .FirstOrDefault();

            if (unit.IsArchers && enemy != null)
            {
                actions.Add(BattleCommand.Attack((int)unit.Uid, (int)enemy.Uid, -1, -1));
            }
            else
            {
                // 农民够不着人，先原地不动。
                actions.Add(BattleCommand.Skip((int)unit.Uid));
            }
        });

        Assert.Equal(BattleOutcome.Wins, arena.Result.Attacker);
        Assert.False(arena.DefendingForce.IsValid());

        // 打赢的一方拿到经验的算法与参考一致：按对方的伤亡折算，负数不会出现。
        Assert.True(arena.Result.AttackerExperience > 0);
    }

    [Fact]
    public void AnAttackThatCannotReachIsRefused()
    {
        BattleArena arena = new(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(41));

        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        // 农民是近战，隔着整个战场够不着人。
        Assert.Throws<ArgumentException>(
            () => arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1)));
    }

    [Fact]
    public void AMeleeBlowNextToTheEnemyHurtsItAndSpendsTheTurn()
    {
        BattleArena arena = new(Army(PlayerColor.Blue, MonsterType.Swordsman, 5), Army(PlayerColor.Red), new Pcg32(43));

        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        int defenderHp = (int)defender.HitPoints;

        // 把攻方直接挪到守方旁边的可走格，模拟「已经走完、现在出刀」。
        int adjacent = BattleBoard.GetAroundIndexes(defender.Position)
            .First(index => arena.Board.GetCell(index)?.IsPassable(true) == true);

        attacker.SetPosition(adjacent);

        arena.ApplyAction(BattleCommand.Attack((int)attacker.Uid, (int)defender.Uid, -1, -1));

        Assert.True(attacker.HasModes(BattleMonsterState.Moved));
        Assert.True((int)defender.HitPoints < defenderHp);
    }

    /// <summary>
    /// The stepping API a frame-driven screen uses — <c>BeginRound()</c>, one unit per call,
    /// <c>EndRound()</c> — has to play exactly the round <c>Turns()</c> plays.
    /// </summary>
    [Fact]
    public void TheSteppingApiPlaysTheSameRoundAsTurns()
    {
        SetControl(PlayerColor.Blue, PlayerControl.Ai);
        SetControl(PlayerColor.Red, PlayerControl.Ai);

        BattleArena whole = new(
            Army(PlayerColor.Blue, MonsterType.Archer, 5), Army(PlayerColor.Red, MonsterType.Peasant, 7), new Pcg32(101));

        BattleArena stepped = new(
            Army(PlayerColor.Blue, MonsterType.Archer, 5), Army(PlayerColor.Red, MonsterType.Peasant, 7), new Pcg32(101));

        whole.Turns();

        stepped.BeginRound();

        while (stepped.BattleValid())
        {
            BattleUnit? next = stepped.SelectNextUnit();

            stepped.PlayCastleActions();

            if (!stepped.BattleValid() || next is null)
            {
                break;
            }

            stepped.PlayCurrentUnitTurn((BattleUnitTurnHandler?)null);
        }

        stepped.EndRound();

        Assert.Equal(whole.Result.Attacker, stepped.Result.Attacker);
        Assert.Equal(whole.Result.Defender, stepped.Result.Defender);
        Assert.Equal(whole.Result.AttackerExperience, stepped.Result.AttackerExperience);
        Assert.Equal(whole.Result.DefenderExperience, stepped.Result.DefenderExperience);
        Assert.Equal(TotalCount(whole.AttackingForce), TotalCount(stepped.AttackingForce));
        Assert.Equal(TotalCount(whole.DefendingForce), TotalCount(stepped.DefendingForce));
    }

    /// <summary>
    /// Commands a human picked while the screen waited are played without asking any controller: that is
    /// the seam the frame-driven battle screen needs (the reference blocks inside <c>HumanTurn()</c>).
    /// </summary>
    [Fact]
    public void AHumansPreparedCommandsPlayTheTurnWithoutAController()
    {
        BattleArena arena = HumanField(MonsterType.Peasant, MonsterType.Peasant, 103);
        arena.BeginRound();

        BattleUnit unit = arena.SelectNextUnit()!;
        arena.PlayCastleActions();

        arena.PlayCurrentUnitTurn([BattleCommand.Skip((int)unit.Uid)]);

        Assert.True(unit.HasModes(BattleMonsterState.Moved));
        Assert.Equal(Speed.Standing, unit.GetSpeed());
    }

    private static uint TotalCount(BattleForce force) =>
        force.Aggregate(0u, (total, unit) => total + (unit.IsValid ? unit.Count : 0));

    private static void SetControl(PlayerColor color, PlayerControl control)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = control;

        Players.Set(color, player);
    }
}
