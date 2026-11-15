using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game;

/// <summary>
/// The map wide movement search, <c>WorldPathfinder</c> (<c>world_pathfinding.h:40-200</c>,
/// <c>world_pathfinding.cpp:325-441</c>).
/// </summary>
/// <remarks>
/// <para>
/// One search fills a cache with one node per tile: the cost of the cheapest route from the start tile
/// and the tile that route came from. Building a path is then a walk backwards through the
/// <c>from</c> links, which is why <see cref="WorldNode"/> has to survive between the search and the
/// path building.
/// </para>
/// <para>
/// The search is label correcting, not A*: <see cref="CheckAdjacentNodes"/> relaxes the neighbours of a
/// tile and pushes a neighbour onto the queue again whenever a cheaper route to it appears. The queue is
/// a plain list walked by index and <b>appended to while it is being walked</b> — replacing it with a
/// <c>Queue</c> or a priority queue would change the order of the relaxations, and with it the paths
/// that come out equal in cost but differ in shape.
/// </para>
/// <para>
/// The three subclasses of the reference differ only in three places: which directions are allowed
/// (<see cref="IsMovementAllowed"/>), what a step costs (<see cref="GetMovementPenalty"/>), and what a
/// tile does after it has been reached (<see cref="ProcessCurrentNode"/>). The base class keeps the
/// rest: the cache, the queue, the neighbour walk and the "last move" rule.
/// </para>
/// <para>
/// The reference reads the global <c>world</c>; the port takes the map in the constructor and holds it,
/// so two worlds (a map and a test map) never share a search.
/// </para>
/// </remarks>
public abstract class WorldPathfinder
{
    /// <summary>The value the cache is cleared with: a node that was never reached.</summary>
    private static readonly WorldNode Unvisited = new() { From = -1 };

    private readonly World _map;
    private readonly MapGeometry _geometry;

    private WorldNode[] _cache = [];

    /// <summary>
    /// Per direction delta added to a tile index, <c>_mapOffset</c>. The reference computes it once from
    /// tile 0 and then adds it to the current tile instead of calling <c>Maps::GetDirectionIndex</c> per
    /// neighbour; the deltas are the same for every row, so the shortcut is exact.
    /// </summary>
    private int[] _mapOffset = [];

    private int _pathStart = -1;
    private PlayerColor _color = PlayerColor.None;
    private uint _remainingMovePoints;
    private SkillLevel _pathfindingSkill = SkillLevel.Expert;

    /// <summary>
    /// Creates a pathfinder for <paramref name="map"/> and allocates its cache.
    /// </summary>
    /// <param name="map">The world to search; it has to hold a loaded map already.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The world holds no map.</exception>
    protected WorldPathfinder(World map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (map.Size == 0)
        {
            throw new ArgumentException("A pathfinder can only be created for a loaded map.", nameof(map));
        }

        _map = map;
        _geometry = new MapGeometry(map.Width, map.Height);

        EnsureCache();
    }

    /// <summary>The world this pathfinder searches.</summary>
    protected World Map => _map;

    /// <summary>
    /// The dimensions of the map, for the helpers of <see cref="MapGeometry"/> that take a tile index
    /// and a direction.
    /// </summary>
    protected MapGeometry Geometry => _geometry;

    /// <summary>Index of the tile the current search started at; <c>-1</c> before the first search.</summary>
    protected int PathStart => _pathStart;

    /// <summary>Color of the side that walks the path, <c>PlayerColor::NONE</c> before the first search.</summary>
    protected PlayerColor PathColor => _color;

    /// <summary>Movement points the hero had when the search started.</summary>
    protected uint PathRemainingMovePoints => _remainingMovePoints;

    /// <summary>Level of the pathfinding skill the search charges the terrain with.</summary>
    protected SkillLevel PathfindingSkill => _pathfindingSkill;

