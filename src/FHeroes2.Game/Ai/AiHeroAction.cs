using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Ai;

/// <summary>
/// What an AI hero does on the adventure map, the free functions of <c>ai_hero_action.h</c>
/// (<c>ai_hero_action.cpp</c>).
/// </summary>
/// <remarks>
/// <b>This is the contract, not the implementation</b> (ADR-161): the bodies throw until <c>ai_hero_action.cpp</c> is
/// ported. The file is the counterpart of <c>HeroActions</c> for the player (ADR-132) - the same objects, the same
/// tile rules, but the decisions are taken by the AI instead of by a click.
/// </remarks>
public static class AiHeroAction
{
    /// <summary>
    /// Performs the action of the object the hero stands next to, <c>AI::HeroesAction()</c>
    /// (<c>ai_hero_action.h:34</c>).
    /// </summary>
    /// <param name="hero">The hero that acts.</param>
    /// <param name="dstIndex">Tile index of the object.</param>
    public static void HeroesAction(Hero hero, int dstIndex) =>
        throw new NotImplementedException("AiHeroAction.HeroesAction — ai_hero_action.cpp 未移植（ADR-161）");

    /// <summary>
    /// Moves a hero for one turn, <c>AI::HeroesMove()</c> (<c>ai_hero_action.h:37</c>).
    /// </summary>
    /// <param name="hero">The hero to move.</param>
    /// <returns><see cref="AiTurnOutcome.EndTurn"/> when the hero has nothing left to do.</returns>
    public static AiTurnOutcome HeroesMove(Hero hero) =>
        throw new NotImplementedException("AiHeroAction.HeroesMove — ai_hero_action.cpp 未移植（ADR-161）");

    /// <summary>
    /// Makes a hero cast Dimension Door, <c>AI::HeroesCastDimensionDoor()</c> (<c>ai_hero_action.h:40</c>).
    /// </summary>
    /// <param name="hero">The hero that casts.</param>
    /// <param name="targetIndex">Tile to jump to.</param>
    public static void HeroesCastDimensionDoor(Hero hero, int targetIndex) =>
        throw new NotImplementedException("AiHeroAction.HeroesCastDimensionDoor — ai_hero_action.cpp 未移植（ADR-161）");

    /// <summary>
    /// Makes a hero summon a boat, <c>AI::HeroesCastSummonBoat()</c> (<c>ai_hero_action.h:42-46</c>).
    /// </summary>
    /// <remarks>
    /// The caller has to make sure the hero may cast the spell and that there is a boat to summon; the function does
    /// not check either.
    /// </remarks>
    /// <param name="hero">The hero that casts.</param>
    /// <param name="boatDestinationIndex">Tile the boat appears on.</param>
    /// <returns>Tile the boat was on before the summoning.</returns>
    public static int HeroesCastSummonBoat(Hero hero, int boatDestinationIndex) =>
        throw new NotImplementedException("AiHeroAction.HeroesCastSummonBoat — ai_hero_action.cpp 未移植（ADR-161）");
}
