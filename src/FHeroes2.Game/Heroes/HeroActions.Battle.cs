using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

using ArmyStack = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// What happened when the hero walked onto wandering monsters, <c>ActionToMonster()</c>
/// (<c>heroes_action.cpp:351-496</c>).
/// </summary>
/// <remarks>
/// 一支怪物只有四种下场：加入（白送或收钱）、被打散（没打，它们跑了）、被打光、把英雄打走。
/// 参考用 <c>destroy</c> 与战果两个局部变量拼出这些组合，端口把它折成这里的取值。
/// </remarks>
public enum MonsterEncounterOutcome
{
    /// <summary>No monsters on the tile; nothing happened (<c>assert( 0 )</c> in the reference).</summary>
    NoMonsters,

    /// <summary>They joined for free, <c>heroes_action.cpp:392-397</c>.</summary>
    JoinedForFree,

    /// <summary>They joined for gold, <c>heroes_action.cpp:410-415</c>.</summary>
    JoinedForMoney,

    /// <summary>Too scared to fight; the hero chose not to pursue, <c>heroes_action.cpp:421-430</c>.</summary>
    Scattered,

    /// <summary>The hero beat them, <c>heroes_action.cpp:448-452</c>.</summary>
    Won,

    /// <summary>The hero lost and was dismissed, <c>heroes_action.cpp:453-481</c>.</summary>
    Lost,
}

/// <summary>
/// What happened when the hero walked onto a castle, <c>ActionToCastle()</c>
/// (<c>heroes_action.cpp:498-604</c>).
/// </summary>
public enum CastleEncounterOutcome
{
    /// <summary>No castle entrance on the tile; nothing happened (<c>assert( 0 )</c> in the reference).</summary>
    NoCastle,

    /// <summary>An allied castle the hero may not visit; nothing happened (<c>heroes_action.cpp:521-527</c>).</summary>
    Allied,

    /// <summary>His own castle: mage guild training and the castle screen, <c>heroes_action.cpp:510-519</c>.</summary>
    Visited,

    /// <summary>An enemy castle whose gate he already stood in: taken without a fight, <c>heroes_action.cpp:595-603</c>.</summary>
    Captured,

    /// <summary>He beat the garrison and took the town, <c>heroes_action.cpp:582-586</c>.</summary>
    SiegeWon,

    /// <summary>The garrison held, <c>heroes_action.cpp:587-592</c>.</summary>
    SiegeLost,
}

/// <summary>
/// What happened when the hero walked onto another hero, <c>ActionToHeroes()</c>
/// (<c>heroes_action.cpp:606-666</c>).
/// </summary>
public enum HeroMeetingOutcome
{
    /// <summary>Nobody on the tile; nothing happened (<c>assert( 0 )</c> in the reference).</summary>
    NoHero,

    /// <summary>An allied hero he may not meet; nothing happened (<c>heroes_action.cpp:625-631</c>).</summary>
    Allied,

    /// <summary>
    /// A hero of his own colour: the reference opens the exchange dialog, <c>heroes_action.cpp:617-623</c>.
    /// </summary>
    /// <remarks>
    /// 交换界面还没移植（<see cref="Hero"/> 没有 <c>MeetingDialog()</c>），所以游戏层只能报「见了面」，
    /// 界面层要么开交换窗，要么什么都不开。见 <c>PLAN.md</c> §5.4 的残留清单。
    /// </remarks>
    Met,

    /// <summary>He beat the other hero, <c>heroes_action.cpp:658-661</c>.</summary>
    Won,

    /// <summary>The other hero beat him, <c>heroes_action.cpp:662-665</c>.</summary>
    Lost,
}

