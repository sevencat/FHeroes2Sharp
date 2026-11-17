using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Battle::Graveyard</c> (<c>battle_grave.h:36-57</c>): where the bodies lie and in which order.
/// </summary>
public class BattleGraveyardTests
{
    private static BattleUnit Spawn(
        MonsterType monster,
        uint count,
        int head,
        uint uid,
        bool wide = false,
        BattleBoardCells? board = null)
    {
        BattlePosition position = new(board ?? new BattleBoardCells(), head, wide, false);

        return new BattleUnit(new Troop(monster, count), position, false, uid);
    }

    [Fact]
    public void TheLastStackLiesOnTop()
    {
        BattleGraveyard graveyard = new();
        BattleUnit first = Spawn(MonsterType.Peasant, 1, 40, 1);
        BattleUnit second = Spawn(MonsterType.Peasant, 1, 40, 2);

        graveyard.AddUnit(first);
        Assert.Same(first, graveyard.GetLastUnit(40));

        graveyard.AddUnit(second);
        Assert.Same(second, graveyard.GetLastUnit(40));
        Assert.Equal([first, second], graveyard.GetUnits(40));
    }

    [Fact]
    public void AWideStackLiesOnBothOfItsCells()
    {
        BattleGraveyard graveyard = new();
        BattleUnit cavalry = Spawn(MonsterType.Cavalry, 1, 45, 1, wide: true);

        graveyard.AddUnit(cavalry);

        Assert.Same(cavalry, graveyard.GetLastUnit(45));
        Assert.Same(cavalry, graveyard.GetLastUnit(44));
        Assert.Equal([44, 45], graveyard.GetOccupiedCells());
    }

    [Fact]
    public void AnEmptyCellHasNoStackOnIt()
    {
        BattleGraveyard graveyard = new();

        Assert.Null(graveyard.GetLastUnit(40));
        Assert.Empty(graveyard.GetUnits(40));
        Assert.Empty(graveyard.GetOccupiedCells());
        Assert.Equal(0, graveyard.Count);
    }

    [Fact]
    public void RemovingAStackTakesItOffBothCells()
    {
        BattleGraveyard graveyard = new();
        BattleUnit cavalry = Spawn(MonsterType.Cavalry, 1, 45, 1, wide: true);

        graveyard.AddUnit(cavalry);
        graveyard.RemoveUnit(cavalry);

        Assert.Null(graveyard.GetLastUnit(45));
        Assert.Null(graveyard.GetLastUnit(44));
        Assert.Empty(graveyard.GetOccupiedCells());
    }

    [Fact]
    public void MovingAStackToTheLastKeepsTheOthersInOrder()
    {
        BattleGraveyard graveyard = new();
        BattleUnit bottom = Spawn(MonsterType.Peasant, 1, 40, 1);
        BattleUnit middle = Spawn(MonsterType.Peasant, 1, 40, 2);
        BattleUnit top = Spawn(MonsterType.Peasant, 1, 40, 3);

        graveyard.AddUnit(bottom);
        graveyard.AddUnit(middle);
        graveyard.AddUnit(top);

        graveyard.MoveToLastIfPresent(bottom);

        Assert.Equal([middle, top, bottom], graveyard.GetUnits(40));
        Assert.Same(bottom, graveyard.GetLastUnit(40));
    }

    [Fact]
    public void MovingAStackThatIsNotThereChangesNothing()
    {
        BattleGraveyard graveyard = new();
        BattleUnit lying = Spawn(MonsterType.Peasant, 1, 40, 1);
        BattleUnit alive = Spawn(MonsterType.Peasant, 1, 41, 2);

        graveyard.AddUnit(lying);
        graveyard.MoveToLastIfPresent(alive);

        Assert.Equal([lying], graveyard.GetUnits(40));
    }

    [Fact]
    public void OccupiedCellsComeOutInAscendingOrder()
    {
        BattleGraveyard graveyard = new();

        graveyard.AddUnit(Spawn(MonsterType.Peasant, 1, 70, 1));
        graveyard.AddUnit(Spawn(MonsterType.Peasant, 1, 12, 2));
        graveyard.AddUnit(Spawn(MonsterType.Peasant, 1, 45, 3));

        Assert.Equal([12, 45, 70], graveyard.GetOccupiedCells());
        Assert.Equal(3, graveyard.Count);
    }

    [Fact]
    public void DyingPutsTheBodyIntoTheGraveyard()
    {
        BattleGraveyard graveyard = new();
        BattleUnit unit = Spawn(MonsterType.Peasant, 5, 40, 1);
        BattleUnit enemy = Spawn(MonsterType.Peasant, 5, 20, 2);

        unit.Graveyard = graveyard;
        unit.ApplyDamage(enemy, 100, out _, out _);
        unit.PostKilledAction();

        Assert.Same(unit, graveyard.GetLastUnit(40));
        Assert.Null(unit.Position.Board.GetCell(40)!.Unit);
    }

    [Fact]
    public void MirrorImagesAndElementalsDoNotFillTheGraveyard()
    {
        BattleGraveyard graveyard = new();
        BattleUnit mirror = Spawn(MonsterType.Peasant, 5, 40, 1);
        BattleUnit elemental = Spawn(MonsterType.FireElement, 3, 20, 2);

        mirror.Graveyard = graveyard;
        elemental.Graveyard = graveyard;

        mirror.SetModes(BattleMonsterState.CapMirrorImage);
        mirror.PostKilledAction();
        elemental.ApplyDamage(Spawn(MonsterType.Peasant, 1, 10, 3), 100, out _, out _);
        elemental.PostKilledAction();

        Assert.Empty(graveyard.GetOccupiedCells());
    }
}
