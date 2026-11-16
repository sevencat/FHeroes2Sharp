using FHeroes2.Core.Random;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The experience of a hero and what a level-up does with it — <c>Heroes::IncreaseExperience()</c>
/// (<c>heroes.cpp:1482</c>), <c>_levelUp()</c> (<c>heroes.cpp:1848</c>) and <c>_getSeedsForLevelUp()</c>
/// (<c>heroes.cpp:2201</c>).
/// </summary>
/// <remarks>
/// <para>
/// 经验本身只是一个数，等级是从 <see cref="HeroExperience"/> 的阈值表反查出来的，不单独存——参考同样如此
/// （存档里只有经验），所以加经验时「跳了多少级」要用加之前和加之后的等级比出来。
/// </para>
/// <para>
/// <b>一次加很多经验会连升多级。</b>参考的写法是：先把经验压到每一级的门槛上逐级 <c>_levelUp</c>，最后再把
/// 真实经验写回去。这样中间每一级的抽签种子都基于「当时的等级和技能」，而不是基于最终状态——否则连升三级会
/// 变成同一套抽签连抽三次，参考在 <c>_getSeedsForLevelUp()</c> 的注释里专门解释过为什么不能这么干。
/// </para>
/// <para>
/// <b>谁能选副技能。</b>参考有三种：人类玩家弹窗、AI 由 <c>AI::Planner::pickSecondarySkill</c> 挑、
/// <c>autoselect</c> 时按种子在两张牌里掷一次。端口把「谁选」抽成一个委托：传 <see langword="null"/>
/// 时就是参考的 <c>autoselect</c>（种子决定），界面层接上以后传自己的选择函数即可，升级算法本身不用改。
/// 主技能没有选择——参考也从来不问，直接加。
/// </para>
/// <para>
/// 侦察术（<c>SCOUTING</c>）学成后立刻开图的那一步没有搬：它要雾与雷达，属于迷雾系统。
/// </para>
/// </remarks>
public sealed partial class Hero
{
    private uint _experience;

    /// <summary>The experience of the hero, <c>_experience</c> (<c>heroes.h:349</c>).</summary>
    public uint Experience => _experience;

    /// <summary>
    /// The level of the hero, <c>Heroes::GetLevel()</c> (<c>heroes.h:334</c>).
    /// </summary>
    /// <remarks>
    /// Derived from the experience every time: the reference has no level field either, and a cached one would
    /// have to be invalidated by every write to <see cref="SetExperience"/>.
    /// </remarks>
    public int Level => HeroExperience.LevelFrom(_experience);

    /// <summary>
    /// Sets the experience of the hero without levelling him up, <c>_experience = value</c> of the loader.
    /// </summary>
    /// <param name="experience">The experience.</param>
    /// <remarks>
    /// 给读档与地图编辑用。参考在读 MP2 时会按经验补等级，那一步属于加载流程，这里只存值——调用方要等级与
    /// 经验一致，得自己调 <see cref="IncreaseExperience"/> 或 <see cref="LevelUp"/>。
    /// </remarks>
    public void SetExperience(uint experience) => _experience = experience;

    /// <summary>
    /// Adds experience and levels the hero up as many times as the amount pays for,
    /// <c>Heroes::IncreaseExperience()</c> (<c>heroes.cpp:1482</c>).
    /// </summary>
    /// <param name="amount">The experience to add.</param>
    /// <param name="mapSeed">
    /// The seed of the map, <c>World::GetMapSeed()</c> (<c>world.h:504</c>); together with the hero's state it
    /// decides the two draws of every level-up.
    /// </param>
    /// <param name="chooseSecondarySkill">
    /// Picks one of the two candidates; <see langword="null"/> lets the seed pick, which is the reference's
    /// <c>autoselect</c>. The human dialog and the AI planner would both be passed in here.
    /// </param>
    public void IncreaseExperience(
        uint amount,
        uint mapSeed,
        Func<SecondarySkillLevel, SecondarySkillLevel, SecondarySkillLevel>? chooseSecondarySkill = null)
    {
        int oldLevel = Level;
        int newLevel = HeroExperience.LevelFrom(_experience + amount);
        uint updatedExperience = _experience + amount;

        for (int level = oldLevel; level < newLevel - 1; ++level)
        {
            _experience = HeroExperience.FromLevel(level);
            LevelUp(mapSeed, chooseSecondarySkill);
        }

        _experience = updatedExperience;

        if (newLevel > oldLevel)
        {
            LevelUp(mapSeed, chooseSecondarySkill);
        }
    }

