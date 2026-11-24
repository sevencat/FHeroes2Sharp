using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using Xunit;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The commands a click turns into, <see cref="BattleArena.CreateMoveCommand"/> and
/// <see cref="BattleArena.CreateAttackCommand"/> (the decision half of
/// <c>Interface::HumanBattleTurn()</c>).
/// </summary>
public class BattleArenaCommandTests
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

    private static BattleArena Field()
    {
        SetControl(PlayerColor.Blue, PlayerControl.Human);
        SetControl(PlayerColor.Red, PlayerControl.Ai);

        return new BattleArena(
            Army(PlayerColor.Blue, MonsterType.Swordsman, 5), Army(PlayerColor.Red, MonsterType.Peasant, 10), new Pcg32(17));
    }

    [Fact]
    public void AReachableCellBecomesAMoveCommand()
    {
        BattleArena arena = Field();
        BattleUnit attacker = arena.AttackingForce.First();
        int destination = attacker.HeadIndex + 2;

        BattleCommand? command = arena.CreateMoveCommand(attacker, destination);

        Assert.NotNull(command);
        Assert.Equal(BattleCommandType.Move, command!.Type);
        Assert.Equal(destination, command[0]);
    }

    [Fact]
    public void ACellOutOfReachGivesNoMoveCommand()
    {
        BattleArena arena = Field();
        BattleUnit attacker = arena.AttackingForce.First();

        // Far corner of the board: beyond a swordsman's four speed.
        BattleCommand? command = arena.CreateMoveCommand(attacker, 98);

        Assert.Null(command);
    }

    [Fact]
    public void AnEnemyCellBecomesAnAttackCommand()
    {
        BattleArena arena = Field();
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        int adjacent = BattleBoard.GetAroundIndexes(defender.Position)
            .First(index => arena.Board.GetCell(index)?.IsPassable(false) == true);

        attacker.SetPosition(adjacent);

        BattleCommand? command = arena.CreateAttackCommand(attacker, defender.HeadIndex);

        Assert.NotNull(command);
        Assert.Equal(BattleCommandType.Attack, command!.Type);
    }

    [Fact]
    public void AnEmptyOrFriendlyCellGivesNoAttackCommand()
    {
        BattleArena arena = Field();
        BattleUnit attacker = arena.AttackingForce.First();

        Assert.Null(arena.CreateAttackCommand(attacker, attacker.HeadIndex));
        Assert.Null(arena.CreateAttackCommand(attacker, 24));
        Assert.Null(arena.CreateMoveCommand(attacker, -1));
        Assert.Null(arena.CreateMoveCommand(attacker, BattleBoard.SizeInCells));
    }

    private static void SetControl(PlayerColor color, PlayerControl control)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = control;

        Players.Set(color, player);
    }
}
