namespace FHeroes2.Game.Heroes;

/// <summary>
/// The secondary-skill questions the adventure map asks of a hero, <c>heroes.cpp:1722-1747</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>Heroes::HasSecondarySkill</c>（<c>heroes.cpp:1722</c>）、<c>HasMaxSecondarySkill</c>
/// （<c>heroes.cpp:1732</c>）、<c>GetLevelSkill</c>（<c>heroes.cpp:1737</c>）与 <c>LearnSkill</c>
/// （<c>heroes.cpp:1742</c>）。三个都是对 <see cref="SecSkills"/> 的一行转发，之所以独立出来，是因为它们
/// 代表「英雄会不会这门技能」这一层语义，而不是容器的读写。
/// </para>
/// <para>
/// 女巫小屋是第一个用它们的地方：八个槽满了就学不了，已经会了也不重复教，否则按基础等级记下来。
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// Whether the hero has the skill at any level, <c>Heroes::HasSecondarySkill()</c>
    /// (<c>heroes.cpp:1722</c>).
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <returns><see langword="true"/> when the level is anything but <see cref="SkillLevel.None"/>.</returns>
    public bool HasSecondarySkill(SecondarySkill skill) => _secondarySkills.GetLevel(skill) != SkillLevel.None;

    /// <summary>
    /// The level of one skill, <c>Heroes::GetLevelSkill()</c> (<c>heroes.cpp:1737</c>).
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <returns>The level, or <see cref="SkillLevel.None"/> when the hero does not have the skill.</returns>
    public SkillLevel GetLevelSkill(SecondarySkill skill) => _secondarySkills.GetLevel(skill);

    /// <summary>
    /// Whether the hero has no room for another skill, <c>Heroes::HasMaxSecondarySkill()</c>
    /// (<c>heroes.cpp:1732</c>).
    /// </summary>
    /// <remarks>
    /// 比的是 <see cref="SecSkills.Count"/> 与 <see cref="SecSkills.MaxCount"/>，也就是「八个槽都满了」，
    /// 不是「十四种技能都会了」——英雄永远只有八个槽。
    /// </remarks>
    /// <returns><see langword="true"/> when all eight slots are taken.</returns>
    public bool HasMaxSecondarySkill() => _secondarySkills.Count >= SecSkills.MaxCount;

    /// <summary>
    /// Learns a skill or raises an already learned one, <c>Heroes::LearnSkill()</c>
    /// (<c>heroes.cpp:1742</c>).
    /// </summary>
    /// <remarks>
    /// 参考只挡掉无效的 <c>Skill::Secondary</c>，其余交给 <c>SecSkills::AddSkill</c>——包括「第九个技能被
    /// 静默丢掉」这件事。要拒绝满槽，调用方得先问 <see cref="HasMaxSecondarySkill"/>，女巫小屋就是这么做的。
    /// </remarks>
    /// <param name="skill">The skill and its new level.</param>
    public void LearnSkill(SecondarySkillLevel skill) => _secondarySkills.Add(skill);
}
