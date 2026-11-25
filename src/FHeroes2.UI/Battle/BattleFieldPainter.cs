using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

using ArmyText = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The static layers of the battle screen, the drawing half of <c>Battle::Interface</c>
/// (<c>battle_interface.cpp:2269-2908</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>范围</b>：这一步只画「不会自己动」的东西——地形底图与边框杂物、地表装饰、地图带进来的障碍、
/// 攻城时的城堡地面/护城河/城墙/塔/投石车，以及双方摆好阵形的单位（各取待机动画的第一帧）、单位的血量条；
/// 此外还有战场顶上的行动顺序条（<see cref="BattleTurnOrderLayout"/>）、状态栏的两行文字与可展开的
/// 6 行战斗日志窗（<see cref="BattleStatusLog"/>）。
/// 行动交互的其余部分与所有法术特效都还没有，见 <c>bd list</c>。
/// </para>
/// <para>
/// <b>坐标系</b>：参考把战场画进 640x443 的 <c>_mainSurface</c>，底下的 37 行留给状态栏
/// （<c>TEXTBAR</c> 的 8 号 20 行加 9 号 17 行）；格子按 <c>_surfaceInnerArea</c>（整块 640x480）摆位，
/// 所以最后一行单位的身子会被裁掉——参考本来就是这么裁的。端口照抄：格子布局按 640x480 算，
/// 画布只有 640x443。
/// </para>
/// <para>
/// <b>绘制次序</b>（<c>fullRedraw()</c> → <c>_redrawBattleGround()</c> → <c>RedrawCover()</c> →
/// <c>RedrawArmies()</c> → <c>redrawPreRender()</c>）：底图 → 边框杂物 → 地表装饰 → 城堡地面/护城河 →
/// 地表障碍 → 城堡顶墙；然后逐行画高障碍、城墙与单位；最后把战场贴到屏幕上，再画状态栏与三个按钮的底。
/// </para>
/// <para>
/// <b>与参考的偏差</b>：顺序条只画「战场窗口内、顶边」这一档（<c>BattleTurnOrderState.Bottom</c> 等同不画），
/// 且当前/悬停单位用固定的 110 号色而不是 110→126 的循环闪光；六角网格照 <c>Settings::BattleShowGrid</c>
/// 开关（战斗设置窗里的 Grid 一档）。
/// </para>
/// </remarks>
public sealed class BattleFieldPainter
{
    /// <summary>Width of the battlefield surface, <c>_surfaceInnerArea.width</c>.</summary>
    public const int Width = ScreenBuffer.DefaultWidth;

    /// <summary>
    /// Height of the battlefield surface: the display minus the status bar, which is
    /// <c>TEXTBAR</c> frame 8 (20 rows) plus frame 9 (17 rows).
    /// </summary>
    public const int Height = 443;

    /// <summary>Rows the status bar and its buttons take at the bottom of the screen.</summary>
    public const int StatusHeight = ScreenBuffer.DefaultHeight - Height;

    /// <summary>Left edge of the status bar, the width of the settings button column (49).</summary>
    private const int StatusLeft = 49;

    /// <summary>Vertical shift of every board anchored sprite, <c>cellYOffset</c> (<c>battle_interface.cpp:91</c>).</summary>
    private const int CellYOffset = BattleTroopSprite.CellYOffset;

    /// <summary>Height of <c>TEXTBAR</c> frame 8, the upper half of the status bar.</summary>
    private const int StatusUpperHeight = 20;

    /// <summary>Inset of the status text inside the bar, <c>offsetForTextBar</c> (<c>battle_interface.cpp:105</c>).</summary>
    private const int StatusTextInset = 32;

    /// <summary>Width of <c>TEXTBAR</c> frame 8, the status bar both lines are centred in.</summary>
    private const int StatusWidth = 543;

    private readonly BattleArena _arena;
    private readonly IViewWorldSpriteSource _sprites;
    private readonly MonsterAnimInfoCache _monsterInfo;
    private readonly IGlyphSource? _counterFont;
    private readonly IGlyphSource? _statusFont;
    private readonly BattleFieldStyle _style;
    private readonly Palette? _palette;
    private readonly Dictionary<uint, UnitAnimation> _animations = [];

    /// <summary>The green of the hit point bars, resolved from the palette on first use.</summary>
    private byte? _remainingHitPointsColor;

    /// <summary>The red of the hit point bars, resolved from the palette on first use.</summary>
    private byte? _missingHitPointsColor;

    /// <summary>
    /// The frame colour of the stack acting right now, <c>_contourColor</c>'s first value
    /// (<c>battle_interface.cpp:4568</c>).
    /// </summary>
    private const byte TurnOrderContourColor = 110;

    /// <summary><c>MONS32</c> icons of the turn order bar, keyed by creature, mirror image and facing.</summary>
    private readonly Dictionary<int, Sprite> _turnOrderIcons = [];

    private readonly BattleStatusLog _statusLog;
    private readonly BattleStatusButtons _buttons;
    private readonly PixelBuffer _ground = new(Width, Height);
    private readonly PixelBuffer _surface = new(Width, Height);

    /// <summary>The hero or captain of the attacking side, <c>Interface::_attackingOpponent</c>.</summary>
    private readonly BattleOpponentSprite? _attackingOpponent;

    /// <summary>The hero or captain of the defending side, <c>Interface::_defendingOpponent</c>.</summary>
    private readonly BattleOpponentSprite? _defendingOpponent;

    /// <summary>Counter of the waving flags, <c>Interface::_flagAnimationFrameIndex</c>.</summary>
    private uint _flagAnimationFrameIndex;

    /// <summary>Mirrored flag frames, keyed by archive and frame, so a side is flipped once.</summary>
    private readonly Dictionary<(IcnType Icn, int Frame), Sprite> _flippedFlags = [];

    /// <summary>The upper status line, <c>Status::_upperText</c>.</summary>
    private string _statusUpper = string.Empty;

    /// <summary>The lower status line, <c>Status::_lowerText</c>.</summary>
    private string _statusLower = string.Empty;

    /// <summary>The last lower message, <c>Status::_lastMessage</c>, so an unchanged hint is not refitted.</summary>
    private string _lastStatusLower = string.Empty;

    /// <summary>The hexagon drawn over every cell when the grid is on, <c>Interface::_hexagonGrid</c>.</summary>
    private Sprite? _hexagonGrid;

    /// <summary>The shadow under the cell the cursor is on, <c>Interface::_hexagonCursorShadow</c>.</summary>
    private Sprite? _hexagonCursorShadow;

    /// <summary>The shadow under every cell the current unit may walk to, <c>Interface::_hexagonShadow</c>.</summary>
    private Sprite? _hexagonShadow;

    /// <summary>That same shadow with the grid inside it, <c>Interface::_hexagonGridShadow</c>.</summary>
    private Sprite? _hexagonGridShadow;

    /// <summary>The doubled shadow of a unit the mouse rests on, <c>Interface::_hexagonHighlightShadow</c>.</summary>
    private Sprite? _hexagonHighlightShadow;

    /// <summary>The cell the pointer is over, or <see langword="null"/> when it is off the board.</summary>
    private int? _hoveredCell;

    /// <summary>
    /// The unit under the pointer whose walking range is shown, <c>Interface::_highlightUnitMovementArea</c>.
    /// </summary>
    private BattleUnit? _movementAreaUnit;

    /// <summary>
    /// The march being drawn right now, the frame-by-frame form of <c>Interface::_movingUnit</c>,
    /// <c>_movingPos</c>, <c>_flyingUnit</c> and <c>_flyingPos</c>.
    /// </summary>
    private MovePlayback? _move;

    /// <summary>
    /// The attack being drawn right now, <c>RedrawActionAttackPart1()</c>/<c>_redrawActionWincesKills()</c>
    /// (<c>battle_interface.cpp:4088-4439</c>).
    /// </summary>
    private AttackPlayback? _attack;

    /// <summary>
    /// The shot on its way, <c>Interface::_currentTroopRangedAttack</c>'s route walked one frame at a time
    /// (<c>battle_interface.cpp:1062-1136</c>).
    /// </summary>
    private MissilePlayback? _missile;

    /// <summary>
    /// The shot that belongs to the swing being drawn: it leaves only once the swing is over
    /// (<c>RedrawActionAttackPart1()</c> fires it after <c>AnimateUnitWithDelay( attacker, true )</c>, <c>:4148</c>).
    /// </summary>
    private MissilePlayback? _pendingMissile;

    /// <summary>
    /// Builds the painter of one arena, <c>Battle::Interface::Interface()</c>
    /// (<c>battle_interface.cpp:1380-1512</c>) up to the layers it draws.
    /// </summary>
    /// <param name="arena">The battle to draw.</param>
    /// <param name="sprites">Where the <c>ICN</c> frames come from.</param>
    /// <param name="monsterInfo">The <c>.BIN</c> records of the creatures, for their sprite frames.</param>
    /// <param name="style">Which background archives the ground of the battle picks.</param>
    /// <param name="counterFont">
    /// The font of the troop counters, <c>FontType::smallWhite()</c>; <see langword="null"/> leaves the numbers out.
    /// </param>
    /// <param name="palette">
    /// The game palette in video values, for the two colours the screen derives from it (the grid and the
    /// hit point bar); <see langword="null"/> leaves both out.
    /// </param>
    /// <param name="statusFont">
    /// The font of the two status lines and the battle log, <c>FontType::normalWhite()</c>;
    /// <see langword="null"/> draws the bar without text.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// 参考在竞技场的构造函数里问界面要战场区域（<c>battle_arena.cpp:366</c> 的
    /// <c>board.SetArea( _interface->GetArea() )</c>）：格子位置是界面的坐标系，所以摆放格子这件事落在
    /// 绘制这一侧。端口照做，区域取整块 640x480，超出 443 行的部分由裁剪处理。
    /// </remarks>
    public BattleFieldPainter(
        BattleArena arena,
        IViewWorldSpriteSource sprites,
        MonsterAnimInfoCache monsterInfo,
        BattleFieldStyle style,
        IGlyphSource? counterFont = null,
        Palette? palette = null,
        IGlyphSource? statusFont = null)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(monsterInfo);

        _arena = arena;
        _sprites = sprites;
        _monsterInfo = monsterInfo;
        _style = style;
        _counterFont = counterFont;
        _palette = palette;
        _statusFont = statusFont;

        _statusLog = new BattleStatusLog(_sprites, statusFont);

        // 参考在 Interface 的构造函数里把日志窗放在战场底边（area.y + battlefieldHeight），也就是状态栏上沿。
        _statusLog.SetPosition(0, Height);

        _buttons = new BattleStatusButtons(_sprites, ScreenBuffer.DefaultWidth, ScreenBuffer.DefaultHeight);

        // 参考按 `getAttackingArmyCommander()` 建两个 OpponentSprite，没有英雄/队长的一方就没有立绘。
        // 偏移取 `_interfacePosition`：屏幕 640x480 时它是 (0,0)，战场画布因此从 0 开始量。
        Point position = new((ScreenBuffer.DefaultWidth - Width) / 2, (ScreenBuffer.DefaultHeight - 480) / 2);

        if (_arena.AttackingArmyCommander is { } attacker)
        {
            _attackingOpponent = new BattleOpponentSprite(position, attacker, isFlipped: false, _sprites);
        }

        if (_arena.DefendingArmyCommander is { } defender)
        {
            _defendingOpponent = new BattleOpponentSprite(position, defender, isFlipped: true, _sprites);
        }

