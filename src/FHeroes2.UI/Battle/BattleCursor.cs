using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Monsters;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The cursor and the status hint of one hover over the battlefield, the pair
/// <c>Interface::GetBattleCursor()</c> answers with.
/// </summary>
/// <param name="Theme">Which cursor image the hover shows.</param>
/// <param name="Status">The lower status line while the pointer rests there.</param>
public readonly record struct BattleHover(CursorTheme Theme, string Status);

/// <summary>
/// The cursor a hover over the battlefield shows and the hint under it,
/// <c>Battle::Interface::GetBattleCursor()</c> (<c>battle_interface.cpp:2922-3064</c>) plus
/// <c>getSwordCursorForAttackDirection()</c> (<c>:637-661</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么单独一层</b>：参考把这段写在界面里，但它只问竞技场、当前单位和鼠标下的格子，不碰一张图，
/// 于是和 <see cref="BattleSpellCursor"/> 一样搬出来做成纯函数，让 <c>tests/FHeroes2.UI.Tests</c> 能直接
/// 对着一个竞技场问答案。
/// </para>
/// <para>
/// <b>八把剑的来历</b>：光标形状按「刀从哪一侧砍过来」挑，而这一侧由鼠标落在格子八片花瓣里的哪一片决定
/// （<see cref="BattleCell.GetTriangleDirection"/>）。六边形的格子只有六个邻格，正上、正下两片是宽单位
/// （两格身体）才够得着的方向，所以要先算出「这一格实际可攻击的方向集合」，再把鼠标指的方向吸附到其中
/// 最近的一个——先顺时针找，再逆时针。
/// </para>
/// <para>
/// <b>与参考的差异</b>：文案不经过翻译层（<c>_( "…" )</c>），与端口其余界面一致；参考在这里顺带记住
/// <c>_highlightUnitMovementArea</c>（鼠标下的兵），端口的行动区高亮由
/// <see cref="BattleFieldPainter"/> 自己管，不放进来。
/// </para>
/// </remarks>
public static class BattleCursor
{
    /// <summary>The hint of a cell nothing can be done with, <c>_( "Turn %{turn}" )</c> without the number.</summary>
    private const string ViewInfoTemplate = "View %{monster} info";

    /// <summary>The placeholder the hint templates spell a creature's name with.</summary>
    private const string MonsterPlaceholder = "%{monster}";

    /// <summary>
    /// The cursor and the hint of a hover, <c>Interface::GetBattleCursor()</c>
    /// (<c>battle_interface.cpp:2922-3064</c>).
    /// </summary>
    /// <param name="arena">The battle, asked about reachability, archery and the round number.</param>
    /// <param name="currentUnit">The unit whose turn it is; the reference calls it <c>_currentUnit</c>.</param>
    /// <param name="hoveredCell">The cell under the pointer, or an invalid index when the pointer is off the board.</param>
    /// <param name="cursor">The pointer in the same coordinates as the cells.</param>
    /// <returns>The cursor to show and the lower status line to write.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arena"/> or <paramref name="currentUnit"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 顺序照参考：自己的格子只看能不能落（落在脚下就是「查看」），别人的格子依次问「同阵营 → 查看」、
    /// 「自己是弓手且没被贴身 → 射击」、最后才走攻击方向那一支。
    /// </remarks>
    public static BattleHover Describe(BattleArena arena, BattleUnit currentUnit, int hoveredCell, Point cursor)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(currentUnit);

        if (arena.Board.GetCell(hoveredCell) is not { } cell)
        {
            return new BattleHover(CursorTheme.CombatNone, Turn(arena));
        }

        BattleUnit? unit = cell.Unit;

        if (unit is null || ReferenceEquals(unit, currentUnit))
        {
            BattlePosition position = BattlePosition.GetReachable(arena, currentUnit, hoveredCell);

            if (position.Head is null)
            {
                return new BattleHover(CursorTheme.CombatNone, Turn(arena));
            }

            if (position.Head.Index == currentUnit.HeadIndex)
            {
                return new BattleHover(CursorTheme.CombatInfo, ViewInfo(currentUnit));
            }

            return currentUnit.IsFlying
                ? new BattleHover(CursorTheme.CombatFly, MoveHere("Fly", currentUnit))
                : new BattleHover(CursorTheme.CombatMove, MoveHere("Move", currentUnit));
        }

