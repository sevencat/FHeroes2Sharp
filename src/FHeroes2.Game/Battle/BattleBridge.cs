using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Battle;

/// <summary>
/// The drawbridge of a besieged town, <c>Battle::Bridge</c> (<c>battle_bridge.h:33-91</c>).
/// </summary>
/// <remarks>
/// <para>
/// 桥只有两种状态（升起/放下）加一种终态（被投石车砸断），但它决定三件事：能不能走过去
/// （<see cref="IsPassable"/>）、守城方要不要在出发前把它放下来（<see cref="NeedDown"/>）、以及
/// <see cref="AllowUp"/> 能不能再把它拉起来。
/// </para>
/// <para>
/// 参考从 <c>Arena</c> 单例里取三样东西：格盘（<c>Board::GetCell</c>）、坟场与守城方的颜色
/// （<c>Arena::GetCastle()->GetColor()</c>）。端口把前两样做成构造参数，颜色做成可写属性——竞技场建好城堡之后
/// 再设置它，测试则可以随手指定。
/// </para>
/// <para>
/// 动画（<c>Interface::RedrawBridgeAnimation</c>）不在这里：<see cref="ActionUp"/>/<see cref="ActionDown"/> 只改状态并
/// 返回「到底动没动」，要不要重绘由调用方决定。参考用 <c>assert</c> 卡住前置条件，端口改成返回 <see langword="false"/>。
/// </para>
/// </remarks>
public sealed class BattleBridge
{
    /// <summary>The cell above the bridge, <c>CELL_ABOVE_BRIDGE</c>.</summary>
    public const int CellAboveBridge = 39;

    /// <summary>The cell in front of the moat, <c>CELL_BEFORE_MOAT</c>.</summary>
    public const int CellBeforeMoat = 48;

    /// <summary>The moat cell the bridge covers, <c>CELL_MOAT</c>.</summary>
    public const int CellMoat = 49;

    /// <summary>The gate cell, <c>CELL_GATES</c> — the one whose passability the bridge drives.</summary>
    public const int CellGates = 50;

    /// <summary>The cell behind the gates, <c>CELL_AFTER_GATES</c>.</summary>
    public const int CellAfterGates = 51;

    /// <summary>The cell below the bridge, <c>CELL_BELOW_BRIDGE</c>.</summary>
    public const int CellBelowBridge = 61;

    private readonly BattleBoardCells _board;
    private readonly BattleGraveyard _graveyard;

    private bool _isDestroyed;
    private bool _isDown;

    /// <summary>Builds the bridge of a town.</summary>
    /// <param name="board">The battlefield whose gate cell the bridge drives.</param>
    /// <param name="graveyard">The ground the bridge has to look at before it may move.</param>
    /// <param name="castleColor">
    /// The colour of the town that owns the bridge, <c>Arena::GetCastle()->GetColor()</c>; the defender may cross a
    /// raised bridge and, when it is not wrecked, may lower it.
    /// </param>
    public BattleBridge(BattleBoardCells board, BattleGraveyard graveyard, PlayerColor castleColor = PlayerColor.None)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(graveyard);

