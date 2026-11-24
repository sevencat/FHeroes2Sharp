using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What happened at a dwelling that hands its troops over for free, <c>ActionToDwellingJoinMonster()</c>
/// (<c>heroes_action.cpp:2237</c>).
/// </summary>
public enum DwellingJoinOutcome
{
    /// <summary>The action was dispatched to something that is not a joining dwelling.</summary>
    NotADwelling,

    /// <summary>The dwelling was already emptied: a visit record and a shrug (<c>heroes_action.cpp:2260-2263</c>).</summary>
    NobodyHere,

    /// <summary>The hero said no, <c>heroes_action.cpp:2268</c>.</summary>
    Declined,

    /// <summary>All seven army slots are taken, <c>heroes_action.cpp:2271-2274</c>.</summary>
    RanksFull,

    /// <summary>They joined: the dwelling is now empty, <c>heroes_action.cpp:2277-2280</c>.</summary>
    Joined,
}

/// <summary>
/// The joining dwelling's answer: which way it went and who was standing there.
/// </summary>
/// <param name="Outcome">How the visit ended.</param>
/// <param name="Monster">The creature the dwelling holds, <see cref="MonsterType.Unknown"/> when there was none.</param>
/// <param name="Count">How many stood there — for <see cref="DwellingJoinOutcome.Joined"/> that is also how many joined.</param>
public readonly record struct DwellingJoinResult(DwellingJoinOutcome Outcome, MonsterType Monster, uint Count);

/// <summary>
/// What happened at a dwelling that sells its troops, <c>ActionToDwellingRecruitMonster()</c>
/// (<c>heroes_action.cpp:2356</c>).
/// </summary>
public enum DwellingRecruitOutcome
{
    /// <summary>The action was dispatched to something that does not sell troops.</summary>
    NotADwelling,

    /// <summary>The dwelling was already emptied (<c>heroes_action.cpp:2380-2384</c>).</summary>
    NobodyHere,

    /// <summary>The hero said no, <c>heroes_action.cpp:2387</c>.</summary>
    Declined,

    /// <summary>All seven army slots are taken (<c>RecruitMonsterFromTile</c>, <c>heroes_action.cpp:272-278</c>).</summary>
    RanksFull,

    /// <summary>Some were bought and paid for, <c>heroes_action.cpp:300-312</c>.</summary>
    Recruited,
}

/// <summary>
/// A dwelling's answer to a purchase request: what was bought, how many and for how much.
/// </summary>
/// <remarks>
/// <see cref="Count"/> 在 <see cref="DwellingRecruitOutcome.Recruited"/> 之外是「还摆着多少个」，让界面能说出
/// 「兵营满了」或「你拒绝了这 X 个」；<see cref="Paid"/> 只在真的成交时才非空。
/// </remarks>
/// <param name="Outcome">How the purchase ended.</param>
/// <param name="Monster">The creature on offer, <see cref="MonsterType.Unknown"/> when there was none.</param>
/// <param name="Count">How many were bought, or how many stayed when nothing was.</param>
/// <param name="Paid">What the kingdom was charged.</param>
public readonly record struct DwellingRecruitResult(
    DwellingRecruitOutcome Outcome,
    MonsterType Monster,
    uint Count,
    Funds Paid);

/// <summary>
/// What happened at a dwelling the hero has to fight first, <c>ActionToDwellingBattleMonster()</c>
/// (<c>heroes_action.cpp:2426</c>).
/// </summary>
public enum DwellingBattleOutcome
{
    /// <summary>The action was dispatched to something that is not one of the three guarded dwellings.</summary>
    NotADwelling,

    /// <summary>Already subdued and emptied: a visit record, nothing else (<c>heroes_action.cpp:2437-2441</c>).</summary>
    Empty,

    /// <summary>The guards are gone but the hero does not want to buy, <c>heroes_action.cpp:2447</c>.</summary>
    DeclinedToRecruit,

