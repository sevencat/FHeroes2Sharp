using System.Diagnostics;

using FHeroes2.Core.Random;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
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
    /// Opens the battle screen over a prepared arena, the <c>Battle::Interface</c> constructor the reference runs
    /// from <c>Battle::Loader</c> (<c>battle_main.cpp:371-385</c>).
    /// </summary>
    /// <remarks>
    /// 参考在竞技场建好之后、第一回合之前就把界面立起来，于是<b>第一个电脑单位的动作也有人看</b>。端口照这个位置调用：
    /// 实现方在这里拿到 <see cref="BattleArena.TileIndex"/> 去取战场外观，并把 <see cref="BattleArena.Animator"/>
    /// 装成自己，之后每一个动作（人打的、AI 打的）都会先走动画再结算。
    /// </remarks>
    /// <param name="arena">The arena that is about to be played.</param>
    void BattleStarted(BattleArena arena)
    {
        // 默认没有人看：与 showBattle == false 同样的表现。
    }

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

    /// <summary>
    /// Who plays the human side's units, the <c>humanTurn</c> hook of <c>Arena::Turns()</c>
    /// (<c>battle_arena.cpp:152-... </c>).
    /// </summary>
    /// <remarks>
    /// 参考在回合循环里把每个单位分给两条路：有人类玩家就交给 <c>_interface</c> 等它点，否则交给
    /// <c>AI::GetBattleTurn()</c>。端口没有界面线程，于是把「人类那一侧怎么打」留成这个回调：
    /// 默认 <see langword="null"/> 表示没人坐在电脑前，全部由 AI 代打，等价于参考里 <c>showBattle == false</c>。
    /// </remarks>
    BattleUnitTurnHandler? HumanTurn => null;
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
/// One battle set up but not yet played or settled, the two halves of <c>Battle::Loader()</c> split apart so a
/// frame-driven interface can drive the fight itself.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BattleLoader.Load"/> 把它两半一次跑完，是参考的直译；战斗画面要走自己的帧循环时，
/// 用 <see cref="BattleLoader.TryPrepare"/> 拿到一个 session，自己跑 <see cref="BattleLoader.RunToEnd"/>
/// 或逐帧驱动 <see cref="Arena"/>，打完再交给 <see cref="BattleLoader.Settle"/>，最后
/// <see cref="BattleLoader.CloseSession"/>。
/// </para>
/// <para>
/// 里面装的是两半之间必须传递的东西：请求、竞技场、种子、两军指挥官与「重打成手动」要回滚的四个快照。
/// 它不持任何资源，丢掉就等于放弃这场战斗。
/// </para>
/// </remarks>
public sealed class BattleSession
{
    internal BattleSession(
        BattleLoadRequest request,
        BattleArena arena,
        bool showBattle,
        uint battleSeed,
        IArmyCommander? attackingCommander,
        IArmyCommander? defendingCommander,
        uint attackingInitialSpellPoints,
        uint defendingInitialSpellPoints,
        Funds attackingInitialFunds,
        Funds defendingInitialFunds)
    {
        Request = request;
        Arena = arena;
        ShowBattle = showBattle;
        BattleSeed = battleSeed;
        IsHumanBattle = request.AttackingArmy.GetControl() == PlayerControl.Human
            || request.DefendingArmy.GetControl() == PlayerControl.Human;
        AttackingCommander = attackingCommander;
        DefendingCommander = defendingCommander;
        AttackingInitialSpellPoints = attackingInitialSpellPoints;
        DefendingInitialSpellPoints = defendingInitialSpellPoints;
        AttackingInitialFunds = attackingInitialFunds;
        DefendingInitialFunds = defendingInitialFunds;
    }

    /// <summary>The request the battle was set up from.</summary>
    public BattleLoadRequest Request { get; }

    /// <summary>The arena to play, rebuilt by <see cref="Restart"/>.</summary>
    public BattleArena Arena { get; private set; }

    /// <summary>Whether the fight is shown to a human, <c>show_battle</c>.</summary>
    public bool ShowBattle { get; private set; }

    /// <summary>Whether either side is played by a human, <c>isHumanBattle</c>.</summary>
    public bool IsHumanBattle { get; }

