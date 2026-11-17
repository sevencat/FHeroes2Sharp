using FHeroes2.Assets.Maps;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game;

/// <summary>
/// The kingdoms of the world: the seven slots of <c>vec_kingdoms</c>, and the two steps that fill them.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>world.h:312</c> vec_kingdoms、<c>world.cpp:287</c>（<c>World::Defaults()</c> 的第一件事是
/// <c>vec_kingdoms.Init()</c>）、<c>:308</c>（<c>World::Reset()</c> 的 <c>vec_kingdoms.clear()</c>）、
/// <c>world_loadmap.cpp:1443-1445</c>（装载末尾把英雄与城塞进各自王国的名册）。
/// </para>
/// <para>
/// 装载顺序照抄参考：王国先于英雄表建立（参考在 <c>Defaults</c> 里就是这个次序，因为英雄初始化要问玩家的起始
/// 资源），名册则在整张图读完、瓦片上的英雄都摆好之后才填——名册按颜色分类，而颜色是装载过程中才写进英雄与城的。
/// </para>
/// </remarks>
public sealed partial class World
{
    private readonly Kingdoms _kingdoms = new();

    /// <summary>
    /// The seven kingdoms of the world, <c>vec_kingdoms</c> (<c>world.h:312</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>world.h:297</c> GetKingdoms。六个玩家色各一个槽，第七个槽装「没有颜色」的查询，见
    /// <see cref="Kingdoms.GetKingdom"/>。
    /// </remarks>
    public Kingdoms Kingdoms => _kingdoms;

    /// <summary>
    /// The kingdom of a color, <c>World::GetKingdom()</c> (<c>world.h:275</c>).
    /// </summary>
    /// <remarks>
    /// 参考只有一行 <c>return vec_kingdoms.GetKingdom( color );</c>，跨槽写回那份内存是 <c>Kingdom</c> 的引用语义在
    /// 做事；端口的 <see cref="Kingdom"/> 是引用类型，同样直接返回。
    /// </remarks>
    /// <param name="color">The player color; <see cref="PlayerColor.None"/> returns the seventh slot.</param>
    /// <returns>The kingdom.</returns>
    public Kingdom.Kingdom GetKingdom(PlayerColor color) => _kingdoms.GetKingdom(color);

    /// <summary>
    /// Starts a kingdom for every color the map puts in play, <c>vec_kingdoms.Init()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>world.cpp:287</c>，实现 <c>kingdom.cpp:688</c>。参考从 <c>Settings</c> 取「谁在局中」与「谁是电脑」，
    /// 端口取地图文件头里的两张表（<c>Mp2FileInfo.KingdomColors</c> / <c>ComputerPlayers</c>）——地图装载时
    /// <c>Settings</c> 还不存在，而文件头对这两件事说得和它一样清楚。
    /// </para>
    /// <para>起始资源按难度分玩家/电脑两张表（<see cref="Kingdom.GetStartingResources"/>）。</para>
    /// </remarks>
    /// <param name="info">The map header, which carries the colors in play and the difficulty.</param>
    private void CreateMapKingdoms(Mp2FileInfo info) =>
        _kingdoms.Init(info.KingdomColors, info.Difficulty, info.ComputerPlayers);

    /// <summary>
    /// Puts every hero and town of the loaded map into the roster of its kingdom,
    /// <c>vec_kingdoms.AddHeroes()</c> / <c>vec_kingdoms.AddCastles()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>world_loadmap.cpp:1443-1445</c>，顺序是英雄在前、城在后；两者都只收「颜色不是 NONE」的条目，
    /// 所以地图上的中立城与守卫英雄不会进任何名册。
    /// </remarks>
    private void AttachMapRosters()
    {
        _kingdoms.AddHeroes(_heroes);
        _kingdoms.AddCastles(_castles);
    }
}
