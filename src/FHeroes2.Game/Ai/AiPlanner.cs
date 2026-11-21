using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using ArmyTroops = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Ai;

/// <summary>
/// What the strategic AI wants the game to do after it finished a kingdom's turn.
/// </summary>
/// <remarks>
/// The reference returns <c>fheroes2::GameMode</c> from <c>Planner::KingdomTurn()</c> and only ever produces two of
/// its values: <c>END_TURN</c> when the AI is done and <c>QUIT_GAME</c> when it gave up (the turn limit of an
/// auto battle, a battle it cannot win). The port has no <c>GameMode</c> yet - scenes decide what comes next
/// (<c>App/SceneKind.cs</c>) - so the two outcomes are spelled out here instead of returning a whole game state.
/// </remarks>
public enum AiTurnOutcome
{
    /// <summary>The kingdom's turn is over, <c>fheroes2::GameMode::END_TURN</c>.</summary>
    EndTurn = 0,

    /// <summary>The AI gives up, <c>fheroes2::GameMode::QUIT_GAME</c>.</summary>
    QuitGame = 1,
}

/// <summary>
/// The strategic AI, <c>AI::Planner</c> (<c>ai_planner.h:160-281</c>, <c>ai_planner*.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Mostly a contract.</b> Every member below exists so that the rest of the port can be written against the same
/// names the reference uses; the bodies throw <see cref="NotImplementedException"/> until the corresponding
/// <c>ai_planner*.cpp</c> file is ported (see <c>docs/DECISIONS.md</c>, ADR-161).
/// </para>
/// <para>
/// The reference is a Meyers singleton (<c>Planner::Get()</c>) whose state - the priority targets, the enemy armies,
/// the cached army strengths, the budget and the AI pathfinder - lives between the calls of one turn and is dropped
/// when the next turn starts. The port keeps the same shape: <see cref="Instance"/> is the singleton, the members
/// are instance methods exactly as in the header, and nothing here is saved.
/// </para>
/// <para>
/// <b>What already runs</b>, in the order <c>docs/planv3.md</c> ports it: the two inline helpers of the header, the
/// per-turn caches, the clean-up at the start of a turn, the guardian strength cache
/// (<see cref="GetTileArmyStrength"/>), the two resource weights (<see cref="GetResourcePriorityModifier"/> and
/// <see cref="GetFundsValueBasedOnPriority"/>), and the whole castle side of <c>ai_planner_castle.cpp</c>: what a
/// castle does before a battle, on a defensive turn, when it is reinforced, and what its turn puts on the kingdom's
/// budget. The kingdom and hero turns that call these, and the region evaluation the peaceful castle turn reads,
/// still throw.
/// </para>
/// </remarks>
public sealed partial class AiPlanner
{
    private AiPlanner()
    {
    }

    /// <summary>The one planner, <c>Planner::Get()</c>.</summary>
    public static AiPlanner Instance { get; } = new();

    private AIWorldPathfinder? _pathfinder;

    /// <summary>
    /// The pathfinder the AI asks for distances, refusing to guess when none was attached.
    /// </summary>
    /// <remarks>
    /// 参考里路径寻路器是全局单例，随时可用；端口按世界构造，所以由 <see cref="AttachPathfinder"/> 交进来。没交就直接
    /// 报错，而不是退回一个「大概安全」的判断——距离算错的 AI 会白白送掉英雄。
    /// </remarks>
    private AIWorldPathfinder Pathfinder =>
        _pathfinder ?? throw new InvalidOperationException("AiPlanner.Pathfinder — 尚未 AttachPathfinder（ADR-180）");

    /// <summary>
    /// Objects on the map whose value was already worked out this turn, <c>_mapActionObjects</c>
    /// (<c>ai_planner.h:267</c>).
    /// </summary>
    private readonly Dictionary<int, Mp2ObjectType> _mapActionObjects = [];

    /// <summary>What the kingdom's heroes should do this turn, <c>_priorityTargets</c> (<c>ai_planner.h:268</c>).</summary>
    private readonly Dictionary<int, PriorityTask> _priorityTargets = [];

    /// <summary>Enemy armies seen this turn, <c>_enemyArmies</c> (<c>ai_planner.h:269</c>).</summary>
    private readonly Dictionary<int, EnemyArmy> _enemyArmies = [];

    /// <summary>
    /// Strength of the armies guarding the tiles, <c>_tileArmyStrengthValues</c> (<c>ai_planner.h:274</c>): the same
    /// army is measured many times per turn and the measurement is expensive, so the answers are cached.
    /// </summary>
    private readonly Dictionary<int, double> _tileArmyStrengthValues = [];

    /// <summary>Safety of each region of the map this turn, <c>_regions</c> (<c>ai_planner.h:276</c>).</summary>
    private readonly List<RegionStats> _regions = [];

    /// <summary>
    /// How much of each resource the plans of this turn still need, <c>_budget</c> (<c>ai_planner.h:278</c>): one
    /// entry per resource, in the order of the header.
    /// </summary>
    private readonly BudgetEntry[] _budget =
    [
        new(Resource.Wood),
        new(Resource.Mercury),
        new(Resource.Ore),
        new(Resource.Sulfur),
        new(Resource.Crystal),
        new(Resource.Gems),
        new(Resource.Gold),
    ];

    /// <summary>
    /// Gives the planner the pathfinder it resets, the port's stand-in for <c>AIWorldPathfinder::Get()</c>.
    /// </summary>
    /// <remarks>
    /// The reference reaches its pathfinder through a global getter, which works because the reference has one
    /// world. The port builds the pathfinder against a <see cref="World"/>, so the object that owns the game
    /// hands it over here; until that happens <see cref="ResetPathfinder"/> has nothing to drop.
    /// </remarks>
    /// <param name="pathfinder">The pathfinder built for the current world.</param>
    /// <exception cref="ArgumentNullException">The pathfinder is null.</exception>
    public void AttachPathfinder(AIWorldPathfinder pathfinder)
    {
        ArgumentNullException.ThrowIfNull(pathfinder);

        _pathfinder = pathfinder;
    }

