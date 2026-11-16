using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The route a hero is about to walk, <c>Route::Path</c> (<c>route.h:120-215</c>,
/// <c>route.cpp:38-414</c>).
/// </summary>
/// <remarks>
/// <para>
/// The steps are a queue, not a stack: the hero always walks <see cref="FrontIndex"/>, and everything that
/// happens to a route — the animation popping steps, the mouse truncating it, the load restoring it —
/// works on the front. The index the hero stands on is not stored: it is <c>front.From</c>, which is also
/// how <see cref="IsValidForMovement"/> notices that the hero has moved since the route was built.
/// </para>
/// <para>
/// <b>Substitution.</b> The reference keeps a pointer to its hero and asks it for the movement points and
/// the tile index (<c>hasAllowedSteps</c>, <c>GetAllowedSteps</c>, <c>String</c>). The port has no hero
/// with movement points yet, so those two numbers are parameters. Everything else — the queue, the
/// teleport flag, the visibility flag — is the reference's.
/// </para>
/// <para>
/// Not ported: <c>GetIndexSprite</c> (a 260 line table of <c>ICN::ROUTE</c> sprite numbers, needs the
/// rendering side) and <c>String</c> (a debug helper that needs the object and direction names).
/// </para>
/// </remarks>
public sealed class HeroPath
{
    private readonly List<RouteStep> _steps = [];

    private bool _hide = true;

    /// <summary>Number of steps left in the route.</summary>
    public int Count => _steps.Count;

    /// <summary>Whether the route has run out.</summary>
    public bool IsEmpty => _steps.Count == 0;

    /// <summary>
    /// Whether the route is drawn on the map, <c>isShow()</c>. Heroes start with a hidden route: a route
    /// is a thing the player asked for, not a thing a hero has by existing.
    /// </summary>
    public bool IsShow => !_hide;

    /// <summary>
    /// The steps of the route, in walking order. Handed out for reading only — the route has to be
    /// changed through its methods, or the invariants of the queue (a step starts where the previous one
    /// ended) stop holding.
    /// </summary>
    public IReadOnlyList<RouteStep> Steps => _steps;

    /// <summary>
    /// Index of the last step of the route, <c>GetDestinationIndex()</c>; <c>-1</c> when the route is
    /// empty.
    /// </summary>
    public int DestinationIndex => IsEmpty ? -1 : _steps[^1].Index;

    /// <summary>Index of the tile the front step lands on, <c>GetFrontIndex()</c>; <c>-1</c> when empty.</summary>
    public int FrontIndex => IsEmpty ? -1 : _steps[0].Index;

    /// <summary>Index of the tile the front step starts at, <c>GetFrontFrom()</c>; <c>-1</c> when empty.</summary>
    public int FrontFrom => IsEmpty ? -1 : _steps[0].From;

    /// <summary>Direction of the front step, <c>GetFrontDirection()</c>; unknown when empty.</summary>
    public Direction FrontDirection => IsEmpty ? Direction.Unknown : _steps[0].Direction;

    /// <summary>Cost of the front step, <c>GetFrontPenalty()</c>; zero when empty.</summary>
    public uint FrontPenalty => IsEmpty ? 0 : _steps[0].Penalty;

    /// <summary>
    /// Direction of the step after the front one, <c>GetNextStepDirection()</c>; unknown when the route has
    /// fewer than two steps.
    /// </summary>
    public Direction NextStepDirection => _steps.Count < 2 ? Direction.Unknown : _steps[1].Direction;

    /// <summary>
    /// Replaces the route, <c>setPath()</c>.
    /// </summary>
    /// <param name="path">The steps to walk, in order.</param>
    public void SetPath(IReadOnlyList<RouteStep> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        _steps.Clear();
        _steps.AddRange(path);
    }

    /// <summary>Empties the route, <c>Reset()</c>.</summary>
    public void Reset() => _steps.Clear();

    /// <summary>
    /// Keeps only the front step, <c>Truncate()</c>. The mouse uses it while it drags a route around: the
    /// hero is going to take one step and the rest of the old route is a different question.
    /// </summary>
    public void Truncate()
    {
        if (_steps.Count > 1)
        {
            _steps.RemoveRange(1, _steps.Count - 1);
        }
    }

    /// <summary>
    /// Drops the front step, <c>PopFront()</c>. This is what finishing a step of the walk animation does;
    /// a route that runs out of steps is a hero who has arrived.
    /// </summary>
    public void PopFront()
    {
        if (!IsEmpty)
        {
            _steps.RemoveAt(0);
        }
    }

    /// <summary>Makes the route visible, <c>Show()</c>.</summary>
    public void Show() => _hide = false;

    /// <summary>Makes the route invisible, <c>Hide()</c>.</summary>
    public void Hide() => _hide = true;

    /// <summary>
    /// Whether the front step is a normal step to an adjacent tile, <c>isValidForMovement()</c>.
    /// </summary>
    /// <remarks>
    /// Validity is about the step, not about the hero: a step with a direction and a tile on the map can be
    /// walked. Whether the hero is still standing where the step starts is
    /// <see cref="IsValidForMovement(int, int)"/>'s question.
    /// </remarks>
    /// <param name="mapSize">Number of tiles of the map, <c>world.Size</c>.</param>
    /// <returns><see langword="true"/> when the front step can be walked.</returns>
    public bool IsValidForMovement(int mapSize) =>
        !IsEmpty && FrontDirection != Direction.Unknown && FrontIndex >= 0 && FrontIndex < mapSize;

    /// <summary>
    /// Whether the front step is a teleport jump, <c>isValidForTeleportation()</c>.
    /// </summary>
    /// <remarks>
    /// The two are distinguished by the direction being unknown: a teleport moves the hero to a tile that is
    /// not adjacent, so there is no direction to name. Both still ask for a valid tile, which is what keeps
    /// a route restored from a save from pointing off the map.
    /// </remarks>
    /// <param name="mapSize">Number of tiles of the map, <c>world.Size</c>.</param>
    /// <returns><see langword="true"/> when the front step is a teleport.</returns>
    public bool IsValidForTeleportation(int mapSize) =>
        !IsEmpty && FrontDirection == Direction.Unknown && FrontIndex >= 0 && FrontIndex < mapSize;

    /// <summary>
    /// Whether the front step starts where the hero stands and whether he can pay for it, the whole test
    /// a cached route has to pass before it is walked, <c>isValidForMovement() &amp;&amp; hasAllowedSteps()</c>.
    /// </summary>
    /// <param name="heroIndex">Index of the tile the hero stands on.</param>
    /// <param name="movePoints">Movement points the hero has left.</param>
    /// <returns><see langword="true"/> when the route can be walked from where the hero is.</returns>
    public bool IsWalkableFrom(int heroIndex, uint movePoints) => FrontFrom == heroIndex && HasAllowedSteps(movePoints);

    /// <summary>
    /// Whether the hero can pay for the front step, <c>hasAllowedSteps()</c>.
    /// </summary>
    /// <param name="movePoints">Movement points the hero has left.</param>
    /// <returns><see langword="true"/> when the step is affordable.</returns>
    public bool HasAllowedSteps(uint movePoints) => !IsEmpty && movePoints >= FrontPenalty;

    /// <summary>
    /// How many steps of the route the hero can pay for, <c>GetAllowedSteps()</c>.
    /// </summary>
    /// <remarks>
    /// The count stops at the first step the hero cannot pay for in full — the "last move" rule of the
    /// pathfinder does not apply here, because this is what the interface colors green, and a green step
    /// is one the hero will really take before the day ends.
    /// </remarks>
    /// <param name="movePoints">Movement points the hero has left.</param>
    /// <returns>Number of affordable steps from the front of the route.</returns>
    public int GetAllowedSteps(uint movePoints)
    {
        int allowedSteps = 0;

        foreach (RouteStep step in _steps)
        {
            if (movePoints == 0)
            {
                break;
            }

            if (movePoints >= step.Penalty)
            {
                movePoints -= step.Penalty;
                ++allowedSteps;
            }
            else
            {
                movePoints = 0;
            }
        }

        return allowedSteps;
    }

    /// <summary>
    /// The sprite of one arrow of the route preview, <c>Route::Path::GetIndexSprite()</c>
    /// (<c>route.cpp:53-319</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ICN::ROUTE</c> holds six blocks of twenty-four arrows, one block per size: the hero has 100, 125,
    /// 150, 175 or 200 movement points and the preview shows how far he can still go. The block starts at
    /// 1, 25, 49, 73, 97 and 121; anything else — a shorter day, a modified game — uses the smallest block.
    /// </para>
    /// <para>
    /// Inside a block the arrow is picked by the pair (where the step comes from, where it goes): seven of the
    /// eight neighbours are drawn, the one pointing straight back at the previous step is not — that is the
    /// <c>index = 0</c> of the reference, and the caller then draws nothing. The eight rows below are the
    /// reference's nested switches, one row per "from", with <c>-1</c> for the missing arrow.
    /// </para>
    /// <para>
    /// The table is not symmetric: an arrow leaving a tile to the north-east and one arriving from it are
    /// different sprites, and the reference stores both.
    /// </para>
    /// </remarks>
    /// <param name="from">The direction the step comes from.</param>
    /// <param name="to">The direction the step goes to.</param>
    /// <param name="cost">The cost of the step, which picks the block.</param>
    /// <returns>The sprite index inside <c>ICN::ROUTE</c>, or zero when there is no such arrow.</returns>
    public static int GetIndexSprite(Direction from, Direction to, int cost)
    {
        int baseIndex = cost switch
        {
            200 => 121,
            175 => 97,
            150 => 73,
            125 => 49,
            100 => 25,
            _ => 1
        };

        int row = DirectionIndex(from);
        int column = DirectionIndex(to);

        if (row < 0 || column < 0)
        {
            return 0;
        }

        int offset = ArrowOffsets[row, column];

        return offset < 0 ? 0 : baseIndex + offset;
    }

    /// <summary>
    /// The arrow offsets of <c>route.cpp:80-316</c>, rows and columns in <see cref="DirectionIndex"/> order.
    /// </summary>
    private static readonly sbyte[,] ArrowOffsets =
    {
        //                     TL   T   TR   R   BR   B   BL   L
        /* TopLeft */        { 15, 16, 17, 18, -1,  4,  5,  6 },
        /* Top */            {  7,  8, 17, 18, 19, -1,  5,  6 },
        /* TopRight */       {  7,  0,  9, 18, 19, 20, -1,  6 },
        /* Right */          {  7,  0,  1, 10, 19, 20, 21, -1 },
        /* BottomRight */    { -1,  0,  1,  2, 11, 20, 21, 22 },
        /* Bottom */         { 16, -1,  1,  2,  3, 12, 21, 22 },
        /* BottomLeft */     { 23, 16, -1,  2,  3,  4, 13, 22 },
        /* Left */           { 23, 16, 17, -1,  3,  4,  5, 14 },
    };

    /// <summary>
    /// The position of a direction in <see cref="ArrowOffsets"/>, clockwise from the top left.
    /// </summary>
    /// <param name="direction">One of the eight neighbouring directions.</param>
    /// <returns>The index, or <c>-1</c> for anything else.</returns>
    private static int DirectionIndex(Direction direction) => direction switch
    {
        Direction.TopLeft => 0,
        Direction.Top => 1,
        Direction.TopRight => 2,
        Direction.Right => 3,
        Direction.BottomRight => 4,
        Direction.Bottom => 5,
        Direction.BottomLeft => 6,
        Direction.Left => 7,
        _ => -1
    };

    /// <summary>
    /// The cost of the whole route, <c>Route::calculatePathPenalty()</c>.
    /// </summary>
    /// <returns>Movement points the route costs.</returns>
    public uint CalculatePenalty() => CalculatePenalty(_steps);

    /// <summary>
    /// The cost of a route, <c>Route::calculatePathPenalty()</c> (<c>route.cpp:411</c>).
    /// </summary>
    /// <param name="path">The steps to add up.</param>
    /// <returns>Movement points the route costs.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static uint CalculatePenalty(IEnumerable<RouteStep> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        uint total = 0;

        foreach (RouteStep step in path)
        {
            total += step.Penalty;
        }

        return total;
    }
}
