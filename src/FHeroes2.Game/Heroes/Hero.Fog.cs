using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What a hero can see: how far he uncovers the fog and the uncovering itself, <c>heroes.cpp:1749-1800</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把这两件事放在 <c>Heroes::Scout( tileIndex )</c>（<c>heroes.cpp:1749</c>）与
/// <c>Heroes::GetScoutingDistance()</c>（<c>heroes.cpp:1768</c>）里。前者一行，
/// <c>Maps::ClearFog( tileIndex, GetScoutingDistance(), GetColor() )</c>；后者是三项之和。
/// </para>
/// <para>
/// 拆成独立分部文件而不是塞进 <see cref="Hero.SecondarySkills"/>：侦察术只是距离里的一项，另一项是望远镜那类
/// 神器，用它们的是迷雾而不是技能表。
/// </para>
/// </remarks>
public sealed partial class Hero
{
    /// <summary>
    /// The radius of the disc the hero uncovers, <c>Heroes::GetScoutingDistance()</c>
    /// (<c>heroes.cpp:1768-1772</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:1768-1772</c>。距离是<b>三项之和</b>：
    /// </para>
    /// <list type="number">
    /// <item>神器给的加成（<c>AREA_REVEAL_DISTANCE</c>，望远镜每件 1 格）；</item>
    /// <item>基础半径 <see cref="MapFog.HeroRevealDistance"/>，即参考的
    /// <c>GameStatic::getFogDiscoveryDistance( FogDiscoveryType::HEROES )</c>，值为 4；</item>
    /// <item>侦察术的<b>数值</b>（基础 1／高级 2／专家 3，见
    /// <c>secondarySkillValuesPerLevel</c>），不是等级本身。</item>
    /// </list>
    /// <para>
    /// 所以一位没有神器、也没学侦察术的英雄看到 4 格，专家侦察术加望远镜是 8 格。参考在这里<b>不做上限裁剪</b>；
    /// 距离再大也只是揭不满地图，没有副作用。
    /// </para>
    /// </remarks>
    /// <returns>The scouting distance in tiles.</returns>
    public int GetScoutingDistance() =>
        BagArtifacts.GetTotalArtifactEffectValue(ArtifactBonusType.AreaRevealDistance) +
        MapFog.HeroRevealDistance +
        (int)GetSecondarySkillValue(SecondarySkill.Scouting);

    /// <summary>
    /// Uncovers the fog around a tile for the hero's color, <c>Heroes::Scout( int32_t )</c>
    /// (<c>heroes.cpp:1749-1766</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:1749-1766</c>。真正的工作是 <c>Maps::ClearFog( tileIndex, GetScoutingDistance(),
    /// GetColor() )</c>（端口 <see cref="World.ClearFog(int, int, PlayerColor)"/>），所以「英雄走一步就揭开
    /// 周围」靠的就是这个方法——<see cref="IHeroMovementHandler.Scout"/> 的
    /// <see cref="FogHeroMovementHandler"/> 实现只是把它接到移动上去。
    /// </para>
    /// <para>
    /// <b>瓦片号由调用方给</b>：走路时给的是英雄「即将踏入」的那一格（参考 <c>_moveStep()</c> 就这么传），
    /// 开局侦察给的是英雄脚下那一格。所以 <paramref name="tileIndex"/> 不是
    /// <see cref="MapIndex"/>，虽然两者常常相等。
    /// </para>
    /// <para>
    /// 参考 <c>assert</c> 英雄的颜色不为 <see cref="PlayerColor.None"/>，端口<b>返回 0</b>：中立英雄（还没有
    /// 归属的那种）本来就不该揭开任何人的迷雾，崩溃没有意义。参考在 AI 自动控制模式下会顺手
    /// <c>ScoutRadar()</c>，那是雷达的事，端口没有雷达。
    /// </para>
    /// </remarks>
    /// <param name="world">The world holding the fog.</param>
    /// <param name="tileIndex">The tile whose surroundings are uncovered.</param>
    /// <returns>Number of tiles the hero uncovered; <c>0</c> when his color is
    /// <see cref="PlayerColor.None"/>.</returns>
    public int Scout(World world, int tileIndex)
    {
        ArgumentNullException.ThrowIfNull(world);

        return world.ClearFog(tileIndex, GetScoutingDistance(), Color);
    }
}
