namespace FHeroes2.Game.Heroes;

/// <summary>
/// The names of the four primary skills, <c>Skill::Primary::String()</c> (<c>skill.cpp:262-276</c>).
/// </summary>
/// <remarks>
/// 城建窗口的船长面板要拿这四个词当行首（<c>castle_town.cpp:359-386</c>）；参考里它们就是界面上的原文。
/// </remarks>
public static class PrimarySkillNames
{
    /// <summary>The name of one primary skill.</summary>
    /// <param name="skill">The skill.</param>
    /// <returns>The name, empty for <see cref="PrimarySkill.Unknown"/>.</returns>
    public static string Get(PrimarySkill skill) => skill switch
    {
        PrimarySkill.Attack => "Attack Skill",
        PrimarySkill.Defense => "Defense Skill",
        PrimarySkill.Power => "Spell Power",
        PrimarySkill.Knowledge => "Knowledge",
        _ => string.Empty,
    };

    /// <summary>
    /// The sentence saying what a primary skill does, <c>Skill::Primary::StringDescription( skill, nullptr )</c>
    /// (<c>skill.cpp:282-323</c>).
    /// </summary>
    /// <remarks>
    /// 参考的这个函数接受一个可空的英雄：给了英雄就再追加一段「当前加成」。战役奖励弹窗（<c>ui_campaign.cpp:119</c>）
    /// 传的是 <c>nullptr</c>，所以这里只落无英雄的那一半。带英雄的版本要等 <c>skill.cpp</c> 的逐函数核对。
    /// </remarks>
    /// <param name="skill">The skill.</param>
    /// <returns>The sentence, empty for <see cref="PrimarySkill.Unknown"/>.</returns>
    public static string GetDescription(PrimarySkill skill) => skill switch
    {
        PrimarySkill.Attack => "The hero's attack skill is a bonus added to each unit's attack skill.",
        PrimarySkill.Defense => "The hero's defense skill is a bonus added to each unit's defense skill.",
        PrimarySkill.Power => "The hero's spell power determines the duration or power of a spell.",
        PrimarySkill.Knowledge =>
            "The hero's knowledge determines how many spell points the hero may have. Under normal circumstances, a hero is limited to 10 spell points per level of knowledge.",
        _ => string.Empty,
    };
}
