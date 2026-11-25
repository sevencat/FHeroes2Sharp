using FHeroes2.App.Fonts;
using FHeroes2.Assets;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game;

using CastleTown = FHeroes2.Game.Castle.Castle;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.App;

/// <summary>
/// What a hero walking on the adventure map asks of the interface layer: the battle screen first of all,
/// <c>IHeroActionHost</c>'s human branch.
/// </summary>
/// <remarks>
/// <para>
/// 参考的英雄动作直接伸手去拿界面（<c>Dialog::*</c>、<c>Battle::Loader</c>、<c>Game::OpenCastleDialog</c>）；
/// 端口把「现在就要答案」的那一小撮收成 <see cref="IHeroActionHost"/>，这个类就是它的人类那一支：
/// 战斗交给 <see cref="BattlePresenter"/>，问句与城堡、市场、盗贼公会留给 <c>0lm.12</c>。
/// </para>
/// <para>
/// <b>问句暂时是全答「不」</b>：与 <see cref="NullHeroActionHost"/> 同一条老规矩——「不」在参考里总是安全的那一支
/// （不打、不买、不入城），而「是」会改世界状态。每答一次都留一条警告，好让 <c>0lm.12</c> 把缺的对话框一条条补上时
/// 能看到还有哪些问句真的被问到过。
/// </para>
/// <para>
/// <b>帧循环从哪来</b>：<see cref="RunModal"/> 由 <c>GameHost</c> 在造好冒险地图之后接上（和
/// <c>AdventureMapScene.ExitEvent</c> 一样的位置）；没接上时战斗会在日志里报错、然后由电脑自己打完。
/// </para>
/// </remarks>
internal sealed class AdventureHeroActionHost : IHeroActionHost
{
    private readonly ScreenBuffer _frame;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly World _world;

    private BattlePresenter? _presenter;

    /// <summary>Builds the host of one adventure map.</summary>
    /// <param name="frame">The 640x480 frame the battle screen draws into.</param>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="fonts">The fonts of the battle screen; <see langword="null"/> when the data is missing.</param>
    /// <param name="world">The map the heroes walk on.</param>
    /// <exception cref="ArgumentNullException">The frame or the world is <see langword="null"/>.</exception>
    public AdventureHeroActionHost(ScreenBuffer frame, AssetBundle? assets, GameFonts? fonts, World world)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(world);

        _frame = frame;
        _assets = assets;
        _fonts = fonts;
        _world = world;
    }

    /// <summary>
    /// Runs a modal window to its end, <c>GameHost.RunModal</c>; wired up right after the adventure map is built.
    /// </summary>
    /// <remarks>
    /// 战斗的人类回合、动作动画与结果窗都要一段「画一帧、看一眼输入」的循环（ADR 见 <see cref="BattlePresenter"/>），
    /// 而主循环只有一个，所以借宿主的。没接上时是 <see langword="null"/>，战斗退回电脑自己打。
    /// </remarks>
    public Action<IModalScreen>? RunModal { get; set; }

    /// <inheritdoc />
    public IBattlePresenter? BattlePresenter =>
        _presenter ??= new BattlePresenter(this, _frame, _assets, _fonts, _world);

    /// <inheritdoc />
    public bool Ask(HeroActionQuestion question, Troop troop, uint gold = 0)
    {
        GameLog.Warn(
            $"The {question} question about {troop.Count} x {troop.Monster}" +
            (gold == 0 ? string.Empty : $" at {gold} gold each") +
            " cannot be asked yet (0lm.12); answering no.");

        return false;
    }

    /// <inheritdoc />
    public bool Ask(HeroActionQuestion question, uint gold = 0)
    {
        GameLog.Warn(
            $"The {question} question" + (gold == 0 ? string.Empty : $" about {gold} gold") +
            " cannot be asked yet (0lm.12); answering no.");

        return false;
    }

    /// <inheritdoc />
    public uint AskRecruitCount(MonsterType monster, uint available, PlayerKingdom kingdom)
    {
        _ = available;
        _ = kingdom;

        GameLog.Warn($"The recruit window of {monster} cannot be opened yet (0lm.12); buying nothing.");

        return 0;
    }

    /// <inheritdoc />
    public string AskRiddle(string riddle)
    {
        _ = riddle;

        GameLog.Warn("The sphinx's input window cannot be opened yet (0lm.12); answering nothing.");

        return string.Empty;
    }

    /// <inheritdoc />
    public void OpenCastle(CastleTown castle)
    {
        // 「走进去就占了这座城」那部分状态在游戏层已经改完，这里是界面那一半。
        GameLog.Warn($"The castle screen of '{castle.Name}' is not ported yet (0lm.12).");
    }

    /// <inheritdoc />
    public void OpenMarketplace(PlayerKingdom kingdom, bool fromTradingPost)
    {
        _ = kingdom;

        GameLog.Warn($"The marketplace is not ported yet (0lm.12){(fromTradingPost ? " (trading post)" : string.Empty)}.");
    }

    /// <inheritdoc />
    public void OpenThievesGuild(PlayerKingdom kingdom)
    {
        _ = kingdom;

        GameLog.Warn("The thieves' guild report is not ported yet (0lm.12).");
    }
}
