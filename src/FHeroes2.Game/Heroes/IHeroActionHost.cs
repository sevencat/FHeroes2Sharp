using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;

using CastleTown = FHeroes2.Game.Castle.Castle;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The questions the reference puts to the player in the middle of a hero action, <c>heroes_action.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考里每个问句都是一个对话框（<c>Dialog::ArmyJoinFree</c>、<c>Dialog::ArmyJoinWithCost</c>、
/// <c>Dialog::RunAway</c>），各带着兵种、人数与价钱；端口把它们收成一个枚举，措辞归界面（ADR-122），
/// 游戏层只关心「问了什么」与「答的是不是」。
/// </para>
/// <para>
/// 为什么收成枚举而不是每支动作各自收一个 <c>Func</c>：怪物、城堡、英雄、招募所、事件、神谕……十几支都要问
/// 同一批问题，一处处传会把这个类变成一张参数表；而且 AI 需要的正是「同一批问题的另一种回答」，枚举正好是
/// 那条缝的两端。
/// </para>
/// </remarks>
public enum HeroActionQuestion
{
    /// <summary>
    /// 「他们想加入你，收下吗？」<c>Dialog::ArmyJoinFree</c> (<c>heroes_action.cpp:362</c>).
    /// </summary>
    JoinForFree,

    /// <summary>
    /// 「他们愿意为你效力，每人 <paramref name="gold"/> 金，收下吗？」<c>Dialog::ArmyJoinWithCost</c>
    /// (<c>heroes_action.cpp:392</c>).
    /// </summary>
    JoinForMoney,

    /// <summary>
    /// 「要追上去打吗？」<c>Dialog::RunAway</c> (<c>heroes_action.cpp:371</c>)——怪物弱于英雄时问的，
    /// 答「不」他们就散了。
    /// </summary>
    PursueFleeingMonsters,

    /// <summary>
    /// 「要搜这艘沉船/这座坟吗？守军会被惊动。」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:1391</c>).
    /// </summary>
    SearchPoorMoraleObject,

    /// <summary>
    /// 「要进这座废矿吗？里面有幽灵。」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:2194</c>).
    /// </summary>
    EnterAbandonedMine,

    /// <summary>
    /// 「要搜这座金字塔吗？故事里说有诅咒与亡灵。」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:1176</c>).
    /// </summary>
    SearchPyramid,

    /// <summary>
    /// 「要进这座洞窟吗？」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:3069</c>).
    /// </summary>
    EnterDaemonCave,

    /// <summary>
    /// 「你要打我的仆从吗？」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:3082</c>)——答「不」就是打恶魔本人，
    /// 落进那条「拿钱赎命」或「直接拿赏」的支线。
    /// </summary>
    FightDaemonServants,

    /// <summary>
    /// 「拿 <paramref name="gold"/> 金赎命吗？」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:3159</c>)——答「不」
    /// 就是全军覆没。
    /// </summary>
    PayDaemonRansom,

    /// <summary>
    /// 「一群想挣功名的 <paramref name="troop"/> 愿意投你，收下吗？」<c>heroes_action.cpp:2247</c>——九座怪物居所，
    /// 白送。
    /// </summary>
    JoinDwellingMonsters,

    /// <summary>
    /// 「这些 <paramref name="troop"/> 愿意收钱办事，要招吗？」<c>heroes_action.cpp:2374</c>——招募型居所与
    /// 打服过的居所都用它，问句之后才是数量窗。
    /// </summary>
    RecruitDwellingMonsters,

    /// <summary>
    /// 「<paramref name="troop"/> 在挑战你，要打吗？」<c>heroes_action.cpp:2459</c>——三座可打服的居所。
    /// </summary>
    FightDwellingMonsters,

    /// <summary>
    /// 「守军服了，愿意收钱加入，要招吗？」<c>heroes_action.cpp:2500</c>——与
    /// <see cref="RecruitDwellingMonsters"/> 是同一件事，但参考用了另一段文案（「幸存的那些」）。
    /// </summary>
    RecruitDefeatedDwellingMonsters,

    /// <summary>
    /// 「要搓这盏精灵灯吗？里面有个精灵。」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:1927</c>)——答「是」之后
    /// 才是招募窗。
    /// </summary>
    RubGenieLamp,

    /// <summary>
    /// 「炼金术士愿意帮你把身上的诅咒烧掉，<paramref name="gold"/> 金，要吗？」
    /// <c>Dialog::YES|NO</c> (<c>heroes_action.cpp:3287</c>).
    /// </summary>
    PayAlchemist,

    /// <summary>
    /// 「答对有关，答错被吞，敢接吗？」<c>Dialog::YES|NO</c> (<c>heroes_action.cpp:3547-3555</c>)——答「不」就
    /// 当没来过，连访问都不记。
    /// </summary>
    AcceptSphinxChallenge,
}

