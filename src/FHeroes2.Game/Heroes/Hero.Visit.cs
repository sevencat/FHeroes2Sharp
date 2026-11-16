using FHeroes2.Assets.Maps;

using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// A hero's own visit book, <c>Heroes::visit_object</c>, and the three ticks that thin it out
/// (<c>Heroes::ActionNewDay</c>, <c>ActionNewWeek</c>, <c>ActionAfterBattle</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>heroes.h</c> 的 <c>std::list&lt;IndexObject&gt; visit_object</c>、<c>heroes.cpp</c> 的
/// <c>SetVisited</c>/<c>isVisited</c>/<c>isObjectTypeVisited</c>/<c>getAllVisitedObjectTypes</c>、
/// <c>heroes.cpp:634-660</c> 的三个 tick。
/// </para>
/// <para>
/// 为什么英雄要自己记一份，王国也要记一份：同一个风车，每位英雄一周各有一次；同一座石阵，全王国只算一次。
/// 参考用 <c>Visit::LOCAL</c>/<c>Visit::GLOBAL</c> 在调用处选一本账簿，端口把选择留在动作层
/// （<see cref="HeroActions"/>）：<b>这里只有英雄自己的那本</b>，全局登记直接调 <see cref="Kingdom.SetVisited"/>。
/// 于是英雄不认识王国，也就不需要 <c>hero.GetKingdom()</c> 那条还没搭的链接。
/// </para>
/// <para>
/// 记录的是「格子 + 对象类型」（<see cref="IndexObject"/>），与王国那本完全同构：去重键包含对象类型，
/// 因为一个格子上的对象会被事件、编辑器与战后的地形换掉。
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>The objects this hero has visited, newest first, <c>visit_object</c>.</summary>
    private readonly List<IndexObject> _visitedObjects = [];

    /// <summary>
    /// The objects this hero has visited, newest first, <c>visit_object</c> (<c>heroes.h</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes.h</c> 的 <c>visit_object</c>。与王国的同名列表一样按「最新在前」维护。
    /// </remarks>
    public IReadOnlyList<IndexObject> VisitedObjects => _visitedObjects;

    /// <summary>
    /// Whether this hero has visited that tile for that object, <c>isVisited( index, type )</c>.
    /// </summary>
    /// <remarks>参考：<c>heroes.cpp</c> 的 <c>isVisited( const int32_t, const MP2::MapObjectType )</c>。</remarks>
    /// <param name="index">Tile index on the map.</param>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> when the pair is in the hero's visit list.</returns>
    public bool IsVisited(int index, Mp2ObjectType objectType) =>
        _visitedObjects.Exists(entry => entry.Index == index && entry.Object == objectType);

    /// <summary>
    /// Whether this hero has visited that tile for the object standing on it, <c>isVisited( tile )</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp</c> 的 <c>isVisited( const Maps::Tile & )</c>，比较的是瓦片缓存的主对象类型。
    /// </para>
    /// <para>
    /// 瓦片那份带着动作位（<see cref="Mp2ObjectType.ActionGazebo"/>），名册里记的是折过的值
    /// （<see cref="Mp2ObjectType.Gazebo"/>，ADR-133），所以比较前先摘掉那一位；不摘的话「第二次来」永远
    /// 查不出来，因为 <see cref="SetVisited"/> 存的和这里查的差 128。
    /// </para>
    /// </remarks>
    /// <param name="tile">The tile.</param>
    /// <returns><see langword="true"/> when the tile's main object is in the hero's visit list.</returns>
    public bool IsVisited(Tile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        return IsVisited(tile.Index, Mp2ObjectTypes.NonActionType(tile.MainObjectType));
    }

    /// <summary>
    /// Whether this hero has visited any object of that type, <c>isObjectTypeVisited()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes.cpp</c> 的 <c>isObjectTypeVisited</c>。魔法井用它回答「今天喝过了没有」——
    /// 一天寿命的访问记录还没被清掉，再喝就是白跑一趟。
    /// </remarks>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> when an object of that type is in the hero's visit list.</returns>
    public bool IsObjectTypeVisited(Mp2ObjectType objectType) =>
        _visitedObjects.Exists(entry => entry.Object == objectType);

    /// <summary>
    /// The distinct object types this hero has visited, <c>getAllVisitedObjectTypes()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes.cpp</c> 的 <c>getAllVisitedObjectTypes</c>，返回的是按类型去重的集合。运气/士气
    /// （<see cref="GetLuck"/>、<see cref="GetMorale"/>）按这份集合查表：去过三座神殿也只算一次 +2。
    /// </remarks>
    /// <returns>The distinct object types, in the order they were first found.</returns>
    public IReadOnlyList<Mp2ObjectType> GetAllVisitedObjectTypes() =>
        _visitedObjects.Select(entry => entry.Object).Distinct().ToList();

    /// <summary>
    /// Remembers a visit in the hero's own book, the <c>Visit::LOCAL</c> half of <c>SetVisited()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp</c> 的 <c>SetVisited( index, type, scope )</c> 的本地分支——已在表里就直接返回，
    /// 否则插到表头。全局分支（<c>Visit::GLOBAL</c>）在端口里由调用方直接写
    /// <see cref="Kingdom.SetVisited"/>，因为英雄不该认识王国。
    /// </para>
    /// <para>
    /// <see cref="Mp2ObjectType.None"/> 不记：没有对象的格子不算拜访。
    /// </para>
    /// </remarks>
    /// <param name="index">Tile index on the map.</param>
    /// <param name="objectType">The object visited.</param>
    public void SetVisited(int index, Mp2ObjectType objectType)
    {
        if (objectType == Mp2ObjectType.None)
        {
            return;
        }

        var visit = new IndexObject(index, objectType);

        if (IsVisited(index, objectType))
        {
            return;
        }

        _visitedObjects.Insert(0, visit);
    }

    /// <summary>
    /// Forgets every visit, <c>visit_object.clear()</c>.
    /// </summary>
    /// <remarks>
    /// 参考在英雄换图（<c>Heroes::Clear</c>/<c>reset</c>）时清空；端口把它留给重置英雄状态的调用方。
    /// </remarks>
    public void ClearVisitedObjects() => _visitedObjects.Clear();

    /// <summary>
    /// The hero's part of a new day, <c>Heroes::ActionNewDay()</c> (<c>heroes.cpp:634</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:634-660</c>。顺序照抄：移动点恢复满 → 第二天起每天回一点法力 → 清一天寿命的访问
    /// 记录 → 清「存档时的移动点」模式位。
    /// </para>
    /// <para>
    /// 最后那位（<c>SAVEMP</c>）不在端口的 <see cref="HeroModes"/> 里：它是「读档后恢复当天的移动点」用的，
    /// 等存档层落地时再加，加在这里不动别处。
    /// </para>
    /// <para>
    /// 与王国不同，这里的清理<b>不看「在不在局中」</b>：参考的 <c>vec_heroes</c> 只装着还活着的英雄。
    /// </para>
    /// </remarks>
    /// <param name="dayCount">Days since the start of the game, <c>world.CountDay()</c>.</param>
    public void ActionNewDay(uint dayCount)
    {
        SetMovePoints(GetMaxMovePoints());

        // heroes.cpp:640：第一天不回法力——那天的法力是地图给的初始值。
        if (dayCount > 1)
        {
            ReplenishSpellPoints();
        }

        // heroes.cpp:645：一天寿命的对象（魔法井）明天又能喝一次。
        _visitedObjects.RemoveAll(Visit.IsDayLife);
    }

    /// <summary>
    /// The hero's part of a new week, <c>Heroes::ActionNewWeek()</c> (<c>heroes.cpp:662</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes.cpp:662-670</c>。只清一周寿命的访问记录（风车、水车、各处巢穴）。
    /// </remarks>
    public void ActionNewWeek() => _visitedObjects.RemoveAll(Visit.IsWeekLife);

    /// <summary>
    /// The hero's part of the end of a battle, <c>Heroes::ActionAfterBattle()</c> (<c>heroes.cpp:672</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:672-680</c>。战后清掉「战前拜访」的那一批（喷泉、仙女环、雕像、美人鱼、绿洲、
    /// 水坑、神殿、浮标）——这些对象的效果是「下一战之前有效」，打完就该再走一趟。
    /// </para>
    /// <para>
    /// 同时置上 <see cref="HeroModes.Action"/>：参考用它挡住「动作动画与行走动画同时播放」，动作层收尾时
    /// 自己清掉（<c>Hero.Movement</c> 已照此实现）。
    /// </para>
    /// </remarks>
    public void ActionAfterBattle()
    {
        _visitedObjects.RemoveAll(Visit.IsBattleLife);
        SetModes(HeroModes.Action);
    }

    /// <summary>
    /// How many spell points the hero recovers per day, <c>getDailyRestoredSpellPoints()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:1000</c> 附近的 <c>getDailyRestoredSpellPoints</c>：基础值来自
    /// <see cref="GameStatic.HeroesRestoreSpellPointsPerDay"/>，加上神秘术的等级，再加上宝物的加成。
    /// </para>
    /// <para>
    /// 宝物那一项（<c>ART_RESTORE_SPELLPOINTS_PER_DAY</c> 一类）没搬：包里的宝物枚举还没有这几个值。
    /// </para>
    /// </remarks>
    /// <returns>The points restored each day.</returns>
    public uint GetDailyRestoredSpellPoints() =>
        GameStatic.HeroesRestoreSpellPointsPerDay + (uint)SecondarySkills.GetValue(SecondarySkill.Mysticism);

    /// <summary>
    /// Tops up the spell points, <c>Heroes::_replenishSpellPoints()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes.cpp</c> 的 <c>_replenishSpellPoints</c>：已满就不动，否则加上今天的恢复量并封顶在
    /// <see cref="GetMaxSpellPoints"/>——不会因为跨天攒出超过上限的法力。
    /// </remarks>
    private void ReplenishSpellPoints()
    {
        uint max = GetMaxSpellPoints();

        if (_spellPoints < max)
        {
            _spellPoints = Math.Min(max, _spellPoints + GetDailyRestoredSpellPoints());
        }
    }
}
