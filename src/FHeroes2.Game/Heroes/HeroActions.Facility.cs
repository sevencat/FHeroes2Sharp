using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Army;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What happened at one of the four objects that raise a primary skill, <c>ActionToPrimarySkillObject()</c>
/// (<c>heroes_action.cpp:1291</c>).
/// </summary>
public enum PrimarySkillObjectOutcome
{
    /// <summary>The action was dispatched to an object that teaches nothing.</summary>
    NotAPrimarySkillObject,

    /// <summary>The hero had already been taught here (<c>heroes_action.cpp:1337</c>).</summary>
    AlreadyLearned,

    /// <summary>The skill went up by one, <c>heroes_action.cpp:1341</c>.</summary>
    Learned,
}

/// <summary>
/// The primary-skill object's answer: whether anything was taught and what.
/// </summary>
/// <param name="Outcome">How the visit ended.</param>
/// <param name="Skill">Which skill rose, or would have risen.</param>
public readonly record struct PrimarySkillObjectResult(PrimarySkillObjectOutcome Outcome, PrimarySkill Skill);

/// <summary>
/// What happened at the Alchemist's Tower, <c>ActionToAlchemistTower()</c> (<c>heroes_action.cpp:3258</c>).
/// </summary>
public enum AlchemistTowerOutcome
{
    /// <summary>Nothing cursed in the pack: the alchemist sends the hero away (<c>heroes_action.cpp:3309</c>).</summary>
    NoCursedItems,

    /// <summary>The hero said no, <c>heroes_action.cpp:3282</c>.</summary>
    Declined,

    /// <summary>The hero agreed but cannot pay (<c>heroes_action.cpp:3301-3305</c>).</summary>
    CannotAfford,

    /// <summary>Every cursed artifact was burned, <c>heroes_action.cpp:3284-3299</c>.</summary>
    Purified,
}

/// <summary>
/// The alchemist's answer: what he found, what he charged and whether he burned it.
/// </summary>
/// <param name="Outcome">How the visit ended.</param>
/// <param name="Cursed">How many cursed artifacts the pack held when he looked.</param>
/// <param name="Paid">What the kingdom was charged, zero unless <see cref="AlchemistTowerOutcome.Purified"/>.</param>
public readonly record struct AlchemistTowerResult(AlchemistTowerOutcome Outcome, int Cursed, Funds Paid);

/// <summary>
/// What happened at the Hut of the Magi, <c>ActionToHutMagi()</c> (<c>heroes_action.cpp:3436</c>).
/// </summary>
public enum HutOfMagiOutcome
{
    /// <summary>The kingdom already heard the magician out; the map stays as it is (<c>heroes_action.cpp:3447</c>).</summary>
    AlreadyTold,

    /// <summary>The eyes of the magi were revealed, <c>heroes_action.cpp:3462-3463</c>.</summary>
    Revealed,
}

/// <summary>
/// The magician's answer: whether he told anything new and how much map it uncovered.
/// </summary>
/// <param name="Outcome">How the visit ended.</param>
/// <param name="Revealed">Tiles that left the fog, zero for <see cref="HutOfMagiOutcome.AlreadyTold"/>.</param>
public readonly record struct HutOfMagiResult(HutOfMagiOutcome Outcome, int Revealed);

public sealed partial class HeroActions
{
    /// <summary>
    /// Rubbing the genie's lamp, <c>ActionToGenieLamp()</c> (<c>heroes_action.cpp:1912</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1912-1938</c>。这是<b>唯一</b>会让 <c>RecruitMonsterFromTile</c> 的
    /// <c>remove</c> 为真的对象：买光了灯就消失（清元数据），下一位英雄连「这地方有盏灯」都看不出来。
    /// </para>
    /// <para>
    /// 三条别的地方都有的规矩在这里都没有：<b>没有访问记录</b>（灯用它自己「消失」表达「用过了」）、
    /// <b>不弹「兵营满了」之外的任何提示</b>、拒绝之后灯还在。灯的存货不随周刷新，开局是多少就是多少。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the lamp lies on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the rubbing ended.</returns>
    public DwellingRecruitResult ActionToGenieLamp(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Troop troop = tile.GetTroop(world);

        if (!troop.IsValid)
        {
            return new DwellingRecruitResult(DwellingRecruitOutcome.NobodyHere, MonsterType.Unknown, 0, default);
        }

        if (!_host.Ask(HeroActionQuestion.RubGenieLamp, troop))
        {
            return new DwellingRecruitResult(DwellingRecruitOutcome.Declined, troop.Monster, troop.Count, default);
        }

        return RecruitMonsterFromTile(tile, troop, remove: true);
    }

