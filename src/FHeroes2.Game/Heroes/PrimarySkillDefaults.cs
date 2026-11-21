using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The primary-skill defaults of a race: its initial spell, the four starting numbers and the legal value range,
/// <c>Skill::Primary::GetInitialSpell()</c>, <c>getHeroDefaultSkillValue()</c> and <c>getSkillValueRange()</c>
/// (<c>skill.cpp:167-215</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的这三个都是 <c>Skill::Primary</c> 命名空间里的自由函数，读的是 <c>GameStatic::GetFactionProperties()</c>；
/// 端口对应 <see cref="Factions.Get"/>，于是落成一个静态类，而不是挂在某个类型上——它们既不属于某个英雄，也不
/// 属于某个技能对象，只跟种族有关。
/// </para>
/// <para>
/// 「力量与知识从 1 起、攻防从 0 起」是范围上的硬约束：<see cref="GetSkillValueRange"/> 的调用方用它来给升级
/// 与随机初始化夹边。
/// </para>
/// </remarks>
public static class PrimarySkillDefaults
{
    /// <summary>
    /// The spell every hero of the race knows from the start, <c>Skill::Primary::GetInitialSpell()</c>
    /// (<c>skill.cpp:167-175</c>).
    /// </summary>
    /// <remarks>
    /// 参考在种族表缺失时返回 <c>Spell::NONE</c>，端口照抄：这个值只对「这个种族的第一章」有意义，表里没有
    /// 就说明调用方给的不是一个真种族。
    /// </remarks>
    /// <param name="race">The hero's race.</param>
    /// <returns>The spell, or <see cref="SpellType.None"/> when the race has none.</returns>
    public static SpellType GetInitialSpell(MapRace race) => (SpellType)(Factions.Get(race)?.InitialSpellId ?? 0);

    /// <summary>
    /// The value a hero of the race starts with in one primary skill, <c>getHeroDefaultSkillValue()</c>
    /// (<c>skill.cpp:177-198</c>).
    /// </summary>
    /// <remarks>
    /// 表里没有这个种族时回落到「力量与知识各 1、攻防各 0」——这是参考对未知种族的兜底，不是笔误。
    /// </remarks>
    /// <param name="skill">One of the four primary skills.</param>
    /// <param name="race">The hero's race.</param>
    /// <returns>The starting value.</returns>
    public static int GetHeroDefaultSkillValue(PrimarySkill skill, MapRace race)
    {
        if (Factions.Get(race) is { } properties)
        {
            return skill switch
            {
                PrimarySkill.Attack => properties.HeroInitialPrimarySkills.Attack,
                PrimarySkill.Defense => properties.HeroInitialPrimarySkills.Defense,
                PrimarySkill.Power => properties.HeroInitialPrimarySkills.Power,
                PrimarySkill.Knowledge => properties.HeroInitialPrimarySkills.Knowledge,
                _ => UnsupportedSkillValue(skill),
            };
        }

        return skill is PrimarySkill.Power or PrimarySkill.Knowledge ? 1 : 0;
    }

    /// <summary>
    /// The name of a primary skill, <c>Skill::Primary::String()</c> (<c>skill.cpp:262-280</c>).
    /// </summary>
    /// <remarks>
    /// 与参数名 <c>skillType</c> 一致，参考按「技能类型」取值；四句里只有「力量」不叫 "Power"——参考的文字是
    /// "Spell Power"，因为它是法术力量而不是攻击力。
    /// </remarks>
    /// <param name="skill">One of the four primary skills.</param>
    /// <returns>The name, or <c>"Unknown"</c> for anything else.</returns>
    public static string GetName(PrimarySkill skill) => skill switch
    {
        PrimarySkill.Attack => "Attack Skill",
        PrimarySkill.Defense => "Defense Skill",
        PrimarySkill.Power => "Spell Power",
        PrimarySkill.Knowledge => "Knowledge",
        _ => UnsupportedName(skill),
    };

    /// <summary>
    /// The values a primary skill may take, <c>getSkillValueRange()</c> (<c>skill.cpp:200-215</c>).
    /// </summary>
    /// <param name="skill">One of the four primary skills.</param>
    /// <returns>The inclusive range; <c>(0, 0)</c> for anything that is not a primary skill.</returns>
    public static (int Minimum, int Maximum) GetSkillValueRange(PrimarySkill skill) => skill switch
    {
        PrimarySkill.Attack or PrimarySkill.Defense => (0, 99),
        PrimarySkill.Power or PrimarySkill.Knowledge => (1, 99),
        _ => Unsupported(skill),
    };

    /// <summary>Logs the reference's <c>assert( 0 )</c> for a value that is not a primary skill.</summary>
    /// <param name="skill">The offending value.</param>
    /// <returns>The word the reference returns after the assertion, <c>"Unknown"</c>.</returns>
    private static string UnsupportedName(PrimarySkill skill)
    {
        Debug.Assert(false, $"Not a primary skill: {skill}.");

        return "Unknown";
    }

    /// <summary>Logs the reference's <c>assert( 0 )</c> for a value that is not a primary skill.</summary>
    /// <param name="skill">The offending value.</param>
    /// <returns>The range the reference returns after the assertion, <c>(0, 0)</c>.</returns>
    private static (int Minimum, int Maximum) Unsupported(PrimarySkill skill)
    {
        Debug.Assert(false, $"Not a primary skill: {skill}.");

        return (0, 0);
    }

    /// <summary>Logs the reference's <c>assert( 0 )</c> and returns the value it falls through to, 0.</summary>
    /// <param name="skill">The offending value.</param>
    /// <returns>Zero.</returns>
    private static int UnsupportedSkillValue(PrimarySkill skill)
    {
        Debug.Assert(false, $"Not a primary skill: {skill}.");

        return 0;
    }
}
