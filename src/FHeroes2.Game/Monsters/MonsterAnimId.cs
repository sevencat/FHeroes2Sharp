namespace FHeroes2.Game.Monsters;

/// <summary>
/// The animation tracks of a creature, the reference's <c>Bin_Info::MonsterAnimInfo::ANIM_TYPE</c>
/// (<c>src/fheroes2/agg/bin_info.h:34</c>). The numbers are the column indices of the <c>.BIN</c> record,
/// so they are part of the file format.
/// </summary>
/// <remarks>
/// 参考嵌套在 <c>MonsterAnimInfo</c> 里；端口按 <see cref="MonsterType"/> 的同一做法提到命名空间层，
/// 因为 <c>.BIN</c> 里按这个序号存放「每支动画有几帧」「每帧是第几号图」。
/// </remarks>
public enum MonsterAnimId
{
    /// <summary>Start of the moving sequence on the first animation cycle: flyers fly up.</summary>
    MoveStart,

    /// <summary>Played at the beginning of the second and later tiles of a move.</summary>
    MoveTileStart,

    /// <summary>Core animation. Most units only have this one.</summary>
    MoveMain,

    /// <summary>Cavalry and wolf: the end of a cycle, but not of the last tile.</summary>
    MoveTileEnd,

    /// <summary>End of the moving sequence on arrival, the landing for example.</summary>
    MoveStop,

    /// <summary>Used when moving one tile; Lich and Power Lich have none and use <see cref="MoveMain"/>.</summary>
    MoveOne,

    /// <summary>Empty placeholder for an animation built from the previous parts.</summary>
    Temporary,

    /// <summary>Frame 1.</summary>
    Static,

    /// <summary>First idle animation.</summary>
    Idle1,

    /// <summary>Idle animations: picked at random with different weights, rarely all five are present.</summary>
    Idle2,

    /// <summary>Third idle animation.</summary>
    Idle3,

    /// <summary>Fourth idle animation.</summary>
    Idle4,

    /// <summary>Fifth idle animation.</summary>
    Idle5,

    /// <summary>Death.</summary>
    Death,

    /// <summary>Start of the wince (being hit) animation.</summary>
    WinceUp,

    /// <summary>End of the wince animation.</summary>
    WinceEnd,

    /// <summary>Attack towards the top.</summary>
    Attack1,

    /// <summary>End of <see cref="Attack1"/>.</summary>
    Attack1End,

    /// <summary>Two-hex attack towards the top.</summary>
    DoubleHex1,

    /// <summary>End of <see cref="DoubleHex1"/>.</summary>
    DoubleHex1End,

    /// <summary>Attack towards the center.</summary>
    Attack2,

    /// <summary>End of <see cref="Attack2"/>.</summary>
    Attack2End,

    /// <summary>Two-hex attack towards the center.</summary>
    DoubleHex2,

    /// <summary>End of <see cref="DoubleHex2"/>.</summary>
    DoubleHex2End,

    /// <summary>Attack towards the bottom.</summary>
    Attack3,

    /// <summary>End of <see cref="Attack3"/>.</summary>
    Attack3End,

    /// <summary>Two-hex attack towards the bottom.</summary>
    DoubleHex3,

    /// <summary>End of <see cref="DoubleHex3"/>.</summary>
    DoubleHex3End,

    /// <summary>Ranged attack towards the top.</summary>
    Shoot1,

    /// <summary>End of <see cref="Shoot1"/>.</summary>
    Shoot1End,

    /// <summary>Ranged attack towards the center.</summary>
    Shoot2,

    /// <summary>End of <see cref="Shoot2"/>.</summary>
    Shoot2End,

    /// <summary>Ranged attack towards the bottom.</summary>
    Shoot3,

    /// <summary>End of <see cref="Shoot3"/>; the last track a <c>.BIN</c> record holds.</summary>
    Shoot3End,
}