    /// <summary>
    /// Browsing the trading post, <c>ActionToTradingPost()</c> (<c>heroes_action.cpp:1284</c>).
    /// </summary>
    /// <remarks>
    /// 参考只有一行 <c>Dialog::Marketplace( kingdom, true )</c>：<b>不记访问、不改状态</b>，买卖全在窗口里完成，
    /// 所以这里也只把窗口转出去。这一点和城堡里的市场是同一个窗口，参数只影响标题。
    /// </remarks>
    public void ActionToTradingPost() => _host.OpenMarketplace(Kingdom, fromTradingPost: true);

    /// <summary>
    /// The four one-shot objects that raise a primary skill, <c>ActionToPrimarySkillObject()</c>
    /// (<c>heroes_action.cpp:1291</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:1291-1352</c>。堡垒 +1 防御、雇佣兵营 +1 攻击、巫医小屋 +1 知识、石圈 +1 力量。
    /// 四座共用一段代码，只有「加哪一项」和文案不同，文案归界面。
    /// </para>
    /// <para>
    /// 判断用的是<b>英雄级</b>访问记录（<see cref="Hero.IsVisited(Tile)"/>），不是王国级：同一位英雄不会两次拿到
    /// 同一座建筑的那一点，但另一位英雄还可以。加完才记访问，所以中途失败不会白白用掉机会——这一支没有失败。
    /// </para>
    /// </remarks>
    /// <param name="objectType">The object, without its action bit.</param>
    /// <param name="tileIndex">The tile it stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>Whether the skill went up.</returns>
    public PrimarySkillObjectResult ActionToPrimarySkillObject(
        Mp2ObjectType objectType,
        int tileIndex,
        World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        PrimarySkill skill = objectType switch
        {
            Mp2ObjectType.Fort => PrimarySkill.Defense,
            Mp2ObjectType.MercenaryCamp => PrimarySkill.Attack,
            Mp2ObjectType.WitchDoctorsHut => PrimarySkill.Knowledge,
            Mp2ObjectType.StandingStones => PrimarySkill.Power,
            _ => PrimarySkill.Unknown,
        };

        if (skill == PrimarySkill.Unknown)
        {
            Debug.Assert(false, $"A primary-skill action was dispatched to {objectType}.");

            return new PrimarySkillObjectResult(PrimarySkillObjectOutcome.NotAPrimarySkillObject, skill);
        }

        if (_hero.IsVisited(world.GetTile(tileIndex)))
        {
            return new PrimarySkillObjectResult(PrimarySkillObjectOutcome.AlreadyLearned, skill);
        }

        _hero.IncreasePrimarySkill(skill);
        _hero.SetVisited(tileIndex, objectType);

        return new PrimarySkillObjectResult(PrimarySkillObjectOutcome.Learned, skill);
    }

