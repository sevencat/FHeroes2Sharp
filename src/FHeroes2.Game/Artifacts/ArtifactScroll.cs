using FHeroes2.Core.Random;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Artifacts;

/// <summary>
/// What spell a spell scroll carries: <c>Artifact::SetSpell()</c> and the scroll half of <c>Artifact::getSpellId()</c>
/// (<c>artifact.cpp:384-432</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>Artifact</c> 是「编号 + 附加值」一对，卷轴把法术存在附加值里，<c>SetSpell()</c> 负责把
/// <c>RANDOM</c>/<c>RANDOM1</c>.. 展开成一支确定的法术再写进去。端口没有 <c>Artifact</c> 这个类（见
/// <see cref="ArtifactType"/> 的说明）：卷轴的法术由地图对象元数据或宝物栏的条目各自保存，所以这里不提供
/// 「修改自身」的方法，只提供两个纯函数，让调用方自己决定把结果写到哪里。
/// </para>
/// <para>
/// <see cref="SpellOf"/> 对应 <c>getSpellId()</c> 的两段：表里 <c>ADD_SPELL</c> 有确定值就用它（普通卷轴在表里
/// 就写好了法术），值是 <c>NONE</c> 时才回落到调用方存的那份（玩家从两个法术里挑一个的卷轴）。
/// </para>
/// </remarks>
public static class ArtifactScroll
{
    /// <summary>
    /// Expands a requested spell into the one the scroll will carry, <c>Artifact::SetSpell()</c>
    /// (<c>artifact.cpp:384-420</c>).
    /// </summary>
    /// <remarks>
    /// <c>RANDOM</c> 先掷一次等级（1 到 5）再掷法术，所以高等级法术的命中率只有低级的一半——这是参考的
    /// 原始行为，不是加权失误。
    /// </remarks>
    /// <param name="requested">The spell the caller asked for, possibly a placeholder.</param>
    /// <returns>A real spell; <paramref name="requested"/> itself when it is already one.</returns>
    public static SpellType ResolveRequestedSpell(SpellType requested) => requested switch
    {
        SpellType.Random => Spell.RandomSpell(Rand.Get(1, 5)).Type,
        SpellType.Random1 => Spell.RandomSpell(1).Type,
        SpellType.Random2 => Spell.RandomSpell(2).Type,
        SpellType.Random3 => Spell.RandomSpell(3).Type,
        SpellType.Random4 => Spell.RandomSpell(4).Type,
        SpellType.Random5 => Spell.RandomSpell(5).Type,
        _ => requested,
    };

    /// <summary>
    /// The spell a scroll of that kind holds, the scroll branch of <c>Artifact::getSpellId()</c>
    /// (<c>artifact.cpp:432-448</c>).
    /// </summary>
    /// <param name="artifact">The artifact, normally <see cref="ArtifactType.SpellScroll"/>.</param>
    /// <param name="storedSpell">The spell the caller keeps for this copy, used only when the table says nothing.</param>
    /// <returns>The spell, or <see cref="SpellType.None"/> when the artifact carries no spell at all.</returns>
    public static SpellType SpellOf(ArtifactType artifact, SpellType storedSpell)
    {
        if (!ArtifactTable.Get(artifact).TryGetBonus(ArtifactBonusType.AddSpell, out ArtifactBonus bonus))
        {
            return SpellType.None;
        }

        return bonus.Spell == SpellType.None ? storedSpell : bonus.Spell;
    }
}