    /// <summary>The hero does not want to fight, <c>heroes_action.cpp:2463</c> — the dwelling is not even visited.</summary>
    DeclinedToFight,

    /// <summary>The guards won; the hero was dismissed, <c>heroes_action.cpp:2471-2473</c>.</summary>
    Lost,

    /// <summary>The guards lost but nothing was bought, <c>heroes_action.cpp:2511</c>.</summary>
    Won,

    /// <summary>The guards lost and some of them were hired, <c>heroes_action.cpp:2503-2508</c>.</summary>
    Recruited,

    /// <summary>The guards lost and the hero's ranks are full, <c>heroes_action.cpp:2500</c>.</summary>
    RanksFull,
}

/// <summary>
/// The guarded dwelling's answer: which way it went, what the fight was worth and what was bought afterwards.
/// </summary>
/// <param name="Outcome">How the visit ended.</param>
/// <param name="Experience">Experience the battle was worth, zero when there was no battle.</param>
/// <param name="Monster">The creature the dwelling holds, <see cref="MonsterType.Unknown"/> when there was none.</param>
/// <param name="Count">How many were bought, or how many stayed when nothing was.</param>
/// <param name="Paid">What the kingdom was charged for the recruits.</param>
public readonly record struct DwellingBattleResult(
    DwellingBattleOutcome Outcome,
    uint Experience,
    MonsterType Monster,
    uint Count,
    Funds Paid);

public sealed partial class HeroActions
{
    /// <summary>
    /// A dwelling that gives its troops away, <c>ActionToDwellingJoinMonster()</c> (<c>heroes_action.cpp:2237</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes_action.cpp:2237-2290</c>。九个居所（农舍、树屋、弓箭手小屋……守望塔）走这一支，白送。
    /// 有三处细节值得记：<b>兵营满时居所原封不动</b>（不扣数量）；<b>收下之后数量清零而不是清元数据</b>——怪物种类
    /// 留在瓦片上，下一位英雄看到的还是「这里曾经住着枪兵」；以及<b>不管哪条路都要记全局访问</b>。
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the visit ended and who was there.</returns>
    public DwellingJoinResult ActionToDwellingJoinMonster(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!IsJoinDwelling(objectType))
        {
            Debug.Assert(false, $"A joining-home action was dispatched to {objectType}.");

            return new DwellingJoinResult(DwellingJoinOutcome.NotADwelling, MonsterType.Unknown, 0);
        }

        Tile tile = world.GetTile(tileIndex);
        Troop troop = tile.GetTroop(world);
        DwellingJoinResult result;

        if (!troop.IsValid)
        {
            result = new DwellingJoinResult(DwellingJoinOutcome.NobodyHere, MonsterType.Unknown, 0);
        }
        else if (!_host.Ask(HeroActionQuestion.JoinDwellingMonsters, troop))
        {
            result = new DwellingJoinResult(DwellingJoinOutcome.Declined, troop.Monster, troop.Count);
        }
        else if (!_hero.Army.CanJoinTroop(troop.Monster))
        {
            result = new DwellingJoinResult(DwellingJoinOutcome.RanksFull, troop.Monster, troop.Count);
        }
        else
        {
            // 参考把数量清成 0 而不是清元数据：种类还在瓦片上，回来的英雄仍能看出这儿住过什么。
            tile.SetMonsterCount(world, 0);
            _hero.Army.JoinTroop(troop.Monster, troop.Count, false);

            result = new DwellingJoinResult(DwellingJoinOutcome.Joined, troop.Monster, troop.Count);
        }

        Kingdom.SetVisited(tileIndex, objectType);

