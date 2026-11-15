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
