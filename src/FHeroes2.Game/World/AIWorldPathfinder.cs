using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

// "Army" is both a namespace (FHeroes2.Game.Army) and the type inside it, so the type needs an alias.
using ArmyUnit = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game;

/// <summary>
/// The pathfinder the AI reasons with, <c>AIWorldPathfinder</c> (<c>world_pathfinding.h:157-277</c>,
/// <c>world_pathfinding.cpp:543-1434</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is not a <see cref="PlayerWorldPathfinder"/>.</b> A player's pathfinder answers "where can this
/// hero walk this turn"; the AI asks "how far away is everything, and what would it cost me" about an army
/// that is not on the map. The AI's model never runs out of movement points, so
/// <see cref="GetMaxMovePoints"/> returns zero and every rule about "what is left after the step"
/// collapses; that is why the cost function is its own.
/// </para>
/// <para>
/// <b>What it adds.</b> Its own passability (a tile is reachable when the army can beat its guards, see
/// <see cref="Map.IsTileProtectionStrongerThan"/>); memoisation of those answers inside the search cache,
/// because each of them builds an <see cref="ArmyUnit"/>; and a cost function that charges a whole turn for
/// boarding a boat or stepping ashore.
/// </para>
/// <para>
/// <b>What is not here yet.</b> The members that take a <see cref="Hero"/> throw
/// <see cref="NotImplementedException"/>: they need hero data the port does not read yet (the artifact bag,
/// the patrol centre, the dimension door uses, spell points as a movement budget). The fields they would
/// fill are already here and stay at their "no hero" values, so the non-hero search is complete.
/// </para>
/// <para>
/// <b>Town Gate and Town Portal.</b> The reference seeds the walk with the castle entrances the hero could
/// teleport to; they are seeded here too, but only the hero batch sets the two fields, so in practice the
/// search starts from one tile.
/// </para>
/// </remarks>
public sealed class AIWorldPathfinder : WorldPathfinder
{
    /// <summary>The default <c>_minimalArmyStrengthAdvantage</c> of the header.</summary>
    private const double DefaultMinimalArmyStrengthAdvantage = 1.0;

    /// <summary>The default <c>_spellPointsReserveRatio</c> of the header.</summary>
    private const double DefaultSpellPointsReserveRatio = 0.5;

    private int _patrolCenter = -1;
    private uint _patrolDistance;
    private uint _maxMovePointsOnLand;
    private uint _maxMovePointsOnWater;
    private uint _remainingSpellPoints;
    private uint _maxSpellPoints;
    private uint _dimensionDoorSpCost;
    private uint _dimensionDoorNumOfUses;
    private double _armyStrength = -1;
    private bool _isOnPatrol;
    private bool _isArtifactsBagFull;
    private bool _isEquippedWithSpellBook;
    private bool _isSummonBoatSpellAvailable;
    private bool _isDimensionDoorSpellAvailable;

    /// <summary>Entrance tile index of the castle Town Gate would reach, or <c>-1</c>.</summary>
    private int _townGateCastleIndex = -1;

    /// <summary>Entrance tile indexes of the castles Town Portal would reach.</summary>
    private readonly List<int> _townPortalCastleIndexes = [];

    private double _minimalArmyStrengthAdvantage = DefaultMinimalArmyStrengthAdvantage;
    private double _spellPointsReserveRatio = DefaultSpellPointsReserveRatio;

    /// <summary>Creates a pathfinder for the AI of <paramref name="map"/>.</summary>
    /// <param name="map">The world to search; it has to hold a loaded map already.</param>
    public AIWorldPathfinder(World map)
        : base(map)
    {
    }

    /// <summary>How much stronger than a guard the army has to be, <c>_minimalArmyStrengthAdvantage</c>.</summary>
    public double MinimalArmyStrengthAdvantage => _minimalArmyStrengthAdvantage;

    /// <summary>Which share of the spell points the planner may spend, <c>_spellPointsReserveRatio</c>.</summary>
    public double SpellPointsReserveRatio => _spellPointsReserveRatio;

