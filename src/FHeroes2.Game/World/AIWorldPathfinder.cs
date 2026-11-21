using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Random;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

// "Army" is both a namespace (FHeroes2.Game.Army) and the type inside it, so the type needs an alias.
using ArmyUnit = FHeroes2.Game.Army.Army;

// Same for "Kingdom": the namespace and the type share the name.
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

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
/// <b>Heroes.</b> The members that take a <see cref="Hero"/> — <see cref="ReEvaluateIfNeeded(Hero)"/>,
/// <see cref="GetFogDiscoveryTile"/>, <see cref="GetNearestTileToMove"/>, <see cref="IsHeroPossiblyBlockingWay"/>,
/// <see cref="BuildDimensionDoorPath"/> and <see cref="GetObjectsOnTheWay"/> — set the hero-side fields
/// (patrol centre, dimension door uses, spell points as a movement budget, bag artifacts) before reading the
/// cache. Their hero comes from the caller rather than from a cached <c>_pathStart</c>, unlike the reference.
/// </para>
/// <para>
/// <b>Town Gate and Town Portal.</b> The reference seeds the walk with the castle entrances the hero could
/// teleport to; they are seeded here too, from the castles the hero owns for that color.
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

    /// <summary>
    /// Searches for a hero that is on the map, <c>reEvaluateIfNeeded( const Heroes &amp; )</c>
    /// (<c>world_pathfinding.cpp:566-646</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything the search needs is baked into the cache, so the whole search only runs when one of the
    /// inputs changed. The spells are the interesting part: they are only worth planning with when the hero
    /// can pay for them and still keep the reserve the planner budgets with.
    /// </para>
    /// <para>
    /// <b>Dimension Door</b> is the exception — its reserve is not checked here, because a route that ends on
    /// something that gives spell points back does not need one.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero to model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public void ReEvaluateIfNeeded(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        ReEvaluateIfNeeded(hero, hero.Army.GetStrength());
    }

    /// <summary>
    /// The same search with the army strength supplied by the caller, <c>reEvaluateIfNeeded( const Heroes &amp; )</c>
    /// (<c>world_pathfinding.cpp:566-646</c>) split for tests and for callers that already know the strength.
    /// </summary>
    /// <param name="hero">The hero to model.</param>
    /// <param name="armyStrength">Strength of the army the hero leads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public void ReEvaluateIfNeeded(Hero hero, double armyStrength)
    {
        ArgumentNullException.ThrowIfNull(hero);

        Spell summonBoat = new(SpellType.SummonBoat);
        Spell dimensionDoor = new(SpellType.DimensionDoor);

        uint spellPoints = hero.SpellPoints;
        uint maxSpellPoints = hero.GetMaxSpellPoints();
        uint reserve = (uint)(maxSpellPoints * _spellPointsReserveRatio);

        bool isOnPatrol = hero.Modes.HasFlag(HeroModes.Patrol);

        // A boat is only worth planning with when the hero has the spell, can pay for it with the reserve kept
        // and there is a boat he could summon.
        bool isSummonBoatSpellAvailable = hero.HasSpell(summonBoat)
                                          && spellPoints >= summonBoat.SpellPoints(hero.BagArtifacts) + reserve
                                          && SpellInfo.GetSummonableBoat(Map, hero, GetMapVersion(), Players.Get(hero.Color)) != -1;

        bool isDimensionDoorSpellAvailable = hero.HasSpell(dimensionDoor);

        int townGateCastleIndex = -1;
        Spell townGate = new(SpellType.TownGate);
        Spell townPortal = new(SpellType.TownPortal);

        if (hero.CanCastSpell(townGate) && spellPoints >= townGate.SpellPoints(hero.BagArtifacts) + reserve)
        {
            // The reference asserts the nearest castle is free. The port cannot hold a hero in a castle yet, so
            // every castle is free and the nearest one is taken as is.
            if (SpellInfo.GetNearestCastle(Geometry, Map.GetKingdom(hero.Color).Castles, Geometry.GetPoint(hero.MapIndex)) is { } castle)
            {
                townGateCastleIndex = castle.TileIndex;
            }
        }

        List<int> townPortalCastleIndexes = [];

        if (hero.CanCastSpell(townPortal) && spellPoints >= townPortal.SpellPoints(hero.BagArtifacts) + reserve)
        {
            foreach (var castle in Map.GetKingdom(hero.Color).Castles)
            {
                townPortalCastleIndexes.Add(castle.TileIndex);
            }
        }

        uint movePoints = hero.MovePoints;
        SkillLevel pathfindingSkill = hero.GetLevelSkill(SecondarySkill.Pathfinding);
        int patrolCenter = isOnPatrol ? hero.PatrolCenterIndex : -1;
        uint patrolDistance = isOnPatrol ? (uint)hero.PatrolDistance : 0u;
        uint maxMovePointsOnLand = hero.GetMaxMovePoints(false, Map);
        uint maxMovePointsOnWater = hero.GetMaxMovePoints(true, Map);
        uint dimensionDoorSpCost = dimensionDoor.SpellPoints(hero.BagArtifacts);
        uint dimensionDoorNumOfUses = hero.DimensionDoorUses;
        bool isArtifactsBagFull = hero.BagArtifacts.IsFull;
        bool isEquippedWithSpellBook = hero.HasSpellBook;

        if (PathStart == hero.MapIndex
            && PathColor == hero.Color
            && PathRemainingMovePoints == movePoints
            && PathfindingSkill == pathfindingSkill
            && _patrolCenter == patrolCenter
            && _patrolDistance == patrolDistance
            && _maxMovePointsOnLand == maxMovePointsOnLand
            && _maxMovePointsOnWater == maxMovePointsOnWater
            && _remainingSpellPoints == spellPoints
            && _maxSpellPoints == maxSpellPoints
            && _dimensionDoorSpCost == dimensionDoorSpCost
            && _dimensionDoorNumOfUses == dimensionDoorNumOfUses
            && _armyStrength == armyStrength
            && _isOnPatrol == isOnPatrol
            && _isArtifactsBagFull == isArtifactsBagFull
            && _isEquippedWithSpellBook == isEquippedWithSpellBook
            && _isSummonBoatSpellAvailable == isSummonBoatSpellAvailable
            && _isDimensionDoorSpellAvailable == isDimensionDoorSpellAvailable
            && _townGateCastleIndex == townGateCastleIndex
            && _townPortalCastleIndexes.SequenceEqual(townPortalCastleIndexes))
        {
            return;
        }

        SetSearchContext(hero.MapIndex, hero.Color, movePoints, pathfindingSkill);

        _patrolCenter = patrolCenter;
        _patrolDistance = patrolDistance;
        _maxMovePointsOnLand = maxMovePointsOnLand;
        _maxMovePointsOnWater = maxMovePointsOnWater;
        _remainingSpellPoints = spellPoints;
        _maxSpellPoints = maxSpellPoints;
        _dimensionDoorSpCost = dimensionDoorSpCost;
        _dimensionDoorNumOfUses = dimensionDoorNumOfUses;
        _armyStrength = armyStrength;
        _isOnPatrol = isOnPatrol;
        _isArtifactsBagFull = isArtifactsBagFull;
        _isEquippedWithSpellBook = isEquippedWithSpellBook;
        _isSummonBoatSpellAvailable = isSummonBoatSpellAvailable;
        _isDimensionDoorSpellAvailable = isDimensionDoorSpellAvailable;
        _townGateCastleIndex = townGateCastleIndex;
        _townPortalCastleIndexes.Clear();
        _townPortalCastleIndexes.AddRange(townPortalCastleIndexes);

        ProcessWorldMap();
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

    /// <summary>
    /// The version of the map being played, <c>Settings::getCurrentMapInfo().version</c> at the one place that
    /// asks for it (<c>reEvaluateIfNeeded( hero )</c>).
    /// </summary>
    /// <remarks>
    /// The port has no settings object that travels with the world, so a world built before a map was chosen
    /// answers with the Succession Wars rules — the strictest of the two for the summon boat spell.
    /// </remarks>
    /// <returns>The version of the current map, or <see cref="MapVersion.SuccessionWars"/> before one is chosen.</returns>
    private static MapVersion GetMapVersion() =>
        Settings.Get().CurrentMapFileInfo?.Version ?? MapVersion.SuccessionWars;

    /// <summary>
    /// Where to go to lift the fog, <c>getFogDiscoveryTile()</c> (<c>world_pathfinding.cpp:886-966</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The best tile is the cheapest one to reach that uncovers something, ties going to the tile that uncovers
    /// the most. Two passes: first the tiles with a fogged neighbour — the common case, since whatever hides the
    /// neighbour usually hides the rest of that pocket as well — and then, if that found nothing, the tiles with a
    /// neighbour the hero cannot reach at all, because the fog behind an obstacle is exactly what the first pass
    /// cannot see.
    /// </para>
    /// <para>
    /// A tile is only a candidate when <see cref="Mp2ObjectTypes.IsSafeForFogDiscoveryObject"/> agrees: standing on
    /// a guarded object to look around is not worth the fight.
    /// </para>
    /// </remarks>
    /// <param name="hero">The scouting hero.</param>
    /// <returns>
    /// The tile to walk to, and whether the fog lies right next to it — i.e. whether walking there most likely
    /// opens a new area; the index is -1 when nothing is left.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public (int Index, bool IsTerritoryExpansion) GetFogDiscoveryTile(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        ReEvaluateIfNeeded(hero);

        int FindBestTile(Func<int, bool> nearbyTilePredicate)
        {
            int bestIndex = -1;
            uint bestCost = uint.MaxValue;
            int bestTilesToReveal = 0;

            int scoutingDistance = hero.GetScoutingDistance();

            for (int tileIndex = 0; tileIndex < Map.Size; ++tileIndex)
            {
                uint nodeCost = GetNodeCost(tileIndex);

                if (nodeCost == 0 || !Mp2ObjectTypes.IsSafeForFogDiscoveryObject(Map.GetTile(tileIndex).MainObjectType))
                {
                    continue;
                }

                bool isTileSuitable = false;

                foreach (Direction direction in Directions.AllNeighboringDirections)
                {
                    if (!Geometry.IsValidDirection(tileIndex, direction)
                        || !nearbyTilePredicate(Geometry.GetDirectionIndex(tileIndex, direction)))
                    {
                        continue;
                    }

                    isTileSuitable = true;

                    break;
                }

                if (!isTileSuitable)
                {
                    continue;
                }

                Point position = Geometry.GetPoint(tileIndex);
                int tilesToReveal = Map.Fog?.CountFoggedAround(position.X, position.Y, scoutingDistance, PathColor) ?? 0;

                if (tilesToReveal == 0)
                {
                    continue;
                }

                if (nodeCost < bestCost || (nodeCost == bestCost && tilesToReveal > bestTilesToReveal))
                {
                    bestIndex = tileIndex;
                    bestCost = nodeCost;
                    bestTilesToReveal = tilesToReveal;
                }
            }

            return bestIndex;
        }

        int bestTileIndex = FindBestTile(tileIndex => Map.GetTile(tileIndex).IsFog(PathColor));

        if (bestTileIndex != -1)
        {
            return (bestTileIndex, true);
        }

        bestTileIndex = FindBestTile(tileIndex => GetNodeCost(tileIndex) == 0);

        return bestTileIndex != -1 ? (bestTileIndex, false) : (-1, false);
    }

    /// <summary>
    /// The closest tile worth moving to, <c>getNearestTileToMove()</c> (<c>world_pathfinding.cpp:968-1003</c>).
    /// </summary>
    /// <remarks>
    /// The direction order is shuffled on purpose: a hero that cannot reach anything worthwhile would otherwise
    /// step the same way every turn. Action objects are skipped because they may be a guarded castle or dwelling.
    /// </remarks>
    /// <param name="hero">The hero to move.</param>
    /// <returns>The tile to move to, or -1 when the hero has nowhere to go.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public int GetNearestTileToMove(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        ReEvaluateIfNeeded(hero);

        int start = hero.MapIndex;
        Direction[] directions = (Direction[])Directions.AllNeighboringDirections.Clone();
        Rand.Shuffle(directions);

        foreach (Direction direction in directions)
        {
            if (!Geometry.IsValidDirection(start, direction))
            {
                continue;
            }

            int newIndex = Geometry.GetDirectionIndex(start, direction);

            if (newIndex == start || Mp2ObjectTypes.IsInGameActionObject(Map.GetTile(newIndex).MainObjectType))
            {
                continue;
            }

            // Reachable in one move, and the hero has enough army to beat whatever guards it.
            if (GetNodeCost(newIndex) > 0 && GetNodeFrom(newIndex) == start)
            {
                return newIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether a friendly hero is in the way, <c>isHeroPossiblyBlockingWay()</c>
    /// (<c>world_pathfinding.cpp:1005-1160</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference encodes a handful of patterns, each of them a case where the hero stands between two parts of
    /// the map that would otherwise be connected: a corridor in either axis, the mouth of a corner, and the tiles
    /// around stone liths and whirlpools. The eleven cases below are copied one to one.
    /// </para>
    /// <para>
    /// The last two questions are about the stone liths themselves: a hero may block the exit another hero would
    /// teleport to, and a hero standing on the liths can be teleported himself.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that would walk.</param>
    /// <returns><see langword="true"/> when a friendly hero stands in the way.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public bool IsHeroPossiblyBlockingWay(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        int heroIndex = hero.MapIndex;
        PlayerColor heroColor = hero.Color;

        bool IsReachableDirection(Direction direction) =>
            Geometry.IsValidDirection(heroIndex, direction)
            && IsMovementAllowedForColor(heroIndex, direction, heroColor, ignoreFog: false, isSummonBoatSpellAvailable: false);

        bool left = IsReachableDirection(Direction.Left);
        bool right = IsReachableDirection(Direction.Right);
        bool top = IsReachableDirection(Direction.Top);
        bool bottom = IsReachableDirection(Direction.Bottom);
        bool topLeft = IsReachableDirection(Direction.TopLeft);
        bool topRight = IsReachableDirection(Direction.TopRight);
        bool bottomLeft = IsReachableDirection(Direction.BottomLeft);
        bool bottomRight = IsReachableDirection(Direction.BottomRight);

        // A dead straight corridor: reachable on two opposite sides, closed on the other two.
        if ((top && bottom && !left && !right) || (left && right && !top && !bottom))
        {
            return true;
        }

        // The mouth of a corner: the closed diagonal is the obstacle, the closed straight side is the map edge.
        if ((left && (top || right) && !topLeft && !bottom)
            || (right && (top || left) && !topRight && !bottom)
            || (left && (bottom || right) && !top && !bottomLeft)
            || (right && (bottom || left) && !top && !bottomRight)
            || (left && (top || bottom || right) && !topLeft && !bottomLeft)
            || (top && (bottom || right || left) && !topLeft && !topRight)
            || (right && (top || bottom || left) && !topRight && !bottomRight)
            || (bottom && (top || left || right) && !bottomLeft && !bottomRight)
            || (top && (right || bottom) && !topRight && !left)
            || (bottom && (right || top) && !bottomRight && !left)
            || (top && (left || bottom) && !topLeft && !right)
            || (bottom && (left || top) && !bottomLeft && !right))
        {
            return true;
        }

        Tile heroTile = Map.GetTile(heroIndex);

        // A hero in a boat can neither occupy nor block stone liths.
        if (heroTile.IsWater)
        {
            return false;
        }

        // A hero may block the exit another hero would teleport to.
        foreach (int lithsIndex in Map.ScanAroundObject(heroIndex, Mp2ObjectType.ActionStoneLiths))
        {
            Tile tile = Map.GetTile(lithsIndex);

            if (tile.MainObjectType == Mp2ObjectType.ActionHero)
            {
                Direction direction = Geometry.GetDirection(lithsIndex, heroIndex);

                if (tile.IsPassableTo(direction) && heroTile.IsPassableFrom(Directions.Reflect(direction)))
                {
                    return true;
                }
            }
        }

        // A hero standing on the stone liths can be teleported himself.
        return heroTile.GetMainObjectType(Map, ignoreObjectUnderHero: false) == Mp2ObjectType.ActionStoneLiths;
    }

    /// <summary>
    /// A route that uses Dimension Door, <c>buildDimensionDoorPath()</c> (<c>world_pathfinding.cpp:1216-1343</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every cast jumps as far towards the target as the spell allows, at most <c>distance / 2</c> in either axis;
    /// when that landing tile is not usable the best neighbouring tile is taken instead. The number of casts is
    /// bounded by three things: the spell points left after the reserve, the movement points (each door costs the
    /// same as walking), and the difficulty cap on doors per turn.
    /// </para>
    /// <para>
    /// The reserve is not subtracted when the target itself gives spell points back ({magic well},
    /// {artesian spring}), exactly as in the reference: arriving there pays for the trip.
    /// </para>
    /// <para>
    /// Only the <i>fixed</i> part of the route is returned — the hops the search pre-planned. The last, short
    /// stretch is the ordinary path, which the caller adds by finding the last node and asking for its route.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that would cast it.</param>
    /// <param name="targetIndex">Index of the destination tile.</param>
    /// <returns>The route, or an empty list when the door cannot get there.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hero"/> is <see langword="null"/>.</exception>
    public List<RouteStep> BuildDimensionDoorPath(Hero hero, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        ReEvaluateIfNeeded(hero);

        if (!_isDimensionDoorSpellAvailable || PathStart == targetIndex)
        {
            return [];
        }

        if (_isOnPatrol
            && Geometry.GetApproximateDistance(targetIndex, _patrolCenter) > _patrolDistance)
        {
            return [];
        }

        uint difficultyLimit = Difficulty.GetDimensionDoorLimitForAi(Settings.Get().GameDifficulty);

        if (_dimensionDoorNumOfUses >= difficultyLimit)
        {
            return [];
        }

        difficultyLimit -= _dimensionDoorNumOfUses;

        uint remainingSpellPoints = _remainingSpellPoints;

        // The reserve is only kept when the target does not give the spell points back.
        if (Map.GetTile(targetIndex).MainObjectType is not (Mp2ObjectType.ActionMagicWell or Mp2ObjectType.ActionArtesianSpring))
        {
            if (remainingSpellPoints < _maxSpellPoints * _spellPointsReserveRatio)
            {
                return [];
            }

            remainingSpellPoints -= (uint)(_maxSpellPoints * _spellPointsReserveRatio);
        }

        if (!IsTileAccessibleForAI(targetIndex))
        {
            return [];
        }

        uint dimensionDoorMovementCost = new Spell(SpellType.DimensionDoor).MovePoints();

        if (dimensionDoorMovementCost == 0 || _dimensionDoorSpCost == 0)
        {
            // A door that costs nothing would loop forever: the reference asserts both are not zero.
            return [];
        }

        int distanceLimit = (int)(Spell.DimensionDoorDistance() / 2);
        bool isHeroOnWater = Map.GetTile(PathStart).IsWater;
        uint maxCasts = Math.Min(
            Math.Min(remainingSpellPoints / _dimensionDoorSpCost, PathRemainingMovePoints / dimensionDoorMovementCost),
            difficultyLimit);

        Point targetPoint = Geometry.GetPoint(targetIndex);
        Point current = Geometry.GetPoint(PathStart);

        List<RouteStep> path = [];

        for (uint spellsUsed = 0; spellsUsed < maxCasts; ++spellsUsed)
        {
            int currentNodeIndex = Geometry.GetIndexFromAbsPoint(current);
            Point differencePoint = new(targetPoint.X - current.X, targetPoint.Y - current.Y);

            Point next = new(
                current.X + (differencePoint.X > 0 ? Math.Min(differencePoint.X, distanceLimit) : Math.Max(differencePoint.X, -distanceLimit)),
                current.Y + (differencePoint.Y > 0 ? Math.Min(differencePoint.Y, distanceLimit) : Math.Max(differencePoint.Y, -distanceLimit)));

            int nextNodeIndex = Geometry.GetIndexFromAbsPoint(next);

            if (Map.IsValidForDimensionDoor(nextNodeIndex, isHeroOnWater))
            {
                path.Add(new RouteStep(nextNodeIndex, currentNodeIndex, Direction.Center, dimensionDoorMovementCost));

                current = next;
            }
            else
            {
                int bestNextIndex = -1;
                int bestDirectionDifference = int.MaxValue;

                foreach (Direction direction in Directions.AllNeighboringDirections)
                {
                    if (!Geometry.IsValidDirection(nextNodeIndex, direction))
                    {
                        continue;
                    }

                    int newIndex = Geometry.GetDirectionIndex(nextNodeIndex, direction);

                    if (!Map.IsValidForDimensionDoor(newIndex, isHeroOnWater))
                    {
                        continue;
                    }

                    // Near the destination but unable to step onto it: skip that direction.
                    if (nextNodeIndex == targetIndex
                        && !IsMovementAllowedForColor(newIndex, Directions.Reflect(direction), PathColor, ignoreFog: true, isSummonBoatSpellAvailable: _isSummonBoatSpellAvailable))
                    {
                        continue;
                    }

                    Point newPoint = Geometry.GetPoint(newIndex);
                    int directionDifference = Math.Abs(current.X - newPoint.X) + Math.Abs(current.Y - newPoint.Y);

                    if (Math.Abs(current.X - newPoint.X) > distanceLimit || Math.Abs(current.Y - newPoint.Y) > distanceLimit)
                    {
                        continue;
                    }

                    if (bestNextIndex < 0 || bestDirectionDifference > directionDifference)
                    {
                        bestNextIndex = newIndex;
                        bestDirectionDifference = directionDifference;
                    }
                }

                if (bestNextIndex == -1)
                {
                    return [];
                }

                path.Add(new RouteStep(bestNextIndex, currentNodeIndex, Direction.Center, dimensionDoorMovementCost));

                current = Geometry.GetPoint(bestNextIndex);
            }

            differencePoint = new Point(targetPoint.X - current.X, targetPoint.Y - current.Y);

            if (Math.Abs(differencePoint.X) <= 1 && Math.Abs(differencePoint.Y) <= 1)
            {
                return path;
            }
        }

        return [];
    }

    /// <summary>
    /// The tiles worth stopping at on the way, <c>getObjectsOnTheWay()</c> (<c>world_pathfinding.cpp:1162-1214</c>).
    /// </summary>
    /// <remarks>
    /// The walk goes from the target back to the hero through the search cache, and every tile of it is asked
    /// whether it holds something valuable for the kingdom. The target itself is skipped: it is what the route
    /// was built for and is counted by the caller.
    /// </remarks>
    /// <param name="targetIndex">Index of the destination tile.</param>
    /// <returns>One entry per object worth a stop, with the type the tile holds.</returns>
    public List<(int Index, Mp2ObjectType ObjectType)> GetObjectsOnTheWay(int targetIndex)
    {
        var result = new List<(int Index, Mp2ObjectType ObjectType)>();

        if (GetNodeCost(targetIndex) == 0)
        {
            return result;
        }

        GameKingdom kingdom = Map.GetKingdom(PathColor);
        HashSet<int> uniqueIndices = [];

        void ValidateAndAdd(int index)
        {
            if (!uniqueIndices.Add(index))
            {
                return;
            }

            Mp2ObjectType objectType = Map.GetTile(index).MainObjectType;

            if (AiCommon.IsValuableAdventureMapObject(kingdom, objectType, index))
            {
                result.Add((index, objectType));
            }
        }

        // The target itself is counted by the caller, so it is not added here.
        uniqueIndices.Add(targetIndex);

        int currentNode = targetIndex;

        while (currentNode != PathStart)
        {
            int from = GetNodeFrom(currentNode);

            if (from == -1)
            {
                // The cache was cleared from under us: the route is unusable.
                return result;
            }

            ValidateAndAdd(currentNode);

            currentNode = from;
        }

        return result;
    }

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
