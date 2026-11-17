namespace FHeroes2.Game.Battle;

/// <summary>
/// Hands out the unique ids of the stacks in one battle, <c>Battle::TroopsUidGenerator</c>
/// (<c>battle_arena.h:77-93</c>).
/// </summary>
/// <remarks>
/// 参考把它塞在竞技场的头文件里，因为它只是竞技场的一个计数器、十几行而已；端口的 <see cref="BattleForce"/> 在
/// 构造时就要拿号，所以单独成类，方便测试里也给得出来。
/// <para>
/// 第一个号是 1：参考的 <c>_id</c> 初值是 1、返回 <c>_id++</c>，而 <c>0</c> 被 <c>Force::uids</c> 的空槽当成
/// 「这里没有单位」的哨兵。别把它改成从零开始——那会让空槽与第一个单位撞号。
/// </para>
/// </remarks>
public sealed class BattleTroopsUidGenerator
{
    private uint _id = 1;

    /// <summary>Hands out the next free id, <c>GetUnique()</c>.</summary>
    public uint GetUnique() => _id++;
}