    /// <summary>
    /// The Alchemist's Tower, <c>ActionToAlchemistTower()</c> (<c>heroes_action.cpp:3258</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3258-3311</c>。四道门：包里的<b>诅咒物品数量</b>为 0 → 直接赶人（连问都不问）；
    /// 玩家说不要 → 什么都不发生；说了要但钱不够 → 他隔着门笑你；钱够 → 扣钱、把每一件诅咒物品<b>就地清成
    /// 未知</b>（槽位不动，位置有意义）。
    /// </para>
    /// <para>
    /// 价钱是固定的 <see cref="PaymentConditions.ForAlchemist"/>（750 金），<b>与件数无关</b>：一件和五件一个价。
    /// 「钱不够」那一支不扣钱也不清物品，所以英雄可以攒够钱再来。
    /// </para>
    /// <para>
    /// 与别的对象不同，这里<b>既不记访问也不清元数据</b>：塔永远在那里，每次带着新的诅咒物品来都可以再烧。
    /// </para>
    /// </remarks>
    /// <returns>What the alchemist found and did.</returns>
    public AlchemistTowerResult ActionToAlchemistTower()
    {
        int cursed = CountCursedArtifacts();

        if (cursed == 0)
        {
            return new AlchemistTowerResult(AlchemistTowerOutcome.NoCursedItems, 0, default);
        }

        Funds payment = PaymentConditions.ForAlchemist();

        if (!_host.Ask(HeroActionQuestion.PayAlchemist, (uint)payment.Gold))
        {
            return new AlchemistTowerResult(AlchemistTowerOutcome.Declined, cursed, default);
        }

        if (!Kingdom.AllowPayment(payment))
        {
            return new AlchemistTowerResult(AlchemistTowerOutcome.CannotAfford, cursed, default);
        }

        Kingdom.OddFundsResource(payment);

        // 逐个槽位烧：诅咒物品不能同时持有两件同名的，但按槽位遍历与参考的循环形状一致。
        foreach (ArtifactType artifact in _hero.BagArtifacts.Slots)
        {
            if (ArtifactTable.Get(artifact).Curses.Count > 0)
            {
                _hero.BagArtifacts.Remove(artifact);
            }
        }

        return new AlchemistTowerResult(AlchemistTowerOutcome.Purified, cursed, payment);
    }

    /// <summary>
    /// The Hut of the Magi, <c>ActionToHutMagi()</c> (<c>heroes_action.cpp:3436</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:3436-3510</c>。话是每次都说的（「他告诉你远近有些地方值得一去」），
    /// 但<b>揭图只揭一次</b>——按<b>王国级</b>记录判断，同阵营的盟友也算来过。
    /// </para>
    /// <para>
    /// 揭的是<b>所有法师之眼</b>周围 <see cref="MapFog.MagiEyesRevealDistance"/>（9 格）的圆，一座一片，
    /// 地图上没有眼睛就什么都不揭。参考随后把镜头一片片摇过去给你看，那是动画，端口只报揭了多少格。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the hut stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>Whether anything new was told and how much map it uncovered.</returns>
    public HutOfMagiResult ActionToHutMagi(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (Kingdom.IsVisited(Mp2ObjectType.HutOfMagi))
        {
            return new HutOfMagiResult(HutOfMagiOutcome.AlreadyTold, 0);
        }

        Kingdom.SetVisited(tileIndex, Mp2ObjectType.HutOfMagi);

        int revealed = 0;

        foreach (int eyeIndex in world.AllEyeOfMagi)
        {
            revealed += world.ClearFog(eyeIndex, MapFog.MagiEyesRevealDistance, _hero.Color);
        }

        return new HutOfMagiResult(HutOfMagiOutcome.Revealed, revealed);
    }

    /// <summary>
    /// The blind seer, <c>ActionToOracle()</c> (<c>heroes_action.cpp:3023</c>).
    /// </summary>
    /// <remarks>
    /// 参考：<c>heroes_action.cpp:3023-3040</c>。一段固定文案加一张盗贼公会报表（<c>Dialog::ThievesGuild(true)</c>，
    /// 那个 <c>true</c> 就是「这是神谕，不是偷听来的」）。<b>不记访问、不改状态</b>：报表只读，随时可以再来看。
    /// </remarks>
    public void ActionToOracle() => _host.OpenThievesGuild(Kingdom);

    /// <summary>
    /// Counts the cursed artifacts in the hero's pack, the <c>count_if</c> of <c>heroes_action.cpp:3263</c>.
    /// </summary>
    /// <returns>How many slots hold an artifact that carries a curse.</returns>
    private int CountCursedArtifacts()
    {
        int cursed = 0;

        foreach (ArtifactType artifact in _hero.BagArtifacts.Slots)
        {
            if (ArtifactTable.Get(artifact).Curses.Count > 0)
            {
                ++cursed;
            }
        }

        return cursed;
    }
}