    /// <summary>
    /// Puts the search back into its "nothing has been searched" state, <c>reset()</c>
    /// (<c>world_pathfinding.cpp:543</c>).
    /// </summary>
    /// <remarks>
    /// The reference writes the two tunables back to their defaults here as well, so a planner that wants a
    /// margin sets it again after every reset.
    /// </remarks>
    public override void Reset()
    {
        base.Reset();

        _patrolCenter = -1;
        _patrolDistance = 0;
        _maxMovePointsOnLand = 0;
        _maxMovePointsOnWater = 0;
        _remainingSpellPoints = 0;
        _maxSpellPoints = 0;
        _dimensionDoorSpCost = 0;
        _dimensionDoorNumOfUses = 0;
        _armyStrength = -1;
        _isOnPatrol = false;
        _isArtifactsBagFull = false;
        _isEquippedWithSpellBook = false;
        _isSummonBoatSpellAvailable = false;
        _isDimensionDoorSpellAvailable = false;
        _townGateCastleIndex = -1;
        _townPortalCastleIndexes.Clear();
        _minimalArmyStrengthAdvantage = DefaultMinimalArmyStrengthAdvantage;
        _spellPointsReserveRatio = DefaultSpellPointsReserveRatio;
    }

    /// <summary>
    /// Searches again if anything the result depends on has changed, <c>reEvaluateIfNeeded()</c>
    /// (<c>world_pathfinding.cpp:617</c>).
    /// </summary>
    /// <remarks>
    /// The comparison is exact, not approximate: the strength comes from one caller and is stored
    /// unchanged, so a repeated value must not trigger a search and a different one must.
    /// </remarks>
    /// <param name="startIndex">Index of the tile the AI starts from.</param>
    /// <param name="color">Color of the side the AI plays.</param>
    /// <param name="armyStrength">Strength of the army that will walk the path.</param>
    /// <param name="pathfindingSkill">Level of the pathfinding skill the army has.</param>
    /// <exception cref="ArgumentOutOfRangeException">The start index is outside the map.</exception>
    public void ReEvaluateIfNeeded(int startIndex, PlayerColor color, double armyStrength, SkillLevel pathfindingSkill)
    {
        if (PathStart == startIndex
            && PathColor == color
            && PathRemainingMovePoints == 0
            && PathfindingSkill == pathfindingSkill
            && _patrolCenter == -1
            && _patrolDistance == 0
            && _maxMovePointsOnLand == 0
            && _maxMovePointsOnWater == 0
            && _remainingSpellPoints == 0
            && _maxSpellPoints == 0
            && _dimensionDoorSpCost == 0
            && _dimensionDoorNumOfUses == 0
            && _armyStrength.Equals(armyStrength)
            && !_isOnPatrol
            && !_isArtifactsBagFull
            && !_isEquippedWithSpellBook
            && !_isSummonBoatSpellAvailable
            && !_isDimensionDoorSpellAvailable
            && _townGateCastleIndex == -1
            && _townPortalCastleIndexes.Count == 0)
        {
            return;
        }

        SetSearchContext(startIndex, color, 0, pathfindingSkill);

        _patrolCenter = -1;
        _patrolDistance = 0;
        _maxMovePointsOnLand = 0;
        _maxMovePointsOnWater = 0;
        _remainingSpellPoints = 0;
        _maxSpellPoints = 0;
        _dimensionDoorSpCost = 0;
        _dimensionDoorNumOfUses = 0;
        _armyStrength = armyStrength;
        _isOnPatrol = false;
        _isArtifactsBagFull = false;
        _isEquippedWithSpellBook = false;
        _isSummonBoatSpellAvailable = false;
        _isDimensionDoorSpellAvailable = false;
        _townGateCastleIndex = -1;
        _townPortalCastleIndexes.Clear();

        ProcessWorldMap();
    }

    /// <summary>Searches for a hero that is on the map, <c>reEvaluateIfNeeded( const Heroes &amp; )</c>.</summary>
    /// <param name="hero">The hero to model.</param>
    /// <param name="armyStrength">Strength of the army on the map.</param>
    /// <exception cref="NotImplementedException">The hero data is not read yet.</exception>
    public void ReEvaluateIfNeeded(Hero hero, double armyStrength)
    {
        ArgumentNullException.ThrowIfNull(hero);

        throw new NotImplementedException(
            "reEvaluateIfNeeded(hero, armyStrength) needs the hero's artifact bag, patrol centre and spell"
            + " points, none of which the port reads yet.");
    }

