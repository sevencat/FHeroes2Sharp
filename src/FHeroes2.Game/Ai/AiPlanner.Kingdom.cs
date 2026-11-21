using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.System;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The kingdom side of the strategic AI, <c>ai_planner_kingdom.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// What one kingdom does with its turn: which of its heroes is a champion, a courier, a scout, a fighter or a plain
/// hunter; which enemy armies and which objects are worth walking to; how safe each region of the map is; which castle
/// gets the heroes first; and who the newest hero is.
/// </para>
/// <para>
/// The turn loop itself (<c>KingdomTurn()</c>) still belongs to the hero side of the planner
/// (<c>ai_planner_hero.cpp</c>, batch B5), so the scan of the map that the loop runs on is a member of its own,
/// <see cref="ScanMap"/>, and <c>KingdomTurn()</c> calls it before it stops at the hero turn.
/// </para>
/// </remarks>
public sealed partial class AiPlanner
{
    /// <summary>
    /// One hero's worth of numbers while the roles are handed out, <c>HeroValue</c>
    /// (<c>ai_planner_kingdom.cpp:70-83</c>).
    /// </summary>
    /// <param name="Hero">The hero being ranked.</param>
    /// <param name="Strength">Strength of the hero's army.</param>
    /// <param name="Stats">Sum of the hero's primary and secondary skills.</param>
    private readonly record struct HeroValue(Hero Hero, double Strength, int Stats);

    /// <summary>
    /// Takes the heroes off their tiles for as long as it lives, <c>TemporaryHeroEraser</c>
    /// (<c>ai_planner_kingdom.cpp:85-128</c>).
    /// </summary>
    /// <remarks>
    /// The scan of the map asks every tile what is on it, and a hero's tile answers "a hero" only because the tile
    /// remembers him; the reference erases the heroes first so that the objects underneath become visible again and
    /// restores them when the scan is over.
    /// </remarks>
    private sealed class TemporaryHeroEraser : IDisposable
    {
        private readonly List<Hero> _heroes = [];

        /// <summary>Takes every hero of <paramref name="heroes"/> off the tile he stands on.</summary>
        /// <param name="heroes">The heroes to erase.</param>
        public TemporaryHeroEraser(IReadOnlyList<Hero> heroes)
        {
            World world = World.Current;

            foreach (Hero hero in heroes)
            {
                Tile tile = world.GetTile(hero.MapIndex);

                if (tile.GetHero(world) is null)
                {
                    // A hero who is on his way has already left the tile he is walking away from.
                    continue;
                }

                _heroes.Add(hero);
                tile.SetHero(world, null);
            }
        }

        /// <summary>Puts the heroes back on their tiles.</summary>
        public void Dispose()
        {
            World world = World.Current;

            foreach (Hero hero in _heroes)
            {
                world.GetTile(hero.MapIndex).SetHero(world, hero);
            }
        }
    }

    /// <summary>
    /// Hires a hero in a castle, <c>Planner::recruitHero()</c> (<c>ai_planner_kingdom.cpp:273-321</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two heroes the tavern offers are compared by <see cref="Hero.GetRecruitValue"/> and the better one is hired;
    /// a hero named by a victory or a loss condition is never hired, because the map's own conditions would then
    /// change hands.
    /// </para>
    /// <para>
    /// The reference takes the kingdom from the castle; the port has no owner back pointer, so the kingdom is found
    /// through the world by the castle's colour.
    /// </para>
    /// </remarks>
    /// <param name="castle">The castle to hire in.</param>
    /// <param name="buyArmy">Whether the new hero should get troops as well.</param>
    /// <returns>Whether a hero was hired.</returns>
    private bool RecruitHero(CastleTown castle, bool buyArmy)
    {
        World world = World.Current;
        GameKingdom kingdom = world.GetKingdom(castle.Color);
        Recruits recruits = kingdom.GetRecruits();

        Hero? heroesCondWins = world.GetHeroesCondWins();
        Hero? heroesCondLoss = world.GetHeroesCondLoss();

        Hero? UseIfPossible(Hero? hero) =>
            hero is null || ReferenceEquals(hero, heroesCondWins) || ReferenceEquals(hero, heroesCondLoss) ? null : hero;

        Hero? firstRecruit = UseIfPossible(recruits.GetHero1(world.Heroes));
        Hero? secondRecruit = UseIfPossible(recruits.GetHero2(world.Heroes));
        Hero? guestHero = AiCommon.FindGuestHero(world, castle);

        Hero? recruit = null;

        if (firstRecruit is not null && secondRecruit is not null)
        {
            recruit = secondRecruit.GetRecruitValue() > firstRecruit.GetRecruitValue()
                ? castle.RecruitHero(secondRecruit, kingdom, world, guestHero)
                : castle.RecruitHero(firstRecruit, kingdom, world, guestHero);
        }
        else if (firstRecruit is not null)
        {
            recruit = castle.RecruitHero(firstRecruit, kingdom, world, guestHero);
        }
        else if (secondRecruit is not null)
        {
            recruit = castle.RecruitHero(secondRecruit, kingdom, world, guestHero);
        }

        if (recruit is null)
        {
            return false;
        }

        if (buyArmy)
        {
            ReinforceCastle(castle);
        }
        else
        {
            AiCommon.OptimizeTroopsOrder(recruit.Army);
        }

        return true;
    }

