using FHeroes2.Assets.Maps;

using FHeroes2.Game.Battle;
using FHeroes2.Game.Maps;

using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Heroes;

public sealed partial class Hero
{
    /// <summary>
    /// Takes the hero off the map for good after he lost a battle, <c>Heroes::Dismiss()</c> (<c>heroes.cpp:1959</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:1959-2002</c>。整段就是「把这个人从三处擦掉」——军队、王国的名册、瓦片上的站位——
    /// 再清模式位、清移动点、置 <see cref="HeroModes.Action"/>。
    /// </para>
    /// <para>
    /// <b>军队看输法</b>（<c>1965-1972</c>）：没投降的（战败或撤退）重置成种族的初始军队，因为下一位来招他的人
    /// 拿到的是这个模板；投降的留着残兵——他立刻就能被招回来，军队是他被招回来时的样子；投降且军队已经空了，
    /// 重置成名册里第一个初级兵，否则招回来是个空将。
    /// </para>
    /// <para>
    /// 为什么 <paramref name="reason"/> 收的是 <see cref="BattleOutcome"/> 的位而不是一个布尔：参考的参数就是
    /// <c>Battle::RESULT_*</c> 这一串位，唯一被读的是「有没有投降」，但调用方手里正好有整串位。
    /// </para>
    /// <para>
    /// <b>没搬的两句</b>（<c>1994-2000</c>）：<c>SAVEMP</c> 位与王国的投降名册
    /// （<c>kingdom.appendSurrenderedHero()</c>）。前者是「今天已经动过，别再给他移动点」的界面记号，端口的
    /// 移动点由 <see cref="Hero.Movement"/> 自己按「动过没有」算；后者要配酒馆当天重招的规则，而端口的招人还没
    /// 有那条路（<c>PLAN.md</c> §5.6 的 P2）。两样都不影响「这个人已经离场」这个结论，也不影响谁赢。
    /// </para>
    /// <para>
    /// 为什么要传 <paramref name="world"/>：参考的 <c>world</c> 是全局单例，端口的 <see cref="World"/> 是显式参数，
    /// 而 <c>world.getTile( GetIndex() ).setHero( nullptr )</c> 与「王国在哪」都要经过它。
    /// </para>
    /// </remarks>
    /// <param name="world">The world the hero stands in.</param>
    /// <param name="reason">How he left, <c>Battle::RESULT_*</c> bits.</param>
    public void Dismiss(World world, BattleOutcome reason)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (IsAvailableForHire)
        {
            return;
        }

        bool surrendered = (reason & BattleOutcome.Surrender) != 0;

        if (!surrendered)
        {
            _army.Reset(true);
        }
        else if (!_army.IsValid)
        {
            _army.Reset();
        }

        PlayerColor heroColor = Color;

        if (heroColor != PlayerColor.None)
        {
            PlayerKingdom kingdom = world.Kingdoms.GetKingdom(heroColor);
            kingdom.RemoveHero(this);
        }

        SetColor(PlayerColor.None);

        if (MapIndex >= 0)
        {
            world.GetTile(MapIndex).SetHero(world, null);
        }

        SetMapIndex(-1);

        ClearModes();
        SetMove(false);
        SetModes(HeroModes.Action);
    }
}
