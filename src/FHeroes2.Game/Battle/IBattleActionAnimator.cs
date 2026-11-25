namespace FHeroes2.Game.Battle;

/// <summary>
/// The hooks the battle picture wants around one applied command, the two places
/// <c>Battle::Arena::ApplyAction*()</c> call <c>_interface</c> to play the animation
/// (<c>battle_action.cpp:198-210</c>, <c>battle_interface.cpp:1078-1200</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的结算与动画是交错的：<c>ApplyActionAttack()</c> 先让界面把行军与挥击播完，再掷骰结算，最后让界面播受击与死亡。
/// 端口把结算一口气做完，画面只能事后照 <see cref="BattleArena.LastAttackBlows"/> 回放——但「先走/先挥」与
/// 「后受击」必须仍然夹在结算两侧，否则单位会瞬移到目标格、伤害数字落在旧位置上。
/// </para>
/// <para>
/// 这两个回调是<b>阻塞</b>的：实现方在这里把一帧一帧跑到动画结束（参考同样在结算里阻塞），
/// 所以它们不该做任何规则上的事，只负责画。竞技场通过 <see cref="BattleArena.Animator"/> 拿到它，
/// 默认 <see langword="null"/> 表示没有人看，动作立即生效。
/// </para>
/// </remarks>
public interface IBattleActionAnimator
{
    /// <summary>
    /// Plays whatever has to happen before the command takes effect: the march of a move or an attack,
    /// the swing of a blow (<c>battle_interface.cpp:1078-1100</c>).
    /// </summary>
    /// <param name="unit">The unit whose turn it is.</param>
    /// <param name="command">The command about to be applied.</param>
    void BeforeAction(BattleUnit unit, BattleCommand command);

    /// <summary>
    /// Plays what the applied command did to the picture: the status line and the winces of the blows
    /// (<c>battle_interface.cpp:1153-1200</c>).
    /// </summary>
    /// <param name="blows">
    /// The blows of this command, empty when it was not an attack. A move or a spell passes an empty list.
    /// </param>
    void AfterAction(IReadOnlyList<BattleAttackBlow> blows);
}
