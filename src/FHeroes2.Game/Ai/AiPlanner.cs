using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
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
/// <b>This is the contract, not the implementation.</b> Every member below exists so that the rest of the port can be
/// written against the same names the reference uses; the bodies throw <see cref="NotImplementedException"/> until
/// the corresponding <c>ai_planner*.cpp</c> file is ported (see <c>docs/DECISIONS.md</c>, ADR-161).
/// </para>
/// <para>
/// The reference is a Meyers singleton (<c>Planner::Get()</c>) whose state - the priority targets, the enemy armies,
/// the cached army strengths, the budget and the AI pathfinder - lives between the calls of one turn and is dropped
/// when the next turn starts. The port keeps the same shape: <see cref="Instance"/> is the singleton, the members
/// are instance methods exactly as in the header, and nothing here is saved.
/// </para>
/// <para>
/// <b>The private half is declared, not implemented.</b> The members <c>ai_planner_hero.cpp</c>,
/// <c>ai_planner_castle.cpp</c> and <c>ai_planner_kingdom.cpp</c> implement are all listed with the signatures of
/// <c>ai_planner.h:200-281</c> and throw exactly like the public ones, so the shape of the layer is complete while
/// the rest of the port is written (ADR-180). Only what a declared member already needs - the two inline helpers of
/// the header, the per-turn caches and the clean-up at the start of a turn - carries code.
/// </para>
/// </remarks>
public sealed class AiPlanner
{
    private AiPlanner()
    {
    }

    /// <summary>The one planner, <c>Planner::Get()</c>.</summary>
    public static AiPlanner Instance { get; } = new();

    private AIWorldPathfinder? _pathfinder;

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
    /// Plays one turn of one kingdom, <c>Planner::KingdomTurn()</c> (<c>ai_planner.h:166</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <returns><see cref="AiTurnOutcome.EndTurn"/> when the turn is over.</returns>
    public AiTurnOutcome KingdomTurn(GameKingdom kingdom) =>
        throw new NotImplementedException("AiPlanner.KingdomTurn — ai_planner.cpp 未移植（ADR-161）");

    /// <summary>
    /// What the AI does when a hero starts moving, <c>Planner::HeroesBeginMovement()</c>
    /// (<c>ai_planner.h:169</c>): the transparent <c>SUMMON_BOAT</c> cast.
    /// </summary>
    /// <param name="hero">The hero that starts moving.</param>
    public void HeroesBeginMovement(Hero hero) =>
        throw new NotImplementedException("AiPlanner.HeroesBeginMovement — ai_planner_hero.cpp 未移植（ADR-161）");

    /// <summary>
    /// What the AI does every time its hero reaches a new tile, <c>Planner::HeroesActionNewPosition()</c>
    /// (<c>ai_planner.h:172</c>): preparing the dig for the ultimate artifact and the transparent
    /// <c>SUMMON_BOAT</c> cast.
    /// </summary>
    /// <param name="hero">The hero that moved.</param>
    public void HeroesActionNewPosition(Hero hero) =>
        throw new NotImplementedException("AiPlanner.HeroesActionNewPosition — ai_planner_hero.cpp 未移植（ADR-161）");

    /// <summary>
    /// What the AI does after its hero finished an action, <c>Planner::HeroesActionComplete()</c>
    /// (<c>ai_planner.h:174</c>).
    /// </summary>
    /// <param name="hero">The hero that acted.</param>
    /// <param name="tileIndex">Tile the action happened on.</param>
    /// <param name="objectType">Object on that tile, <c>MP2::OBJ_NONE</c> when there is none.</param>
    public void HeroesActionComplete(Hero hero, int tileIndex, Mp2ObjectType objectType) =>
        throw new NotImplementedException("AiPlanner.HeroesActionComplete — ai_planner_hero.cpp 未移植（ADR-161）");

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
    /// Reveals the fog around a tile for a kingdom, <c>Planner::revealFog()</c> (<c>ai_planner.h:181</c>).
    /// </summary>
    /// <param name="tile">The tile to reveal.</param>
    /// <param name="kingdom">The kingdom that sees it.</param>
    public void RevealFog(Tile tile, GameKingdom kingdom) =>
        throw new NotImplementedException("AiPlanner.RevealFog — ai_planner_hero.cpp 未移植（ADR-161）");

