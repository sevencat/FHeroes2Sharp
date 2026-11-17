using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using FHeroes2.Game.Resources;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The helpers every part of the AI uses, the free functions of <c>ai_common.h</c> (<c>ai_common.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the contract, not the implementation</b> (ADR-161): the bodies throw until <c>ai_common.cpp</c> is
/// ported. Their names and parameters follow the header one to one, so the ported bodies can be dropped in without
/// touching a single caller.
/// </para>
/// <para>
/// The reference groups them in <c>namespace AI</c> as free functions because they are shared by the planner, the
/// hero actions and the battle AI; the port keeps that shape instead of hanging them off a class that owns no state.
/// </para>
/// </remarks>
public static class AiCommon
{
    /// <summary>
    /// Builds a building, trading for what is missing, <c>AI::BuildIfPossible()</c> (<c>ai_common.h:46</c>,
    /// <c>ai_common.cpp:57</c>).
    /// </summary>
    /// <param name="castle">Castle to build in.</param>
    /// <param name="building">The building.</param>
    /// <returns><see langword="true"/> when the building now stands.</returns>
    public static bool BuildIfPossible(CastleTown castle, BuildingType building) =>
        throw new NotImplementedException("AiCommon.BuildIfPossible — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// Builds a building if the kingdom can spare the resources, <c>AI::BuildIfEnoughFunds()</c>
    /// (<c>ai_common.h:52</c>, <c>ai_common.cpp:81</c>).
    /// </summary>
    /// <param name="castle">Castle to build in.</param>
    /// <param name="building">The building.</param>
    /// <param name="fundsMultiplier">How many times the price the kingdom must still have left afterwards.</param>
    /// <returns><see langword="true"/> when the building now stands.</returns>
    public static bool BuildIfEnoughFunds(CastleTown castle, BuildingType building, uint fundsMultiplier) =>
        throw new NotImplementedException("AiCommon.BuildIfEnoughFunds — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// Rearranges an army before a battle, <c>AI::OptimizeTroopsOrder()</c> (<c>ai_common.h:55</c>,
    /// <c>ai_common.cpp:102</c>).
    /// </summary>
    /// <param name="army">The army to rearrange; modified in place.</param>
    public static void OptimizeTroopsOrder(GameArmy army) =>
        throw new NotImplementedException("AiCommon.OptimizeTroopsOrder — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// Moves the slowest troops to the garrison so the hero gets a movement bonus next turn,
    /// <c>AI::transferSlowestTroopsToGarrison()</c> (<c>ai_common.h:58</c>, <c>ai_common.cpp:206</c>).
    /// </summary>
    /// <param name="hero">The hero whose army is trimmed.</param>
    /// <param name="castle">The castle that receives the troops, or <see langword="null"/> when the hero is not in one.</param>
    public static void TransferSlowestTroopsToGarrison(Hero hero, CastleTown? castle) =>
        throw new NotImplementedException("AiCommon.TransferSlowestTroopsToGarrison — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// The marketplace deal that would yield a given amount, <c>AI::calculateMarketplaceTransaction()</c>
    /// (<c>ai_common.h:64</c>, <c>ai_common.cpp:249</c>).
    /// </summary>
    /// <remarks>
    /// The returned transaction has to be subtracted from the kingdom's funds to become real; the function itself
    /// changes nothing.
    /// </remarks>
    /// <param name="kingdom">The kingdom that trades.</param>
    /// <param name="fundsToObtain">What the kingdom has to be able to pay afterwards.</param>
    /// <returns>The transaction, or <see langword="null"/> when trading cannot produce the amount.</returns>
    public static Funds? CalculateMarketplaceTransaction(GameKingdom kingdom, in Funds fundsToObtain) =>
        throw new NotImplementedException("AiCommon.CalculateMarketplaceTransaction — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// Trades on the marketplace until the kingdom can pay an amount, <c>AI::tradeAtMarketplace()</c>
    /// (<c>ai_common.h:69</c>, <c>ai_common.cpp:368</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom that trades; its funds change when the trade succeeds.</param>
    /// <param name="fundsToObtain">What the kingdom has to be able to pay afterwards.</param>
    /// <returns><see langword="true"/> when the funds are now available.</returns>
    public static bool TradeAtMarketplace(GameKingdom kingdom, in Funds fundsToObtain) =>
        throw new NotImplementedException("AiCommon.TradeAtMarketplace — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// Tells allied AI kingdoms about an object a hero just visited,
    /// <c>AI::shareObjectVisitInfoWithAllies()</c> (<c>ai_common.h:74</c>, <c>ai_common.cpp:389</c>).
    /// </summary>
    /// <remarks>
    /// Nothing is shared when any ally of the kingdom is a human player: the reference assumes that AI allies talk to
    /// each other and that a human ally does not.
    /// </remarks>
    /// <param name="kingdom">The kingdom whose hero visited the object.</param>
    /// <param name="tileIndex">Tile the object stands on.</param>
    public static void ShareObjectVisitInfoWithAllies(GameKingdom kingdom, int tileIndex) =>
        throw new NotImplementedException("AiCommon.ShareObjectVisitInfoWithAllies — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// Whether a hero may pick the ultimate artifact up, <c>AI::isUltimateArtifactAvailableToHero()</c>
    /// (<c>ai_common.h:79</c>, <c>ai_common.cpp:418</c>).
    /// </summary>
    /// <param name="artifact">The ultimate artifact of the current map.</param>
    /// <param name="hero">The hero that would dig.</param>
    /// <returns><see langword="true"/> when the hero knows where it is and has a free slot.</returns>
    public static bool IsUltimateArtifactAvailableToHero(UltimateArtifact artifact, Hero hero) =>
        throw new NotImplementedException("AiCommon.IsUltimateArtifactAvailableToHero — ai_common.cpp 未移植（ADR-161）");

    /// <summary>
    /// Whether an adventure map object is worth visiting at all, <c>AI::isValuableAdventureMapObject()</c>
    /// (<c>ai_common.h:82</c>, <c>ai_common.cpp:438</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom that would visit it.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="tileIndex">Tile the object stands on.</param>
    /// <returns><see langword="true"/> when the object can benefit the AI under any circumstances.</returns>
    public static bool IsValuableAdventureMapObject(GameKingdom kingdom, Mp2ObjectType objectType, int tileIndex) =>
        throw new NotImplementedException("AiCommon.IsValuableAdventureMapObject — ai_common.cpp 未移植（ADR-161）");
}