/// <summary>
/// What a hero action needs from the layers above it: the player's answers, the battle screen and the castle screen.
/// </summary>
/// <remarks>
/// <para>
/// 参考的英雄动作直接伸手去拿界面：<c>Dialog::YES|NO</c> 之类的弹框、<c>Battle::Loader</c> 开战斗界面、
/// <c>Game::OpenCastleDialog</c> 开城堡屏；AI 那一支则整段绕开（<c>ai_hero_action.cpp</c> 里 34 处
/// <c>Interface::AdventureMap::Get()</c>）。端口没有对话框层、也没有全局单例（ADR-122），于是这些「必须现在
/// 就知道答案」的点收进这个接口：人这一支由界面层实现，AI 与测试用 <see cref="NullHeroActionHost"/>。
/// </para>
/// <para>
/// <b>只有「现在就要答案」的东西在这里</b>。重绘、雷达、音效、动画这些事后能补的一律不做——它们不是动作的
/// 输入，调用方按 <see cref="HeroActionResult.Changed"/> 自己补。这就是参考那 34 处 <c>Get()</c> 里真正
/// 影响状态的那一小撮。
/// </para>
/// <para>
/// 成员各自对应参考的一类调用：<see cref="BattlePresenter"/> 是 <c>Battle::Loader</c> 的界面参数，
/// <see cref="Ask(HeroActionQuestion, Troop, uint)"/> 是「是/否」那些 <c>Dialog::*</c>，
/// <see cref="AskRecruitCount"/> 是唯一一个要玩家给<b>数字</b>的窗（<c>Dialog::RecruitMonster</c>），
/// <see cref="OpenCastle"/> 是 <c>Game::OpenCastleDialog</c>。之后补齐事件、神谕等分支时，新问题加进
/// <see cref="HeroActionQuestion"/>，不减不改已有的。
/// </para>
/// </remarks>
public interface IHeroActionHost
{
    /// <summary>
    /// The battle presentation layer, <c>Battle::Loader</c>'s interface argument.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示没人在看：战斗照打，只是无人观战，等价于参考的 <c>showBattle == false</c>。
    /// 人类玩家的战斗界面属于 P3，在那之前界面层也传 <see langword="null"/>，于是人打的仗与 AI 打的仗走同一段代码。
    /// </remarks>
    IBattlePresenter? BattlePresenter { get; }

    /// <summary>
    /// Puts one of the reference's questions to the player, <c>Dialog::ArmyJoinFree</c> and friends.
    /// </summary>
    /// <remarks>
    /// 参考在被拒绝之后往往紧跟一条提示（「Insulted by your refusal, they attack!」之类），那句提示由实现
    /// 自己补：本方法只回答「是/不是」，因为拒绝之后紧接着就开打，游戏层没有第二次停下来的机会。
    /// </remarks>
    /// <param name="question">Which question.</param>
    /// <param name="troop">The monster stack concerned.</param>
    /// <param name="gold">Price per creature for <see cref="HeroActionQuestion.JoinForMoney"/>, zero otherwise.</param>
    /// <returns><see langword="true"/> for the reference's <c>Dialog::YES</c>.</returns>
    bool Ask(HeroActionQuestion question, Troop troop, uint gold = 0);

    /// <summary>
    /// Same, for the questions that carry no stack, <c>Dialog::YES|NO</c> of <c>heroes_action.cpp:1391</c> and
    /// <c>2194</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 搜沉船、进废矿这两处参考只给一段提示加两个按钮，提示里的故事（「幽灵在看守」）属于文案，界面按
    /// <paramref name="question"/> 自己挑。
    /// </para>
    /// <para>
    /// 之所以分成两个重载而不是给 <see cref="Ask(HeroActionQuestion, Troop, uint)"/> 的兵种参数一个缺省：
    /// 兵种是问句的一部分（「一群幽灵愿意为你效力」），缺省的 <c>default</c> 会让「忘了传」看起来像「空兵种」
    /// 而不是编译错误。
    /// </para>
    /// <para>
    /// <paramref name="gold"/> 只有<b>报出价钱</b>的问句才用（目前是 <see cref="HeroActionQuestion.PayDaemonRansom"/>），
    /// 它是问句里的一部分而不是玩家要付的钱——扣钱在游戏层，宿主只负责把数字念出来。
    /// </para>
    /// </remarks>
    /// <param name="question">Which question.</param>
    /// <param name="gold">The price the question quotes, zero when it quotes none.</param>
    /// <returns><see langword="true"/> for the reference's <c>Dialog::YES</c>.</returns>
    bool Ask(HeroActionQuestion question, uint gold = 0);

