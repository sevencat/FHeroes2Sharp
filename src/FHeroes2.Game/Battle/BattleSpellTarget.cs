namespace FHeroes2.Game.Battle;

/// <summary>
/// One unit a spell would hit, <c>Battle::TargetInfo</c> (<c>battle.h:TargetInfo</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里这是个很小的 POD，只有「打到谁、多少伤害、死多少、有没有抗住」四件事。战斗 AI 估值时只用
/// <see cref="Defender"/> 与 <see cref="Resist"/>；伤害与死亡数留给真正的施法流程填。
/// </para>
/// <para>
/// 端口用类而不是结构体：<see cref="BattleUnit.ApplySpell"/> 要把结果写回调用方手上的同一个对象，类天然是引用语义，
/// 不必在每个签名里加 <c>ref</c>。参考的 <c>TargetsInfo</c> 就是 <c>std::vector&lt;TargetInfo&gt;</c>，
/// 端口对应的容器是 <c>List&lt;BattleSpellTarget&gt;</c>，不另立类型——竞技场选目标与单位应用法术必须写同一份结果，
/// 两套并行的小类会让伤害与死亡数在传递中丢掉。
/// </para>
/// </remarks>
public sealed class BattleSpellTarget
{
    /// <summary>Builds an empty target, the reference's <c>TargetInfo() = default</c>.</summary>
    public BattleSpellTarget()
    {
    }

    /// <summary>Builds a target for a unit, the reference's <c>explicit TargetInfo( Unit * )</c>.</summary>
    /// <param name="defender">The unit the spell is aimed at.</param>
    public BattleSpellTarget(BattleUnit? defender) => Defender = defender;

    /// <summary>The unit on the receiving end.</summary>
    public BattleUnit? Defender { get; set; }

    /// <summary>The damage the spell deals to it.</summary>
    public uint Damage { get; set; }

    /// <summary>How many creatures the damage kills.</summary>
    public uint Killed { get; set; }

    /// <summary>Whether the unit's magic resistance shrugged the spell off.</summary>
    public bool Resist { get; set; }
}