    /// <summary>The seed both the arena and a replay use.</summary>
    internal uint BattleSeed { get; }

    internal IArmyCommander? AttackingCommander { get; }

    internal IArmyCommander? DefendingCommander { get; }

    internal uint AttackingInitialSpellPoints { get; }

    internal uint DefendingInitialSpellPoints { get; }

    internal Funds AttackingInitialFunds { get; }

    internal Funds DefendingInitialFunds { get; }

    /// <summary>
    /// Rebuilds the arena over the same armies and seed for the player's replay, the <c>continue</c> of
    /// <c>battle_main.cpp:387-500</c>.
    /// </summary>
    internal void Restart()
    {
        Arena = BattleLoader.BuildArena(Request, BattleSeed);
        ShowBattle = true;
    }
}

/// <summary>
/// Starts a battle and settles its consequences, <c>Battle::Loader()</c> (<c>battle.h:68</c>,
/// <c>battle_main.cpp:291-535</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的实现分四段：
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
/// 两处没照抄的地方，都在 <see cref="Load"/> 的注释里写明：打赢一方若是战役英雄，参考会给一个 <c>Campaign</c> 奖励，
/// 战役系统还没移植，这一步跳过；战后存档摘要（<c>Settings</c> 里的 <c>BattleShowArmySummary</c>）可能要重跑整场战斗，
/// 那是复查战斗结果的确认框，也归界面，端口不实现。
/// </para>
/// <para>
/// 重打用的「回滚」有两份：法术点与王国金库。参考在循环开头就把它们抄下来，界面点了重打就写回去；端口照做，但
/// <b>兵员损失不回滚</b>——参考也不回滚，重打是用原始军队重开一个竞技场（<see cref="GameArmy"/> 的兵堆在开战时
/// 已被战斗单位共享并改小，重开时由 <see cref="BattleForce"/> 重新取人数），所以两次结果不会互相污染。
/// </para>
/// </remarks>
public static class BattleLoader
{
    /// <summary>
    /// Runs one battle from the first turn to the result, <c>Battle::Loader()</c>.
    /// </summary>
    /// <param name="request">The two armies and everything else the loader needs.</param>
    /// <returns>The result of the fight, with both armies already updated.</returns>
    /// <exception cref="ArgumentException">One of the armies has no commander at all.</exception>
    public static BattleResult Load(BattleLoadRequest request)
    {
        BattleSession? session = TryPrepare(request, out BattleResult immediateResult);

        if (session is null)
        {
            return immediateResult;
        }

        while (true)
        {
            BattleResult result = RunToEnd(session.Arena, session.ShowBattle ? session.Request.Presenter : null);

            if (!Settle(session, result, out bool restart))
            {
                return CloseSession(session, result);
            }

            // The player asked to fight the shown battle by hand once more: a fresh arena over the same armies
            // and the same seed, the `continue` of the reference's loop (battle_main.cpp:387-500).
            session.Restart();
        }
    }

