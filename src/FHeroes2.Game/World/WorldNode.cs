namespace FHeroes2.Game;

/// <summary>
/// One entry of the pathfinder cache, <c>WorldNode</c> (<c>world_pathfinding.h:33</c>).
/// </summary>
/// <remarks>
/// <para>
/// The cache holds one node per map tile and is the whole state of a search: the reference is not a
/// textbook A* with a priority queue, it is a <b>label correcting</b> walk over the map — a tile is put
/// back on the queue every time a cheaper route to it turns up, so a node only ever holds the best
/// route found so far.
/// </para>
/// <para>
/// <see cref="From"/> doubles as the "not reached yet" flag. The start tile is the one node that keeps
/// <c>-1</c> while being reached (the search writes it explicitly), which is also why the cache has to
/// be filled with <c>{ From = -1 }</c> and not with the default value of the struct — <c>default</c>
/// would make every tile look like it was reached from tile 0.
/// </para>
/// </remarks>
internal struct WorldNode
{
    /// <summary>Index of the tile this node was reached from, <c>_from</c>; <c>-1</c> means "not reached".</summary>
    public int From;

    /// <summary>Movement points spent to get here from the start of the path, <c>_cost</c>.</summary>
    public uint Cost;

    /// <summary>
    /// Movement points a real hero would have left after this step, <c>_remainingMovePoints</c>.
    /// Zero when the search runs without a hero (the AI does that).
    /// </summary>
    public uint RemainingMovePoints;

    /// <summary>
    /// The AI's cached answer to "may I stand on this tile", <c>_isAccessibleForAI</c>
    /// (<c>world_pathfinding.h:52</c>).
    /// </summary>
    /// <remarks>
    /// The AI's passability rules are expensive — they build an <see cref="Army.Army"/> for the tile — and
    /// the same tile is asked about over and over during one search, so the answer is kept next to the
    /// route. <see langword="null"/> means "not asked yet"; it is cleared with the rest of the node, which
    /// is why the whole cache is refilled on every search.
    /// </remarks>
    public bool? IsAccessibleForAI;

    /// <summary>
    /// The AI's cached answer to "may I walk through this tile when arriving from water",
    /// <c>_isAvailableForWalkThroughForAI.fromWater</c> (<c>world_pathfinding.h:53</c>).
    /// </summary>
    public bool? IsAvailableForWalkThroughForAiFromWater;

    /// <summary>
    /// The AI's cached answer to "may I walk through this tile when arriving from land",
    /// <c>_isAvailableForWalkThroughForAI.fromLand</c> (<c>world_pathfinding.h:53</c>).
    /// </summary>
    /// <remarks>
    /// Two slots and not one: a boat can be entered from land but not sailed through, so the same tile
    /// answers differently depending on the side the hero comes from.
    /// </remarks>
    public bool? IsAvailableForWalkThroughForAiFromLand;

    /// <summary>
    /// The three fields at once, <c>WorldNode::update()</c>.
    /// </summary>
    /// <param name="from">Index of the tile the step came from.</param>
    /// <param name="cost">Movement points spent to get here.</param>
    /// <param name="remainingMovePoints">Movement points left after the step.</param>
    public void Update(int from, uint cost, uint remainingMovePoints)
    {
        From = from;
        Cost = cost;
        RemainingMovePoints = remainingMovePoints;
    }
}
