using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Battle;

/// <summary>
/// Everything the battle loader asks of the presentation layer, the counterpart of the dialogs
/// <c>battle_main.cpp</c> and <c>arena.DialogBattleSummary</c> open.
/// </summary>
/// <remarks>
/// 参考把这些对话框直接写在 <c>Battle::Loader</c> 里（<c>battle_main.cpp:410-467</c>）；端口的表现层在
/// <c>FHeroes2.UI</c>，所以这里只留回调，Game 层不碰任何界面对象。每个方法对应参考里的一个明确调用点，
/// 注释里标了行号；<see langword="null"/> 表示「无人观看」，等价于参考里 <c>showBattle == false</c>。
/// </remarks>
public interface IBattlePresenter
{
    /// <summary>
    /// Lets the arena fade out after the fight, <c>Arena::FadeArena()</c> (<c>battle_main.cpp:408</c>).
    /// </summary>
    /// <param name="clearMessageLog">Whether the battle log is wiped, which the reference does after a retreat.</param>
    void FadeArena(bool clearMessageLog);

    /// <summary>
    /// Shows the result dialog, <c>Arena::DialogBattleSummary()</c> (<c>battle_main.cpp:410-414</c>).
    /// </summary>
    /// <param name="result">The result of the fight.</param>
    /// <param name="artifactsToTransfer">What the winner would take from the loser, empty when nobody can.</param>
    /// <param name="autoResolve">Whether the fight ran as an auto battle.</param>
    /// <returns>
    /// Whether the player asked to fight the battle again by hand, which makes the loader replay it.
    /// </returns>
    bool ShowSummary(BattleResult result, IReadOnlyList<ArtifactType> artifactsToTransfer, bool autoResolve);

    /// <summary>
    /// Tells the player how many skeletons necromancy raised, <c>Arena::DialogBattleNecromancy()</c>
    /// (<c>battle_main.cpp:247</c>).
    /// </summary>
    /// <param name="raiseCount">How many skeletons joined the winner's army.</param>
    void ShowNecromancy(uint raiseCount);

    /// <summary>
    /// Tells the player that an artifact set came together, <c>Dialog::ArtifactSetAssembled()</c>
    /// (<c>battle_main.cpp:444</c>).
    /// </summary>
    /// <param name="artifact">The artifact the set produces.</param>
    void ShowArtifactSetAssembled(ArtifactType artifact);

    /// <summary>
    /// Tells the player which spells Eagle Eye just taught, <c>eagleEyeSkillAction()</c>
    /// (<c>battle_main.cpp:216-231</c>).
    /// </summary>
    /// <param name="spells">The spells added to the winner's book.</param>
    void ShowEagleEyeLearned(IReadOnlyList<SpellType> spells);
}

/// <summary>
/// What the loader needs to start one battle, the port's replacement for the globals
/// <c>Battle::Loader( Army &amp;, Army &amp;, const int32_t )</c> reaches for (<c>battle.h:68</c>,
/// <c>battle_main.cpp:291-535</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考从全局拿三样东西：两军的指挥官（<c>Army::GetCommander()</c>）、<c>Settings</c>（自动战斗开关）、
/// 以及 <c>world.GetMapSeed()</c>。端口不设全局，所以全部显式传进来；<see cref="Castle"/> 有值就表示
/// 防守方在城里，等价于参考的 <c>Arena::GetCastle()</c> 非空。
/// </para>
/// <para>
/// 参考的循环（<c>battle_main.cpp:387-500</c>）允许把一场自动战斗重打成手动，所以 <see cref="Load"/>
/// 可能在 <see cref="IBattlePresenter.ShowSummary"/> 返回 <see langword="true"/> 时重跑一次，并且要先把
/// 两军的法术点与两国金库恢复到战前。
/// </para>
/// </remarks>
public sealed class BattleLoadRequest
{
    /// <summary>The world the battle happens in, needed for the map seed and the kingdoms.</summary>
    public required World World { get; init; }

    /// <summary>The army that attacked, <c>Battle::Loader</c>'s first argument.</summary>
    public required GameArmy AttackingArmy { get; init; }

    /// <summary>The army that defends, <c>Battle::Loader</c>'s second argument.</summary>
    public required GameArmy DefendingArmy { get; init; }

    /// <summary>The tile the battle happens on, used for the seed and the castle lookup.</summary>
    public required int TileIndex { get; init; }

