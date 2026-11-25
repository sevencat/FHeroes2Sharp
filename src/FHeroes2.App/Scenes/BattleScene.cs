using FHeroes2.Assets;
using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Random;
using FHeroes2.Game;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Cursor;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Battle;
using FHeroes2.UI.Input;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.App.Scenes;

/// <summary>
/// The battle screen of one fight, the shell the ported <see cref="BattleArena"/> is drawn in
/// (<c>Battle::Interface</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>范围</b>：把一场真打逐帧推出来并接上人类输入——点格子走/打、空格或 <c>SKIP</c> 跳过、
/// <c>AUTO</c>/<c>Q</c> 交给电脑、点状态栏展开 6 行战斗日志、状态栏两行文字与三个按钮的悬停/按下。
/// 施法、等待/防御、撤退/投降、战斗设置窗、路径预览、顺序条与所有动画特效还没有（`bd list`）；
/// 缺什么都可以从 <c>bd 归档（PROGRESS.md，FHeroes2Sharp-jwk）</c> §7.6 的缺口清单对回去。
/// </para>
/// <para>
/// <b>谁来打</b>：攻击方是冒险地图上的焦点英雄（<see cref="AdventureSession.FocusHero"/> 的军队），
/// 防守方是地图上第一个游荡怪物（<c>World.GetObjectPositions( Mp2ObjectType.ActionMonster )</c> 的第 0 格）；
/// <c>--battle-siege</c> 改为攻打第一座别的颜色的城，守军就是它的驻军。参考的战斗由 <c>Heroes::Action</c>
/// 走到怪物格/城门格上触发，端口还没有那条链（见同节），所以先从地图里取一场真打。
/// </para>
/// <para>
/// <b>战场从哪来</b>：<see cref="BattleArena.FromTile"/> 按那一格地形铺装饰与障碍，
/// 城堡入口则给攻城。背景与边框杂物按 <see cref="BattleFieldStyle"/> 挑，与参考的构造函数同表。
/// </para>
/// <para>
/// <b>不消耗地图状态</b>：这一步的回合是直接跑在英雄的军队上的（AI 打完会真的损兵），
/// 冒烟里英雄的军队会变少。参考在战后把结果写回军队，端口将来接上 <c>BattleLoader</c> 之后
/// 这一段自然收归它；眼下 <c>--battle</c> 是画面冒烟，不是正常玩法入口。
/// </para>
/// </remarks>
public sealed class BattleScene : IScene
{
    /// <summary>Archive the battle settings OK button comes from, <see cref="SettingsArtwork.OkayAssetName"/>.</summary>
    private const string BattleSettingsAssetName = "SYSTEM.ICN";

    /// <summary>Frame of the released OK button in <see cref="BattleSettingsAssetName"/>.</summary>
    private const int BattleSettingsOkayReleasedFrame = 1;

    /// <summary>Frame of the pressed OK button in <see cref="BattleSettingsAssetName"/>.</summary>
    private const int BattleSettingsOkayPressedFrame = 2;

    private readonly ScreenBuffer _frame;
    private readonly BattleArena _arena;
    private readonly BattleFieldPainter? _painter;
    private readonly IViewWorldSpriteSource? _sprites;
    private readonly Sprite? _settingsOkay;
    private readonly Sprite? _settingsOkayPressed;
    private readonly bool _frozen;

    private bool _closed;
    private bool _roundStarted;

    /// <summary>The battle settings window while it is open, <c>DialogBattleSettings</c> of the reference.</summary>
    private BattleSettingsWindow? _settings;

    /// <summary>The cell the cursor theme was last computed for, so the reachability query runs on changes only.</summary>
    private int? _lastHoverCell;

    private BattleScene(ScreenBuffer frame, BattleArena arena, BattleFieldPainter? painter,
        IViewWorldSpriteSource? sprites, Sprite? settingsOkay, Sprite? settingsOkayPressed, bool frozen)
    {
        _frame = frame;
        _arena = arena;
        _painter = painter;
        _sprites = sprites;
        _settingsOkay = settingsOkay;
        _settingsOkayPressed = settingsOkayPressed;
        _frozen = frozen;
    }

