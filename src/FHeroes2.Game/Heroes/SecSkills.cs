using System.Text;

using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// One learned secondary skill and the level the hero has in it — the pair <c>Skill::Secondary</c> extends
/// (<c>skill.h:67-126</c>).
/// </summary>
/// <param name="Skill">The skill.</param>
/// <param name="Level">The level of the hero in that skill.</param>
public readonly record struct SecondarySkillLevel(SecondarySkill Skill, SkillLevel Level)
{
    /// <summary>
    /// Whether both halves are set — <c>Skill::Secondary::isValid()</c>. A slot that is not valid is an empty
    /// slot, not a bug: <see cref="SecSkills"/> keeps them so that the eight slots of a hero are positional.
    /// </summary>
    public bool IsValid => Skill != SecondarySkill.Unknown && Level != SkillLevel.None;

    /// <summary>
    /// The same skill at another level, <c>Skill::Secondary::SetLevel()</c> (<c>skill.cpp:380-383</c>).
    /// </summary>
    /// <remarks>
    /// 记录结构不可变，参考的「就地改等级」在这里是「换一个值」。参考把越界等级压成 <c>NONE</c>，端口照抄：
    /// 一个不在 0..3 里的数只可能来自坏数据。
    /// </remarks>
    /// <param name="level">The new level.</param>
    /// <returns>The skill at that level.</returns>
    public SecondarySkillLevel WithLevel(SkillLevel level) =>
        this with { Level = level is >= SkillLevel.None and <= SkillLevel.Expert ? level : SkillLevel.None };

    /// <summary>
    /// The same level applied to another skill, <c>Skill::Secondary::SetSkill()</c> (<c>skill.cpp:375-378</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="WithLevel"/> 一样是「换一个值」，越界技能压成 <see cref="SecondarySkill.Unknown"/>——参考对
    /// 不在 <c>UNKNOWN..ESTATES</c> 里的编号就是这么处理的，这种值只可能来自存档里的坏数据。
    /// </remarks>
    /// <param name="skill">The new skill.</param>
    /// <returns>The skill at that level.</returns>
    public SecondarySkillLevel WithSkill(SecondarySkill skill) =>
        this with { Skill = skill > SecondarySkill.Unknown && skill <= SecondarySkill.Estates ? skill : SecondarySkill.Unknown };

    /// <summary>
    /// The next level up, <c>Skill::Secondary::NextLevel()</c> (<c>skill.cpp:385-400</c>).
    /// </summary>
    /// <remarks>
    /// 已经到专家的技能原地不动——参考的 <c>default: break;</c> 就是「什么都不做」，不是回到初级。
    /// </remarks>
    /// <returns>The skill one level higher, or itself when it is already expert.</returns>
    public SecondarySkillLevel NextLevel() => Level switch
    {
        SkillLevel.None => this with { Level = SkillLevel.Basic },
        SkillLevel.Basic => this with { Level = SkillLevel.Advanced },
        SkillLevel.Advanced => this with { Level = SkillLevel.Expert },
        _ => this,
    };
}

/// <summary>
/// The secondary skills of one hero — <c>Skill::SecSkills</c> (<c>skill.h:128-153</c>,
/// <c>skill.cpp:621-780</c>).
/// </summary>
/// <remarks>
/// <para>
/// A hero has at most <see cref="MaxCount"/> skills (<c>Heroes::maxNumOfSecSkills</c>, <c>heroes.h:85</c>),
/// and a skill is stored once with the highest level reached. The reference keeps a vector that may hold
/// <b>empty slots</b> (<c>{ UNKNOWN, NONE }</c>) — <see cref="FillMax"/> pads it to eight so that the save
/// format has a fixed width — so every reading member here has to skip them, exactly like the reference's
/// <c>Count()</c>/<c>GetTotalLevel()</c> do.
/// </para>
/// <para>
/// <b>Not ported</b>: <c>FindSkillsForLevelUp</c> (<c>skill.cpp:782</c>), which needs the two random seeds
/// the level-up screen rolls and the race-based weight draw the reference does through
/// <c>SecondaryGetWeightSkillFromRace</c>. It belongs with the level-up itself, and the weights it draws from
/// are already here.
/// </para>
/// </remarks>
public sealed class SecSkills
{
    /// <summary>
    /// The maximum number of secondary skills a hero can have — <c>Heroes::maxNumOfSecSkills</c>
    /// (<c>heroes.h:85</c>). Eight, though there are fourteen skills.
    /// </summary>
    public const int MaxCount = 8;

    private readonly List<SecondarySkillLevel> _skills = new(MaxCount);

    /// <summary>An empty set of skills.</summary>
    public SecSkills()
    {
    }

