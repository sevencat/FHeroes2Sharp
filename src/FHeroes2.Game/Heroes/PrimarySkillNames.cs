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
}
