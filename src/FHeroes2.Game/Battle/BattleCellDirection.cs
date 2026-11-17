namespace FHeroes2.Game.Battle;

/// <summary>
/// The six neighbours of a battlefield cell plus the cell itself, <c>Battle::CellDirection</c>
/// (<c>battle_cell.h:37-49</c>).
/// </summary>
/// <remarks>
/// <para>
/// The values are single bits precisely because the reference walks the ring by doubling:
/// <c>for ( dir = TOP_LEFT; dir &lt; CENTER; ++dir )</c> turns <c>1, 2, 4, 8, 16, 32</c> into
/// <c>TOP_LEFT, TOP_RIGHT, RIGHT, BOTTOM_RIGHT, BOTTOM_LEFT, LEFT</c> — the clockwise order, starting at the
/// top left. <see cref="BattleCellDirections.All"/> is that same order, and
/// <see cref="BattleCellDirections.Next"/> is the C# stand-in for <c>operator++</c>.
/// </para>
/// <para>
/// <see cref="RightSide"/> and <see cref="LeftSide"/> are masks, not directions, so they must never be fed to
/// <see cref="BattleBoard.GetIndexDirection"/>; <see cref="BattleCellDirections.IsLeftSide"/> and
/// <see cref="BattleCellDirections.IsRightSide"/> are the only consumers.
/// </para>
/// </remarks>
[Flags]
public enum CellDirection
{
    /// <summary>No direction: the two indexes are not neighbours, or one of them is off the board.</summary>
    Unknown = 0x00,

    /// <summary><c>TOP_LEFT</c>.</summary>
    TopLeft = 0x01,

    /// <summary><c>TOP_RIGHT</c>.</summary>
    TopRight = 0x02,

    /// <summary><c>RIGHT</c>.</summary>
    Right = 0x04,

    /// <summary><c>BOTTOM_RIGHT</c>.</summary>
    BottomRight = 0x08,

    /// <summary><c>BOTTOM_LEFT</c>.</summary>
    BottomLeft = 0x10,

    /// <summary><c>LEFT</c>.</summary>
    Left = 0x20,

    /// <summary><c>CENTER</c>: the same cell. It ends the ring, so it is the only value above <see cref="Left"/>.</summary>
    Center = 0x40,

    /// <summary><c>RIGHT_SIDE</c>: the three neighbours to the right of the cell.</summary>
    RightSide = TopRight | Right | BottomRight,

    /// <summary><c>LEFT_SIDE</c>: the three neighbours to the left of the cell.</summary>
    LeftSide = TopLeft | Left | BottomLeft,
}

/// <summary>
/// The eight directions an attack can come from, <c>Battle::AttackDirection</c>
/// (<c>battle_cell.h:71-83</c>).
/// </summary>
/// <remarks>
/// A different ring from <see cref="CellDirection"/>: there are eight of them because a blow can land from
/// straight above or straight below, which the hex grid itself does not have. <c>CENTER</c> sits at 0x100 rather
/// than 0x40, again so that doubling walks the ring.
/// </remarks>
[Flags]
public enum AttackDirection
{
    /// <summary>No direction.</summary>
    Unknown = 0x00,

    /// <summary><c>TOP_LEFT</c>.</summary>
    TopLeft = 0x01,

    /// <summary><c>TOP</c>: only reachable on the attack ring, not on the movement ring.</summary>
    Top = 0x02,

    /// <summary><c>TOP_RIGHT</c>.</summary>
    TopRight = 0x04,

    /// <summary><c>RIGHT</c>.</summary>
    Right = 0x08,

    /// <summary><c>BOTTOM_RIGHT</c>.</summary>
    BottomRight = 0x10,

    /// <summary><c>BOTTOM</c>: only reachable on the attack ring.</summary>
    Bottom = 0x20,

    /// <summary><c>BOTTOM_LEFT</c>.</summary>
    BottomLeft = 0x40,

    /// <summary><c>LEFT</c>.</summary>
    Left = 0x80,

    /// <summary><c>CENTER</c>: a blow that lands on the cell the unit stands on.</summary>
    Center = 0x100,
}

/// <summary>
/// The ring arithmetic of <c>CellDirection</c> and <c>AttackDirection</c>
/// (<c>battle_cell.h:51-113</c>, <c>battle_board.h:49-65</c>).
/// </summary>
public static class BattleCellDirections
{
    /// <summary>
    /// The movement ring in clockwise order, <see cref="CellDirection.TopLeft"/> first — the order
    /// <c>operator++</c> produces and the order every loop in the reference relies on.
    /// </summary>
    public static readonly CellDirection[] All =
    [
        CellDirection.TopLeft,
        CellDirection.TopRight,
        CellDirection.Right,
        CellDirection.BottomRight,
        CellDirection.BottomLeft,
        CellDirection.Left,
    ];