    /// <summary>
    /// Sets a battle up without playing it, the front half of <c>Battle::Loader()</c>:
    /// the pre-battle hooks, the undo snapshots and the arena itself.
    /// </summary>
    /// <param name="request">The two armies and everything else the loader needs.</param>
    /// <param name="immediateResult">
    /// The result when the fight never happens because one side has nothing to fight with; otherwise the default.
    /// </param>
    /// <returns>The prepared battle, or <see langword="null"/> when <paramref name="immediateResult"/> answers.</returns>
    /// <remarks>
    /// <para>
    /// 这是「把战斗交给别人跑」的那一半：参考的 <c>Battle::Loader</c> 是阻塞的，人类回合在
    /// <c>Arena::Turns()</c> 里自带事件循环；端口没有界面线程，于是把前半段（战前钩子、回滚快照、竞技场）与
    /// 后半段（结算、宝物、鹰眼、招魂）拆开——调用方拿到 <see cref="BattleSession"/> 后用
    /// <see cref="RunToEnd"/> 或自己的帧循环把它跑完，再交给 <see cref="Settle"/>。
    /// </para>
    /// <para>
    /// <see cref="Load"/> 就是这两半的一个循环封装，语义与参考一字不差；<paramref name="immediateResult"/> 对应
    /// 参考开头那段「一方没有兵，直接判负」（<c>battle_main.cpp:296-307</c>）。
    /// </para>
    /// </remarks>
    public static BattleSession? TryPrepare(BattleLoadRequest request, out BattleResult immediateResult)
    {
        ArgumentNullException.ThrowIfNull(request);

        immediateResult = default;

        GameArmy attackingArmy = request.AttackingArmy;
        GameArmy defendingArmy = request.DefendingArmy;

        if (!attackingArmy.IsValid || !defendingArmy.IsValid)
        {
            // A side with nothing to fight with loses without a battle taking place.
            if (!defendingArmy.IsValid)
            {
                immediateResult.Attacker = BattleOutcome.Wins;
            }
            else
            {
                immediateResult.Defender = BattleOutcome.Wins;
            }

            return null;
        }

        IArmyCommander? attackingCommander = attackingArmy.Commander;
        attackingCommander?.ActionPreBattle();

        if (attackingCommander != null && attackingArmy.GetControl() == PlayerControl.Ai)
        {
            AiPlanner.HeroesPreBattle(attackingArmy, true);
        }

        uint attackingInitialSpellPoints = attackingCommander?.SpellPoints ?? 0;
        Funds attackingInitialFunds = GetKingdomFunds(request.World, attackingCommander);

        IArmyCommander? defendingCommander = defendingArmy.Commander;
        defendingCommander?.ActionPreBattle();

        if (defendingCommander != null && defendingArmy.GetControl() == PlayerControl.Ai)
        {
            AiPlanner.HeroesPreBattle(defendingArmy, false);
        }

        uint defendingInitialSpellPoints = defendingCommander?.SpellPoints ?? 0;
        Funds defendingInitialFunds = GetKingdomFunds(request.World, defendingCommander);

        bool isHumanBattle = attackingArmy.GetControl() == PlayerControl.Human
            || defendingArmy.GetControl() == PlayerControl.Human;
        bool showBattle = !request.AutoResolve && isHumanBattle;

        uint battleSeed = ComputeBattleSeed(request.TileIndex, request.World.MapSeed, attackingArmy, defendingArmy);

        BattleArena arena = BuildArena(request, battleSeed);

        // 不放映就等于没有界面：人类那一方也交给电脑（参考在竞技场构造函数里做，
        // battle_arena.cpp:372-380）。
        if (!showBattle)
        {
            arena.EnableAutoCombatForHumanSides();
        }
        else
        {
            // 参考在竞技场构造完之后立刻建界面（battle_main.cpp:371-385）：第一个电脑单位的动作也要有人看。
            request.Presenter?.BattleStarted(arena);
        }

        return new BattleSession(
            request,
            arena,
            showBattle,
            battleSeed,
            attackingCommander,
            defendingCommander,
            attackingInitialSpellPoints,
            defendingInitialSpellPoints,
            attackingInitialFunds,
            defendingInitialFunds);
    }

