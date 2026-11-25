using FHeroes2.Core.Geometry;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Battle;

/// <summary>
/// One visual step of a march, the frame-by-frame form of <c>Battle::RedrawActionMove()</c> and
/// <c>Battle::RedrawActionFly()</c> (<c>battle_interface.cpp:4524-4830</c>).
/// </summary>
/// <param name="CellIndex">The cell the step walks onto or lands on.</param>
/// <param name="Animation">The monster animation the step plays.</param>
/// <param name="IsTurnOnly">
/// A wide unit turning on the spot: no animation, the sprite only flips
/// (<c>battle_interface.cpp:4612-4617</c>).
/// </param>
/// <param name="MovingPosition">
/// The sprite's target point while the step plays, the reference's <c>_movingPos</c>; <see langword="null"/>
/// for a turn.
/// </param>
/// <param name="FlyingPosition">
/// The point the flight left from, the reference's <c>_flyingPos</c>; set only while the flyer is in the air
/// (<c>battle_interface.cpp:4781-4813</c>).
/// </param>
public readonly record struct BattleMoveStep(
    int CellIndex,
    MonsterAnimationType Animation,
    bool IsTurnOnly,
    Point? MovingPosition,
    Point? FlyingPosition)
{
    /// <summary>A step that walks onto a cell.</summary>
    /// <param name="cellIndex">The cell being walked onto.</param>
    /// <param name="animation">The move animation of that cell.</param>
    /// <param name="movingPosition">The cell's sprite point.</param>
    /// <returns>The step.</returns>
    public static BattleMoveStep Walk(int cellIndex, MonsterAnimationType animation, Point movingPosition) =>
        new(cellIndex, animation, IsTurnOnly: false, movingPosition, FlyingPosition: null);

    /// <summary>A wide unit turning on the spot before the next cell.</summary>
    /// <param name="cellIndex">The cell whose tail the unit turns on.</param>
    /// <returns>The step.</returns>
    public static BattleMoveStep Turn(int cellIndex) =>
        new(cellIndex, MonsterAnimationType.Static, IsTurnOnly: true, MovingPosition: null, FlyingPosition: null);

    /// <summary>One step of a flight: lift-off, one stretch of air, or landing.</summary>
    /// <param name="cellIndex">The landing cell, the same for every step of the flight.</param>
    /// <param name="animation">The flight animation of the step.</param>
    /// <param name="movingPosition">The point the stretch heads for.</param>
    /// <param name="flyingPosition">The point the stretch left from, or <see langword="null"/> for lift-off and landing.</param>
    /// <returns>The step.</returns>
    public static BattleMoveStep Flight(int cellIndex, MonsterAnimationType animation, Point movingPosition, Point? flyingPosition) =>
        new(cellIndex, animation, IsTurnOnly: false, movingPosition, flyingPosition);
}

/// <summary>
/// What a unit about to move will play, worked out before the command is applied: the cells of a walk with the
/// animation of each, or the arc of a flight. Presentation only — the arena is not touched
/// (<c>battle_interface.cpp:4524-4830</c>).
/// </summary>
/// <remarks>
/// 参考是在 <c>Arena::ApplyActionMove()</c> 里一路阻塞着画完的；端口没有可以阻塞的事件循环，所以先算出
/// 「要播哪几步」，由 <see cref="BattleFieldPainter"/> 每帧推进，场景在播完之后才把命令交给竞技场。
/// </remarks>
public sealed class BattleMoveAnimation
{
    private readonly BattleMoveStep[] _steps;

    /// <summary>Creates a plan around a unit.</summary>
    /// <param name="unit">The unit that will move.</param>
    /// <param name="isFlight">Whether the plan is a flyer's arc rather than a walk.</param>
    /// <param name="steps">The steps in play order; at least one.</param>
    private BattleMoveAnimation(BattleUnit unit, bool isFlight, BattleMoveStep[] steps)
    {
        Unit = unit;
        IsFlight = isFlight;
        _steps = steps;
    }

    /// <summary>The unit that will move.</summary>
    public BattleUnit Unit { get; }

    /// <summary>Whether the plan is a flyer's arc, which is drawn above the whole battlefield.</summary>
    public bool IsFlight { get; }

    /// <summary>The steps in play order.</summary>
    public IReadOnlyList<BattleMoveStep> Steps => _steps;

    /// <summary>The cell the march ends on.</summary>
    public int Destination => _steps[^1].CellIndex;

    /// <summary>
    /// Works out how the unit would walk or fly to a cell, <c>RedrawActionMove()</c>/<c>RedrawActionFly()</c>
    /// (<c>battle_interface.cpp:4524-4830</c>).
    /// </summary>
    /// <param name="arena">The arena holding the board and the pathfinder.</param>
    /// <param name="unit">The unit that would move.</param>
    /// <param name="destination">The head cell it would stop on.</param>
    /// <param name="flightStep">
    /// The flyer's flight speed in pixels per stretch, <c>AnimationReference.FlightSpeed</c>; ignored for walkers.
    /// </param>
    /// <returns>The plan, or <see langword="null"/> when there is nothing to draw (the unit is already there).</returns>
    public static BattleMoveAnimation? Create(BattleArena arena, BattleUnit unit, int destination, uint flightStep)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(unit);