public sealed partial class HeroActions
{
    /// <summary>
    /// The hero walks onto wandering monsters, <c>ActionToMonster()</c> (<c>heroes_action.cpp:351</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:351-496</c>。先问 <see cref="ArmyStack.GetJoinSolution"/>「他们想怎样」，再按
    /// 答案分四支：白送（<c>392</c>）、收钱（<c>410</c>）、害怕（<c>421</c>）、不加入（<c>433</c> 起开打）。
    /// 战役的 <c>Alliance</c>/<c>Bane</c> 两支（<c>362-385</c>）不走：战役没移植。
    /// </para>
    /// <para>
    /// <b>打之前要记一笔</b>：<see cref="Hero.SetAttackedMonsterTileIndex"/> 让地图上的怪物换攻击姿态
    /// （<c>437</c>），打完复位成 <c>-1</c>（<c>495</c>）。参考在两者之间还要重绘一次地图区，端口由调用方按
    /// <see cref="HeroActionResult.Changed"/> 重绘。
    /// </para>
    /// <para>
    /// <b>输了不一定没</b>：守军是「瓦片上那支军队」，<see cref="BattleLoader"/> 会把伤亡写回它；活下来的部分写回
    /// 瓦片（<c>471</c>），并且把「免费加入」改成「收钱」（<c>473-475</c>）——被主人打过一次的怪物不再白送。
    /// 打光才算清掉对象（<c>484-492</c>，参考同时播淡出动画与音效，端口只做模型那一半）。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the monsters stand on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the encounter ended.</returns>
    public MonsterEncounterOutcome ActionToMonster(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Tile tile = world.GetTile(tileIndex);
        Troop troop = tile.GetTroop(world);

        if (!troop.IsValid)
        {
            Debug.Assert(false, "A monster action was dispatched to a tile without monsters.");

            return MonsterEncounterOutcome.NoMonsters;
        }

        bool destroy = false;
        MonsterEncounterOutcome outcome = MonsterEncounterOutcome.NoMonsters;

        NeutralMonsterJoiningCondition join = ArmyStack.GetJoinSolution(_hero, tile, troop, world, Kingdom);

        switch (join.Reason)
        {
            case NeutralMonsterJoiningReason.Free:
                Debug.Assert(_hero.Army.CanJoinTroop(troop.Monster), "The solution says they join, the army says it cannot.");

                if (_host.Ask(HeroActionQuestion.JoinForFree, troop))
                {
                    _hero.Army.JoinTroop(troop.Monster, troop.Count, false);

                    destroy = true;
                    outcome = MonsterEncounterOutcome.JoinedForFree;
                }

                // 拒绝之后参考紧跟一条提示（399），然后照打——提示归宿主，流程在这里继续往下走。
                break;

            case NeutralMonsterJoiningReason.ForMoney:
            {
                uint joiningCost = (uint)troop.GetTotalCost().Gold;
                Troop joining = new(troop.Monster, (uint)join.MonsterCount);
                Funds price = new(Resource.Gold, joiningCost);

                Debug.Assert(
                    _hero.Army.CanJoinTroop(joining.Monster) && Kingdom.AllowPayment(price),
                    "The solution says they join for money, the kingdom says it cannot pay.");

                if (_host.Ask(HeroActionQuestion.JoinForMoney, joining, joiningCost))
                {
                    _hero.Army.JoinTroop(joining.Monster, joining.Count, false);
                    Kingdom.OddFundsResource(price);

                    destroy = true;
                    outcome = MonsterEncounterOutcome.JoinedForMoney;
                }

                // 同上：拒绝的提示与开打都由下面统一处理。
                break;
            }

            case NeutralMonsterJoiningReason.RunAway:
                if (!_host.Ask(HeroActionQuestion.PursueFleeingMonsters, troop))
                {
                    destroy = true;
                    outcome = MonsterEncounterOutcome.Scattered;
                }

                break;

            case NeutralMonsterJoiningReason.Alliance:
            case NeutralMonsterJoiningReason.Bane:
                // 战役奖励才有的两支，参考在 362-385 给了白送/逃跑的文案；战役没移植，落到这里就是数据坏了。
                Debug.Assert(false, $"No campaign award can make monsters join or flee yet ({join.Reason}).");
                break;

            case NeutralMonsterJoiningReason.None:
            default:
                break;
        }

        if (!destroy)
        {
            _hero.SetAttackedMonsterTileIndex(tileIndex);

            ArmyStack defenders = new();
            defenders.SetFromTile(world, tile);

            BattleResult result = Fight(world, _hero, defenders, tileIndex, castle: null, presenter: _host.BattlePresenter);

            if (result.IsAttackerWin)
            {
                _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

                destroy = true;
                outcome = MonsterEncounterOutcome.Won;
            }
            else
            {
                BattleLose(world, _hero, result, attacker: true);

                uint monstersLeft = defenders.GetCount();

                if (monstersLeft > 0)
                {
                    tile.SetMonsterCount(world, monstersLeft);

                    if (tile.IsMonsterJoinConditionFree(world))
                    {
                        tile.SetMonsterJoinCondition(world, MonsterJoinConditions.Money);
                    }

                    outcome = MonsterEncounterOutcome.Lost;
                }
                else
                {
                    destroy = true;
                    outcome = MonsterEncounterOutcome.Lost;
                }
            }
        }

        if (destroy)
        {
            // 参考在这里先播 KILLFADE 再跑淡出动画（484-490），动画结束时才把对象从瓦片上摘掉；端口没有动画层，
            // 于是直接做模型那一步：摘掉主对象的所有部件，再清元数据（等于 Maps::Tiles::Remove + resetObjectMetadata）。
            tile.RemoveObjects(tile.MainObjectPart.IcnType);
            tile.ResetObjectMetadata();
        }

        _hero.SetAttackedMonsterTileIndex(-1);

        return outcome;
    }