    /// <summary>
    /// Settles a played battle, the back half of <c>Battle::Loader()</c>: the result dialog, the artifacts, the
    /// post-battle hooks, Eagle Eye and necromancy (<c>battle_main.cpp:398-500</c>).
    /// </summary>
    /// <param name="session">The battle that was played.</param>
    /// <param name="result">What its arena settled on.</param>
    /// <param name="restart">Whether the player asked to fight the shown battle by hand once more.</param>
    /// <returns>Whether the battle has to be replayed before anything is settled.</returns>
    /// <remarks>
    /// 返回 <see langword="true"/> 时<b>什么都没写回</b>：法术点与金库已经被调用方（或 <see cref="Load"/>）回滚，
    /// 两军原封不动，重打用同一批兵与同一个种子再开一次即可。
    /// </remarks>
    public static bool Settle(BattleSession session, BattleResult result, out bool restart)
    {
        ArgumentNullException.ThrowIfNull(session);

        World world = session.Request.World;
        IArmyCommander? attackingCommander = session.AttackingCommander;
        IArmyCommander? defendingCommander = session.DefendingCommander;
        bool showBattle = session.ShowBattle;

        restart = false;

        IArmyCommander? winnerCommander = result.IsAttackerWin
            ? attackingCommander
            : result.IsDefenderWin
                ? defendingCommander
                : null;

        BattleOutcome loserOutcome = result.IsAttackerWin
            ? result.Defender
            : result.IsDefenderWin
                ? result.Attacker
                : BattleOutcome.None;

        IArmyCommander? loserCommander = result.IsAttackerWin ? defendingCommander : attackingCommander;

        // A side that ran away or bought itself out is not wiped out, so nothing is taken from it.
        bool isLoserAbandoned = (loserOutcome & (BattleOutcome.Retreat | BattleOutcome.Surrender)) == 0;
        bool canTransferArtifacts = isLoserAbandoned && winnerCommander is Hero && loserCommander is Hero;

        if (showBattle)
        {
            bool clearMessageLog = (result.Attacker & (BattleOutcome.Retreat | BattleOutcome.Surrender)) != 0
                || (result.Defender & (BattleOutcome.Retreat | BattleOutcome.Surrender)) != 0;

            session.Request.Presenter?.FadeArena(clearMessageLog);
        }

        if (session.IsHumanBattle && session.Request.Presenter != null
            && session.Request.Presenter.ShowSummary(
                result,
                canTransferArtifacts
                    ? GetArtifactsToTransfer(((Hero)winnerCommander!).BagArtifacts, ((Hero)loserCommander!).BagArtifacts)
                    : [],
                !showBattle))
        {
            // The player asked to fight this one by hand: put the spell points and the treasuries back and replay.
            restart = true;

            if (attackingCommander != null)
            {
                attackingCommander.SetSpellPoints(session.AttackingInitialSpellPoints);
                RestoreKingdomFunds(world, attackingCommander, session.AttackingInitialFunds);
            }

            if (defendingCommander != null)
            {
                defendingCommander.SetSpellPoints(session.DefendingInitialSpellPoints);
                RestoreKingdomFunds(world, defendingCommander, session.DefendingInitialFunds);
            }

            return true;
        }

        if (canTransferArtifacts && loserCommander is Hero loserHero)
        {
            Hero winnerHero = (Hero)winnerCommander!;

            TransferArtifacts(winnerHero.BagArtifacts, loserHero.BagArtifacts);

            if (winnerHero.IsControlHuman)
            {
                foreach (ArtifactSet set in winnerHero.BagArtifacts.AssembleArtifactSets())
                {
                    session.Request.Presenter?.ShowArtifactSetAssembled(set.Assembled);
                }
            }
        }

        // No campaign reward: the campaign system is not ported yet (battle_main.cpp:447-459).

        session.Arena.AttackingForce.SyncOriginalArmy();
        session.Arena.DefendingForce.SyncOriginalArmy();

        attackingCommander?.ActionAfterBattle();
        defendingCommander?.ActionAfterBattle();

        if (winnerCommander is Hero eagleEyeOwner && loserCommander is Hero)
        {
            EagleEyeSkillAction(eagleEyeOwner, session.Arena.UsedSpells, session.Arena.RandomGenerator,
                session.Request.Presenter);
        }

        if (winnerCommander is Hero necromancer)
        {
            NecromancySkillAction(necromancer, result.NumOfDeadUnitsForNecromancy, world, session.Request.Presenter);
        }

        return false;
    }

    /// <summary>
    /// Puts a settled battle away, the tail of <c>Battle::Loader()</c>: <c>resetInvalidMonsters()</c>
    /// (<c>battle_main.cpp:505-506</c>).
    /// </summary>
    /// <param name="session">The battle that is over.</param>
    /// <param name="result">What it settled on.</param>
    /// <returns>The result, handed back for the caller to return.</returns>
    public static BattleResult CloseSession(BattleSession session, BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.Request.AttackingArmy.ResetInvalidMonsters();
        session.Request.DefendingArmy.ResetInvalidMonsters();

        return result;
    }

