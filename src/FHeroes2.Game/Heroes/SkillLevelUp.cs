using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The two draws of a level-up: which primary skill grows by one, and which two secondary skills are offered —
/// <c>Skill::Primary::LevelUp</c> (<c>skill.cpp:217</c>) and
/// <c>Skill::SecSkills::FindSkillsForLevelUp</c> (<c>skill.cpp:782</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里这两个函数一个直接改 <c>attack/defense/power/knowledge</c>，一个返回两个 <c>Secondary</c> 让上层写回。
/// 端口都做成纯函数：主技能返回「该加哪一个」，由 <see cref="Hero.IncreasePrimarySkill"/> 落地；副技能返回
/// 「两个候选」，由 <see cref="Hero"/> 挑一个再 <see cref="SecSkills.Add"/>。这样升级流程只剩一处状态变更，
/// 不会出现「抽签抽了两次、加点加了两次」这类错位。
/// </para>
/// <para>
/// 抽签靠加权池 <see cref="Rand.Queue"/>，权重来自 <see cref="FactionProperties"/> 的四张表——种族固定，
/// 所以随机性全在种子里。<b>种子不是随便取的</b>：参考的 <c>_getSeedsForLevelUp()</c>
/// (<c>heroes.cpp:2201</c>) 把地图种子、英雄 id、种族、四项主技能和十四个副技能等级混成一个哈希，
/// 让同一个英雄在同一张图上每次重开得到同样的成长，但不同英雄、不同成长状态得到不同的抽签。端口照抄这个
/// 混合过程（<see cref="Hero"/> 里的 <c>CreateLevelUpSeeds</c>），否则每级都会抽到同一个技能。
/// </para>
/// <para>
/// 黑名单是 <c>FindSkillsForLevelUp</c> 的核心，两条规则缺一不可：Expert 技能不再出现；八格满了以后只
/// 允许提升已有的技能（否则 <see cref="SecSkills.Add"/> 会静默丢掉新技能，玩家会以为升级没生效）。
/// 魔法职业还多一条——每三级至少把 <c>Wisdom</c> 摆到第一候选，这条是参考在 2023 年加的补丁，
/// 目的是不让魔法英雄因为运气差在十级还没有智慧术。
/// </para>
/// </remarks>
public static class SkillLevelUp
{
    /// <summary>
    /// The fourteen skills in id order, <c>allSecondarySkills</c> (<c>skill.cpp:47</c>).
    /// </summary>
    /// <remarks>
    /// The reference spells the array out by hand because the ids are an unnamed enum; the port walks the
    /// enum instead, which cannot go stale when a skill is added.
    /// </remarks>
    private static readonly SecondarySkill[] AllSecondarySkills =
    [
        SecondarySkill.Pathfinding,
        SecondarySkill.Archery,
        SecondarySkill.Logistics,
        SecondarySkill.Scouting,
        SecondarySkill.Diplomacy,
        SecondarySkill.Navigation,
        SecondarySkill.Leadership,
        SecondarySkill.Wisdom,
        SecondarySkill.Mysticism,
        SecondarySkill.Luck,
        SecondarySkill.Ballistics,
        SecondarySkill.EagleEye,
        SecondarySkill.Necromancy,
        SecondarySkill.Estates,
    ];

    /// <summary>
    /// Whether a race is one of the four magic classes — <c>Race::isMagicalRace()</c> (<c>race.cpp:110</c>).
    /// </summary>
    /// <param name="race">The race.</param>
    /// <returns><see langword="true"/> for the sorceress, the warlock, the wizard and the necromancer.</returns>
    public static bool IsMagicalRace(MapRace race) =>
        race is MapRace.Sorceress or MapRace.Warlock or MapRace.Wizard or MapRace.Necromancer;

    /// <summary>
    /// The primary skill a hero gains at a level-up, <c>Skill::Primary::LevelUp()</c> (<c>skill.cpp:217</c>).
    /// </summary>
    /// <param name="properties">The race of the hero.</param>
    /// <param name="level">The current level of the hero, one-based.</param>
    /// <param name="seed">The primary-skill seed of this level-up.</param>
    /// <returns>The skill to raise by one; <see cref="PrimarySkill.Unknown"/> only for a race without properties.</returns>
    /// <remarks>
    /// The pool switches tables at <see cref="FactionProperties.BoundaryBetweenLowAndHighLevels"/> (ten for every
    /// race of the original game): below it a knight grows attack and defense, above it the four are equal. The
    /// comparison is strict — at exactly level ten the high table is already in force.
    /// </remarks>
    public static PrimarySkill Primary(FactionProperties properties, int level, uint seed)
    {
        ArgumentNullException.ThrowIfNull(properties);

        PrimarySkillValues weights = properties.BoundaryBetweenLowAndHighLevels > level
            ? properties.WeightsOfPrimarySkillsForLowLevels
            : properties.WeightsOfPrimarySkillsForHighLevels;

        Rand.Queue pool = new();
        pool.Push((int)PrimarySkill.Attack, (uint)weights.Attack);
        pool.Push((int)PrimarySkill.Defense, (uint)weights.Defense);
        pool.Push((int)PrimarySkill.Power, (uint)weights.Power);
        pool.Push((int)PrimarySkill.Knowledge, (uint)weights.Knowledge);

        return pool.Size == 0 ? PrimarySkill.Unknown : (PrimarySkill)pool.GetWithSeed(seed);
    }