    /// <summary>
    /// The hero walks onto a castle, <c>ActionToCastle()</c> (<c>heroes_action.cpp:498</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:498-604</c>。三个出口：
    /// </para>
    /// <list type="number">
    /// <item>自己的城（<c>510</c>）：魔法行会教一次英雄，然后开城堡屏，<b>不进战斗</b>。</item>
    /// <item>盟友的城（<c>521</c>）：参考断言这不该发生，直接返回。</item>
    /// <item>敌人的城：站在门口就是攻城（<c>555-592</c>），已经站进去了就是直接占领（<c>595-603</c>）。</item>
    /// </list>
    /// <para>
    /// <b>攻城两件事的顺序不能换</b>：<see cref="Castle.ActionPreBattle"/> 在开打前铺临时驻军（守将的军队进城堡，
    /// 只在战斗期间有效），<see cref="Castle.ActionAfterBattle"/> 打完拆掉它（<c>562</c>、<c>566</c>）。
    /// </para>
    /// <para>
    /// <b>占领是四步</b>（<c>529-550</c>）：敌国注销、本国登记、<see cref="World.CaptureObject"/> 改所有权与颜色、
    /// 城堡重新侦察视野。参考另外刷图标面板与雷达，端口由调用方按结果重绘。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the castle entrance stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the visit ended.</returns>
    public CastleEncounterOutcome ActionToCastle(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        CastleTown? castle = world.GetCastleEntrance(tileIndex);

        if (castle is null)
        {
            Debug.Assert(false, "A castle action was dispatched to a tile without a castle entrance.");

            return CastleEncounterOutcome.NoCastle;
        }

        if (_hero.Color == castle.Color)
        {
            Debug.Assert(_hero.MapIndex == tileIndex, "A hero may only visit the castle he stands in.");

            castle.TrainHeroInMageGuild(_hero);
            _host.OpenCastle(castle);

            return CastleEncounterOutcome.Visited;
        }

        if (_hero.IsFriends(castle.Color))
        {
            Debug.Assert(false, "A hero cannot visit an allied castle.");

            return CastleEncounterOutcome.Allied;
        }

        PlayerKingdom castleKingdom = _kingdoms.GetKingdom(castle.Color);

        void Capture()
        {
            castleKingdom.RemoveCastle(castle);
            Kingdom.AddCastle(castle);

            world.CaptureObject(tileIndex, _hero.Color);

            castle.Scout(world);
        }

        Hero? defender = world.GetHero(castle);
        ArmyStack garrison = castle.GetActualArmy(defender);

        if (_hero.MapIndex != tileIndex)
        {
            Debug.Assert(garrison.IsValid, "The castle has no enemy garrison to fight.");

            castle.ActionPreBattle(defender, _hero.GetControl() == PlayerControl.Ai);

            BattleResult result = Fight(world, _hero, garrison, tileIndex, castle, presenter: _host.BattlePresenter);

            castle.ActionAfterBattle(result.IsAttackerWin);

            if (!result.IsDefenderWin && defender is not null)
            {
                BattleLose(world, defender, result, attacker: false);
            }

            if (!result.IsAttackerWin)
            {
                BattleLose(world, _hero, result, attacker: true);

                return CastleEncounterOutcome.SiegeLost;
            }

            Capture();

            _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

            return CastleEncounterOutcome.SiegeWon;
        }

        Debug.Assert(garrison.IsValid, "The hero already stands in an enemy castle, so it holds his army.");

        Capture();

        castle.TrainHeroInMageGuild(_hero);
        _host.OpenCastle(castle);

        return CastleEncounterOutcome.Captured;
    }

