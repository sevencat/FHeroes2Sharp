using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Game.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// 英雄入伙：把一位「还没有主人」的英雄交给某个王国，<c>Heroes::Recruit()</c>
/// (<c>heroes.cpp:1105</c>)。
/// </summary>
/// <remarks>
/// <para>
/// <b>两条入口</b>：酒馆里付钱雇一个（<c>Heroes::Recruit( const Castle &amp; )</c>），以及从监牢里放出来
/// （<see cref="HeroActions.ActionToJail"/>）。两支最后都走这里，所以这里要做的正是「一位自由英雄变成在编英雄」
/// 的全部副作用：清掉 <see cref="HeroModes.Recruit"/> 与 <see cref="HeroModes.Jail"/> 两个模式位、上色、
/// 站到瓦片上、发一天的行动力、没有军队就给一副、把英雄挂进王国名册。
/// </para>
/// <para>
/// <b>为什么不传 <c>PlayerColor</c> 而传王国</b>：参考写的是 <c>Recruit( const PlayerColor, const Point &amp; )</c>，
/// 里面再用 <c>world.GetKingdom( col )</c> 取王国。端口没有 <c>World.GetKingdom</c>（王国集合由 <c>Kingdoms</c>
/// 持有，见 ADR-122），所以把王国直接递进来——这一层已经是「谁雇的」，颜色由王国的
/// <see cref="Kingdom.Kingdom.Color"/> 回答，不会出现「颜色与名册不一致」的中间态。
/// </para>
/// <para>
/// <b>不搬的四步，都因为前置系统不在</b>：<c>setAlphaValue( 255 )</c>（端口还没有英雄的透明值）、
/// <c>kingdom.GetRecruits()</c> 的刷新（端口的 <c>GetRecruits</c> 只返回已结算的一对，ADR-118）、
/// <c>Scout( GetIndex() )</c> 与 <c>ScoutRadar()</c>（迷雾与雷达都未搬）。
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// Hires the hero for <paramref name="kingdom"/> and puts him on <paramref name="mapIndex"/>,
    /// <c>Heroes::Recruit( const PlayerColor, const fheroes2::Point &amp; )</c> (<c>heroes.cpp:1105</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:1105-1150</c>。两道门，顺序不能换：<b>已经有主人</b>的英雄直接拒绝（
    /// <see cref="PlayerColor.None"/> 之外的颜色），再看王国<b>名额与钱</b>（这里不查钱，理由见类注释）。
    /// </para>
    /// <para>
    /// <b>行动力给满</b>：参考只在 <c>!Modes( SAVEMP )</c> 时给，<c>SAVEMP</c> 是「这天已经动过」的存档标记。
    /// 端口还没有这个模式位，于是等价于永远走给满的分支——刚出狱/刚雇来的英雄本来就该有整天的行动力。
    /// </para>
    /// <para>
    /// <b>军队</b>：参考只在 <c>!_army.isValid()</c> 时才 <c>Reset</c>，这里照做——监狱里的英雄可能在地图里就
    /// 带着兵（<see cref="LoadFromMP2"/> 会读兵），有兵就不覆盖。
    /// </para>
    /// <para>
    /// <b>瓦片占位是最后一步</b>：<see cref="Maps.Tile.SetHero"/> 会把瓦片当前显示的物件「寄存」到英雄身上
    /// （监狱就藏在这下面），英雄一离开那块瓦片，空的监狱重新露出来。参考的 <c>world.getTile( pt ).setHero( this )</c>
    /// 顺序相同。
    /// </para>
    /// </remarks>
    /// <param name="kingdom">The kingdom that hires the hero.</param>
    /// <param name="mapIndex">Tile the hero is placed on, i.e. the jail tile for a prisoner.</param>
    /// <param name="world">The world holding the tile.</param>
    /// <returns>
    /// <see langword="true"/> when the hero joined; <see langword="false"/> when he already has a colour or
    /// the kingdom has no seat left.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="kingdom"/> or <paramref name="world"/> is <see langword="null"/>.</exception>
    public bool Recruit(Kingdom.Kingdom kingdom, int mapIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(kingdom);
        ArgumentNullException.ThrowIfNull(world);

        if (Color != PlayerColor.None)
        {
            Debug.Assert(false, "The hero has already been hired by a kingdom.");

            return false;
        }

        if (!kingdom.AllowRecruitHero(false))
        {
            return false;
        }

        ResetModes(HeroModes.Recruit);
        ResetModes(HeroModes.Jail);

        SetColor(kingdom.Color);
        SetMapIndex(mapIndex);
        Direction = Maps.Direction.Right;

        SetMovePoints(GetMaxMovePoints(false, world));

        if (!Army.IsValid)
        {
            Army.Reset(false);
        }

        world.GetTile(mapIndex).SetHero(world, this);
        kingdom.AddHero(this);

        return true;
    }
}
