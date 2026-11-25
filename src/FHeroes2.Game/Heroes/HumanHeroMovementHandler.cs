using System.Diagnostics;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// 人类玩家的英雄走路时要做的四件事：开图、处理脚下的对象、落地后的动作、雷达，
/// <c>Heroes::Action()</c> 的人类那一支（<c>heroes_action.cpp:3747-3777</c>）与
/// <c>Heroes::ActionNewPosition()</c> 的人类那一半（<c>heroes.cpp:2019-2046</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>与 AI 的分岔在安装时。</b>参考里 <c>Heroes::Action()</c> 自己按 <c>GetKingdom().isControlAI()</c> 分岔：
/// 人类的走 <c>heroes_action.cpp</c> 那一大张表（各 <c>ActionTo*</c>，遇到要问玩家的点就开界面），AI 的直接转给
/// <c>AI::HeroesAction()</c>。端口把这个分岔放在 <see cref="IHeroMovementHandler"/> 接缝上：人类英雄装这一个，
/// 电脑英雄装 <see cref="Ai.AiHeroMovementHandler"/>。
/// </para>
/// <para>
/// <b>它只属于人类英雄。</b>控制权是建世界时定下的（<see cref="Hero.IsControlHuman"/>），中途不会变，所以
/// <see cref="Action"/> 只用 <see cref="Debug.Assert(bool)"/> 守一句，不在运行时再分岔一次。
/// </para>
/// <para>
/// <b>只在装了界面宿主之后才装。</b>没有宿主的运行（命令行、测试）里没有玩家在点，走到怪物格上直接开打既没画面
/// 也没提示，于是 <see cref="AdventureSession.AttachMovementHandler"/> 退回只开图的
/// <see cref="FogHeroMovementHandler"/>。
/// </para>
/// </remarks>
/// <param name="world">The world the hero walks in; the fog and the interface host belong to it.</param>
public sealed class HumanHeroMovementHandler(World world) : IHeroMovementHandler
{
    /// <summary>
    /// Uncovers the fog around a tile, <c>Heroes::Scout( int32_t )</c> (<c>heroes.cpp:1749</c>).
    /// </summary>
    /// <param name="hero">The hero who scouts.</param>
    /// <param name="tileIndex">Tile whose surroundings get revealed.</param>
    public void Scout(Hero hero, int tileIndex) => hero.Scout(world, tileIndex);

    /// <summary>
    /// Runs the action of the object on a tile through the shared <see cref="HeroActions"/> table.
    /// </summary>
    /// <remarks>
    /// 参考的人类这一支与 AI 走的是两张不同的表，但端口的人类动作表（<see cref="HeroActions.Action"/>）已经照
    /// <c>heroes_action.cpp</c> 的那张 <c>switch</c> 写完，要问玩家的地方通过 <see cref="World.HeroActionHost"/>
    /// 交回界面层。
    /// </remarks>
    /// <param name="hero">The human hero acting.</param>
    /// <param name="tileIndex">Tile of the object.</param>
    public void Action(Hero hero, int tileIndex)
    {
        Debug.Assert(hero.IsControlHuman, "The human movement handler is installed on human-controlled heroes.");

        new HeroActions(hero, world.Kingdoms, world.HeroActionHost).Action(tileIndex, world);
    }

    /// <summary>
    /// Fights the monsters guarding the tile the hero just stepped on, <c>Heroes::ActionNewPosition( bool )</c>
    /// (<c>heroes.cpp:2019-2046</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 守卫扫描对人对 AI 都做（<c>heroes.cpp:2024-2043</c> 在分岔之前）；分岔之后的 AI 规划（
    /// <c>AI::HeroesActionNewPosition()</c>）人类这一侧没有，所以到这里就结束。
    /// </para>
    /// <para>
    /// 先打<b>路线终点那格</b>上的守卫，没有才打第一组邻格的：参考用 <c>GetPath().GetDestinationIndex()</c>
    /// 在守卫列表里找，找到谁就打谁（<c>heroes.cpp:2031-2041</c>）。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero who moved.</param>
    /// <param name="allowMonsterAttack">
    /// <see langword="false"/> for a teleport, which must not drag the hero into a fight
    /// (<c>heroes.cpp:2021</c>).
    /// </param>
    public void ActionNewPosition(Hero hero, bool allowMonsterAttack)
    {
        if (!allowMonsterAttack)
        {
            return;
        }

        List<int> targets = world.GetMonstersProtectingTile(hero.MapIndex, false);

        if (targets.Count == 0)
        {
            return;
        }

        hero.SetMove(false);
        hero.ShowPath(false);

        int destination = hero.Path.DestinationIndex;

        Action(hero, targets.Contains(destination) ? destination : targets[0]);
    }

    /// <summary>
    /// Redraws the minimap around the hero, <c>Heroes::ScoutRadar()</c>.
    /// </summary>
    /// <remarks>
    /// 雷达是界面元素，端口的小地图在 <c>FHeroes2.UI</c> 里画，规则层没有可做的。
    /// </remarks>
    /// <param name="hero">The hero who moved.</param>
    public void ScoutRadar(Hero hero)
    {
    }
}
