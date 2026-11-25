using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.UI.Battle;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The cursor and the hint of a hover over the battlefield, <see cref="BattleCursor"/>, a port of
/// <c>Interface::GetBattleCursor()</c> (<c>battle_interface.cpp:2922-3064</c>) and
/// <c>getSwordCursorForAttackDirection()</c> (<c>:637-661</c>).
/// </summary>
public sealed class BattleCursorTests
{
    /// <summary>A field battle between one peasant on each side, so a stack and free cells are both at hand.</summary>
    private static BattleArena Arena()
    {
        static GameArmy Army(PlayerColor color)
        {
            GameArmy army = new()
            {
                Color = color,
            };

            army.GetTroop(0).Set(MonsterType.Peasant, 10);

            return army;
        }

        return new BattleArena(Army(PlayerColor.Blue), Army(PlayerColor.Red), new Pcg32(7));
    }

    /// <summary>The first stack on the board, in cell order.</summary>
    private static BattleUnit FirstUnit(BattleArena arena)
    {
        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (arena.Board.GetCell(index)?.Unit is { } unit)
            {
                return unit;
            }
        }

        throw new InvalidOperationException("The arena placed no unit.");
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

    /// <summary>
    /// A point inside the petal of the cell that faces a neighbour, so <see cref="BattleCell.GetTriangleDirection"/>
    /// answers that direction. Each point is the centre of mass of the triangle the reference tests for it
    /// (<c>battle_cell.cpp:279-317</c>).
    /// </summary>
    private static Point PetalPoint(Rect cell, CellDirection direction) => direction switch
    {
        CellDirection.Left => new Point(cell.X + 7, cell.Y + 26),
        CellDirection.Right => new Point(cell.X + 37, cell.Y + 26),
        CellDirection.TopLeft => new Point(cell.X + 11, cell.Y + 14),
        CellDirection.TopRight => new Point(cell.X + 33, cell.Y + 14),
        CellDirection.BottomLeft => new Point(cell.X + 11, cell.Y + 38),
        CellDirection.BottomRight => new Point(cell.X + 33, cell.Y + 38),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a neighbour direction."),
    };

    /// <summary>The sword of an attack direction is the one pointing the other way round.</summary>
    [Theory]
    [InlineData(AttackDirection.TopLeft, CursorTheme.SwordBottomRight)]
    [InlineData(AttackDirection.Top, CursorTheme.SwordBottom)]
    [InlineData(AttackDirection.TopRight, CursorTheme.SwordBottomLeft)]
    [InlineData(AttackDirection.Right, CursorTheme.SwordLeft)]
    [InlineData(AttackDirection.BottomRight, CursorTheme.SwordTopLeft)]
    [InlineData(AttackDirection.Bottom, CursorTheme.SwordTop)]
    [InlineData(AttackDirection.BottomLeft, CursorTheme.SwordTopRight)]
    [InlineData(AttackDirection.Left, CursorTheme.SwordRight)]
    public void SwordFor_MapsEveryAttackDirection(AttackDirection direction, CursorTheme expected) =>
        Assert.Equal(expected, BattleCursor.SwordFor(direction));

    /// <summary>The centre and the unknown have no sword: the reference's <c>default</c> returns <c>NONE</c>.</summary>
    [Theory]
    [InlineData(AttackDirection.Center)]
    [InlineData(AttackDirection.Unknown)]
    public void SwordFor_LeavesTheCentreWithoutASword(AttackDirection direction) =>
        Assert.Equal(CursorTheme.CombatNone, BattleCursor.SwordFor(direction));

    /// <summary>Reading a sword back into a direction is the inverse of painting the direction as a sword.</summary>
    [Theory]
    [InlineData(AttackDirection.TopLeft)]
    [InlineData(AttackDirection.Top)]
    [InlineData(AttackDirection.TopRight)]
    [InlineData(AttackDirection.Right)]
    [InlineData(AttackDirection.BottomRight)]
    [InlineData(AttackDirection.Bottom)]
    [InlineData(AttackDirection.BottomLeft)]
    [InlineData(AttackDirection.Left)]
    public void AttackDirectionOf_IsTheInverseOfSwordFor(AttackDirection direction) =>
        Assert.Equal(direction, BattleCursor.AttackDirectionOf(BattleCursor.SwordFor(direction)));

    /// <summary>A cursor that is not a sword names no side.</summary>
    [Theory]
    [InlineData(CursorTheme.CombatPointer)]
    [InlineData(CursorTheme.CombatMove)]
    [InlineData(CursorTheme.CombatArrow)]
    [InlineData(CursorTheme.CombatNone)]
    public void AttackDirectionOf_LeavesOtherCursorsUnknown(CursorTheme theme) =>
        Assert.Equal(AttackDirection.Unknown, BattleCursor.AttackDirectionOf(theme));

    /// <summary>A direction that can be struck from is kept as it is.</summary>
    [Fact]
    public void Nearest_KeepsAnAvailableDirection()
    {
        HashSet<AttackDirection> available = [AttackDirection.Right, AttackDirection.BottomLeft];

        Assert.Equal(AttackDirection.BottomLeft, BattleCursor.Nearest(AttackDirection.BottomLeft, available));
    }

