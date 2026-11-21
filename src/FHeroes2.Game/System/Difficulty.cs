using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;

namespace FHeroes2.Game.System;

/// <summary>
/// The difficulty dependent switches of <c>namespace Difficulty</c> (<c>src/fheroes2/game/difficulty.h</c>).
/// </summary>
/// <remarks>
/// <para>
/// 移植的是 AI 与守卫法术用得到的那些开关（ADR-195）。<c>difficulty.cpp</c> 里还有成长、收入等乘数，那些随
/// 游戏设置一起补。
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

    /// <summary>
    /// Whether the AI may give its heroes different roles,
    /// <c>Difficulty::areAIHeroRolesAllowed()</c> (<c>difficulty.cpp:220</c>).
    /// </summary>
    /// <remarks>
    /// Easy only: every hero is a hunter, so the AI neither concentrates its army in a champion nor spreads it over
    /// scouts and couriers. Every other difficulty lets the roles be handed out.
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <returns>Whether the roles may differ from hunter.</returns>
    public static bool AreAiHeroRolesAllowed(GameDifficulty difficulty) => difficulty != GameDifficulty.Easy;

    /// <summary>
    /// Whether the basic battle logic applies, <c>Difficulty::isBasicAIBattleLogicApplicable()</c>
    /// (<c>difficulty.cpp:338-349</c>).
    /// </summary>
    /// <remarks>
    /// 「基础战斗逻辑」指只按伤害算价值的做法，不做噬魂/吸血的复活点数、后续回合收益这类额外估算。
    /// 参考的规则出乎意料却是明确的：Easy 下**只有人类**用基础逻辑，其余难度下**只有 AI** 用基础逻辑
    /// （为了让高难度下的人类也享受完整估算）。
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <param name="isControlledByHuman">Whether a human gives the unit its orders.</param>
    /// <returns>Whether only the basic battle logic is used.</returns>
    public static bool IsBasicAiBattleLogicApplicable(GameDifficulty difficulty, bool isControlledByHuman) =>
        difficulty == GameDifficulty.Easy ? isControlledByHuman : !isControlledByHuman;

    /// <summary>
    /// How much stronger the enemy must be before the AI runs away,
    /// <c>Difficulty::getArmyStrengthRatioForAIRetreat()</c> (<c>difficulty.cpp:187-205</c>).
    /// </summary>
    /// <remarks>
    /// 参考给的是「敌方 ÷ 我方」的倍数：Normal 13.3 倍、Hard/Expert 11.8 倍、Impossible 10 倍、Easy 16.7 倍。
    /// 调用方写成 <c>我方 × 该值 ≥ 敌方</c>，即只有兵力低于敌方的 1/13 左右才考虑撤退。
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <returns>The ratio.</returns>
    public static double GetArmyStrengthRatioForAiRetreat(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Normal => 100.0 / 7.5,
        GameDifficulty.Hard or GameDifficulty.Expert => 100.0 / 8.5,
        GameDifficulty.Impossible => 100.0 / 10.0,
        _ => 100.0 / 6.0,
    };

    /// <summary>
    /// How far ahead in stats an AI hero must be before it bothers to meet a friendly hero of the same role,
    /// <c>Difficulty::getMinStatDiffForAIHeroesMeeting()</c> (<c>difficulty.cpp:234-255</c>).
    /// </summary>
    /// <remarks>
    /// Easy keeps armies merged only when the difference is huge; every other level merges two heroes whose stats are
    /// almost equal.
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <returns>The minimum difference in stats.</returns>
    public static int GetMinStatDiffForAiHeroesMeeting(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Easy => 10,
        GameDifficulty.Normal or GameDifficulty.Hard or GameDifficulty.Expert or GameDifficulty.Impossible => 2,
        _ => 2,
    };

    /// <summary>
    /// How many dimension doors the AI may open per turn, <c>Difficulty::GetDimensionDoorLimitForAI()</c>
    /// (<c>difficulty.cpp:204-218</c>).
    /// </summary>
    /// <remarks>
    /// Easy, Normal and Hard are capped; Expert and Impossible are not, which is the same as saying the spell
    /// points and the movement points are the only limits there.
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <returns>The number of doors per turn.</returns>
    public static uint GetDimensionDoorLimitForAi(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Easy => 1,
        GameDifficulty.Normal => 2,
        GameDifficulty.Hard => 3,
        _ => uint.MaxValue,
    };

    /// <summary>
    /// Whether the AI may guess that an object which is useless now will refill later,
    /// <c>Difficulty::isFutureObjectPredictionAllowedForAI()</c> (<c>difficulty.cpp:361-366</c>).
    /// </summary>
    /// <remarks>
    /// Easy only: the AI looks at what is there and does not plan a trip around next week's refill.
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <returns>Whether the future value of an object may be used.</returns>
    public static bool IsFutureObjectPredictionAllowedForAi(GameDifficulty difficulty) => difficulty != GameDifficulty.Easy;

    /// <summary>
    /// How strongly a neutral guardian's spells hit, <c>Difficulty::getGuardianSpellMultiplier()</c>
    /// (<c>difficulty.cpp:272-292</c>).
    /// </summary>
    /// <remarks>
    /// 这是「守卫法术强度」的百分数分母意义上的档位值：数字越小，守卫的法术越弱，玩家的难度越低。参考给五个档位
    /// 写死 16/14/12/10/8，越界值走 <c>assert( 0 )</c> 后返回 20（最弱的默认值）。
    /// </remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <returns>The multiplier, 8 to 20.</returns>
    public static int GetGuardianSpellMultiplier(GameDifficulty difficulty) => difficulty switch
    {
        GameDifficulty.Easy => 16,
        GameDifficulty.Normal => 14,
        GameDifficulty.Hard => 12,
        GameDifficulty.Expert => 10,
        GameDifficulty.Impossible => 8,
        _ => UnsupportedGuardianSpellMultiplier(),
    };

    /// <summary>
    /// Whether the AI may reorder a hero's artifacts, <c>Difficulty::isArtifactSortingAllowedForAI()</c>
    /// (<c>difficulty.cpp:350-361</c>).
    /// </summary>
    /// <remarks>Easy 与 Normal 不让 AI 整理宝物栏，Hard 及以上允许。</remarks>
    /// <param name="difficulty">Difficulty of the game.</param>
    /// <returns>Whether the sorting pass may run.</returns>
    public static bool IsArtifactSortingAllowedForAi(GameDifficulty difficulty) => difficulty != GameDifficulty.Easy && difficulty != GameDifficulty.Normal;

    /// <summary>Logs the reference's <c>assert( 0 )</c> for an unknown difficulty.</summary>
    /// <returns>The value the reference falls through to, <c>20</c>.</returns>
    private static int UnsupportedGuardianSpellMultiplier()
    {
        Debug.Assert(false, "Unknown difficulty level.");

        return 20;
    }
}
