using FHeroes2.App.Fonts;
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
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using FHeroes2.UI.Battle;
using FHeroes2.UI.Dialogs;
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
/// <c>AUTO</c>/<c>Q</c> 交给电脑、<c>C</c> 开书施法、<c>R</c>/<c>S</c> 撤退与投降、点状态栏展开 6 行战斗日志、
/// 状态栏两行文字与三个按钮的悬停/按下、战斗设置窗、战场顶上的行动顺序条。路径预览与所有动画特效还没有（`bd list`）；
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

    /// <summary>Title of the box that opens when <c>CanCastSpell()</c> refuses with a sentence, <c>_( "Error" )</c>.</summary>
    private const string ErrorTitle = "Error";

    /// <summary>The page and filter the spell book reopens on, the hero's <c>SpellBook</c> state.</summary>
    private readonly SpellBook _spellBookState = new();

    private readonly ScreenBuffer _frame;
    private readonly BattleArena _arena;
    private readonly BattleFieldPainter? _painter;
    private readonly IViewWorldSpriteSource? _sprites;
    private readonly Sprite? _settingsOkay;
    private readonly Sprite? _settingsOkayPressed;
    private readonly SpellBookArtwork? _spellBookArtwork;
    private readonly SpellBookFonts? _spellBookFonts;
    private readonly World _world;
    private readonly AssetBundle? _assets;
    private readonly IGlyphSource? _whiteFont;
    private readonly IGlyphSource? _smallWhiteFont;
    private readonly GameFonts? _fonts;
    private readonly SystemButtonArtwork? _systemButtons;
    private readonly bool _frozen;
    private readonly bool _autoPlay;
    private readonly Hero _attacker;
    private readonly Mons32Sprites? _miniMonsters;

    private bool _closed;
    private bool _roundStarted;

    /// <summary>The battle settings window while it is open, <c>DialogBattleSettings</c> of the reference.</summary>
    private BattleSettingsWindow? _settings;

    /// <summary>The spell book while it is open, <c>HeroBase::OpenSpellBook( CMBT, ... )</c>.</summary>
    private SpellBookWindow? _spellBook;

    /// <summary>Text box while it is open, the reference's <c>Dialog::Message</c> of retreat and of the refusals.</summary>
    private MessageBox? _message;

    /// <summary>What to do with the answer of <see cref="_message"/>.</summary>
    private Action<DialogButton>? _messageAnswer;

    /// <summary>
    /// Whether <see cref="_message"/> is one of the reference's <c>Dialog::ZERO</c> boxes, which live while the right
    /// button is held and go away when it is let go, instead of waiting for a button of their own.
    /// </summary>
    private bool _messageHeldByRightButton;

    /// <summary>The enemy's offer while it is open, <c>DialogBattleSurrender()</c>.</summary>
    private BattleSurrenderWindow? _surrender;

    /// <summary>The marketplace reached from the surrender window, <c>Dialog::Marketplace( ... )</c>.</summary>
    private MarketplaceWindow? _marketplace;

    /// <summary>The unit whose turn the retreat or surrender belongs to.</summary>
    private BattleUnit? _surrenderUnit;

    /// <summary>The spell being aimed, <c>Interface::humanturn_spell</c>; <see cref="SpellType.None"/> when not casting.</summary>
    private Spell _pendingSpell = new(SpellType.None);

    /// <summary>
    /// The player's command whose march or attack swing is still playing, <c>Interface::_movingUnit</c> in the
    /// port's frame form. The command reaches the arena only after that drawing is done.
    /// </summary>
    private (BattleUnit Unit, List<BattleCommand> Commands, string StatusLine)? _pendingAction;

    /// <summary>
    /// The blows of the attack that was just applied, whose reactions (wince and death) are still to be drawn,
    /// <c>_redrawActionWincesKills()</c> (<c>battle_interface.cpp:4258-4439</c>).
    /// </summary>
    private IReadOnlyList<BattleAttackBlow>? _pendingReactions;

    /// <summary>
    /// The source cell picked for a teleport, <c>Interface::_teleportSpellSrcIdx</c>; <c>-1</c> while no source has
    /// been picked.
    /// </summary>
    private int _teleportSourceCell = BattleSpellHighlight.NoCell;

    /// <summary>The cell the cursor theme was last computed for, so the reachability query runs on changes only.</summary>
    private int? _lastHoverCell;

    /// <summary>
    /// The pointer the cursor theme was last computed for. The eight sword cursors depend on which petal of the
    /// hovered cell the pointer rests on, so a move inside one cell still recomputes.
    /// </summary>
    private Point _lastHoverCursor = new(-1, -1);

    /// <summary>The result window while the fight is wrapped up, <c>DialogBattleSummary()</c>.</summary>
    private BattleSummaryWindow? _summary;

    /// <summary>
    /// Whether an outer presenter drives the arena and this screen only paints and collects commands,
    /// <see cref="TryCreateForArena"/>.
    /// </summary>
    /// <remarks>
    /// 冒险地图上打起来的仗走这一支：竞技场的轮次与结算在 <see cref="BattleLoader"/> 里跑，
    /// 这一屏既不调 <see cref="BattleArena.SelectNextUnit"/> 也不调 <see cref="BattleArena.PlayCurrentUnitTurn"/>，
    /// 免得同一场仗被两个循环各推一遍。
    /// </remarks>
    private readonly bool _presented;

    /// <summary>The commands the player just picked, waiting for the presenter to hand them to the arena.</summary>
    private List<BattleCommand>? _takenCommands;

    /// <summary>The upper status line to write once the arena has applied <see cref="_takenCommands"/>.</summary>
    private string? _takenStatus;

    private BattleScene(ScreenBuffer frame, BattleArena arena, BattleFieldPainter? painter,
        IViewWorldSpriteSource? sprites, Sprite? settingsOkay, Sprite? settingsOkayPressed,
        SpellBookArtwork? spellBookArtwork, SpellBookFonts? spellBookFonts, World world, AssetBundle? assets,
        IGlyphSource? whiteFont, IGlyphSource? smallWhiteFont, GameFonts? fonts,
        SystemButtonArtwork? systemButtons, bool frozen, bool autoPlay, Hero attacker, Mons32Sprites? miniMonsters,
        bool presented)
    {
        _frame = frame;
        _arena = arena;
        _painter = painter;
        _sprites = sprites;
        _settingsOkay = settingsOkay;
        _settingsOkayPressed = settingsOkayPressed;
        _spellBookArtwork = spellBookArtwork;
        _spellBookFonts = spellBookFonts;
        _world = world;
        _assets = assets;
        _whiteFont = whiteFont;
        _smallWhiteFont = smallWhiteFont;
        _fonts = fonts;
        _systemButtons = systemButtons;
        _frozen = frozen;
        _autoPlay = autoPlay;
        _attacker = attacker;
        _miniMonsters = miniMonsters;
        _presented = presented;
    }

    /// <summary>The fight this screen shows.</summary>
    public BattleArena Arena => _arena;

    /// <summary>Whether the battle is over and the screen asked to be left.</summary>
    public bool IsClosed => _closed;

    /// <summary>
    /// Whether a march, a swing or a reaction is being drawn, <c>Interface::AnimateInfrequentDelay()</c>'s question
    /// in the port's blocking form.
    /// </summary>
    /// <remarks>
    /// presenter 在这一段时间里一帧一帧地把画面跑完（<see cref="Update"/> 自己会让出输入），
    /// 跑完才继续结算，与参考在 <c>ApplyAction*()</c> 里阻塞播放等价。
    /// </remarks>
    public bool IsAnimating => _painter is { IsAnimating: true };

    /// <summary>Whether the player's command is waiting to be handed to the arena.</summary>
    public bool HasPendingCommands => _takenCommands is not null;

    /// <summary>Whether the result window of the fight is open, <c>DialogBattleSummary()</c>.</summary>
    public bool IsSummaryOpen => _summary is not null;

    /// <summary>Whether a text box is open over the battlefield, the <c>Dialog::Message</c> of the loader's tail.</summary>
    public bool IsMessageOpen => _message is not null;

    /// <summary>
    /// Shows one sentence over the battlefield and waits for the player, the <c>Dialog::Message( "", text )</c> the
    /// reference runs after the summary for necromancy, the assembled artifact set and the eagle eye
    /// (<c>battle_main.cpp:417-437</c>).
    /// </summary>
    /// <param name="body">The sentence to show.</param>
    /// <remarks>
    /// 只在 presented 这一支有用：<c>BattleLoader.Settle</c> 手上没有对话框层，所以它把话交给 presenter，
    /// presenter 再借这一屏的按钮与字把它弹出来。没有按钮美术时退回状态行（<see cref="ShowMessageBox"/> 的老规矩）。
    /// </remarks>
    public void ShowMessage(string body) => ShowMessageBox(string.Empty, body, null);

    /// <summary>
    /// Takes the commands the player picked for the current unit and clears them,
    /// the <see cref="BattleUnitTurnHandler"/> side of a presented battle.
    /// </summary>
    /// <param name="commands">Filled with what the unit does.</param>
    /// <returns><see langword="true"/> when commands were waiting.</returns>
    /// <remarks>
    /// 取走之后由竞技场去应用（<c>Arena::UnitTurn()</c> 的动作循环），动画与状态行由
    /// <see cref="BeginActionAnimation"/> / <see cref="CompleteAction"/> 在结算两侧补上。
    /// </remarks>
    public bool TryTakeCommands(List<BattleCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        if (_takenCommands is not { } taken)
        {
            return false;
        }

        commands.AddRange(taken);
        _takenCommands = null;

        return true;
    }

    /// <summary>
    /// Starts the drawing that has to happen before the arena applies <paramref name="command"/>: the march of a
    /// move or an attack, the swing of a blow (<c>battle_interface.cpp:1078-1100</c>).
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <param name="command">The command about to be applied.</param>
    public void BeginActionAnimation(BattleUnit unit, BattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(command);

        _lastHoverCell = null;

        if (_painter is null)
        {
            return;
        }

        if (command.Type == BattleCommandType.Move)
        {
            _painter.StartMovement(unit, command[0]);
        }
        else if (command.Type == BattleCommandType.Attack
            && _arena.GetTroopUID((uint)command[3]) is { } defender)
        {
            _painter.StartAttackSwing(unit, defender);
        }
    }

    /// <summary>
    /// Writes the status line of the command the arena has just applied and starts the reactions of its blows,
    /// <c>Interface::ApplyAction()</c>'s tail (<c>battle_interface.cpp:1153-1200</c>).
    /// </summary>
    /// <param name="blows">The blows of the applied command; empty when it was not an attack.</param>
    public void CompleteAction(IReadOnlyList<BattleAttackBlow> blows)
    {
        ArgumentNullException.ThrowIfNull(blows);

        _painter?.SetStatus(_takenStatus ?? string.Empty, top: true);
        _takenStatus = null;
        _lastHoverCell = null;

        if (blows.Count > 0)
        {
            _painter?.StartAttackReactions(blows);
        }
    }

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
    /// <param name="smallGrayFont">
    /// The grey small font of the spell book rows, or <see langword="null"/> when the fonts are missing.
    /// </param>
    /// <param name="fonts">
    /// The font set the marketplace opened from the surrender offer needs, or <see langword="null"/> when the fonts
    /// are missing.
    /// </param>
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
        IGlyphSource? smallGrayFont,
        GameFonts? fonts,
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

        return Create(frame, assets, world, attacker, arena, tileIndex, $"{defenderTroop.Count} x {defenderTroop.Monster}",
            freezeAnimation, autoPlay, presented: false, counterFont, statusFont, smallGrayFont, fonts);
    }

    /// <summary>
    /// Builds the screen around an arena the loader has already prepared, the <c>Battle::Interface</c> constructor
    /// the reference runs from <c>Battle::Loader</c> (<c>battle_main.cpp:371-385</c>).
    /// </summary>
    /// <param name="frame">The 640x480 frame to draw into.</param>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="world">The map the fight happens on.</param>
    /// <param name="attacker">The hero whose army attacks, the side the camera looks from.</param>
    /// <param name="arena">The arena as <see cref="BattleLoader.TryPrepare"/> built it.</param>
    /// <param name="counterFont">The font of the troop counters, or <see langword="null"/> when the fonts are missing.</param>
    /// <param name="statusFont">The font of the two status lines, or <see langword="null"/> without fonts.</param>
    /// <param name="smallGrayFont">
    /// The grey small font of the spell book rows, or <see langword="null"/> when the fonts are missing.
    /// </param>
    /// <param name="fonts">
    /// The font set the marketplace opened from the surrender offer needs, or <see langword="null"/> when the fonts
    /// are missing.
    /// </param>
    /// <param name="error">Why the screen cannot be built, when it cannot.</param>
    /// <returns>The screen, or <see langword="null"/> with an <paramref name="error"/>.</returns>
    /// <remarks>
    /// 与 <see cref="TryCreate"/> 的差别只有两条：竞技场是外面给的（所以要它自己的战场格子号），
    /// 以及这一屏不再推进回合，只画与收命令（<see cref="_presented"/>）。
    /// </remarks>
    public static BattleScene? TryCreateForArena(
        ScreenBuffer frame,
        AssetBundle? assets,
        World world,
        Hero attacker,
        BattleArena arena,
        IGlyphSource? counterFont,
        IGlyphSource? statusFont,
        IGlyphSource? smallGrayFont,
        GameFonts? fonts,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(arena);

        error = null;

        if (arena.TileIndex < 0)
        {
            error = "the arena has no map tile to take the battlefield look from";

            return null;
        }

        return Create(frame, assets, world, attacker, arena, arena.TileIndex, DescribeDefender(arena),
            freezeAnimation: false, autoPlay: false, presented: true, counterFont, statusFont, smallGrayFont, fonts);
    }

    /// <summary>
    /// The artwork half both factories share: the painter, the spell book and the buttons of one arena.
    /// </summary>
    /// <param name="frame">The 640x480 frame to draw into.</param>
    /// <param name="assets">Where the sprites come from; <see langword="null"/> when the data is missing.</param>
    /// <param name="world">The map the fight happens on.</param>
    /// <param name="attacker">The hero whose army attacks.</param>
    /// <param name="arena">The arena to draw.</param>
    /// <param name="tileIndex">The map tile of the fight, for the field look and the log.</param>
    /// <param name="defenderDescription">What defends, for the log line.</param>
    /// <param name="freezeAnimation">Whether the fight stands still, the <c>--freeze-animation</c> of a screenshot.</param>
    /// <param name="autoPlay">Whether both sides are handed to the computer.</param>
    /// <param name="presented">Whether an outer presenter drives the arena.</param>
    /// <param name="counterFont">The font of the troop counters.</param>
    /// <param name="statusFont">The font of the two status lines.</param>
    /// <param name="smallGrayFont">The grey small font of the spell book rows.</param>
    /// <param name="fonts">The font set the marketplace needs.</param>
    /// <returns>The screen.</returns>
    private static BattleScene Create(
        ScreenBuffer frame,
        AssetBundle? assets,
        World world,
        Hero attacker,
        BattleArena arena,
        int tileIndex,
        string defenderDescription,
        bool freezeAnimation,
        bool autoPlay,
        bool presented,
        IGlyphSource? counterFont,
        IGlyphSource? statusFont,
        IGlyphSource? smallGrayFont,
        GameFonts? fonts)
    {
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
            $"({attacker.Army.OccupiedSlotCount} stacks), defender {defenderDescription}" +
            (castle is null ? ", in the open field." : $", at '{castle.Name}' ({castle.Race})."));

        CursorState.Current?.SetThemes(CursorTheme.CombatPointer);

        // 魔法书的图与字：字要两种小字，缺一种就不开（否则列表画出来是空的）。参考每次打开都去取图，
        // 端口在建造时取一次，因为归档在整个战斗里不会变。
        SpellBookArtwork? spellBookArtwork = SpellBookArtwork.TryCreate(assets);
        SpellBookFonts? spellBookFonts = counterFont is null || smallGrayFont is null
            ? null
            : new SpellBookFonts(counterFont, smallGrayFont);

        return new BattleScene(frame, arena, painter, spriteSource, settingsOkay, settingsOkayPressed,
            spellBookArtwork, spellBookFonts, world, assets, statusFont, counterFont, fonts,
            SystemButtonArtwork.TryCreate(assets), freezeAnimation, autoPlay, attacker,
            Mons32Sprites.TryCreate(assets), presented);
    }

    /// <summary>What defends, for the log line of <see cref="Create"/>: the strongest stack of the other side.</summary>
    /// <param name="arena">The arena whose defending force to describe.</param>
    /// <returns>Something like <c>20 x PEASANT</c>, or <c>an empty force</c>.</returns>
    private static string DescribeDefender(BattleArena arena)
    {
        BattleUnit? strongest = null;

        foreach (BattleUnit unit in arena.DefendingForce)
        {
            if (strongest is null || unit.Count > strongest.Count)
            {
                strongest = unit;
            }
        }

        return strongest is null ? "an empty force" : $"{strongest.Count} x {strongest.Monster}";
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

        // 结果窗是这场战斗的最后一屏，参考在 Turns() 之后用一个阻塞的 DialogBattleSummary 等它关掉。
        if (_summary is { } summary)
        {
            UpdateSummary(summary, events);

            return;
        }

        // 消息框（撤退确认、各种拒绝、法术说明）是模态的，参考用一个阻塞的 Dialog::Message 等它关掉。
        if (_message is { } message)
        {
            // Dialog::ZERO 的框没有按钮：按住右键期间显示，松开就收起，左键不理。
            if (_messageHeldByRightButton)
            {
                if (!events.IsMouseRightButtonPressed)
                {
                    _message = null;
                    _messageHeldByRightButton = false;
                }

                return;
            }

            DialogButton answer = message.Buttons?.ProcessEvents(events) ?? DialogButton.None;

            if (answer != DialogButton.None)
            {
                Action<DialogButton>? callback = _messageAnswer;
                _message = null;
                _messageAnswer = null;
                callback?.Invoke(answer);
            }

            return;
        }

        // 从投降窗点进的市场盖在它上面；关掉后回到投降窗并重算一次「钱够不够」。
        if (_marketplace is { } marketplace)
        {
            marketplace.SetCursor(events.CursorPosition);
            marketplace.SetLeftButton(events.IsMouseLeftButtonPressed);
            marketplace.SetRightButton(events.IsMouseRightButtonPressed);
            _ = marketplace.Update();

            if (marketplace.IsClosed)
            {
                _marketplace = null;
                _surrender?.Refresh();
            }

            return;
        }

        // 投降报价窗同样是模态的，只是它不开自己的循环，输入一帧一帧喂进去。
        if (_surrender is { } surrender)
        {
            UpdateSurrender(surrender, events);

            return;
        }

        // 战斗设置窗是模态的：打开期间它吃掉输入，战场不再推进（参考里它是一个阻塞的对话框）。
        if (_settings is not null)
        {
            if (_settings.ProcessEvents(events))
            {
                _settings = null;
            }

            return;
        }

        // 魔法书同样是模态的：它盖在战场之上，打开期间不吃战场的点击，回合不推进。
        if (_spellBook is not null)
        {
            UpdateSpellBook(events);

            return;
        }

        // 参考在 ApplyAction* 里阻塞着把整段动画播完；端口把这段时间挡在输入与回合推进之前，
        // 播完后才把命令交给竞技场，回合正常结算。攻击多一拍：起手播完先结算，再播受击与死亡。
        if (_pendingAction is not null || _pendingReactions is not null)
        {
            _painter?.Update();

            if (_painter is { IsAnimating: true })
            {
                return;
            }

            if (_pendingAction is { } pending)
            {
                _pendingAction = null;
                ApplyTurn(pending.Unit, pending.Commands, pending.StatusLine);

                if (_pendingReactions is { } reactions)
                {
                    _pendingReactions = null;
                    _painter?.StartAttackReactions(reactions);
                }
            }
            else
            {
                _pendingReactions = null;
            }

            return;
        }

        // 由 presenter 驱动时（冒险地图上打起来的仗），这一屏只画与收命令：轮次、AI 与结算都归外层的
        // BattleLoader，同一个竞技场不能被两个循环各推一遍（见 TryCreateForArena）。
        if (_presented)
        {
            UpdatePresentedTurn(events);

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
    /// The frame of a presented turn: paint, take the player's command, and never advance the arena.
    /// </summary>
    /// <param name="events">Input state of this frame.</param>
    /// <remarks>
    /// 参考的人类回合本身就是「画到有人点了什么为止」的循环（<c>Arena::HumanTurn()</c>），端口把这个循环交给
    /// presenter：这里只处理一帧，命令攒够就由 <see cref="TryTakeCommands"/> 交出去。
    /// </remarks>
    private void UpdatePresentedTurn(LocalEvent events)
    {
        // The idle breathing and the action animations are the painter's own business.
        _painter?.Update();

        // 动画期间不接输入：参考在 ApplyAction* 里阻塞着播完，玩家的点击不会落到下一个单位身上。
        if (_painter is { IsAnimating: true })
        {
            return;
        }

        UpdateHover(events);

        // 状态栏点击展开/收起日志；日志展开时先吃掉落在自己身上的点击，战场就收不到。
        UpdateBattleLog(events);

        // 状态栏三个按钮与两个自动战斗快捷键：吃掉点击的这一帧不再看战场。
        if (_frozen || UpdateStatusBarCommands(events))
        {
            return;
        }

        if (_arena.CurrentUnit is { } unit && IsHumanUnit(unit))
        {
            TryPlayHumanCommand(unit, events);
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

        // 交回电脑时正在瞄准的法术作废：参考的自动战斗会清掉 humanturn_spell。
        if (enabled)
        {
            CancelPendingSpell();
        }

        GameLog.Info($"Auto combat for {color} is now {(enabled ? "on" : "off")}.");

        if (_presented)
        {
            // 交回电脑也是一条命令，要由竞技场那一边应用才会改控制权。
            _takenCommands = [BattleCommand.ToggleAutoCombat((int)color)];
            _takenStatus = string.Empty;

            return true;
        }

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

        if (_presented)
        {
            _takenCommands = [BattleCommand.QuickCombat()];
            _takenStatus = string.Empty;

            return true;
        }

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
    /// 这一批接五种输入：左键点格子（空格子走、敌人格打）、<c>HotKeyEvent::BattleSkip</c>（默认空格）跳过、
    /// <c>HotKeyEvent::BattleCastSpell</c>（默认 <c>C</c>）打开魔法书、<c>HotKeyEvent::BattleRetreat</c>（<c>R</c>）
    /// 与 <c>HotKeyEvent::BattleSurrender</c>（<c>S</c>），以及状态栏那三个按钮
    /// （见 <see cref="UpdateStatusBarCommands"/>）。正在瞄准法术时输入全归 <see cref="TryPlayPendingSpell"/>。
    /// </remarks>
    private bool TryPlayHumanCommand(BattleUnit unit, LocalEvent events)
    {
        if (_pendingSpell.Type != SpellType.None)
        {
            return TryPlayPendingSpell(unit, events);
        }

        List<BattleCommand>? commands = null;

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleCastSpell))
        {
            // 参考在这里查「能不能施法」与「有没有魔法书」，两句话都由 OpenSpellBook 开成消息框。
            OpenSpellBook(unit);

            return false;
        }

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleRetreat))
        {
            AskRetreat(unit);

            return false;
        }

        if (GameHotKeys.HotKeyPressEvent(HotKeyEvent.BattleSurrender))
        {
            AskSurrender(unit);

            return false;
        }

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

        // 跳过的提示是参考自己的整句（带生物的复数名与单复数动词），其余动作仍是短标签。
        string statusLine = commands[0].Type == BattleCommandType.Skip
            ? BattleActionText.Skip(unit)
            : status;

        // presented 时结算不在这一屏：命令交出去，竞技场应用之前先播行军/挥击、之后播受击与死亡
        // （BattleArena.Animator 的两个回调），状态行等应用完再写。
        if (_presented)
        {
            _takenCommands = commands;
            _takenStatus = statusLine;

            return;
        }

        // 参考在 Arena::ApplyActionMove() 里阻塞着把这一步走完才改位置（battle_interface.cpp:4735-4830）；
        // 端口先播完动画，播完再由 Update 调 ApplyTurn 把命令交给竞技场。
        if (_painter is not null
            && commands is [BattleCommand move]
            && move.Type == BattleCommandType.Move
            && _painter.StartMovement(unit, move[0]))
        {
            HoldForDrawing(unit, commands, statusLine);

            return;
        }

        // 攻击的起手也赶在伤害结算之前播（battle_action.cpp:198-203）：受击与死亡要等 ApplyTurn
        // 之后才知道结果，由 _pendingReactions 补上（battle_action.cpp:206-210）。
        if (_painter is not null
            && commands is [BattleCommand attack]
            && attack.Type == BattleCommandType.Attack
            && _arena.GetTroopUID((uint)attack[3]) is { } defender
            && _painter.StartAttackSwing(unit, defender))
        {
            HoldForDrawing(unit, commands, statusLine);

            return;
        }

        ApplyTurn(unit, commands, statusLine);
    }

    /// <summary>
    /// Defers a command until the drawing it started is over, <c>Interface::_movingUnit</c>'s role in the port.
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <param name="commands">What it does.</param>
    /// <param name="statusLine">The upper status line to show once it is applied.</param>
    private void HoldForDrawing(BattleUnit unit, List<BattleCommand> commands, string statusLine)
    {
        _pendingAction = (unit, commands, statusLine);

        // The pointer no longer decides anything while the action plays (battle_interface.cpp:3207-3219).
        _painter?.SetHoveredCell(null);
        _painter?.SetMovementAreaUnit(null);
    }

    /// <summary>
    /// Applies a player's commands and writes the status line, <c>Interface::ApplyAction()</c> once the
    /// march (if any) has been drawn.
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <param name="commands">What it does.</param>
    /// <param name="statusLine">The upper status line to show.</param>
    private void ApplyTurn(BattleUnit unit, List<BattleCommand> commands, string statusLine)
    {
        bool isAttack = commands is [BattleCommand attack] && attack.Type == BattleCommandType.Attack;

        _arena.PlayCurrentUnitTurn(commands);

        // 结算顺带把这一击打到了谁、掉多少血记了下来，受击与死亡动画照它播（见 BattleAttackBlow）。
        if (isAttack && _arena.LastAttackBlows.Count > 0)
        {
            _pendingReactions = [.. _arena.LastAttackBlows];
        }

        // The upper status line reports the action and pushes it to the battle log.
        _painter?.SetStatus(statusLine, top: true);

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
        // Parameters are stored in reverse: SpellCast keeps the target first and the spell id last.
        BattleCommandType.Spellcast => $"Cast {new Spell((SpellType)command[command.Count - 1]).GetName()}",
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

        // 参考每帧先把 _highlightUnitMovementArea 清空，再由 GetBattleCursor() 设成鼠标下的兵
        // （battle_interface.cpp:3219、2940），开关是「highlight movement area」。
        _painter.SetMovementAreaUnit(
            Settings.Get().IsBattleMovementAreaHighlight && hovered is { } highlightedCell
                ? _arena.Board.GetCell(highlightedCell)?.Unit
                : null);

        // 瞄准法术时不再算走/打，光标与提示由这一支独占（参考的 humanturn_spell 分支）。
        if (_pendingSpell.Type != SpellType.None)
        {
            UpdateSpellHover(hovered);

            return;
        }

        if (hovered != _lastHoverCell || cursor != _lastHoverCursor)
        {
            _lastHoverCell = hovered;
            _lastHoverCursor = cursor;
            UpdateCursorTheme(hovered, cursor);
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
    /// Picks the cursor theme and the hint of the hovered cell, <c>Interface::GetBattleCursor()</c>
    /// (<c>battle_interface.cpp:2922-3064</c>).
    /// </summary>
    /// <param name="cell">The hovered cell, or <see langword="null"/>.</param>
    /// <param name="cursor">The pointer in the coordinates of the board cells.</param>
    /// <remarks>
    /// 判定本身在 <see cref="BattleCursor.Describe"/> 里，这里只负责喂竞技场、把结果交给光标与状态行。
    /// 指针不在盘上（或在盘上而这一帧没有可做的事）时留下普通的战场箭头。
    /// </remarks>
    private void UpdateCursorTheme(int? cell, Point cursor)
    {
        CursorTheme theme = CursorTheme.CombatPointer;
        string hint = string.Empty;

        if (_arena.CurrentUnit is { } unit && cell is { } index && IsHumanUnit(unit))
        {
            BattleHover hover = BattleCursor.Describe(_arena, unit, index, cursor);

            theme = hover.Theme;
            hint = hover.Status;
        }

        CursorState.Current?.SetThemes(theme);

        // The lower status line carries the cursor hint, the reference's setStatus( msg, false ).
        _painter?.SetStatus(hint, top: false);
    }

    /// <summary>
    /// Opens the spell book of the acting unit, the <c>cast</c> branch of the reference's turn menu
    /// (<c>battle_interface.cpp:7295-7333</c>).
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <returns><see langword="true"/> when the book opened.</returns>
    /// <remarks>
    /// 参考的三道门按原顺序：有没有指挥官、有没有魔法书、现在能不能施法。没有指挥官时它什么都不弹
    /// （<c>if ( hero )</c>），端口照旧只写日志；后两道门用模态框弹「没有魔法书」与竞技场给出的拒施理由。
    /// </remarks>
    private bool OpenSpellBook(BattleUnit unit)
    {
        if (_spellBook is not null || _pendingSpell.Type != SpellType.None)
        {
            return false;
        }

        if (unit.GetCurrentOrArmyCommander() is not ISpellBookOwner owner)
        {
            GameLog.Info("The acting unit has no commander, so it cannot open a spell book.");

            return false;
        }

        if (!owner.HasSpellBook)
        {
            ShowMessageBox(string.Empty, SpellBookWindow.NoMagicBookMessage, null);

            return false;
        }

        if (_arena.IsDisableCastSpell(new Spell(SpellType.None), out string? reason))
        {
            ShowMessageBox(string.Empty, reason ?? string.Empty, null);

            return false;
        }

        if (_spellBookArtwork is not { } artwork || _spellBookFonts is not { } fonts)
        {
            GameLog.Warn("The spell book has no artwork, so it cannot open.");

            return false;
        }

        // 参考：CMBT 页、允许施放、保留上次翻到的页（restorePreviousState）。
        SpellBookWindow? book = SpellBookWindow.TryOpen(owner, _spellBookState, artwork, fonts,
            SpellBookFilter.Combat, canCastSpell: true, restorePreviousState: true,
            new Size(ScreenBuffer.DefaultWidth, ScreenBuffer.DefaultHeight));

        if (book is null)
        {
            // TryOpen 只在「没有魔法书」与「书里没有可施放的法术」两种情况下放弃。
            ShowMessageBox(string.Empty, SpellBookWindow.NoSpellToCastMessage, null);

            return false;
        }

        _spellBook = book;
        _lastHoverCell = null;

        // 书打开期间战场不重算光标，这里先把箭头指针放好。
        CursorState.Current?.SetThemes(CursorTheme.CombatPointer);

        return true;
    }

    /// <summary>
    /// The frame of an open spell book, the body of <c>SpellBook::Open()</c>'s message loop.
    /// </summary>
    /// <param name="events">Input state of this frame.</param>
    /// <remarks>
    /// <para>
    /// 书的提示写在状态行（参考的 <c>statusCallback</c>）；选中的法术接着走 <see cref="StartCasting"/>。
    /// </para>
    /// <para>
    /// 另外两句提示与法术说明按参考开消息框（<c>spell_book.cpp:326</c>、<c>:592-604</c>）：左键点开的是
    /// <c>Dialog::OK</c>（点一下收起），右键点开的是 <c>Dialog::ZERO</c>（按住右键期间显示）。
    /// </para>
    /// </remarks>
    private void UpdateSpellBook(LocalEvent events)
    {
        if (_spellBook is not { } book)
        {
            return;
        }

        SpellBookHotKeys hotKeys = new(
            CloseWindow: GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel),
            Cancel: GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel),
            PreviousPage: GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultLeft),
            NextPage: GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultRight));

        SpellBookFrame frame = book.ProcessEvents(events, hotKeys);

        if (!string.IsNullOrEmpty(book.StatusText))
        {
            _painter?.SetStatus(book.StatusText, top: false);
        }

        switch (frame.Action)
        {
            case SpellBookAction.Select:
                _spellBook = null;
                StartCasting(frame.Spell);
                break;

            case SpellBookAction.Close:
                _spellBook = null;
                _lastHoverCell = null;
                CursorState.Current?.SetThemes(CursorTheme.CombatPointer);
                _painter?.SetStatus(string.Empty, top: false);
                break;

            // 提示的标题只有「法术太贵」那一句是法术名，其余（法力值、页签、翻页）都是空标题。
            case SpellBookAction.ShowMessage:
                ShowTextMessage(frame.Spell == SpellType.None ? string.Empty : new Spell(frame.Spell).GetName(),
                    frame.Text, frame.ByRightClick);
                break;

            case SpellBookAction.ShowSpell:
                ShowSpellInfo(frame.Spell, frame.ByRightClick);
                break;
        }
    }

    /// <summary>
    /// Begins casting a spell picked in the book, the tail of the reference's <c>cast</c> branch
    /// (<c>battle_interface.cpp:7316-7345</c>).
    /// </summary>
    /// <param name="type">The spell picked in the book.</param>
    /// <remarks>
    /// 不需要目标的法术（群体法术、地震、招元素…）在参考里当场就落，目标参数给 <c>-1</c>；其余的进入瞄准态，
    /// 由 <see cref="UpdateSpellHover"/> 与 <see cref="TryPlayPendingSpell"/> 接着处理。
    /// </remarks>
    private void StartCasting(SpellType type)
    {
        _lastHoverCell = null;

        if (_arena.CurrentUnit is not { } unit)
        {
            return;
        }

        Spell spell = new(type);

        if (_arena.IsDisableCastSpell(spell, out string? reason))
        {
            ShowMessageBox(spell.GetName(), reason ?? string.Empty, null);

            return;
        }

        if (unit.GetCurrentOrArmyCommander() is not ISpellBookOwner owner)
        {
            GameLog.Info("The acting unit has no commander, so it cannot cast a spell.");

            return;
        }

        if (!owner.CanCastSpell(spell))
        {
            ShowMessageBox(ErrorTitle, SpellBookWindow.DescribeCantCast(owner, spell), null);

            return;
        }

        if (spell.IsApplyWithoutFocusObject())
        {
            PlayCommands(unit, [BattleCommand.SpellCast(type, -1)]);

            return;
        }

        _pendingSpell = spell;
        _teleportSourceCell = BattleSpellHighlight.NoCell;

        GameLog.Info($"The player aims {spell.GetName()}.");

        UpdateSpellHover(null);
    }

    /// <summary>
    /// The cursor and the hint of an aim, <c>Interface::GetBattleSpellCursor()</c>
    /// (<c>battle_interface.cpp:3066-3117</c>) called from <see cref="UpdateHover"/>.
    /// </summary>
    /// <param name="hovered">The cell under the pointer, or <see langword="null"/>.</param>
    private void UpdateSpellHover(int? hovered)
    {
        BattleSpellHover hover;

        if (_arena.CurrentUnit is { } unit && hovered is { } index)
        {
            hover = BattleSpellCursor.Describe(_arena, unit, _pendingSpell, index, _teleportSourceCell);
        }
        else
        {
            hover = new BattleSpellHover(CursorTheme.CombatNone, BattleSpellCursor.SelectTargetStatus);
        }

        CursorState.Current?.SetThemes(hover.Theme);

        if (_painter is { } painter)
        {
            painter.PendingSpell = _pendingSpell;
            painter.TeleportSourceCell = _teleportSourceCell;
            painter.CursorTheme = hover.Theme;
            painter.SetStatus(hover.Status, top: false);
        }
    }

    /// <summary>
    /// Turns the clicks of an aim into the cast command, <c>Interface::HumanCastSpellTurn()</c>
    /// (<c>battle_interface.cpp:3531-3630</c>).
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <param name="events">Input state of this frame.</param>
    /// <returns><see langword="true"/> when the spell was cast.</returns>
    /// <remarks>
    /// 参考只有在光标表示「这一格能落」时才落子（<c>WAR_NONE</c> 的点击被丢掉）；传送要点两次，第一次选起点
    /// 且不消耗回合。右键或 Esc 取消瞄准，法术不消耗。
    /// </remarks>
    private bool TryPlayPendingSpell(BattleUnit unit, LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(BattleFieldPainter.BattlefieldArea) ||
            GameHotKeys.HotKeyPressEvent(HotKeyEvent.DefaultCancel))
        {
            CancelPendingSpell();

            return false;
        }

        if (!events.MouseClickLeft(BattleFieldPainter.BattlefieldArea))
        {
            return false;
        }

        int cell = BattleBoard.GetIndexAbsPosition(_arena.Board, events.CursorPosition);

        if (!BattleBoard.IsValidIndex(cell))
        {
            return false;
        }

        BattleSpellHover hover = BattleSpellCursor.Describe(_arena, unit, _pendingSpell, cell, _teleportSourceCell);

        if (hover.Theme == CursorTheme.CombatNone)
        {
            return false;
        }

        if (_pendingSpell.Type == SpellType.Teleport && _teleportSourceCell == BattleSpellHighlight.NoCell)
        {
            _teleportSourceCell = cell;

            UpdateSpellHover(cell);

            return false;
        }

        List<BattleCommand> commands = _pendingSpell.Type == SpellType.Teleport
            ? [BattleCommand.SpellCast(SpellType.Teleport, _teleportSourceCell, cell)]
            : [BattleCommand.SpellCast(_pendingSpell.Type, cell)];

        CancelPendingSpell();

        PlayCommands(unit, commands);

        return true;
    }

    /// <summary>Drops the aim, the <c>humanturn_spell = Spell::NONE</c> of a cancel or a cast.</summary>
    private void CancelPendingSpell()
    {
        _pendingSpell = new Spell(SpellType.None);
        _teleportSourceCell = BattleSpellHighlight.NoCell;
        _lastHoverCell = null;

        if (_painter is { } painter)
        {
            painter.PendingSpell = _pendingSpell;
            painter.TeleportSourceCell = _teleportSourceCell;
        }

        CursorState.Current?.SetThemes(CursorTheme.CombatPointer);
    }

    /// <summary>
    /// Asks whether the player really wants to retreat, <c>HotKeyEvent::BattleRetreat</c> of
    /// <c>Interface::HumanBattleTurn()</c> (<c>battle_interface.cpp:3250-3256</c>).
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <remarks>
    /// 参考先问 <c>CanRetreatOpponent()</c>，不能撤退时弹一句「Retreat disabled」；能时弹 YES/NO 的
    /// 「Are you sure you want to retreat?」，YES 才把 <c>Command::RETREAT</c> 交给竞技场
    /// （<c>ProcessingHeroDialogResult()</c>，<c>:7366-7374</c>）。
    /// </remarks>
    private void AskRetreat(BattleUnit unit)
    {
        if (!_arena.CanRetreatOpponent(_arena.GetCurrentColor()))
        {
            ShowMessageBox("", "Retreat disabled", null);

            return;
        }

        ShowMessageBox("", "Are you sure you want to retreat?", answer =>
        {
            if (answer == DialogButton.Yes)
            {
                PlayCommands(unit, [BattleCommand.Retreat()]);
            }
        });
    }

    /// <summary>
    /// Offers the enemy's terms, <c>HotKeyEvent::BattleSurrender</c> of
    /// <c>Interface::HumanBattleTurn()</c> (<c>battle_interface.cpp:3258-3266</c>).
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <remarks>
    /// 参考同样先问 <c>CanSurrenderOpponent()</c>，不能时弹「Surrender disabled」；能时打开
    /// <c>DialogBattleSurrender( kingdom, *getEnemyCommander( color ), cost )</c>，窗口里 ACCEPT 才发
    /// <c>Command::SURRENDER</c>。敌人不是英雄（例如守城的只是驻军）时参考会解空指针，
    /// 端口改成与「不能投降」同一句话，免得为了复刻崩溃而崩。
    /// </remarks>
    private void AskSurrender(BattleUnit unit)
    {
        PlayerColor color = _arena.GetCurrentColor();

        if (!_arena.CanSurrenderOpponent(color) || _arena.GetEnemyCommander(color) is not Hero enemy)
        {
            ShowMessageBox("", "Surrender disabled", null);

            return;
        }

        if (_sprites is null || _whiteFont is null || _smallWhiteFont is null)
        {
            GameLog.Warn("The surrender offer cannot be drawn without the battle artwork.");

            return;
        }

        _surrender = BattleSurrenderWindow.TryCreate(_sprites, enemy,
            _arena.GetCurrentForce().GetSurrenderCost(), _world.Kingdoms.GetKingdom(color),
            _whiteFont, _smallWhiteFont, _frame.Size);

        _surrenderUnit = unit;

        if (_surrender is null)
        {
            GameLog.Warn("The surrender offer cannot be drawn: SURDRBKG or SURRENDR is missing.");
        }
    }

    /// <summary>Drives the open offer for one frame, <c>while ( le.HandleEvents() &amp;&amp; !result )</c>.</summary>
    /// <param name="surrender">The open window.</param>
    /// <param name="events">Input state of the frame.</param>
    private void UpdateSurrender(BattleSurrenderWindow surrender, LocalEvent events)
    {
        switch (surrender.ProcessEvents(events))
        {
            case BattleSurrenderAnswer.Accept:
                _surrender = null;

                if (_surrenderUnit is { } accepted)
                {
                    _surrenderUnit = null;
                    PlayCommands(accepted, [BattleCommand.Surrender()]);
                }

                break;

            case BattleSurrenderAnswer.Decline:
                _surrender = null;
                _surrenderUnit = null;
                break;

            case BattleSurrenderAnswer.Market:
                OpenSurrenderMarketplace();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Opens the marketplace behind the MARKET button, <c>Dialog::Marketplace( kingdom, true )</c> of
    /// <c>DialogBattleSurrender()</c> (<c>battle_dialogs.cpp:1103-1110</c>).
    /// </summary>
    private void OpenSurrenderMarketplace()
    {
        if (_assets is null || _fonts is null)
        {
            GameLog.Warn("The marketplace cannot be opened without the loaded resources.");

            return;
        }

        PlayerColor color = _arena.GetCurrentColor();
        _marketplace = MarketplaceWindow.TryCreate(_assets, _world.Kingdoms.GetKingdom(color), ActivePlayers(), _fonts);
    }

    /// <summary>The number of kingdoms still playing, <c>world.CountPlayers()</c> of the reference.</summary>
    /// <returns>How many kingdoms are neither out nor uncolored.</returns>
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

    /// <summary>
    /// Opens a text box and remembers what to do with its answer, the reference's blocking
    /// <c>Dialog::Message( "", text, Dialog::YES | Dialog::NO )</c>.
    /// </summary>
    /// <param name="header">Title line of the box, usually empty in the battle.</param>
    /// <param name="body">The sentence to show.</param>
    /// <param name="answer">What to do with the pressed button, or <see langword="null"/> for an OK-only box.</param>
    /// <param name="elements">Elements between the title and the text; empty or <see langword="null"/> for none.</param>
    /// <remarks>
    /// 没有按钮美术（数据不全）时退回状态行加日志，和冒险地图各处的做法一致，至少玩家能看到这句话。
    /// </remarks>
    private void ShowMessageBox(string header, string body, Action<DialogButton>? answer,
        IReadOnlyList<DialogElement>? elements = null)
    {
        GameLog.Info($"Battle message: {body}");

        if (_systemButtons is null || _whiteFont is null || _smallWhiteFont is null)
        {
            _painter?.SetStatus(body, top: true);

            return;
        }

        DialogButton buttons = answer is null ? DialogButton.Ok : DialogButton.Yes | DialogButton.No;

        _message = MessageBox.Create(header, body, _whiteFont, _smallWhiteFont, elements,
            buttons: buttons, buttonArtwork: _systemButtons, screen: _frame.Size);

        _messageAnswer = answer;
        _messageHeldByRightButton = false;
    }

    /// <summary>
    /// Opens a box whose lifetime is the press of the right button, the reference's
    /// <c>showStandardTextMessage( "", text, Dialog::ZERO )</c> of the spell book's right-click hints.
    /// </summary>
    /// <param name="header">Title line of the box.</param>
    /// <param name="body">The sentence to show.</param>
    /// <param name="elements">Elements under the text; empty or <see langword="null"/> for none.</param>
    /// <remarks>
    /// 没有按钮美术时退回状态行加日志，与 <see cref="ShowMessageBox"/> 一致；这样右键提示至少还看得见。
    /// </remarks>
    private void ShowHeldMessageBox(string header, string body, IReadOnlyList<DialogElement>? elements = null)
    {
        GameLog.Info($"Battle message: {body}");

        if (_whiteFont is null || _smallWhiteFont is null)
        {
            _painter?.SetStatus(body, top: true);

            return;
        }

        _message = MessageBox.Create(header, body, _whiteFont, _smallWhiteFont, elements, screen: _frame.Size);
        _messageAnswer = null;
        _messageHeldByRightButton = true;
    }

    /// <summary>
    /// Shows a sentence of the spell book: a held box for a right click, an OK box for a left click.
    /// </summary>
    /// <param name="header">Title line of the box.</param>
    /// <param name="body">The sentence to show.</param>
    /// <param name="byRightClick">Whether the sentence came from a right click, i.e. <c>Dialog::ZERO</c>.</param>
    private void ShowTextMessage(string header, string body, bool byRightClick,
        IReadOnlyList<DialogElement>? elements = null)
    {
        if (byRightClick)
        {
            ShowHeldMessageBox(header, body, elements);
        }
        else
        {
            ShowMessageBox(header, body, null, elements);
        }
    }

    /// <summary>
    /// Shows the description of a spell of the open book, the reference's
    /// <c>fheroes2::SpellDialogElement( spell, hero ).showPopup( buttons )</c> (<c>spell_book.cpp:326</c>,
    /// <c>:592-604</c>).
    /// </summary>
    /// <param name="type">The spell to describe.</param>
    /// <param name="byRightClick">Whether the box came from a right click, i.e. <c>Dialog::ZERO</c>.</param>
    /// <remarks>
    /// 正文是 <see cref="SpellInfo.GetDescription"/>，与参考的 <c>getSpellDescription( spell, hero )</c> 同源，
    /// 只对施法者代入数值；法术图标与花费由 <see cref="SpellDialogElement"/> 画在正文上方。施法者从竞技场
    /// 当前的行动单位取——书只能替它打开。
    /// </remarks>
    private void ShowSpellInfo(SpellType type, bool byRightClick)
    {
        Spell spell = new(type);
        ISpellBookOwner? owner = _arena.CurrentUnit?.GetCurrentOrArmyCommander() as ISpellBookOwner;

        string description = SpellInfo.GetDescription(spell, owner, _world);

        List<DialogElement> elements = [];

        if (_spellBookArtwork is { } artwork)
        {
            uint icon = spell.IndexSprite();

            if (icon < (uint)artwork.SpellIcons.Count &&
                (_smallWhiteFont ?? _whiteFont) is { } font)
            {
                elements.Add(new SpellDialogElement(artwork.SpellIcons[(int)icon], spell,
                    owner is null ? 0 : spell.SpellPoints(owner.BagArtifacts), font));
            }
        }

        ShowTextMessage(spell.GetName(), description, byRightClick, elements);
    }

    /// <summary>
    /// Logs the settled result once and opens the result window, <c>Battle::Loader</c>'s
    /// <c>DialogBattleSummary( res, artifacts, allowToRestart )</c> (<c>battle_main.cpp:390-401</c>).
    /// </summary>
    /// <remarks>
    /// 无人值守（<c>--battle-auto</c>）不开窗：参考那边 <c>isHumanBattle</c> 为假时也不开，冒烟跑到这里要能自己
    /// 往下一帧走。没有美术时同样不开，只留日志。
    /// </remarks>
    private void Finish()
    {
        _pendingAction = null;
        _pendingReactions = null;
        _painter?.CancelMovement();
        _painter?.CancelAttack();

        SettleRound();

        // presented 时这一屏不结算：轮次由 BattleLoader 收尾，结果窗也由它的 presenter 那一头开
        // （ShowSummary），这里只把自己关掉。
        if (_presented)
        {
            Close();

            return;
        }

        BattleResult result = _arena.Result;
        string outcome = result.IsAttackerWin
            ? "the attacker won"
            : result.IsDefenderWin
                ? "the defender won"
                : "nobody won";

        GameLog.Info($"The battle is over: attacker '{result.Attacker}', defender '{result.Defender}', {outcome}.");

        if (_autoPlay || !TryOpenSummary(result))
        {
            Close();
        }
    }

    /// <summary>
    /// Opens the result window of the fight, the port of <c>DialogBattleSummary()</c> as called by
    /// <c>Battle::Loader</c>.
    /// </summary>
    /// <param name="result">What the arena settled on.</param>
    /// <returns>Whether a window was opened; the caller leaves the screen when it was not.</returns>
    /// <remarks>
    /// <para>
    /// 视角永远是攻方（人类英雄）：赢了用黄字标题、赔款写守军的买回价，输了不写赔款——这正是参考
    /// <c>attackerIsHuman</c> 那一支（<c>battle_dialogs.cpp:508-527</c>）。
    /// </para>
    /// <para>
    /// <c>allowToRestart</c> 传假：参考的「重开」只在战斗没显示过（自动结算）时给，而这个窗本身就是显示着的。
    /// 战败方是游荡怪或城镇守军，没有英雄也就没有神器可交接，<see cref="BattleSummaryWindow.BeginArtifacts"/>
    /// 会立刻返回假。
    /// </para>
    /// </remarks>
    /// <summary>
    /// Opens the result window of a fight that is already over, the <c>DialogBattleSummary()</c> the reference runs
    /// once <c>Arena::Turns()</c> returns (<c>battle_main.cpp:404-410</c>).
    /// </summary>
    /// <param name="result">What the arena settled on.</param>
    /// <returns>Whether a window was opened; the caller has nothing to wait for when it was not.</returns>
    /// <remarks>
    /// presented 时 <see cref="BattleLoader.Settle"/> 负责收尾，它通过 presenter 的
    /// <see cref="IBattlePresenter.ShowSummary"/> 回到这里开窗——这一屏的图与字都在手里，不必重取一遍。
    /// </remarks>
    public bool OpenSummary(BattleResult result) => TryOpenSummary(result);

    private bool TryOpenSummary(BattleResult result)
    {
        if (_sprites is null || _miniMonsters is null || _settingsOkay is null || _settingsOkayPressed is null)
        {
            return false;
        }

        bool attackerWins = result.IsAttackerWin;

        BattleSummaryText text = BattleSummaryText.Create(
            result.Attacker,
            result.Defender,
            _attacker.Name,
            result.AttackerExperience,
            attackerWins ? _arena.DefendingForce.GetSurrenderCost() : 0);

        BattleSummaryWindow? window = BattleSummaryWindow.TryCreate(
            _sprites,
            _miniMonsters,
            text,
            _arena.AttackingForce.GetKilledTroops(),
            _arena.DefendingForce.GetKilledTroops(),
            highlightTitle: attackerWins,
            allowToRestart: false,
            _settingsOkay,
            _settingsOkayPressed,
            restartReleased: null,
            restartPressed: null,
            screen: _frame.Size);

        if (window is null)
        {
            return false;
        }

        _summary = window;

        return true;
    }

    /// <summary>
    /// Feeds one frame of input to the result window, <c>DialogBattleSummary()</c>'s modal loop.
    /// </summary>
    /// <param name="summary">The window that is open.</param>
    /// <param name="events">Input state of the frame.</param>
    private void UpdateSummary(BattleSummaryWindow summary, LocalEvent events)
    {
        summary.Update();

        if (summary.IsShowingArtifacts)
        {
            if (summary.ProcessArtifactEvents(events))
            {
                _summary = null;
                Close();
            }

            return;
        }

        switch (summary.ProcessEvents(events))
        {
            case BattleSummaryWindowAction.Close:
                // 守方没有英雄，神器段是空的，BeginArtifacts 立刻答假，窗口就此关掉。
                if (!summary.BeginArtifacts([], winnerIsHuman: true))
                {
                    _summary = null;
                    Close();
                }

                break;

            case BattleSummaryWindowAction.Restart:
                // allowToRestart 为假时不会走到这里；真重开要重建竞技场，留给冒险→战斗那条链。
                break;
        }
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

        // 魔法书画在战场之上（参考里书的 StandardWindow 直接画到屏幕上）。
        _spellBook?.Draw(_frame.Pixels);

        // 设置窗画在战场之上（参考里它自己的 StandardWindow 直接画到屏幕上）。
        _settings?.Draw(_frame.Pixels);

        // 战后结果窗盖住整个战场（参考的 StandardWindow 直接画到屏幕上）。
        _summary?.Draw(_frame.Pixels);

        // 投降报价、从它点进去的市场、以及最后盖在最上面的消息框。
        _surrender?.Draw(_frame.Pixels);
        _marketplace?.Draw(_frame.Pixels);
        _message?.Draw(_frame.Pixels);

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