    /// <summary>
    /// Plays one turn of one kingdom, <c>Planner::KingdomTurn()</c> (<c>ai_planner_kingdom.cpp:623-927</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 一天的顺序：清掉昨天的计划 → 英雄醒来、放「全图视野」、扫描地图 → 挖宝 → 反复「给英雄派活 / 买新英雄 /
    /// 最后一天召回」直到没人有事可做 → 按危险程度开发城堡 → 把城里英雄的慢兵交给守军。
    /// </para>
    /// <para>
    /// <b>表现层的事不在这里</b>：参考会画状态栏进度、切电脑回合的背景音乐、以及在回合结束时释放 AI 自动控制的
    /// 玩家（<c>AIAutoControlModeCommitter</c>），这些都是 UI 与 Players 层的事。
    /// </para>
    /// </remarks>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <returns><see cref="AiTurnOutcome.EndTurn"/> when the turn is over, or the outcome that ended it.</returns>
    public AiTurnOutcome KingdomTurn(GameKingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        World world = World.Current;
        PlayerColor myColor = kingdom.Color;

        // 昨天的计划、敌人位置与区域安全度全部作废：一天里城堡与军队都可能变了。
        ResetTurnCaches();

        if (kingdom.IsLoss || myColor == PlayerColor.None)
        {
            // 参考在这里还会做 LossPostActions：那要 Players 层，端口还没有。
            return AiTurnOutcome.EndTurn;
        }

        IReadOnlyList<Hero> heroes = kingdom.Heroes;
        IReadOnlyList<CastleTown> castles = kingdom.Castles;

        // 新的一天：英雄都醒过来，次元门次数重新算。
        foreach (Hero hero in heroes)
        {
            hero.ResetModes(HeroModes.Sleeper);
            hero.DimensionDoorUses = 0;
        }

        // 有英雄能放全图视野就先放一个：扫描地图时就能无视迷雾，看到整张图。
        Hero? bestHeroToViewAll = null;

        foreach (Hero hero in heroes)
        {
            if (!hero.CanCastSpell(new Spell(SpellType.ViewAll)))
            {
                continue;
            }

            // 有神秘术的英雄能把法术点省下来，优先让他放。
            if (bestHeroToViewAll is null || hero.GetLevelSkill(SecondarySkill.Mysticism) > 0)
            {
                bestHeroToViewAll = hero;
            }
        }

        bool isUnderViewSpell = false;

        if (bestHeroToViewAll is not null)
        {
            bestHeroToViewAll.SpellCasted(new Spell(SpellType.ViewAll));
            isUnderViewSpell = true;
        }

        int availableHeroCount = ScanMap(kingdom, isUnderViewSpell);

        // 有英雄正站在终极宝物上，就得在这一天开始前把它挖出来。
        foreach (Hero hero in heroes)
        {
            UltimateArtifact ultimateArtifact = world.UltimateArtifact;

            if (!ultimateArtifact.IsAt(hero.MapIndex) || !AiCommon.IsUltimateArtifactAvailableToHero(ultimateArtifact, hero))
            {
                continue;
            }

            Debug.Assert(hero.MovePoints == hero.GetMaxMovePoints() && hero.Path.IsEmpty,
                "A hero who can dig for the artifact has not moved yet.");

            hero.SetMovePoints(0);

            if (!world.DiggingForUltimateArtifact(world.Geometry!.Value.GetPoint(hero.MapIndex))
                || !hero.PickupArtifact(ultimateArtifact.Artifact, world))
            {
                Debug.Fail("The ultimate artifact was available but could not be dug up.");
            }

            // 挖到终极宝物本身可能就是胜利条件：胜负判定在会话层，这里只管结束这一天。
            break;
        }

        HashSet<int> castlesInDanger = [];
        List<AiCastle> sortedCastleList = [];

        // 进度条从 1 走到 9；端口没有状态栏，这个数字只是传给 HeroesTurn 的刻度。
        uint currentProgressValue = 1;

        while (true)
        {
            // 城里的英雄多半没事干，先让他把守军与背包整理好。
            foreach (Hero hero in heroes)
            {
                HeroesActionComplete(hero, hero.MapIndex, Mp2ObjectType.None);
            }

            SetHeroRoles(heroes, Settings.Get().GameDifficulty);

            castlesInDanger = FindCastlesInDanger(kingdom);

            foreach (Hero hero in heroes)
            {
                if (!castlesInDanger.Contains(hero.MapIndex))
                {
                    continue;
                }

                // 危险城堡里的英雄多半打不过野外的敌人，让他守着城堡别出去。
                HeroesActionComplete(hero, hero.MapIndex, hero.ObjectTypeUnderHero);
            }

            sortedCastleList = GetSortedCastleList(castles, castlesInDanger);

            // 开局英雄少于一队时估计这一回合还会买一个，进度只涨两点。
            uint endProgressValue = currentProgressValue == 1
                ? Math.Min(((uint)heroes.Count * 2U) + 1U, 8U)
                : Math.Min(currentProgressValue + 2U, 9U);

            bool moreTasksForHeroes = false;

            AiTurnOutcome gameState = HeroesTurn(heroes, ref currentProgressValue, endProgressValue, ref moreTasksForHeroes);

            if (gameState != AiTurnOutcome.EndTurn)
            {
                return gameState;
            }

            if (PurchaseNewHeroes(sortedCastleList, castlesInDanger, availableHeroCount, moreTasksForHeroes))
            {
                ++availableHeroCount;

                UpdateMapActionObjectCache(kingdom, heroes[^1].MapIndex);

                // 新英雄还能做事，重新派一轮活。
                continue;
            }

            if (!moreTasksForHeroes && world.IsLastDay)
            {
                // 英雄都没事可做，又是本周最后一天：把他们召回空城堡过夜，明天就能拿到全部法术点与新兵。
                foreach (CastleTown castle in castles)
                {
                    if (world.GetTile(castle.TileIndex).GetHero(world) is not null)
                    {
                        continue;
                    }

                    if (_priorityTargets.TryAdd(castle.TileIndex, new PriorityTask(PriorityTaskType.Reinforce)))
                    {
                        moreTasksForHeroes = true;
                    }
                }

                if (moreTasksForHeroes)
                {
                    continue;
                }
            }

            break;
        }

        // 回合里新打下的城堡会让列表对不上，先重新排一次再开发。
        if (castles.Count != sortedCastleList.Count)
        {
            EvaluateRegionSafety();

            castlesInDanger = FindCastlesInDanger(kingdom);
            sortedCastleList = GetSortedCastleList(castles, castlesInDanger);
        }

        foreach (AiCastle entry in sortedCastleList)
        {
            CastleTurn(entry.Castle, entry.UnderThreat);
        }

        // 城里的英雄把最慢的兵交给守军：明天的行动力就按队伍里最快的兵算。
        foreach (Hero hero in heroes)
        {
            CastleTown? castle = world.GetCastleEntrance(hero.MapIndex);

            if (castle is null)
            {
                continue;
            }

            AiCommon.TransferSlowestTroopsToGarrison(hero, castle);
        }

        return AiTurnOutcome.EndTurn;
    }

    /// <summary>
    /// Drops whatever the AI pathfinder cached, <c>Planner::resetPathfinder()</c> (<c>ai_planner.h:176-179</c>).
    /// </summary>
    /// <remarks>
    /// The pathfinder is the one handed over by <see cref="AttachPathfinder"/>; nothing has been attached until
    /// the AI is set up for a game, and then this is the only place the cache is dropped between turns.
    /// <see cref="World.ResetPathfinder"/> resets the player-side one.
    /// </remarks>
    public void ResetPathfinder() => _pathfinder?.Reset();