    /// <summary>
    /// The cost of the cheapest route to a tile found by the last search, <c>getDistance()</c>. Zero
    /// means "no route" — a route always costs something, because the goal is at least one step away.
    /// </summary>
    /// <param name="targetIndex">Index of the tile to ask about.</param>
    /// <returns>Movement points from the start of the path, or zero when it cannot be reached.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the map.</exception>
    public uint GetDistance(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _cache.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(targetIndex), targetIndex, "The index is outside the map.");
        }

        return _cache[targetIndex].Cost;
    }

    /// <summary>
    /// Puts the search back into its "nothing has been searched" state, <c>reset()</c>.
    /// </summary>
    /// <remarks>
    /// The cache itself is kept: it is a tile per map tile and the next search overwrites it. Only its
    /// size can be wrong, namely when the world has loaded another map since — that is what
    /// <see cref="EnsureCache"/> fixes.
    /// </remarks>
    public virtual void Reset()
    {
        EnsureCache();

        _pathStart = -1;
        _color = PlayerColor.None;
        _remainingMovePoints = 0;
        _pathfindingSkill = SkillLevel.Expert;
    }

    /// <summary>
    /// Sets what the next search is about, the four scalars the subclasses share.
    /// </summary>
    /// <param name="startIndex">Index of the tile the hero stands on.</param>
    /// <param name="color">Color of the side that walks the path.</param>
    /// <param name="remainingMovePoints">Movement points the hero has left.</param>
    /// <param name="pathfindingSkill">Level of the hero's pathfinding skill.</param>
    /// <exception cref="ArgumentOutOfRangeException">The start index is outside the map.</exception>
    protected void SetSearchContext(int startIndex, PlayerColor color, uint remainingMovePoints, SkillLevel pathfindingSkill)
    {
        if (startIndex < 0 || startIndex >= _map.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "The index is outside the map.");
        }

        _pathStart = startIndex;
        _color = color;
        _remainingMovePoints = remainingMovePoints;
        _pathfindingSkill = pathfindingSkill;
    }

    /// <summary>
    /// Fills the cache from the current start tile, <c>processWorldMap()</c>.
    /// </summary>
    /// <remarks>
    /// The whole cache is cleared first, so two searches never mix. The start node is the only one whose
    /// <c>from</c> stays at <c>-1</c> while being reached — that is the sentinel the backwards walk of
    /// <c>buildPath()</c> stops at.
    /// </remarks>
    protected void ProcessWorldMap()
    {
        Debug.Assert(_cache.Length == _map.Size, "The cache does not belong to this map.");
        Debug.Assert(_pathStart >= 0 && _pathStart < _map.Size, "The search has no valid start tile.");

        Array.Fill(_cache, Unvisited);

        _cache[_pathStart].Update(-1, 0, _remainingMovePoints);

        List<int> nodesToExplore = [_pathStart];

        // The list grows while it is walked: every improved neighbour is appended and will be processed
        // after the ones already queued. See the class remarks — this order is part of the result.
        for (int lastProcessedNode = 0; lastProcessedNode < nodesToExplore.Count; ++lastProcessedNode)
        {
            ProcessCurrentNode(nodesToExplore, nodesToExplore[lastProcessedNode]);
        }
    }

    /// <summary>
    /// Relaxes the eight neighbours of one tile, <c>checkAdjacentNodes()</c>.
    /// </summary>
    /// <param name="nodesToExplore">The queue being walked; improved neighbours are appended to it.</param>
    /// <param name="currentNodeIdx">Index of the tile whose neighbours are taken.</param>
    protected void CheckAdjacentNodes(List<int> nodesToExplore, int currentNodeIdx)
    {
        WorldNode currentNode = _cache[currentNodeIdx];

        // A hero on a boat pays for water, a hero on foot for the ground he stands on; that is the only
        // thing the penalty of a step depends on as far as the "last move" rule is concerned.
        uint maxMovePoints = GetMaxMovePoints(_map.GetTile(currentNodeIdx).IsWater);

        for (int i = 0; i < Directions.AllNeighboringDirections.Length; ++i)
        {
            Direction direction = Directions.AllNeighboringDirections[i];

            if (!_geometry.IsValidDirection(currentNodeIdx, direction) || !IsMovementAllowed(currentNodeIdx, direction))
            {
                continue;
            }

            int newIndex = currentNodeIdx + _mapOffset[i];

            // Walking back to the start would make a path that returns where it came from.
            if (newIndex == _pathStart)
            {
                continue;
            }

            uint movementPenalty = GetMovementPenalty(currentNodeIdx, newIndex, direction);
            uint movementCost = currentNode.Cost + movementPenalty;

            if (_cache[newIndex].From == -1 || _cache[newIndex].Cost > movementCost)
            {
                _cache[newIndex].Update(
                    currentNodeIdx,
                    movementCost,
                    SubtractMovePoints(currentNode.RemainingMovePoints, movementPenalty, maxMovePoints));

                nodesToExplore.Add(newIndex);
            }
        }
    }

    /// <summary>
    /// Whether a hero of the side being searched for may take one step, <c>isMovementAllowed()</c>.
    /// </summary>
    /// <param name="from">Index of the tile the step starts at.</param>
    /// <param name="direction">Direction of the step.</param>
    /// <returns><see langword="true"/> when the step is allowed.</returns>
    protected virtual bool IsMovementAllowed(int from, Direction direction) =>
        IsMovementAllowedForColor(from, direction, _color, ignoreFog: false, isSummonBoatSpellAvailable: false);

    /// <summary>
    /// Whether a hero of <paramref name="color"/> may take one step, <c>isMovementAllowedForColor()</c>
    /// (<c>world_pathfinding.cpp:220</c>).
    /// </summary>
    /// <remarks>
    /// Two questions, in this order. First the boat rule: a boat cannot sail through a corner formed by
    /// land, so a diagonal step is refused when the tile behind the corner is water but at least one of
    /// the two tiles the boat would squeeze past is not. Note that the test asks about the tile
    /// <b>behind</b> the corner and not about the destination — the destination's own passability is the
    /// second question and is not duplicated here.
    /// </remarks>
    /// <param name="from">Index of the tile the step starts at.</param>
    /// <param name="direction">Direction of the step.</param>
    /// <param name="color">Color of the side that walks the path.</param>
    /// <param name="ignoreFog">Whether a tile still under the fog counts as known.</param>
    /// <param name="isSummonBoatSpellAvailable">Whether the hero may reach a water tile with Summon Boat.</param>
    /// <returns><see langword="true"/> when the step is allowed.</returns>
    protected bool IsMovementAllowedForColor(int from, Direction direction, PlayerColor color, bool ignoreFog, bool isSummonBoatSpellAvailable)
    {
        Tile fromTile = _map.GetTile(from);
        bool fromWater = fromTile.IsWater;

        if (fromWater && !CanSailAroundCorners(from, direction))
        {
            return false;
        }

        if (!fromTile.IsPassableTo(direction))
        {
            return false;
        }

        Tile toTile = _map.GetTile(_geometry.GetDirectionIndex(from, direction));

        if (toTile.IsPassableFrom(_map, Directions.Reflect(direction), fromWater, ignoreFog, color))
        {
            return true;
        }

        // Reaching a water tile with no boat on it is what Summon Boat is for: the spell may only be cast
        // from the shore, so the step has to start on land, and the tile has to be reachable as if the
        // shore tile were water.
        if (!isSummonBoatSpellAvailable)
        {
            return false;
        }

        if (fromWater || !toTile.IsSuitableForSummoningBoat())
        {
            return false;
        }

        return toTile.IsPassableFrom(_map, Directions.Reflect(direction), fromWater: true, ignoreFog, color);
    }

    /// <summary>
    /// Whether a boat standing on <paramref name="from"/> can take a diagonal step, the corner rule of
    /// <c>isMovementAllowedForColor()</c>.
    /// </summary>
    /// <remarks>
    /// A straight step is always fine here: only the four diagonals can squeeze through a gap of one
    /// tile. The rule is the reference's, including the shape of the condition — the tile behind the
    /// corner must be water, and the two tiles the boat passes must not both be water, otherwise the
    /// boat would sail over land.
    /// </remarks>
    /// <param name="from">Index of the tile the boat stands on.</param>
    /// <param name="direction">Direction of the step; only diagonals are examined.</param>
    /// <returns><see langword="false"/> when the corner blocks the step.</returns>
    private bool CanSailAroundCorners(int from, Direction direction)
    {
        int width = _geometry.Width;

        switch (direction)
        {
        case Direction.TopLeft:
            Debug.Assert(from >= width + 1, "The direction is not valid for this tile.");
            return !(_map.GetTile(from - width - 1).IsWater
                     && (!_map.GetTile(from - 1).IsWater || !_map.GetTile(from - width).IsWater));

        case Direction.TopRight:
            Debug.Assert(from >= width && from + 1 < _map.Size, "The direction is not valid for this tile.");
            return !(_map.GetTile(from - width + 1).IsWater
                     && (!_map.GetTile(from + 1).IsWater || !_map.GetTile(from - width).IsWater));

        case Direction.BottomRight:
            Debug.Assert(from + width + 1 < _map.Size, "The direction is not valid for this tile.");
            return !(_map.GetTile(from + width + 1).IsWater
                     && (!_map.GetTile(from + 1).IsWater || !_map.GetTile(from + width).IsWater));

        case Direction.BottomLeft:
            Debug.Assert(from >= 1 && from + width - 1 < _map.Size, "The direction is not valid for this tile.");
            return !(_map.GetTile(from + width - 1).IsWater
                     && (!_map.GetTile(from - 1).IsWater || !_map.GetTile(from + width).IsWater));

        default:
            return true;
        }
    }

    /// <summary>
    /// What a step costs, <c>getMovementPenalty()</c> (<c>world_pathfinding.cpp:332</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three rules, in this order: a step from a road tile to a road tile is charged as a road, the
    /// terrain penalty of the <b>tile the step starts from</b> otherwise (not the destination's), and a
    /// diagonal step costs half as much again. The road test needs both ends, the terrain test needs
    /// only the start.
    /// </para>
    /// <para>
    /// Then the "last move" rule of a real hero: if the hero has enough movement points to walk over the
    /// tile he stands on but not enough to reach the next one, he may still take the step and spend
    /// <b>all</b> his remaining points. That is how a hero with a handful of points left can enter a
    /// desert tile instead of stopping one tile short.
    /// </para>
    /// </remarks>
    /// <param name="from">Index of the tile the step starts at.</param>
    /// <param name="to">Index of the destination tile.</param>
    /// <param name="direction">Direction of the step.</param>
    /// <returns>Movement points the step costs, in hundredths.</returns>
    protected virtual uint GetMovementPenalty(int from, int to, Direction direction)
    {
        Tile fromTile = _map.GetTile(from);
        Tile toTile = _map.GetTile(to);

        uint penalty = fromTile.IsRoad && toTile.IsRoad
            ? MapGrounds.RoadPenalty
            : MapGrounds.GetPenalty(fromTile.Ground, _pathfindingSkill);

        if (Directions.IsDiagonal(direction))
        {
            penalty = penalty * 3 / 2;
        }

        // The "last move" rule only exists for a hero walking the map: the AI pathfinder reports zero
        // movement points and must not get the discount.
        if (GetMaxMovePoints(fromTile.IsWater) > 0)
        {
            WorldNode node = _cache[from];

            Debug.Assert(from == _pathStart || node.From != -1, "The search reached a dead end.");

            uint remainingMovePoints = node.RemainingMovePoints;
            uint fromTilePenalty = fromTile.IsRoad
                ? MapGrounds.RoadPenalty
                : MapGrounds.GetPenalty(fromTile.Ground, _pathfindingSkill);

            if (remainingMovePoints >= fromTilePenalty && remainingMovePoints < penalty)
            {
                return remainingMovePoints;
            }
        }

        return penalty;
    }

    /// <summary>
    /// What a tile is worth once it has been reached, <c>processCurrentNode()</c>.
    /// </summary>
    /// <param name="nodesToExplore">The queue being walked; new tiles are appended to it.</param>
    /// <param name="currentNodeIdx">Index of the tile that was just reached.</param>
    protected abstract void ProcessCurrentNode(List<int> nodesToExplore, int currentNodeIdx);

    /// <summary>
    /// How many movement points a hero has on the given kind of tile, <c>getMaxMovePoints()</c>.
    /// </summary>
    /// <param name="onWater">Whether the hero is on a boat.</param>
    /// <returns>The maximal number of movement points, or zero when no real hero is searched for.</returns>
    protected abstract uint GetMaxMovePoints(bool onWater);

    /// <summary>
    /// The cost of the route to <paramref name="index"/>, or zero when no route was found.
    /// </summary>
    /// <param name="index">Index of a tile of the map.</param>
    /// <returns>Movement points spent to get there.</returns>
    protected uint GetNodeCost(int index) => _cache[index].Cost;

    /// <summary>The tile <paramref name="index"/> was reached from, or <c>-1</c>.</summary>
    /// <param name="index">Index of a tile of the map.</param>
    /// <returns>Index of the previous tile of the route.</returns>
    protected int GetNodeFrom(int index) => _cache[index].From;

    /// <summary>Whether a tile was reached by the last search.</summary>
    /// <param name="index">Index of a tile of the map.</param>
    /// <returns><see langword="true"/> when the tile has a route.</returns>
    protected bool IsNodeReached(int index) => _cache[index].From != -1;

    /// <summary>Overwrites one node of the cache, <c>WorldNode::update()</c>.</summary>
    /// <param name="index">Index of the tile to write.</param>
    /// <param name="from">Index of the tile the step came from.</param>
    /// <param name="cost">Movement points spent to get there.</param>
    /// <param name="remainingMovePoints">Movement points left after the step.</param>
    protected void UpdateNode(int index, int from, uint cost, uint remainingMovePoints) =>
        _cache[index].Update(from, cost, remainingMovePoints);

    /// <summary>Movement points left after a step at <paramref name="index"/>.</summary>
    /// <param name="index">Index of a tile of the map.</param>
    /// <returns>The remaining movement points recorded for the route to the tile.</returns>
    protected uint GetNodeRemainingMovePoints(int index) => _cache[index].RemainingMovePoints;

    /// <summary>
    /// Whether a hero may step <b>onto</b> a tile on his way somewhere else,
    /// <c>isTileAvailableForWalkThrough()</c> (<c>world_pathfinding.cpp:58</c>).
    /// </summary>
    /// <remarks>
    /// The difference between this and the passability mask is the difference between "the ground allows
    /// it" and "the game allows it": heroes, monsters and boats block a tile even though the ground under
    /// them is walkable, pickup objects and action objects are refused because interacting with them is
    /// what the hero should do instead of walking on, and the last line keeps a walker on land on land
    /// and a boat on water.
    /// </remarks>
    /// <param name="map">The world the tile belongs to.</param>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <param name="fromWater">Whether the hero arrives on a boat.</param>
    /// <returns><see langword="true"/> when the hero may pass through the tile.</returns>
    protected static bool IsTileAvailableForWalkThrough(World map, int tileIndex, bool fromWater)
    {
        Tile tile = map.GetTile(tileIndex);
        Mp2ObjectType objectType = tile.MainObjectType;

        if (objectType is Mp2ObjectType.ActionHero or Mp2ObjectType.Monster or Mp2ObjectType.ActionBoat)
        {
            return false;
        }

        if (Mp2ObjectTypes.IsPickupObject(objectType) || Mp2ObjectTypes.IsInGameActionObject(objectType, fromWater))
        {
            return false;
        }

        return fromWater == tile.IsWater;
    }

    /// <summary>
    /// Movement points left after a step, <c>subtractMovePoints()</c>
    /// (<c>world_pathfinding.cpp:305</c>).
    /// </summary>
    /// <remarks>
    /// The two branches are two different turns. When the step costs more than the hero has left, the
    /// next turn has begun — the hero spends it from a full tank, and the leftovers of the previous turn
    /// are not carried over. Otherwise it is the same turn and the cost comes off what is left.
    /// </remarks>
    /// <param name="movePoints">Movement points the hero had before the step.</param>
    /// <param name="subtractedMovePoints">Cost of the step.</param>
    /// <param name="maxMovePoints">Movement points of a full turn, zero without a real hero.</param>
    /// <returns>Movement points after the step.</returns>
    protected static uint SubtractMovePoints(uint movePoints, uint subtractedMovePoints, uint maxMovePoints)
    {
        if (maxMovePoints == 0)
        {
            return 0;
        }

        if (movePoints < subtractedMovePoints)
        {
            Debug.Assert(maxMovePoints >= subtractedMovePoints, "A single step costs more than a whole turn.");
            return maxMovePoints - subtractedMovePoints;
        }

        return movePoints - subtractedMovePoints;
    }

    /// <summary>
    /// Makes the cache and the direction offsets match the map the pathfinder currently points at.
    /// </summary>
    private void EnsureCache()
    {
        if (_cache.Length == _map.Size)
        {
            return;
        }

        // The reference asserts a square map here and the port deliberately does not: the offsets are
        // row deltas, they are the same for every row of any shape, and every use is guarded by
        // IsValidDirection first. A rectangle would otherwise trip an assertion over a shortcut that is
        // in fact exact.
        _cache = new WorldNode[_map.Size];

        _mapOffset = new int[Directions.AllNeighboringDirections.Length];
        for (int i = 0; i < _mapOffset.Length; ++i)
        {
            _mapOffset[i] = _geometry.GetDirectionIndex(0, Directions.AllNeighboringDirections[i]);
        }
    }
}