    /// <summary>
    /// Whether an object is worth visiting for a hero, <c>Planner::isValidHeroObject()</c>
    /// (<c>ai_planner.h:183</c>).
    /// </summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="underHero">Whether the object is under the hero.</param>
    /// <returns><see langword="true"/> when the object is a valid target.</returns>
    public bool IsValidHeroObject(Hero hero, int index, bool underHero) =>
        throw new NotImplementedException("AiPlanner.IsValidHeroObject — ai_planner_hero.cpp 未移植（ADR-161）");

    /// <summary>
    /// The value of an object for a hero, <c>Planner::getObjectValue()</c> (<c>ai_planner.h:185-186</c>).
    /// </summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="valueToIgnore">Value subtracted from the result, the <c>valueToIgnore</c> of the reference.</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, already divided by the distance.</returns>
    public double GetObjectValue(Hero hero, int index, Mp2ObjectType objectType, double valueToIgnore, uint distanceToObject) =>
        throw new NotImplementedException("AiPlanner.GetObjectValue — ai_planner_hero.cpp 未移植（ADR-161）");

    /// <summary>
    /// The value of an object for a hero that will not visit it now, <c>Planner::getFutureObjectValue()</c>
    /// (<c>ai_planner.h:188-189</c>).
    /// </summary>
    /// <param name="hero">The hero that would visit it later.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="valueToIgnore">Value subtracted from the result.</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, already divided by the distance.</returns>
    public double GetFutureObjectValue(Hero hero, int index, Mp2ObjectType objectType, double valueToIgnore, uint distanceToObject) =>
        throw new NotImplementedException("AiPlanner.GetFutureObjectValue — ai_planner_hero.cpp 未移植（ADR-161）");

    /// <summary>
    /// Strength of the army guarding a tile, <c>Planner::getTileArmyStrength()</c> (<c>ai_planner.h:191-193</c>).
    /// </summary>
    /// <remarks>
    /// The reference fills a temporary <c>Army</c> from the tile and measures it, so the result is valid for neutral
    /// monsters and dwelling guardians only - not for hero armies or garrisons.
    /// </remarks>
    /// <param name="tile">The tile to measure.</param>
    /// <returns>The strength of the guarding army, <c>0</c> when nothing guards the tile.</returns>
    public double GetTileArmyStrength(Tile tile) =>
        throw new NotImplementedException("AiPlanner.GetTileArmyStrength — ai_planner_hero.cpp 未移植（ADR-161）");

    /// <summary>
    /// Prepares a hero or captain for a battle, <c>Planner::HeroesPreBattle()</c> (<c>ai_planner.h:195</c>).
    /// </summary>
    /// <remarks>
    /// The reference takes <c>HeroBase&amp;</c>, the common base of an adventure hero and a castle captain; the port
    /// has no such base (a hero is <see cref="Hero"/>, a captain is <c>Captain</c>), so the parameter is the
    /// <see cref="IArmyCommander"/> both of them implement. Narrow it to <see cref="Hero"/> if the implementation
    /// needs hero-only members.
    /// </remarks>
    /// <param name="commander">The hero or captain that is about to fight.</param>
    /// <param name="isAttacking">Whether it attacks.</param>
    public static void HeroesPreBattle(IArmyCommander commander, bool isAttacking) =>
        throw new NotImplementedException("AiPlanner.HeroesPreBattle — ai_planner.cpp 未移植（ADR-161）");

    /// <summary>
    /// Prepares a castle for a battle, <c>Planner::CastlePreBattle()</c> (<c>ai_planner.h:196</c>).
    /// </summary>
    /// <param name="castle">The castle that is about to be attacked.</param>
    public static void CastlePreBattle(CastleTown castle) =>
        throw new NotImplementedException("AiPlanner.CastlePreBattle — ai_planner.cpp 未移植（ADR-161）");

    /// <summary>
    /// Picks one of two secondary skills, <c>Planner::pickSecondarySkill()</c> (<c>ai_planner.h:198</c>).
    /// </summary>
    /// <param name="hero">The hero that levels up.</param>
    /// <param name="left">First offered skill.</param>
    /// <param name="right">Second offered skill.</param>
    /// <returns>The skill the AI takes.</returns>
    public static SecondarySkill PickSecondarySkill(Hero hero, SecondarySkill left, SecondarySkill right) =>
        throw new NotImplementedException("AiPlanner.PickSecondarySkill — ai_planner.cpp 未移植（ADR-161）");

