using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
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
/// 攻城时的城堡地面/护城河/城墙/塔/投石车，以及双方摆好阵形的单位（各取待机动画的第一帧）；此外还有
/// 状态栏的两行文字与可展开的 6 行战斗日志窗（<see cref="BattleStatusLog"/>）。
/// 行动交互的其余部分、顺序条与所有法术特效都还没有，见 <c>bd list</c>。
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
/// <b>与参考的偏差</b>：坟场（<c>RedrawKilled</c>）不画，开战时本来就没有尸体；单位的血量条
/// （<c>drawHitPointsBar</c>）与顺序条还没画，见 <c>bd list</c> 的 <c>ukw.21</c> 与 <c>ukw.22</c>；
/// 六角网格照 <c>Settings::BattleShowGrid</c> 开关（战斗设置窗里的 Grid 一档）。
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
    private const int CellYOffset = -9;

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
    private readonly BattleStatusLog _statusLog;
    private readonly BattleStatusButtons _buttons;
    private readonly PixelBuffer _ground = new(Width, Height);
    private readonly PixelBuffer _surface = new(Width, Height);

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

    /// <summary>The cell the pointer is over, or <see langword="null"/> when it is off the board.</summary>
    private int? _hoveredCell;

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
    /// The game palette in video values, for the two colours the screen derives from it (the grid and, later,
    /// the hit point bar); <see langword="null"/> leaves the grid out.
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

        _arena.Board.SetArea(new Rect(0, 0, ScreenBuffer.DefaultWidth, ScreenBuffer.DefaultHeight));
    }

    /// <summary>
    /// Sets the cell the pointer is over, the <c>_currentCellIndex</c> the hover shadow follows
    /// (<c>battle_interface.cpp:2285-2474</c>).
    /// </summary>
    /// <param name="cellIndex">The cell, or <see langword="null"/> when the pointer is off the board.</param>
    public void SetHoveredCell(int? cellIndex) => _hoveredCell = cellIndex;

    /// <summary>The battle log window, <c>Interface::listlog</c> (<c>battle_interface.cpp:803-980</c>).</summary>
    public BattleStatusLog StatusLog => _statusLog;

    /// <summary>The three status bar buttons, <c>Interface::_buttonAuto</c> and friends.</summary>
    public BattleStatusButtons Buttons => _buttons;

    /// <summary>
    /// The button held down this frame, <c>_buttonAuto.drawOnState()</c> and friends
    /// (<c>battle_interface.cpp:3222-3224</c>).
    /// </summary>
    public BattleStatusButton PressedButton { get; set; }

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
        RedrawHoverShadow();
        RedrawArmies();

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

            UnitAnimation animation = GetAnimation(unit);

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
    /// 参考的循环里还夹着「正在移动的单位」「飞行单位」「坟场」「法术覆盖层」四组列表；这一步一个都没有，
    /// 所以每行只剩两件事：先画这一行的九个高障碍与城墙，再把站在这一行的单位（只认头部格）画出来。
    /// 兵力计数条（`RedrawTroopCount`）跟着单位画、但在整行之后，要字体层，随第二步补。
    /// </remarks>
    private void RedrawArmies()
    {
        Castle? castle = _arena.Castle;

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
                    CollectRowUnit((row * BattleBoard.WidthInCells) + column, IsCellBeforeWall(row, column, wallCell), beforeWall, afterWall);
                }
            }
            else
            {
                for (int column = 0; column < BattleBoard.WidthInCells; ++column)
                {
                    CollectRowUnit((row * BattleBoard.WidthInCells) + column, isBeforeWall: true, beforeWall, afterWall);
                }
            }

            foreach (BattleUnit unit in beforeWall)
            {
                RedrawTroopSprite(unit);
            }

            foreach (BattleUnit unit in beforeWall)
            {
                RedrawTroopCount(unit);
            }

            // The wall goes between the troops in front of it and the ones behind it.
            if (castle is not null)
            {
                RedrawCastle(castle, wallCells[row]);
            }

            foreach (BattleUnit unit in afterWall)
            {
                RedrawTroopSprite(unit);
            }

            foreach (BattleUnit unit in afterWall)
            {
                RedrawTroopCount(unit);
            }
        }
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

        (isBeforeWall ? beforeWall : afterWall).Add(unit);
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
    /// Draws one unit at rest, <c>Interface::RedrawTroopSprite()</c> plus <c>_drawTroopSprite()</c>
    /// (<c>battle_interface.cpp:2028-2119</c>) without the moving, flying and moat-mask branches.
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

        ImageOps.BlitSprite(_surface, GetTroopPosition(unit, sprite), drawn);
    }

    /// <summary>
    /// Where the sprite of a unit goes, <c>GetTroopPosition()</c> (<c>battle_interface.cpp:502-527</c>).
    /// </summary>
    /// <param name="unit">The unit that owns the sprite.</param>
    /// <param name="sprite">The sprite itself, whose own offsets take part in the arithmetic.</param>
    private static Point GetTroopPosition(BattleUnit unit, Sprite sprite)
    {
        Rect area = unit.Position.GetRect();

        int offsetX;

        if (unit.IsReflect)
        {
            offsetX = unit.IsWide
                ? area.X + (area.Width / 2) + (area.Width / 4) - sprite.Width - sprite.OffsetX + 1
                : area.X + (area.Width / 2) - sprite.Width - sprite.OffsetX + 1;
        }
        else
        {
            offsetX = unit.IsWide
                ? area.X + (area.Width / 4) + sprite.OffsetX
                : area.X + (area.Width / 2) + sprite.OffsetX;
        }

        int offsetY = area.Y + area.Height + sprite.OffsetY + CellYOffset;

        return new Point(offsetX, offsetY);
    }

    /// <summary>
    /// The troop counter of a unit, <c>Interface::RedrawTroopCount()</c> (<c>battle_interface.cpp:2230-2267</c>).
    /// </summary>
    /// <param name="unit">The unit whose counter to draw.</param>
    /// <remarks>
    /// <para>
    /// 计数条是 <c>TEXTBAR</c> 的 10–14 号（10 紫、12 绿、13 黄、14 红，按单位身上的法术位挑），
    /// 数字是 <c>smallWhite</c> 的缩写形式（<c>Army::AbbreviateNumber</c>），压在条子正中偏下两像素。
    /// </para>
    /// <para>
    /// <b>未做</b>：血量条（<c>drawHitPointsBar</c>）。它要按游戏调色板取「绿 0,200,0 / 红 200,0,0」两个索引
    /// （<c>fheroes2::GetColorId</c>），而绘制器还没有调色板注入；这一项在设置里默认关闭
    /// （<c>Settings</c> 的默认选项不含 <c>BattleShowHitPointsBar</c>），等战斗设置窗那一步一起补。
    /// </para>
    /// </remarks>
    private void RedrawTroopCount(BattleUnit unit)
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

        ImageOps.BlitSprite(_surface, new Point(x + bar.OffsetX, y + bar.OffsetY), bar);

        if (_counterFont is null)
        {
            return;
        }

        string count = ArmyText.AbbreviateNumber(unit.Count);
        int width = GlyphText.Measure(_counterFont, count);

        GlyphText.DrawLine(_counterFont, _surface, new Point(x + ((bar.Width - width) / 2), y + 2), count);
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
    /// Draws the shadow under the cell the pointer is on, the static half of
    /// <c>Interface::RedrawCover()</c> (<c>battle_interface.cpp:2285-2474</c>) for the current unit.
    /// </summary>
    /// <remarks>
    /// 参考只在「有当前单位、设置里开着鼠标阴影」时画，且画在单位之下（`RedrawCover` 先于 `RedrawArmies`）。
    /// 高亮集合的其他分支（施法范围、攻击格、宽单位的两格）要行动交互的整套光标，随那一步补。
    /// </remarks>
    private void RedrawHoverShadow()
    {
        if (_hoveredCell is not { } index || !Settings.Get().IsBattleShowMouseShadow)
        {
            return;
        }

        if (_arena.CurrentUnit is null || _arena.Board.GetCell(index) is not { } cell)
        {
            return;
        }

        Sprite? shadow = EnsureHexagonCursorShadow();

        if (shadow is not null)
        {
            DrawFrame(shadow, _surface, cell.Position);
        }
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