    /// <summary>
    /// Hands out the roles of the kingdom's heroes, <c>setHeroRoles()</c> (<c>ai_planner_kingdom.cpp:130-216</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A patrolling hero can only be a fighter; the rest are ranked twice, once by skill to pick the champion, the
    /// courier and the scout out of a large kingdom, and once by army strength to split the remainder into fighters
    /// (more than three times the median strength) and hunters.
    /// </para>
    /// <para>
    /// On the easiest difficulty every hero is a hunter — the reference lets the roles lag behind the difficulty
    /// because a weak AI should not concentrate its forces.
    /// </para>
    /// </remarks>
    /// <param name="heroes">The kingdom's heroes.</param>
    /// <param name="difficulty">The difficulty of the game.</param>
    internal static void SetHeroRoles(IReadOnlyList<Hero> heroes, GameDifficulty difficulty)
    {
        if (heroes.Count == 0)
        {
            return;
        }

        if (!Difficulty.AreAiHeroRolesAllowed(difficulty))
        {
            foreach (Hero hero in heroes)
            {
                hero.AiRole = HeroRole.Hunter;
            }

            return;
        }

        Hero? valuableHero = World.Current.GetHeroesCondWins();

        if (heroes.Count == 1)
        {
            heroes[0].AiRole = ReferenceEquals(valuableHero, heroes[0]) ? HeroRole.Champion : HeroRole.Hunter;

            return;
        }

        List<HeroValue> heroList = [];

        foreach (Hero hero in heroes)
        {
            // A hero set on patrol can only fight; he is no yardstick for the rest of the kingdom.
            if (hero.Modes.HasFlag(HeroModes.Patrol))
            {
                hero.AiRole = HeroRole.Fighter;
            }
            else
            {
                heroList.Add(new HeroValue(hero, hero.Army.GetStrength(), hero.GetStatsValue()));
            }
        }

        if (heroList.Count == 0)
        {
            return;
        }

        if (heroList.Count > 3)
        {
            heroList.Sort((first, second) => second.Stats.CompareTo(first.Stats));

            if (valuableHero is null)
            {
                heroList[0].Hero.AiRole = HeroRole.Champion;
                heroList.RemoveAt(0);
            }

            // The weakest hero by skill becomes the courier, and one more the scout: neither is counted towards the
            // median strength below, because neither is meant to fight.
            heroList[^1].Hero.AiRole = HeroRole.Courier;
            heroList.RemoveAt(heroList.Count - 1);

            if (heroList.Count > 2)
            {
                heroList[^1].Hero.AiRole = HeroRole.Scout;
                heroList.RemoveAt(heroList.Count - 1);
            }
        }

        heroList.Sort((first, second) => second.Strength.CompareTo(first.Strength));

        double medianStrength = heroList[heroList.Count / 2].Strength;

        foreach (HeroValue value in heroList)
        {
            if (valuableHero is not null && ReferenceEquals(value.Hero, valuableHero))
            {
                value.Hero.AiRole = HeroRole.Champion;

                continue;
            }

            value.Hero.AiRole = value.Strength > medianStrength * 3 ? HeroRole.Fighter : HeroRole.Hunter;
        }
    }

