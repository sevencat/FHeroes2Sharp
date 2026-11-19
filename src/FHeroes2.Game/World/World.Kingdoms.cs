using FHeroes2.Assets.Maps;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.System;

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
    /// 参考：<c>world.cpp:287</c>，实现 <c>kingdom.cpp:688</c>。参考从 <c>Settings</c> 取「谁在局中」与「谁是电脑」
    /// （<c>Players::GetPlayerControl()</c>），而 <c>Players</c> 的槽位是剧本选择时由 <c>Players::Init( fi )</c> 建好的。
    /// 端口的 <see cref="Kingdoms.Init"/> 仍然只收一个电脑色集合，所以答案先由 <see cref="ComputerColorsOf"/> 算出来。
    /// </para>
    /// <para>
    /// 文件头里的两张表（<c>Mp2FileInfo.HumanPlayers</c> / <c>ComputerPlayers</c>）说的是「谁可以让人类玩、谁可以让
    /// 电脑玩」，它们经常同时包含同一个颜色；直接拿 <c>ComputerPlayers</c> 当 AI 会把整张图都判成电脑，单机开图后
    /// 没有任何一方能行动。<c>Players::Init( fi )</c> 加 <c>SetStartGame()</c> 按参考的规则只留一个人类（第一个可被
    /// 人类玩的颜色），其余全部交给电脑——这正是剧本选择里「一名人类玩家」的默认答案。
    /// </para>
    /// <para>起始资源按难度分玩家/电脑两张表（<see cref="Kingdom.GetStartingResources"/>）。</para>
    /// </remarks>
    /// <param name="info">The map header, which carries the colors in play and the difficulty.</param>
    private void CreateMapKingdoms(Mp2FileInfo info) =>
        _kingdoms.Init(info.KingdomColors, info.Difficulty, ComputerColorsOf(info));

    /// <summary>
    /// Which colors of the map the computer plays, the answer of <c>Players::Init( fi )</c> and
    /// <c>Players::SetStartGame()</c> (<c>players.cpp:299-352</c> and <c>:487-508</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header separates "colours a person may play" from "colours the computer may play", and the two masks
    /// overlap on most maps, so neither one alone says who sits at the keyboard. The reference builds the player
    /// slots and lets <c>SetStartGame()</c> leave exactly the human-only colours as humans, or, when the map has
    /// none, promotes the first colour a person may play; every other slot falls to the computer.
    /// </para>
    /// <para>
    /// The rule is repeated here instead of asking <see cref="Players"/> because the slots are a static table
    /// shared by the whole process: filling it while a map is being loaded would make two worlds loaded at the
    /// same time (as the tests do) fight over it. <see cref="AdventureSession.Start"/> fills the real table when
    /// a game actually begins, which is also when the reference fills it.
    /// </para>
    /// </remarks>
    /// <param name="info">The map header.</param>
    /// <returns>The colors of <paramref name="info"/> that fall to the computer.</returns>
    private static PlayerColor ComputerColorsOf(Mp2FileInfo info)
    {
        PlayerColor human = PlayerColor.None;

        if (Settings.Get().IsGameType(GameTypes.Multi))
        {
            human = info.HumanOnlyColors & info.KingdomColors;
        }

        if (human == PlayerColor.None)
        {
            foreach (PlayerColor color in PlayerColors.Ordered)
            {
                if ((info.HumanPlayers & color) != 0 && (info.KingdomColors & color) != 0)
                {
                    human = color;
                    break;
                }
            }
        }

        return info.KingdomColors & ~human;
    }

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

        // 参考的 Heroes::isControlHuman() 问的是 Players::GetPlayerControl( color )；端口把答案记在英雄上
        //（Hero.IsControlHuman）。王国初始化时已经从地图文件头拿到电脑色，所以名册建好之后把这个答案抄下去，
        //免得每个读英雄的地方都要再查一次王国。
        foreach (Hero hero in _heroes)
        {
            if (hero.Color != PlayerColor.None)
            {
                hero.IsControlHuman = _kingdoms.GetKingdom(hero.Color).IsControlHuman;
            }
        }
    }
}
