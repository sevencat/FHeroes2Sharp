using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The two difficulty corrections of the daily income: the computer's bonus and the human's handicap
/// (<c>kingdom.cpp:61-101</c> and <c>difficulty.cpp:59-185</c>).
/// </summary>
/// <remarks>
/// <para>
/// These four functions are the part of <c>Kingdom::GetIncome()</c> that is <b>not</b> a sum over the kingdom's own
/// property: they read the difficulty and the handicap tables. The reference keeps the handicap pair in an anonymous
/// namespace at the top of <c>kingdom.cpp</c> and the AI pair in <c>difficulty.cpp</c>; the port keeps all four here so
/// that <see cref="GetIncome(Func{int, uint}, IncomeType, GameDifficulty, Func{Funds}?)"/> stays readable.
/// </para>
/// <para>
/// 四个函数都是纯函数（不吃全局、不改状态），测量口径与参考逐条对齐：手牌折扣与 AI 加成互不相干——手牌只对
/// 人类生效，AI 加成只对电脑生效，两者永远不会同时落到同一份收入上。
/// </para>
/// </remarks>
public sealed partial class Kingdom
{
    /// <summary>
    /// How much of the income a handicap leaves, <c>getHandicapIncomePercentage()</c> (<c>kingdom.cpp:63-78</c>).
    /// </summary>
    /// <remarks>
    /// 三档：无 100%、轻度 85%、重度 70%。参考对未知档断言后回 100——加了一档却忘了改这里，宁可不少给玩家钱。
    /// </remarks>
    /// <param name="handicap">The handicap of the player.</param>
    /// <returns>A percentage, 0…100.</returns>
    public static int GetHandicapIncomePercentage(Player.HandicapStatus handicap) => handicap switch
    {
        Player.HandicapStatus.Mild => 85,
        Player.HandicapStatus.Severe => 70,
        Player.HandicapStatus.None => 100,
        _ => 100,
    };

    /// <summary>
    /// Cuts an income down to what a handicap allows, <c>getHandicapDependentIncome()</c>
    /// (<c>kingdom.cpp:80-95</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每个资源（含金）各自算 <c>min( 原值, (原值 × 百分比 + 99) / 100 )</c>。那个 <c>+99</c> 是向上取整：
    /// 85% 的 6 变成 6（<c>(510+99)/100 = 6</c>），不会被砍成 5。参考对每个资源都写了同样一行，包括金。
    /// </para>
    /// <para>
    /// <c>min</c> 不是多余的：<see cref="Funds"/> 的字段是 <see cref="int"/>，负数收入（参考里不会出现，但事件可以
    /// 造出来）在取整时会被「放大」，<c>min</c> 把它按回原值，折扣永远不会让负数更负。
    /// </para>
    /// </remarks>
    /// <param name="income">The income before the handicap.</param>
    /// <param name="handicap">The handicap of the player.</param>
    /// <returns>The income after the handicap.</returns>
    public static Funds ApplyHandicap(in Funds income, Player.HandicapStatus handicap)
    {
        int percentage = GetHandicapIncomePercentage(handicap);

        Funds corrected = income;

        corrected.Wood = Math.Min(corrected.Wood, (corrected.Wood * percentage + 99) / 100);
        corrected.Mercury = Math.Min(corrected.Mercury, (corrected.Mercury * percentage + 99) / 100);
        corrected.Ore = Math.Min(corrected.Ore, (corrected.Ore * percentage + 99) / 100);
        corrected.Sulfur = Math.Min(corrected.Sulfur, (corrected.Sulfur * percentage + 99) / 100);
        corrected.Crystal = Math.Min(corrected.Crystal, (corrected.Crystal * percentage + 99) / 100);
        corrected.Gems = Math.Min(corrected.Gems, (corrected.Gems * percentage + 99) / 100);
        corrected.Gold = Math.Min(corrected.Gold, (corrected.Gold * percentage + 99) / 100);

        return corrected;
    }

    /// <summary>
    /// The extra resources the computer gets on the harder difficulties,
    /// <c>Difficulty::getResourceIncomeBonusForAI()</c> (<c>difficulty.cpp:59-172</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 三档递进，前两档简单到可以一句话说完：
    /// </para>
    /// <list type="bullet">
    /// <item><b>困难</b>：半份金矿收入；</item>
    /// <item><b>专家</b>：一份金矿收入，加上城堡那份的一半；</item>
    /// <item><b>不可能</b>：一份半金矿收入，加上整份城堡补贴。</item>
    /// </list>
    /// <para>
    /// 「城堡那份」逐城算，每城先给一份金矿收入（让电脑买得起每周的增长），再按<b>六级兵营 + 水井</b>决定要不要
    /// 补稀有资源——没有水井时增长只有 1–2，补了反而打乱建设节奏。补哪种稀有资源看城堡的族：野蛮人要水晶、
    /// 女巫要水银、术士要硫磺、法师要宝石；骑士与亡灵的高阶兵不要稀有资源，所以不补。
    /// </para>
    /// <para>
    /// 补稀有资源的<b>前提是这个王国已经有来源</b>（有市场，或者占着对应的矿）——否则电脑会拿自己买不到的资源，
    /// 那是白给。术士与法师的最高级兵特别贵，所以额外按雕像收入补 1–3 份。
    /// </para>
    /// <para>
    /// 简单与普通档回空，这就是「参考里没有加成」的那一档。
    /// </para>
    /// </remarks>
    /// <param name="difficulty">Map difficulty.</param>
    /// <param name="castles">The towns of the kingdom, <c>GetCastles()</c>.</param>
    /// <param name="countCapturedMines">How many mines of one resource the color owns, <c>world.CountCapturedMines</c>.</param>
    /// <returns>The bonus, added on top of the normal income.</returns>
    public static Funds GetResourceIncomeBonusForAi(
        GameDifficulty difficulty,
        IReadOnlyList<CastleTown> castles,
        Func<int, uint> countCapturedMines)
    {
        ArgumentNullException.ThrowIfNull(castles);
        ArgumentNullException.ThrowIfNull(countCapturedMines);

        Funds goldMine = ProfitConditions.FromMine(Resource.Gold);

        switch (difficulty)
        {
            case GameDifficulty.Hard:
                return goldMine / 2;

            case GameDifficulty.Expert:
                return goldMine + GetCastleBonusForAi(castles, countCapturedMines) / 2;

            case GameDifficulty.Impossible:
                return goldMine * 3 / 2 + GetCastleBonusForAi(castles, countCapturedMines);

            default:
                return default;
        }
    }

