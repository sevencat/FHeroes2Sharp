using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Maps;

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
