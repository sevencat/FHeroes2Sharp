using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The small questions the kingdom asks about a hero: how many artifacts he carries, whether he can still move,
/// and how far a Crystal Ball lets him see (<c>heroes.h:477</c>, <c>heroes.cpp:1658</c>, <c>heroes.cpp:1922</c>,
/// <c>heroes.cpp:1801</c>).
/// </summary>
/// <remarks>
/// <para>
/// These live in their own partial file because they are the "queries" a <see cref="FHeroes2.Game.Kingdom.Kingdom"/>
/// makes while settling a day or answering the player — none of them changes the hero, so none belongs next to
/// <c>Move()</c> or <c>Recruit()</c>.
/// </para>
/// <para>
/// <c>CanMove()</c> is the one that looks at the world: the reference asks the tile the hero stands on for its
/// terrain and road flag, and compares the remaining movement with that step's price. The port keeps the world as a
/// parameter for the same reason the kingdom does — the hero does not own the map.
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// Whether this object is a real hero and not the "no hero" placeholder, <c>Heroes::isValid()</c>
    /// (<c>heroes.cpp:376</c>).
    /// </summary>
    /// <remarks>
    /// 参考只有一句：<c>id != UNKNOWN</c>。它问的是「这个对象指到一位真英雄吗」，不是「这位英雄在图上吗」——
    /// 后者是 <see cref="MapIndex"/> 与 <see cref="IsActive"/> 的事。地图把一位英雄放在城堡门口时，参考先问
    /// <b>这个</b>，再决定是把他挪进城里还是重新招募他。
    /// </remarks>
    public bool IsValid => Id != UnknownId;

    /// <summary>
    /// Sets the tile a patrolling hero keeps close to, <c>Heroes::SetPatrolCenter()</c> (<c>heroes.h</c>).
    /// </summary>
    /// <remarks>
    /// 参考收的是一对数，内部转成索引存下来；端口的名册按索引记（<see cref="PatrolCenterIndex"/>），所以这里直接
    /// 收索引。<see cref="FHeroes2.Game.Kingdom.Kingdom.ApplyPlayWithStartingHero"/> 在把地图作者的巡逻英雄挪进城
    /// 之后要把它补回来——不补的话，那位英雄会在新位置上「巡逻」一片他从未走过的区域。
    /// </remarks>
    /// <param name="mapIndex">The tile index of the patrol center.</param>
    public void SetPatrolCenter(int mapIndex) => PatrolCenterIndex = mapIndex;

    /// <summary>
    /// How many artifacts the hero carries, <c>Heroes::GetCountArtifacts()</c> (<c>heroes.h:477</c>).
    /// </summary>
    /// <remarks>
    /// 参考是一行转调：<c>_bagArtifacts.CountArtifacts()</c>。背包里有多少件宝物就是多少，不问种类是否重复。
    /// </remarks>
    /// <returns>The number of artifacts in the bag.</returns>
    public int GetCountArtifacts() => _bagArtifacts.CountArtifacts();

    /// <summary>
    /// One point of a skill is worth this much next to one point of army strength, <c>SKILL_VALUE</c>
    /// (<c>heroes.h:725</c>).
    /// </summary>
    private const double SkillValue = 100;

    /// <summary>
    /// How much the hero is worth as a set of numbers, <c>Heroes::getStatsValue()</c> (<c>heroes.cpp:875</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>getTotalPrimarySkillLevel() + _secondarySkills.GetTotalLevel()</c>，也就是四维的<b>基础</b>值加上副技能
    /// 等级之和——神器与经验都不算，所以它衡量的是这位英雄「天生的底子」。AI 用它挑出王国的冠军。
    /// </remarks>
    /// <returns>Sum of the primary skills and of the secondary skill levels.</returns>
    public int GetStatsValue() => _attack + _defense + _power + _knowledge + _secondarySkills.TotalLevel;

    /// <summary>
    /// The sum of the four base primary skills, <c>Skill::Primary::getTotalPrimarySkillLevel()</c>
    /// (<c>skill.h:180-183</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 <c>Heroes</c> 经 <c>HeroBase</c> 继承 <c>Skill::Primary</c>，所以这个方法问的是<b>基础值</b>，
    /// 不含神器加成。战斗 AI 用它判断「这位英雄等级够不够高、值不值得投降/撤退保下来」。
    /// </remarks>
    /// <returns>The sum of attack, defense, power and knowledge.</returns>
    public int GetTotalPrimarySkillLevel() => _attack + _defense + _power + _knowledge;

    /// <summary>
    /// How much the hero is worth at the tavern, <c>Heroes::getRecruitValue()</c> (<c>heroes.cpp:881</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>_army.GetStrength() + (( _bagArtifacts.getArtifactValue() * 10.0 + getStatsValue() ) * SKILL_VALUE)</c>。
    /// 宝物按十份、每份照基础属性一点的千分之一折算的同一把尺子，所以一位带着宝物的英雄比一位空手的同族更值得招募。
    /// </remarks>
    /// <returns>Army strength plus the hero's own worth.</returns>
    public double GetRecruitValue() =>
        Army.GetStrength() + ((_bagArtifacts.GetArtifactValue() * 10.0 + GetStatsValue()) * SkillValue);

    /// <summary>
    /// What meeting this hero is worth to another friendly hero, <c>Heroes::getMeetingValue()</c>
    /// (<c>heroes.cpp:886-901</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两笔账：这一队兵并进去能涨多少战力，加上宝物——宝物按五份、再乘 <see cref="SkillValue"/> 折算。宝物只算装得下的：
    /// 收礼的英雄背包满了，价值就按比例摊薄，魔法书不转移所以先扣掉。
    /// </para>
    /// <para>
    /// 参考在这里留了 TODO：还没考虑诅咒宝物和「对某位英雄无价」的宝物。端口照旧。
    /// </para>
    /// </remarks>
    /// <param name="receivingHero">The hero who would receive the army and the artifacts.</param>
    /// <returns>The value of the meeting.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="receivingHero"/> is <see langword="null"/>.</exception>
    public double GetMeetingValue(Hero receivingHero)
    {
        ArgumentNullException.ThrowIfNull(receivingHero);

        // 魔法书不转移。
        int artCount = _bagArtifacts.CountArtifacts() - _bagArtifacts.Count(ArtifactType.MagicBook);
        int canFit = ArtifactBag.MaxCapacity - receivingHero._bagArtifacts.CountArtifacts();

        double artifactValue = _bagArtifacts.GetArtifactValue() * 5.0;

        if (artCount > canFit)
        {
            artifactValue = canFit * (artifactValue / artCount);
        }

        // 参考 TODO：只留一队兵风险很大，应该只搬有用的那部分。端口照旧。
        return receivingHero._army.GetReinforcementValue(_army) + (artifactValue * SkillValue);
    }

    /// <summary>
    /// The smallest army that is worth a detour for this hero,
    /// <c>Heroes::getAIMinimumJoiningArmyStrength()</c> (<c>heroes.cpp:2228-2282</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 门槛是「英雄自己兵力的一个比例」：兵越多，越不值得为一小队兵绕路。系数按 AI 角色分档：猎人 0.15、
    /// 信使 0.3、侦察兵 0.5、冠军 0.1、战士 0.3。
    /// </para>
    /// <para>
    /// 参考取的是 <c>Troops::GetStrength()</c>（不含英雄攻防与士气幸运），所以这里用
    /// <see cref="Army.Army.GetTroopsStrength"/> 而不是 <see cref="Army.Army.GetStrength"/>。
    /// </para>
    /// </remarks>
    /// <returns>The strength threshold a stack must exceed to be worth visiting.</returns>
    public double GetAiMinimumJoiningArmyStrength()
    {
        double strengthThreshold = AiRole switch
        {
            HeroRole.Hunter => 0.15,
            HeroRole.Courier => 0.3,
            HeroRole.Scout => 0.5,
            HeroRole.Champion => 0.1,
            HeroRole.Fighter => 0.3,
            _ => 0.15,
        };

        return strengthThreshold * _army.GetTroopsStrength();
    }

    /// <summary>
    /// How far a Crystal Ball lets a hero see monster stacks, <c>Heroes::GetVisionsDistance()</c>
    /// (<c>heroes.cpp:1801</c>).
    /// </summary>
    /// <remarks>
    /// 参考直接 <c>return 8;</c>。它同时被水晶球（<c>Kingdom::IsTileVisibleFromCrystalBall</c>）与「视野」法术
    /// 的判定使用，所以放在英雄上而不是王国上。
    /// </remarks>
    /// <returns>The distance in tiles.</returns>
    public static uint GetVisionsDistance() => 8;

    /// <summary>
    /// Whether the hero has enough movement left to leave the tile he stands on, <c>Heroes::CanMove()</c>
    /// (<c>heroes.cpp:1658</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考读脚下瓦片：路上是 <c>roadPenalty</c>（75），否则按地形与寻路技能查表（<c>ground.cpp:169</c>）。
    /// 注意门槛是「够不够迈出<b>这一步</b>」，不是「还有没有一点」——剩 10 点也走不动草地。
    /// </para>
    /// <para>
    /// 英雄不在图上（<see cref="MapIndex"/> 为 <see cref="NoPosition"/> 或越界）时回假，参考那边是断言。
    /// </para>
    /// </remarks>
    /// <param name="world">The map the hero stands on.</param>
    /// <returns><see langword="true"/> when the remaining points pay for the tile under the hero.</returns>
    public bool CanMove(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (MapIndex < 0 || MapIndex >= world.Tiles.Count)
        {
            return false;
        }

        Tile tile = world.Tiles[MapIndex];

        SkillLevel pathfinding = (SkillLevel)Math.Min(GetSecondarySkillValue(SecondarySkill.Pathfinding), (uint)SkillLevel.Expert);
        uint penalty = tile.IsRoad ? MapGrounds.RoadPenalty : MapGrounds.GetPenalty(tile.Ground, pathfinding);

        return MovePoints >= penalty;
    }

    /// <summary>
    /// Whether the hero still has something to do today, <c>Heroes::MayStillMove( ignorePath, ignoreSleeper )</c>
    /// (<c>heroes.cpp:1922</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的三段判据必须按顺序：
    /// </para>
    /// <list type="number">
    /// <item>不在局中（没有颜色、关在牢里）就没有「还能不能动」这回事；</item>
    /// <item>玩家让他原地待命（<see cref="HeroModes.Sleeper"/>）时，除非调用方明说忽略，否则也算不能再动；</item>
    /// <item>已经点出一条合法路线、且第一步的代价付得起时，回「能」——这时甚至不必看脚下这一格；否则退回
    /// <see cref="CanMove"/>。</item>
    /// </list>
    /// <para>
    /// 第二段的 <c>ignoreSleeper</c> 是给「玩家自己点了一下英雄」用的：点醒他要允许，问「还有没有人能动」时
    /// 就不该把他算进来。<see cref="FHeroes2.Game.Kingdom.Kingdom.HeroesMayStillMove"/> 传的是假，所以睡着
    /// 的英雄不会让「结束回合」按钮亮着。
    /// </para>
    /// </remarks>
    /// <param name="world">The map the hero stands on.</param>
    /// <param name="ignorePath">Whether an existing path is disregarded, <c>ignorePath</c>.</param>
    /// <param name="ignoreSleeper">Whether <see cref="HeroModes.Sleeper"/> is disregarded, <c>ignoreSleeper</c>.</param>
    /// <returns><see langword="true"/> when the hero may still move.</returns>
    public bool MayStillMove(World world, bool ignorePath = false, bool ignoreSleeper = false)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!IsActive)
        {
            return false;
        }

        if (!ignoreSleeper && _modes.HasFlag(HeroModes.Sleeper))
        {
            return false;
        }

        if (!ignorePath && Path.IsValidForMovement(world.Size))
        {
            return Path.HasAllowedSteps(MovePoints);
        }

        return CanMove(world);
    }
}