    /// <summary>With one direction only there is nothing to choose, however far the pointer points.</summary>
    [Fact]
    public void Nearest_TakesTheOnlyCandidate()
    {
        HashSet<AttackDirection> available = [AttackDirection.Left];

        Assert.Equal(AttackDirection.Left, BattleCursor.Nearest(AttackDirection.TopLeft, available));
    }

    /// <summary>Two candidates at the same distance: the reference walks the ring clockwise first.</summary>
    [Fact]
    public void Nearest_PrefersTheClockwiseCandidate()
    {
        // From TOP_LEFT the ring runs TOP, TOP_RIGHT, …; LEFT is the way round the other side.
        HashSet<AttackDirection> available = [AttackDirection.Top, AttackDirection.Left];

        Assert.Equal(AttackDirection.Top, BattleCursor.Nearest(AttackDirection.TopLeft, available));
    }

    /// <summary>The same walk tries both sides every step, so the shorter way round wins.</summary>
    [Fact]
    public void Nearest_SlidesCounterClockwiseWhenItIsCloser()
    {
        HashSet<AttackDirection> available = [AttackDirection.Left];

        Assert.Equal(AttackDirection.Left, BattleCursor.Nearest(AttackDirection.TopLeft, available));
    }

    /// <summary>Without a direction to strike from the reference falls through to <c>WAR_NONE</c>.</summary>
    [Fact]
    public void Nearest_WithoutCandidatesReportsUnknown()
    {
        Assert.Equal(AttackDirection.Unknown, BattleCursor.Nearest(AttackDirection.Top, new HashSet<AttackDirection>()));
    }

    /// <summary>The cell a stack stands on is the «view» hover, <c>WAR_INFO</c>.</summary>
    [Fact]
    public void Describe_OnTheOwnCell_ShowsTheInfoCursor()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);

        BattleHover hover = BattleCursor.Describe(arena, unit, unit.HeadIndex, new Point(0, 0));

        Assert.Equal(CursorTheme.CombatInfo, hover.Theme);
        Assert.Equal("View peasants info", hover.Status);
    }

    /// <summary>A free cell the unit can walk onto asks to move there and names the walker.</summary>
    [Fact]
    public void Describe_OnAReachableCell_ShowsTheMoveCursor()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);
        int destination = ReachableEmptyCell(arena, unit);

        BattleHover hover = BattleCursor.Describe(arena, unit, destination, new Point(0, 0));

        Assert.Equal(CursorTheme.CombatMove, hover.Theme);
        Assert.Equal("Move peasant here", hover.Status);
    }

    /// <summary>
    /// An enemy the unit can strike asks for an attack, and the sword points at the side the blow comes from —
    /// the eight cursors differ only in which petal of the target cell the pointer rests on.
    /// </summary>
    [Fact]
    public void Describe_OnAnAdjacentEnemy_ShowsTheSwordOfThatSide()
    {
        BattleArena arena = Arena();
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        int adjacent = BattleBoard.GetAroundIndexes(defender.Position)
            .First(index => arena.Board.GetCell(index)?.IsPassable(false) == true);

        attacker.SetPosition(adjacent);

        CellDirection side = BattleBoard.GetDirection(defender.HeadIndex, adjacent);
        BattleCell cell = arena.Board.GetCell(defender.HeadIndex)!;
        AttackDirection expected = BattleCellDirections.AsAttackDirection(side);

        BattleHover hover = BattleCursor.Describe(arena, attacker, defender.HeadIndex, PetalPoint(cell.Position, side));

        Assert.Equal(BattleCursor.SwordFor(expected), hover.Theme);
        Assert.Equal("Attack peasant", hover.Status);
    }

    /// <summary>An enemy out of reach leaves the battlefield pointer and the round on the status line.</summary>
    [Fact]
    public void Describe_OnAnEnemyOutOfReach_ShowsNoCursorAndTheRound()
    {
        BattleArena arena = Arena();
        BattleUnit unit = FirstUnit(arena);
        BattleUnit enemy = arena.DefendingForce.First();

        Assert.Empty(BattleCursor.AvailableAttackDirections(arena, unit, enemy.HeadIndex));

        BattleHover hover = BattleCursor.Describe(arena, unit, enemy.HeadIndex, new Point(0, 0));

        Assert.Equal(CursorTheme.CombatNone, hover.Theme);
        Assert.StartsWith("Turn ", hover.Status, StringComparison.Ordinal);
    }

    /// <summary>
    /// A neighbour the unit could strike from is an available direction, and the set is described from the
    /// target's side: standing to the left of the defender makes <see cref="AttackDirection.Left"/> available.
    /// </summary>
    [Fact]
    public void AvailableAttackDirections_NamesTheSideTheBlowComesFrom()
    {
        BattleArena arena = Arena();
        BattleUnit attacker = arena.AttackingForce.First();
        BattleUnit defender = arena.DefendingForce.First();

        int adjacent = BattleBoard.GetAroundIndexes(defender.Position)
            .First(index => arena.Board.GetCell(index)?.IsPassable(false) == true);

        attacker.SetPosition(adjacent);

        HashSet<AttackDirection> available = BattleCursor.AvailableAttackDirections(arena, attacker, defender.HeadIndex);

        Assert.Contains(BattleCellDirections.AsAttackDirection(BattleBoard.GetDirection(defender.HeadIndex, adjacent)),
            available);
    }
}
