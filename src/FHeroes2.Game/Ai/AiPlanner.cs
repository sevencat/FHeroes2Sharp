using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using FHeroes2.Game.Maps;
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
/// <b>What is deliberately absent:</b> the private half of the class (about thirty methods that
/// <c>ai_planner_hero.cpp</c>, <c>ai_planner_castle.cpp</c> and <c>ai_planner_kingdom.cpp</c> implement) is not
/// declared. Those are implementation details with no contract value, and declaring them now would mean guessing
/// their exact parameter shapes before the object-value tables they depend on exist.
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
}