    /// <summary>
    /// One level-up: a primary skill grows by one and a secondary skill is offered, <c>_levelUp()</c>
    /// (<c>heroes.cpp:1848</c>) and <c>_levelUpSecondarySkill()</c> (<c>heroes.cpp:1862</c>).
    /// </summary>
    /// <param name="mapSeed">The seed of the map.</param>
    /// <param name="chooseSecondarySkill">The chooser; <see langword="null"/> means the seed picks.</param>
    /// <returns>The primary skill that grew; <see cref="PrimarySkill.Unknown"/> for a race without properties.</returns>
    /// <remarks>
    /// <b>Does not touch the experience.</b> That is how the reference splits it too: the loop of
    /// <see cref="IncreaseExperience"/> owns the number, this owns the two skills.
    /// </remarks>
    public PrimarySkill LevelUp(
        uint mapSeed,
        Func<SecondarySkillLevel, SecondarySkillLevel, SecondarySkillLevel>? chooseSecondarySkill = null)
    {
        FactionProperties? properties = Factions.Get(Race);
        if (properties is null)
        {
            return PrimarySkill.Unknown;
        }

        LevelUpSeeds seeds = CreateLevelUpSeeds(mapSeed);

        PrimarySkill primary = SkillLevelUp.Primary(properties, Level, seeds.PrimarySkill);
        if (primary != PrimarySkill.Unknown)
        {
            IncreasePrimarySkill(primary);
        }

        (SecondarySkillLevel first, SecondarySkillLevel second) = SkillLevelUp.SecondaryCandidates(
            Race,
            properties,
            _secondarySkills,
            seeds.SecondarySkill1,
            seeds.SecondarySkill2);

        SecondarySkillLevel selected = PickSecondarySkill(first, second, seeds.SecondarySkillChoose, chooseSecondarySkill);
        if (selected.IsValid)
        {
            _secondarySkills.Add(selected);
        }

        return primary;
    }

    /// <summary>
    /// The four seeds of one level-up, <c>_getSeedsForLevelUp()</c> (<c>heroes.cpp:2201</c>).
    /// </summary>
    /// <param name="mapSeed">The seed of the map.</param>
    /// <returns>The seeds.</returns>
    /// <remarks>
    /// 十四个副技能等级都要混进去，不只是已学的：某个技能从无到有也会改变下一级的抽签，参考在注释里说这是为了
    /// 「避免每次升级都发同一批技能」。四个种子是同一个哈希加零到三，保证一次升级里的四次抽取互不相同。
    /// </remarks>
    private LevelUpSeeds CreateLevelUpSeeds(uint mapSeed)
    {
        uint hash = mapSeed;

        hash = Rand.CombineSeedWithValueHash(hash, Id);
        hash = Rand.CombineSeedWithValueHash(hash, (int)Race);
        hash = Rand.CombineSeedWithValueHash(hash, _attack);
        hash = Rand.CombineSeedWithValueHash(hash, _defense);
        hash = Rand.CombineSeedWithValueHash(hash, _power);
        hash = Rand.CombineSeedWithValueHash(hash, _knowledge);

        for (int skill = (int)SecondarySkill.Pathfinding; skill <= (int)SecondarySkill.Estates; ++skill)
        {
            hash = Rand.CombineSeedWithValueHash(hash, (int)_secondarySkills.GetLevel((SecondarySkill)skill));
        }

        return new LevelUpSeeds(hash, hash + 1, hash + 2, hash + 3);
    }

    /// <summary>
    /// Picks one of the two candidates, the three-way branch of <c>_levelUpSecondarySkill()</c> minus the AI.
    /// </summary>
    /// <param name="first">The first candidate.</param>
    /// <param name="second">The second candidate.</param>
    /// <param name="seed">The seed of the coin toss.</param>
    /// <param name="chooser">The caller's chooser, or <see langword="null"/> for the coin toss.</param>
    /// <returns>The chosen candidate, or an invalid one when neither half is valid.</returns>
    private static SecondarySkillLevel PickSecondarySkill(
        SecondarySkillLevel first,
        SecondarySkillLevel second,
        uint seed,
        Func<SecondarySkillLevel, SecondarySkillLevel, SecondarySkillLevel>? chooser)
    {
        if (!first.IsValid)
        {
            return second;
        }

        if (!second.IsValid)
        {
            return first;
        }

        if (chooser is not null)
        {
            return chooser(first, second);
        }

        return Rand.GetWithSeed(0, 1, seed) != 0 ? first : second;
    }

    /// <summary>The four seeds of one level-up, <c>HeroSeedsForLevelUp</c> (<c>heroes.cpp:1848</c>).</summary>
    /// <param name="PrimarySkill">Seed of the primary-skill draw.</param>
    /// <param name="SecondarySkill1">Seed of the first secondary-skill draw.</param>
    /// <param name="SecondarySkill2">Seed of the second secondary-skill draw.</param>
    /// <param name="SecondarySkillChoose">Seed of the coin toss between the two candidates.</param>
    private readonly record struct LevelUpSeeds(
        uint PrimarySkill,
        uint SecondarySkill1,
        uint SecondarySkill2,
        uint SecondarySkillChoose);
}