    /// <summary>
    /// Drives one arena to its end, the inner loop of <c>Battle::Loader()</c> (<c>battle_main.cpp:394-396</c>).
    /// </summary>
    /// <param name="arena">The arena to run.</param>
    /// <param name="presenter">The presentation layer, or <see langword="null"/> for an unseen fight.</param>
    /// <returns>The result the arena settled on.</returns>
    /// <remarks>
    /// 参考在这一层不做任何决策：谁动、AI 怎么想、人类怎么点，全在 <see cref="BattleArena.Turns"/> 里。
    /// 端口把「人类那一侧怎么打」交给 <see cref="IBattlePresenter.HumanTurn"/>；没人观看时它为空，
    /// <see cref="BattleArena.Turns"/> 就会像自动战斗一样让 AI 把两边都打了。
    /// </remarks>
    public static BattleResult RunToEnd(BattleArena arena, IBattlePresenter? presenter)
    {
        ArgumentNullException.ThrowIfNull(arena);

        while (arena.BattleValid())
        {
            arena.Turns(presenter?.HumanTurn);
        }

        return arena.Result;
    }

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
    /// 所以同一场战斗在任何机器上都得到同一个种子。按位置（而不是按在场单位）遍历，是因为「谁在场」在开战前
    /// 还没定下来——座位号才是稳定的。
    /// </remarks>
    public static uint ComputeBattleSeed(int tileIndex, uint mapSeed, GameArmy attackingArmy, GameArmy defendingArmy)
    {
        ArgumentNullException.ThrowIfNull(attackingArmy);
        ArgumentNullException.ThrowIfNull(defendingArmy);

        uint seed = (uint)tileIndex + mapSeed;

        foreach (GameArmy army in (ReadOnlySpan<GameArmy>)[attackingArmy, defendingArmy])
        {
            for (int index = 0; index < GameArmy.MaximumTroopCount; ++index)
            {
                Troop troop = army.GetTroop(index);

                if (troop.IsValid)
                {
                    seed = Rand.CombineSeedWithValueHash(seed, (uint)troop.Monster);
                    seed = Rand.CombineSeedWithValueHash(seed, troop.Count);
                }
                else
                {
                    seed = Rand.CombineSeedWithValueHash(seed, 0);
                }
            }
        }

        return seed;
    }

    internal static BattleArena BuildArena(BattleLoadRequest request, uint battleSeed)
    {
        BattleArena arena = BattleArena.FromTile(request.World, request.AttackingArmy, request.DefendingArmy,
            request.TileIndex, new Pcg32(battleSeed));

        Debug.Assert(request.Castle == null || ReferenceEquals(arena.Castle, request.Castle),
            "The castle of the request must be the one the arena found on that tile.");

        return arena;
    }

    private static Funds GetKingdomFunds(World world, IArmyCommander? commander) =>
        commander == null ? default : world.GetKingdom(commander.GetColor()).Treasury;

    private static void RestoreKingdomFunds(World world, IArmyCommander commander, in Funds initialFunds)
    {
        var kingdom = world.GetKingdom(commander.GetColor());
        Funds difference = kingdom.Treasury - initialFunds;

        Debug.Assert(kingdom.AllowPayment(difference), "Rolling a battle back never spends more than was spent.");

        kingdom.OddFundsResource(difference);
    }

    /// <summary>
    /// What the winner would take from the loser, <c>getArtifactsToTransfer()</c> (<c>battle_main.cpp:58-90</c>).
    /// </summary>
    /// <remarks>
    /// 只挑「能立刻装进赢家背包」的：先数赢家还有几个空位，再按输家的格子顺序挑；终极宝物攒到最后，
    /// 因为它们在参考的界面里另起一段显示。挑选不改变任何一方——真正搬东西的是
    /// <see cref="TransferArtifacts"/>，这一步只是为了让摘要对话框能先说清楚「他将得到什么」。
    /// </remarks>
    private static List<ArtifactType> GetArtifactsToTransfer(ArtifactBag winnerBag, ArtifactBag loserBag)
    {
        int slots = winnerBag.Slots.Count(artifact => artifact == ArtifactType.Unknown);

        List<ArtifactType> artifacts = [];
        List<ArtifactType> ultimateArtifacts = [];

        foreach (ArtifactType artifact in loserBag.Slots)
        {
            if (slots == 0)
            {
                break;
            }

            if (!IsArtifactSuitableForTransfer(artifact))
            {
                continue;
            }

            (ArtifactLevels.IsUltimate(artifact) ? ultimateArtifacts : artifacts).Add(artifact);
            --slots;
        }

        artifacts.AddRange(ultimateArtifacts);

        return artifacts;
    }