    /// <summary>
    /// Reads the army standing on a tile, <c>getEnemyArmyOnTile()</c> (<c>ai_planner_kingdom.cpp:218-270</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hero that belongs to the kingdom, a hero on patrol who has nowhere to patrol to, a neutral town and a town
    /// that cannot become a castle are all worth nothing: none of them can come after the kingdom.
    /// </para>
    /// <para>
    /// The strength is deliberately rough — a hero in a castle counts his garrison on top of his army, and a town
    /// counts as an army that can move 1500 points, slightly more than a fresh hero.
    /// </para>
    /// </remarks>
    /// <param name="kingdomColor">The colour of the kingdom that is looking.</param>
    /// <param name="tile">The tile to read.</param>
    /// <returns>The army, or <see langword="null"/> when there is nothing to fear on the tile.</returns>
    private static EnemyArmy? GetEnemyArmyOnTile(PlayerColor kingdomColor, Tile tile)
    {
        World world = World.Current;
        // The tile stores the action value (a hero tile is ActionHero); the port compares the folded one (ADR-133).
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(world));
        int tileIndex = tile.Index;

        if (objectType == Mp2ObjectType.Hero)
        {
            Hero? hero = tile.GetHero(world);

            if (hero is null)
            {
                return null;
            }

            if (hero.IsFriends(kingdomColor))
            {
                return null;
            }

            // A hero who stands still is no threat, and neither is the castle he stands in.
            if (hero.Modes.HasFlag(HeroModes.Patrol) && hero.PatrolDistance == 0)
            {
                return null;
            }

            CastleTown? castle = world.GetCastleEntrance(hero.MapIndex);
            double threat = castle is not null
                ? castle.Army.GetStrength() + hero.Army.GetStrength()
                : hero.Army.GetStrength();

            return new EnemyArmy(tileIndex, hero, threat, hero.GetMaxMovePoints());
        }

        if (objectType == Mp2ObjectType.Castle)
        {
            CastleTown? castle = world.GetCastleEntrance(tileIndex);

            if (castle is null)
            {
                return null;
            }

            // A neutral town hires no heroes, so it cannot come after the kingdom.
            if (castle.Color == PlayerColor.None || castle.Color == kingdomColor)
            {
                return null;
            }

            // A tent that cannot become a castle hires nobody either.
            if (!castle.IsCastle && !CanBecomeCastle(castle))
            {
                return null;
            }

            return new EnemyArmy(tileIndex, null, castle.Army.GetStrength(), 1500);
        }

