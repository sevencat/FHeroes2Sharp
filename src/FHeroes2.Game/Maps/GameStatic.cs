using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The static game-wide tables, the parts of <c>game_static.cpp</c> that the object effects need.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>game_static.cpp:167-198</c>（两张对象效果表）、<c>game_static.cpp:447</c>（每日回复法力）、
/// <c>game_static.cpp</c> 的 <c>getMovementPointBonus</c>（访问绿洲/水坑的移动点奖励）。参考里这一整篇是
/// 一个 1000 行的「游戏常数 + 对象效果」杂物间，端口按用途把它分成几处，这里是「按对象类型查的三张表」。
/// </para>
/// <para>
/// 为什么这些表要独立成一处：它们同时被英雄的运气/士气（<c>Heroes::GetLuck()</c>/<c>GetMorale()</c>，
/// 读的是<b>访问过的对象</b>）和访问动作（<c>heroes_action.cpp</c>，写的是<b>移动点</b>）用到。放任何一边，
/// 另一边都得反向依赖；放这里两边都只是查表。
/// </para>
/// </remarks>
public static class GameStatic
{
    /// <summary>
    /// The spell points every hero recovers per day, <c>GameStatic::GetHeroesRestoreSpellPointsPerDay()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>game_static.cpp:447</c>，常数 1。神秘术与宝物在此之上加成，见
    /// <see cref="Heroes.Hero.GetDailyRestoredSpellPoints"/>。
    /// </remarks>
    public const uint HeroesRestoreSpellPointsPerDay = 1;

    /// <summary>
    /// How much defense a unit loses while it stands in a moat, <c>GameStatic::GetBattleMoatReduceDefense()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>game_static.cpp:273-276</c>，常数 3。用在两处：护城河的说明文字
    /// （<see cref="Castle.Castle.GetDescriptionBuilding"/>），以及战斗里对护城河内单位减防——战斗系统还没搬。
    /// </remarks>
    public const int BattleMoatReduceDefense = 3;

    /// <summary>
    /// How much luck an object grants once visited, <c>GameStatic::getObjectLuckEffect()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>game_static.cpp:167-183</c>。正数来自仙女环/喷泉/雕像/美人鱼的<b>一次拜访</b>——但运气并不在
    /// 拜访时被加到一个字段上，而是 <c>GetLuck()</c> 每次重算时从这里查出来（ADR-122）。
    /// </para>
    /// <para>
    /// 负数（黑猫、金字塔）也是同一张表：踏进去就被扣，而且扣多少只跟「访问过」有关，跟访问几次无关——
    /// 调用方按类型去重（<see cref="Heroes.Hero.GetAllVisitedObjectTypes"/>）。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object type.</param>
    /// <returns>The luck change, zero for objects that do not move it.</returns>
    public static int GetObjectLuckEffect(Mp2ObjectType objectType) => objectType switch
    {
        Mp2ObjectType.FaerieRing or Mp2ObjectType.Fountain or Mp2ObjectType.Idol or Mp2ObjectType.Mermaid => 1,
        Mp2ObjectType.BlackCat or Mp2ObjectType.Pyramid => -2,
        _ => 0,
    };

    /// <summary>
    /// How much morale an object grants once visited, <c>GameStatic::getObjectMoraleEffect()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>game_static.cpp:185-203</c>。神殿是 +2，比绿洲与水坑的 +1 更值钱；沉船/墓地/幽灵船的残骸是 -1。
    /// </remarks>
    /// <param name="objectType">The object type.</param>
    /// <returns>The morale change, zero for objects that do not move it.</returns>
    public static int GetObjectMoraleEffect(Mp2ObjectType objectType) => objectType switch
    {
        Mp2ObjectType.Buoy or Mp2ObjectType.Oasis or Mp2ObjectType.WateringHole => 1,
        Mp2ObjectType.Temple => 2,
        Mp2ObjectType.BlackCat => 3,
        Mp2ObjectType.DerelictShip or Mp2ObjectType.Graveyard or Mp2ObjectType.Shipwreck => -1,
        _ => 0,
    };

    /// <summary>
    /// The movement points a visit to an object grants outright, <c>GameStatic::getMovementPointBonus()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>game_static.cpp:283-296</c>。三处：绿洲 800、水坑 400、马厩 400（参考里马厩与水坑共用一个
    /// <c>case</c>，所以不是很多人以为的 500）。这笔奖励是<b>当场</b>加到 <see cref="Heroes.Hero.MovePoints"/>
    /// 上的（<c>IncreaseMovePoints</c>），不像运气/士气那样靠事后重算。
    /// </para>
    /// <para>
    /// 马厩的另一半（给在场的骑兵免费升级）在 <c>ActionToUpgradeArmyObject</c> 里，属于军队升级流程，还没搬；
    /// 这里只负责移动点这一句，调用方要记得那条缺口。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object type.</param>
    /// <returns>The bonus in movement points, zero for objects that do not grant one.</returns>
    public static uint GetMovementPointBonus(Mp2ObjectType objectType) => objectType switch
    {
        Mp2ObjectType.Oasis => 800,
        Mp2ObjectType.Stables or Mp2ObjectType.WateringHole => 400,
        _ => 0,
    };

    /// <summary>
    /// Whether the butler of Xanadu lets the hero in, <c>GameStatic::isHeroWorthyToVisitXanadu()</c>
    /// (<c>game_static.cpp:298</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>game_static.cpp:298-308</c>。两条路：<b>十级以上</b>直接放行；否则看外交术——基础/高级/专家分别要
    /// 八级/六级/四级以上（都是严格大于 7/5/3）。没有外交术就永远进不去，等级再高也不行，除非过了十级这条硬门槛。
    /// </para>
    /// <para>
    /// 注意与「已访问」无关：参考先查名册再看资格，端口把前者留在动作层（<see cref="Mp2ObjectType.Xanadu"/> 的
    /// 瓦片名册），这里只回答资格。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero asking for admission.</param>
    /// <returns><see langword="true"/> when the butler should open the door.</returns>
    public static bool IsHeroWorthyToVisitXanadu(Heroes.Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        int level = hero.Level;

        if (level > 9)
        {
            return true;
        }

        return hero.GetLevelSkill(Heroes.SecondarySkill.Diplomacy) switch
        {
            Heroes.SkillLevel.Basic => level > 7,
            Heroes.SkillLevel.Advanced => level > 5,
            Heroes.SkillLevel.Expert => level > 3,
            _ => false,
        };
    }

    /// <summary>
    /// The share of the weakest stack a whirlpool drowns, in percent,
    /// <c>GameStatic::GetLostOnWhirlpoolPercent()</c> (<c>game_static.cpp:107</c>).
    /// </summary>
    /// <remarks>
    /// 参考有两层转手：<c>Game::GetWhirlpoolPercent()</c>（<c>game.cpp:446</c>）只是把这里转给
    /// <c>Heroes::ActionToWhirlpools</c> 用，真正的常数是这里的 50。用法不是「砍掉一半数量」而是「砍掉一半
    /// <b>血量</b>」，再按单兵血量折回数量（<see cref="Army.Troop.GetCountFromHitPoints"/>）——这正是为什么
    /// 一只 20 点血的兵受了 10 点伤之后还是「一只」而不是半只。
    /// </remarks>
    public const uint LostOnWhirlpoolPercent = 50;
}