    /// <summary>
    /// The two secondary skills a level-up offers, <c>Skill::SecSkills::FindSkillsForLevelUp()</c>
    /// (<c>skill.cpp:782</c>).
    /// </summary>
    /// <param name="race">The race of the hero; the magic classes get the wisdom guarantee.</param>
    /// <param name="properties">The weights of that race.</param>
    /// <param name="skills">What the hero already knows.</param>
    /// <param name="firstSeed">The seed of the first candidate.</param>
    /// <param name="secondSeed">The seed of the second candidate.</param>
    /// <returns>
    /// The two candidates at their <b>raised</b> level, i.e. what the hero would gain, not what he has. Either
    /// half can be invalid — a hero who knows all fourteen at expert gets two invalid halves, and the reference
    /// shows no dialog at all in that case.
    /// </returns>
    /// <remarks>
    /// The second candidate is drawn with the first one already blacklisted, so the two never name the same
    /// skill; the reference asserts that instead of checking it.
    /// </remarks>
    public static (SecondarySkillLevel First, SecondarySkillLevel Second) SecondaryCandidates(
        MapRace race,
        FactionProperties properties,
        SecSkills skills,
        uint firstSeed,
        uint secondSeed)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(skills);

        HashSet<SecondarySkill> blacklist = BuildBlacklist(skills);

        // Wisdom is forced on a magic class every third total level, regardless of the race weights.
        bool forceWisdom = IsMagicalRace(race)
            && skills.TotalLevel % 3 == 0
            && !blacklist.Contains(SecondarySkill.Wisdom);

        SecondarySkillLevel first = Raised(skills, forceWisdom ? SecondarySkill.Wisdom : PickWeighted(properties, blacklist, firstSeed));

        if (!first.IsValid)
        {
            return (default, default);
        }

        blacklist.Add(first.Skill);

        SecondarySkillLevel second = Raised(skills, PickWeighted(properties, blacklist, secondSeed));

        return (first, second);
    }

    /// <summary>
    /// The skills that cannot be offered, <c>FindSkillsForLevelUp()</c>'s blacklist.
    /// </summary>
    /// <param name="skills">What the hero already knows.</param>
    /// <returns>The blacklist.</returns>
    private static HashSet<SecondarySkill> BuildBlacklist(SecSkills skills)
    {
        HashSet<SecondarySkill> blacklist = [];

        foreach (SecondarySkillLevel entry in skills.Skills)
        {
            if (entry.Level == SkillLevel.Expert)
            {
                blacklist.Add(entry.Skill);
            }
        }

        if (skills.Count >= SecSkills.MaxCount)
        {
            // A full book: only the skills the hero already has can be raised, every other one is barred.
            foreach (SecondarySkill skill in AllSecondarySkills)
            {
                if (skills.GetLevel(skill) != SkillLevel.None)
                {
                    continue;
                }

                blacklist.Add(skill);
            }
        }

        return blacklist;
    }

    /// <summary>
    /// One weighted draw over the skills that are not blacklisted, <c>SecondaryPriorityFromRace()</c>
    /// (<c>skill.cpp:99</c>).
    /// </summary>
    /// <param name="properties">The weights of the race.</param>
    /// <param name="blacklist">The skills to leave out.</param>
    /// <param name="seed">The seed of the draw.</param>
    /// <returns>The skill, or <see cref="SecondarySkill.Unknown"/> when nothing is left.</returns>
    private static SecondarySkill PickWeighted(FactionProperties properties, HashSet<SecondarySkill> blacklist, uint seed)
    {
        Rand.Queue pool = new();

        foreach (SecondarySkill skill in AllSecondarySkills)
        {
            if (blacklist.Contains(skill))
            {
                continue;
            }

            pool.Push((int)skill, (uint)properties.WeightsOfSecondarySkills[skill]);
        }

        return pool.Size == 0 ? SecondarySkill.Unknown : (SecondarySkill)pool.GetWithSeed(seed);
    }

    /// <summary>
    /// A skill at the level above the one the hero has, <c>Skill::Secondary::NextLevel()</c>.
    /// </summary>
    /// <param name="skills">What the hero already knows.</param>
    /// <param name="skill">The skill to raise; <see cref="SecondarySkill.Unknown"/> gives an invalid half.</param>
    /// <returns>The raised skill.</returns>
    /// <remarks>
    /// An unknown skill is invalid and not "basic unknown": the caller relies on <see cref="SecondarySkillLevel.IsValid"/>
    /// to tell "nothing was drawn" from "a real candidate".
    /// </remarks>
    private static SecondarySkillLevel Raised(SecSkills skills, SecondarySkill skill)
    {
        if (skill == SecondarySkill.Unknown)
        {
            return default;
        }

        SkillLevel level = skills.GetLevel(skill);

        Debug.Assert(level != SkillLevel.Expert, "An expert skill must not be offered again.");

        return level switch
        {
            SkillLevel.None => new SecondarySkillLevel(skill, SkillLevel.Basic),
            SkillLevel.Basic => new SecondarySkillLevel(skill, SkillLevel.Advanced),
            _ => new SecondarySkillLevel(skill, SkillLevel.Expert),
        };
    }
}