    /// <summary>The fight this screen shows.</summary>
    public BattleArena Arena => _arena;

    /// <summary>Whether the battle is over and the screen asked to be left.</summary>
    public bool IsClosed => _closed;

    /// <summary>
    /// Builds the battle screen of the first wandering monster of <paramref name="world"/>,
    /// <c>Battle::Loader()</c>'s job of setting the arena up (<c>battle_main.cpp:291-400</c>).
    /// </summary>
    /// <param name="frame">The 640x480 frame to draw into.</param>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="world">The map the fight happens on.</param>
    /// <param name="attacker">The hero whose army attacks.</param>
    /// <param name="freezeAnimation">Whether the fight stands still, the <c>--freeze-animation</c> of a screenshot.</param>
    /// <param name="siege">Whether to attack the first town of another colour instead of a wandering monster.</param>
    /// <param name="autoPlay">Whether both sides are handed to the computer.</param>
    /// <param name="counterFont">The font of the troop counters, or <see langword="null"/> when the fonts are missing.</param>
    /// <param name="statusFont">The font of the two status lines, or <see langword="null"/> without fonts.</param>
    /// <param name="error">Why the screen cannot be built, when it cannot.</param>
    /// <returns>The screen, or <see langword="null"/> with an <paramref name="error"/>.</returns>
    public static BattleScene? TryCreate(
        ScreenBuffer frame,
        AssetBundle? assets,
        World world,
        Hero attacker,
        bool freezeAnimation,
        bool siege,
        bool autoPlay,
        IGlyphSource? counterFont,
        IGlyphSource? statusFont,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(attacker);

        error = null;

        if (!attacker.Army.IsValid)
        {
            error = "the hero has no army to fight with";

            return null;
        }

        int tileIndex;
        Army defender;
        Troop defenderTroop;

        if (siege)
        {
            CastleTown? target = FindEnemyCastle(world, attacker.GetColor());

            if (target is null)
            {
                error = "the map has no town of another colour to attack";

                return null;
            }

            if (!target.GetActualArmy(null).IsValid)
            {
                error = $"the garrison of '{target.Name}' is empty";

                return null;
            }

            tileIndex = target.TileIndex;

            // The garrison is the defending army, which is what the reference hands to Battle::Loader.
            defender = target.GetActualArmy(null);
            defenderTroop = defender.GetTroop(0);
        }
        else
        {
            tileIndex = FindMonsterTile(world);

            if (tileIndex < 0)
            {
                error = "the map has no wandering monster to fight";

                return null;
            }

            defenderTroop = world.GetTile(tileIndex).GetTroop(world);

            if (!defenderTroop.IsValid)
            {
                error = $"the monster on tile {tileIndex} has no stack";

                return null;
            }

            // A wandering stack has no commander and no colour, which the arena reads as the computer's side.
            defender = new Army();
            defender.JoinTroop(defenderTroop.Monster, defenderTroop.Count, emptySlotFirst: false);
        }

        uint seed = BattleLoader.ComputeBattleSeed(tileIndex, world.MapSeed, attacker.Army, defender);
        BattleArena arena = BattleArena.FromTile(world, attacker.Army, defender, tileIndex, new Pcg32(seed));

        if (autoPlay)
        {
            // The reference does this in the arena when no interface watches the fight.
            arena.EnableAutoCombatForHumanSides();
        }

        BattleFieldPainter? painter = null;
        IViewWorldSpriteSource? spriteSource = null;
        Sprite? settingsOkay = null;
        Sprite? settingsOkayPressed = null;
        CastleTown? castle = arena.Castle;

        if (assets is not null)
        {
            // 参考用 ICN::BUTTON_SMALL_OKAY_GOOD，这套数据里没有那个归档，端口与系统选项窗一样从
            // SYSTEM.ICN 的 1/2 号取（SettingsArtwork 的做法）。
            if (assets.TryReadIcn(BattleSettingsAssetName) is { } systemIcn &&
                systemIcn.Count > BattleSettingsOkayPressedFrame)
            {
                settingsOkay = systemIcn.Sprites[BattleSettingsOkayReleasedFrame];
                settingsOkayPressed = systemIcn.Sprites[BattleSettingsOkayPressedFrame];
            }

            Tile tile = world.GetTile(tileIndex);
            bool trees = HasNearbyTrees(world, tileIndex);

            BattleFieldStyle style = castle is null
                ? BattleFieldStyle.ForGround(tile.Ground, trees)
                : BattleFieldStyle.ForCastle(tile.Ground, trees, castle.Race);

            spriteSource = new IcnViewWorldSpriteSource(assets);

            painter = new BattleFieldPainter(
                arena,
                spriteSource,
                new MonsterAnimInfoCache(assets),
                style,
                counterFont,
                frame.Palette,
                statusFont);
        }
        else
        {
            GameLog.Warn("The battle screen has no artwork, so the battlefield stays black.");
        }

        GameLog.Info(
            $"Battle opened on tile {tileIndex}: attacker '{attacker.Name}' " +
            $"({attacker.Army.OccupiedSlotCount} stacks), defender {defenderTroop.Count} x {defenderTroop.Monster}" +
            (castle is null ? ", in the open field." : $", at '{castle.Name}' ({castle.Race})."));

        CursorState.Current?.SetThemes(CursorTheme.CombatPointer);

        return new BattleScene(frame, arena, painter, spriteSource, settingsOkay, settingsOkayPressed,
            freezeAnimation);
    }