        if (!BattleBoard.IsValidIndex(destination))
        {
            return null;
        }

        BattleCell? cell = arena.Board.GetCell(destination);

        // The unit is already standing there: the reference returns before drawing anything
        // (battle_interface.cpp:4721-4723).
        if (cell is null || unit.Position.Contains(destination))
        {
            return null;
        }

        return unit.IsFlying
            ? CreateFlight(unit, destination, cell, flightStep)
            : CreateWalk(arena, unit, destination);
    }

    /// <summary>
    /// The flight's straight line split into stretches, the reference's <c>getLinePoints()</c>
    /// (<c>math_tools.cpp:47-74</c>).
    /// </summary>
    /// <param name="from">The point the unit lifts off from.</param>
    /// <param name="to">The point it lands on.</param>
    /// <param name="step">How many pixels one stretch covers.</param>
    /// <returns>The points of the line from <paramref name="from"/> to <paramref name="to"/>.</returns>
    public static IReadOnlyList<Point> GetLinePoints(Point from, Point to, uint step)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;

        uint intervals = step == 0
            ? 0
            : ((uint)(Math.Sqrt((dx * dx) + (dy * dy)) + 0.5) + step - 1) / step;

        if (intervals < 2)
        {
            return from == to ? [from] : [from, to];
        }

        double stepX = dx / intervals;
        double stepY = dy / intervals;

        List<Point> points = new((int)intervals + 1)
        {
            from,
        };

        double x = from.X;
        double y = from.Y;

        for (uint i = 0; i < intervals - 1; ++i)
        {
            x += stepX;
            y += stepY;
            points.Add(new Point((int)x, (int)y));
        }

        points.Add(to);

        return points;
    }

    /// <summary>Plans a walk, <c>RedrawActionMove()</c>.</summary>
    /// <param name="arena">The arena holding the board and the pathfinder.</param>
    /// <param name="unit">The walking unit.</param>
    /// <param name="destination">The cell it stops on.</param>
    /// <returns>The plan, or <see langword="null"/> when the path is empty.</returns>
    private static BattleMoveAnimation? CreateWalk(BattleArena arena, BattleUnit unit, int destination)
    {
        int[] path = arena.GetPath(unit, BattlePosition.GetReachable(arena, unit, destination));

        if (path.Length == 0)
        {
            return null;
        }

        // The reference's isOneStepPath: a wide unit's turn is one of the three cells of one step.
        bool isOneStepPath = path.Length == 1 || (unit.IsWide && path.Length == 3 && path[0] == unit.TailIndex);

        List<BattleMoveStep> steps = new(path.Length);

        for (int i = 0; i < path.Length; ++i)
        {
            int index = path[i];

            // A wide unit turns on the spot when the next cell is its own tail.
            if (unit.IsWide && index == unit.TailIndex)
            {
                steps.Add(BattleMoveStep.Turn(index));
                continue;
            }

            if (arena.Board.GetCell(index) is not { } cell)
            {
                continue;
            }

            MonsterAnimationType animation = isOneStepPath
                ? MonsterAnimationType.MoveQuick
                : i == 0
                    ? MonsterAnimationType.MoveStart
                    : i == path.Length - 1
                        ? MonsterAnimationType.MoveEnd
                        : MonsterAnimationType.Moving;

            steps.Add(BattleMoveStep.Walk(index, animation, new Point(cell.Position.X, cell.Position.Y)));
        }

        return steps.Count == 0 ? null : new BattleMoveAnimation(unit, isFlight: false, [.. steps]);
    }

    /// <summary>Plans a flight, <c>RedrawActionFly()</c>.</summary>
    /// <param name="unit">The flying unit.</param>
    /// <param name="destination">The cell it lands on.</param>
    /// <param name="cell">That cell.</param>
    /// <param name="flightStep">The flight speed in pixels per stretch.</param>
    /// <returns>The plan.</returns>
    private static BattleMoveAnimation CreateFlight(BattleUnit unit, int destination, BattleCell cell, uint flightStep)
    {
        Rect area = unit.Position.GetRect();
        Point from = new(area.X, area.Y);
        Point target = new(cell.Position.X, cell.Position.Y);

        // A wide flyer landing to the right is shifted by half a cell so the landing does not pop sideways
        // (battle_interface.cpp:4735-4737).
        if (unit.IsWide && target.X > from.X)
        {
            target = new Point(target.X - BattleCell.WidthPx, target.Y);
        }

        IReadOnlyList<Point> points = GetLinePoints(from, target, flightStep);

        List<BattleMoveStep> steps = new(points.Count + 1)
        {
            BattleMoveStep.Flight(destination, MonsterAnimationType.FlyUp, points[0], flyingPosition: null),
        };

        for (int i = 1; i < points.Count; ++i)
        {
            steps.Add(BattleMoveStep.Flight(destination, MonsterAnimationType.Moving, points[i], points[i - 1]));
        }

        steps.Add(BattleMoveStep.Flight(destination, MonsterAnimationType.FlyLand, target, flyingPosition: null));

        return new BattleMoveAnimation(unit, isFlight: true, [.. steps]);
    }
}
