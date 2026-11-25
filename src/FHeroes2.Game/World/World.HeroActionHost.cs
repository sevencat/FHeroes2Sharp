using FHeroes2.Game.Heroes;

namespace FHeroes2.Game;

/// <summary>
/// The interface layer that answers a human hero's actions, <see cref="IHeroActionHost"/>, hung on the world so
/// the walking handlers can reach it without threading an extra parameter through every map-object call.
/// </summary>
/// <remarks>
/// <para>
/// 参考里这些「停下来问玩家 / 开界面」的点直接调全局的 <c>Interface::AdventureMap::Get()</c>。端口没有那个全局，
/// 把它放在世界上最省事：世界本来就是这一局运行时的家（雾、被占领对象、每日事件都挂在这里），而界面层的实现
/// 又与这一局一一对应，换图时和 <see cref="Reset"/> 一起丢掉正好。
/// </para>
/// <para>
/// 缺省是 <see cref="NullHeroActionHost"/>：AI、测试、以及还没装界面的命令行都会落到它身上，行为等于参考的
/// 「没有界面」那一支（战斗交给电脑、按钮全答否）。
/// </para>
/// <para>
/// 它<b>不</b>进存档：宿主是这一局界面层的对象，存档只留地图与规则状态。
/// </para>
/// </remarks>
public sealed partial class World
{
    /// <summary>
    /// Who answers the human hero's actions: the messages, the battle screen and the castle screen.
    /// </summary>
    /// <remarks>
    /// 装它的地方是 <see cref="AdventureSession.Start"/>；人类英雄的走路处理器每做一次动作都读它
    /// （<see cref="HumanHeroMovementHandler"/>）。它也是「有没有界面」的判据：
    /// <see cref="AdventureSession.AttachMovementHandler"/> 只在这个不是 <see cref="NullHeroActionHost"/> 时
    /// 才给人类英雄装动作版，否则退回只开图。
    /// </remarks>
    public IHeroActionHost HeroActionHost { get; set; } = NullHeroActionHost.Instance;
}
