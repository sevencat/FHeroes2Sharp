using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game;

/// <summary>
/// The pathfinder of a hero that is actually on the map, <c>PlayerWorldPathfinder</c>
/// (<c>world_pathfinding.h:130-190</c>, <c>world_pathfinding.cpp:443-541</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two things separate this from the AI pathfinder: the movement points of a real turn (so the "last
/// move" rule of <see cref="WorldPathfinder.GetMovementPenalty"/> applies) and the rule that a tile
/// protected by monsters may be reached but not walked through — a hero stops at the monsters instead of
/// strolling past them, and from the tile in front of them the only steps the search tries are the ones
/// towards the monsters themselves.
/// </para>
/// <para>
/// <b>Substitution.</b> The reference takes the hero itself
/// (<c>reEvaluateIfNeeded( const Heroes &amp; )</c>) and reads four of its members. The port has no
/// <c>Hero</c> instance with movement points yet, so the four values are passed in — they are exactly
/// <c>hero.GetIndex()</c>, <c>hero.GetColor()</c>, <c>hero.GetMovePoints()</c>,
/// <c>hero.GetLevelSkill( PATHFINDING )</c> and <c>hero.GetMaxMovePoints()</c>. When the hero side of
/// the port lands, the hero overload calls this one and nothing else changes.
/// </para>
/// </remarks>
public sealed class PlayerWorldPathfinder : WorldPathfinder
{
    private uint _maxMovePoints;

    /// <summary>
    /// Creates a pathfinder for the heroes of <paramref name="map"/>.
    /// </summary>
    /// <param name="map">The world to search; it has to hold a loaded map already.</param>
    public PlayerWorldPathfinder(World map)
        : base(map)
    {
    }

    /// <summary>
    /// Puts the search back into its "nothing has been searched" state, <c>reset()</c>.
    /// </summary>
    public override void Reset()
    {
        base.Reset();

        _maxMovePoints = 0;
    }

    /// <summary>
    /// Searches again if anything the result depends on has changed, <c>reEvaluateIfNeeded()</c>.
    /// </summary>
    /// <remarks>
    /// The search is the expensive part, so it only runs when one of the five settings differs from the
    /// ones the cache was built with. A hero standing still with the same movement points and the same
    /// skill asks the same question over and over — every frame of a mouse hover, for example — and gets
    /// the cached answer.
    /// </remarks>
    /// <param name="startIndex">Index of the tile the hero stands on.</param>
    /// <param name="color">Color of the hero's side.</param>
    /// <param name="remainingMovePoints">Movement points the hero has left.</param>
    /// <param name="pathfindingSkill">Level of the hero's pathfinding skill.</param>
    /// <param name="maxMovePoints">Movement points of a full turn for this hero.</param>
    /// <exception cref="ArgumentOutOfRangeException">The start index is outside the map.</exception>
    public void ReEvaluateIfNeeded(
        int startIndex,
        PlayerColor color,
        uint remainingMovePoints,
        SkillLevel pathfindingSkill,
        uint maxMovePoints)
    {
        if (PathStart == startIndex
            && PathColor == color
            && PathRemainingMovePoints == remainingMovePoints
            && PathfindingSkill == pathfindingSkill
            && _maxMovePoints == maxMovePoints)
        {
            return;
        }

        SetSearchContext(startIndex, color, remainingMovePoints, pathfindingSkill);
        _maxMovePoints = maxMovePoints;

        ProcessWorldMap();
    }