    /// <summary>
    /// Advances the fight one unit per frame, the decomposition of <c>Turns()</c> into
    /// <see cref="BattleArena.BeginRound"/> / <see cref="BattleArena.SelectNextUnit"/> /
    /// <see cref="BattleArena.PlayCurrentUnitTurn(BattleUnitTurnHandler?)"/> / <see cref="BattleArena.EndRound"/>.
    /// </summary>
    /// <param name="elapsedSeconds">Seconds since the previous frame; unused.</param>
    /// <remarks>
    /// 参考的 <c>Arena::Turns()</c> 一口气跑完一轮，因为它的人类回合里面就带着事件循环。端口的场景一帧只推一个
    /// 单位：AI 的那一手在这一帧算完，画面上单位才会「动」得出来；人类的点选接上之后，轮到人类时这一帧就只
    /// 等输入，不再往下推。
    /// </remarks>
    public void Update(double elapsedSeconds)
    {
        _ = elapsedSeconds;

        if (_closed)
        {
            return;
        }

        LocalEvent events = LocalEvent.Current;

        // 战斗设置窗是模态的：打开期间它吃掉输入，战场不再推进（参考里它是一个阻塞的对话框）。
        if (_settings is not null)
        {
            if (_settings.ProcessEvents(events))
            {
                _settings = null;
            }

            return;
        }

        UpdateHover(events);

        // 状态栏点击展开/收起日志；日志展开时先吃掉落在自己身上的点击，战场就收不到。
        UpdateBattleLog(events);

        // 状态栏三个按钮与两个自动战斗快捷键：吃掉点击的这一帧不再推进回合。
        if (UpdateStatusBarCommands(events))
        {
            return;
        }

        if (_frozen)
        {
            // A frozen frame still answers "who would act": the hover shadow needs a current unit.
            EnsureCurrentUnit();

            return;
        }

        // The idle breathing of both armies runs every frame, independently of the turns.
        _painter?.Update();

        if (!_arena.BattleValid())
        {
            Finish();

            return;
        }

        if (!_roundStarted)
        {
            _arena.BeginRound();

            _roundStarted = true;
        }

        BattleUnit? next = _arena.SelectNextUnit();

        _arena.PlayCastleActions();

        if (!_arena.BattleValid())
        {
            Finish();

            return;
        }

        if (next is null)
        {
            // Nobody could act any more: the round is over.
            SettleRound();

            if (!_arena.BattleValid())
            {
                Finish();
            }

            return;
        }

        // A human stack waits for the pointer; everything else is played by the AI for now.
        if (IsHumanUnit(next))
        {
            TryPlayHumanCommand(next, events);

            return;
        }

        _arena.PlayCurrentUnitTurn((BattleUnitTurnHandler?)null);

        if (!_arena.BattleValid())
        {
            Finish();
        }
    }