    /// <summary>
    /// Reveals the fog around a tile for a kingdom, <c>Planner::revealFog()</c> (<c>ai_planner.h:181</c>,
    /// <c>ai_planner.cpp:47-72</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The AI learns about the map as its heroes scout, so every tile that comes out of the fog is offered here before
    /// the fog is cleared: only action objects are worth remembering, and only they can change the plans of the turn.
    /// </para>
    /// <para>
    /// A new object may be more valuable than the one a hero is already walking to, so the first hero who is moving has
    /// his route cut down to the step he is about to take — the same thing the mouse does when a route is dragged
    /// around. The reference stops at that hero; the port does too.
    /// </para>
    /// </remarks>
    /// <param name="tile">The tile that leaves the fog.</param>
    /// <param name="kingdom">The kingdom that sees it.</param>
    public void RevealFog(Tile tile, GameKingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(kingdom);

        if (!Mp2ObjectTypes.IsInGameActionObject(tile.MainObjectType))
        {
            return;
        }

        UpdateMapActionObjectCache(kingdom, tile.Index);
        UpdatePriorityAttackTarget(kingdom, tile);

        foreach (Hero hero in kingdom.Heroes)
        {
            if (hero.IsMoveEnabled)
            {
                hero.Path.Truncate();

                break;
            }
        }
    }

    /// <summary>
    /// Strength of the army guarding a tile, <c>Planner::getTileArmyStrength()</c> (<c>ai_planner.h:191-193</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference fills a temporary <c>Army</c> from the tile and measures it, so the result is valid for neutral
    /// monsters and dwelling guardians only - not for hero armies or garrisons.
    /// </para>
    /// <para>
    /// The cache is keyed by <see cref="Tile.Index"/>, which is assigned when a map is loaded. A world built by
    /// <see cref="World.GenerateUninitializedMap"/> leaves every index at zero, so a test world has to number its
    /// tiles first - otherwise every tile shares one entry.
    /// </para>
    /// </remarks>
    /// <param name="tile">The tile to measure.</param>
    /// <returns>The strength of the guarding army, <c>0</c> when nothing guards the tile.</returns>
    public double GetTileArmyStrength(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        if (_tileArmyStrengthValues.TryGetValue(tile.Index, out double strength))
        {
            return strength;
        }

        // Building an army is relatively expensive and the same tile is measured many times per turn.
        Army.Army tileArmy = new();
        tileArmy.SetFromTile(World.Current, tile);

        strength = tileArmy.GetStrength();
        _tileArmyStrengthValues[tile.Index] = strength;

        return strength;
    }

    /// <summary>
    /// Prepares a hero or captain for a battle, <c>Planner::HeroesPreBattle()</c>
    /// (<c>ai_planner_hero.cpp:3087-3092</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference takes <c>HeroBase&amp;</c> and only touches its army; the port has no such base, so the army
    /// itself is the parameter — whoever starts the battle has the army at hand.
    /// </para>
    /// <para>
    /// 只有主动进攻才重排兵种顺序：防守方的顺序是玩家／上一场战斗定下来的，不该被 AI 改动。
    /// </para>
    /// </remarks>
    /// <param name="army">The army of the hero or captain that is about to fight.</param>
    /// <param name="isAttacking">Whether it attacks.</param>
    public static void HeroesPreBattle(ArmyTroops army, bool isAttacking)
    {
        ArgumentNullException.ThrowIfNull(army);

        if (isAttacking)
        {
            AiCommon.OptimizeTroopsOrder(army);
        }
    }

    /// <summary>
    /// Prepares a castle for a battle, <c>Planner::CastlePreBattle()</c> (<c>ai_planner.h:196</c>,
    /// <c>ai_planner_castle.cpp:252</c>).
    /// </summary>
    /// <remarks>
    /// The garrison is folded into the guest hero's army only when a hero stands in the castle; a castle without
    /// one defends with its garrison alone and has nothing to arrange. The troops are only reordered when they
    /// actually moved, because the arrangement the attacker saw while marching in has to be the one it fights.
    /// </remarks>
    /// <param name="castle">The castle that is about to be attacked.</param>
    public static void CastlePreBattle(CastleTown castle)
    {
        World world = World.Current;
        Hero? guestHero = AiCommon.FindGuestHero(world, castle);

        if (guestHero is null)
        {
            return;
        }

        // The guest is the AI's own hero, so the reference's default argument makes it AI controlled.
        if (!guestHero.Army.ArrangeForCastleDefense(castle.Army, true))
        {
            return;
        }

        AiCommon.OptimizeTroopsOrder(guestHero.Army);
    }

    // -------------------------------------------------------------------------------------------------------------
    // 私有半边（ai_planner.h:200-281）：签名照抄参考，正文留到移植对应的 ai_planner_*.cpp 时再写（ADR-180）。
    // -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Plays the turn of one castle, <c>Planner::CastleTurn()</c> (<c>ai_planner.h:203</c>,
    /// <c>ai_planner_castle.cpp:516</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A castle that may be attacked this turn hires first and builds last: the troops have to be bought before the
    /// enemy takes the castle and hires them, and no dwelling is built because a dwelling the enemy captures would
    /// arm the enemy. Only defensive buildings go up, and only when the castle has someone to defend it.
    /// </para>
    /// <para>
    /// Every other castle develops along the build order of its race, gated by the safety and the spell level of the
    /// region it stands in. Both come from <see cref="_regions"/>, which <c>evaluateRegionSafety()</c> fills at the
    /// start of the turn — it is not ported yet (B4), so a peaceful castle turn is only reachable from there.
    /// </para>
    /// </remarks>
    /// <param name="castle">The castle whose turn it is.</param>
    /// <param name="defensiveStrategy">Whether the castle is expected to be attacked this turn.</param>
    internal void CastleTurn(CastleTown castle, bool defensiveStrategy)
    {
        World world = World.Current;

        if (defensiveStrategy)
        {
            ReinforceCastle(castle);

            // The garrison may be empty after the hero took everything with it; then there is nothing to defend
            // with and no point in paying for turrets.
            if (castle.GetActualArmy(AiCommon.FindGuestHero(world, castle)).GetCount() > 0)
            {
                Build(castle, DefensiveStructures);
                Build(castle, SupportingDefensiveStructures);
            }

            return;
        }

        RegionStats stats = _regions[(int)world.GetTile(castle.TileIndex).Region];

        CastleDevelopment(castle, stats.SafetyFactor, stats.SpellLevel);
    }