        _arena.Board.SetArea(new Rect(0, 0, ScreenBuffer.DefaultWidth, ScreenBuffer.DefaultHeight));
    }

    /// <summary>
    /// Sets the cell the pointer is over, the <c>_currentCellIndex</c> the hover shadow follows
    /// (<c>battle_interface.cpp:2285-2474</c>).
    /// </summary>
    /// <param name="cellIndex">The cell, or <see langword="null"/> when the pointer is off the board.</param>
    public void SetHoveredCell(int? cellIndex) => _hoveredCell = cellIndex;

    /// <summary>
    /// Sets the unit whose walking range is highlighted, <c>Interface::_highlightUnitMovementArea</c>
    /// (<c>battle_interface.cpp:2940</c>, cleared every frame at <c>:3219</c>).
    /// </summary>
    /// <param name="unit">The unit under the pointer, or <see langword="null"/> when there is none.</param>
    /// <remarks>
    /// 参考把它设成**鼠标下的兵**，不分敌我；真正决定画不画的是设置项「highlight movement area」。
    /// 场景每帧先清空再设，与参考的 `_highlightUnitMovementArea = nullptr` 同拍。
    /// </remarks>
    public void SetMovementAreaUnit(BattleUnit? unit) => _movementAreaUnit = unit;

    /// <summary>Whether a march is being drawn right now, <c>Interface::_movingUnit || _flyingUnit</c>.</summary>
    public bool IsMoving => _move is not null;

    /// <summary>
    /// Starts drawing the unit's march to a cell; the arena is untouched, the caller applies the command
    /// once <see cref="IsMoving"/> turns <see langword="false"/>.
    /// </summary>
    /// <param name="unit">The unit that would move.</param>
    /// <param name="destination">The head cell it would stop on.</param>
    /// <returns>
    /// <see langword="false"/> when there is nothing to draw (already there, no path), so the caller should
    /// apply the command right away.
    /// </returns>
    /// <remarks>
    /// 参考在 <c>Arena::ApplyActionMove()</c> 里阻塞着画完再改位置；端口反过来：先播，播完场景才把命令交给竞技场，
    /// 这样「已经站在终点」的画面与「还没动过」的模型能对上。
    /// </remarks>
    public bool StartMovement(BattleUnit unit, int destination)
    {
        ArgumentNullException.ThrowIfNull(unit);

        AnimationState state = GetAnimation(unit).State;
        BattleMoveAnimation? plan = BattleMoveAnimation.Create(_arena, unit, destination, state.FlightSpeed);

        if (plan is null)
        {
            return false;
        }

        _move = new MovePlayback(plan, state, _arena);

        if (!_move.IsFinished)
        {
            return true;
        }

        _move = null;

        return false;
    }

    /// <summary>Stops the march without finishing it, for a scene that is torn down mid-step.</summary>
    public void CancelMovement() => _move = null;

    /// <summary>Whether a march, an attack or a shot is being drawn right now.</summary>
    public bool IsAnimating => _move is not null || _attack is not null || _missile is not null;

    /// <summary>
    /// Starts drawing the attacker's swing, <c>RedrawActionAttackPart1()</c> (<c>battle_interface.cpp:4088-4175</c>).
    /// </summary>
    /// <param name="attacker">The unit that strikes.</param>
    /// <param name="defender">The unit it strikes.</param>
    /// <returns><see langword="false"/> when the creature has no attack sequence, so nothing is drawn.</returns>
    /// <remarks>
    /// 参考在结算伤害**之前**播这一段（<c>battle_action.cpp:198-203</c>），所以调用方先播、再把命令交给竞技场，
    /// 受击与死亡由 <see cref="StartAttackReactions"/> 在结算之后补上。
    /// </remarks>
    public bool StartAttackSwing(BattleUnit attacker, BattleUnit defender)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);

        bool isShooting = attacker.IsArchers && !BattleUnit.IsHandFighting(attacker, defender);

        // 参考只有在双击真的扫到两格时才改用 RANG_FRONT；那一格在结算前还定不下来，这里按「是双击」猜，
        // 画面上只影响前一半的姿势。
        MonsterAnimationType swing = BattleAttackAnimation.SelectSwing(
            attacker, defender, isShooting, attacker.IsDoubleAttack);

        AnimationState state = GetAnimation(attacker).State;

        if (!state.SwitchAnimation(swing))
        {
            return false;
        }

        _pendingMissile = isShooting ? CreateMissile(attacker, defender) : null;

        _attack = new AttackPlayback(
            [new BattleAttackStep(attacker, swing, isShooting), new BattleAttackStep(attacker, BattleAttackAnimation.SelectSwingEnd(swing), isShooting)],
            this,
            state);

        return true;
    }

    /// <summary>
    /// Draws what is left of an attack once the arena has applied it: the later blows' swings and every
    /// reaction (<c>RedrawActionAttackPart2()</c> and <c>_redrawActionWincesKills()</c>).
    /// </summary>
    /// <param name="blows">The blow list of the attack, <see cref="BattleArena.LastAttackBlows"/>.</param>
    public void StartAttackReactions(IReadOnlyList<BattleAttackBlow> blows)
    {
        ArgumentNullException.ThrowIfNull(blows);

        // 参考在受击与死亡动画之前先让双方英雄表态（:4331）；端口一次回放整段攻击，
        // 所以取最后一个死光的单位，等价于参考循环里最后一次赋值。
        SetHeroAnimationReactionToTroopDeath(SelectDeathColor(blows));

        IReadOnlyList<BattleAttackStep> steps = BattleAttackAnimation.CreateReplay(blows);

        if (steps.Count == 0)
        {
            return;
        }

        _attack = new AttackPlayback(steps, this, null);
    }

    /// <summary>The colour of the last army that lost a whole stack, <c>deathColor</c> of <c>_redrawActionWincesKills()</c>.</summary>
    /// <param name="blows">The blows of the attack.</param>
    /// <returns>That colour, or <see cref="PlayerColor.Unused"/> when nothing died.</returns>
    private static PlayerColor SelectDeathColor(IReadOnlyList<BattleAttackBlow> blows)
    {
        PlayerColor deathColor = PlayerColor.Unused;

        foreach (BattleAttackBlow blow in blows)
        {
            foreach (BattleDamageTarget target in blow.Targets)
            {
                if (!target.Defender.IsValid)
                {
                    deathColor = target.Defender.Color;
                }
            }
        }

        return deathColor;
    }

    /// <summary>Stops the attack drawing without finishing it, for a scene that is torn down mid-swing.</summary>
    public void CancelAttack()
    {
        _attack = null;
        _missile = null;
        _pendingMissile = null;
    }

    /// <summary>
    /// Lays out the shot of a shooting attack, <c>RedrawActionAttackPart1()</c>'s missile block
    /// (<c>battle_interface.cpp:4112-4156</c>).
    /// </summary>
    /// <param name="attacker">The shooter, whose swing is being drawn.</param>
    /// <param name="defender">The unit aimed at.</param>
    /// <returns>The shot to walk, or <see langword="null"/> when the sprites it needs are missing.</returns>
    private MissilePlayback? CreateMissile(BattleUnit attacker, BattleUnit defender)
    {
        AnimationState animation = GetAnimation(attacker).State;

        Sprite? attackerSprite = _sprites.GetSprite(MonsterIcnIds.GetSpriteIcn(attacker.Monster), animation.Frame);
        Sprite? defenderSprite = _sprites.GetSprite(
            MonsterIcnIds.GetSpriteIcn(defender.Monster), GetAnimation(defender).State.Frame);

        if (attackerSprite is null || defenderSprite is null)
        {
            return null;
        }

        // 同排算平射，参考比的是两边的行号（:4126-4128）。
        bool isSameRow = (attacker.HeadIndex / BattleBoard.WidthInCells) == (defender.HeadIndex / BattleBoard.WidthInCells);

        Point target = BattleTroopSprite.GetCenterPoint(defender, defenderSprite);
        BattleMissile.Aim aim = BattleMissile.AimShot(attacker, target, attackerSprite, animation, isSameRow);

        Sprite? projectile = _sprites.GetSprite(
            MonsterIcnIds.GetMissileIcn(attacker.Monster), animation.GetProjectileFrame(aim.Angle));

        // 法师的「弹道」是闪电，没有弹体：几何照样算，画的时候没有 sprite 可贴（见 ADR-227）。
        int width = projectile?.Width ?? 0;
        int height = projectile?.Height ?? 0;

        return new MissilePlayback(
            new BattleMissile(aim.Start, aim.Target, aim.Angle, attacker.Monster, width, height, projectile?.OffsetX ?? 0, projectile?.OffsetY ?? 0),
            projectile);
    }

    /// <summary>The battle log window, <c>Interface::listlog</c> (<c>battle_interface.cpp:803-980</c>).</summary>
    public BattleStatusLog StatusLog => _statusLog;

    /// <summary>The three status bar buttons, <c>Interface::_buttonAuto</c> and friends.</summary>
    public BattleStatusButtons Buttons => _buttons;

    /// <summary>
    /// The button held down this frame, <c>_buttonAuto.drawOnState()</c> and friends
    /// (<c>battle_interface.cpp:3222-3224</c>).
    /// </summary>
    public BattleStatusButton PressedButton { get; set; }

    /// <summary>
    /// The spell being aimed, <c>Interface::humanturn_spell</c>; <see cref="SpellType.None"/> when no human is
    /// aiming a spell right now.
    /// </summary>
    public Spell PendingSpell { get; set; } = new(SpellType.None);

    /// <summary>
    /// The source cell picked for a teleport, <c>Interface::_teleportSpellSrcIdx</c>; <c>-1</c> while no source has
    /// been picked.
    /// </summary>
    public int TeleportSourceCell { get; set; } = BattleSpellHighlight.NoCell;

    /// <summary>
    /// The cursor theme shown right now, <c>Cursor::Get().Themes()</c>; the teleport highlight reads it to tell
    /// "picking the source" from "picking the destination".
    /// </summary>
    public CursorTheme CursorTheme { get; set; } = CursorTheme.CombatNone;

    /// <summary>The lines the battle log holds so far, <c>StatusListBox::_messages</c>.</summary>
    public IReadOnlyList<string> LogMessages => _statusLog.Messages;

    /// <summary>
    /// The bar the two status lines are painted in, the click target that expands the log
    /// (<c>battle_interface.cpp:3506-3522</c>).
    /// </summary>
    public static Rect StatusBarArea => new(StatusLeft, Height, StatusWidth, StatusHeight);

    /// <summary>
    /// Writes one of the two status lines, <c>Status::setMessage()</c> (<c>battle_interface.cpp:1146-1164</c>).
    /// </summary>
    /// <param name="message">The text; it is cut to the bar's width like the reference does.</param>
    /// <param name="top">
    /// <see langword="true"/> for the upper line (an action result, which also goes to the log and clears the
    /// lower hint), <see langword="false"/> for the lower one (a cursor hint).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void SetStatus(string message, bool top)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (top)
        {
            _statusUpper = FitStatus(message);

            // The reference logs every message that goes on the upper line (Status::setMessage, top branch).
            AddLogMessage(message);

            _statusLower = string.Empty;
            _lastStatusLower = string.Empty;

            return;
        }

        if (message == _lastStatusLower)
        {
            return;
        }

        _statusLower = FitStatus(message);
        _lastStatusLower = message;
    }

    /// <summary>Clears both status lines, <c>Status::clear()</c> (<c>battle_interface.cpp:1180-1185</c>).</summary>
    public void ClearStatus()
    {
        _statusUpper = string.Empty;
        _statusLower = string.Empty;
        _lastStatusLower = string.Empty;
    }

    /// <summary>
    /// Adds a message to the battle log, <c>StatusListBox::AddMessage()</c> (<c>battle_interface.cpp:847-905</c>).
    /// </summary>
    /// <param name="message">The message to append; it is wrapped into rows by the log itself.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void AddLogMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _statusLog.AddMessage(message);
    }

    /// <summary>The arena this painter draws.</summary>
    public BattleArena Arena => _arena;

    /// <summary>The battlefield rectangle inside the 640x480 frame.</summary>
    public static Rect BattlefieldArea => new(0, 0, Width, Height);

    /// <summary>
    /// Draws one whole frame of the battle, <c>Interface::Redraw()</c> (<c>battle_interface.cpp:1608-1625</c>)
    /// without the parts that animate.
    /// </summary>
    /// <param name="output">The 640x480 frame to paint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Redraw(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        RedrawBattleGround();

        // RedrawCover() 的静态那半：底图原样铺到工作面上，随后的逐行动作直接画在工作面上。
        ImageOps.Blit(_surface, Point.Zero, _ground, _ground.Bounds);
        RedrawMovementArea();
        RedrawHoverShadow();
        RedrawArmies();
        RedrawTurnOrderBar();

        // redrawPreRender(): 战场贴到屏幕，然后画状态栏与按钮（RedrawInterface() 的底），最后是展开的日志窗。
        ImageOps.Blit(output, Point.Zero, _surface, _surface.Bounds);
        RedrawStatusBar(output);

        if (_statusLog.IsOpen)
        {
            _statusLog.Redraw(output);
        }
    }

    /// <summary>
    /// Advances the idle animations of both armies, <c>Interface::IdleTroopsAnimation()</c>
    /// (<c>battle_interface.cpp:7199-7207</c>) plus <c>Force::animateIdleUnits()</c>
    /// (<c>battle_army.cpp:232-257</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的节拍走全局延迟表：<c>BATTLE_IDLE_DELAY</c> 由战斗速度换算（速度 4 约 160 毫秒），每过一格
    /// 才推进一帧待机动画，所以所有单位一起呼吸而不是各走各的。端口照抄，问 <see cref="GameDelays"/>。
    /// </para>
    /// <para>
    /// 状态机与参考一致：待机序列播完（或单位被定身）回 <c>STATIC</c>；停在 <c>STATIC</c> 的单位等到自己
    /// 的随机延迟（<see cref="RandomizedDelay"/>，75%–125%）到点才切进 <c>IDLE</c>。定身/石化的单位不播待机。
    /// </para>
    /// </remarks>
    public void Update()
    {
        // 两侧英雄与旗帜也各有自己的节拍（battle_interface.cpp:3940-3945），与待机动画互不相干。
        UpdateOpponents();

        // The march has its own clock (CUSTOM_BATTLE_UNIT_MOVEMENT_DELAY), not the idle one
        // (battle_interface.cpp:4725-4728).
        if (_move is { } move && !move.Advance())
        {
            _move = null;
        }

        // The attack has its own clock too (custom delay), so it is not gated by the idle one.
        if (_attack is { } attack && !attack.Advance())
        {
            _attack = null;

            // 起手播完才放箭（RedrawActionAttackPart1() 在 :4148 才调 RedrawMissileAnimation）。
            if (_pendingMissile is { } pending)
            {
                _pendingMissile = null;
                pending.Restart();
                _missile = pending;
            }
        }

        // The shot walks BATTLE_MISSILE_DELAY per point (:1071-1076).
        if (_missile is { } shot && !shot.Advance())
        {
            _missile = null;
        }

        if (!GameDelays.ValidateAnimationDelay(DelayType.BattleIdleDelay))
        {
            return;
        }

        AnimateIdleUnits(_arena.AttackingForce);
        AnimateIdleUnits(_arena.DefendingForce);
    }

    /// <summary>One army's worth of <c>Force::animateIdleUnits()</c>.</summary>
    /// <param name="force">The army to advance.</param>
    private void AnimateIdleUnits(BattleForce force)
    {
        foreach (BattleUnit unit in force)
        {
            if (!unit.IsValid)
            {
                continue;
            }

            // The unit on its way, or in the middle of an attack, is driven by that playback, not by the idle loop.
            if (_move?.Unit == unit || _attack?.Units.Contains(unit) == true)
            {
                continue;
            }

            UnitAnimation animation = GetAnimation(unit);

            // 被复活的单位还带着死亡序列；活人不该躺着（复活流程不碰动画状态，ADR-229）。
            if (animation.State.CurrentState == MonsterAnimationType.Kill)
            {
                animation.State.SwitchAnimation(MonsterAnimationType.Static);

                continue;
            }

            if (animation.State.CurrentState == MonsterAnimationType.Idle)
            {
                if (animation.State.IsLastFrame || unit.IsImmovable)
                {
                    animation.State.SwitchAnimation(MonsterAnimationType.Static);
                }
                else
                {
                    animation.State.PlayAnimation();
                }
            }
            else if (animation.State.CurrentState == MonsterAnimationType.Static
                     && !unit.IsImmovable
                     && animation.IdleTimer.CheckDelay())
            {
                animation.State.SwitchAnimation(MonsterAnimationType.Idle);
            }
        }
    }

    /// <summary>
    /// The terrain, the border objects, the ground decoration and the castle ground,
    /// <c>Interface::_redrawBattleGround()</c> (<c>battle_interface.cpp:2477-2556</c>).
    /// </summary>
    private void RedrawBattleGround()
    {
        // The whole background image is copied without its offsets, the reference's fheroes2::Copy.
        if (!CopyFrame(_style.Ground, 0, _ground))
        {
            ImageOps.Clear(_ground, 0);
        }

        DrawFrame(_style.Border, 0, _ground, Point.Zero);

        if (_arena.CoverIcn != IcnType.Unknown)
        {
            DrawFrame(_arena.CoverIcn, 0, _ground, Point.Zero);
        }

        Castle? castle = _arena.Castle;

        if (castle is not null)
        {
            // Castle ground, then the moat over it.
            DrawFrame(_style.CastleBackground, 1, _ground, Point.Zero);

            if (castle.IsBuild(BuildingType.Moat))
            {
                DrawFrame(IcnType.Moatwhol, 0, _ground, Point.Zero);
            }
        }

        // Ground obstacles. The hexagon grid goes under them, the reference's order.
        if (Settings.Get().IsBattleShowGrid)
        {
            Sprite? grid = EnsureHexagonGrid();

            if (grid is not null)
            {
                for (int index = 0; index < BattleBoard.SizeInCells; ++index)
                {
                    if (_arena.Board.GetCell(index) is { } gridCell)
                    {
                        DrawFrame(grid, _ground, gridCell.Position);
                    }
                }
            }
        }

        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            RedrawGroundObject(index);
        }

        // Castle top wall.
        if (castle is not null)
        {
            DrawFrame(_style.CastleBackground, castle.IsFortificationBuilt ? 4 : 3, _ground, Point.Zero);
        }
    }

    /// <summary>
    /// A ground obstacle, <c>Interface::_redrawGroundObjects()</c> (<c>battle_interface.cpp:2766-2797</c>).
    /// </summary>
    /// <param name="cellIndex">The cell to look at.</param>
    private void RedrawGroundObject(int cellIndex)
    {
        if (_arena.Board.GetCell(cellIndex) is not { } cell || cell.Object == 0)
        {
            return;
        }

        IcnType icn = cell.Object switch
        {
            0x90 => IcnType.Cobj0016,
            0x9E => IcnType.Cobj0030,
            0x9F => IcnType.Cobj0031,
            _ => IcnType.Unknown,
        };

        if (icn == IcnType.Unknown)
        {
            return;
        }

        Rect area = cell.Position;

        DrawFrame(icn, 0, _ground, new Point(area.X + (area.Width / 2), area.Y + area.Height + CellYOffset));
    }

    /// <summary>
    /// Walks the board row by row and draws the high obstacles, the castle walls and the units,
    /// <c>Interface::RedrawArmies()</c> (<c>battle_interface.cpp:1689-1978</c>).
    /// </summary>
    /// <remarks>
    /// 参考的循环里夹着「正在移动的单位」「飞行单位」「坟场」「法术覆盖层」四组列表；端口把移动与飞行单独处理，
    /// 坟场按参考的两条路走：野外战在整轮之前一次画完（<c>battle_interface.cpp:1703-1705</c>），城战则在每行里
    /// 分城墙前后两批插进单位之前画（<c>:1758-1766</c>）。法术覆盖层仍未搬。
    /// </remarks>
    private void RedrawArmies()
    {
        Castle? castle = _arena.Castle;

        // Read once for the whole pass, the reference's local at battle_interface.cpp:1694.
        bool hitPointsBarEnabled = Settings.Get().IsBattleShowHitPointsBar;

        // 野外战没有城墙要遮挡，尸体铺在整轮最底下（:1703-1705）。
        if (castle is null)
        {
            RedrawKilled();
        }

        int[] wallCells =
        [
            BattleArena.CastleFirstTopWallPos,
            BattleArena.CastleTopArcherTowerPos,
            BattleArena.CastleSecondTopWallPos,
            BattleArena.CastleTopGateTowerPos,
            BattleArena.CastleGatePos,
            BattleArena.CastleBottomGateTowerPos,
            BattleArena.CastleThirdTopWallPos,
            BattleArena.CastleBottomArcherTowerPos,
            BattleArena.CastleFourthTopWallPos,
        ];

        for (int row = 0; row < BattleBoard.HeightInCells; ++row)
        {
            for (int column = 0; column < BattleBoard.WidthInCells; ++column)
            {
                RedrawHighObject((row * BattleBoard.WidthInCells) + column);
            }

            List<BattleUnit> beforeWall = [];
            List<BattleUnit> afterWall = [];
            List<BattleUnit> deadBeforeWall = [];
            List<BattleUnit> deadAfterWall = [];

            if (castle is not null)
            {
                // Main tower and catapult are drawn between the rows, the reference's cellRowId checks.
                if (row == 5)
                {
                    RedrawCastleMainTower(castle);
                }
                else if (row == 7)
                {
                    RedrawCastle(castle, BattleArena.CatapultPos);
                }

                int wallCell = wallCells[row];

                for (int column = 0; column < BattleBoard.WidthInCells; ++column)
                {
                    int cellId = (row * BattleBoard.WidthInCells) + column;
                    bool isBeforeWall = IsCellBeforeWall(row, column, wallCell);

                    CollectRowDead(cellId, isBeforeWall, deadBeforeWall, deadAfterWall);
                    CollectRowUnit(cellId, isBeforeWall, beforeWall, afterWall);
                }
            }
            else
            {
                for (int column = 0; column < BattleBoard.WidthInCells; ++column)
                {
                    CollectRowUnit((row * BattleBoard.WidthInCells) + column, isBeforeWall: true, beforeWall, afterWall);
                }
            }

            // 尸体垫在活人下面：城墙前的先画（:1749-1752）。
            foreach (BattleUnit dead in deadBeforeWall)
            {
                RedrawCorpse(dead);
            }

            foreach (BattleUnit unit in beforeWall)
            {
                RedrawTroopSprite(unit);
            }

            foreach (BattleUnit unit in beforeWall)
            {
                RedrawTroopCount(unit, hitPointsBarEnabled);
            }

            // The wall goes between the troops in front of it and the ones behind it.
            if (castle is not null)
            {
                RedrawCastle(castle, wallCells[row]);
            }

            foreach (BattleUnit dead in deadAfterWall)
            {
                RedrawCorpse(dead);
            }

            foreach (BattleUnit unit in afterWall)
            {
                RedrawTroopSprite(unit);
            }

            foreach (BattleUnit unit in afterWall)
            {
                RedrawTroopCount(unit, hitPointsBarEnabled);
            }

            // 英雄站在战场两侧，参考在中排（cellRowId == 2）画完单位之后画它们（:1972-1977）。
            if (row == 2)
            {
                RedrawOpponents();
            }
        }

        // The flyer in the air goes over everything (battle_interface.cpp:1972-1975).
        if (_move is { IsAirborne: true } flight)
        {
            RedrawTroopSprite(flight.Unit);
        }

        // A unit that fell to the attack being drawn is already off the board, so the painter stands it back
        // on its last cells itself: it has to be visible for its fall, not vanish when the blow lands.
        if (_attack is { } attack)
        {
            // 正在播的这一轮里死亡序列还在走，别把它推到末帧。
            foreach (BattleUnit dying in attack.DyingUnits)
            {
                RedrawTroopSprite(dying);
            }
        }

        // The shot flies over everything already drawn (battle_interface.cpp:1119-1131).
        RedrawMissile();
    }

    /// <summary>
    /// Draws both heroes beside the battlefield, <c>Interface::RedrawOpponents()</c>
    /// (<c>battle_interface.cpp:1982-1988</c>).
    /// </summary>
    private void RedrawOpponents()
    {
        _attackingOpponent?.Redraw(_surface);
        _defendingOpponent?.Redraw(_surface);

        RedrawOpponentsFlags();
    }

    /// <summary>
    /// Draws the waving flag above each hero, <c>Interface::RedrawOpponentsFlags()</c>
    /// (<c>battle_interface.cpp:1990-2026</c>).
    /// </summary>
    private void RedrawOpponentsFlags()
    {
        // 左边那面从英雄左上角往外量；右边那面整体水平翻转，x 从屏幕右沿往回量。
        if (_attackingOpponent is { } attacker)
        {
            IcnType icn = IcnCatalog.GetFlagIcnId(_arena.AttackingArmyColor);

            if (GetFlag(icn) is { } flag)
            {
                Point position = new(
                    attacker.Offset.X + BattleHeroSprites.LeftHeroXOffset + flag.OffsetX,
                    attacker.Offset.Y + BattleHeroSprites.LeftHeroYOffset + flag.OffsetY);

                ImageOps.BlitSprite(_surface, position, flag);
            }
        }

        if (_defendingOpponent is { } defender)
        {
            IcnType icn = IcnCatalog.GetFlagIcnId(_arena.DefendingArmyColor);

            if (GetFlag(icn) is { } flag)
            {
                Point position = new(
                    defender.Offset.X + ScreenBuffer.DefaultWidth - BattleHeroSprites.RightHeroXOffset
                        - (flag.OffsetX + flag.Width),
                    defender.Offset.Y + BattleHeroSprites.RightHeroYOffset + flag.OffsetY);

                BlitFlipped(icn, flag, position);
            }
        }
    }

    /// <summary>The flag frame of the current animation step, <c>Assets::getImage( icn, getAnimatedIcnIndex() )</c>.</summary>
    /// <param name="icn">The flag archive of a colour.</param>
    /// <returns>The frame, or <see langword="null"/> if the archive is missing.</returns>
    private Sprite? GetFlag(IcnType icn) =>
        _sprites.GetSprite(icn, (int)IcnCatalog.GetAnimatedIndex(icn, 0, _flagAnimationFrameIndex));

    /// <summary>Blits a frame horizontally mirrored, flipping it once and caching the result.</summary>
    /// <param name="icn">The archive the frame belongs to.</param>
    /// <param name="sprite">The frame.</param>
    /// <param name="position">Where its left edge goes.</param>
    private void BlitFlipped(IcnType icn, Sprite sprite, Point position)
    {
        (IcnType, int) key = (icn, (int)IcnCatalog.GetAnimatedIndex(icn, 0, _flagAnimationFrameIndex));

        // 与英雄一样：透明像素记在 transform 层，翻转时不能只翻图像。
        if (!_flippedFlags.TryGetValue(key, out Sprite? flipped))
        {
            flipped = sprite.Flip(horizontal: true, vertical: false);
            _flippedFlags[key] = flipped;
        }

        ImageOps.BlitSprite(_surface, position, flipped);
    }

    /// <summary>
    /// Makes the surviving side cheer and the losing side sigh when a stack dies,
    /// <c>Interface::SetHeroAnimationReactionToTroopDeath()</c> (<c>battle_interface.cpp:4441-4456</c>).
    /// </summary>
    /// <param name="deathColor">The colour of the army that lost the whole stack, <c>PlayerColor.Unused</c> for none.</param>
    public void SetHeroAnimationReactionToTroopDeath(PlayerColor deathColor)
    {
        if (deathColor == PlayerColor.Unused)
        {
            return;
        }

        // 死的是守方就是攻方在打：攻方英雄欢呼，守方英雄叹气。
        bool attackersTurn = deathColor == _arena.DefendingArmyColor;
        BattleOpponentSprite? attackingHero = attackersTurn ? _attackingOpponent : _defendingOpponent;
        BattleOpponentSprite? defendingHero = attackersTurn ? _defendingOpponent : _attackingOpponent;

        // 60%: `Rand::Get( 1, 5 ) < 4`；否则 80%: `Rand::Get( 1, 5 ) < 5`。
        if (attackingHero is not null && Rand.Get(1, 5) < 4)
        {
            attackingHero.Animation.SetAnimation(BattleHeroAnimation.Joy);
        }
        else if (defendingHero is not null && Rand.Get(1, 5) < 5)
        {
            defendingHero.Animation.SetAnimation(BattleHeroAnimation.Sorrow);
        }
    }

    /// <summary>
    /// Steps the flags and the two hero poses, the time-driven half of <c>Interface::Events()</c>
    /// (<c>battle_interface.cpp:3940-3945</c>).
    /// </summary>
    private void UpdateOpponents()
    {
        if (GameDelays.ValidateAnimationDelay(DelayType.BattleFlagsDelay))
        {
            // 参考的计数器是无符号的、只增不减，取模交给 ICN::getAnimatedIcnIndex。
            ++_flagAnimationFrameIndex;
        }

        if (GameDelays.ValidateAnimationDelay(DelayType.BattleOpponentsDelay))
        {
            _attackingOpponent?.Animation.UpdateAnimationState();
            _defendingOpponent?.Animation.UpdateAnimationState();
        }
    }

    /// <summary>
    /// Draws the projectile where the walk has got to, the loop body of <c>RedrawMissileAnimation()</c>
    /// (<c>battle_interface.cpp:1119-1131</c>).
    /// </summary>
    private void RedrawMissile()
    {
        if (_missile is not { Position: { } position } flight || flight.Projectile is not { } projectile)
        {
            return;
        }

        // 参考在 blit 时按 reverse 镜像（:1121-1125）；镜像过的 sprite 占同一片位置。
        Sprite drawn = flight.IsMirrored ? projectile.Flip(horizontal: true, vertical: false) : projectile;

        ImageOps.BlitSprite(_surface, position + flight.BlitOffset, drawn);
    }

    /// <summary>
    /// Whether a cell of a castle row is drawn before or after the wall of that row,
    /// the <c>isCellBefore</c> arithmetic of <c>RedrawArmies()</c> (<c>battle_interface.cpp:1747-1757</c>).
    /// </summary>
    /// <param name="row">The row being drawn.</param>
    /// <param name="column">The column being drawn.</param>
    /// <param name="wallCell">The wall cell of this row.</param>
    private bool IsCellBeforeWall(int row, int column, int wallCell)
    {
        int cellIndex = (row * BattleBoard.WidthInCells) + column;

        if (row < 5)
        {
            return cellIndex < wallCell;
        }

        bool isBeforeWall = cellIndex > wallCell;

        // A wrecked third or fourth wall cell has no wall left to draw, so the whole row goes after it.
        if ((wallCell == BattleArena.CastleThirdTopWallPos || wallCell == BattleArena.CastleFourthTopWallPos)
            && _arena.Board.GetCell(wallCell)?.Object == 0)
        {
            isBeforeWall = false;
        }

        return isBeforeWall;
    }

    /// <summary>
    /// Adds the unit standing on <paramref name="cellIndex"/> to the list of its side of the wall.
    /// </summary>
    /// <param name="cellIndex">The cell to look at.</param>
    /// <param name="isBeforeWall">Whether the cell is drawn before the wall.</param>
    /// <param name="beforeWall">List of units to draw before the wall.</param>
    /// <param name="afterWall">List of units to draw after the wall.</param>
    private void CollectRowUnit(int cellIndex, bool isBeforeWall, List<BattleUnit> beforeWall, List<BattleUnit> afterWall)
    {
        BattleUnit? unit = _arena.Board.GetCell(cellIndex)?.Unit;

        if (unit is null || !unit.IsValid || cellIndex == unit.TailIndex)
        {
            return;
        }

        // A flyer in the air is drawn above the whole battlefield instead of in its row
        // (battle_interface.cpp:1955-1975).
        if (_move?.IsAirborne == true && _move.Unit == unit)
        {
            return;
        }

        (isBeforeWall ? beforeWall : afterWall).Add(unit);
    }

    /// <summary>
    /// Adds the dead lying on <paramref name="cellIndex"/> to the list of its side of the wall,
    /// <c>Interface::RedrawArmies()</c>'s <c>deadTroopBeforeWall</c> / <c>deadTroopAfterWall</c>
    /// (<c>battle_interface.cpp:1758-1766</c>).
    /// </summary>
    /// <param name="cellIndex">The cell to look at.</param>
    /// <param name="isBeforeWall">Whether the cell is drawn before the wall.</param>
    /// <param name="beforeWall">List of bodies to draw before the wall.</param>
    /// <param name="afterWall">List of bodies to draw after the wall.</param>
    private void CollectRowDead(int cellIndex, bool isBeforeWall, List<BattleUnit> beforeWall, List<BattleUnit> afterWall)
    {
        foreach (BattleUnit dead in _arena.Graveyard.GetUnitsForRedraw(cellIndex))
        {
            (isBeforeWall ? beforeWall : afterWall).Add(dead);
        }
    }

    /// <summary>
    /// Draws every body on the field, <c>Interface::RedrawKilled()</c> (<c>battle_interface.cpp:2910-2920</c>).
    /// </summary>
    /// <remarks>野外战没有城墙，尸体一次性铺在最底下；城战里它们跟着行与城墙前后分批画。</remarks>
    private void RedrawKilled()
    {
        foreach (int cell in _arena.Graveyard.GetOccupiedCells())
        {
            foreach (BattleUnit unit in _arena.Graveyard.GetUnitsForRedraw(cell))
            {
                RedrawCorpse(unit);
            }
        }
    }

    /// <summary>Draws a body, making sure its death sequence sits on the last frame first.</summary>
    /// <param name="unit">A unit that is already off the board.</param>
    private void RedrawCorpse(BattleUnit unit)
    {
        AnimationState state = GetAnimation(unit).State;

        // 参考总是把死亡序列播到末帧；端口在 AI 回合即时结算，没播过，这里补上，
        // 免得尸体以站姿躺在场上（ADR-229）。
        if (state.CurrentState != MonsterAnimationType.Kill || !state.IsLastFrame)
        {
            if (state.SwitchAnimation(MonsterAnimationType.Kill))
            {
                while (!state.IsLastFrame)
                {
                    state.PlayAnimation();
                }
            }
        }

        RedrawTroopSprite(unit);
    }

    /// <summary>
    /// A high obstacle, <c>Interface::_redrawHighObjects()</c> (<c>battle_interface.cpp:2799-2908</c>).
    /// </summary>
    /// <param name="cellIndex">The cell to look at.</param>
    private void RedrawHighObject(int cellIndex)
    {
        if (_arena.Board.GetCell(cellIndex) is not { } cell || cell.Object == 0)
        {
            return;
        }

        IcnType icn = cell.Object switch
        {
            0x80 => IcnType.Cobj0000,
            0x81 => IcnType.Cobj0001,
            0x82 => IcnType.Cobj0002,
            0x83 => IcnType.Cobj0003,
            0x84 => IcnType.Cobj0004,
            0x85 => IcnType.Cobj0005,
            0x86 => IcnType.Cobj0006,
            0x87 => IcnType.Cobj0007,
            0x88 => IcnType.Cobj0008,
            0x89 => IcnType.Cobj0009,
            0x8A => IcnType.Cobj0010,
            0x8B => IcnType.Cobj0011,
            0x8C => IcnType.Cobj0012,
            0x8D => IcnType.Cobj0013,
            0x8E => IcnType.Cobj0014,
            0x8F => IcnType.Cobj0015,
            0x91 => IcnType.Cobj0017,
            0x92 => IcnType.Cobj0018,
            0x93 => IcnType.Cobj0019,
            0x94 => IcnType.Cobj0020,
            0x95 => IcnType.Cobj0021,
            0x96 => IcnType.Cobj0022,
            0x97 => IcnType.Cobj0023,
            0x98 => IcnType.Cobj0024,
            0x99 => IcnType.Cobj0025,
            0x9A => IcnType.Cobj0026,
            0x9B => IcnType.Cobj0027,
            0x9C => IcnType.Cobj0028,
            0x9D => IcnType.Cobj0029,
            _ => IcnType.Unknown,
        };

        if (icn == IcnType.Unknown)
        {
            return;
        }

        Rect area = cell.Position;

        DrawFrame(icn, 0, _surface, new Point(area.X + (area.Width / 2), area.Y + area.Height + CellYOffset));
    }

    /// <summary>
    /// Draws one unit, <c>Interface::RedrawTroopSprite()</c> plus <c>_drawTroopSprite()</c>
    /// (<c>battle_interface.cpp:2028-2119</c>) without the moat-mask branch.
    /// </summary>
    /// <param name="unit">The unit to draw.</param>
    private void RedrawTroopSprite(BattleUnit unit)
    {
        AnimationState animation = GetAnimation(unit).State;
        Sprite? sprite = _sprites.GetSprite(MonsterIcnIds.GetSpriteIcn(unit.Monster), animation.Frame);

        if (sprite is null)
        {
            return;
        }

        // The reference mirrors at blit time (isReflect); mirroring the sprite keeps the same bounds.
        Sprite drawn = unit.IsReflect ? sprite.Flip(horizontal: true, vertical: false) : sprite;

        Point position = BattleTroopSprite.GetPosition(unit, sprite);

        Point movementDelta = Point.Zero;
        CellDirection movementDirection = CellDirection.Center;
        bool isFlyingInMotion = false;

        // The unit on its way is drawn between two cells (battle_interface.cpp:2069-2110).
        if (_move?.Unit == unit)
        {
            position += BattleTroopDisplacement.GetOffset(
                unit.Position.GetRect(),
                unit.IsFlying,
                animation.AnimationLength,
                animation.MovementProgress,
                animation.GetCurrentFrameXOffset(),
                _move.MovingPosition,
                _move.FlyingPosition);

            BattleTroopDisplacement.Movement movement = BattleTroopDisplacement.GetMovement(
                unit.Position.GetRect(),
                unit.IsFlying,
                animation.AnimationLength,
                animation.MovementProgress,
                animation.GetCurrentFrameXOffset(),
                _move.MovingPosition);

            movementDelta = movement.Delta;
            movementDirection = movement.Direction;
            isFlyingInMotion = _move.FlyingPosition is not null;
        }

        Rect spriteArea = new(position.X, position.Y, drawn.Width, drawn.Height);

        // 踩在护城河里时，水面盖住 sprite 的下沿（battle_interface.cpp:2121-2200）。
        if (BattleMoatMasking.GetMask(
                _arena.Board, unit, _arena.Bridge, HasBuiltMoat(), isFlyingInMotion, movementDirection, movementDelta,
                spriteArea) is Rect water)
        {
            RedrawTroopSpriteWithMoatMask(drawn, position, spriteArea, water);

            return;
        }

        ImageOps.BlitSprite(_surface, position, drawn);
    }

    /// <summary>
    /// Draws a sprite with the moat's water band cut out,
    /// <c>_drawTroopSpriteWithMoatMask()</c> (<c>battle_interface.cpp:2188-2200</c>).
    /// </summary>
    /// <param name="sprite">The sprite to draw, already mirrored when the unit faces left.</param>
    /// <param name="position">Where its top left corner goes.</param>
    /// <param name="spriteArea">Its rectangle, the same as <paramref name="position"/> plus its size.</param>
    /// <param name="water">The band to leave out, clipped to <paramref name="spriteArea"/>.</param>
    /// <remarks>
    /// 参考把 sprite 切成三块画：水面之上整幅、水带左侧、水带右侧。水面之下不画，脚就「泡」在水里了。
    /// 源矩形用 sprite 自己的坐标（左上角为原点），目标点始终是 sprite 的左上角。
    /// </remarks>
    private void RedrawTroopSpriteWithMoatMask(Sprite sprite, Point position, Rect spriteArea, Rect water)
    {
        BattleMoatMasking.SpritePieces pieces = BattleMoatMasking.GetVisiblePieces(spriteArea, water);

        ImageOps.BlitSprite(_surface, position, sprite, pieces.Top);
        ImageOps.BlitSprite(_surface, position, sprite, pieces.Left);
        ImageOps.BlitSprite(_surface, position, sprite, pieces.Right);
    }

    /// <summary>Whether the defending castle built its moat, <c>Arena::GetCastle()</c>'s <c>BUILD_MOAT</c>.</summary>
    /// <returns><see langword="true"/> when a moat exists to be drawn.</returns>
    private bool HasBuiltMoat() =>
        _arena.Castle is { } castle && castle.IsBuild(FHeroes2.Game.Castle.BuildingType.Moat);

    /// <summary>
    /// The troop counter of a unit, <c>Interface::RedrawTroopCount()</c> (<c>battle_interface.cpp:2230-2267</c>).
    /// </summary>
    /// <param name="unit">The unit whose counter to draw.</param>
    /// <param name="hitPointsBarEnabled">
    /// Whether the hit point bar is drawn under the counter, the reference's argument of the same name.
    /// </param>
    /// <remarks>
    /// <para>
    /// 计数条是 <c>TEXTBAR</c> 的 10–14 号（10 紫、12 绿、13 黄、14 红，按单位身上的法术位挑），
    /// 数字是 <c>smallWhite</c> 的缩写形式（<c>Army::AbbreviateNumber</c>），压在条子正中偏下两像素。
    /// </para>
    /// <para>
    /// 打开「血量条」时，计数条先上移四像素（镜像的单位上移八像素，免得压住对面队伍的计数条），再把
    /// 血量条挂在它的下方（<c>:2250-2261</c>）。条子的绿红两色要按调色板取
    /// （<c>GetColorId( 0, 200, 0 )</c> 与 <c>GetColorId( 200, 0, 0 )</c>）：没有注入调色板时这一档退化为
    /// 不画，与网格的降级一致。
    /// </para>
    /// </remarks>
    private void RedrawTroopCount(BattleUnit unit, bool hitPointsBarEnabled)
    {
        Sprite? bar = _sprites.GetSprite(IcnType.Textbar, GetIndexIndicator(unit));

        if (bar is null)
        {
            return;
        }

        Rect area = unit.Position.GetRect();
        int head = unit.HeadIndex;
        bool reflected = unit.IsReflect;
        int front = BattleBoard.GetIndexDirection(head, reflected ? CellDirection.Left : CellDirection.Right);

        // The reference's own expression (battle_interface.cpp:2238) compares the row of the head with
        // "is the front tile index equal to the board width", i.e. with 1 for row 1 and 0 for row 0. It
        // looks wrong, but it is what decides whether a neighbour's counter may slide over: copied as is.
        bool isValidFrontMonster = (head / BattleBoard.WidthInCells) == (front == BattleBoard.WidthInCells ? 1 : 0);

        int x = area.X + (reflected ? -7 : area.Width - 13);
        int y = area.Y + area.Height - bar.Height - (reflected ? 21 : 9);

        int xOffset = GetAnimation(unit).State.GetTroopCountOffset(reflected);

        if (xOffset > 0 && isValidFrontMonster && BattleBoard.IsValidIndex(front)
            && _arena.Board.GetCell(front)?.Unit is not null)
        {
            xOffset = 0;
        }

        x += reflected ? -xOffset : xOffset;

        bool showHitPointsBar = hitPointsBarEnabled && _palette is not null;

        if (showHitPointsBar)
        {
            y -= BattleHitPointsBar.Height;

            if (reflected)
            {
                y -= BattleHitPointsBar.Height;
            }
        }

        Point barPosition = new(x + bar.OffsetX, y + bar.OffsetY);

        if (showHitPointsBar)
        {
            uint monsterHitPoints = (uint)MonsterStatistics.GetBattleStats(unit.Monster).HitPoints;

            BattleHitPointsBarPainter.Draw(_surface, barPosition, new Size(bar.Width, bar.Height),
                unit.HitPoints, monsterHitPoints, GetRemainingHitPointsColor(), GetMissingHitPointsColor());
        }

        ImageOps.BlitSprite(_surface, barPosition, bar);

        if (_counterFont is null)
        {
            return;
        }

        string count = ArmyText.AbbreviateNumber(unit.Count);
        int width = GlyphText.Measure(_counterFont, count);

        GlyphText.DrawLine(_counterFont, _surface, new Point(x + ((bar.Width - width) / 2), y + 2), count);
    }

    /// <summary>
    /// The green of the hit point bar, <c>GetColorId( 0, 200, 0 )</c>.
    /// </summary>
    /// <returns>The palette index, resolved once and kept.</returns>
    private byte GetRemainingHitPointsColor() =>
        _remainingHitPointsColor ??= PALColorId.Get(
            SixBit(BattleHitPointsBar.RemainingRed),
            SixBit(BattleHitPointsBar.RemainingGreen),
            SixBit(BattleHitPointsBar.RemainingBlue),
            _palette!);

    /// <summary>
    /// The red of the hit point bar, <c>GetColorId( 200, 0, 0 )</c>.
    /// </summary>
    /// <returns>The palette index, resolved once and kept.</returns>
    private byte GetMissingHitPointsColor() =>
        _missingHitPointsColor ??= PALColorId.Get(
            SixBit(BattleHitPointsBar.MissingRed),
            SixBit(BattleHitPointsBar.MissingGreen),
            SixBit(BattleHitPointsBar.MissingBlue),
            _palette!);

    /// <summary>An eight bit channel of <c>fheroes2::GetColorId()</c> in the six bit form the port takes.</summary>
    /// <param name="channel">The channel, 0 to 255.</param>
    /// <returns>The channel divided by four, as <c>GetColorId()</c> does internally.</returns>
    private static byte SixBit(byte channel) => (byte)(channel >> 2);

    /// <summary>
    /// The turn order bar over the top of the battlefield, <c>Battle::TurnOrder</c>
    /// (<c>battle_interface.cpp:1120-1420</c>, drawn from <c>redrawPreRender()</c> at <c>:1641-1656</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只画「战场窗口内、顶边」这一档：屏幕固定 640x480，窗口外没有第二块地方可放，所以
    /// <see cref="BattleTurnOrderState.Bottom"/> 在这里等同于不画（战斗设置窗因此也不会让玩家选到它）。
    /// </para>
    /// <para>
    /// <b>偏差</b>：正在行动的单位、以及鼠标所指的单位，都用固定的 110 号色描边。参考让前者在
    /// 110/114/118/122/126 之间按 <c>BATTLE_SELECTED_UNIT_DELAY</c> 循环闪光，因为那套节奏跟着单位的描边
    /// 动画走，而端口还没画描边。
    /// </para>
    /// </remarks>
    private void RedrawTurnOrderBar()
    {
        if (Settings.Get().BattleTurnOrder != BattleTurnOrderState.Top)
        {
            return;
        }

        IReadOnlyList<BattleUnit> order = _arena.OrderOfUnits;
        int validUnitCount = 0;

        foreach (BattleUnit unit in order)
        {
            if (unit.IsValid)
            {
                validUnitCount++;
            }
        }

        int slots = BattleTurnOrderLayout.GetSlotCount(_surface.Width, validUnitCount);

        if (slots == 0)
        {
            return;
        }

        Rect area = BattleTurnOrderLayout.GetArea(new Rect(0, 0, _surface.Width, _surface.Height), validUnitCount);
        IReadOnlyList<int> indices = BattleTurnOrderLayout.SelectSlots(order, slots);

        BattleUnit? hoveredUnit = _hoveredCell is { } cell ? _arena.Board.GetCell(cell)?.Unit : null;
        BattleUnit? currentUnit = _arena.CurrentUnit;

        for (int slot = 0; slot < indices.Count; slot++)
        {
            BattleUnit unit = order[indices[slot]];

            byte borderColor = ReferenceEquals(unit, currentUnit) || ReferenceEquals(unit, hoveredUnit)
                ? TurnOrderContourColor
                : BattleArmyColor.GetPaletteIndex(unit.GetCurrentColor());

            Rect icon = new(area.X + (slot * BattleTurnOrderLayout.IconSize), area.Y,
                BattleTurnOrderLayout.IconSize, BattleTurnOrderLayout.IconSize);

            // 参考按「原始军队色」决定图标朝哪边：攻方朝右，守方（`_opponentColor`）镜像朝左。
            RedrawTurnOrderIcon(icon, unit, unit.Color == _arena.DefendingForce.Color, borderColor);
        }
    }

    /// <summary>
    /// One icon of the turn order bar, <c>Battle::TurnOrder::_redrawUnit()</c> (<c>battle_interface.cpp:1219-1260</c>).
    /// </summary>
    /// <param name="pos">The 43x43 slot, <c>pos</c>.</param>
    /// <param name="unit">The stack the slot stands for.</param>
    /// <param name="revert">Whether the creature icon faces left, <c>revert</c>.</param>
    /// <param name="borderColor">The one pixel frame, already picked by the caller.</param>
    private void RedrawTurnOrderIcon(Rect pos, BattleUnit unit, bool revert, byte borderColor)
    {
        // 面板取自 SWAPWIN 第 0 帧的 (37,268)，往里缩一像素；参考用的是 Copy，不带变换层。
        if (_sprites.GetSprite(IcnType.Swapwin, 0) is { } background)
        {
            ImageOps.Blit(_surface, new Point(pos.X + 1, pos.Y + 1), background.Image,
                new Rect(37, 268, pos.Width - 2, pos.Height - 2));
        }

        if (GetTurnOrderIcon(unit, revert) is { } icon)
        {
            int x = pos.X + ((pos.Width - icon.Width) / 2);
            int y = pos.Y + pos.Height - icon.Height - ((icon.Height + 3) < pos.Height ? 3 : 0);

            ImageOps.BlitSprite(_surface, new Point(x, y), icon);
        }

        if (_counterFont is { } font)
        {
            GlyphText.DrawLine(font, _surface, new Point(pos.X + 2, pos.Y + 4),
                ArmyText.AbbreviateNumber(unit.Count), pos);
        }

        // 描边在压暗之前：参考连着边框一起压暗，所以已行动单位的边框也是灰的。
        ImageOps.DrawRect(_surface, pos, borderColor);

        // 已经行动过的压暗：先套 GRAY 再套 GRAY_FONT（参考这里直接写 3），与参考的两道 ApplyPalette 一致。
        if (unit.HasModes(BattleMonsterState.Moved))
        {
            ImageOps.ApplyPalette(_surface, pos, PaletteTable.Get(PaletteId.Gray));
            ImageOps.ApplyPalette(_surface, pos, PaletteTable.Get(PaletteId.GrayFont));
        }
    }

    /// <summary>
    /// The <c>MONS32</c> icon of a stack, as the turn order bar needs it.
    /// </summary>
    /// <param name="unit">The stack.</param>
    /// <param name="revert">Whether the icon is mirrored horizontally.</param>
    /// <returns>The sprite, or <see langword="null"/> when the icon archive is missing.</returns>
    /// <remarks>
    /// 镜像只改调色板（<c>CAP_MIRRORIMAGE</c> 的单位在战场上也是半透明的蓝紫），翻转则重画像素；两者都只做一次，
    /// 之后按（兵种，镜像，翻转）三元组缓存，免得每帧重新翻一遍二十来个精灵。
    /// </remarks>
    private Sprite? GetTurnOrderIcon(BattleUnit unit, bool revert)
    {
        bool mirror = unit.HasModes(BattleMonsterState.CapMirrorImage);
        int key = (((int)unit.Monster - (int)MonsterType.Peasant) * 4) + (mirror ? 2 : 0) + (revert ? 1 : 0);

        if (_turnOrderIcons.TryGetValue(key, out Sprite? cached))
        {
            return cached;
        }

        if (_sprites.GetSprite(IcnType.Mons32, (int)unit.Monster - (int)MonsterType.Peasant) is not { } source)
        {
            return null;
        }

        Sprite icon = source.Clone();

        if (mirror)
        {
            ImageOps.ApplyPalette(icon.Image, icon.Image.Bounds, PaletteTable.Get(PaletteId.MirrorImage));
        }

        if (revert)
        {
            icon = icon.Flip(horizontal: true, vertical: false);
        }

        _turnOrderIcons[key] = icon;

        return icon;
    }

    /// <summary>
    /// The <c>TEXTBAR</c> frame of a unit's counter, <c>GetIndexIndicator()</c>
    /// (<c>battle_interface.cpp:529-548</c>): green for good magic, yellow when both kinds are on,
    /// red for bad magic only and purple for none.
    /// </summary>
    /// <param name="unit">The unit whose spells to look at.</param>
    /// <returns>10, 12, 13 or 14.</returns>
    private static int GetIndexIndicator(BattleUnit unit)
    {
        if (unit.HasModes(BattleMonsterState.GoodMagic | BattleMonsterState.SpellAntiMagic))
        {
            return unit.HasModes(BattleMonsterState.BadMagic) ? 13 : 12;
        }

        return unit.HasModes(BattleMonsterState.BadMagic) ? 14 : 10;
    }

    /// <summary>
    /// The animation state of a unit, created on first sight.
    /// </summary>
    /// <param name="unit">The unit whose record to fetch.</param>
    /// <returns>The state and its idle timer, kept for the rest of the battle.</returns>
    private UnitAnimation GetAnimation(BattleUnit unit)
    {
        if (_animations.TryGetValue(unit.Uid, out UnitAnimation? animation))
        {
            return animation;
        }

        animation = new UnitAnimation(new AnimationState(unit.Monster, _monsterInfo));
        _animations[unit.Uid] = animation;

        return animation;
    }

    /// <summary>
    /// One part of a castle, <c>Interface::RedrawCastle()</c> (<c>battle_interface.cpp:2643-2757</c>).
    /// </summary>
    /// <param name="castle">The defending town.</param>
    /// <param name="cellIndex">The castle cell the part stands on.</param>
    private void RedrawCastle(Castle castle, int cellIndex)
    {
        IcnType icn = BattleFieldStyle.CastleFor(castle.Race);

        if (icn == IcnType.Unknown)
        {
            return;
        }

        if (cellIndex == BattleArena.CatapultPos)
        {
            // The catapult's frame at the start of a battle is the first one (HumanTurn resets it).
            DrawFrame(IcnType.Catapult, 0, _surface, new Point(22, 390));

            return;
        }

        if (cellIndex == BattleArena.CastleGatePos)
        {
            // The raised drawbridge; a wrecked one has no sprite of its own.
            if (_arena.Bridge is { IsDestroyed: false })
            {
                DrawFrame(icn, 4, _surface, Point.Zero);
            }

            return;
        }

        if (cellIndex is BattleArena.CastleFirstTopWallPos or BattleArena.CastleSecondTopWallPos
            or BattleArena.CastleThirdTopWallPos or BattleArena.CastleFourthTopWallPos)
        {
            DrawCastleWall(icn, castle, cellIndex);

            return;
        }

        if (cellIndex == BattleArena.CastleTopArcherTowerPos || cellIndex == BattleArena.CastleBottomArcherTowerPos)
        {
            DrawCastleArcherTower(icn, castle, cellIndex);

            return;
        }

        if (cellIndex == BattleArena.CastleTopGateTowerPos || cellIndex == BattleArena.CastleBottomGateTowerPos)
        {
            int index = _arena.Board.GetCell(cellIndex)?.Object == 1 ? 19 : 17;
            var offset = cellIndex == BattleArena.CastleTopGateTowerPos ? new Point(399, 237) : new Point(399, 321);

            DrawFrame(icn, index, _surface, offset);
        }
    }

    /// <summary>
    /// One of the four wall pieces, <c>RedrawCastle()</c>'s wall branch (<c>battle_interface.cpp:2659-2716</c>).
    /// </summary>
    /// <param name="icn">The castle archive of the race.</param>
    /// <param name="castle">The defending town.</param>
    /// <param name="cellIndex">The wall cell.</param>
    private void DrawCastleWall(IcnType icn, Castle castle, int cellIndex)
    {
        int index = cellIndex switch
        {
            BattleArena.CastleFirstTopWallPos => 5,
            BattleArena.CastleSecondTopWallPos => 6,
            BattleArena.CastleThirdTopWallPos => 7,
            _ => 8,
        };

        int cellObject = _arena.Board.GetCell(cellIndex)?.Object ?? 0;

        // 3 means the wall is still whole, 0 means it was knocked down: the plus values are the
        // reference's four sprite rows.
        index += castle.IsFortificationBuilt
            ? cellObject switch { 0 => 31, 1 => 35, 2 => 27, 3 => 23, _ => 0 }
            : cellObject switch { 0 => 8, 1 => 4, 2 => 0, _ => 0 };

        DrawFrame(icn, index, _surface, Point.Zero);
    }

    /// <summary>
    /// The left or right archer tower, <c>RedrawCastle()</c>'s two tower branches
    /// (<c>battle_interface.cpp:2717-2746</c>).
    /// </summary>
    /// <param name="icn">The castle archive of the race.</param>
    /// <param name="castle">The defending town.</param>
    /// <param name="cellIndex">The tower cell.</param>
    private void DrawCastleArcherTower(IcnType icn, Castle castle, int cellIndex)
    {
        bool isLeftTower = cellIndex == BattleArena.CastleTopArcherTowerPos;
        BattleTower? tower = _arena.GetTower(isLeftTower ? BattleTowerType.Left : BattleTowerType.Right);
        bool isBuilt = castle.IsBuild(isLeftTower ? BuildingType.LeftTurret : BuildingType.RightTurret);

        int index;

        if (isBuilt && tower is not null)
        {
            index = tower.IsValid ? 18 : 19;
        }
        else
        {
            // A tower without a built turret can still be damaged by Earthquake.
            index = _arena.Board.GetCell(cellIndex)?.Object == 1 ? 19 : 17;
        }

        var offset = isLeftTower ? new Point(443, 153) : new Point(443, 405);

        DrawFrame(icn, index, _surface, offset);
    }

    /// <summary>
    /// The keep of a castle, <c>Interface::RedrawCastleMainTower()</c> (<c>battle_interface.cpp:2759-2764</c>).
    /// </summary>
    /// <param name="castle">The defending town.</param>
    private void RedrawCastleMainTower(Castle castle)
    {
        IcnType icn = BattleFieldStyle.CastleFor(castle.Race);

        if (icn == IcnType.Unknown)
        {
            return;
        }

        bool towerIsValid = _arena.GetTower(BattleTowerType.Center)?.IsValid == true;

        DrawFrame(icn, towerIsValid ? 20 : 26, _surface, Point.Zero);
    }

    /// <summary>
    /// Fits a status message into the bar, <c>fitToOneRow( width - offsetForTextBar * 2 )</c>
    /// (<c>battle_interface.cpp:1151</c>).
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The message or its cut version; the message itself when no font is available.</returns>
    private string FitStatus(string message) =>
        _statusFont is null
            ? message
            : GlyphText.FitToOneRow(_statusFont, message, StatusWidth - (StatusTextInset * 2));

    /// <summary>Draws the two status lines over the bar, <c>Status::redraw()</c> (<c>battle_interface.cpp:1166-1178</c>).</summary>
    /// <param name="output">The frame to paint into.</param>
    private void RedrawStatusText(PixelBuffer output)
    {
        if (_statusFont is null)
        {
            return;
        }

        DrawStatusLine(output, _statusUpper, Height + 4);
        DrawStatusLine(output, _statusLower, Height + StatusUpperHeight);
    }

    /// <summary>Draws one status line centred in the bar.</summary>
    /// <param name="output">The frame to paint into.</param>
    /// <param name="text">The line, already fitted.</param>
    /// <param name="y">Top of the line.</param>
    private void DrawStatusLine(PixelBuffer output, string text, int y)
    {
        if (text.Length == 0)
        {
            return;
        }

        int width = GlyphText.Measure(_statusFont!, text);
        int x = StatusLeft + ((StatusWidth - width) / 2);

        GlyphText.DrawLine(_statusFont!, output, new Point(x, y), text);
    }

    /// <summary>
    /// The status bar and the three buttons under it, the background half of
    /// <c>Interface::RedrawInterface()</c> (<c>battle_interface.cpp:1674-1687</c>, <c>:1482-1492</c>).
    /// </summary>
    /// <param name="output">The frame to paint into.</param>
    /// <remarks>
    /// 先画底：状态栏的上下两块，以及自动战斗、设置、跳过三个按钮的底图；再把两行状态文字叠上去。
    /// </remarks>
    private void RedrawStatusBar(PixelBuffer output)
    {
        DrawFrame(IcnType.Textbar, 8, output, new Point(StatusLeft, Height));
        DrawFrame(IcnType.Textbar, 9, output, new Point(StatusLeft, Height + StatusUpperHeight));
        _buttons.Draw(output, PressedButton);

        RedrawStatusText(output);
    }

    /// <summary>
    /// Draws the walking range of the current unit and of the unit under the pointer, the static half of
    /// <c>Interface::_redrawCoverStatic()</c> (<c>battle_interface.cpp:2558-2641</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考只对<b>人类</b>的当前单位画（`_currentUnit->GetCurrentControl() & CONTROL_AI` 时直接返回），
    /// 免得把 AI 的打算摊在玩家眼前。两个开关各管一段：「highlight movement area」管鼠标下的兵
    /// （双份阴影，画出来比当前单位的范围更亮），`BattleShowMoveShadow` 管当前单位自己的可走范围。
    /// </para>
    /// <para>
    /// 已经出手的单位速度会被 <c>TR_MOVED</c> 归零，参考为了显示它真实的行动范围临时清掉模式、画完再放回，
    /// 这里照办。移动动画期间的跳过（参考的 `_movingUnit`）等动画层做出来再补。
    /// </para>
    /// </remarks>
    private void RedrawMovementArea()
    {
        if (_arena.CurrentUnit is not { } currentUnit)
        {
            return;
        }

        bool showMoveShadow = Settings.Get().IsBattleShowMoveShadow;

        if (!showMoveShadow && _movementAreaUnit is null)
        {
            return;
        }

        if (!currentUnit.GetCurrentControl().IsHuman())
        {
            return;
        }

        bool[] processed = new bool[BattleBoard.SizeInCells];

        if (_movementAreaUnit is { IsValid: true } highlighted && EnsureHexagonHighlightShadow() is { } areaShadow)
        {
            bool isMoved = highlighted.HasModes(BattleMonsterState.Moved);

            if (isMoved)
            {
                highlighted.ResetModes(BattleMonsterState.Moved);
            }

            for (int index = 0; index < BattleBoard.SizeInCells; ++index)
            {
                if (_arena.Board.GetCell(index) is not { } cell
                    || BattlePosition.GetReachable(_arena, highlighted, index).IsEmpty)
                {
                    continue;
                }

                // 两份叠着画，参考用它把「鼠标下的兵」与当前单位自己的范围区分开。
                DrawFrame(areaShadow, _surface, cell.Position);
                DrawFrame(areaShadow, _surface, cell.Position);

                processed[index] = true;
            }

            if (isMoved)
            {
                highlighted.SetModes(BattleMonsterState.Moved);
            }
        }

        if (!showMoveShadow)
        {
            return;
        }

        Sprite? shadow = Settings.Get().IsBattleShowGrid ? EnsureHexagonGridShadow() : EnsureHexagonShadow();

        if (shadow is null)
        {
            return;
        }

        for (int index = 0; index < BattleBoard.SizeInCells; ++index)
        {
            if (processed[index] || _arena.Board.GetCell(index) is not { } cell)
            {
                continue;
            }

            if (!BattlePosition.GetReachable(_arena, currentUnit, index).IsEmpty)
            {
                // 开着格子网格时用带网格的阴影，否则网格会被阴影盖掉（参考 :2632 的注释）。
                DrawFrame(shadow, _surface, cell.Position);
            }
        }
    }

    /// <summary>
    /// Draws the shadow under the cell the pointer is on, the static half of
    /// <c>Interface::RedrawCover()</c> (<c>battle_interface.cpp:2285-2474</c>) for the current unit.
    /// </summary>
    /// <remarks>
    /// 参考只在「有当前单位、设置里开着鼠标阴影」时画，且画在单位之下（`RedrawCover` 先于 `RedrawArmies`）。
    /// 没有施法时按 <see cref="BattleCoverHighlight.GetCells"/> 给出的格子画（攻击落点、宽单位的两格、目标格本身）；
    /// 正在瞄准法术时按 <see cref="BattleSpellHighlight"/> 给出的格子逐个过滤后画，过滤条件是参考的原话：
    /// 格子能过、格子上的兵吃这个法术。
    /// </remarks>
    private void RedrawHoverShadow()
    {
        if (_hoveredCell is not { } index || !Settings.Get().IsBattleShowMouseShadow)
        {
            return;
        }

        if (_arena.CurrentUnit is not { } currentUnit || _arena.Board.GetCell(index) is not { } cell)
        {
            return;
        }

        Sprite? shadow = EnsureHexagonCursorShadow();

        if (shadow is null)
        {
            return;
        }

        if (PendingSpell.Type == SpellType.None)
        {
            foreach (int highlighted in BattleCoverHighlight.GetCells(_arena, currentUnit, cell.Index, CursorTheme))
            {
                if (_arena.Board.GetCell(highlighted) is { } highlightedCell)
                {
                    DrawFrame(shadow, _surface, highlightedCell.Position);
                }
            }

            return;
        }

        BattleUnit? teleportingUnit = TeleportSourceCell >= 0 ? _arena.GetTroopBoard(TeleportSourceCell) : null;
        IArmyCommander? commander = _arena.GetCurrentCommander();

        foreach (int highlighted in BattleSpellHighlight.GetCells(PendingSpell.Type, index, CursorTheme, teleportingUnit))
        {
            if (_arena.Board.GetCell(highlighted) is not { } highlightedCell)
            {
                continue;
            }

            bool isApplicable = highlightedCell.IsPassable(false);

            if (isApplicable)
            {
                BattleUnit? unit = highlightedCell.Unit;
                isApplicable = unit is null || unit.AllowApplySpell(PendingSpell, commander);
            }

            if (isApplicable)
            {
                DrawFrame(shadow, _surface, highlightedCell.Position);
            }
        }
    }

    /// <summary>
    /// The shadow of every walkable cell, <c>DrawHexagonShadow( 4, 2 )</c> (<c>battle_interface.cpp:478-500</c>).
    /// </summary>
    /// <returns>The sprite, built once.</returns>
    private Sprite? EnsureHexagonShadow()
    {
        _hexagonShadow ??= BuildHexagonShadow(alpha: 4, horizontalSpace: 2);

        return _hexagonShadow;
    }

    /// <summary>
    /// That shadow with the grid baked in, <c>DrawHexagonShadow( 4, 1 )</c>, used while the grid is shown.
    /// </summary>
    /// <returns>The sprite, built once.</returns>
    private Sprite? EnsureHexagonGridShadow()
    {
        _hexagonGridShadow ??= BuildHexagonShadow(alpha: 4, horizontalSpace: 1);

        return _hexagonGridShadow;
    }

    /// <summary>
    /// The doubled shadow of the unit under the pointer, <c>DrawHexagonShadow( 2, 2 )</c>.
    /// </summary>
    /// <returns>The sprite, built once.</returns>
    private Sprite? EnsureHexagonHighlightShadow()
    {
        _hexagonHighlightShadow ??= BuildHexagonShadow(alpha: 2, horizontalSpace: 2);

        return _hexagonHighlightShadow;
    }

    /// <summary>
    /// The grid sprite of every cell, <c>DrawHexagon()</c> (<c>battle_interface.cpp:462-476</c>), built once.
    /// </summary>
    /// <returns>The sprite, or <see langword="null"/> when the palette or <c>CMBTMISC</c> is missing.</returns>
    private Sprite? EnsureHexagonGrid()
    {
        if (_hexagonGrid is not null || _palette is null)
        {
            return _hexagonGrid;
        }

        if (_sprites.GetSprite(IcnType.Cmbtmisc, 0) is not { } original)
        {
            return null;
        }

        int width = original.Width;
        int height = original.Height + 1;

        PixelBuffer image = new(width, height);
        byte[] transform = new byte[width * height];
        Array.Fill(transform, (byte)1);

        // Move the hexagon one pixel down; the new top row stays transparent.
        for (int y = 0; y < original.Height; ++y)
        {
            original.Image.Row(y).CopyTo(image.Row(y + 1));

            if (original.Transform is { } sourceTransform)
            {
                sourceTransform.AsSpan(y * width, width).CopyTo(transform.AsSpan((y + 1) * width, width));
            }
            else
            {
                transform.AsSpan((y + 1) * width, width).Fill(0);
            }
        }

        // fheroes2::ReplaceColorId( hexagonSprite, 0, colorId ) on the image layer.
        byte gridColor = PALColorId.Get(0x68 / 4, 0x8C / 4, 0x04 / 4, _palette);
        Span<byte> pixels = image.Pixels;

        for (int i = 0; i < pixels.Length; ++i)
        {
            if (pixels[i] == 0)
            {
                pixels[i] = gridColor;
            }
        }

        _hexagonGrid = new Sprite(image, 0, 0, transform);

        return _hexagonGrid;
    }

    /// <summary>
    /// The hover shadow, <c>DrawHexagonShadow( 2, 1 )</c> (<c>battle_interface.cpp:478-500</c>), built once.
    /// </summary>
    /// <returns>The sprite, or <see langword="null"/> when the palette is missing (it is not needed, but the
    /// grid and the shadow are built together).</returns>
    private Sprite? EnsureHexagonCursorShadow()
    {
        _hexagonCursorShadow ??= BuildHexagonShadow(alpha: 2, horizontalSpace: 1);

        return _hexagonCursorShadow;
    }

    /// <summary>
    /// Builds one hexagonal shadow of <c>alpha</c> transform pixels,
    /// <c>DrawHexagonShadow()</c> (<c>battle_interface.cpp:478-500</c>).
    /// </summary>
    /// <param name="alpha">Transform value written into the shadow, 2 is the cursor's.</param>
    /// <param name="horizontalSpace">Horizontal inset of the first stripe.</param>
    /// <returns>A transparent sprite whose middle rows carry the transform.</returns>
    private static Sprite BuildHexagonShadow(byte alpha, int horizontalSpace)
    {
        const int sideLength = 13;

        int width = BattleCell.WidthPx;
        int height = BattleCell.HeightPx;

        PixelBuffer image = new(width, height);
        byte[] transform = new byte[width * height];
        Array.Fill(transform, (byte)1);

        int x = horizontalSpace;
        int y = sideLength - 1;
        int stripeWidth = width + 1 - (horizontalSpace * 2);
        int stripeHeight = (2 * sideLength) + 4;

        for (int i = 0; i < width / 2; i += 2)
        {
            for (int column = 0; column < stripeWidth; ++column)
            {
                int pixelX = x + column;

                if (pixelX < 0 || pixelX >= width)
                {
                    continue;
                }

                for (int row = 0; row < stripeHeight; ++row)
                {
                    int pixelY = y + row;

                    if (pixelY < 0 || pixelY >= height)
                    {
                        continue;
                    }

                    transform[(pixelY * width) + pixelX] = alpha;
                }
            }

            --y;
            stripeHeight += 2;
            x += i == 0 ? 1 : 2;
            stripeWidth -= i == 0 ? 2 : 4;
        }

        return new Sprite(image, 0, 0, transform);
    }

    /// <summary>
    /// Draws an already built sprite at <paramref name="position"/> plus its own offsets.
    /// </summary>
    /// <param name="sprite">The sprite to draw.</param>
    /// <param name="target">The surface to paint into.</param>
    /// <param name="position">Where the sprite's anchor goes.</param>
    private static void DrawFrame(Sprite sprite, PixelBuffer target, Rect position) =>
        ImageOps.BlitSprite(target, new Point(position.X + sprite.OffsetX, position.Y + sprite.OffsetY), sprite);

    /// <summary>
    /// Draws one frame of an archive with the frame's own offsets added to <paramref name="position"/>,
    /// the reference's <c>fheroes2::Blit( sprite, out, x + sprite.x(), y + sprite.y() )</c>.
    /// </summary>
    /// <param name="icn">The archive to read.</param>
    /// <param name="index">The frame inside the archive.</param>
    /// <param name="target">The surface to paint into.</param>
    /// <param name="position">Where the sprite's anchor goes.</param>
    private void DrawFrame(IcnType icn, int index, PixelBuffer target, Point position)
    {
        if (icn == IcnType.Unknown || _sprites.GetSprite(icn, index) is not { } sprite)
        {
            return;
        }

        ImageOps.BlitSprite(target, new Point(position.X + sprite.OffsetX, position.Y + sprite.OffsetY), sprite);
    }

    /// <summary>
    /// Copies one frame of an archive to the top left corner of <paramref name="target"/> without its
    /// offsets, the reference's <c>fheroes2::Copy( sprite, image )</c>.
    /// </summary>
    /// <param name="icn">The archive to read.</param>
    /// <param name="index">The frame inside the archive.</param>
    /// <param name="target">The surface to paint into.</param>
    /// <returns><see langword="false"/> when the archive or the frame is missing.</returns>
    private bool CopyFrame(IcnType icn, int index, PixelBuffer target)
    {
        if (icn == IcnType.Unknown || _sprites.GetSprite(icn, index) is not { } sprite)
        {
            return false;
        }

        ImageOps.Blit(target, Point.Zero, sprite.Image, sprite.Image.Bounds);

        return true;
    }

    /// <summary>
    /// Plays a <see cref="BattleMoveAnimation"/> one frame at a time, the per-cell loop of
    /// <c>RedrawActionMove()</c>/<c>RedrawActionFly()</c> (<c>battle_interface.cpp:4524-4830</c>).
    /// </summary>
    /// <remarks>
    /// 参考每格都阻塞一轮事件循环；这里把「等节拍 → 推一帧 → 一序列播完就换下一步」变成
    /// <see cref="Advance"/> 的一次调用。位置在这一层改，和参考在每格播完后 <c>SetPosition( *dst )</c> 一致，
    /// 否则下一格的水平偏移会从起点算起，画面上就会越走越落后。
    /// </remarks>
    private sealed class MovePlayback
    {
        private readonly BattleMoveAnimation _plan;
        private readonly BattleArena _arena;
        private readonly AnimationState _state;
        private readonly ulong _frameDelay;
        private int _stepIndex;

        /// <summary>Starts the march: sets the shared movement clock and switches to the first step.</summary>
        /// <param name="plan">The steps to play.</param>
        /// <param name="state">The unit's animation machine, owned by the painter.</param>
        /// <param name="arena">The arena, for the rectangles the facing follows.</param>
        public MovePlayback(BattleMoveAnimation plan, AnimationState state, BattleArena arena)
        {
            _plan = plan;
            _state = state;
            _arena = arena;
            _stepIndex = -1;

            uint delay = GameDelays.ApplyBattleSpeed(state.MoveSpeed);

            if (plan.Unit.HasModes(BattleMonsterState.SpellHaste) && delay > 1)
            {
                delay = delay * 8 / 10;
            }
            else if (plan.Unit.HasModes(BattleMonsterState.SpellSlow))
            {
                delay = delay * 12 / 10;
            }

            // One delay for every movement frame, taken from the MOVING sequence itself
            // (battle_interface.cpp:4754-4756).
            int frames = Math.Max(1, state.GetAnimationVector(MonsterAnimationType.Moving).Length);
            _frameDelay = Math.Max(1, delay / (uint)frames);

            GameDelays.SetCustomUnitMovementDelay(_frameDelay);
            GameDelays.AnimateResetDelay(DelayType.CustomDelay);

            BeginStep();
        }

        /// <summary>The unit on its way.</summary>
        public BattleUnit Unit => _plan.Unit;

        /// <summary>Whether every step has finished playing.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>The point the current step heads for, <c>_movingPos</c>.</summary>
        public Point? MovingPosition => IsFinished ? null : Current.MovingPosition;

        /// <summary>The point the current stretch of air left from, <c>_flyingPos</c>.</summary>
        public Point? FlyingPosition => IsFinished ? null : Current.FlyingPosition;

        /// <summary>Whether the unit is in the air right now.</summary>
        public bool IsAirborne => FlyingPosition is not null;

        /// <summary>The step being played.</summary>
        private BattleMoveStep Current => _plan.Steps[_stepIndex];

        /// <summary>Advances one frame if the movement clock allows it.</summary>
        /// <returns><see langword="true"/> while the march still has something to draw.</returns>
        public bool Advance()
        {
            if (IsFinished)
            {
                return false;
            }

            if (!GameDelays.ValidateCustomAnimationDelay(_frameDelay))
            {
                return true;
            }

            if (_state.IsLastFrame)
            {
                NextStep();
            }
            else
            {
                _state.PlayAnimation();
            }

            return !IsFinished;
        }

        /// <summary>Ends the current step: the unit now stands on its cell, then the next step starts.</summary>
        private void NextStep()
        {
            if (!_plan.IsFlight && !Current.IsTurnOnly)
            {
                _plan.Unit.SetPosition(Current.CellIndex);
            }

            ++_stepIndex;
            BeginStep();
        }

        /// <summary>Switches to the next step that has frames, turning and reflecting on the way.</summary>
        private void BeginStep()
        {
            while (_stepIndex < _plan.Steps.Count)
            {
                BattleMoveStep step = Current;

                if (step.IsTurnOnly)
                {
                    // A wide unit swaps its two cells on the spot (battle_interface.cpp:4612-4617).
                    _plan.Unit.SetReflection(!_plan.Unit.IsReflect);
                    ++_stepIndex;
                    continue;
                }

                if (_plan.IsFlight)
                {
                    if (step.Animation == MonsterAnimationType.FlyLand)
                    {
                        // The unit is already on its cell when it lands, so the landing frames correct
                        // against it rather than sliding in from the start (battle_interface.cpp:4816).
                        _plan.Unit.SetPosition(_plan.Destination);

                        if (_arena.Board.GetCell(_plan.Destination) is { } landing)
                        {
                            _plan.Unit.UpdateDirection(landing.Position);
                        }
                    }
                }
                else if (!_plan.Unit.IsWide && _arena.Board.GetCell(step.CellIndex) is { } cell)
                {
                    _plan.Unit.UpdateDirection(cell.Position);
                }

                if (_state.SwitchAnimation(step.Animation))
                {
                    return;
                }

                // The creature has no such animation: the step is over before it starts.
                if (!_plan.IsFlight)
                {
                    _plan.Unit.SetPosition(step.CellIndex);
                }

                ++_stepIndex;
            }

            Finish();
        }

        /// <summary>Puts the unit back on its rest pose once the whole march is played.</summary>
        private void Finish()
        {
            if (_plan.IsFlight)
            {
                _plan.Unit.SetPosition(_plan.Destination);
            }

            IsFinished = true;
            _state.SwitchAnimation(MonsterAnimationType.Static);
        }
    }

    /// <summary>
    /// The attack being drawn, one sequence at a time: the sequential form of <c>RedrawActionAttackPart1()</c>,
    /// <c>RedrawActionAttackPart2()</c> and <c>_redrawActionWincesKills()</c>
    /// (<c>battle_interface.cpp:4088-4439</c>).
    /// </summary>
    /// <remarks>
    /// 参考把攻方与受击方放在<b>同一个</b>循环里推进，端口一次只推进一个单位：结果是「砍完再挨打」，
    /// 而不是「砍到一半对方已经在抖」。这是有意的取舍，见 ADR-226。
    /// </remarks>
    private sealed class AttackPlayback
    {
        private readonly IReadOnlyList<BattleAttackStep> _steps;
        private readonly BattleFieldPainter _painter;
        private readonly HashSet<BattleUnit> _units = [];
        private readonly List<BattleUnit> _dying = [];
        private int _step;
        private AnimationState? _state;
        private ulong _frameDelay = 1;

        /// <summary>
        /// Whether the step being drawn is a death sequence, which stays on its last frame: a body keeps lying
        /// where it fell (<c>battle_interface.cpp:4298-4323</c> — the reference never resets a killed unit).
        /// </summary>
        private bool _isDyingStep;

        /// <summary>Starts the playback.</summary>
        /// <param name="steps">The sequences to play, in order.</param>
        /// <param name="painter">The painter that owns the animation states.</param>
        /// <param name="current">
        /// The state already showing the first step, <c>RedrawActionAttackPart1()</c>'s switch that happened
        /// before the playback was made; <see langword="null"/> when the first step still has to be switched to.
        /// </param>
        public AttackPlayback(
            IReadOnlyList<BattleAttackStep> steps, BattleFieldPainter painter, AnimationState? current)
        {
            _steps = steps;
            _painter = painter;

            foreach (BattleAttackStep step in steps)
            {
                _units.Add(step.Unit);

                if (step.Animation == MonsterAnimationType.Kill && !_dying.Contains(step.Unit))
                {
                    _dying.Add(step.Unit);
                }
            }

            GameDelays.AnimateResetDelay(DelayType.CustomDelay);

            if (current is null)
            {
                BeginStep();
            }
            else
            {
                _state = current;
                _isDyingStep = steps[0].Animation == MonsterAnimationType.Kill;
                SetFrameDelay(steps[0]);
            }
        }

        /// <summary>Whether every step has finished playing.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>The units whose own idle animation must not run while the attack is drawn.</summary>
        public IReadOnlyCollection<BattleUnit> Units => _units;

        /// <summary>
        /// The units that fell to this attack. They are already off the board, so the painter draws them
        /// on its own for the whole playback (<c>_redrawActionWincesKills()</c> draws the same unit anyway).
        /// </summary>
        public IReadOnlyList<BattleUnit> DyingUnits => _dying;

        /// <summary>Advances one frame if the attack clock allows it.</summary>
        /// <returns><see langword="true"/> while the attack still has something to draw.</returns>
        public bool Advance()
        {
            if (IsFinished)
            {
                return false;
            }

            if (!GameDelays.ValidateCustomAnimationDelay(_frameDelay))
            {
                return true;
            }

            if (_state!.IsLastFrame)
            {
                NextStep();
            }
            else
            {
                _state.PlayAnimation();
            }

            return !IsFinished;
        }

        /// <summary>Returns the current unit to its rest pose and moves on; a body stays where it fell.</summary>
        private void NextStep()
        {
            if (!_isDyingStep)
            {
                _state!.SwitchAnimation(MonsterAnimationType.Static);
            }

            ++_step;
            BeginStep();
        }

        /// <summary>Switches to the next step the creature actually has frames for.</summary>
        private void BeginStep()
        {
            while (_step < _steps.Count)
            {
                BattleAttackStep step = _steps[_step];
                AnimationState state = _painter.GetAnimation(step.Unit).State;

                if (state.SwitchAnimation(step.Animation))
                {
                    _state = state;
                    _isDyingStep = step.Animation == MonsterAnimationType.Kill;
                    SetFrameDelay(step);

                    return;
                }

                // The creature has no such sequence: the step is over before it starts.
                ++_step;
            }

            Finish();
        }

        /// <summary>
        /// How long one frame of the current step lasts: a shot spreads its shooting speed over the whole
        /// sequence (<c>battle_interface.cpp:4112-4119</c>), everything else runs on the battle frame delay.
        /// </summary>
        private void SetFrameDelay(BattleAttackStep step)
        {
            uint delay = step.IsShooting
                ? Math.Max(1u, GameDelays.ApplyBattleSpeed(_state!.ShootSpeed) / (uint)Math.Max(1, _state.AnimationLength))
                : GameDelays.ApplyBattleSpeed(120u);

            _frameDelay = Math.Max(1u, delay);
        }

        /// <summary>Puts the last unit back on its rest pose once the whole attack is played.</summary>
        private void Finish()
        {
            IsFinished = true;

            if (!_isDyingStep)
            {
                _state?.SwitchAnimation(MonsterAnimationType.Static);
            }
        }
    }

    /// <summary>
    /// One shot's walk over its route, the loop of <c>RedrawMissileAnimation()</c>
    /// (<c>battle_interface.cpp:1062-1136</c>): one point per <c>BATTLE_MISSILE_DELAY</c>.
    /// </summary>
    private sealed class MissilePlayback
    {
        private readonly BattleMissile _missile;
        private int _index;
        private bool _isFinished;

        /// <summary>Takes a route over; the clock is reset by <see cref="Restart"/> when it actually leaves.</summary>
        /// <param name="missile">The route.</param>
        /// <param name="projectile">The projectile frame to blit, <see langword="null"/> for a mage's lightning.</param>
        public MissilePlayback(BattleMissile missile, Sprite? projectile)
        {
            _missile = missile;
            Projectile = projectile;
        }

        /// <summary>The projectile frame, once the sprite has been picked.</summary>
        public Sprite? Projectile { get; }

        /// <summary>Where the projectile sprite goes relative to the point it stands on.</summary>
        public Point BlitOffset => _missile.BlitOffset;

        /// <summary>Whether the shot flies to the left.</summary>
        public bool IsMirrored => _missile.IsMirrored;

        /// <summary>The point the shot stands on right now, <see langword="null"/> once it has landed.</summary>
        public Point? Position => _isFinished || _index >= _missile.Points.Count ? null : _missile.Points[_index];

        /// <summary>Starts the shot clock, <c>AnimateResetDelay( BATTLE_MISSILE_DELAY )</c> before the loop.</summary>
        public void Restart()
        {
            _index = 0;
            _isFinished = false;
            GameDelays.AnimateResetDelay(DelayType.BattleMissileDelay);
        }

        /// <summary>Walks one point if the shot clock allows it.</summary>
        /// <returns><see langword="true"/> while the shot still has somewhere to go.</returns>
        public bool Advance()
        {
            if (_isFinished)
            {
                return false;
            }

            if (!GameDelays.ValidateAnimationDelay(DelayType.BattleMissileDelay))
            {
                return true;
            }

            ++_index;

            if (_index >= _missile.Points.Count)
            {
                _isFinished = true;

                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// One unit's animation state with the idle timer of <c>Battle::Unit::_idleTimer</c>
    /// (<c>battle_troop.h:383</c>), created from the creature's <c>.BIN</c> record.
    /// </summary>
    private sealed class UnitAnimation
    {
        /// <summary>Builds the pair; the timer takes the creature's own idle delay.</summary>
        /// <param name="state">The state machine, already on the static pose.</param>
        public UnitAnimation(AnimationState state)
        {
            State = state;
            IdleTimer = new RandomizedDelay(state.IdleDelay);
        }

        /// <summary>The state machine of the creature, <c>Unit::animation</c>.</summary>
        public AnimationState State { get; }

        /// <summary>How long until the unit may start its idle animation, <c>Unit::_idleTimer</c>.</summary>
        public RandomizedDelay IdleTimer { get; }
    }
}