    /// <summary>
    /// The share of the gold income a difficulty adds or takes, <c>Difficulty::getGoldIncomeBonusForAI()</c>
    /// (<c>difficulty.cpp:174-185</c>).
    /// </summary>
    /// <remarks>
    /// 只有简单档回 −0.25（电脑少拿四分之一），其余全为 0。参考的注释特地写了「It is deduction from the income」，
    /// 因为它返回的是<b>比例</b>而不是绝对值，负数很容易被误读成「简单档电脑更穷」以外的意思。
    /// </remarks>
    /// <param name="difficulty">Map difficulty.</param>
    /// <returns>The fraction of the gold income to add.</returns>
    public static double GetGoldIncomeBonusForAi(GameDifficulty difficulty) =>
        difficulty == GameDifficulty.Easy ? -0.25 : 0.0;

    /// <summary>
    /// The per-town part of <see cref="GetResourceIncomeBonusForAi"/>, <c>getBonusForCastles()</c>
    /// (<c>difficulty.cpp:71-158</c>).
    /// </summary>
    /// <param name="castles">The towns of the kingdom.</param>
    /// <param name="countCapturedMines">How many mines of one resource the color owns.</param>
    /// <returns>The bonus of all towns together.</returns>
    private static Funds GetCastleBonusForAi(IReadOnlyList<CastleTown> castles, Func<int, uint> countCapturedMines)
    {
        Funds result = default;

        bool hasMarketplace = castles.Any(castle => castle.IsBuild(BuildingType.Marketplace));

        bool HasResourceSource(int resource) => hasMarketplace || countCapturedMines(resource) > 0;

        foreach (CastleTown castle in castles)
        {
            // One gold mine per town: enough to buy the weekly growth.
            result += ProfitConditions.FromMine(Resource.Gold);

            // Only towns that can actually grow to the top tier get the rare resources.
            if (!castle.IsBuild(BuildingType.DwellingMonster6) || !castle.IsBuild(BuildingType.Well))
            {
                continue;
            }

            switch (castle.Race)
            {
                case MapRace.Barbarian:
                    if (HasResourceSource(Resource.Crystal))
                    {
                        result += ProfitConditions.FromMine(Resource.Crystal);
                    }

                    break;

                case MapRace.Sorceress:
                    if (HasResourceSource(Resource.Mercury))
                    {
                        result += ProfitConditions.FromMine(Resource.Mercury);
                    }

                    break;

                case MapRace.Warlock:
                    if (HasResourceSource(Resource.Sulfur))
                    {
                        result += ProfitConditions.FromMine(Resource.Sulfur);
                    }

                    // 黑龙 4000、赤龙 3500、绿龙 3000：按雕像收入补 3、2、1 份。
                    if (castle.IsBuild(BuildingType.DwellingUpgrade7))
                    {
                        result += ProfitConditions.FromBuilding(BuildingType.Statue, castle.Race) * 3;
                    }
                    else if (castle.IsBuild(BuildingType.DwellingUpgrade6))
                    {
                        result += ProfitConditions.FromBuilding(BuildingType.Statue, castle.Race) * 2;
                    }
                    else
                    {
                        result += ProfitConditions.FromBuilding(BuildingType.Statue, castle.Race);
                    }

                    break;

                case MapRace.Wizard:
                    if (HasResourceSource(Resource.Gems))
                    {
                        result += ProfitConditions.FromMine(Resource.Gems);
                    }

                    // 泰坦 5000、巨人 2000。
                    if (castle.IsBuild(BuildingType.DwellingUpgrade6))
                    {
                        result += ProfitConditions.FromBuilding(BuildingType.Statue, castle.Race) * 3;
                    }
                    else
                    {
                        result += ProfitConditions.FromBuilding(BuildingType.Statue, castle.Race);
                    }

                    break;

                case MapRace.Knight:
                case MapRace.Necromancer:
                    // 这两个族的高阶兵不要稀有资源，所以只留下那一份金矿。
                    break;

                default:
                    break;
            }
        }

        return result;
    }
}