    /// <summary>Movement points to a tile, <c>getDistance()</c> (<c>world_pathfinding.cpp:1244</c>).</summary>
    /// <remarks>
    /// The "no hero" overload: the remaining movement points are fixed at zero, so the answer is the sum of
    /// the step costs and not a remaining budget.
    /// </remarks>
    /// <param name="startIndex">Index of the tile the AI starts from.</param>
    /// <param name="targetIndex">Index of the tile to measure.</param>
    /// <param name="color">Color of the side the AI plays.</param>
    /// <param name="armyStrength">Strength of the army that will walk the path.</param>
    /// <param name="pathfindingSkill">Level of the pathfinding skill the army has.</param>
    /// <returns>Movement points the route costs, or zero when it does not exist.</returns>
    public uint GetDistance(int startIndex, int targetIndex, PlayerColor color, double armyStrength, SkillLevel pathfindingSkill = SkillLevel.Expert)
    {
        ReEvaluateIfNeeded(startIndex, color, armyStrength, pathfindingSkill);

        return GetDistance(targetIndex);
    }

    /// <summary>The route to a tile, in walking order, <c>buildPath()</c> (<c>world_pathfinding.cpp:1345</c>).</summary>
    /// <remarks>
    /// <para>
    /// The path is read backwards: the cache knows for every tile which tile it was reached from.
    /// </para>
    /// <para>
    /// <b>The cut.</b> The AI walks through objects it can take, so the chain can end on a tile the army
    /// cannot stand on — it takes the object and moves on. With <paramref name="accountNearestObject"/>
    /// the walk stops there: the last tile the base passability test refuses is remembered while the chain
    /// is read back, and every step from it onwards is dropped. Without the flag the reference returns the
    /// chain untouched, even when the destination is such an object.
    /// </para>
    /// </remarks>
    /// <param name="targetIndex">Index of the destination tile.</param>
    /// <param name="accountNearestObject">Whether to stop at the last object that cannot be walked through.</param>
    /// <returns>The steps from the tile next to the start to the last tile the AI heads for.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the map.</exception>
    public List<RouteStep> BuildPath(int targetIndex, bool accountNearestObject)
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

        // The reference asks the passability test with the side the whole walk starts on, not with the
        // side of every single step.
        bool fromWater = Map.GetTile(PathStart).IsWater;
        int lastValidNode = targetIndex;
        int currentNode = targetIndex;

        while (currentNode != PathStart)
        {
            Debug.Assert(currentNode != -1, "The path chain runs off the map.");

            if (accountNearestObject && !IsTileAvailableForWalkThrough(Map, currentNode, fromWater))
            {
                lastValidNode = currentNode;
            }

            int from = GetNodeFrom(currentNode);

            Debug.Assert(from != -1, "The path chain has no start.");

            uint cost = GetNodeCost(currentNode) - GetNodeCost(from);

            path.Add(new RouteStep(currentNode, from, Geometry.GetDirection(from, currentNode), cost));

            currentNode = from;
        }

        // The chain was walked from the destination back to the start.
        path.Reverse();

        // The cut drops the step that leaves the last object and everything behind it; the object itself
        // stays the end of the route, because taking it is what the walk was for.
        if (lastValidNode != targetIndex)
        {
            int cutIndex = path.FindIndex(step => step.From == lastValidNode);

            if (cutIndex >= 0)
            {
                path.RemoveRange(cutIndex, path.Count - cutIndex);
            }
        }

