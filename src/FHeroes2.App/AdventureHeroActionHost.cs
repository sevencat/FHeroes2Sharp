using FHeroes2.App.Fonts;
using FHeroes2.App.Modals;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;

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
/// 战斗交给 <see cref="BattlePresenter"/>，是/否问句交给 <see cref="MessageBoxModal"/>（文案在
/// <see cref="HeroActionQuestionText"/>），城堡交给城堡屏。
/// </para>
/// <para>
/// <b>窗口都借 <see cref="RunModal"/></b>：参考的这些调用是阻塞的，端口的帧循环只有一条，所以窗口进宿主的
/// 内层循环，关掉之后外层接着跑——与 <see cref="BattlePresenter"/> 同一条缝。
/// </para>
/// <para>
/// <b>帧循环从哪来</b>：<see cref="RunModal"/> 与 <see cref="OpenCastleScreen"/> 都由 <c>GameHost</c> 在造好
/// 冒险地图之后接上（和 <c>AdventureMapScene.ExitEvent</c> 一样的位置）；没接上时战斗会在日志里报错、然后由
/// 电脑自己打完，问句按「不」回答，城堡屏不开。
/// </para>
/// </remarks>
internal sealed class AdventureHeroActionHost : IHeroActionHost
{
    private readonly ScreenBuffer _frame;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly World _world;

    private BattlePresenter? _presenter;
    private SystemButtonArtwork? _buttons;

    /// <summary>Builds the host of one adventure map.</summary>
    /// <param name="frame">The 640x480 frame the battle screen draws into.</param>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="fonts">The fonts of the windows; <see langword="null"/> when the data is missing.</param>
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
    /// 战斗的人类回合、动作动画、结果窗与是/否问句都要一段「画一帧、看一眼输入」的循环
    /// （ADR 见 <see cref="BattlePresenter"/>），而主循环只有一个，所以借宿主的。没接上时是
    /// <see langword="null"/>，战斗退回电脑自己打，问句按「不」回答。
    /// </remarks>
    public Action<IModalScreen>? RunModal { get; set; }

    /// <summary>
    /// Builds the town screen of a castle, <c>Game::OpenCastleDialog()</c>'s screen; wired up by the adventure map.
    /// </summary>
    /// <remarks>
    /// 城堡屏是另一个场景（<see cref="CastleScene"/>，和地图点进城的那个是同一个），要造它得有地图、王国与
    /// 字体，这些都在冒险地图手里，所以由它接一个「给我一座城，还我一个屏」的函数。没接上时城堡屏不开。
    /// </remarks>
    public Func<CastleTown, CastleScene?>? OpenCastleScreen { get; set; }

    /// <inheritdoc />
    public IBattlePresenter? BattlePresenter =>
        _presenter ??= new BattlePresenter(this, _frame, _assets, _fonts, _world);

    /// <inheritdoc />
    public bool Ask(HeroActionQuestion question, Troop troop, uint gold = 0)
    {
        (string header, string body) = HeroActionQuestionText.Describe(question, troop, gold);

        if (ShowYesNo(header, body))
        {
            return true;
        }

        // 拒绝之后参考立刻补一条提示，然后照打（heroes_action.cpp:399）：游戏层没有第二次停下来的机会，
        // 所以这条提示由宿主自己跟进。
        if (HeroActionQuestionText.Refusal(question) is { } refusal)
        {
            ShowMessage(string.Empty, refusal);
        }

        return false;
    }

    /// <inheritdoc />
    public bool Ask(HeroActionQuestion question, uint gold = 0)
    {
        (string header, string body) = HeroActionQuestionText.Describe(question, gold);

        if (ShowYesNo(header, body))
        {
            return true;
        }

        if (HeroActionQuestionText.Refusal(question) is { } refusal)
        {
            ShowMessage(header, refusal);
        }

        return false;
    }

