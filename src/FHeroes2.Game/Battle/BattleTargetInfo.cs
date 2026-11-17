namespace FHeroes2.Game.Battle;

/// <summary>
/// One target of a spell and what the spell did to it, <c>Battle::TargetInfo</c> (<c>battle.h:70-84</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考是一个带四个公开字段的小结构体，法术应用时按引用传下去：<c>damage</c> 既是「将要造成的伤害」也是
/// <c>CalculateSpellDamage</c> 的入参（连锁闪电用它记跳跃序号），<c>killed</c> 是 <c>_applyDamage</c> 的返回值，
/// <c>resist</c> 由选目标的那一侧置位。
/// </para>
/// <para>
/// 端口用类而不是结构体：<see cref="BattleUnit.ApplySpell"/> 要把结果写回调用方手上的同一个对象，类天然是引用语义，
/// 不必在每个签名里加 <c>ref</c>。参考的 <c>TargetsInfo</c> 就是 <c>std::vector&lt;TargetInfo&gt;</c>，
/// 端口对应的容器是 <c>List&lt;BattleTargetInfo&gt;</c>，本类不另立类型。
/// </para>
/// </remarks>
public sealed class BattleTargetInfo
{
    /// <summary>Builds an empty target, the reference's <c>TargetInfo() = default</c>.</summary>
    public BattleTargetInfo()
    {
    }

    /// <summary>Builds a target for a unit, the reference's <c>explicit TargetInfo( Unit * )</c>.</summary>
    /// <param name="defender">The unit the spell is aimed at.</param>
    public BattleTargetInfo(BattleUnit? defender) => Defender = defender;

    /// <summary>The unit the spell is aimed at, <c>TargetInfo::defender</c>.</summary>
    public BattleUnit? Defender { get; set; }

    /// <summary>
    /// The damage of the spell, <c>TargetInfo::damage</c>: the input of <c>CalculateSpellDamage</c> before the
    /// cast and the damage actually dealt after it.
    /// </summary>
    public uint Damage { get; set; }

    /// <summary>How many creatures died, <c>TargetInfo::killed</c>.</summary>
    public uint Killed { get; set; }

    /// <summary>Whether the target shrugged the spell off, <c>TargetInfo::resist</c>.</summary>
    public bool Resist { get; set; }
}
