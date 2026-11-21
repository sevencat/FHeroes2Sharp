using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;

namespace FHeroes2.Game.System;

/// <summary>
/// The difficulty dependent switches of <c>namespace Difficulty</c> (<c>src/fheroes2/game/difficulty.h</c>).
/// </summary>
/// <remarks>
/// <para>
/// Only the four the AI needs so far (ADR-195). <c>difficulty.cpp</c> also carries the growth, income, guard and
/// spell multipliers; those belong with the game settings batch and are not ported yet.
/// </para>
/// <para>
/// The reference passes the difficulty in instead of reading <c>Game::getDifficulty()</c>, so the caller decides
/// where it comes from; the AI reads it from <see cref="Settings.GameDifficulty"/>.
/// </para>
/// </remarks>
public static class Difficulty
{
    /// <summary>
    /// Whether the AI may split its weakest stacks to make an attack harder,
    /// <c>Difficulty::allowAIToSplitWeakStacks()</c> (<c>difficulty.cpp:259</c>).
    /// </summary>
    /// <remarks>
    /// Easy and normal keep the stacks merged; hard and above split them, because a split stack soaks a whole
    /// attack instead of a fraction of one.
    /// </remarks>
    public static bool AllowAiToSplitWeakStacks(GameDifficulty difficulty) =>
        difficulty is not (GameDifficulty.Easy or GameDifficulty.Normal);

    /// <summary>
    /// Whether AI kingdoms allied to each other share what their heroes visit,
    /// <c>Difficulty::isObjectVisitInfoSharingAllowedForAI()</c> (<c>difficulty.cpp:294</c>).
    /// </summary>
    /// <remarks>
    /// Same two levels as <see cref="AllowAiToSplitWeakStacks"/>; the reference keeps them separate because the
    /// two switches are tuned independently.
    /// </remarks>
    public static bool IsObjectVisitInfoSharingAllowedForAi(GameDifficulty difficulty) =>
        difficulty is not (GameDifficulty.Easy or GameDifficulty.Normal);

    /// <summary>
    /// Whether the AI may develop its castles on a given day,
    /// <c>Difficulty::allowAIToDevelopCastlesOnDay()</c> (<c>difficulty.cpp:313</c>).
    /// </summary>
    /// <remarks>
    /// Easy develops every other day - and every day in a campaign, where the AI is meant to keep up with the player.
    /// Everything above Easy is not restricted at all. The day is counted from one, so Easy does not develop on the
    /// first day of the game.
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <param name="isCampaign">Whether the game is a campaign.</param>
    /// <param name="day">The current day of the game.</param>
    /// <returns>Whether the castles may be developed today.</returns>
    public static bool AllowAiToDevelopCastlesOnDay(GameDifficulty difficulty, bool isCampaign, uint day) =>
        difficulty != GameDifficulty.Easy || isCampaign || day % 2 == 0;

    /// <summary>
    /// Whether the AI may build a given building in a castle,
    /// <c>Difficulty::allowAIToBuildCastleBuilding()</c> (<c>difficulty.cpp:325</c>).
    /// </summary>
    /// <remarks>
    /// Easy is not allowed the last dwelling nor the last mage guild, which is what keeps its castles from fielding
    /// the strongest units; the upgrades of the sixth dwelling are not restricted, only the dwelling itself. A
    /// campaign lifts the restriction.
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <param name="isCampaign">Whether the game is a campaign.</param>
    /// <param name="building">The building to build.</param>
    /// <returns>Whether the building may be built.</returns>
    public static bool AllowAiToBuildCastleBuilding(GameDifficulty difficulty, bool isCampaign, BuildingType building) =>
        difficulty != GameDifficulty.Easy || isCampaign
        || building is not (BuildingType.DwellingMonster6 or BuildingType.MageGuild5);
}