    /// <summary>
    /// How many of the stack to buy, <c>Dialog::RecruitMonster()</c> (<c>heroes_action.cpp:276</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这个不是「是/否」：参考的招募窗带一个数量滑条与总价，还要按买家的钱袋封顶，所以只能让界面把数字报回来。
    /// 窗口自己会读 <paramref name="kingdom"/> 的钱，游戏层不重复算——它只按返回值扣钱、加兵、改瓦片。
    /// </para>
    /// <para>
    /// 返回 <c>0</c> 等于玩家取消：什么都没买、什么都没扣。返回超过 <paramref name="available"/> 的数属于宿主违约，
    /// 游戏层会先断言再夹到 <paramref name="available"/>（存档里的数字不能交给宿主管）。
    /// </para>
    /// </remarks>
    /// <param name="monster">Which creature.</param>
    /// <param name="available">How many stand there, the window's upper bound.</param>
    /// <param name="kingdom">The buyer's kingdom, the window's purse.</param>
    /// <returns>0 to <paramref name="available"/>.</returns>
    uint AskRecruitCount(MonsterType monster, uint available, PlayerKingdom kingdom);

    /// <summary>
    /// The sphinx's riddle, <c>Dialog::inputString()</c> (<c>heroes_action.cpp:3563-3564</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是<b>唯一</b>一个要玩家敲字的问题，而且参考连「取消」都不区分：输入框返回什么就拿什么去比对，
    /// 空字符串照样是一次错误的回答（所以想反悔只能在上一句的「接不接」那里反悔）。
    /// </para>
    /// <para>
    /// 比对留给游戏层（<see cref="MapSphinx.IsCorrectAnswer"/> 有「前四个字母」这条老规矩），宿主只负责收集字符。
    /// </para>
    /// </remarks>
    /// <param name="riddle">The riddle to show above the input field.</param>
    /// <returns>What the player typed, empty when they typed nothing.</returns>
    string AskRiddle(string riddle);

    /// <summary>
    /// Opens the castle screen, <c>Game::OpenCastleDialog()</c> (<c>castle.cpp:1425</c>).
    /// </summary>
    /// <param name="castle">The castle to show.</param>
    void OpenCastle(CastleTown castle);

    /// <summary>
    /// Opens the marketplace, <c>Dialog::Marketplace()</c> (<c>heroes_action.cpp:1289</c>).
    /// </summary>
    /// <remarks>
    /// 市场和别的对象不同：<b>它什么都不改</b>，买卖全在窗口里完成，所以游戏层既没有结果也没有访问记录。
    /// 参考的第二个参数只影响标题那一行字（「市集」还是「贸易站」），端口照传。
    /// </remarks>
    /// <param name="kingdom">The kingdom that trades.</param>
    /// <param name="fromTradingPost">Whether the window was opened by a trading post rather than a castle market.</param>
    void OpenMarketplace(PlayerKingdom kingdom, bool fromTradingPost);

    /// <summary>
    /// Opens the thieves' guild report, <c>Dialog::ThievesGuild()</c> (<c>heroes_action.cpp:3039</c>).
    /// </summary>
    /// <remarks>
    /// 参考的布尔值是「这是不是神谕」：神谕那份没有「本月谁最富」之类的偷听味道，标题与措辞不同。
    /// 报表本身只读，不改任何状态。
    /// </remarks>
    /// <param name="kingdom">The kingdom that asks.</param>
    void OpenThievesGuild(PlayerKingdom kingdom);
}

/// <summary>
/// The answer of a host that is not there: every question is <c>NO</c>, no battle is watched, no castle is opened.
/// </summary>
/// <remarks>
/// <para>
/// 给 AI 与测试用。全答「不」而不是全答「是」，因为「不」在参考里总是安全的那一支——不打、不买、不入城；
/// 「是」会改世界状态（扣钱、开战、少一堆怪物），拿它当默认值会让一个没接线的调用方悄悄改坏存档。
/// </para>
/// <para>
/// 单例：这一类没有任何状态，多个 <see cref="HeroActions"/> 共用一个即可。
/// </para>
/// </remarks>
public sealed class NullHeroActionHost : IHeroActionHost
{
    /// <summary>
    /// The only instance.
    /// </summary>
    public static NullHeroActionHost Instance { get; } = new();

    private NullHeroActionHost()
    {
    }

    /// <inheritdoc/>
    public IBattlePresenter? BattlePresenter => null;

    /// <inheritdoc/>
    public bool Ask(HeroActionQuestion question, Troop troop, uint gold = 0) => false;

    /// <inheritdoc/>
    public bool Ask(HeroActionQuestion question, uint gold = 0) => false;

    /// <inheritdoc/>
    public uint AskRecruitCount(MonsterType monster, uint available, PlayerKingdom kingdom) => 0;

    /// <inheritdoc/>
    public string AskRiddle(string riddle) => string.Empty;

    /// <inheritdoc/>
    public void OpenCastle(CastleTown castle)
    {
        // 参考在这里打开城堡屏；无人的宿主什么都不开。「走进去就占了这座城」那部分状态在游戏层已经改完。
    }

    /// <inheritdoc/>
    public void OpenMarketplace(PlayerKingdom kingdom, bool fromTradingPost)
    {
        // 买卖全在窗口里做，无人的宿主于是什么都不发生——AI 本来也不逛市场（它在 ai_hero_action 里自己算）。
    }

    /// <inheritdoc/>
    public void OpenThievesGuild(PlayerKingdom kingdom)
    {
        // 报表只读，不开也没人损失什么。
    }
}