        return path;
    }

    /// <summary>Sets how much stronger than a guard the army has to be, <c>setMinimalArmyStrengthAdvantage()</c>.</summary>
    /// <param name="advantage">The multiplier, at least zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
    public void SetMinimalArmyStrengthAdvantage(double advantage)
    {
        if (!double.IsFinite(advantage) || advantage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(advantage), advantage, "The advantage has to be finite and non-negative.");
        }

        _minimalArmyStrengthAdvantage = advantage;
    }

    /// <summary>Sets which share of the spell points the planner may spend, <c>setSpellPointsReserveRatio()</c>.</summary>
    /// <param name="ratio">The ratio, between zero and one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside [0, 1].</exception>
    public void SetSpellPointsReserveRatio(double ratio)
    {
        if (!double.IsFinite(ratio) || ratio < 0 || ratio > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "The ratio has to be between zero and one.");
        }

        _spellPointsReserveRatio = ratio;
    }

    /// <summary>Where to go to lift the fog, <c>getFogDiscoveryTile()</c> (<c>world_pathfinding.cpp:886</c>).</summary>
    /// <param name="hero">The scouting hero.</param>
    /// <returns>The tile to walk to, and whether it is a boat.</returns>
    /// <exception cref="NotImplementedException">The hero search is not implemented.</exception>
    public (int Index, bool IsBoat) GetFogDiscoveryTile(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        throw new NotImplementedException(
            "getFogDiscoveryTile() needs the hero's scouting distance and the objects-on-the-way scan.");
    }

    /// <summary>The closest tile worth moving to, <c>getNearestTileToMove()</c> (<c>world_pathfinding.cpp:968</c>).</summary>
    /// <param name="hero">The hero to move.</param>
    /// <returns>The tile to move to.</returns>
    /// <exception cref="NotImplementedException">The hero search is not implemented.</exception>
    public int GetNearestTileToMove(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        throw new NotImplementedException("getNearestTileToMove() needs the objects-on-the-way scan.");
    }

    /// <summary>Whether a friendly hero is in the way, <c>isHeroPossiblyBlockingWay()</c>.</summary>
    /// <param name="hero">The hero that would walk.</param>
    /// <returns><see langword="true"/> when a friendly hero blocks the way.</returns>
    /// <exception cref="NotImplementedException">The hero search is not implemented.</exception>
    public bool IsHeroPossiblyBlockingWay(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        throw new NotImplementedException("isHeroPossiblyBlockingWay() needs the hero search.");
    }

    /// <summary>A route that uses Dimension Door, <c>buildDimensionDoorPath()</c>.</summary>
    /// <param name="hero">The hero that would cast it.</param>
    /// <param name="targetIndex">Index of the destination tile.</param>
    /// <returns>The route, or an empty list.</returns>
    /// <exception cref="NotImplementedException">The hero search is not implemented.</exception>
    public List<RouteStep> BuildDimensionDoorPath(Hero hero, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        throw new NotImplementedException(
            "buildDimensionDoorPath() needs the hero's spell points and dimension door uses, neither of"
            + " which the port reads yet.");
    }

    /// <summary>
    /// The tiles worth stopping at on the way, <c>getObjectsOnTheWay()</c> (<c>world_pathfinding.cpp:1162</c>).
    /// </summary>
    /// <param name="targetIndex">Index of the destination tile.</param>
    /// <returns>One entry per object worth a stop, nearest first.</returns>
    /// <exception cref="NotImplementedException">The objects-on-the-way scan is not implemented.</exception>
    public List<(int Index, int Priority)> GetObjectsOnTheWay(int targetIndex) =>
        throw new NotImplementedException(
            "getObjectsOnTheWay() needs object UIDs and AiCommon.IsValuableAdventureMapObject(), which is not"
            + " implemented.");

    /// <summary>
    /// Whether the AI may step from one tile in a direction, <c>isMovementAllowed()</c>
    /// (<c>world_pathfinding.cpp:728</c>).
    /// </summary>
    /// <remarks>
    /// The only difference from the base rule is that the AI may summon a boat, so a shore tile counts as
    /// connected to a suitable water tile.
    /// </remarks>
    /// <param name="from">Index of the tile the step starts on.</param>
    /// <param name="direction">Direction of the step.</param>
    /// <returns><see langword="true"/> when the step is allowed.</returns>
    protected override bool IsMovementAllowed(int from, Direction direction) =>
        IsMovementAllowedForColor(from, direction, PathColor, ignoreFog: false, isSummonBoatSpellAvailable: _isSummonBoatSpellAvailable);

    /// <summary>Seeds the search, <c>processWorldMap()</c> (<c>world_pathfinding.cpp:686</c>).</summary>
    /// <remarks>
    /// The base search starts from one tile; the AI also starts from the castle entrances Town Gate and
    /// Town Portal could reach, at the price of the spell, so that those tiles are explored like the start.
    /// </remarks>
    protected override void ProcessWorldMap()
    {
        ClearCache();
        SeedStartNode();

        List<int> nodesToExplore = [PathStart];

        if (_townGateCastleIndex != -1)
        {
            ProcessTownPortalSeed(new Spell(SpellType.TownGate), _townGateCastleIndex, nodesToExplore);
        }

        foreach (int castleIndex in _townPortalCastleIndexes)
        {
            if (castleIndex == _townGateCastleIndex)
            {
                continue;
            }

            ProcessTownPortalSeed(new Spell(SpellType.TownPortal), castleIndex, nodesToExplore);
        }

        ProcessNodes(nodesToExplore);
    }

    /// <summary>
    /// Adds a castle entrance as an extra start of the search, the <c>processTownPortal</c> lambda of
    /// <c>processWorldMap()</c>.
    /// </summary>
    /// <param name="spell">The spell that reaches the castle.</param>
    /// <param name="castleIndex">Entrance tile index of the castle.</param>
    /// <param name="nodesToExplore">The queue the walk starts from.</param>
    private void ProcessTownPortalSeed(Spell spell, int castleIndex, List<int> nodesToExplore)
    {
        uint cost = spell.MovePoints();
        uint remaining = PathRemainingMovePoints < cost ? 0 : PathRemainingMovePoints - cost;

        UpdateNode(castleIndex, PathStart, cost, remaining);
        nodesToExplore.Add(castleIndex);
    }

    /// <summary>
    /// What a tile does once it has been reached, <c>processCurrentNode()</c>
    /// (<c>world_pathfinding.cpp:733</c>).
    /// </summary>
    /// <remarks>
    /// The AI's additions to the base walk: a tile the army may not stand on is dropped again and not
    /// expanded; a tile it may not walk through is reached but not expanded; and a stone liths or a
    /// whirlpool links to the other end of the teleport at no cost — unless the tile was reached *through*
    /// that teleport, in which case walking on would undo it.
    /// </remarks>
    /// <param name="nodesToExplore">The queue being walked; new tiles are appended to it.</param>
    /// <param name="currentNodeIdx">Index of the tile that was just reached.</param>
    protected override void ProcessCurrentNode(List<int> nodesToExplore, int currentNodeIdx)
    {
        bool isFirstNode = currentNodeIdx == PathStart;

        if (!isFirstNode)
        {
            if (!IsTileAccessibleForAI(currentNodeIdx))
            {
                ResetNode(currentNodeIdx);

                return;
            }

            int nodeFrom = GetNodeFrom(currentNodeIdx);

            Debug.Assert(nodeFrom != -1, "A reached node has no start.");

            if (!IsTileAvailableForWalkThroughForAI(currentNodeIdx, Map.GetTile(nodeFrom).IsWater))
            {
                return;
            }
        }

        Mp2ObjectType mainObjectType = Map.GetTile(currentNodeIdx).GetMainObjectType(Map, ignoreObjectUnderHero: false);

        if (mainObjectType is Mp2ObjectType.ActionStoneLiths or Mp2ObjectType.ActionWhirlpool)
        {
            List<int> teleports = [];

            if (!isFirstNode)
            {
                teleports = mainObjectType == Mp2ObjectType.ActionStoneLiths
                    ? Map.GetTeleportEndPoints(currentNodeIdx)
                    : Map.GetWhirlpoolEndPoints(currentNodeIdx);
            }

            foreach (int teleportIdx in teleports)
            {
                // The start tile is never re-reached through a teleport: it is already at cost zero.
                if (teleportIdx == PathStart)
                {
                    continue;
                }

                if (!IsNodeReached(teleportIdx) || GetNodeCost(teleportIdx) > GetNodeCost(currentNodeIdx))
                {
                    UpdateNode(
                        teleportIdx,
                        currentNodeIdx,
                        GetNodeCost(currentNodeIdx),
                        GetNodeRemainingMovePoints(currentNodeIdx));

                    nodesToExplore.Add(teleportIdx);
                }
            }

            // A teleport that led here must not be walked away from: doing so would undo it.
            if (teleports.Count > 0 && !teleports.Contains(GetNodeFrom(currentNodeIdx)))
            {
                return;
            }
        }

        CheckAdjacentNodes(nodesToExplore, currentNodeIdx);
    }

    /// <summary>
    /// Movement points of a full turn for the army, <c>getMaxMovePoints()</c>
    /// (<c>world_pathfinding.cpp:809</c>).
    /// </summary>
    /// <param name="onWater">Whether the army would be on water.</param>
    /// <returns>The movement points, zero when the search models no real hero.</returns>
    protected override uint GetMaxMovePoints(bool onWater) => onWater ? _maxMovePointsOnWater : _maxMovePointsOnLand;

    /// <summary>What a step costs, <c>getMovementPenalty()</c> (<c>world_pathfinding.cpp:814</c>).</summary>
    /// <remarks>
    /// <para>
    /// <b>Stepping off an object.</b> A tile the army may only interact with from the front is not a tile it
    /// can stand on, so a step out of it means the army took the object first — and that took movement
    /// points. The reference charges the step into the tile a second time as an estimate, because the real
    /// path will never contain the step out of it.
    /// </para>
    /// <para>
    /// <b>Boarding and landing.</b> Getting into a boat or stepping ashore spends everything left, so the
    /// cost is the remaining budget, and a step that cannot be afforded this turn costs a whole turn. When
    /// the search models no real hero both budgets are zero and the rule collapses.
    /// </para>
    /// </remarks>
    /// <param name="from">Index of the tile the step starts on.</param>
    /// <param name="to">Index of the tile the step ends on.</param>
    /// <param name="direction">Direction of the step.</param>
    /// <returns>The cost in movement points.</returns>
    protected override uint GetMovementPenalty(int from, int to, Direction direction)
    {
        Tile fromTile = Map.GetTile(from);
        Mp2ObjectType fromObjectType = fromTile.GetMainObjectType(Map);

        uint defaultPenalty = base.GetMovementPenalty(from, to, direction);

        if (from != PathStart
            && Mp2ObjectTypes.IsNeedStayFront(fromObjectType)
            && fromObjectType != Mp2ObjectType.ActionBoat)
        {
            int nodeFrom = GetNodeFrom(from);

            Debug.Assert(nodeFrom != -1, "A node reached by the search has a start.");

            Direction prevStepDirection = Geometry.GetDirection(nodeFrom, from);

            Debug.Assert(
                prevStepDirection is not (Direction.Unknown or Direction.Center),
                "Two different tiles never share a direction.");

            defaultPenalty += base.GetMovementPenalty(nodeFrom, from, prevStepDirection);
        }

        uint maxMovePoints = GetMaxMovePoints(fromTile.IsWater);

        Debug.Assert(maxMovePoints == 0 || defaultPenalty <= maxMovePoints, "A step cannot cost more than a turn.");

        if (maxMovePoints > 0)
        {
            Tile toTile = Map.GetTile(to);

            bool isComesOnBoard = !fromTile.IsWater
                && (toTile.GetMainObjectType(Map) == Mp2ObjectType.ActionBoat || toTile.IsSuitableForSummoningBoat());

            bool isDisembarks = fromTile.IsWater && toTile.IsSuitableForDisembarkation();

            if (isComesOnBoard || isDisembarks)
            {
                uint remainingMovePoints = GetNodeRemainingMovePoints(from);

                return defaultPenalty > remainingMovePoints ? maxMovePoints : remainingMovePoints;
            }
        }

        return defaultPenalty;
    }

    /// <summary>Whether the army may stand on a tile, <c>isTileAccessibleForAI()</c> (<c>world_pathfinding.cpp:664</c>).</summary>
    /// <remarks>The answer is memoised in the search cache: it builds an <see cref="ArmyUnit"/> for the tile.</remarks>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <returns><see langword="true"/> when the army may stand on it.</returns>
    private bool IsTileAccessibleForAI(int tileIndex)
    {
        bool? cached = GetAccessibleForAI(tileIndex);

        if (cached is null)
        {
            cached = IsTileAccessibleForAIWithArmy(tileIndex, _armyStrength, _minimalArmyStrengthAdvantage);
            SetAccessibleForAI(tileIndex, cached.Value);
        }

        return cached.Value;
    }

    /// <summary>
    /// Whether the army may walk through a tile, <c>isTileAvailableForWalkThroughForAI()</c>
    /// (<c>world_pathfinding.cpp:674</c>).
    /// </summary>
    /// <remarks>Two slots per tile, one per side the army arrives from.</remarks>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <param name="fromWater">Whether the army arrives on a boat.</param>
    /// <returns><see langword="true"/> when the army may pass through the tile.</returns>
    private bool IsTileAvailableForWalkThroughForAI(int tileIndex, bool fromWater)
    {
        bool? cached = GetAvailableForWalkThroughForAI(tileIndex, fromWater);

        if (cached is null)
        {
            cached = IsTileAvailableForWalkThroughForAIWithArmy(
                tileIndex,
                fromWater,
                PathColor,
                _isArtifactsBagFull,
                _isEquippedWithSpellBook,
                _armyStrength,
                _minimalArmyStrengthAdvantage);

            SetAvailableForWalkThroughForAI(tileIndex, fromWater, cached.Value);
        }

        return cached.Value;
    }

    /// <summary>
    /// Whether the guards of a tile can be beaten, <c>isTileAccessibleForAIWithArmy()</c>
    /// (<c>world_pathfinding.cpp:293</c>).
    /// </summary>
    /// <remarks>A tile with monsters is always accessible: which of them to attack is the planner's call.</remarks>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <param name="armyStrength">Strength of the army.</param>
    /// <param name="minimalAdvantage">How much stronger than a guard the army has to be.</param>
    /// <returns><see langword="true"/> when the guards can be beaten.</returns>
    private bool IsTileAccessibleForAIWithArmy(int tileIndex, double armyStrength, double minimalAdvantage)
    {
        if (Map.GetTile(tileIndex).GetMainObjectType(Map) == Mp2ObjectType.ActionMonster)
        {
            return true;
        }

        return !Map.IsTileProtectionStrongerThan(tileIndex, armyStrength / minimalAdvantage);
    }

    /// <summary>
    /// Whether the army may walk through a tile, <c>isTileAvailableForWalkThroughForAIWithArmy()</c>
    /// (<c>world_pathfinding.cpp:74</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order of the checks is the reference's and matters: heroes first, then pickup objects (the only
    /// ones the army takes and walks over), monsters, barriers, boats, the objects interacted with from the
    /// front, castles, and finally the objects that force a fight when protected.
    /// </para>
    /// <para>
    /// <b>Placeholder.</b> The reference also refuses a hero standing inside a castle; the port does not
    /// link a hero to a castle yet, so that check is missing. It is the only place where this method can
    /// answer differently from the reference.
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <param name="fromWater">Whether the army arrives on a boat.</param>
    /// <param name="color">Color of the side the AI plays.</param>
    /// <param name="isArtifactsBagFull">Whether the army has no room for another artifact.</param>
    /// <param name="isEquippedWithSpellBook">Whether the army already carries a spell book.</param>
    /// <param name="armyStrength">Strength of the army.</param>
    /// <param name="minimalAdvantage">How much stronger than a guard the army has to be.</param>
    /// <returns><see langword="true"/> when the army may pass through the tile.</returns>
    private bool IsTileAvailableForWalkThroughForAIWithArmy(
        int tileIndex,
        bool fromWater,
        PlayerColor color,
        bool isArtifactsBagFull,
        bool isEquippedWithSpellBook,
        double armyStrength,
        double minimalAdvantage)
    {
        Debug.Assert(color != PlayerColor.None, "The AI plays a color.");

        Tile tile = Map.GetTile(tileIndex);
        Mp2ObjectType objectType = tile.GetMainObjectType(Map);

        bool IsTileAccessible()
        {
            ArmyUnit tileArmy = new();
            tileArmy.SetFromTile(Map, tile);

            PlayerColor tileArmyColor = tileArmy.Color;

            // The tile may be guarded by our own or a friendly army, e.g. an ally's Set Elemental Guardian.
            if (color == tileArmyColor || Players.IsFriends(color, tileArmyColor))
            {
                return true;
            }

            return tileArmy.GetStrength() * minimalAdvantage <= armyStrength;
        }

        // Enemy heroes can be defeated and passed through.
        if (objectType == Mp2ObjectType.ActionHero)
        {
            // Heroes on water can be attacked from the shore but not passed through.
            if (fromWater != tile.IsWater)
            {
                Debug.Assert(!fromWater && tile.IsWater, "A water hero is the only one reached from land.");

                return false;
            }

            Hero? otherHero = tile.GetHero(Map);

            Debug.Assert(otherHero is not null, "A hero tile holds a hero.");

            if (otherHero is null)
            {
                return false;
            }

            // Friendly heroes cannot be passed through.
            if (otherHero.IsFriends(color))
            {
                return false;
            }

            // A WINS_HERO victory condition does not apply to the AI; the hero must be kept alive.
            if (otherHero == Map.GetHeroesCondWins())
            {
                return false;
            }

            return otherHero.Army.GetStrength() * minimalAdvantage <= armyStrength;
        }

        // Pickup objects (artifacts included) can be taken and walked over.
        if (Mp2ObjectTypes.IsPickupObject(objectType))
        {
            // There may not be enough gold to hire all the genies, so the planner decides about the lamp.
            if (objectType == Mp2ObjectType.ActionGenieLamp)
            {
                return false;
            }

            if (!Mp2ObjectTypes.IsArtifactObject(objectType))
            {
                return true;
            }

            ArtifactType artifact = tile.GetArtifactFromTile(Map);

            if (!ArtifactTypes.IsValid(artifact))
            {
                return true;
            }

            // The object holds an artifact but is not the artifact itself.
            if (objectType != Mp2ObjectType.ActionArtifact)
            {
                return true;
            }

            if (isArtifactsBagFull)
            {
                return false;
            }

            // A spell book cannot be taken by an army that already has one.
            if (artifact == ArtifactType.MagicBook && isEquippedWithSpellBook)
            {
                return false;
            }

            // A guarded artifact with a price is left to the planner.
            if (tile.GetArtifactCaptureCondition(Map) is ArtifactCaptureCondition.Pay2000Gold
                or ArtifactCaptureCondition.Pay2500GoldAnd3Resources
                or ArtifactCaptureCondition.Pay3000GoldAnd5Resources
                or ArtifactCaptureCondition.HaveWisdomSkill
                or ArtifactCaptureCondition.HaveLeadershipSkill)
            {
                return false;
            }

            return IsTileAccessible();
        }

        // Monsters can be defeated and passed through.
        if (objectType == Mp2ObjectType.ActionMonster)
        {
            return IsTileAccessible();
        }

        // The AI may hold the key for the barrier.
        if (objectType == Mp2ObjectType.ActionBarrier)
        {
            return Map.GetKingdom(color).IsTravellerTentVisited(tile.GetBarrierColorFromTile(Map));
        }

        // Boats are entered from the shore and sailed away.
        if (objectType == Mp2ObjectType.ActionBoat)
        {
            Debug.Assert(!fromWater && tile.IsWater, "A boat sits on water and is entered from land.");

            return true;
        }

        // A tile that cannot be stepped on cannot be walked through either.
        if (Mp2ObjectTypes.IsNeedStayFront(objectType))
        {
            return false;
        }

        // The castle tile is only passable when it was reached by Town Gate or Town Portal, which means it is
        // the AI's own castle.
        if (objectType == Mp2ObjectType.ActionCastle)
        {
            return color == tile.GetColor(Map);
        }

        // A protected object that forces a fight can be walked through once the guards are beaten.
        if (Mp2ObjectTypes.IsBattleMandatoryIfObjectIsProtected(objectType))
        {
            return IsTileAccessible();
        }

        // Nothing protects the tile, or the fight can be refused.
        return true;
    }
}