    /// <inheritdoc />
    public uint AskRecruitCount(MonsterType monster, uint available, PlayerKingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn($"The recruit window of {monster} has no frame pump (RunModal); buying nothing.");

            return 0;
        }

        if (_assets is not { } assets || _fonts is not { } fonts)
        {
            GameLog.Warn($"The recruit window of {monster} needs the archives and the fonts, and they are missing.");

            return 0;
        }

        // 参考就传这四样：兵种、存货、能否换降级兵（这里恒真，和城镇屏一致）与纵向偏移 0（heroes_action.cpp:276）。
        RecruitWindow? window = RecruitWindow.TryCreate(
            assets, monster, available, kingdom, fonts, RecruitWindow.MapWindowOffsetY);

        if (window is null)
        {
            GameLog.Warn($"The recruit window of {monster} x{available} could not be built from its artwork.");

            return 0;
        }

        runModal(new RecruitWindowModal(window));

        // 取消或关窗都是空队；游戏层按 0 当作「不买」（heroes_action.cpp:278 的 if ( recruit )）。
        return window.Selected?.Count ?? 0;
    }

    /// <inheritdoc />
    public string AskRiddle(string riddle)
    {
        ArgumentNullException.ThrowIfNull(riddle);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The riddle input window has no frame pump (RunModal); answering nothing.");

            return string.Empty;
        }

        if (_fonts is not { } fonts)
        {
            GameLog.Warn("The riddle input window needs the game fonts, and they are missing.");

            return string.Empty;
        }

        // 标题是对象名（MP2::StringObject(Sphinx)），正文把谜语原样夹在两句之间（heroes_action.cpp:3558-3564）。
        InputStringDialog dialog = InputStringDialog.Create(
            HeroActionQuestionText.SphinxTitle, HeroActionQuestionText.SphinxRiddle(riddle));

        InputStringWindow? window = InputStringWindow.TryCreate(dialog, fonts.Yellow, fonts.White);

        if (window is null)
        {
            GameLog.Warn("The riddle input window could not be built from its artwork.");

            return string.Empty;
        }

        runModal(new InputStringModal(window));

        // 取消时参考也返回空串：isCorrectAnswer("") 为假，英雄就按答错处理。
        return window.Result ?? string.Empty;
    }

    /// <inheritdoc />
    public void OpenCastle(CastleTown castle)
    {
        ArgumentNullException.ThrowIfNull(castle);

        if (OpenCastleScreen?.Invoke(castle) is not { } screen)
        {
            GameLog.Warn($"The castle screen of '{castle.Name}' cannot be shown: its artwork is missing.");

            return;
        }

        if (RunModal is not { } runModal)
        {
            GameLog.Warn($"The castle screen of '{castle.Name}' has no frame pump (RunModal); closing it again.");
            screen.Close();

            return;
        }

        runModal(new CastleScreenModal(screen));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 参考的交易站是 <c>Dialog::Marketplace( hero.GetKingdom(), true )</c>（<c>heroes_action.cpp:1288</c>）：市场窗口
    /// 与城镇屏里的是同一个，只是汇率按交易站算。买卖只改王国金库，地图的资源栏每帧从王国重画。
    /// </remarks>
    public void OpenMarketplace(PlayerKingdom kingdom, bool fromTradingPost)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The marketplace has no frame pump (RunModal); it stays closed.");

            return;
        }

        if (_assets is not { } assets || _fonts is not { } fonts)
        {
            GameLog.Warn("The marketplace needs the archives and the fonts, and they are missing.");

            return;
        }

        MarketplaceWindow? window = MarketplaceWindow.TryCreate(
            assets, kingdom, ActivePlayers(), fonts, fromTradingPost);

        if (window is null)
        {
            GameLog.Warn("The marketplace could not be built from its artwork.");

            return;
        }

        runModal(new MarketplaceModal(window, _world, kingdom, assets, fonts));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 参考的预言所是 <c>Dialog::ThievesGuild( true )</c>（<c>heroes_action.cpp:3039</c>）：参数的意思是在局者的排名
    /// 而不是某座城的，所以报告直接用当前世界算；地图上没有城镇屏那种「坏界面」按钮，一律用好的那一套。
    /// </remarks>
    public void OpenThievesGuild(PlayerKingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(kingdom);

        if (RunModal is not { } runModal)
        {
            GameLog.Warn("The thieves' guild report has no frame pump (RunModal); it stays closed.");

            return;
        }

        if (_assets is not { } assets || _fonts is not { } fonts)
        {
            GameLog.Warn("The thieves' guild report needs the archives and the fonts, and they are missing.");

            return;
        }

        ThievesGuildReport report = ThievesGuildReport.FromWorld(_world, kingdom.Color, oracle: true);

        ThievesGuildWindow? window = ThievesGuildWindow.TryCreate(assets, report, fonts);

        if (window is null)
        {
            GameLog.Warn("The thieves' guild report could not be built from its artwork.");

            return;
        }

        runModal(new ThievesGuildModal(window, assets, fonts));
    }

    /// <summary>How many players are still in the game, <c>Settings::Get().GetPlayers()</c> of the gift test.</summary>
    /// <remarks>
    /// 与城镇屏同一条算法：参考问的是剧本里在局的玩家槽，端口的槽还没被开局流程填满，于是问王国自己——
    /// <see cref="Kingdoms.GetColors"/> 给的正是 <c>Kingdoms.Init</c> 建的同一份表。
    /// </remarks>
    /// <returns>The number of kingdoms in play; one or zero in a single player game.</returns>
    private int ActivePlayers()
    {
        int count = 0;

        foreach (PlayerColor color in PlayerColors.ToList(_world.Kingdoms.GetColors()))
        {
            if (_world.Kingdoms.GetKingdom(color).IsPlay)
            {
                ++count;
            }
        }

        return count;
    }

    /// <summary>Puts a YES/NO box to the player, <c>fheroes2::showStandardTextMessage( …, Dialog::YES | Dialog::NO )</c>.</summary>
    /// <param name="header">Title of the box; empty for none.</param>
    /// <param name="body">The question.</param>
    /// <returns><see langword="true"/> when the player pressed YES.</returns>
    private bool ShowYesNo(string header, string body)
    {
        MessageBoxModal? box = ShowBox(header, body, DialogButton.Yes | DialogButton.No);

        return box?.Answer == DialogButton.Yes;
    }

    /// <summary>Puts an OK-only box to the player and waits for it, <c>fheroes2::showStandardTextMessage( …, Dialog::OK )</c>.</summary>
    /// <param name="header">Title of the box; empty for none.</param>
    /// <param name="body">The message.</param>
    private void ShowMessage(string header, string body) =>
        ShowBox(header, body, DialogButton.Ok);

    /// <summary>Builds a box and runs it to its end on the host's frame pump.</summary>
    /// <param name="header">Title of the box; empty for none.</param>
    /// <param name="body">Text of the box.</param>
    /// <param name="buttons">Buttons on the bottom edge.</param>
    /// <returns>The box that was run, or <see langword="null"/> when it cannot be shown.</returns>
    private MessageBoxModal? ShowBox(string header, string body, DialogButton buttons)
    {
        if (RunModal is not { } runModal)
        {
            GameLog.Warn($"The box \"{body}\" has no frame pump (RunModal); it is skipped.");

            return null;
        }

        if (_fonts is null)
        {
            GameLog.Warn($"The box \"{body}\" needs the game fonts, and they are missing.");

            return null;
        }

        _buttons ??= SystemButtonArtwork.TryCreate(_assets);

        if (_buttons is null)
        {
            GameLog.Warn($"The box \"{body}\" needs the standard buttons, and they are missing.");

            return null;
        }

        MessageBoxModal box = MessageBoxModal.Create(header, body, _fonts.Yellow, _fonts.White, buttons, _buttons);

        runModal(box);

        return box;
    }
}