    /// <summary>
    /// The attack ring in clockwise order, <see cref="AttackDirection.TopLeft"/> first.
    /// </summary>
    public static readonly AttackDirection[] AllAttackDirections =
    [
        AttackDirection.TopLeft,
        AttackDirection.Top,
        AttackDirection.TopRight,
        AttackDirection.Right,
        AttackDirection.BottomRight,
        AttackDirection.Bottom,
        AttackDirection.BottomLeft,
        AttackDirection.Left,
    ];

    /// <summary>
    /// <c>operator++( CellDirection &amp; )</c>: clockwise, wrapping from <see cref="CellDirection.Left"/> back to
    /// the centre.
    /// </summary>
    /// <param name="direction">The current direction.</param>
    /// <returns>The next direction clockwise.</returns>
    public static CellDirection Next(CellDirection direction) =>
        direction == CellDirection.Center ? CellDirection.TopLeft : (CellDirection)((int)direction << 1);

    /// <summary>
    /// <c>operator--( CellDirection &amp; )</c>: counter-clockwise, wrapping from
    /// <see cref="CellDirection.TopLeft"/> to the centre.
    /// </summary>
    /// <param name="direction">The current direction.</param>
    /// <returns>The next direction counter-clockwise.</returns>
    public static CellDirection Previous(CellDirection direction) =>
        direction == CellDirection.TopLeft ? CellDirection.Center : (CellDirection)((int)direction >> 1);

    /// <summary>
    /// <c>operator++( AttackDirection &amp; )</c>: clockwise, wrapping from <see cref="AttackDirection.Left"/> back
    /// to the centre.
    /// </summary>
    /// <param name="direction">The current direction.</param>
    /// <returns>The next direction clockwise.</returns>
    public static AttackDirection Next(AttackDirection direction) =>
        direction == AttackDirection.Center ? AttackDirection.TopLeft : (AttackDirection)((int)direction << 1);

    /// <summary>
    /// <c>operator--( AttackDirection &amp; )</c>: counter-clockwise, wrapping from
    /// <see cref="AttackDirection.TopLeft"/> to the centre.
    /// </summary>
    /// <param name="direction">The current direction.</param>
    /// <returns>The next direction counter-clockwise.</returns>
    public static AttackDirection Previous(AttackDirection direction) =>
        direction == AttackDirection.TopLeft ? AttackDirection.Center : (AttackDirection)((int)direction >> 1);

    /// <summary>
    /// <c>isLeftSide()</c>: is any of the three left-hand neighbours set?
    /// </summary>
    /// <param name="direction">A direction or a mask of directions.</param>
    /// <returns><see langword="true"/> if the mask intersects <see cref="CellDirection.LeftSide"/>.</returns>
    public static bool IsLeftSide(CellDirection direction) => (direction & CellDirection.LeftSide) != 0;

    /// <summary>
    /// <c>isRightSide()</c>: is any of the three right-hand neighbours set?
    /// </summary>
    /// <param name="direction">A direction or a mask of directions.</param>
    /// <returns><see langword="true"/> if the mask intersects <see cref="CellDirection.RightSide"/>.</returns>
    public static bool IsRightSide(CellDirection direction) => (direction & CellDirection.RightSide) != 0;

    /// <summary>
    /// <c>asAttackDirection()</c> (<c>battle_cell.h:95-113</c>): the six movement directions that also exist on the
    /// attack ring.
    /// </summary>
    /// <remarks>
    /// <see cref="CellDirection.Center"/>, <see cref="CellDirection.Unknown"/> and the two masks have no attack
    /// direction and answer <see cref="AttackDirection.Unknown"/>, as the reference's <c>default</c> does.
    /// </remarks>
    /// <param name="direction">A movement direction.</param>
    /// <returns>The matching attack direction, or <see cref="AttackDirection.Unknown"/>.</returns>
    public static AttackDirection AsAttackDirection(CellDirection direction) => direction switch
    {
        CellDirection.TopLeft => AttackDirection.TopLeft,
        CellDirection.TopRight => AttackDirection.TopRight,
        CellDirection.Right => AttackDirection.Right,
        CellDirection.BottomRight => AttackDirection.BottomRight,
        CellDirection.BottomLeft => AttackDirection.BottomLeft,
        CellDirection.Left => AttackDirection.Left,
        _ => AttackDirection.Unknown,
    };
}
