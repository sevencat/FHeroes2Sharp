using FHeroes2.App.Fonts;
using FHeroes2.App.Scenes;
using FHeroes2.Assets;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game;

namespace FHeroes2.App;

/// <summary>
/// The battle screen of a fight a human hero started on the adventure map, the <c>Battle::Interface</c> the reference
/// builds inside <c>Battle::Loader</c> and then lets run its own event loop.
/// </summary>
/// <remarks>
/// <para>
/// 参考在这条路上是阻塞的：<c>Battle::Loader</c> 建界面，<c>Arena::Turns()</c> 里一旦轮到人类单位就转界面自己的
/// 事件循环（<c>Interface::HumanBattleTurn()</c>），打完再问界面要结果窗。端口的战斗规则与画面是拆开的
/// （<see cref="BattleLoader"/> 的 <c>TryPrepare</c>/<c>RunToEnd</c>/<c>Settle</c>），所以这里把「转界面自己的循环」
/// 翻成「向宿主借一段帧循环」：<see cref="HumanTurn"/> 借到玩家出完这一手为止，
/// <see cref="IBattleActionAnimator"/> 的两个回调借到动画播完为止，<see cref="ShowSummary"/> 借到结果窗关掉为止。
/// </para>
/// <para>
/// 画面本身不重写：<see cref="BattleScene.TryCreateForArena"/> 那一支只画与收命令，所以移动、攻击、施法、撤退、
/// 投降、自动战斗、战斗日志与魔法书都和 <c>--battle</c> 冒烟走同一条代码。
/// </para>
/// <para>
/// 限制（留给 0lm.12 之后）：只支持「人类是进攻方」。人类守城那一支的镜头与结果文案在参考里另有一套
/// （<c>Interface::_attacking</c>），端口暂时只有进攻方的英雄可问。
/// </para>
/// </remarks>
internal sealed class BattlePresenter : IBattlePresenter, IBattleActionAnimator
{
    private readonly AdventureHeroActionHost _host;
    private readonly ScreenBuffer _frame;
    private readonly AssetBundle? _assets;
    private readonly GameFonts? _fonts;
    private readonly World _world;

    private BattleScene? _scene;
    private BattleArena? _arena;

    /// <summary>Builds the presentation layer of one adventure map.</summary>
    /// <param name="host">The hero action host the frame loop is borrowed from.</param>
    /// <param name="frame">The 640x480 frame to draw into.</param>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="fonts">The fonts of the text boxes; <see langword="null"/> when the data is missing.</param>
    /// <param name="world">The map the fight happens on.</param>
    /// <exception cref="ArgumentNullException">The host or the world is <see langword="null"/>.</exception>
    public BattlePresenter(AdventureHeroActionHost host, ScreenBuffer frame, AssetBundle? assets, GameFonts? fonts,
        World world)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(world);