    /// <summary>
    /// Upgrades and hires as much as possible and hands the best troops to the guest hero,
    /// <c>Planner::reinforceCastle()</c> (<c>ai_planner.h:207</c>, <c>ai_planner_castle.cpp:314</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The castle buys one stack of every dwelling it has, strongest dwelling first, and pays for it by dismissing
    /// the weakest stack it owns when there is no free slot. A stack is only dismissed when it is weaker than the
    /// one being bought, so the army never gets worse.
    /// </para>
    /// <para>
    /// When the castle is safe and its garrison empty, half of the hero's best stack — or the whole of a single
    /// stack — is left behind, so that the castle is not taken by the next wandering monster. Which stack that is
    /// depends on the hero's role: a champion leaves its weakest stack, everyone else the strongest.
    /// </para>
    /// </remarks>
    /// <param name="castle">The castle to reinforce.</param>
    internal void ReinforceCastle(CastleTown castle)
    {
        World world = World.Current;
        GameKingdom kingdom = world.GetKingdom(castle.Color);

        Debug.Assert(CastleTown.IsControlAi(kingdom), "Only an AI castle is reinforced by the AI planner.");

        ref Funds treasury = ref kingdom.Treasury;

        ArmyTroops garrison = castle.Army;

        garrison.UpgradeTroops(castle, ref treasury);
        garrison.MergeSameMonsterTroops();

        Hero? guestHero = AiCommon.FindGuestHero(world, castle);

        if (guestHero is not null)
        {
            // The guest is the AI's own hero, so it is active, AI controlled and of the castle's colour.
            Debug.Assert(guestHero.IsActive, "The hero standing in a castle has to be active.");
            Debug.Assert(world.GetKingdom(guestHero.Color).IsControlAi, "The guest hero has to be AI controlled.");
            Debug.Assert(guestHero.Color == castle.Color, "The guest hero has to serve the castle's colour.");

            guestHero.Army.UpgradeTroops(castle, ref treasury);
            guestHero.Army.MergeSameMonsterTroops();
        }

        // The dwellings are tried from the seventh upgrade down, so an upgraded dwelling wins over its base: the
        // same gold buys a stronger stack.
        foreach (BuildingType dwelling in CastleDwellings)
        {
            if (!castle.IsBuild(dwelling))
            {
                continue;
            }

            MonsterType monster = DwellingMonsters.FromDwelling(castle.Race, dwelling);
            uint count = castle.GetRecruitLimit(monster, treasury);

            if (count == 0)
            {
                continue;
            }

            Troop troop = new(monster, count);

            if (RecruitMonster(castle, troop, ref treasury, guestHero))
            {
                continue;
            }

            Troop? weakestTroop = null;

            if (guestHero is not null)
            {
                ArmyTroops guestHeroArmy = guestHero.Army;

                // The hero has no free slot either, so free one by handing a garrison stack over to it.
                if (TryTransferGarrisonTroopToHero(castle, garrison, guestHero, guestHeroArmy))
                {
                    if (!RecruitMonster(castle, troop, ref treasury, guestHero))
                    {
                        Debug.Assert(false, "A slot was freed but the recruitment still failed.");
                    }

                    continue;
                }

                weakestTroop = guestHeroArmy.GetWeakestTroop();

                Debug.Assert(weakestTroop is not null, "An army without a free slot has at least one stack.");
            }

            {
                Troop? weakestGarrisonTroop = garrison.GetWeakestTroop();

                Debug.Assert(weakestGarrisonTroop is not null, "A garrison without a free slot has at least one stack.");

                // The strengths of the stacks themselves, without the statistics of the hero commanding them: the
                // hero's weakest stack is only dismissed instead of the garrison's when it is the weaker of the two.
                if (weakestTroop is null || weakestTroop.GetStrength() >= weakestGarrisonTroop.GetStrength())
                {
                    weakestTroop = weakestGarrisonTroop;
                }
            }

            if (weakestTroop.GetStrength() > troop.GetStrength())
            {
                continue;
            }

            weakestTroop.Reset();

            if (!RecruitMonster(castle, troop, ref treasury, guestHero))
            {
                Debug.Assert(false, "A stack was dismissed but the recruitment still failed.");
            }
        }

        if (guestHero is not null)
        {
            // Hand the best of what is left to the guest hero.
            guestHero.Army.JoinStrongestFromArmy(garrison);

            // A safe castle has to keep a garrison of its own, otherwise it falls to the first wandering stack.
            // The region is read last so that a garrison that is still manned needs no evaluation at all; filling
            // the regions themselves is B4's job.
            if (castle.IsCastle && !garrison.IsValid && _regions[(int)world.GetTile(castle.TileIndex).Region].SafetyFactor <= 100)
            {
                (Troop? troopForTransfer, bool transferHalf) = ChooseTroopForGarrison(guestHero, guestHero.Army);

                if (troopForTransfer is not null)
                {
                    Debug.Assert(guestHero.Army.OccupiedSlotCount > 1, "A champion hero must keep at least one stack.");

                    uint initialCount = troopForTransfer.Count;
                    uint countToTransfer = transferHalf ? initialCount / 2 : initialCount;

                    if (garrison.JoinTroop(troopForTransfer.Monster, countToTransfer, true))
                    {
                        if (countToTransfer == initialCount)
                        {
                            troopForTransfer.Reset();
                        }
                        else
                        {
                            troopForTransfer.Count = initialCount - countToTransfer;
                        }
                    }
                }
            }
        }

        AiCommon.OptimizeTroopsOrder(garrison);

        if (guestHero is not null)
        {
            AiCommon.OptimizeTroopsOrder(guestHero.Army);
        }
    }

    /// <summary>
    /// How much the AI wants a resource, <c>Planner::getResourcePriorityModifier()</c> (<c>ai_planner.h:227</c>).
    /// </summary>
    /// <param name="resource">One of the <see cref="Resource"/> constants.</param>
    /// <param name="isMine">Whether the source of the resource is a mine.</param>
    /// <returns>The modifier the value of the source is multiplied by.</returns>
    internal double GetResourcePriorityModifier(int resource, bool isMine)
    {
        // One unit of gold is not worth one unit of gems, so the weights are normalised: a mine is worth the
        // two days of income it brings in, a pile the usual size of a pile of that resource on a map.
        IReadOnlyDictionary<int, double> resourcePriorities = isMine ? MinePriorities : PilePriorities;

        double priority = resourcePriorities.TryGetValue(resource, out double known) ? known : 1.0;

        Debug.Assert(resourcePriorities.ContainsKey(resource), "The AI only ever asks about the seven resources.");

        foreach (BudgetEntry budget in _budget)
        {
            if (budget.Resource != resource)
            {
                continue;
            }

            if (budget.RecurringCost)
            {
                priority *= 1.5;
            }

            return budget.Priority ? priority * 2.0 : priority;
        }

        return priority;
    }

    /// <summary>The seven resource flags, in the order the reference iterates them.</summary>
    /// <remarks>Declared before the tables below because their initializers read it.</remarks>
    private static readonly int[] AllResources =
    [
        Resource.Wood,
        Resource.Mercury,
        Resource.Ore,
        Resource.Sulfur,
        Resource.Crystal,
        Resource.Gems,
        Resource.Gold,
    ];

    /// <summary>
    /// What one mine of each resource is worth, <c>minePriorities</c> of
    /// <c>Planner::getResourcePriorityModifier()</c> (<c>ai_planner.cpp:99-116</c>).
    /// </summary>
    /// <remarks>
    /// The weights are the inverse of the daily income, doubled, so that a mine beats a pile of the same resource:
    /// a gold mine earns 1000 per day and weighs 2, an ore mine earns 2 and weighs 1000.
    /// </remarks>
    private static readonly IReadOnlyDictionary<int, double> MinePriorities = BuildMinePriorities();

    /// <summary>
    /// What one pile of each resource is worth, <c>pilePriorities</c> of
    /// <c>Planner::getResourcePriorityModifier()</c> (<c>ai_planner.cpp:120-129</c>).
    /// </summary>
    /// <remarks>
    /// The weights follow the usual size of a pile: a pile holds 500 to 1500 gold, 5 to 10 wood or ore, and 2 to 5
    /// of the four rare resources, so one rare unit is worth about twice one unit of wood.
    /// </remarks>
    private static readonly IReadOnlyDictionary<int, double> PilePriorities = new Dictionary<int, double>
    {
        [Resource.Gold] = 1,
        [Resource.Wood] = 125,
        [Resource.Ore] = 125,
        [Resource.Mercury] = 250,
        [Resource.Sulfur] = 250,
        [Resource.Crystal] = 250,
        [Resource.Gems] = 250,
    };