    /// <summary>
    /// The skills a hero of a race starts with — <c>Skill::SecSkills::SecSkills( race )</c>
    /// (<c>skill.cpp:626</c>), reading <see cref="FactionProperties.HeroInitialSecondarySkills"/>.
    /// </summary>
    /// <param name="race">
    /// The race. A race without properties (<see cref="MapRace.None"/>, random, multiple) gives an empty set,
    /// which is what the reference's <c>early return</c> does.
    /// </param>
    public SecSkills(MapRace race)
    {
        FactionProperties? properties = Factions.Get(race);
        if (properties is null)
        {
            return;
        }

        // The reference walks the fourteen fields in its own order, not in id order: that order is what the
        // hero screen lists a knight's «leadership, ballistics» in.
        foreach (SecondarySkill skill in SecondarySkills.InitialFillOrder)
        {
            int level = properties.HeroInitialSecondarySkills[skill];
            if (level > 0)
            {
                Add(new SecondarySkillLevel(skill, (SkillLevel)level));
            }
        }
    }

    /// <summary>The slots, empty ones included, in the order they were filled.</summary>
    public IReadOnlyList<SecondarySkillLevel> Skills => _skills;

    /// <summary>
    /// The number of learned skills — <c>Skill::SecSkills::Count()</c>. Empty slots do not count.
    /// </summary>
    public int Count => _skills.Count(skill => skill.IsValid);

    /// <summary>
    /// The sum of the levels of the learned skills — <c>Skill::SecSkills::GetTotalLevel()</c>. A hero with
    /// basic wisdom and expert estates has a total of four; the level-up screen uses the sum modulo three to
    /// force wisdom on a magic class.
    /// </summary>
    public int TotalLevel => _skills.Where(skill => skill.IsValid).Sum(skill => (int)skill.Level);

    /// <summary>
    /// The level of one skill — <c>Skill::SecSkills::GetLevel( skill )</c>.
    /// </summary>
    /// <param name="skill">The skill to look for.</param>
    /// <returns>The level, or <see cref="SkillLevel.None"/> when the hero does not have the skill.</returns>
    public SkillLevel GetLevel(SecondarySkill skill)
    {
        foreach (SecondarySkillLevel entry in _skills)
        {
            if (entry.Skill == skill)
            {
                return entry.Level;
            }
        }

        return SkillLevel.None;
    }

    /// <summary>
    /// The value of one skill — <c>Skill::SecSkills::GetValue( skill )</c>, i.e.
    /// <see cref="SecondarySkills.GetValue"/> at the hero's level, or zero without the skill.
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <returns>The value of the reference's row for the hero's level in that skill.</returns>
    public uint GetValue(SecondarySkill skill) => SecondarySkills.GetValue(skill, GetLevel(skill));

    /// <summary>
    /// Learns a skill or raises an already learned one — <c>Skill::SecSkills::AddSkill</c>
    /// (<c>skill.cpp:719</c>).
    /// </summary>
    /// <param name="skill">The skill and the new level.</param>
    /// <remarks>
    /// The three cases of the reference, in its order: raise the level of a skill the hero already has, fill an
    /// empty slot, or append while there is room. A ninth distinct skill is <b>silently dropped</b> — that is
    /// what the reference does, and the level-up path is responsible for never offering one.
    /// </remarks>
    public void Add(SecondarySkillLevel skill)
    {
        if (!skill.IsValid)
        {
            return;
        }

        for (int index = 0; index < _skills.Count; ++index)
        {
            if (_skills[index].Skill == skill.Skill)
            {
                _skills[index] = skill;
                return;
            }
        }

        for (int index = 0; index < _skills.Count; ++index)
        {
            if (!_skills[index].IsValid)
            {
                _skills[index] = skill;
                return;
            }
        }

        if (_skills.Count < MaxCount)
        {
            _skills.Add(skill);
        }
    }

    /// <summary>
    /// Pads the set to <see cref="MaxCount"/> slots — <c>Skill::SecSkills::FillMax</c> (<c>skill.cpp:775</c>).
    /// </summary>
    /// <param name="filler">The empty slot to pad with; the reference takes it as a parameter and its caller
    /// passes an unset skill.</param>
    public void FillMax(SecondarySkillLevel filler)
    {
        while (_skills.Count < MaxCount)
        {
            _skills.Add(filler);
        }
    }

    /// <summary>
    /// The skills joined by <c>", "</c>, <c>Skill::SecSkills::String()</c> (<c>skill.cpp:763-773</c>).
    /// </summary>
    /// <remarks>
    /// 参考每个技能后面都补一个 <c>", "</c>（含最后一个）且不裁剪末尾；端口照抄这个格式，它只用于日志与调试，
    /// 改了会让人误以为行为变了。空槽也在遍历范围里——参考的 <c>GetName()</c> 对它们返回 "unknown"。
    /// </remarks>
    /// <returns>The comma-separated list with its trailing separator.</returns>
    public override string ToString()
    {
        StringBuilder text = new();

        foreach (SecondarySkillLevel skill in _skills)
        {
            text.Append(SecondarySkills.GetFullName(skill.Skill, skill.Level)).Append(", ");
        }

        return text.ToString();
    }
}