        return null;
    }

    /// <summary>
    /// Whether a town could be turned into a castle, <c>Castle::AllowBuyBuilding( BUILD_CASTLE )</c>
    /// (<c>castle.h:316</c>).
    /// </summary>
    /// <remarks>
    /// The reference's question does not involve money; the port folded the price into
    /// <see cref="Castle.CheckBuyBuilding"/>, so the price itself is offered as the budget, which makes the money
    /// check pass and leaves the other two answers — already built, requirements met — untouched.
    /// </remarks>
    /// <param name="castle">The town to ask about.</param>
    /// <returns>Whether the town may become a castle.</returns>
    private static bool CanBecomeCastle(CastleTown castle) =>
        castle.CheckBuyBuilding(BuildingType.Castle, CastleBuildings.GetCost(BuildingType.Castle, castle.Race))
        == BuildingStatus.AllowBuild;

    /// <summary>
    /// Walks the visible map once and fills the per-turn caches, the opening of <c>Planner::KingdomTurn()</c>
    /// (<c>ai_planner_kingdom.cpp:681-788</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference runs this inline in the turn loop; the port keeps it apart because the loop itself
    /// (<c>KingdomTurn()</c>) belongs to the hero side of the planner (batch B5) and this is the half that knows about
    /// regions. What the scan finds ends up in <see cref="_mapActionObjects"/>, <see cref="_enemyArmies"/> and
    /// <see cref="_regions"/>, so <see cref="EvaluateRegionSafety"/> and <see cref="UpdateKingdomBudget"/> are called
    /// from here rather than being left to the turn loop.
    /// </para>
    /// <para>
    /// A tile under the fog is skipped. The reference lets a hero cast View All to scan the whole map instead; the
    /// port has no adventure-map spell casting yet (ADR-180), so the fog always wins and the hero who would cast the
    /// spell is not tracked. The sleeper flag and the dimension door uses the reference resets for every hero have no
    /// counterpart in the port either, so only the count of heroes that are not on patrol is taken from that loop.
    /// </para>
    /// <para>
    /// Object types are folded with <see cref="Mp2ObjectTypes.NonActionType"/> before they are compared (ADR-133): the
    /// tile stores the action value, the port compares against the non-action one. The cache itself keeps the value the
    /// tile holds, which is what <see cref="UpdateMapActionObjectCache"/> stores as well.
    /// </para>
    /// </remarks>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <param name="ignoreFog">
    /// Whether a hero of the kingdom has cast the View All spell this turn, <c>isUnderViewSpell</c> of the
    /// reference: the whole map is visible, so the fog does not hide anything.
    /// </param>
    /// <returns>The number of heroes that are not on patrol, <c>availableHeroCount</c> of the reference.</returns>
    internal int ScanMap(GameKingdom kingdom, bool ignoreFog = false)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        World world = World.Current;
        PlayerColor myColor = kingdom.Color;
        IReadOnlyList<Hero> heroes = kingdom.Heroes;

        ResetTurnCaches();

        // `_regions.resize( world.getRegionCount() )`: the ids the tiles carry index straight into it.
        for (int region = 0; region < world.RegionCount; ++region)
        {
            _regions.Add(new RegionStats());
        }

        int availableHeroCount = 0;

        foreach (Hero hero in heroes)
        {
            if (!hero.Modes.HasFlag(HeroModes.Patrol))
            {
                ++availableHeroCount;
            }
        }

        int mapSize = world.Width * world.Height;

        for (int index = 0; index < mapSize; ++index)
        {
            Tile tile = world.GetTile(index);
            uint regionId = tile.Region;

            if (regionId >= (uint)_regions.Count)
            {
                // A tile that belongs to no region: cut-off water, a blocked whirlpool exit.
                continue;
            }

            if (!ignoreFog && tile.IsFog(myColor))
            {
                continue;
            }

            Mp2ObjectType tileObjectType = tile.GetMainObjectType(world);

            if (!AiCommon.IsValuableAdventureMapObject(kingdom, tileObjectType, index))
            {
                continue;
            }

            _mapActionObjects[index] = tileObjectType;

            RegionStats stats = _regions[(int)regionId];
            Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tileObjectType);

            if (objectType == Mp2ObjectType.Hero)
            {
                Hero? hero = tile.GetHero(world);

                if (hero is not null && hero.IsFriends(myColor) && !hero.Modes.HasFlag(HeroModes.Patrol))
                {
                    ++stats.FriendlyHeroes;

                    // Secondary-skill levels run 0..3, so the spells the region allows run 2..5.
                    int wisdomLevel = (int)hero.GetLevelSkill(SecondarySkill.Wisdom);

                    if (wisdomLevel + 2 > stats.SpellLevel)
                    {
                        stats.SpellLevel = wisdomLevel + 2;
                    }
                }

                // This hero can be in a castle: look through him.
                objectType = Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(world, false));
            }

            if (objectType == Mp2ObjectType.Castle)
            {
                CastleTown? castle = world.GetCastleEntrance(index);

                if (castle is not null)
                {
                    if (castle.Color == myColor)
                    {
                        ++stats.FriendlyCastles;
                    }
                    else if (castle.Color != PlayerColor.None)
                    {
                        ++stats.EnemyCastles;
                    }
                }
            }

            EnemyArmy? enemyArmy = GetEnemyArmyOnTile(myColor, tile);

            if (enemyArmy is not null)
            {
                _enemyArmies[index] = enemyArmy;

                if (stats.HighestThreat < enemyArmy.Strength)
                {
                    stats.HighestThreat = enemyArmy.Strength;
                }
            }
        }

        EvaluateRegionSafety();
        UpdateKingdomBudget(kingdom);

        return availableHeroCount;
    }

    /// <summary>
    /// The region snapshot <see cref="ScanMap"/> took, <c>_regions</c> (<c>ai_planner.h:170</c>).
    /// </summary>
    /// <remarks>
    /// 参考里 <c>_regions</c> 是私有成员，测试要断言扫描与安全度传播的结果只能从这一只读窗口读（ADR-180）。
    /// </remarks>
    internal IReadOnlyList<RegionStats> Regions => _regions;

    /// <summary>
    /// Works out how safe each region of the map is, <c>Planner::evaluateRegionSafety()</c>
    /// (<c>ai_planner_kingdom.cpp:323-392</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first pass gives every region a starting value from what the scan of the map counted in it: a region with
    /// both friendly and enemy castles, or one an enemy hero has walked into, is contested (-50); enemy territory is
    /// -100; a region held only by the kingdom is 100; and a region nobody holds is untouched (0) and waits to be
    /// valued by its neighbours.
    /// </para>
    /// <para>
    /// The second pass then spreads the values outward, one batch at a time: a region next to a valued one gains that
    /// value divided by the number of neighbours it has, so the safety of a region fades with distance from the
    /// castles that guarantee it. An island with no neighbours at all is counted as half again as safe, because a boat
    /// costs movement points to reach. The integer division is the reference's own: a value is meant to fade to zero.
    /// </para>
    /// </remarks>
    private void EvaluateRegionSafety()
    {
        World world = World.Current;
        List<(int Region, int Value)> regionsToCheck = [];
        int lastPositive = 0;

        for (int regionId = 0; regionId < _regions.Count; ++regionId)
        {
            RegionStats stats = _regions[regionId];

            if ((stats.FriendlyCastles > 0 && stats.EnemyCastles > 0) || (stats.HighestThreat > 0 && stats.EnemyCastles == 0))
            {
                // Contested space, or an enemy hero has walked into our region.
                stats.SafetyFactor = -50;
                stats.Evaluated = true;
                regionsToCheck.Add((regionId, -50));
            }
            else if (stats.EnemyCastles > 0)
            {
                // Straight up enemy territory.
                stats.SafetyFactor = -100;
                stats.Evaluated = true;
                regionsToCheck.Add((regionId, -100));
            }
            else if (stats.FriendlyCastles > 0)
            {
                // Our protected castle.
                stats.SafetyFactor = 100;
                stats.Evaluated = true;
                regionsToCheck.Add((regionId, 100));
                ++lastPositive;
            }
            else
            {
                stats.SafetyFactor = 0;
                stats.Evaluated = false;
            }
        }

        regionsToCheck.Sort((left, right) => right.Value.CompareTo(left.Value));

        int currentEntry = 0;
        int batchStart = 0;
        int batchEnd = lastPositive + 1;

        while (currentEntry < regionsToCheck.Count)
        {
            MapRegion region = world.GetRegion((uint)regionsToCheck[currentEntry].Region);
            int value = regionsToCheck[currentEntry].Value;

            foreach (uint secondaryId in region.Neighbours)
            {
                RegionStats adjacentStats = _regions[(int)secondaryId];

                if (!adjacentStats.Evaluated)
                {
                    adjacentStats.Evaluated = true;
                    regionsToCheck.Add(((int)secondaryId, adjacentStats.SafetyFactor));
                }

                if (adjacentStats.SafetyFactor != 0)
                {
                    value += adjacentStats.SafetyFactor / (int)region.NeighboursCount;
                }
            }

            // No neighbours means it is an island; they are usually safer (or more dangerous to explore) because
            // reaching them costs a boat.
            if (region.NeighboursCount == 0)
            {
                value = value * 3 / 2;
            }

            regionsToCheck[currentEntry] = (regionsToCheck[currentEntry].Region, value);

            if (currentEntry == batchEnd - 1)
            {
                // Apply the calculated values in batches, so that a region is judged by its neighbours' starting
                // values rather than by values that the same pass has just written.
                for (int index = batchStart; index < batchEnd; ++index)
                {
                    _regions[regionsToCheck[index].Region].SafetyFactor = regionsToCheck[index].Value;
                }

                batchStart = batchEnd;
                batchEnd = regionsToCheck.Count;
            }

            ++currentEntry;
        }
    }

    /// <summary>
    /// Ranks the castles by how much they need the heroes, <c>Planner::getSortedCastleList()</c>
    /// (<c>ai_planner_kingdom.cpp:394-429</c>).
    /// </summary>
    /// <param name="castles">The kingdom's castles.</param>
    /// <param name="castlesInDanger">Tile indexes of the castles that an enemy can reach this turn.</param>
    /// <returns>The castles, the ones that need help first.</returns>
    internal List<AiCastle> GetSortedCastleList(IReadOnlyList<CastleTown> castles, IReadOnlySet<int> castlesInDanger)
    {
        ArgumentNullException.ThrowIfNull(castles);
        ArgumentNullException.ThrowIfNull(castlesInDanger);

        World world = World.Current;
        List<AiCastle> sortedCastleList = new(castles.Count);

        foreach (CastleTown castle in castles)
        {
            int castleIndex = castle.TileIndex;
            int regionId = (int)world.GetTile(castleIndex).Region;

            sortedCastleList.Add(new AiCastle(
                castle,
                castlesInDanger.Contains(castleIndex),
                _regions[regionId].SafetyFactor,
                castle.GetBuildingValue()));
        }

        sortedCastleList.Sort((left, right) =>
        {
            if (!left.UnderThreat && !right.UnderThreat)
            {
                return right.SafetyFactor.CompareTo(left.SafetyFactor);
            }

            if (left.UnderThreat && !right.UnderThreat)
            {
                return -1;
            }

            if (!left.UnderThreat && right.UnderThreat)
            {
                return 1;
            }

            // Two castles in danger: the more valuable one in the safer region comes first, which is how the
            // reference multiplies one castle's value by the other castle's safety factor instead of dividing.
            double leftValue = (double)left.BuildingValue * right.SafetyFactor;
            double rightValue = (double)right.BuildingValue * left.SafetyFactor;

            return rightValue.CompareTo(leftValue);
        });

        return sortedCastleList;
    }

    /// <summary>
    /// Finds the castles an enemy can reach this turn, <c>Planner::findCastlesInDanger()</c>
    /// (<c>ai_planner_kingdom.cpp:431-460</c>).
    /// </summary>
    /// <remarks>
    /// The kingdom's own heroes are taken off the map first, because an enemy can only reach a castle the kingdom
    /// does not block the way to, and the pathfinder is asked with the army advantage an enemy would need at its
    /// lowest.
    /// </remarks>
    /// <param name="kingdom">The kingdom to check.</param>
    /// <returns>Tile indexes of the castles in danger.</returns>
    private HashSet<int> FindCastlesInDanger(GameKingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        HashSet<int> castlesInDanger = [];

        using TemporaryHeroEraser heroEraser = new(kingdom.Heroes);
        using AIWorldPathfinderStateRestorer pathfinderStateRestorer = new(Pathfinder);

        // The optimistic settings for an enemy army: the smallest army advantage and no spell points held back.
        Pathfinder.SetMinimalArmyStrengthAdvantage(AiPlannerInternals.ArmyAdvantageDesperate);
        Pathfinder.SetSpellPointsReserveRatio(0.0);

        foreach (EnemyArmy enemyArmy in _enemyArmies.Values)
        {
            foreach (CastleTown castle in kingdom.Castles)
            {
                if (UpdateIndividualPriorityForCastle(castle, enemyArmy))
                {
                    castlesInDanger.Add(castle.TileIndex);
                }
            }
        }

        return castlesInDanger;
    }

    /// <summary>
    /// Re-raises the tasks that answer an enemy army, <c>Planner::updatePriorityForEnemyArmy()</c>
    /// (<c>ai_planner_kingdom.cpp:462-483</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <param name="enemyArmy">The army that was found.</param>
    private void UpdatePriorityForEnemyArmy(GameKingdom kingdom, EnemyArmy enemyArmy)
    {
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(enemyArmy);

        using TemporaryHeroEraser heroEraser = new(kingdom.Heroes);
        using AIWorldPathfinderStateRestorer pathfinderStateRestorer = new(Pathfinder);

        Pathfinder.SetMinimalArmyStrengthAdvantage(AiPlannerInternals.ArmyAdvantageDesperate);
        Pathfinder.SetSpellPointsReserveRatio(0.0);

        foreach (CastleTown castle in kingdom.Castles)
        {
            UpdateIndividualPriorityForCastle(castle, enemyArmy);
        }
    }

    /// <summary>
    /// Re-raises the tasks that defend a castle, <c>Planner::updatePriorityForCastle()</c>
    /// (<c>ai_planner_kingdom.cpp:485-500</c>).
    /// </summary>
    /// <param name="castle">The castle to defend.</param>
    private void UpdatePriorityForCastle(CastleTown castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        GameKingdom kingdom = World.Current.GetKingdom(castle.Color);

        using TemporaryHeroEraser heroEraser = new(kingdom.Heroes);
        using AIWorldPathfinderStateRestorer pathfinderStateRestorer = new(Pathfinder);

        Pathfinder.SetMinimalArmyStrengthAdvantage(AiPlannerInternals.ArmyAdvantageDesperate);
        Pathfinder.SetSpellPointsReserveRatio(0.0);

        foreach (EnemyArmy enemyArmy in _enemyArmies.Values)
        {
            UpdateIndividualPriorityForCastle(castle, enemyArmy);
        }
    }

    /// <summary>
    /// Whether one enemy army makes a castle worth defending, <c>Planner::updateIndividualPriorityForCastle()</c>
    /// (<c>ai_planner_kingdom.cpp:502-564</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header warns not to call this directly: it only answers for one army at a time, and the callers that walk
    /// through all of them are <see cref="UpdatePriorityForEnemyArmy"/> and <see cref="UpdatePriorityForCastle"/>.
    /// </para>
    /// <para>
    /// The distance is measured from the castle owner's point of view on purpose: the owner can see both the castle
    /// and the army, while the army may not yet see the castle through the fog, and letting the AI use the army's own
    /// knowledge would be cheating. The army is counted as half as strong for every extra day it needs to arrive, so
    /// a threat four days away counts for nothing at all.
    /// </para>
    /// </remarks>
    /// <param name="castle">The castle to check.</param>
    /// <param name="enemyArmy">The army that could attack it.</param>
    /// <returns>Whether the castle is in danger from that army.</returns>
    private bool UpdateIndividualPriorityForCastle(CastleTown castle, EnemyArmy enemyArmy)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(enemyArmy);

        // 30 tiles, roughly how far a maxed out hero can move in a turn.
        const uint ThreatDistanceLimit = 3000;

        int castleIndex = castle.TileIndex;

        // Skip the precise distance check when the army is too far away to be a threat.
        if (World.Current.Geometry!.Value.GetApproximateDistance(enemyArmy.Index, castleIndex)
            * MapGrounds.FastestMovePenalty > ThreatDistanceLimit)
        {
            return false;
        }

        uint distance = Pathfinder.GetDistance(enemyArmy.Index, castleIndex, castle.Color, enemyArmy.Strength);

        if (distance == 0 || distance >= ThreatDistanceLimit)
        {
            return false;
        }

        uint daysToReach = (distance + enemyArmy.MovePoints - 1) / enemyArmy.MovePoints;

        // It is too far away. Ignore it.
        if (daysToReach > 3)
        {
            return false;
        }

        double enemyStrength = enemyArmy.Strength;

        --daysToReach;

        while (daysToReach > 0)
        {
            // Each day halves the enemy's strength: an army far away is no reason to panic.
            enemyStrength /= 2;
            --daysToReach;
        }

        if (_priorityTargets.TryGetValue(enemyArmy.Index, out PriorityTask? attackTask))
        {
            attackTask.SecondaryTaskTileIndexes.Add(castleIndex);
        }
        else
        {
            _priorityTargets[enemyArmy.Index] = new PriorityTask(PriorityTaskType.Attack, castleIndex);
        }

        if (_priorityTargets.TryGetValue(castleIndex, out PriorityTask? defendTask))
        {
            defendTask.SecondaryTaskTileIndexes.Add(enemyArmy.Index);
        }
        else
        {
            _priorityTargets[castleIndex] = new PriorityTask(PriorityTaskType.Defend, enemyArmy.Index);
        }

        // The garrison and the guest hero together are weaker than the enemy: the castle has to be defended.
        if (castle.GetGarrisonStrength(AiCommon.FindGuestHero(World.Current, castle), enemyArmy.Hero, false) < enemyStrength)
        {
            return true;
        }

        // The guest hero could not beat the enemy in the open field either, so it should probably stay in.
        Hero? guestHero = AiCommon.FindGuestHero(World.Current, castle);

        return guestHero is not null && guestHero.Army.GetStrength() <= enemyStrength * AiPlannerInternals.ArmyAdvantageSmall;
    }

    /// <summary>
    /// Drops a task that is done, <c>Planner::removePriorityAttackTarget()</c>
    /// (<c>ai_planner_kingdom.cpp:566-601</c>).
    /// </summary>
    /// <remarks>
    /// The task at <paramref name="tileIndex"/> is an attack, and the tasks it points at are the defences of the
    /// castles being attacked; a castle is only dropped once no other army threatens it any more.
    /// </remarks>
    /// <param name="tileIndex">Tile index of the target.</param>
    private void RemovePriorityAttackTarget(int tileIndex)
    {
        if (!_priorityTargets.TryGetValue(tileIndex, out PriorityTask? attackTask))
        {
            return;
        }

        if (attackTask.Type != PriorityTaskType.Attack)
        {
            return;
        }

        foreach (int secondaryTaskIndex in attackTask.SecondaryTaskTileIndexes)
        {
            if (!_priorityTargets.TryGetValue(secondaryTaskIndex, out PriorityTask? defenseTask))
            {
                continue;
            }

            if (defenseTask.Type != PriorityTaskType.Defend)
            {
                continue;
            }

            // Check whether a secondary task is still present.
            defenseTask.SecondaryTaskTileIndexes.Remove(tileIndex);

            if (defenseTask.SecondaryTaskTileIndexes.Count == 0)
            {
                // Nobody else threatens this castle, so there is nothing left to defend.
                _priorityTargets.Remove(secondaryTaskIndex);
            }
        }

        _priorityTargets.Remove(tileIndex);
    }

    /// <summary>
    /// Adds a task for an enemy hero or creature standing on a tile,
    /// <c>Planner::updatePriorityAttackTarget()</c> (<c>ai_planner_kingdom.cpp:603-621</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <param name="tile">The tile to look at.</param>
    private void UpdatePriorityAttackTarget(GameKingdom kingdom, Tile tile)
    {
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(tile);

        int tileIndex = tile.Index;
        EnemyArmy? enemyArmy = GetEnemyArmyOnTile(kingdom.Color, tile);

        if (enemyArmy is null)
        {
            _enemyArmies.Remove(tileIndex);

            return;
        }

        _enemyArmies[tileIndex] = enemyArmy;

        UpdatePriorityForEnemyArmy(kingdom, enemyArmy);
    }

    /// <summary>
    /// Hires the heroes the kingdom can still afford, <c>Planner::purchaseNewHeroes()</c>
    /// (<c>ai_planner_kingdom.cpp:929-987</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The number of heroes the kingdom is allowed is two during the first four days of a one-castle game — enough to
    /// scout and to keep the castle — and afterwards one per 72 tiles of map width plus two, never fewer than the
    /// number of castles and never more than the maximum a kingdom may have.
    /// </para>
    /// <para>
    /// The castle chosen is the one with the most troops still to hire, skipping the ones that already have a hero,
    /// the ones in a region with more than one hero of the kingdom, and the ones in danger unless this would be the
    /// first hero.
    /// </para>
    /// </remarks>
    /// <param name="sortedCastleList">The castles, ranked by <see cref="GetSortedCastleList"/>.</param>
    /// <param name="castlesInDanger">Tile indexes of the castles that an enemy can reach this turn.</param>
    /// <param name="availableHeroCount">How many heroes the kingdom may still hire.</param>
    /// <param name="moreTasksForHeroes">Whether the turn still has tasks for another hero.</param>
    /// <returns>Whether a hero was hired.</returns>
    internal bool PurchaseNewHeroes(IReadOnlyList<AiCastle> sortedCastleList, IReadOnlySet<int> castlesInDanger, int availableHeroCount, bool moreTasksForHeroes)
    {
        ArgumentNullException.ThrowIfNull(sortedCastleList);
        ArgumentNullException.ThrowIfNull(castlesInDanger);

        World world = World.Current;
        bool isEarlyGameWithSingleCastle = world.DayCount < 5 && sortedCastleList.Count == 1;

        // `Maps::SMALL`, the side of the smallest map: the wider the map, the more heroes it takes to cover it.
        const int SmallMapSize = 72;

        int heroLimit = isEarlyGameWithSingleCastle ? 2 : (world.Width / SmallMapSize) + 2;

        if (heroLimit < sortedCastleList.Count)
        {
            // In most cases the number of heroes must at least be equal to the number of castles.
            heroLimit = sortedCastleList.Count;
        }

        if (heroLimit > GameKingdom.MaxHeroes)
        {
            // Make sure the number of heroes stays within what a kingdom may have.
            heroLimit = GameKingdom.MaxHeroes;
        }

        if (availableHeroCount >= heroLimit)
        {
            return false;
        }

        CastleTown? recruitmentCastle = null;
        double bestArmyAvailable = -1.0;

        // Look for the best castle to hire a hero from.
        foreach (AiCastle entry in sortedCastleList)
        {
            CastleTown castle = entry.Castle;

            if (!castle.IsCastle)
            {
                continue;
            }

            int castleIndex = castle.TileIndex;

            // Make sure there is no hero in the castle already, and that a castle in danger does not get one while
            // the kingdom has others.
            if (AiCommon.FindGuestHero(world, castle) is not null || (availableHeroCount > 0 && castlesInDanger.Contains(castleIndex)))
            {
                continue;
            }

            RegionStats stats = _regions[(int)world.GetTile(castleIndex).Region];

            if (stats.FriendlyHeroes > 1)
            {
                continue;
            }

            int neighboursCount = world.GetRegion((uint)world.GetTile(castleIndex).Region).NeighboursCount;

            // Don't buy another hero when there is nothing to do or the castle is on an island.
            if (stats.FriendlyHeroes > 0 && (!moreTasksForHeroes || (sortedCastleList.Count > 1 && neighboursCount == 0)))
            {
                continue;
            }

            double availableArmy = castle.GetArmyRecruitmentValue(world.GetKingdom(castle.Color).Treasury);

            if (recruitmentCastle is null || availableArmy > bestArmyAvailable)
            {
                recruitmentCastle = castle;
                bestArmyAvailable = availableArmy;
            }
        }

        // A castle was found: hire a hero in it.
        return recruitmentCastle is not null && RecruitHero(recruitmentCastle, !isEarlyGameWithSingleCastle);
    }
}