    /// <summary>
    /// Builds <see cref="MinePriorities"/>, <c>minePriorities</c> of <c>Planner::getResourcePriorityModifier()</c>.
    /// </summary>
    /// <returns>One weight per resource, keyed by the resource flag.</returns>
    private static IReadOnlyDictionary<int, double> BuildMinePriorities()
    {
        double goldMineIncome = ProfitConditions.FromMine(Resource.Gold).Gold;
        Debug.Assert(goldMineIncome > 0, "A gold mine earns something.");

        Dictionary<int, double> priorities = [];

        foreach (int resource in AllResources)
        {
            int mineIncome = ProfitConditions.FromMine(resource).Get(resource);

            if (mineIncome <= 0)
            {
                continue;
            }

            priorities[resource] = goldMineIncome / mineIncome * 2;
        }

        return priorities;
    }

    /// <summary>
    /// The value of a pile of resources for the AI, <c>Planner::getFundsValueBasedOnPriority()</c>
    /// (<c>ai_planner.h:228</c>).
    /// </summary>
    /// <param name="funds">The resources.</param>
    /// <returns>How much they are worth.</returns>
    internal double GetFundsValueBasedOnPriority(in Funds funds)
    {
        double value = 0;
        int validItems = funds.GetValidItems();

        foreach (int resource in AllResources)
        {
            if ((validItems & resource) == 0)
            {
                continue;
            }

            int amount = funds.Get(resource);

            if (amount <= 0)
            {
                continue;
            }

            value += amount * GetResourcePriorityModifier(resource, false);
        }

        return value;
    }

    /// <summary>
    /// Works out what the kingdom is short of, <c>Planner::updateKingdomBudget()</c> (<c>ai_planner.h:231</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk asks every castle, in turn, how much of each resource it would still be short of to build the next
    /// entries of its build order, and the worst castle decides: a resource that one castle lacks must be saved for
    /// even when another castle has it in abundance.
    /// </para>
    /// <para>
    /// A castle that already has its sixth dwelling keeps needing the price of the upgraded unit, because that
    /// dwelling keeps hiring every turn; those resources are marked recurring so that the AI treats them as a
    /// running cost rather than a one-off spend.
    /// </para>
    /// </remarks>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    internal void UpdateKingdomBudget(GameKingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        // The budget is rebuilt from scratch every turn.
        foreach (BudgetEntry entry in _budget)
        {
            entry.Reset();
        }

        Funds kingdomFunds = kingdom.Treasury;
        Funds requirements = new();

        foreach (CastleTown castle in kingdom.Castles)
        {
            MapRace race = castle.Race;

            foreach (BuildOrder order in GetBuildOrder(race))
            {
                if (castle.CheckBuyBuilding(order.Building, kingdomFunds) != BuildingStatus.LackResources)
                {
                    continue;
                }

                Funds missing = PaymentConditions.BuyBuilding(race, order.Building) - kingdomFunds;

                requirements = requirements.Max(missing);
            }

            // A castle that can field its strongest unit keeps needing the resources that unit costs.
            if (castle.IsBuild(BuildingType.DwellingMonster6))
            {
                MonsterType bestUpgrade = MonsterUpgrades.GetUpgrade(DwellingMonsters.FromDwelling(race, BuildingType.DwellingMonster6));
                Funds bestUnitCost = MonsterStatistics.GetGeneralStats(bestUpgrade).GetCost();

                foreach (BudgetEntry entry in _budget)
                {
                    if (bestUnitCost.Get(entry.Resource) > 0)
                    {
                        entry.RecurringCost = true;
                    }
                }
            }
        }