    /// <summary>
    /// The route to a tile, in walking order, <c>buildPath()</c>.
    /// </summary>
    /// <remarks>
    /// The path is read backwards: the cache knows for every tile which tile it was reached from, so the
    /// route is the chain of those links from the destination to the start, reversed. An empty list is
    /// the answer for a tile with no route — including the start tile itself, which the search leaves at
    /// cost zero.
    /// </remarks>
    /// <param name="targetIndex">Index of the destination tile.</param>
    /// <returns>The steps from the tile next to the start to the destination.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the map.</exception>
    public List<RouteStep> BuildPath(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= Map.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(targetIndex), targetIndex, "The index is outside the map.");
        }

        List<RouteStep> path = [];

        // No route. A reachable tile always costs something: the goal is at least one step away.
        if (GetNodeCost(targetIndex) == 0)
        {
            return path;
        }

        int currentNode = targetIndex;

        while (currentNode != PathStart)
        {
            Debug.Assert(currentNode != -1, "The path chain runs off the map.");

            int from = GetNodeFrom(currentNode);

            Debug.Assert(from != -1, "The path chain has no start.");

            uint cost = GetNodeCost(currentNode) - GetNodeCost(from);

            path.Add(new RouteStep(currentNode, from, Geometry.GetDirection(from, currentNode), cost));

            currentNode = from;
        }

        // The chain was walked from the destination back to the start.
        path.Reverse();

        return path;
    }

    /// <summary>
    /// What a tile does once it has been reached, <c>processCurrentNode()</c>.
    /// </summary>
    /// <remarks>
    /// A tile that cannot be walked through is reached but not expanded, which is how the search stops at
    /// a monster, a pickup object or the opposite element. A tile protected by monsters is special: the
    /// hero cannot step past the guards, so the only steps tried from it are the ones onto the monsters,
    /// which is what makes "attack those monsters" a path the hero can be sent along.
    /// </remarks>
    /// <param name="nodesToExplore">The queue being walked; new tiles are appended to it.</param>
    /// <param name="currentNodeIdx">Index of the tile that was just reached.</param>
    protected override void ProcessCurrentNode(List<int> nodesToExplore, int currentNodeIdx)
    {
        bool isFirstNode = currentNodeIdx == PathStart;
        bool fromWater = Map.GetTile(PathStart).IsWater;

        if (!isFirstNode && !IsTileAvailableForWalkThrough(Map, currentNodeIdx, fromWater))
        {
            return;
        }

        List<int> monsters = Map.GetMonstersProtectingTile(currentNodeIdx);

        if (isFirstNode || monsters.Count == 0)
        {
            CheckAdjacentNodes(nodesToExplore, currentNodeIdx);

            return;
        }

        uint maxMovePoints = GetMaxMovePoints(fromWater);
        uint remainingMovePoints = GetNodeRemainingMovePoints(currentNodeIdx);

        foreach (int monsterIndex in monsters)
        {
            Direction direction = Geometry.GetDirection(currentNodeIdx, monsterIndex);

            // A monster standing on the tile itself, or one that is not a neighbour (a guard of a nearby
            // object), cannot be stepped onto.
            if (direction is Direction.Unknown or Direction.Center || !IsMovementAllowed(currentNodeIdx, direction))
            {
                continue;
            }

            uint movementPenalty = GetMovementPenalty(currentNodeIdx, monsterIndex, direction);
            uint movementCost = GetNodeCost(currentNodeIdx) + movementPenalty;

            if (GetNodeFrom(monsterIndex) == -1 || GetNodeCost(monsterIndex) > movementCost)
            {
                // The monsters are not queued: the search ends at them, the fight decides what happens
                // next, and the tile beyond them is the player's decision after that.
                UpdateNode(
                    monsterIndex,
                    currentNodeIdx,
                    movementCost,
                    SubtractMovePoints(remainingMovePoints, movementPenalty, maxMovePoints));
            }
        }
    }

    /// <summary>
    /// Movement points of a full turn, <c>getMaxMovePoints()</c>, which the reference answers from the
    /// hero and the port from what <see cref="ReEvaluateIfNeeded"/> was told.
    /// </summary>
    /// <param name="onWater">Whether the hero is on a boat; the reference ignores it, a boat does not
    /// change how far a hero can sail in a day.</param>
    /// <returns>Movement points of a full turn.</returns>
    protected override uint GetMaxMovePoints(bool onWater) => _maxMovePoints;
}