        _board = board;
        _graveyard = graveyard;
        CastleColor = castleColor;
    }

    /// <summary>The owning town's colour, <c>Arena::GetCastle()->GetColor()</c>.</summary>
    public PlayerColor CastleColor { get; set; }

    /// <summary>Whether the bridge can still be moved, <c>isValid()</c>: not wrecked.</summary>
    public bool IsValid => !_isDestroyed;

    /// <summary>Whether the bridge is wrecked, <c>isDestroyed()</c>.</summary>
    public bool IsDestroyed => _isDestroyed;

    /// <summary>Whether the bridge lies down, <c>isDown()</c>; a wrecked bridge is always down.</summary>
    public bool IsDown => _isDown;

    /// <summary>
    /// Whether the bridge may be raised, <c>AllowUp()</c>: intact, down, and nothing on or under it.
    /// </summary>
    public bool AllowUp => IsValid && IsDown && !IsOccupied;

    /// <summary>
    /// Whether the bridge has to be lowered before the unit moves, <c>NeedDown()</c>.
    /// </summary>
    /// <param name="unit">The unit that is about to move.</param>
    /// <param name="destinationIndex">The cell it is moving to.</param>
    /// <returns><see langword="true"/> when the move requires lowering the bridge first.</returns>
    /// <remarks>
    /// 参考只给三种落点开绿灯：飞兵落闸门、地面兵落闸门且当前在闸门之后/桥下/桥上、落护城河而当前不在闸门；
    /// 宽单位还要多一条「落护城河前、尾巴在桥的两侧」。其余一律不动桥——桥不为了让人走而白放下来。
    /// </remarks>
    public bool NeedDown(BattleUnit unit, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(unit);

        // 桥断了、已经放下、这队兵不是守城方的，或者桥上桥下有人（活的死的都算），都不动。
        if (!IsValid || IsDown || unit.Color != CastleColor || IsOccupied)
        {
            return false;
        }

        if (unit.IsFlying)
        {
            return destinationIndex == CellGates;
        }

        int headIndex = unit.HeadIndex;

        if (destinationIndex == CellGates
            && (headIndex == CellAfterGates || headIndex == CellBelowBridge || headIndex == CellAboveBridge))
        {
            return true;
        }

        if (destinationIndex == CellMoat && headIndex != CellGates)
        {
            return true;
        }

        if (unit.IsWide)
        {
            int tailIndex = unit.TailIndex;

            if (destinationIndex == CellBeforeMoat && (tailIndex == CellBelowBridge || tailIndex == CellAboveBridge))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the unit may walk into the gate, <c>isPassable()</c>: the bridge is down, or the unit is the town's own
    /// and nothing lies on or under the bridge.
    /// </summary>
    /// <param name="unit">The unit that wants to cross.</param>
    public bool IsPassable(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return IsDown || (unit.Color == CastleColor && !IsOccupied);
    }

    /// <summary>
    /// Wrecks the bridge, <c>SetDestroyed()</c>: it falls down for good and the gate becomes plain ground.
    /// </summary>
    public void SetDestroyed()
    {
        _isDestroyed = true;
        _isDown = true;

        SetGateObject(0);
    }

    /// <summary>
    /// Writes the bridge's verdict into the gate cell, <c>SetPassability()</c>: 0 for crossable, 1 for blocked.
    /// </summary>
    /// <param name="unit">The unit the question is asked for.</param>
    /// <remarks>
    /// 参考把「能不能过」缓存进格子的 <c>objectType</c>，寻路只看 0/1 不回头问桥。所以这里必须由调用方在
    /// 每次相关状态变化后重算一次，否则格盘会说旧话。
    /// </remarks>
    public void SetPassability(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        SetGateObject(IsPassable(unit) ? 0 : 1);
    }

    /// <summary>
    /// Raises the bridge, <c>ActionUp()</c>.
    /// </summary>
    /// <returns><see langword="false"/> when <see cref="AllowUp"/> is not met and nothing happened.</returns>
    public bool ActionUp()
    {
        if (!AllowUp)
        {
            return false;
        }

        _isDown = false;

        return true;
    }

    /// <summary>
    /// Lowers the bridge, <c>ActionDown()</c>.
    /// </summary>
    /// <returns><see langword="false"/> when the bridge is wrecked, already down, or occupied.</returns>
    public bool ActionDown()
    {
        if (!IsValid || IsDown || IsOccupied)
        {
            return false;
        }

        _isDown = true;

        return true;
    }

    /// <summary>
    /// Whether any stack — alive or dead — stands on the moat or in the gate, <see cref="IsOccupied"/>.
    /// </summary>
    private bool IsOccupied
    {
        get
        {
            bool occupied = _board.GetCell(CellMoat)?.Unit != null || _board.GetCell(CellGates)?.Unit != null;

            if (occupied)
            {
                return true;
            }

            return _graveyard.GetLastUnit(CellMoat) != null || _graveyard.GetLastUnit(CellGates) != null;
        }
    }

    private void SetGateObject(int value)
    {
        BattleCell? gates = _board.GetCell(CellGates);

        if (gates != null)
        {
            gates.Object = value;
        }
    }
}
