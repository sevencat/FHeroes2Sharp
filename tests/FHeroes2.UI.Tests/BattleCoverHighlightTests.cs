using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Battle;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The cells the pointer shadow covers, <see cref="BattleCoverHighlight"/>
/// (the non-spell half of <c>Interface::RedrawCover()</c>, <c>battle_interface.cpp:2360-2453</c>).
/// </summary>
public sealed class BattleCoverHighlightTests
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

    private static BattleArena Arena(MonsterType attacker)
    {
        SetControl(PlayerColor.Blue, PlayerControl.Human);
        SetControl(PlayerColor.Red, PlayerControl.Ai);

        return new BattleArena(
            Army(PlayerColor.Blue, attacker, 5), Army(PlayerColor.Red, MonsterType.Peasant, 10), new Pcg32(31));
    }

    private static void SetControl(PlayerColor color, PlayerControl control)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = control;

        Players.Set(color, player);
    }

    /// <summary>A free cell the unit can walk onto this turn.</summary>
    private static int ReachableEmptyCell(BattleArena arena, BattleUnit unit)
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (arena.Board.GetCell(index) is { Unit: null } && arena.CreateMoveCommand(unit, index) is not null)
            {
                return index;
            }
        }

        throw new InvalidOperationException("The unit can reach no empty cell.");
    }

    /// <summary>The walking range a peasant covers is a handful of cells, not the whole field.</summary>
    [Fact]
    public void TheWalkingRangeStaysWithinTheUnitsSpeed()
    {
        BattleArena arena = Arena(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();

        int reachable = Enumerable.Range(0, BattleBoard.SizeInCells)
            .Count(index => !BattlePosition.GetReachable(arena, unit, index).IsEmpty);

        Assert.InRange(reachable, 1, 40);
    }

    /// <summary>A cursor that is not a sword only lights the cell under the pointer.</summary>
    [Theory]
    [InlineData(CursorTheme.CombatPointer)]
    [InlineData(CursorTheme.CombatInfo)]
    [InlineData(CursorTheme.CombatNone)]
    public void APlainCursorLightsOnlyTheCellUnderThePointer(CursorTheme theme)
    {
        BattleArena arena = Arena(MonsterType.Peasant);
        BattleUnit unit = arena.AttackingForce.First();

        Assert.Equal([45], BattleCoverHighlight.GetCells(arena, unit, 45, theme));
    }

    /// <summary>A sword also lights where the unit would stand, which is not the cell under the pointer.</summary>
    [Fact]
    public void ASwordLightsTheCellTheBlowComesFrom()
    {
        BattleArena arena = Arena(MonsterType.Peasant);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        int target = defender.HeadIndex;
        int standing = BattleBoard.GetIndexDirection(target, CellDirection.Left);

        attacker.SetPosition(standing);

        // The sword pointing right says the blow comes from the left, so the shadow belongs on that neighbour.
        IReadOnlyList<int> cells = BattleCoverHighlight.GetCells(arena, attacker, target, CursorTheme.SwordRight);

        Assert.Equal([standing], cells);
    }

    /// <summary>A wide unit walking covers both halves of its body.</summary>
    [Fact]
    public void AWideUnitWalkingLightsBothHalves()
    {
        BattleArena arena = Arena(MonsterType.Cavalry);
        BattleUnit unit = arena.AttackingForce.First();

        Assert.True(unit.IsWide);

        int destination = ReachableEmptyCell(arena, unit);
        BattlePosition position = BattlePosition.GetReachable(arena, unit, destination);

        IReadOnlyList<int> cells = BattleCoverHighlight.GetCells(arena, unit, destination, CursorTheme.CombatMove);

        Assert.Equal([position.HeadIndex, position.TailIndex], cells);
    }

    /// <summary>A two-cell blow also lights the cell behind the target, which is the second one it hits.</summary>
    [Fact]
    public void ATwoCellBlowLightsTheCellBehindTheTarget()
    {
        BattleArena arena = Arena(MonsterType.Cyclops);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        Assert.True(attacker.IsDoubleCellAttack);

        int target = defender.HeadIndex;
        int standing = BattleBoard.GetIndexDirection(target, CellDirection.Left);

        attacker.SetPosition(standing);

        IReadOnlyList<int> cells = BattleCoverHighlight.GetCells(arena, attacker, target, CursorTheme.SwordRight);

        int behind = BattleBoard.GetIndexDirection(target, CellDirection.Right);

        Assert.Contains(standing, cells);
        Assert.Contains(behind, cells);
    }

    /// <summary>An all-round blow lights the neighbouring enemies, which the strike hits on the way past.</summary>
    [Fact]
    public void AnAllRoundBlowLightsTheNeighbouringEnemies()
    {
        BattleArena arena = Arena(MonsterType.Hydra);
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        Assert.True(attacker.IsAllAdjacentCellsAttack);

        const int target = 45;
        BattlePosition attackPosition = BattleBoard.CalculateAttackPosition(
            arena, attacker, target, AttackDirection.Left);

        Assert.False(attackPosition.IsEmpty);

        // A second enemy right beside the cell the hydra would stand on: the all-round blow covers it too.
        int neighbour = BattleBoard.GetAroundIndexes(attackPosition)
            .First(index => index != target && arena.Board.GetCell(index) is { Unit: null });

        defender.SetPosition(neighbour);

        IReadOnlyList<int> cells = BattleCoverHighlight.GetCells(arena, attacker, target, CursorTheme.SwordRight);

        Assert.Contains(neighbour, cells);
    }
}