    /// <summary>
    /// The hero walks onto another hero, <c>ActionToHeroes()</c> (<c>heroes_action.cpp:606</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes_action.cpp:606-666</c>。<b>同色</b>不是战斗而是交换窗（<c>617-623</c>）：端口还没有
    /// <c>MeetingDialog()</c>，只报 <see cref="HeroMeetingOutcome.Met"/> 让界面自己开窗。
    /// </para>
    /// <para>
    /// <b>守在城里的敌人英雄</b>（<c>633-637</c>）不进这一支：参考转手交给
    /// <see cref="ActionToCastle"/>，攻城才是正解——他在城里，打得就是城。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">The tile the other hero stands on.</param>
    /// <param name="world">The world.</param>
    /// <returns>How the meeting ended.</returns>
    public HeroMeetingOutcome ActionToHeroes(int tileIndex, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        Hero? otherHero = world.GetTile(tileIndex).GetHero(world);

        if (otherHero is null)
        {
            Debug.Assert(false, "A hero action was dispatched to a tile without a hero.");

            return HeroMeetingOutcome.NoHero;
        }

        if (_hero.Color == otherHero.Color)
        {
            return HeroMeetingOutcome.Met;
        }

        if (_hero.IsFriends(otherHero.Color))
        {
            Debug.Assert(false, "A hero cannot meet an allied hero.");

            return HeroMeetingOutcome.Allied;
        }

        if (otherHero.InCastle(world) is not null)
        {
            return ActionToCastle(tileIndex, world) is CastleEncounterOutcome.SiegeLost
                ? HeroMeetingOutcome.Lost
                : HeroMeetingOutcome.Won;
        }

        BattleResult result = Fight(world, _hero, otherHero.Army, tileIndex, castle: null, presenter: _host.BattlePresenter);

        if (!result.IsDefenderWin)
        {
            BattleLose(world, otherHero, result, attacker: false);
        }

        if (!result.IsAttackerWin)
        {
            BattleLose(world, _hero, result, attacker: true);

            return HeroMeetingOutcome.Lost;
        }

        _hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

        return HeroMeetingOutcome.Won;
    }

    /// <summary>
    /// Runs one battle through <see cref="BattleLoader"/>, <c>Battle::Loader()</c>.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="attacker">The hero whose army attacks.</param>
    /// <param name="defenders">The defending army; the loader writes the casualties back into it.</param>
    /// <param name="tileIndex">The tile the battle happens on.</param>
    /// <param name="castle">The castle the defenders hold, or <see langword="null"/> for a field battle.</param>
    /// <param name="presenter">
    /// The battle screen, or <see langword="null"/> to let both sides be played by the AI (reference <c>Battle::Loader()</c>
    /// with no interface) — which is what <see cref="Ai.AiHeroAction"/> passes.
    /// </param>
    /// <returns>The result.</returns>
    internal static BattleResult Fight(
        World world,
        Hero attacker,
        ArmyStack defenders,
        int tileIndex,
        CastleTown? castle,
        IBattlePresenter? presenter = null)
    {
        BattleLoadRequest request = new()
        {
            World = world,
            AttackingArmy = attacker.Army,
            DefendingArmy = defenders,
            TileIndex = tileIndex,
            Castle = castle,
            Presenter = presenter,
        };

        return BattleLoader.Load(request);
    }

    /// <summary>
    /// Sends a beaten hero home, <c>BattleLose()</c> (<c>heroes_action.cpp:207-225</c>).
    /// </summary>
    /// <remarks>
    /// 参考在这里播死亡淡出音效、让英雄淡出、更新雷达上那一格；端口只留下模型那一件：
    /// <see cref="Hero.Dismiss"/>。淡出与音效归界面，雷达由调用方按结果整片重绘。
    /// </remarks>
    /// <param name="world">The world the hero leaves.</param>
    /// <param name="hero">The beaten hero.</param>
    /// <param name="result">The battle he lost.</param>
    /// <param name="attacker">Whether he was the attacker, which picks the half of the result to read.</param>
    internal static void BattleLose(World world, Hero hero, in BattleResult result, bool attacker)
    {
        BattleOutcome reason = attacker ? result.Attacker : result.Defender;

        Debug.Assert(reason != BattleOutcome.None, "A dismissed hero must have a reason.");

        hero.Dismiss(world, reason);
    }
}
