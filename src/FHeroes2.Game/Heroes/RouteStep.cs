using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// One step of a hero's route, <c>Route::Step</c> (<c>route.h:39-78</c>).
/// </summary>
/// <remarks>
/// <para>
/// The three indexes are stored, not derived: a step knows the tile it lands on, the tile it started
/// from and the direction between them, because a route is rebuilt from the pathfinder cache and later
/// replayed (and saved) step by step — the map may change in between, and a route that recomputed its
/// directions from the indexes would then describe a different walk.
/// </para>
/// <para>
/// <see cref="Penalty"/> is the cost of this single step, not of the whole route up to here: the hero
/// pays it as he walks, and the movement animation needs to know when the points of the current turn run
/// out — that is exactly the step whose penalty exceeds what is left.
/// </para>
/// </remarks>
/// <param name="Index">Index of the tile the step lands on, <c>currentIndex</c>.</param>
/// <param name="From">Index of the tile the step starts at, <c>from</c>.</param>
/// <param name="Direction">Direction of the step, <c>direction</c>.</param>
/// <param name="Penalty">Movement points this single step costs, <c>penalty</c>.</param>
public readonly record struct RouteStep(int Index, int From, Direction Direction, uint Penalty);
