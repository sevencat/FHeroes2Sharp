using System.Diagnostics;

using FHeroes2.Game.Heroes;

namespace FHeroes2.Game.Ai;

/// <summary>
/// 计算机玩家的英雄走路时要做的四件事：开图、处理脚下的对象、落地后的动作、雷达，
/// <c>Heroes::Action()</c> 的 AI 那一支（<c>heroes_action.cpp:3778-3785</c>）和
/// <c>Heroes::ActionNewPosition()</c> 的 AI 那一半（<c>heroes.cpp:2019-2046</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么是单独一个类。</b> 参考里 <c>Heroes::Action()</c> 自己按 <c>GetKingdom().isControlAI()</c> 分岔：
/// 人类的走 <c>heroes_action.cpp</c> 那一大张表，AI 的直接转给 <c>AI::HeroesAction()</c>。端口没有把分岔塞进
/// 英雄里，而是留在 <see cref="IHeroMovementHandler"/> 这个接缝上——走路的人装哪个处理器，就是哪一支出力。
/// AI 英雄装这一个，人类英雄暂时还装 <see cref="FogHeroMovementHandler"/>（只有开图，因为人类那一支要问
/// 界面层，见 <c>docs/PROGRESS.md</c> §7.4）。
/// </para>
/// <para>
/// <b>它只属于 AI 英雄。</b> 参考的那两个函数对 <c>isControlAI()</c> 的假设在这里变成了安装时的约定，
/// 所以 <see cref="Action"/> 只用 <see cref="Debug.Assert(bool)"/> 守一句，不做运行时的二次分岔：
/// 控制权是建世界时定下的（<see cref="Hero.IsControlHuman"/>），中途不会变。
/// </para>
/// </remarks>
/// <param name="world">The world the hero walks in; the fog belongs to it.</param>
public sealed class AiHeroMovementHandler(World world) : IHeroMovementHandler
{
    /// <summary>
    /// Uncovers the fog around a tile, <c>Heroes::Scout( int32_t )</c> (<c>heroes.cpp:1749</c>).
    /// </summary>
    /// <param name="hero">The hero who scouts.</param>
    /// <param name="tileIndex">Tile whose surroundings get revealed.</param>
    public void Scout(Hero hero, int tileIndex) => hero.Scout(world, tileIndex);

    /// <summary>
    /// What the tile under the hero asks for, <c>AI::HeroesAction()</c> (<c>ai_hero_action.cpp:1977-2243</c>).
    /// </summary>
    /// <param name="hero">The AI hero acting.</param>
    /// <param name="tileIndex">Tile of the object.</param>
    public void Action(Hero hero, int tileIndex)
    {
        Debug.Assert(!hero.IsControlHuman, "The AI movement handler is installed on computer-controlled heroes.");

        AiHeroAction.HeroesAction(hero, tileIndex);
    }

    /// <summary>
    /// Fights the monsters guarding the tile the hero just stepped on, then lets the AI planner react to the new
    /// position, <c>Heroes::ActionNewPosition( bool )</c> (<c>heroes.cpp:2019-2046</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 守卫扫描对人对 AI 都做，只有 <c>ActionNewPosition()</c> 的后半段按控制权分岔。这里既然只服务 AI，
    /// 后半段就无条件调 <see cref="AiPlanner.HeroesActionNewPosition"/>。
    /// </para>
    /// <para>
    /// 先打<b>路线终点那格</b>上的守卫，没有才打第一组邻格的：参考用
    /// <c>GetPath().GetDestinationIndex()</c> 在守卫列表里找，找到谁就打谁
    /// （<c>heroes.cpp:2031-2041</c>）。这时候路线还没弹出下一步，终点仍是整条路线的落点。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero who moved.</param>
    /// <param name="allowMonsterAttack">
    /// <see langword="false"/> for a teleport, which must not drag the hero into a fight
    /// (<c>heroes.cpp:2021</c>): the reference passes false from the dimension door and from both
    /// teleporters, true from a normal step and from a disembarkation.
    /// </param>
    public void ActionNewPosition(Hero hero, bool allowMonsterAttack)
    {
        if (allowMonsterAttack)
        {
            List<int> targets = world.GetMonstersProtectingTile(hero.MapIndex, false);

            if (targets.Count > 0)
            {
                hero.SetMove(false);
                hero.ShowPath(false);

                int destination = hero.Path.DestinationIndex;

                Action(hero, targets.Contains(destination) ? destination : targets[0]);
            }
        }

        AiPlanner.Instance.HeroesActionNewPosition(hero);
    }

    /// <summary>
    /// Redraws the minimap around the hero, <c>Heroes::ScoutRadar()</c>.
    /// </summary>
    /// <remarks>
    /// 雷达是界面元素，端口的小地图在 <c>FHeroes2.UI</c> 里画，AI 这一侧没有可做的。
    /// </remarks>
    /// <param name="hero">The hero who moved.</param>
    public void ScoutRadar(Hero hero)
    {
    }
}