    /// <summary>The castle of the defender, or <see langword="null"/> for a field battle.</summary>
    public CastleTown? Castle { get; init; }

    /// <summary>Whether the fight runs as an auto battle, <c>Settings::BattleAutoResolve()</c>.</summary>
    public bool AutoResolve { get; init; }

    /// <summary>The presentation layer, or <see langword="null"/> when nobody watches.</summary>
    public IBattlePresenter? Presenter { get; init; }
}

/// <summary>
/// Starts a battle and settles its consequences, <c>Battle::Loader()</c> (<c>battle.h:68</c>,
/// <c>battle_main.cpp:291-535</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>契约先落，正文等战斗行动层</b>（ADR-161）。参考的实现分四段，逐段写明是为了让缺口一目了然：
/// </para>
/// <list type="number">
/// <item>校验两军是否有效：无效的一方直接判负（<c>296-307</c>）。</item>
/// <item>战前：两军指挥官 <c>ActionPreBattle()</c>，AI 控制的军队再走 <c>AI::Planner::HeroesPreBattle()</c>；
/// 记下两军的法术点与两国的金库，供「重打成手动」时回滚（<c>309-359</c>）。</item>
/// <item>主循环：算种子 → 建 <see cref="BattleArena"/> → <c>while ( arena.BattleValid() ) { arena.Turns(); }</c>
/// → 取结果 → 宝物转移、鹰眼、招魂、战后的 <c>ActionAfterBattle()</c>（<c>361-500</c>）。</item>
/// <item>收尾：<c>resetInvalidMonsters()</c> 把打光的兵位清空（<c>505-506</c>）。</item>
/// </list>
/// <para>
/// 端口当前的缺口有三处，都在这一个类的下面：<see cref="BattleArena.Turns()"/> 不存在（回合循环还没写）、
/// <c>BattleArena.ApplyAction</c> 只认 Move/Skip/Morale（攻击与施法还没写）、没有 <c>Battle::Only</c> 的
/// 自动战斗实现。三处齐了，<see cref="Load"/> 就会变成参考的逐行翻译。
/// </para>
/// </remarks>
public static class BattleLoader
{
    /// <summary>
    /// Runs one battle from the first turn to the result, <c>Battle::Loader()</c>.
    /// </summary>
    /// <param name="request">The two armies and everything else the loader needs.</param>
    /// <returns>The result of the fight, with both armies already updated.</returns>
    public static BattleResult Load(BattleLoadRequest request) =>
        throw new NotImplementedException("BattleLoader.Load — battle_main.cpp:291-535 未移植（ADR-161）");

    /// <summary>
    /// Drives one arena to its end, the inner loop of <c>Battle::Loader()</c> (<c>battle_main.cpp:394-396</c>).
    /// </summary>
    /// <param name="arena">The arena to run.</param>
    /// <param name="presenter">The presentation layer, or <see langword="null"/> for an unseen fight.</param>
    /// <returns>The result the arena settled on.</returns>
    /// <remarks>
    /// 参考在这一层不做任何决策：谁动、AI 怎么想、人类怎么点，全在 <see cref="BattleArena.Turns"/> 里。
    /// 端口缺的正是那个回合循环，所以这里只能先声明。
    /// </remarks>
    public static BattleResult RunToEnd(BattleArena arena, IBattlePresenter? presenter) =>
        throw new NotImplementedException("BattleLoader.RunToEnd — battle_main.cpp:394-396 未移植（ADR-161）");

    /// <summary>
    /// The seed a battle uses, <c>computeBattleSeed()</c> (<c>battle_main.cpp:120-150</c>).
    /// </summary>
    /// <param name="tileIndex">The tile the battle happens on.</param>
    /// <param name="mapSeed">The seed of the whole map.</param>
    /// <param name="attackingArmy">The attacking army.</param>
    /// <param name="defendingArmy">The defending army.</param>
    /// <returns>The seed both sides share, so a replay deals the same damage.</returns>
    /// <remarks>
    /// 参考把两军每个兵位的兵种 id 与数量都揉进种子里，空兵位揉一个 0；顺序固定为攻击方先、防御方后，
    /// 所以同一场战斗在任何机器上都得到同一个种子。
    /// </remarks>
    public static uint ComputeBattleSeed(int tileIndex, uint mapSeed, GameArmy attackingArmy, GameArmy defendingArmy) =>
        throw new NotImplementedException("BattleLoader.ComputeBattleSeed — battle_main.cpp:120-150 未移植（ADR-161）");
}