    /// <summary>
    /// Hands the loser's artifacts to the winner, <c>transferArtifacts()</c> (<c>battle_main.cpp:92-116</c>).
    /// </summary>
    /// <remarks>
    /// 终极宝物不给赢家（参考认为它会自己回到战场上），但输家的格子照样清空——那支英雄已经不在了，
    /// 东西不能跟着尸体留在原地。
    /// </remarks>
    private static void TransferArtifacts(ArtifactBag winnerBag, ArtifactBag loserBag)
    {
        foreach (ArtifactType artifact in GetArtifactsToTransfer(winnerBag, loserBag))
        {
            Debug.Assert(IsArtifactSuitableForTransfer(artifact), "Only transferable artifacts are planned.");

            if (!ArtifactLevels.IsUltimate(artifact))
            {
                Debug.Assert(winnerBag.Push(artifact), "The planner only hands over what fits.");
            }

            loserBag.Remove(artifact);
        }
    }

    private static bool IsArtifactSuitableForTransfer(ArtifactType artifact) =>
        artifact != ArtifactType.Unknown && artifact != ArtifactType.MagicBook;

    /// <summary>
    /// Teaches the winner the spells the loser cast, <c>eagleEyeSkillAction()</c>
    /// (<c>battle_main.cpp:205-232</c>).
    /// </summary>
    /// <remarks>
    /// 每条还没学会的法术单独掷一次骰：技能百分比过线就抄进书里，最多抄到「中级 3 级法术、专家 5 级」为止。
    /// 掷骰用战场那个生成器（<c>arena.RandomGenerator</c>），所以同一颗种子下学到的东西也一样。
    /// </remarks>
    private static void EagleEyeSkillAction(Hero hero, SpellStorage spells, Pcg32 randomGenerator,
        IBattlePresenter? presenter)
    {
        if (spells.Count == 0 || !hero.HasSpellBook)
        {
            return;
        }

        SkillLevel level = hero.GetLevelSkill(SecondarySkill.EagleEye);

        int spellLevelCap = level switch
        {
            SkillLevel.Basic => 3,
            SkillLevel.Advanced => 4,
            SkillLevel.Expert => 5,
            _ => 0,
        };

        if (spellLevelCap == 0)
        {
            return;
        }

        uint chance = SecondarySkills.GetValue(SecondarySkill.EagleEye, level);
        List<SpellType> learned = [];

        foreach (Spell spell in spells)
        {
            if (hero.HasSpell(spell) || spell.Level() >= spellLevelCap)
            {
                continue;
            }

            if (chance < Rand.GetWithGen(1U, 100U, randomGenerator))
            {
                continue;
            }

            hero.AppendSpellToBook(spell, true);
            learned.Add(spell.Type);
        }

        if (learned.Count > 0 && hero.IsControlHuman)
        {
            presenter?.ShowEagleEyeLearned(learned);
        }
    }

    /// <summary>
    /// Raises skeletons from the fallen, <c>necromancySkillAction()</c> (<c>battle_main.cpp:234-262</c>).
    /// </summary>
    /// <remarks>
    /// 满编且没有骷髅兵时召不出来（没地方放）；否则按百分比折算，至少 1 个，并给到已有那堆骷髅上
    /// （<c>emptySlotFirst: false</c>，参考同样先并堆）。
    /// </remarks>
    private static void NecromancySkillAction(Hero hero, uint enemyTroopsKilled, World world,
        IBattlePresenter? presenter)
    {
        GameArmy army = hero.Army;

        if (enemyTroopsKilled == 0)
        {
            return;
        }

        bool isFullHouse = true;

        for (int index = 0; index < GameArmy.MaximumTroopCount; ++index)
        {
            if (!army.GetTroop(index).IsValid)
            {
                isFullHouse = false;
                break;
            }
        }

        if (isFullHouse && !army.HasMonster(MonsterType.Skeleton))
        {
            return;
        }

        uint raiseCount = Math.Max(enemyTroopsKilled * SecondarySkills.GetNecromancyPercent(hero, world) / 100, 1U);

        army.JoinTroop(MonsterType.Skeleton, raiseCount, false);

        if (hero.IsControlHuman)
        {
            presenter?.ShowNecromancy(raiseCount);
        }
    }

}
