namespace FHeroes2.Game.Monsters;

/// <summary>
/// Which side of the creature a melee blow comes from, the reference's <c>Monster_Info::AttackDirection</c>
/// (<c>src/fheroes2/monster/monster_anim.h:28</c>).
/// </summary>
/// <remarks>
/// 只是 <c>melee[3]</c> 的下标，不是方向掩码——与 <c>Battle::AttackDirection</c>（八个方向的位环，见
/// <c>Battle.AttackDirection</c>）是两回事：那个说的是「站在哪一格」，这个说的是「图里朝哪边挥」。
/// </remarks>
public enum MonsterAttackDirection
{
    /// <summary>The blow comes from above, index 0.</summary>
    Top,

    /// <summary>The blow comes from the front, index 1.</summary>
    Front,

    /// <summary>The blow comes from below, index 2.</summary>
    Bottom,
}

/// <summary>
/// The set of animation sequences a unit can be in, the reference's <c>Monster_Info::AnimationType</c>
/// (<c>src/fheroes2/monster/monster_anim.h:36</c>).
/// </summary>
/// <remarks>
/// <para>
/// 前七项与 <see cref="MonsterAnimId"/> 的顺序不完全重合：这里是「战斗里要播什么」，那里是「.BIN 里存了
/// 哪几支」。参考在 <c>battle_animation.cpp</c> 里做映射（例如 <see cref="MoveStart"/> 拆成
/// <c>MoveStart + MoveTileStart</c>）。
/// </para>
/// <para>
/// <see cref="Invalid"/> 是给 <c>AnimationState::switchAnimation( { ... } )</c> 的返回值与「没有这支动画」
/// 用的哨兵，参考里它是第一个值之后的最后一个枚举项。
/// </para>
/// </remarks>
public enum MonsterAnimationType
{
    /// <summary>Nothing chosen yet, the reference's default argument.</summary>
    None,

    /// <summary>No animation at all.</summary>
    StandStill,

    /// <summary>The single-frame static pose.</summary>
    Static,

    /// <summary>A random idle cycle.</summary>
    Idle,

    /// <summary>First cycle of a move: flyers lift off.</summary>
    MoveStart,

    /// <summary>Middle cycles of a move.</summary>
    Moving,

    /// <summary>The arrival cycle.</summary>
    MoveEnd,

    /// <summary>The whole move in one cycle, for the units that have it.</summary>
    MoveQuick,

    /// <summary>Flying up: <see cref="MoveStart"/> plus the move cycles.</summary>
    FlyUp,

    /// <summary>Landing: the move cycles plus <see cref="MoveEnd"/>.</summary>
    FlyLand,

    /// <summary>Melee from above, first half.</summary>
    MeleeTop,

    /// <summary>Melee from above, second half.</summary>
    MeleeTopEnd,

    /// <summary>Melee from the front, first half.</summary>
    MeleeFront,

    /// <summary>Melee from the front, second half.</summary>
    MeleeFrontEnd,

    /// <summary>Melee from below, first half.</summary>
    MeleeBot,

    /// <summary>Melee from below, second half.</summary>
    MeleeBotEnd,

    /// <summary>Ranged attack upwards, first half.</summary>
    RangTop,

    /// <summary>Ranged attack upwards, second half.</summary>
    RangTopEnd,

    /// <summary>Ranged attack straight ahead, first half.</summary>
    RangFront,

    /// <summary>Ranged attack straight ahead, second half.</summary>
    RangFrontEnd,

    /// <summary>Ranged attack downwards, first half.</summary>
    RangBot,

    /// <summary>Ranged attack downwards, second half.</summary>
    RangBotEnd,

    /// <summary>First half of a wince.</summary>
    WnceUp,

    /// <summary>Second half of a wince.</summary>
    WnceDown,

    /// <summary>Both halves of a wince in one sequence.</summary>
    Wnce,

    /// <summary>The death sequence.</summary>
    Kill,

    /// <summary>The sentinel returned when a request produced no sequence.</summary>
    Invalid,
}