        return result;
    }

    /// <summary>
    /// A dwelling that sells its troops, <c>ActionToDwellingRecruitMonster()</c> (<c>heroes_action.cpp:2356</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2356-2420</c>。「要买吗」与被派到的对象类型无关，先按对象查一段文案
    /// （<c>msg_full</c>），不在那十座里就直接返回——<b>连访问都不记</b>（<c>2370</c> 的 <c>default: return</c>）。
    /// </para>
    /// <para>
    /// 真正的买卖在 <c>RecruitMonsterFromTile</c>（<c>262-330</c>）：兵营满就先说不行，否则开数量窗，
    /// 成交则「买光就清瓦片、没买光就减数量」，再扣钱、加兵。数量为 0 等于取消，什么都不改。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the purchase ended.</returns>
    public DwellingRecruitResult ActionToDwellingRecruitMonster(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!IsRecruitDwelling(objectType))
        {
            Debug.Assert(false, $"A recruiting-home action was dispatched to {objectType}.");

            return new DwellingRecruitResult(DwellingRecruitOutcome.NotADwelling, MonsterType.Unknown, 0, default);
        }

        Tile tile = world.GetTile(tileIndex);
        Troop troop = tile.GetTroop(world);
        DwellingRecruitResult result;

        if (!troop.IsValid)
        {
            result = new DwellingRecruitResult(DwellingRecruitOutcome.NobodyHere, MonsterType.Unknown, 0, default);
        }
        else if (_host.Ask(HeroActionQuestion.RecruitDwellingMonsters, troop))
        {
            result = RecruitMonsterFromTile(world, tile, troop, remove: false);
        }
        else
        {
            result = new DwellingRecruitResult(DwellingRecruitOutcome.Declined, troop.Monster, troop.Count, default);
        }

        Kingdom.SetVisited(tileIndex, objectType);

        return result;
    }

    /// <summary>
    /// A dwelling that has to be beaten before it sells, <c>ActionToDwellingBattleMonster()</c>
    /// (<c>heroes_action.cpp:2426</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:2426-2522</c>。三座（龙之城、亡灵之城、巨魔桥）共用这一支：颜色不是
    /// <see cref="PlayerColor.None"/> 就说明<b>守卫已经被谁清过了</b>（记成 <see cref="PlayerColor.Unused"/>），
    /// 于是跳过战斗直接进招募；否则先问「打不打」。
    /// </para>
    /// <para>
    /// <b>战斗不会消耗居所的存货</b>：守军是瓦片人口的副本，参考在胜利后重新读一次瓦片人口并断言它仍有效
    /// （<c>2496-2498</c>），端口不写回残兵，一切照这个约定。赢来的经验归英雄，颜色改成
    /// <see cref="PlayerColor.Unused"/>，然后才是「要招吗」。
    /// </para>
    /// <para>
    /// 「不打」那一支<b>不记访问</b>（<c>2463-2466</c>）；打输了也不记——两次都是「这地方跟我没关系」。
    /// 其余各支都记全局访问。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the visit ended.</returns>
    public DwellingBattleResult ActionToDwellingBattleMonster(Mp2ObjectType objectType, int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!IsBattleDwelling(objectType))
        {
            Debug.Assert(false, $"A guarded-home action was dispatched to {objectType}.");

            return new DwellingBattleResult(DwellingBattleOutcome.NotADwelling, 0, MonsterType.Unknown, 0, default);
        }

        Tile tile = world.GetTile(tileIndex);

        if (tile.GetColor(world) != PlayerColor.None)
        {
            Troop subdued = tile.GetTroop(world);

            if (!subdued.IsValid)
            {
                Kingdom.SetVisited(tileIndex, objectType);

                return new DwellingBattleResult(DwellingBattleOutcome.Empty, 0, MonsterType.Unknown, 0, default);
            }

            if (!_host.Ask(HeroActionQuestion.RecruitDwellingMonsters, subdued))
            {
                Kingdom.SetVisited(tileIndex, objectType);

                return new DwellingBattleResult(
                    DwellingBattleOutcome.DeclinedToRecruit,
                    0,
                    subdued.Monster,
                    subdued.Count,
                    default);
            }

            DwellingRecruitResult purchase = RecruitMonsterFromTile(world, tile, subdued, remove: false);

            Kingdom.SetVisited(tileIndex, objectType);

            return new DwellingBattleResult(
                RefineRecruitOutcome(purchase.Outcome),
                0,
                purchase.Monster,
                purchase.Count,
                purchase.Paid);
        }

        Troop guarded = tile.GetTroop(world);

        if (!_host.Ask(HeroActionQuestion.FightDwellingMonsters, guarded))
        {
            return new DwellingBattleResult(DwellingBattleOutcome.DeclinedToFight, 0, MonsterType.Unknown, 0, default);
        }

        ArmyStack defenders = new();
        defenders.SetFromTile(world, tile);

        BattleResult battle = Fight(world, _hero, defenders, tileIndex, castle: null);

        if (!battle.IsAttackerWin)
        {
            BattleLose(world, _hero, battle, attacker: true);

            return new DwellingBattleResult(
                DwellingBattleOutcome.Lost,
                0,
                guarded.Monster,
                guarded.Count,
                default);
        }

        _hero.IncreaseExperience(battle.AttackerExperience, world.MapSeed);

        // 守卫清了：记成「无主但已打服」，下一位英雄来了直接进招募那一支。
        world.CaptureObject(tileIndex, PlayerColor.Unused);

        Troop survivors = tile.GetTroop(world);

        Debug.Assert(survivors.IsValid, "The dwelling's stock must outlive its guards.");

        if (!_host.Ask(HeroActionQuestion.RecruitDefeatedDwellingMonsters, survivors))
        {
            Kingdom.SetVisited(tileIndex, objectType);

            return new DwellingBattleResult(
                DwellingBattleOutcome.Won,
                battle.AttackerExperience,
                survivors.Monster,
                survivors.Count,
                default);
        }

        DwellingRecruitResult afterBattle = RecruitMonsterFromTile(world, tile, survivors, remove: false);

        Kingdom.SetVisited(tileIndex, objectType);

        return new DwellingBattleResult(
            afterBattle.Outcome == DwellingRecruitOutcome.Declined
                ? DwellingBattleOutcome.Won
                : RefineRecruitOutcome(afterBattle.Outcome),
            battle.AttackerExperience,
            afterBattle.Monster,
            afterBattle.Count,
            afterBattle.Paid);
    }

    /// <summary>
    /// The buying half of every dwelling, <c>RecruitMonsterFromTile()</c> (<c>heroes_action.cpp:262</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:262-330</c>。三件事按顺序做：兵营满就先回「不行」（连数量窗都不开）；开着窗
    /// 问数量；成交时先改瓦片再扣钱再加兵。
    /// </para>
    /// <para>
    /// <b>「买光」有两种收尾</b>：<paramref name="remove"/> 为真且买走了全部，就清元数据让这地方彻底空掉；
    /// 否则只把数量减下去。参考只有<b>精灵灯</b>传真（<c>heroes_action.cpp:1936</c>，搓完灯这地方就该消失），
    /// 三支居所动作全部传假；精灵灯还没转，端口先留着这个参数以对齐签名。
    /// </para>
    /// <para>
    /// 数量窗是界面给的，理论上不该超过存货；端口在断言之后仍夹一次，免得宿主违约把瓦片数量写成负数回绕。
    /// </para>
    /// </remarks>
    /// <param name="world">The world the dwelling belongs to.</param>
    /// <param name="tile">The dwelling.</param>
    /// <param name="troop">What it holds.</param>
    /// <param name="remove">Whether buying everything empties the object completely.</param>
    /// <returns>How the purchase ended.</returns>
    private DwellingRecruitResult RecruitMonsterFromTile(World world, Tile tile, Troop troop, bool remove)
    {
        if (!_hero.Army.CanJoinTroop(troop.Monster))
        {
            return new DwellingRecruitResult(DwellingRecruitOutcome.RanksFull, troop.Monster, troop.Count, default);
        }

        uint recruit = _host.AskRecruitCount(troop.Monster, troop.Count, Kingdom);

        if (recruit == 0)
        {
            return new DwellingRecruitResult(DwellingRecruitOutcome.Declined, troop.Monster, troop.Count, default);
        }

        Debug.Assert(recruit <= troop.Count, "The recruit window must not sell more than the dwelling holds.");

        recruit = Math.Min(recruit, troop.Count);

        if (remove && recruit == troop.Count)
        {
            // 参考在这里播拾取音效并把对象整个淡出；淡出归界面（Changed 让调用方重画这一格）。
            tile.ResetObjectMetadata();
        }
        else
        {
            tile.SetMonsterCount(world, troop.Count - recruit);
        }

        Funds payment = troop.GetCost() * recruit;

        Kingdom.OddFundsResource(payment);
        _hero.Army.JoinTroop(troop.Monster, recruit, false);

        return new DwellingRecruitResult(DwellingRecruitOutcome.Recruited, troop.Monster, recruit, payment);
    }

    /// <summary>
    /// Maps a purchase onto the guarded dwelling's richer outcome set.
    /// </summary>
    /// <param name="outcome">The purchase's outcome.</param>
    /// <returns>The same answer in <see cref="DwellingBattleOutcome"/> terms.</returns>
    private static DwellingBattleOutcome RefineRecruitOutcome(DwellingRecruitOutcome outcome) => outcome switch
    {
        DwellingRecruitOutcome.Recruited => DwellingBattleOutcome.Recruited,
        DwellingRecruitOutcome.RanksFull => DwellingBattleOutcome.RanksFull,
        _ => DwellingBattleOutcome.Won,
    };

    /// <summary>
    /// Whether the object gives its troops away, the <c>OBJ_TREE_HOUSE</c> group of
    /// <c>heroes_action.cpp:3968-3976</c>.
    /// </summary>
    /// <param name="objectType">An object type without its action bit.</param>
    /// <returns><see langword="true"/> for the nine joining homes.</returns>
    private static bool IsJoinDwelling(Mp2ObjectType objectType) => objectType
        is Mp2ObjectType.TreeHouse
        or Mp2ObjectType.ArcherHouse
        or Mp2ObjectType.GoblinHut
        or Mp2ObjectType.DwarfCottage
        or Mp2ObjectType.HalflingHole
        or Mp2ObjectType.PeasantHut
        or Mp2ObjectType.WatchTower
        or Mp2ObjectType.Excavation
        or Mp2ObjectType.Cave;

    /// <summary>
    /// Whether the object sells its troops without a fight, the <c>OBJ_RUINS</c> group of
    /// <c>heroes_action.cpp:3980-3984</c> plus the Price of Loyalty group of <c>4027-4032</c>.
    /// </summary>
    /// <param name="objectType">An object type without its action bit.</param>
    /// <returns><see langword="true"/> for the nine unguarded dwellings.</returns>
    private static bool IsRecruitDwelling(Mp2ObjectType objectType) => objectType
        is Mp2ObjectType.Ruins
        or Mp2ObjectType.TreeCity
        or Mp2ObjectType.WagonCamp
        or Mp2ObjectType.DesertTent
        or Mp2ObjectType.EarthAltar
        or Mp2ObjectType.AirAltar
        or Mp2ObjectType.FireAltar
        or Mp2ObjectType.WaterAltar
        or Mp2ObjectType.BarrowMounds;

    /// <summary>
    /// Whether the object has to be beaten first, the <c>OBJ_DRAGON_CITY</c> group of
    /// <c>heroes_action.cpp:3987-3990</c>.
    /// </summary>
    /// <param name="objectType">An object type without its action bit.</param>
    /// <returns><see langword="true"/> for the three guarded dwellings.</returns>
    private static bool IsBattleDwelling(Mp2ObjectType objectType) => objectType
        is Mp2ObjectType.DragonCity
        or Mp2ObjectType.CityOfDead
        or Mp2ObjectType.TrollBridge;
}