        _host = host;
        _frame = frame;
        _assets = assets;
        _fonts = fonts;
        _world = world;
    }

    /// <inheritdoc />
    public BattleUnitTurnHandler? HumanTurn => OnHumanTurn;

    /// <inheritdoc />
    public void BattleStarted(BattleArena arena)
    {
        ArgumentNullException.ThrowIfNull(arena);

        BattleScene? scene = EnsureScene(arena);

        if (scene is null)
        {
            return;
        }

        // 参考在构造函数里就把战场画出来；这里借一帧，免得电脑先手时玩家对着地图发呆。
        int drawn = 0;

        RunModal(new BattleScreenModal(scene, _ => ++drawn > 1));
    }

    /// <inheritdoc />
    public void FadeArena(bool clearMessageLog)
    {
        // 参考在这里把战场淡出、清掉状态行再回地图（battle_main.cpp:408）。端口的战场是模态窗口，
        // 关掉窗口就回到地图，淡出留给以后补动画的那一轮。
        _ = clearMessageLog;
    }

    /// <inheritdoc />
    public bool ShowSummary(BattleResult result, IReadOnlyList<ArtifactType> artifacts, bool autoResolve)
    {
        // 自动结算那一支没有战场可借（showBattle == false），结果只进日志；显示着的那一支永远 allowToRestart
        // 为 false，所以这里不会要求重打。
        _ = artifacts;
        _ = autoResolve;

        if (_scene is null || !_scene.OpenSummary(result))
        {
            GameLog.Info($"Battle over: {result.Attacker} attacks {result.Defender}, " +
                $"{(result.IsAttackerWin ? "the attacker" : "the defender")} wins.");

            return false;
        }

        RunModal(new BattleScreenModal(_scene, scene => !scene.IsSummaryOpen));

        return false;
    }

    /// <inheritdoc />
    public void ShowNecromancy(uint count) =>
        ShowMessage($"The hero's necromancy raises {count} skeletons from the fallen.");

    /// <inheritdoc />
    public void ShowArtifactSetAssembled(ArtifactType artifact) =>
        ShowMessage($"The artifacts combine into the {artifact} set.");

    /// <inheritdoc />
    public void ShowEagleEyeLearned(IReadOnlyList<SpellType> spells)
    {
        ArgumentNullException.ThrowIfNull(spells);

        string names = string.Join(", ", spells.Select(type => new Spell(type).GetName()));

        ShowMessage($"The hero's eagle eye learns {names}.");
    }

    /// <inheritdoc />
    void IBattleActionAnimator.BeforeAction(BattleUnit unit, BattleCommand command)
    {
        if (_scene is null)
        {
            return;
        }

        _scene.BeginActionAnimation(unit, command);
        PumpUntilIdle();
    }

    /// <inheritdoc />
    void IBattleActionAnimator.AfterAction(IReadOnlyList<BattleAttackBlow> blows)
    {
        if (_scene is null)
        {
            return;
        }

        _scene.CompleteAction(blows);
        PumpUntilIdle();
    }

    /// <summary>
    /// Plays the turn of a unit the arena has handed to the player, <c>Interface::HumanBattleTurn()</c>'s loop.
    /// </summary>
    /// <param name="arena">The arena the unit acts in.</param>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <param name="actions">The list to fill; the arena applies the commands in order.</param>
    /// <remarks>
    /// 界面不在（美术缺失）时把这一方交给电脑再补一条跳过：宁可自动打完，也不要把玩家卡在一场等不到输入的仗上。
    /// </remarks>
    private void OnHumanTurn(BattleArena arena, BattleUnit unit, List<BattleCommand> actions)
    {
        BattleScene? scene = EnsureScene(arena);

        if (scene is null || _host.RunModal is null)
        {
            arena.EnableAutoCombatForHumanSides();
            actions.Add(BattleCommand.Skip((int)unit.Uid));

            return;
        }

        RunModal(new BattleScreenModal(scene, s => s.HasPendingCommands || !arena.BattleValid()));

        if (!scene.TryTakeCommands(actions) || actions.Count == 0)
        {
            // 等输入的这一段时间里仗打完了（例如对面被自己的法术打死）：补一条跳过让竞技场收尾。
            actions.Clear();
            actions.Add(BattleCommand.Skip((int)unit.Uid));
        }
    }

    /// <summary>
    /// Gets the battlefield of <paramref name="arena"/>, building it the first time and again after a replay.
    /// </summary>
    /// <param name="arena">The arena about to be played.</param>
    /// <returns>The battlefield, or <see langword="null"/> when it cannot be built.</returns>
    private BattleScene? EnsureScene(BattleArena arena)
    {
        if (_scene is not null && ReferenceEquals(_arena, arena))
        {
            return _scene;
        }

        _scene = null;
        _arena = null;

        if (arena.AttackingForce.Commander is not Hero attacker)
        {
            GameLog.Warn("The battle has no hero on the attacking side, so no screen can be built.");

            return null;
        }

        BattleScene? scene = BattleScene.TryCreateForArena(_frame, _assets, _world, attacker, arena,
            _fonts?.SmallWhite, _fonts?.White, _fonts?.Small, _fonts, out string? error);

        if (scene is null)
        {
            GameLog.Warn($"The battle screen cannot be built ({error}); the computer plays both sides.");
            arena.EnableAutoCombatForHumanSides();

            return null;
        }

        _scene = scene;
        _arena = arena;
        arena.Animator = this;

        return scene;
    }

    /// <summary>Plays the current animation of the battlefield to its end.</summary>
    private void PumpUntilIdle()
    {
        if (_scene is not { } scene)
        {
            return;
        }

        RunModal(new BattleScreenModal(scene, s => !s.IsAnimating));
    }

    /// <summary>Runs a battle window to its end in the host's frame loop.</summary>
    /// <param name="modal">The window to pump.</param>
    private void RunModal(BattleScreenModal modal)
    {
        if (_host.RunModal is not { } loop)
        {
            GameLog.Error("The adventure map has no frame loop wired up, so the battle screen stays closed.");

            return;
        }

        loop(modal);
    }

    /// <summary>Shows one sentence, over the battlefield while it is still there and in the log when it is not.</summary>
    /// <param name="body">The sentence to show.</param>
    private void ShowMessage(string body)
    {
        GameLog.Info(body);

        if (_scene is null || _scene.IsClosed)
        {
            return;
        }

        _scene.ShowMessage(body);

        if (!_scene.IsMessageOpen)
        {
            return;
        }

        RunModal(new BattleScreenModal(_scene, scene => !scene.IsMessageOpen));
    }
}