        if (currentUnit.GetCurrentColor() == unit.GetCurrentColor())
        {
            return new BattleHover(CursorTheme.CombatInfo, ViewInfo(unit));
        }

        if (currentUnit.IsArchers && !currentUnit.IsHandFighting())
        {
            CursorTheme theme = arena.IsShootingPenalty(currentUnit, unit)
                ? CursorTheme.CombatBrokenArrow
                : CursorTheme.CombatArrow;

            return new BattleHover(theme, Shoot(unit, currentUnit.GetShots()));
        }

        HashSet<AttackDirection> availableDirections = AvailableAttackDirections(arena, currentUnit, hoveredCell);

        if (availableDirections.Count == 0)
        {
            return new BattleHover(CursorTheme.CombatNone, Turn(arena));
        }

        AttackDirection direction = cell.GetTriangleDirection(cursor);

        if (direction == AttackDirection.Unknown)
        {
            // 参考的注释：别的窗口弹出来、玩家又挪了鼠标时会落到这个分支。
            direction = AttackDirection.Center;
        }

        return new BattleHover(SwordFor(Nearest(direction, availableDirections)), Attack(unit));
    }

    /// <summary>
    /// The attack directions a unit can strike the hovered cell from, the <c>availableAttackDirections</c> set of
    /// <c>GetBattleCursor()</c> (<c>battle_interface.cpp:2980-3015</c>).
    /// </summary>
    /// <param name="arena">The battle, for the moat rule of <see cref="BattleBoard.CanAttackFromCell"/>.</param>
    /// <param name="unit">The unit that would strike.</param>
    /// <param name="targetCell">The cell holding the defender.</param>
    /// <returns>The directions with at least one usable standing cell; empty when the hit is out of reach.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="arena"/> or <paramref name="unit"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 六个邻格各自问「从这个邻格出刀允许吗」——注意问的是<b>目标格</b>的邻格，不是攻方的邻格：集合描述的
    /// 是刀从哪边来。宽单位还多两支：身体占两格，正上方/正下方的邻格也够得着，条件是那个邻格能整个站下它
    /// （头就落在那里，而不是被挤到尾巴上）。
    /// </remarks>
    public static HashSet<AttackDirection> AvailableAttackDirections(BattleArena arena, BattleUnit unit, int targetCell)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(unit);

        HashSet<AttackDirection> available = [];

        foreach (CellDirection direction in BattleCellDirections.All)
        {
            if (BattleBoard.IsValidDirection(targetCell, direction)
                && BattleBoard.CanAttackFromCell(arena, unit, BattleBoard.GetIndexDirection(targetCell, direction)))
            {
                available.Add(BattleCellDirections.AsAttackDirection(direction));
            }
        }

        if (!unit.IsWide)
        {
            return available;
        }

        // 身体朝右时「正上方」的那一刀要斜着往右上踏；朝左时反过来，见参考 :3004-3013。
        CellDirection top = unit.IsReflect ? CellDirection.TopLeft : CellDirection.TopRight;
        if (CanStrikeFromTopOrBottom(arena, unit, targetCell, top, CellDirection.TopLeft, CellDirection.TopRight))
        {
            available.Add(AttackDirection.Top);
        }

        CellDirection bottom = unit.IsReflect ? CellDirection.BottomLeft : CellDirection.BottomRight;
        if (CanStrikeFromTopOrBottom(arena, unit, targetCell, bottom, CellDirection.BottomLeft, CellDirection.BottomRight))
        {
            available.Add(AttackDirection.Bottom);
        }

        return available;
    }

    /// <summary>
    /// Picks the direction the sword points at: the mouse's own when it is available, else the nearest one
    /// (<c>battle_interface.cpp:3024-3048</c>).
    /// </summary>
    /// <param name="wanted">The direction the pointer points at; <see cref="AttackDirection.Center"/> when unknown.</param>
    /// <param name="available">The directions the unit can strike from; must not be empty.</param>
    /// <returns>The direction to show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="available"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// 只有一个方向时直接用；否则从鼠标方向出发交替试顺时针与逆时针，<b>先顺时针</b>——参考的循环就是这个
    /// 次序，两边同时有解时选顺时针那一个。
    /// </remarks>
    public static AttackDirection Nearest(AttackDirection wanted, IReadOnlySet<AttackDirection> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        if (available.Count == 0)
        {
            return AttackDirection.Unknown;
        }

        if (available.Contains(wanted))
        {
            return wanted;
        }

        if (available.Count == 1)
        {
            return available.First();
        }

        AttackDirection clockWise = wanted;
        AttackDirection antiClockWise = wanted;

        while (true)
        {
            clockWise = BattleCellDirections.Next(clockWise);
            if (available.Contains(clockWise))
            {
                return clockWise;
            }

            antiClockWise = BattleCellDirections.Previous(antiClockWise);
            if (available.Contains(antiClockWise))
            {
                return antiClockWise;
            }
        }
    }

    /// <summary>
    /// The sword cursor of an attack direction, <c>getSwordCursorForAttackDirection()</c>
    /// (<c>battle_interface.cpp:637-661</c>).
    /// </summary>
    /// <param name="direction">The direction the blow comes from.</param>
    /// <returns>The sword cursor, or <see cref="CursorTheme.CombatNone"/> for the centre and the unknown.</returns>
    /// <remarks>
    /// 表是「反过来」的：刀从右下砍来，画的是朝左上那把剑。正上方/正下方只有宽单位打得出，对应竖着的那两把。
    /// </remarks>
    public static CursorTheme SwordFor(AttackDirection direction) => direction switch
    {
        AttackDirection.BottomRight => CursorTheme.SwordTopLeft,
        AttackDirection.BottomLeft => CursorTheme.SwordTopRight,
        AttackDirection.Right => CursorTheme.SwordLeft,
        AttackDirection.TopRight => CursorTheme.SwordBottomLeft,
        AttackDirection.TopLeft => CursorTheme.SwordBottomRight,
        AttackDirection.Left => CursorTheme.SwordRight,
        AttackDirection.Bottom => CursorTheme.SwordTop,
        AttackDirection.Top => CursorTheme.SwordBottom,
        _ => CursorTheme.CombatNone,
    };

    /// <summary>
    /// The side a sword cursor says the blow comes from, the inverse of <see cref="SwordFor"/>.
    /// </summary>
    /// <param name="theme">The cursor under the pointer.</param>
    /// <returns>The direction, or <see cref="AttackDirection.Unknown"/> for anything but a sword.</returns>
    /// <remarks>
    /// 高亮攻击落点要用它：参考在 <c>RedrawCover()</c> 里把光标反查成方向，再问
    /// <c>calculateAttackPosition()</c> 站到哪一格（<c>battle_interface.cpp:2378-2411</c>）。
    /// </remarks>
    public static AttackDirection AttackDirectionOf(CursorTheme theme) => theme switch
    {
        CursorTheme.SwordTopLeft => AttackDirection.BottomRight,
        CursorTheme.SwordTopRight => AttackDirection.BottomLeft,
        CursorTheme.SwordTop => AttackDirection.Bottom,
        CursorTheme.SwordLeft => AttackDirection.Right,
        CursorTheme.SwordRight => AttackDirection.Left,
        CursorTheme.SwordBottomLeft => AttackDirection.TopRight,
        CursorTheme.SwordBottomRight => AttackDirection.TopLeft,
        CursorTheme.SwordBottom => AttackDirection.Top,
        _ => AttackDirection.Unknown,
    };

    /// <summary>Whether a wide unit's head lands on the cell above or below the target, <c>checkTopBottom()</c>.</summary>
    /// <param name="arena">The battle, for the reachability question.</param>
    /// <param name="unit">The wide unit that would strike.</param>
    /// <param name="targetCell">The cell holding the defender.</param>
    /// <param name="standingCell">The neighbour the head has to land on.</param>
    /// <param name="leftSide">The target's upper/lower left neighbour, which has to exist.</param>
    /// <param name="rightSide">The target's upper/lower right neighbour, which has to exist.</param>
    /// <returns><see langword="true"/> when the standing cell takes the head this turn.</returns>
    /// <remarks>
    /// 参考要求目标格的两个斜邻都在盘上（宽单位的身体得横着让开），再问「站得下而且不是把尾巴塞过去」：
    /// 尾巴落在目标旁的那一刀已经由上面六个邻格覆盖了。
    /// </remarks>
    private static bool CanStrikeFromTopOrBottom(BattleArena arena, BattleUnit unit, int targetCell,
        CellDirection standingCell, CellDirection leftSide, CellDirection rightSide)
    {
        if (!BattleBoard.IsValidDirection(targetCell, standingCell)
            || !BattleBoard.IsValidDirection(targetCell, leftSide)
            || !BattleBoard.IsValidDirection(targetCell, rightSide))
        {
            return false;
        }

        int destination = BattleBoard.GetIndexDirection(targetCell, standingCell);
        BattlePosition position = BattlePosition.GetReachable(arena, unit, destination);

        return position.Head is { } head && head.Index == destination;
    }

    /// <summary>The hint of a hover that only reports the round, <c>_( "Turn %{turn}" )</c>.</summary>
    /// <param name="arena">The battle.</param>
    /// <returns>The sentence with the round number spelled in.</returns>
    private static string Turn(BattleArena arena) => $"Turn {arena.TurnNumber}";

    /// <summary>The hint of hovering a stack, <c>_( "View %{monster} info" )</c>.</summary>
    /// <param name="unit">The stack under the pointer.</param>
    /// <returns>The sentence with the creature's plural name spelled in, lowercased.</returns>
    private static string ViewInfo(BattleUnit unit) =>
        Army.StringReplaceWithLowercase(ViewInfoTemplate, MonsterPlaceholder, MonsterNames.GetMulti(unit.Monster));

    /// <summary>The hint of a cell the acting unit can walk to, <c>_( "Move %{monster} here" )</c>.</summary>
    /// <param name="verb">"Move" or "Fly".</param>
    /// <param name="unit">The unit that would walk.</param>
    /// <returns>The sentence with the creature's singular name spelled in, lowercased.</returns>
    private static string MoveHere(string verb, BattleUnit unit) =>
        Army.StringReplaceWithLowercase($"{verb} {MonsterPlaceholder} here", MonsterPlaceholder,
            MonsterNames.GetSingular(unit.Monster));

    /// <summary>The hint of an attack, <c>_( "Attack %{monster}" )</c>.</summary>
    /// <param name="unit">The stack under the pointer.</param>
    /// <returns>The sentence with the creature's singular name spelled in, lowercased.</returns>
    private static string Attack(BattleUnit unit) =>
        Army.StringReplaceWithLowercase($"Attack {MonsterPlaceholder}", MonsterPlaceholder,
            MonsterNames.GetSingular(unit.Monster));

    /// <summary>
    /// The hint of a shot, <c>_( "Shoot %{monster}" )</c> plus the ammunition sentence
    /// (<c>_n( "(1 shot left)", "(%{count} shots left)", count )</c>).
    /// </summary>
    /// <param name="unit">The stack under the pointer.</param>
    /// <param name="shots">The arrows the shooting unit has left.</param>
    /// <returns>The sentence with the creature's plural name and the arrow count spelled in.</returns>
    private static string Shoot(BattleUnit unit, uint shots)
    {
        string ammunition = shots == 1 ? "(1 shot left)" : "(%{count} shots left)";
        string message = Army.StringReplaceWithLowercase($"Shoot {MonsterPlaceholder} {ammunition}",
            MonsterPlaceholder, MonsterNames.GetMulti(unit.Monster));

        return message.Replace("%{count}", shots.ToString(), StringComparison.Ordinal);
    }
}