    // -------------------------------------------------------------------------------------------------------------
    // 私有半边（ai_planner.h:200-281）：签名照抄参考，正文留到移植对应的 ai_planner_*.cpp 时再写（ADR-180）。
    // -------------------------------------------------------------------------------------------------------------

    /// <summary>Plays the turn of one castle, <c>Planner::CastleTurn()</c> (<c>ai_planner.h:203</c>).</summary>
    /// <param name="castle">The castle whose turn it is.</param>
    /// <param name="defensiveStrategy">Whether the castle is expected to be attacked this turn.</param>
    private void CastleTurn(CastleTown castle, bool defensiveStrategy) =>
        throw new NotImplementedException("AiPlanner.CastleTurn — ai_planner_castle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Upgrades and hires as much as possible and hands the best troops to the guest hero,
    /// <c>Planner::reinforceCastle()</c> (<c>ai_planner.h:207</c>).
    /// </summary>
    /// <param name="castle">The castle to reinforce.</param>
    private void ReinforceCastle(CastleTown castle) =>
        throw new NotImplementedException("AiPlanner.ReinforceCastle — ai_planner_castle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Gives the kingdom's heroes their tasks one at a time, <c>Planner::HeroesTurn()</c> (<c>ai_planner.h:210</c>).
    /// </summary>
    /// <param name="heroes">The kingdom's heroes, the ones that can still act first.</param>
    /// <param name="currentProgressValue">How far the turn has got, raised while the turn runs.</param>
    /// <param name="endProgressValue">What <paramref name="currentProgressValue"/> reaches at the end of the turn.</param>
    /// <param name="moreTasksAvailable">Set when a hero was given a task and other heroes may still have one.</param>
    /// <returns><see cref="AiTurnOutcome.EndTurn"/> when no hero is left with something to do.</returns>
    private AiTurnOutcome HeroesTurn(IReadOnlyList<Hero> heroes, ref uint currentProgressValue, uint endProgressValue, ref bool moreTasksAvailable) =>
        throw new NotImplementedException("AiPlanner.HeroesTurn — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>
    /// Hires a hero in a castle, <c>Planner::recruitHero()</c> (<c>ai_planner.h:212</c>).
    /// </summary>
    /// <param name="castle">The castle to hire in.</param>
    /// <param name="buyArmy">Whether the new hero should get troops as well.</param>
    /// <returns>Whether a hero was hired.</returns>
    private bool RecruitHero(CastleTown castle, bool buyArmy) =>
        throw new NotImplementedException("AiPlanner.RecruitHero — ai_planner_castle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Works out how safe each region of the map is, <c>Planner::evaluateRegionSafety()</c>
    /// (<c>ai_planner.h:214</c>).
    /// </summary>
    private void EvaluateRegionSafety() =>
        throw new NotImplementedException("AiPlanner.EvaluateRegionSafety — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Ranks the castles by how much they need the heroes, <c>Planner::getSortedCastleList()</c>
    /// (<c>ai_planner.h:216</c>).
    /// </summary>
    /// <param name="castles">The kingdom's castles.</param>
    /// <param name="castlesInDanger">Tile indexes of the castles that an enemy can reach this turn.</param>
    /// <returns>The castles, the ones that need help first.</returns>
    private List<AiCastle> GetSortedCastleList(IReadOnlyList<CastleTown> castles, IReadOnlySet<int> castlesInDanger) =>
        throw new NotImplementedException("AiPlanner.GetSortedCastleList — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Picks the most valuable task for a hero, <c>Planner::getPriorityTarget()</c> (<c>ai_planner.h:218</c>).
    /// </summary>
    /// <param name="hero">The hero to give a task to.</param>
    /// <param name="maxPriority">Receives the priority of the task that was picked.</param>
    /// <returns>Tile index of the target, <c>-1</c> when nothing is worth doing.</returns>
    private int GetPriorityTarget(Hero hero, out double maxPriority) =>
        throw new NotImplementedException("AiPlanner.GetPriorityTarget — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>The value of an object for any hero, <c>Planner::getGeneralObjectValue()</c> (<c>ai_planner.h:220</c>).</summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value subtracted from the result.</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, already divided by the distance.</returns>
    private double GetGeneralObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject) =>
        throw new NotImplementedException("AiPlanner.GetGeneralObjectValue — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>The value of an object for a fighting hero, <c>Planner::getFighterObjectValue()</c> (<c>ai_planner.h:221</c>).</summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value subtracted from the result.</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, already divided by the distance.</returns>
    private double GetFighterObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject) =>
        throw new NotImplementedException("AiPlanner.GetFighterObjectValue — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>The value of an object for a courier hero, <c>Planner::getCourierObjectValue()</c> (<c>ai_planner.h:222</c>).</summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value subtracted from the result.</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, already divided by the distance.</returns>
    private double GetCourierObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject) =>
        throw new NotImplementedException("AiPlanner.GetCourierObjectValue — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>The value of an object for a scouting hero, <c>Planner::getScoutObjectValue()</c> (<c>ai_planner.h:223</c>).</summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value subtracted from the result.</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, already divided by the distance.</returns>
    private double GetScoutObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject) =>
        throw new NotImplementedException("AiPlanner.GetScoutObjectValue — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>
    /// The task a courier hero should take, <c>Planner::getCourierMainTarget()</c> (<c>ai_planner.h:225</c>).
    /// </summary>
    /// <param name="hero">The courier hero.</param>
    /// <param name="lowestPossibleValue">Below this value nothing is worth a trip.</param>
    /// <returns>Tile index of the target, <c>-1</c> when there is none.</returns>
    private int GetCourierMainTarget(Hero hero, double lowestPossibleValue) =>
        throw new NotImplementedException("AiPlanner.GetCourierMainTarget — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>
    /// How much the AI wants a resource, <c>Planner::getResourcePriorityModifier()</c> (<c>ai_planner.h:227</c>).
    /// </summary>
    /// <param name="resource">One of the <see cref="Resource"/> constants.</param>
    /// <param name="isMine">Whether the source of the resource is a mine.</param>
    /// <returns>The modifier the value of the source is multiplied by.</returns>
    private double GetResourcePriorityModifier(int resource, bool isMine) =>
        throw new NotImplementedException("AiPlanner.GetResourcePriorityModifier — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>
    /// The value of a pile of resources for the AI, <c>Planner::getFundsValueBasedOnPriority()</c>
    /// (<c>ai_planner.h:228</c>).
    /// </summary>
    /// <param name="funds">The resources.</param>
    /// <returns>How much they are worth.</returns>
    private double GetFundsValueBasedOnPriority(in Funds funds) =>
        throw new NotImplementedException("AiPlanner.GetFundsValueBasedOnPriority — ai_planner_hero.cpp 未移植（ADR-180）");

    /// <summary>
    /// Turns an object a hero just visited into tasks, <c>Planner::updatePriorityTargets()</c>
    /// (<c>ai_planner.h:230</c>).
    /// </summary>
    /// <param name="hero">The hero that acted.</param>
    /// <param name="tileIndex">Tile the action happened on.</param>
    /// <param name="objectType">Object on that tile.</param>
    private void UpdatePriorityTargets(Hero hero, int tileIndex, Mp2ObjectType objectType) =>
        throw new NotImplementedException("AiPlanner.UpdatePriorityTargets — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Works out what the kingdom is short of, <c>Planner::updateKingdomBudget()</c> (<c>ai_planner.h:231</c>).
    /// </summary>
    /// <remarks>
    /// The reference cleans the seven entries up before it fills them (<c>ai_planner_castle.cpp:270-275</c>); that
    /// much is kept here so the field has a use while the rest of the body is missing.
    /// </remarks>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    private void UpdateKingdomBudget(GameKingdom kingdom)
    {
        foreach (BudgetEntry entry in _budget)
        {
            entry.Reset();
        }

        throw new NotImplementedException("AiPlanner.UpdateKingdomBudget — ai_planner_castle.cpp 未移植（ADR-180）");
    }

    /// <summary>
    /// Hires the heroes the kingdom can still afford, <c>Planner::purchaseNewHeroes()</c> (<c>ai_planner.h:233</c>).
    /// </summary>
    /// <param name="sortedCastleList">The castles, ranked by <see cref="GetSortedCastleList"/>.</param>
    /// <param name="castlesInDanger">Tile indexes of the castles that an enemy can reach this turn.</param>
    /// <param name="availableHeroCount">How many heroes the kingdom may still hire.</param>
    /// <param name="moreTasksForHeroes">Whether the turn still has tasks for another hero.</param>
    /// <returns>Whether a hero was hired.</returns>
    private bool PurchaseNewHeroes(IReadOnlyList<AiCastle> sortedCastleList, IReadOnlySet<int> castlesInDanger, int availableHeroCount, bool moreTasksForHeroes) =>
        throw new NotImplementedException("AiPlanner.PurchaseNewHeroes — ai_planner_castle.cpp 未移植（ADR-180）");

    /// <summary>
    /// Remembers what stands on a tile, <c>Planner::updateMapActionObjectCache()</c> (<c>ai_planner.h:236</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <param name="mapIndex">Tile index to look at.</param>
    private void UpdateMapActionObjectCache(GameKingdom kingdom, int mapIndex) =>
        throw new NotImplementedException("AiPlanner.UpdateMapActionObjectCache — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Finds the castles an enemy can reach this turn, <c>Planner::findCastlesInDanger()</c>
    /// (<c>ai_planner.h:238</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom to check.</param>
    /// <returns>Tile indexes of the castles in danger.</returns>
    private HashSet<int> FindCastlesInDanger(GameKingdom kingdom) =>
        throw new NotImplementedException("AiPlanner.FindCastlesInDanger — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Re-raises the tasks that answer an enemy army, <c>Planner::updatePriorityForEnemyArmy()</c>
    /// (<c>ai_planner.h:240</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <param name="enemyArmy">The army that was found.</param>
    private void UpdatePriorityForEnemyArmy(GameKingdom kingdom, EnemyArmy enemyArmy) =>
        throw new NotImplementedException("AiPlanner.UpdatePriorityForEnemyArmy — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Re-raises the tasks that defend a castle, <c>Planner::updatePriorityForCastle()</c> (<c>ai_planner.h:241</c>).
    /// </summary>
    /// <param name="castle">The castle to defend.</param>
    private void UpdatePriorityForCastle(CastleTown castle) =>
        throw new NotImplementedException("AiPlanner.UpdatePriorityForCastle — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Whether one enemy army makes a castle worth defending, <c>Planner::updateIndividualPriorityForCastle()</c>
    /// (<c>ai_planner.h:245</c>).
    /// </summary>
    /// <remarks>
    /// The header warns not to call this directly: it only answers for one army at a time, and the callers that walk
    /// through all of them are <see cref="UpdatePriorityForEnemyArmy"/> and <see cref="UpdatePriorityForCastle"/>.
    /// </remarks>
    /// <param name="castle">The castle to check.</param>
    /// <param name="enemyArmy">The army that could attack it.</param>
    /// <returns>Whether the castle is in danger from that army.</returns>
    private bool UpdateIndividualPriorityForCastle(CastleTown castle, EnemyArmy enemyArmy) =>
        throw new NotImplementedException("AiPlanner.UpdateIndividualPriorityForCastle — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Drops a task that is done, <c>Planner::removePriorityAttackTarget()</c> (<c>ai_planner.h:247</c>).
    /// </summary>
    /// <param name="tileIndex">Tile index of the target.</param>
    private void RemovePriorityAttackTarget(int tileIndex) =>
        throw new NotImplementedException("AiPlanner.RemovePriorityAttackTarget — ai_planner_kingdom.cpp 未移植（ADR-180）");

    /// <summary>
    /// Adds a task for an enemy hero or creature standing on a tile,
    /// <c>Planner::updatePriorityAttackTarget()</c> (<c>ai_planner.h:248</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom whose turn it is.</param>
    /// <param name="tile">The tile to look at.</param>
    private void UpdatePriorityAttackTarget(GameKingdom kingdom, Tile tile) =>
        throw new NotImplementedException("AiPlanner.UpdatePriorityAttackTarget — ai_planner_kingdom.cpp 未移植（ADR-180）");

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
}