    /// <summary>
    /// Expands or collapses the battle log on a click on the status bar and lets the open window take the
    /// input that lands on it, the status bar branch of <c>Interface::MouseCursorArea()</c>
    /// (<c>battle_interface.cpp:3506-3522</c>).
    /// </summary>
    /// <param name="events">Input state of this frame.</param>
    /// <returns>
    /// <see langword="true"/> when the input belonged to the log; the click has then already been consumed,
    /// so the battlefield does not react to it.
    /// </returns>
    /// <remarks>
    /// 参考的日志窗打开时盖在战场上，行与滚动条的输入由 <c>ListBox::QueueEventProcessing()</c> 处理，
    /// 而窗口外框与滚动条两列不归列表管——这里补一刀 <see cref="BattleStatusLog.Frame"/>，
    /// 否则点外框会顺带把单位走出去。
    /// </remarks>
    private bool UpdateBattleLog(LocalEvent events)
    {
        if (_painter?.StatusLog is not { } log)
        {
            return false;
        }

        if (events.MouseClickLeft(BattleFieldPainter.StatusBarArea))
        {
            log.SetOpen(!log.IsOpen);

            return true;
        }

        if (!log.IsOpen)
        {
            return false;
        }

        log.QueueEventProcessing(events);

        return events.MouseClickLeft(log.Frame);
    }