        foreach (BudgetEntry entry in _budget)
        {
            entry.Missing = requirements.Get(entry.Resource);

            if (entry.Missing > 0)
            {
                entry.Priority = true;
            }
        }
    }

    /// <summary>
    /// Remembers what stands on a tile, <c>Planner::updateMapActionObjectCache()</c> (<c>ai_planner.h:236</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <param name="mapIndex">Tile index to look at.</param>
    private void UpdateMapActionObjectCache(GameKingdom kingdom, int mapIndex)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        World world = World.Current;
        Mp2ObjectType objectType = world.GetTile(mapIndex).GetMainObjectType(world);

        if (!AiCommon.IsValuableAdventureMapObject(kingdom, objectType, mapIndex))
        {
            _mapActionObjects.Remove(mapIndex);

            return;
        }

        _mapActionObjects[mapIndex] = objectType;
    }

    /// <summary>
    /// Whether a tile is a task of any kind, <c>Planner::isPriorityTask()</c> (<c>ai_planner.h:250-253</c>).
    /// </summary>
    /// <param name="index">Tile index to ask about.</param>
    /// <returns><see langword="true"/> when the tile carries a task.</returns>
    private bool IsPriorityTask(int index) => _priorityTargets.ContainsKey(index);

    /// <summary>
    /// Whether a tile is a task that has to be done now, <c>Planner::isCriticalTask()</c> (<c>ai_planner.h:255-263</c>).
    /// </summary>
    /// <param name="index">Tile index to ask about.</param>
    /// <returns><see langword="true"/> for attack and defend tasks.</returns>
    private bool IsCriticalTask(int index) =>
        _priorityTargets.TryGetValue(index, out PriorityTask? task) &&
        task.Type is PriorityTaskType.Attack or PriorityTaskType.Defend;

    /// <summary>
    /// Drops the caches that live for one turn, the block at the start of <c>Planner::KingdomTurn()</c>
    /// (<c>ai_planner_kingdom.cpp:652-659</c>).
    /// </summary>
    /// <remarks>
    /// The reference clears the maps inline; the port keeps the block in one place because the caches are fields of
    /// this class and <see cref="KingdomTurn"/> is where they are dropped.
    /// </remarks>
    private void ResetTurnCaches()
    {
        _mapActionObjects.Clear();
        _priorityTargets.Clear();
        _enemyArmies.Clear();
        _tileArmyStrengthValues.Clear();
        _regions.Clear();
    }

    // -----------------------------------------------------------------------------------------------------------------
    // File-local helpers of ai_planner_castle.cpp: the build order tables and build()/castleDevelopment() that sit in
    // its anonymous namespace, plus the three lambdas reinforceCastle() defines inline.
    // -----------------------------------------------------------------------------------------------------------------

    /// <summary>One entry of a build order: what to build and how badly, <c>struct BuildOrder</c> (<c>:50-60</c>).</summary>
    /// <remarks>
    /// The priority multiplies the price the castle has to be able to afford, so a 10 costs ten times the ordinary
    /// price before the AI spends anything on it.
    /// </remarks>
    /// <param name="Building">The building.</param>
    /// <param name="Priority">How many times its price has to be in the treasury to be worth building.</param>
    private readonly record struct BuildOrder(BuildingType Building, uint Priority);

    /// <summary>The four buildings that only help the defence, <c>defensiveStructures</c> (<c>:62</c>).</summary>
    private static readonly BuildOrder[] DefensiveStructures =
    [
        new(BuildingType.LeftTurret, 1),
        new(BuildingType.RightTurret, 1),
        new(BuildingType.Moat, 1),
        new(BuildingType.Captain, 1),
    ];

    /// <summary>The buildings that help the defence indirectly, <c>supportingDefensiveStructures</c> (<c>:63</c>).</summary>
    private static readonly BuildOrder[] SupportingDefensiveStructures =
    [
        new(BuildingType.MageGuild1, 1),
        new(BuildingType.Spec, 2),
        new(BuildingType.Tavern, 1),
    ];

    /// <summary>
    /// The buildings every castle of any race wants first, <c>GetIncomeStructures()</c> (<c>:67-70</c>).
    /// </summary>
    /// <remarks>
    /// A warlock castle adds its special structure because it pays for itself in spell power, which is what the whole
    /// town is for.
    /// </remarks>
    /// <param name="race">The race of the castle.</param>
    /// <returns>The build order to try first.</returns>
    private static IReadOnlyList<BuildOrder> GetIncomeStructures(MapRace race) =>
        race == MapRace.Warlock ? WarlockIncomeStructures : StandardIncomeStructures;

    /// <summary>The three buildings every castle wants, <c>standard</c> of <c>GetIncomeStructures()</c>.</summary>
    private static readonly BuildOrder[] StandardIncomeStructures =
    [
        new(BuildingType.Castle, 1),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
    ];

    /// <summary>The warlock version, with the special structure, <c>warlock</c> of <c>GetIncomeStructures()</c>.</summary>
    private static readonly BuildOrder[] WarlockIncomeStructures =
    [
        new(BuildingType.Castle, 1),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
        new(BuildingType.Spec, 1),
    ];

    /// <summary>
    /// The full build order of a race, <c>GetBuildOrder()</c> (<c>:73-137</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every list starts with the castle, the statue and the marketplace, then names the dwellings and ends with the
    /// mage guild and the remaining utility buildings. The races differ in what they consider urgent: the knight
    /// de-prioritises the fifth dwelling (it can reach the sixth without it) and raises the archery upgrade, the
    /// necromancer goes for the fifth and sixth dwellings and the second mage guild, the wizard for the mage guild
    /// and the library.
    /// </para>
    /// <para>
    /// A race with no list of its own falls back to the generic order, which is what the reference's <c>default</c>
    /// branch does - a mercenary or a random castle gets the safe list.
    /// </para>
    /// </remarks>
    /// <param name="race">The race of the castle.</param>
    /// <returns>The build order to try once the income structures are up.</returns>
    private static IReadOnlyList<BuildOrder> GetBuildOrder(MapRace race) => race switch
    {
        MapRace.Knight => KnightBuildOrder,
        MapRace.Necromancer => NecromancerBuildOrder,
        MapRace.Wizard => WizardBuildOrder,
        MapRace.Sorceress => SorceressBuildOrder,
        MapRace.Barbarian => BarbarianBuildOrder,
        _ => GenericBuildOrder,
    };

    /// <summary>The order of a race without a list of its own, <c>genericBuildOrder</c> (<c>:75-80</c>).</summary>
    private static readonly BuildOrder[] GenericBuildOrder =
    [
        new(BuildingType.Castle, 2),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
        new(BuildingType.DwellingUpgrade7, 1),
        new(BuildingType.DwellingUpgrade6, 1),
        new(BuildingType.DwellingMonster6, 1),
        new(BuildingType.DwellingUpgrade5, 1),
        new(BuildingType.DwellingMonster5, 1),
        new(BuildingType.DwellingUpgrade4, 1),
        new(BuildingType.DwellingMonster4, 1),
        new(BuildingType.DwellingUpgrade3, 2),
        new(BuildingType.DwellingMonster3, 2),
        new(BuildingType.DwellingUpgrade2, 3),
        new(BuildingType.DwellingMonster2, 3),
        new(BuildingType.DwellingMonster1, 4),
        new(BuildingType.MageGuild1, 2),
        new(BuildingType.Well2, 10),
        new(BuildingType.Tavern, 5),
        new(BuildingType.ThievesGuild, 10),
        new(BuildingType.MageGuild2, 3),
        new(BuildingType.MageGuild3, 4),
        new(BuildingType.MageGuild4, 5),
        new(BuildingType.MageGuild5, 5),
        new(BuildingType.Shipyard, 4),
    ];

    /// <summary>The barbarian order, <c>barbarianBuildOrder</c> (<c>:82-87</c>).</summary>
    private static readonly BuildOrder[] BarbarianBuildOrder =
    [
        new(BuildingType.Castle, 2),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
        new(BuildingType.DwellingMonster6, 1),
        new(BuildingType.DwellingUpgrade5, 1),
        new(BuildingType.DwellingMonster5, 1),
        new(BuildingType.DwellingUpgrade4, 1),
        new(BuildingType.DwellingMonster4, 1),
        new(BuildingType.DwellingMonster3, 1),
        new(BuildingType.DwellingUpgrade2, 2),
        new(BuildingType.DwellingMonster2, 2),
        new(BuildingType.DwellingMonster1, 4),
        new(BuildingType.MageGuild1, 3),
        new(BuildingType.Well2, 10),
        new(BuildingType.Tavern, 5),
        new(BuildingType.ThievesGuild, 10),
        new(BuildingType.MageGuild2, 4),
        new(BuildingType.MageGuild3, 5),
        new(BuildingType.MageGuild4, 6),
        new(BuildingType.MageGuild5, 7),
        new(BuildingType.Shipyard, 4),
    ];

    /// <summary>The sorceress order, <c>sorceressBuildOrder</c> (<c>:89-94</c>).</summary>
    private static readonly BuildOrder[] SorceressBuildOrder =
    [
        new(BuildingType.Castle, 2),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
        new(BuildingType.DwellingMonster6, 1),
        new(BuildingType.DwellingMonster5, 1),
        new(BuildingType.DwellingMonster4, 1),
        new(BuildingType.MageGuild1, 1),
        new(BuildingType.DwellingMonster3, 1),
        new(BuildingType.DwellingUpgrade4, 1),
        new(BuildingType.DwellingUpgrade3, 2),
        new(BuildingType.DwellingUpgrade2, 5),
        new(BuildingType.DwellingMonster2, 2),
        new(BuildingType.Tavern, 2),
        new(BuildingType.DwellingMonster1, 4),
        new(BuildingType.Well2, 10),
        new(BuildingType.ThievesGuild, 10),
        new(BuildingType.MageGuild2, 3),
        new(BuildingType.MageGuild3, 4),
        new(BuildingType.MageGuild4, 5),
        new(BuildingType.MageGuild5, 5),
        new(BuildingType.Shipyard, 4),
    ];

    /// <summary>
    /// The knight order, <c>knightBuildOrder</c> (<c>:98-103</c>).
    /// </summary>
    /// <remarks>
    /// The reference de-prioritises the fifth dwelling - a knight can reach the sixth without it - and the first
    /// dwelling and the upgrades of the third and fourth, because the well, the tavern and the archery upgrade are
    /// worth more.
    /// </remarks>
    private static readonly BuildOrder[] KnightBuildOrder =
    [
        new(BuildingType.Castle, 2),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
        new(BuildingType.DwellingUpgrade6, 2),
        new(BuildingType.DwellingMonster6, 1),
        new(BuildingType.DwellingUpgrade5, 2),
        new(BuildingType.DwellingMonster5, 2),
        new(BuildingType.DwellingUpgrade4, 2),
        new(BuildingType.DwellingMonster4, 1),
        new(BuildingType.DwellingUpgrade3, 2),
        new(BuildingType.DwellingMonster3, 1),
        new(BuildingType.DwellingUpgrade2, 1),
        new(BuildingType.DwellingMonster2, 3),
        new(BuildingType.DwellingMonster1, 4),
        new(BuildingType.Well, 1),
        new(BuildingType.Tavern, 1),
        new(BuildingType.MageGuild1, 2),
        new(BuildingType.MageGuild2, 3),
        new(BuildingType.MageGuild3, 5),
        new(BuildingType.MageGuild4, 5),
        new(BuildingType.MageGuild5, 5),
        new(BuildingType.Spec, 5),
        new(BuildingType.ThievesGuild, 10),
        new(BuildingType.Well2, 20),
        new(BuildingType.Shipyard, 4),
    ];

    /// <summary>
    /// The necromancer order, <c>necromancerBuildOrder</c> (<c>:106-111</c>).
    /// </summary>
    /// <remarks>Priority on the fifth and sixth dwellings and the second mage guild, which is what raises skeletons.</remarks>
    private static readonly BuildOrder[] NecromancerBuildOrder =
    [
        new(BuildingType.Castle, 2),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
        new(BuildingType.DwellingUpgrade6, 1),
        new(BuildingType.DwellingMonster6, 1),
        new(BuildingType.DwellingUpgrade5, 2),
        new(BuildingType.DwellingMonster5, 1),
        new(BuildingType.MageGuild1, 1),
        new(BuildingType.DwellingUpgrade4, 2),
        new(BuildingType.DwellingMonster4, 1),
        new(BuildingType.DwellingUpgrade3, 3),
        new(BuildingType.DwellingMonster3, 3),
        new(BuildingType.DwellingUpgrade2, 4),
        new(BuildingType.DwellingMonster2, 2),
        new(BuildingType.DwellingMonster1, 3),
        new(BuildingType.MageGuild2, 2),
        new(BuildingType.ThievesGuild, 2),
        new(BuildingType.Well2, 8),
        new(BuildingType.MageGuild3, 4),
        new(BuildingType.MageGuild4, 5),
        new(BuildingType.MageGuild5, 5),
        new(BuildingType.Shrine, 10),
        new(BuildingType.Shipyard, 4),
    ];

    /// <summary>
    /// The wizard order, <c>wizardBuildOrder</c> (<c>:114-119</c>).
    /// </summary>
    /// <remarks>Priority on the mage guild and the library, and every dwelling at the same level.</remarks>
    private static readonly BuildOrder[] WizardBuildOrder =
    [
        new(BuildingType.Castle, 2),
        new(BuildingType.Statue, 1),
        new(BuildingType.Marketplace, 1),
        new(BuildingType.DwellingUpgrade6, 1),
        new(BuildingType.DwellingMonster6, 1),
        new(BuildingType.DwellingUpgrade5, 1),
        new(BuildingType.DwellingMonster5, 1),
        new(BuildingType.DwellingMonster4, 1),
        new(BuildingType.DwellingMonster3, 1),
        new(BuildingType.DwellingMonster2, 1),
        new(BuildingType.DwellingMonster1, 1),
        new(BuildingType.MageGuild1, 1),
        new(BuildingType.DwellingUpgrade3, 4),
        new(BuildingType.Spec, 2),
        new(BuildingType.Well2, 8),
        new(BuildingType.MageGuild2, 3),
        new(BuildingType.MageGuild3, 4),
        new(BuildingType.MageGuild4, 4),
        new(BuildingType.MageGuild5, 4),
        new(BuildingType.Tavern, 10),
        new(BuildingType.ThievesGuild, 10),
        new(BuildingType.Shipyard, 4),
    ];

    /// <summary>The dwellings a castle hires from, best first, <c>castleDwellings</c> (<c>:348-350</c>).</summary>
    private static readonly BuildingType[] CastleDwellings =
    [
        BuildingType.DwellingUpgrade7,
        BuildingType.DwellingUpgrade6,
        BuildingType.DwellingMonster6,
        BuildingType.DwellingUpgrade5,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingUpgrade4,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingUpgrade3,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingUpgrade2,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster1,
    ];

    /// <summary>
    /// Builds the first affordable entry of a build order, <c>Build()</c> (<c>:139-162</c>).
    /// </summary>
    /// <remarks>
    /// A multiplier of one asks only for the price of the building; anything else asks for that many times the price,
    /// which is how the reference keeps the defensive buildings for a castle that can easily afford them. Buildings
    /// the difficulty forbids are skipped, not treated as blockers, so a lower entry can still be built.
    /// </remarks>
    /// <param name="castle">The castle that builds.</param>
    /// <param name="buildOrderList">The order to walk.</param>
    /// <param name="multiplier">How many times the price the treasury has to cover.</param>
    /// <returns>Whether something was built.</returns>
    private static bool Build(CastleTown castle, IReadOnlyList<BuildOrder> buildOrderList, uint multiplier = 1)
    {
        GameDifficulty difficulty = Settings.Get().GameDifficulty;
        bool isCampaign = Settings.Get().IsCampaignGameType;

        foreach (BuildOrder order in buildOrderList)
        {
            if (!Difficulty.AllowAiToBuildCastleBuilding(difficulty, isCampaign, order.Building))
            {
                continue;
            }

            uint fundsMultiplier = order.Priority * multiplier;

            if (fundsMultiplier == 1)
            {
                if (AiCommon.BuildIfPossible(castle, order.Building))
                {
                    return true;
                }
            }
            else if (AiCommon.BuildIfEnoughFunds(castle, order.Building, fundsMultiplier))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds what a peaceful castle wants next, <c>CastleDevelopment()</c> (<c>:164-249</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is not a single list but a sequence of attempts, each of which stops the whole function when it
    /// succeeds: the well on a castle that has none after the first week, the income structures, a shipyard when the
    /// region is a peninsula or smaller, the racial build order, the mage guild when the region still needs spells,
    /// a boat when the kingdom has heroes to use one, and finally the defensive buildings.
    /// </para>
    /// <para>
    /// The spell level of the region is what stops the mage guild from growing past what the heroes can learn:
    /// <paramref name="spellLevel"/> is the guild level the region's shrines and guilds support.
    /// </para>
    /// </remarks>
    /// <param name="castle">The castle that develops.</param>
    /// <param name="safetyFactor">How safe the region of the castle is, from the turn's evaluation.</param>
    /// <param name="spellLevel">The mage guild level the region supports.</param>
    /// <returns>Whether anything was built or bought.</returns>
    private static bool CastleDevelopment(CastleTown castle, int safetyFactor, int spellLevel)
    {
        World world = World.Current;

        GameDifficulty difficulty = Settings.Get().GameDifficulty;
        bool isCampaign = Settings.Get().IsCampaignGameType;

        if (!Difficulty.AllowAiToDevelopCastlesOnDay(difficulty, isCampaign, world.DayCount))
        {
            return false;
        }

        // Without a well the castle cannot grow, so it comes before everything else once a week has passed.
        if (castle.IsCastle && !castle.IsBuild(BuildingType.Well) && world.DayCount > 6)
        {
            return AiCommon.BuildIfPossible(castle, BuildingType.Well);
        }

        if (Build(castle, GetIncomeStructures(castle.Race)))
        {
            return true;
        }

        int neighbourRegions = world.GetRegion((uint)world.GetTile(castle.TileIndex).Region).NeighboursCount;
        bool islandOrPeninsula = neighbourRegions < 3;

        // A castle with few neighbours is where the war has to be taken elsewhere, so it is forced to build a
        // shipyard; the +1 stands for the case of no neighbours at all, where a plain price check would be free.
        if (islandOrPeninsula && AiCommon.BuildIfEnoughFunds(castle, BuildingType.Shipyard, (uint)(neighbourRegions + 1)))
        {
            return true;
        }

        if (Build(castle, GetBuildOrder(castle.Race)))
        {
            return true;
        }

        if (castle.GetLevelMageGuild() < spellLevel && safetyFactor > 0)
        {
            if (Build(castle, MagicGuildUpgrades))
            {
                return true;
            }
        }

        // A boat is only worth it when there is a hero to sail it, and only when the resources can be raised, either
        // by waiting for them or by trading for them.
        bool buyBoat = false;

        GameKingdom kingdom = world.GetKingdom(castle.Color);

        if (castle.AllowBuyBoat(world, checkPayment: false) && kingdom.Heroes.Count > 0)
        {
            Funds requiredFunds = PaymentConditions.BuyBoat() * (uint)(islandOrPeninsula ? 2 : 4);

            if (kingdom.AllowPayment(requiredFunds))
            {
                buyBoat = true;
            }
            else if (AiCommon.CalculateMarketplaceTransaction(kingdom, requiredFunds) is not null)
            {
                if (kingdom.AllowPayment(PaymentConditions.BuyBoat()))
                {
                    buyBoat = true;
                }
                else if (AiCommon.TradeAtMarketplace(kingdom, PaymentConditions.BuyBoat()))
                {
                    buyBoat = true;
                }
                else
                {
                    Debug.Assert(false, "The marketplace promised a trade that it then refused.");
                }
            }
        }

        if (buyBoat)
        {
            castle.BuyBoat(world, kingdom);
        }

        if (Build(castle, DefensiveStructures, 10))
        {
            return true;
        }

        return Build(castle, SupportingDefensiveStructures, 10);
    }

    /// <summary>The mage guild levels a region may still want, <c>magicGuildUpgrades</c> (<c>:192-193</c>).</summary>
    private static readonly BuildOrder[] MagicGuildUpgrades =
    [
        new(BuildingType.MageGuild2, 2),
        new(BuildingType.MageGuild3, 2),
        new(BuildingType.MageGuild4, 1),
        new(BuildingType.MageGuild5, 1),
    ];

    /// <summary>
    /// Hires a stack into the garrison or, when the garrison is full, into the guest hero's army,
    /// the <c>recruitMonster</c> lambda of <c>reinforceCastle()</c> (<c>:318-327</c>).
    /// </summary>
    /// <param name="castle">The castle that hires.</param>
    /// <param name="troop">What to hire.</param>
    /// <param name="treasury">The funds that pay.</param>
    /// <param name="guestHero">The hero in the castle, if any.</param>
    /// <returns>Whether the stack was hired.</returns>
    private static bool RecruitMonster(CastleTown castle, Troop troop, ref Funds treasury, Hero? guestHero) =>
        // The reference passes no dialog flag; the port's version takes the guest army explicitly, which is where
        // the stack goes when the garrison has no free slot.
        castle.RecruitMonster(troop, ref treasury, guestHero?.Army);

    /// <summary>
    /// Frees a garrison slot by handing a garrison stack to the guest hero,
    /// the first lambda of <c>reinforceCastle()</c> (<c>:375-397</c>).
    /// </summary>
    /// <remarks>
    /// Every garrison slot is occupied when this runs - the recruitment just failed for lack of one - so the loop
    /// is only looking for a stack the hero can take.
    /// </remarks>
    /// <param name="castle">The castle, for the castle's own limits.</param>
    /// <param name="garrison">The garrison to take a stack from.</param>
    /// <param name="guestHero">The hero that receives the stack.</param>
    /// <param name="guestHeroArmy">The hero's army.</param>
    /// <returns>Whether a slot was freed.</returns>
    private static bool TryTransferGarrisonTroopToHero(CastleTown castle, ArmyTroops garrison, Hero guestHero, ArmyTroops guestHeroArmy)
    {
        for (int slot = 0; slot < ArmyTroops.MaximumTroopCount; ++slot)
        {
            Troop? garrisonTroop = garrison.GetTroop(slot);

            Debug.Assert(garrisonTroop is { IsValid: true }, "The garrison was supposed to be full.");

            if (garrisonTroop is null || !guestHeroArmy.JoinTroop(garrisonTroop))
            {
                continue;
            }

            garrisonTroop.Reset();

            return true;
        }

        return false;
    }

    /// <summary>
    /// Picks the stack a safe castle keeps behind, the second lambda of <c>reinforceCastle()</c> (<c>:400-440</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A champion leaves its weakest stack - it is off to win the game and half of anything is worth less than
    /// momentum - while every other hero leaves half of the stack it can spare, which is the slowest one first and
    /// only then the weakest.
    /// </para>
    /// <para>
    /// "Can spare" is measured against the strength of the army without the hero: a stack worth more than a tenth
    /// (a twentieth for a fighter or a champion) of everything the hero carries is one the hero needs.
    /// </para>
    /// </remarks>
    /// <param name="guestHero">The hero in the castle.</param>
    /// <param name="guestHeroArmy">The hero's army.</param>
    /// <returns>The stack to hand over and whether only half of it goes; the stack is null when the hero keeps everything.</returns>
    private static (Troop? Troop, bool TransferHalf) ChooseTroopForGarrison(Hero guestHero, ArmyTroops guestHeroArmy)
    {
        bool isFighterRole = guestHero.AiRole is HeroRole.Fighter or HeroRole.Champion;
        double armyStrength = guestHeroArmy.GetTroopsStrength();

        Debug.Assert(armyStrength > 0.0, "An army that can spare a stack has a strength.");

        double significanceRatio = isFighterRole ? 20.0 : 10.0;
        double neededStrength = armyStrength / significanceRatio;

        Troop? candidate = guestHeroArmy.GetSlowestTroop();

        if (candidate is not null && candidate.GetStrength() <= neededStrength)
        {
            return (candidate, false);
        }

        if (isFighterRole)
        {
            return (null, false);
        }

        candidate = guestHeroArmy.GetWeakestTroop();

        if (candidate is not null && candidate.GetStrength() <= neededStrength)
        {
            return (candidate, true);
        }

        return (null, false);
    }
}
