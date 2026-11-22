using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;

using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The four things an adventure spell asks of the player: pick a destination, pick a town, open the View World
/// window, show what a monster stack would do, <c>heroes_spell.cpp:64-357</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考里 <c>ActionSpellCast()</c> 的每一支都直接调界面：<c>Interface::AdventureMap::GetDimensionDoorDestination()</c>、
/// <c>Dialog::selectKingdomCastle()</c>、<c>ViewWorld::ViewWorldWindow()</c>、
/// <c>fheroes2::showStandardTextMessage()</c>。端口没有全局界面单例（ADR-122），
/// 所以「必须当场拿到答案」的四类点收在这里：
/// </para>
/// <list type="number">
/// <item><see cref="SelectDimensionDoorDestination"/> —— 次元门落到哪一格；</item>
/// <item><see cref="SelectTownPortalTown"/> —— 城镇门户去哪个城；</item>
/// <item><see cref="OpenViewWorld"/> —— 观地/观矿/观宝/观城/观英雄/观全图开的那个窗口；</item>
/// <item><see cref="ShowMonsterVision"/> —— <c>Visions</c> 对每堆怪弹的那一条「它们会怎样」。</item>
/// </list>
/// <para>
/// <b>只收「答案决定接下来发生什么」的交互。</b>淡入淡出、音效、雷达重绘、屏幕居中都不是问题，而是效果，
/// 由调用方按返回值自己补——与 <see cref="IHeroActionHost"/> 同一条划分线。
/// </para>
/// <para>
/// 缺省实现是 <see cref="NullHeroSpellCastHost"/>：目的地与城池一律答「取消」，窗口与对话都不开。
/// 于是依赖玩家选择的两个法术（次元门、城镇门户）在这种宿主下返回 <see langword="false"/>、不扣法力；
/// 观地类法术照常扣法力（参考里关掉窗口也照样算施法成功），<c>Visions</c> 也照样记进王国。
/// </para>
/// </remarks>
public interface IHeroSpellCastHost
{
    /// <summary>
    /// Lets the player pick where Dimension Door lands, <c>Interface::AdventureMap::GetDimensionDoorDestination()</c>
    /// (<c>game_interface.cpp:175-323</c>).
    /// </summary>
    /// <remarks>
    /// 参考把一张一格的方框画在地图上、让玩家在框内点选，框边长 <paramref name="distance"/> 格；
    /// 它<b>不是射程</b>——次元门没有距离上限，框外的格子根本点不到而已。
    /// </remarks>
    /// <param name="sourceIndex">Tile the hero stands on.</param>
    /// <param name="distance">Side of the selection square, in tiles, <c>Spell.DimensionDoorDistance()</c>.</param>
    /// <param name="isWater">Whether the hero stands on water, which decides land or sea tiles may be picked.</param>
    /// <returns>The tile index, or -1 when the player cancelled.</returns>
    int SelectDimensionDoorDestination(int sourceIndex, int distance, bool isWater);

    /// <summary>
    /// Lets the player pick the town Town Portal goes to, <c>Dialog::selectKingdomCastle()</c>
    /// (<c>dialog_selectitems.cpp:860-896</c>).
    /// </summary>
    /// <remarks>
    /// 参考传 <c>notOccupiedByHero = true</c>，所以列表里已经剔掉了有英雄驻守的城；端口照做，
    /// <paramref name="castles"/> 是同一份过滤后的清单，顺序与王国一致。选中项按<b>城的入口下标</b>返回。
    /// </remarks>
    /// <param name="castles">The towns the hero may port to, never empty.</param>
    /// <returns>The tile index of the chosen town's gate, or -1 when the player cancelled.</returns>
    int SelectTownPortalTown(IReadOnlyList<CastleTown> castles);

    /// <summary>
    /// Opens the View World window on one of its modes, <c>ViewWorld::ViewWorldWindow()</c>.
    /// </summary>
    /// <param name="mode">What the window shows.</param>
    void OpenViewWorld(ViewWorldMode mode);

    /// <summary>
    /// Shows one monster stack the way the Visions spell shows it,
    /// <c>heroes_spell.cpp:318-321</c>.
    /// </summary>
    /// <remarks>
    /// 参考按 <paramref name="join"/> 的 <c>reason</c> 选一段文案，再把怪物画像一起弹出来。端口把
    /// 已经算好的答案交给宿主，文案与布局由界面层决定。
    /// </remarks>
    /// <param name="troop">The stack on the tile.</param>
    /// <param name="join">What the stack would do if the hero walked into it.</param>
    void ShowMonsterVision(Troop troop, in NeutralMonsterJoiningCondition join);
}

/// <summary>
/// The host a hero starts with: cancels every choice, opens nothing. See <see cref="IHeroSpellCastHost"/>.
/// </summary>
public sealed class NullHeroSpellCastHost : IHeroSpellCastHost
{
    /// <summary>The shared instance; the host is stateless.</summary>
    public static NullHeroSpellCastHost Instance { get; } = new();

    /// <inheritdoc />
    public int SelectDimensionDoorDestination(int sourceIndex, int distance, bool isWater) => -1;

    /// <inheritdoc />
    public int SelectTownPortalTown(IReadOnlyList<CastleTown> castles) => -1;

    /// <inheritdoc />
    public void OpenViewWorld(ViewWorldMode mode)
    {
    }

    /// <inheritdoc />
    public void ShowMonsterVision(Troop troop, in NeutralMonsterJoiningCondition join)
    {
    }
}