    /// <summary>
    /// The Auto, Settings and Skip buttons under the battlefield plus the two auto combat hot keys, the
    /// button branches of <c>Interface::MouseCursorArea()</c> (<c>battle_interface.cpp:3317-3361</c>).
    /// </summary>
    /// <param name="events">Input state of this frame.</param>
    /// <returns>
    /// <see langword="true"/> when a button or a hot key did something; the frame must then not advance the
    /// round again, the command has already played.
    /// </returns>
    /// <remarks>
    /// <para>
    /// 悬停时把下行写成参考的提示串（<c>Automatic combat modes</c> 等）并把光标切成战场指针；按住不放就画按下态的帧，
    /// 判断与绘制分开在 <see cref="BattleStatusButtons"/> 与 <see cref="BattleFieldPainter.PressedButton"/> 两侧。
    /// </para>
    /// <para>
    /// 参考点 Auto 打开一个「AUTO COMBAT / QUICK COMBAT」小窗（<c>OpenAutoModeDialog</c>），端口直接把
    /// Auto 按钮接到 <c>TOGGLE_AUTO_COMBAT</c>（小窗的一半语义），QUICK COMBAT 接到 <c>Q</c> 快捷键；
    /// 两档选择窗留给 <c>bd list</c> 里的战斗对话框（<c>ukw.25</c>）。
    /// </para>
    /// </remarks>
    private bool UpdateStatusBarCommands(LocalEvent events)
    {
        if (_painter is null)
        {
            return false;
        }

        BattleStatusButtons buttons = _painter.Buttons;
        BattleStatusButton hovered = buttons.HitTest(events.CursorPosition);

        _painter.PressedButton = hovered != BattleStatusButton.None
            && events.IsMouseLeftButtonPressedAndHeldInArea(buttons.AreaOf(hovered))
                ? hovered
                : BattleStatusButton.None;

        if (hovered != BattleStatusButton.None)
        {
            CursorState.Current?.SetThemes(CursorTheme.CombatPointer);
            _painter.SetStatus(HintOf(hovered), top: false);

            if (!_frozen && events.MouseClickLeft(buttons.AreaOf(hovered)))
            {
                return hovered switch
                {
                    BattleStatusButton.Auto => ApplyAutoCombatToggle(),
                    BattleStatusButton.Settings => OpenBattleSettings(),
                    BattleStatusButton.Skip => SkipCurrentUnit(),
                    _ => false,
                };
            }
        }

        if (_frozen)
        {
            // A frozen frame shows the hint and the pressed look but plays nothing.
            return false;
        }

        // 参考把两个自动战斗的快捷键与按钮放在同一分支里（battle_interface.cpp:3707-3721）。
        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleToggleAutoCombat))
        {
            return ApplyAutoCombatToggle();
        }

        return GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleQuickCombat) && ApplyQuickCombat();
    }

    /// <summary>The lower line hint of a button, the <c>msg</c> of the reference (<c>battle_interface.cpp:3320-3347</c>).</summary>
    /// <param name="button">The button under the pointer.</param>
    /// <returns>The hint, or an empty string for <see cref="BattleStatusButton.None"/>.</returns>
    private static string HintOf(BattleStatusButton button) => button switch
    {
        BattleStatusButton.Auto => "Automatic combat modes",
        BattleStatusButton.Settings => "Customize system options",
        BattleStatusButton.Skip => "Skip this unit",
        _ => string.Empty,
    };

    /// <summary>
    /// Hands the current unit over to the computer (or takes it back), the <c>AUTO COMBAT</c> half of
    /// <c>Interface::_startAutoCombat()</c> (<c>battle_interface.cpp:3645-3654</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the command was played.</returns>
    private bool ApplyAutoCombatToggle()
    {
        if (_arena.CurrentUnit is not { } unit || !_arena.CanToggleAutoCombat)
        {
            return false;
        }

        PlayerColor color = unit.GetCurrentColor();
        bool enabled = !_arena.IsAutoCombat(color);

        GameLog.Info($"Auto combat for {color} is now {(enabled ? "on" : "off")}.");

        _arena.PlayCurrentUnitTurn([BattleCommand.ToggleAutoCombat((int)color)]);

        _lastHoverCell = null;

        if (!_arena.BattleValid())
        {
            Finish();
        }

        return true;
    }

    /// <summary>
    /// Hands the rest of the fight to the computer, <c>Interface::_quickCombat()</c>
    /// (<c>battle_interface.cpp:3656-3662</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the command was played.</returns>
    private bool ApplyQuickCombat()
    {
        if (_arena.CurrentUnit is null)
        {
            return false;
        }

        GameLog.Info("Quick combat: the computer finishes the battle.");

        _arena.PlayCurrentUnitTurn([BattleCommand.QuickCombat()]);

        _lastHoverCell = null;

        if (!_arena.BattleValid())
        {
            Finish();
        }

        return true;
    }

    /// <summary>
    /// Skips the unit whose turn it is, the <c>SKIP</c> button's command
    /// (<c>battle_interface.cpp:3349-3355</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the command was played.</returns>
    private bool SkipCurrentUnit()
    {
        if (_arena.CurrentUnit is not { } unit || !IsHumanUnit(unit))
        {
            return false;
        }

        PlayCommands(unit, [BattleCommand.Skip((int)unit.Uid)]);

        return true;
    }

    /// <summary>
    /// Opens the battle settings window, <c>Interface::_openBattleSettingsDialog()</c>
    /// (<c>battle_interface.cpp:3623-3635</c>).
    /// </summary>
    /// <returns><see langword="true"/> when the window is open; nothing was played in that case.</returns>
    private bool OpenBattleSettings()
    {
        if (_settings is not null)
        {
            return false;
        }

        _settings = _sprites is null || _settingsOkay is null || _settingsOkayPressed is null
            ? null
            : BattleSettingsWindow.TryCreate(_sprites, _settingsOkay, _settingsOkayPressed,
                new Size(ScreenBuffer.DefaultWidth, ScreenBuffer.DefaultHeight), turnOrderInsideWindow: true);

        if (_settings is null)
        {
            GameLog.Warn("The battle settings window has no artwork.");

            return false;
        }

        GameLog.Info("The battle settings window opened.");

        return true;
    }

    /// <summary>
    /// Picks the unit that acts first so a frozen frame can show the hover shadow, without playing anything.
    /// </summary>
    private void EnsureCurrentUnit()
    {
        if (_arena.CurrentUnit is null && _arena.BattleValid())
        {
            _arena.SelectNextUnit();
        }
    }

    /// <summary>
    /// Whether <paramref name="unit"/> waits for the player, <c>GetCurrentControl() == CONTROL_HUMAN</c> and
    /// not handed over to the computer by the auto combat flag.
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <returns><see langword="true"/> when the screen should wait for a click.</returns>
    private bool IsHumanUnit(BattleUnit unit) =>
        unit.GetCurrentControl().IsHuman() && !_arena.IsAutoCombat(unit.GetCurrentColor());

    /// <summary>
    /// Turns what the player did this frame into the current unit's commands, if anything was asked for.
    /// </summary>
    /// <param name="unit">The human unit whose turn it is.</param>
    /// <param name="events">Input state of this frame.</param>
    /// <returns><see langword="true"/> when a turn was played.</returns>
    /// <remarks>
    /// 这一批接三种输入：左键点格子（空格子走、敌人格打）、<c>HotKeyEvent::BattleSkip</c>（默认空格）跳过，
    /// 以及状态栏那三个按钮（见 <see cref="UpdateStatusBarCommands"/>）。等待/防御、施法、撤退/投降仍然没有，
    /// 见 <c>bd list</c>。
    /// </remarks>
    private bool TryPlayHumanCommand(BattleUnit unit, LocalEvent events)
    {
        List<BattleCommand>? commands = null;

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleSkip))
        {
            // 参考的 HotKeyEvent::BattleSkip 默认就是空格。
            commands = [BattleCommand.Skip((int)unit.Uid)];
        }
        else if (events.MouseClickLeft(BattleFieldPainter.BattlefieldArea))
        {
            int cell = BattleBoard.GetIndexAbsPosition(_arena.Board, events.CursorPosition);

            if (BattleBoard.IsValidIndex(cell))
            {
                BattleCommand? command = _arena.GetTroopBoard(cell) is { } target
                    && target.GetCurrentColor() != unit.GetCurrentColor()
                    ? _arena.CreateAttackCommand(unit, cell)
                    : _arena.CreateMoveCommand(unit, cell);

                if (command is not null)
                {
                    commands = [command];
                }
            }
        }

        if (commands is null)
        {
            return false;
        }

        PlayCommands(unit, commands);

        return true;
    }

    /// <summary>
    /// Plays the commands a player picked, <c>Interface::ApplyAction()</c>'s caller side: the status line is
    /// written first because the arena consumes a command's parameters as it applies them.
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <param name="commands">What it does.</param>
    private void PlayCommands(BattleUnit unit, List<BattleCommand> commands)
    {
        // The arena consumes a command's parameters as it applies them, so the text is taken first.
        string status = StatusOf(commands[0]);

        GameLog.Info($"The player's {unit.Monster} acts: {status}.");

        _arena.PlayCurrentUnitTurn(commands);

        // The upper status line reports the action and pushes it to the battle log.
        _painter?.SetStatus(status, top: true);

        // The next unit may have other reachable cells: recompute the cursor theme on the next hover.
        _lastHoverCell = null;

        if (!_arena.BattleValid())
        {
            Finish();
        }
    }

    /// <summary>
    /// A short human readable form of the command a player gave, for the status line and the smoke log.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A description such as <c>Move to 24</c>.</returns>
    private static string StatusOf(BattleCommand command) => command.Type switch
    {
        // Parameters are stored in reverse: Move( unitUid, cellIndex ) keeps the cell first.
        BattleCommandType.Move => $"Move to {command[0]}",
        BattleCommandType.Attack => "Attack",
        BattleCommandType.Skip => "Skip",
        _ => command.Type.ToString(),
    };

    /// <summary>
    /// Carries the pointer into the painter, the hover half of <c>MouseCursorArea*</c>
    /// (<c>interface_events.cpp</c>); the cell is <see langword="null"/> when the pointer is off the board.
    /// </summary>
    /// <param name="events">Input state of this frame.</param>
    private void UpdateHover(LocalEvent events)
    {
        if (_painter is null)
        {
            return;
        }

        Point cursor = events.CursorPosition;
        int cell = BattleFieldPainter.BattlefieldArea.Contains(cursor)
            ? BattleBoard.GetIndexAbsPosition(_arena.Board, cursor)
            : -1;

        int? hovered = BattleBoard.IsValidIndex(cell) ? cell : null;
        _painter.SetHoveredCell(hovered);

        if (hovered != _lastHoverCell)
        {
            _lastHoverCell = hovered;
            UpdateCursorTheme(hovered);
        }

        if (hovered is not null)
        {
            return;
        }

        // Off the board the lower line carries the status bar hint, the reference's
        // «Show logs»/«Hide logs» (<c>battle_interface.cpp:3511</c>).
        bool overStatusBar = BattleFieldPainter.StatusBarArea.Contains(cursor);

        _painter.SetStatus(overStatusBar ? (_painter.StatusLog.IsOpen ? "Hide logs" : "Show logs") : string.Empty,
            top: false);
    }

    /// <summary>
    /// Picks the cursor theme of the hovered cell, the acting half of <c>Interface::GetBattleCursor()</c>
    /// (<c>battle_interface.cpp:2922-3064</c>).
    /// </summary>
    /// <param name="cell">The hovered cell, or <see langword="null"/>.</param>
    /// <remarks>
    /// 参考按「能不能走 / 能不能打 / 打的是哪个方向」挑 8 把剑光标之一。端口先只区分三种：可走的
    /// <see cref="CursorTheme.CombatMove"/>（飞兵用 <see cref="CursorTheme.CombatFly"/>）、可打的
    /// <see cref="CursorTheme.CombatArrow"/>、其余 <see cref="CursorTheme.CombatPointer"/>；八向剑光标与
    /// 施法光标随行动交互的下一批补。
    /// </remarks>
    private void UpdateCursorTheme(int? cell)
    {
        CursorTheme theme = CursorTheme.CombatPointer;
        string hint = string.Empty;

        if (_arena.CurrentUnit is { } unit && cell is { } index && IsHumanUnit(unit))
        {
            bool isEnemy = _arena.GetTroopBoard(index) is { } target
                && target.GetCurrentColor() != unit.GetCurrentColor();

            if (isEnemy)
            {
                if (_arena.CreateAttackCommand(unit, index) is not null)
                {
                    theme = CursorTheme.CombatArrow;
                    hint = "Attack";
                }
            }
            else if (_arena.CreateMoveCommand(unit, index) is not null)
            {
                theme = unit.IsFlying ? CursorTheme.CombatFly : CursorTheme.CombatMove;
                hint = unit.IsFlying ? "Flying" : "Move";
            }
        }

        CursorState.Current?.SetThemes(theme);

        // The lower status line carries the cursor hint, the reference's setStatus( msg, false ).
        _painter?.SetStatus(hint, top: false);
    }

    /// <summary>Logs the settled result once and hands the screen back to the map.</summary>
    private void Finish()
    {
        SettleRound();

        BattleResult result = _arena.Result;
        string outcome = result.IsAttackerWin
            ? "the attacker won"
            : result.IsDefenderWin
                ? "the defender won"
                : "nobody won";

        GameLog.Info($"The battle is over: attacker '{result.Attacker}', defender '{result.Defender}', {outcome}.");

        Close();
    }

    /// <summary>
    /// Closes the open round, <c>BattleArena.EndRound()</c>, exactly once.
    /// </summary>
    /// <remarks>
    /// 结算必须在离开之前跑：<c>SettleResult()</c> 才是把「场上没人了」翻成 <c>Result</c> 的那一步
    /// （参考在整个 <c>Turns()</c> 的末尾做）。不结算就离开会得到一份全空的战果，日志里是「nobody won」。
    /// </remarks>
    private void SettleRound()
    {
        if (!_roundStarted)
        {
            return;
        }

        _arena.EndRound();

        _roundStarted = false;
    }

    /// <summary>Draws the battlefield into the frame.</summary>
    /// <param name="state">Frame diagnostics, not used by this screen.</param>
    public void DrawFrame(in DiagnosticsState state)
    {
        _ = state;

        if (_painter is null)
        {
            ImageOps.Clear(_frame.Pixels, 0);
        }
        else
        {
            _painter.Redraw(_frame.Pixels);
        }

        // 设置窗画在战场之上（参考里它自己的 StandardWindow 直接画到屏幕上）。
        _settings?.Draw(_frame.Pixels);

        // The battle is painted straight into the indexed pixels, which cannot report the change.
        _frame.InvalidateAll();
    }

    /// <summary>The battle screen draws its text with the game fonts, so nothing goes through the overlay.</summary>
    /// <param name="state">Frame diagnostics, not used by this screen.</param>
    public void DrawOverlay(in DiagnosticsState state)
    {
    }

    /// <summary>Leaves the battle and hands the screen back to the map.</summary>
    public void Close() => _closed = true;

    /// <summary>The first wandering monster of the map, <c>MP2::OBJ_MONSTER</c> positions in tile order.</summary>
    /// <param name="world">The map to scan.</param>
    /// <returns>The tile index, or <c>-1</c> when there is no monster on the map.</returns>
    /// <remarks>
    /// 瓦片存的是文件里的动作值（<see cref="Mp2ObjectType.ActionMonster"/>），与参考的
    /// <c>Maps::GetObjectPositions()</c> 一样按原值比较（ADR-133）。
    /// </remarks>
    private static int FindMonsterTile(World world)
    {
        List<int> positions = world.GetObjectPositions(Mp2ObjectType.ActionMonster);

        return positions.Count > 0 ? positions[0] : -1;
    }

    /// <summary>
    /// The first town of a colour other than <paramref name="attackerColor"/>,
    /// the <c>--battle-siege</c> of the command line.
    /// </summary>
    /// <param name="world">The map to scan.</param>
    /// <param name="attackerColor">The colour of the attacking kingdom.</param>
    /// <returns>The town, or <see langword="null"/> when every town is neutral or the attacker's own.</returns>
    /// <remarks>
    /// 参考的攻城由英雄走到城门格触发，城门格上的城由 <c>World::getCastleEntrance()</c> 给出；
    /// 端口照这条读取，只是把「找哪座城」交给命令行。
    /// </remarks>
    private static CastleTown? FindEnemyCastle(World world, PlayerColor attackerColor)
    {
        foreach (CastleTown castle in world.Castles)
        {
            if (castle.Color == PlayerColor.None || castle.Color == attackerColor)
            {
                continue;
            }

            if (world.GetCastleEntrance(castle.TileIndex) is not null)
            {
                return castle;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether one of the eight tiles around <paramref name="tileIndex"/> holds trees,
    /// <c>Maps::hasNearbyObject( tileIndex, MP2::OBJ_TREES )</c> (<c>maps_tiles_helper.cpp</c>).
    /// </summary>
    /// <param name="world">The map to read.</param>
    /// <param name="tileIndex">The tile in the middle.</param>
    /// <returns><see langword="true"/> when the battlefield is drawn with the wooded background.</returns>
    private static bool HasNearbyTrees(World world, int tileIndex)
    {
        int centerX = tileIndex % world.Width;
        int centerY = tileIndex / world.Width;

        for (int y = centerY - 1; y <= centerY + 1; ++y)
        {
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            for (int x = centerX - 1; x <= centerX + 1; ++x)
            {
                if (x < 0 || x >= world.Width)
                {
                    continue;
                }

                if (world.GetTile(x, y).GetMainObjectType(world, false) == Mp2ObjectType.Trees)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

