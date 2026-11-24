using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Core.Random;

using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Ai;

/// <summary>
/// What the AI does when a hero walks onto something, <c>AI::HeroesAction()</c> and <c>AI::HeroesMove()</c>
/// (<c>ai_hero_action.cpp:1977</c>, <c>2245</c>, <c>2404</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The AI half of the adventure layer.</b> <see cref="Heroes.HeroActions"/> is the same story told for the human
/// player: it asks through <see cref="IHeroActionHost"/> and shows messages. The AI never asks - it decides - so the two
/// cannot share a dispatcher, but they share every model change. Most branches here therefore call into
/// <see cref="Heroes.HeroActions"/> with <see cref="NullHeroActionHost"/> (that host answers "no" to every question and
/// reports nothing), and only the branches where the AI's decision differs from "the player said no" - fights, joins,
/// recruitment, payments, skill picks - are written out.
/// </para>
/// <para>
/// <b>No interface at all.</b> The reference AI versions still animate the hero and center the view
/// (<c>AIIsShowAnimationOfHmmCast()</c> &amp; co., <c>ai_hero_action.cpp:40-64</c>); the port drops all of it, the same
/// way <see cref="Heroes.HeroActions"/> drops it. What is left is the model.
/// </para>
/// <para>
/// <b>Order matters.</b> Every action runs inside an <see cref="AiHeroMeetingUpdater"/>, sets the hero's
/// <see cref="HeroModes.Action"/> flag, and - when the tile asked for an action - finishes with
/// <see cref="AiPlanner.HeroesActionComplete"/>, which is where the AI's own bookkeeping (visited arrays, mage guild
/// training, castle reinforcement) happens. See <c>ai_hero_action.cpp:2210-2243</c>.
/// </para>
/// </remarks>
public static partial class AiHeroAction
{
    /// <summary>
    /// Does whatever the tile under the hero asks for, <c>AI::HeroesAction()</c> (<c>ai_hero_action.cpp:1977-2243</c>).
    /// </summary>
    /// <param name="hero">The AI hero that has just walked onto <paramref name="dstIndex"/>.</param>
    /// <param name="dstIndex">The tile he stands on now.</param>
    public static void HeroesAction(Hero hero, int dstIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;

        using AiHeroMeetingUpdater meetingUpdater = new(hero);

        Tile tile = world.GetTile(dstIndex);

        // 参考拿的是带动作位的类型，switch 里比的是那套常量；端口这套 switch 全用不带位的标签，所以折一次
        // （ADR-133）。折在这里而不是每个 case 里，是因为下面的分派函数也都按不带位的那套写。
        // 但「能不能交互」这件事本身是靠动作位表达的：地图上城堡的塔、城背后的山体都存着不带位的 35，
        // 英雄走上去（这些格子有些是能走的）时参考那边 switch 匹配不到 OBJ_CASTLE，什么也不做。折完之后
        // 两者就分不出来了，所以先按原值筛一遍。
        Mp2ObjectType storedType = tile.GetMainObjectType(world, dstIndex != hero.MapIndex);

        if (!Mp2ObjectTypes.IsActionObject(storedType))
        {
            return;
        }

        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(storedType);

        // 船长靠岸时脚下那格可能空无一物，可这一步本身就是动作（heroes_action.cpp:787-791 的同一个判定）。
        // 传给 IsInGameActionObject 的必须是**带动作位**的原值：它自己就会先看这道位，折过的类型一律答 false，
        // 于是动作后的收尾（HeroesActionComplete）永远不跑，AI 的物件缓存就停在「这件东西还在」上。
        bool isDisembarking = hero.IsShipMaster && tile.IsSuitableForDisembarkation();
        bool isActing = isDisembarking || Mp2ObjectTypes.IsInGameActionObject(storedType, hero.IsShipMaster);

        if (isActing)
        {
            hero.SetModes(HeroModes.Action);
        }

        HeroActions actions = new(hero, world.Kingdoms);

        switch (objectType)
        {
            case Mp2ObjectType.Boat:
                AiToBoat(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Monster:
                AiToMonster(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Hero:
                AiToHeroes(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Castle:
                AiToCastle(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Barrel:
            case Mp2ObjectType.Bottle:
            case Mp2ObjectType.Campfire:
            case Mp2ObjectType.Resource:
                AiToPickupResource(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.SeaChest:
            case Mp2ObjectType.TreasureChest:
                AiToTreasureChest(hero, objectType, dstIndex, world);
                break;

            case Mp2ObjectType.Artifact:
                AiToArtifact(hero, dstIndex, world);
                break;

            case Mp2ObjectType.LeanTo:
            case Mp2ObjectType.MagicGarden:
            case Mp2ObjectType.Windmill:
            case Mp2ObjectType.WaterWheel:
                actions.ActionToObjectResource(objectType, dstIndex, world);
                break;

            case Mp2ObjectType.Wagon:
                AiToWagon(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Skeleton:
                AiToSkeleton(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Flotsam:
                AiToFlotsam(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.AlchemistLab:
            case Mp2ObjectType.Mine:
            case Mp2ObjectType.Sawmill:
            case Mp2ObjectType.Lighthouse:
                AiToCaptureObject(hero, objectType, dstIndex, world, actions);
                break;

            case Mp2ObjectType.AbandonedMine:
                AiToAbandonedMine(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.ShipwreckSurvivor:
                AiToShipwreckSurvivor(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Event:
                AiToEvent(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Sign:
                AiToSign(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.ObservationTower:
                AiToObservationTower(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.MagellansMaps:
                AiToMagellanMaps(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.StoneLiths:
                actions.ActionToTeleports(dstIndex, world);
                break;

            case Mp2ObjectType.Whirlpool:
                AiToWhirlpools(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Fort:
            case Mp2ObjectType.MercenaryCamp:
            case Mp2ObjectType.WitchDoctorsHut:
            case Mp2ObjectType.StandingStones:
            case Mp2ObjectType.Arena:
                AiToPrimarySkillObject(hero, objectType, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Gazebo:
                actions.ActionToExperienceObject(dstIndex, world);
                break;

            case Mp2ObjectType.WitchsHut:
                AiToWitchsHut(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.ShrineFirstCircle:
            case Mp2ObjectType.ShrineSecondCircle:
            case Mp2ObjectType.ShrineThirdCircle:
                AiToShrine(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Fountain:
            case Mp2ObjectType.FaerieRing:
            case Mp2ObjectType.Idol:
            case Mp2ObjectType.Mermaid:
                actions.ActionToGoodLuckObject(objectType, dstIndex);
                break;

            case Mp2ObjectType.Oasis:
            case Mp2ObjectType.Temple:
            case Mp2ObjectType.WateringHole:
            case Mp2ObjectType.Buoy:
                actions.ActionToGoodMoraleObject(objectType, dstIndex);
                break;

            case Mp2ObjectType.Obelisk:
                AiToObelisk(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.ArtesianSpring:
                actions.ActionToArtesianSpring(dstIndex);
                break;

            case Mp2ObjectType.MagicWell:
                AiToMagicWell(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Xanadu:
                actions.ActionToXanadu(dstIndex);
                break;

            case Mp2ObjectType.HillFort:
            case Mp2ObjectType.FreemansFoundry:
                AiToUpgradeArmyObject(hero, objectType);
                break;

            case Mp2ObjectType.Shipwreck:
            case Mp2ObjectType.Graveyard:
            case Mp2ObjectType.DerelictShip:
                AiToPoorMoraleObject(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Pyramid:
                AiToPyramid(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.DaemonCave:
                AiToDaemonCave(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.TreeOfKnowledge:
                AiToTreeKnowledge(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.WatchTower:
            case Mp2ObjectType.Excavation:
            case Mp2ObjectType.Cave:
            case Mp2ObjectType.TreeHouse:
            case Mp2ObjectType.ArcherHouse:
            case Mp2ObjectType.GoblinHut:
            case Mp2ObjectType.DwarfCottage:
            case Mp2ObjectType.HalflingHole:
            case Mp2ObjectType.PeasantHut:
                AiToDwellingJoinMonster(hero, objectType, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Ruins:
            case Mp2ObjectType.TreeCity:
            case Mp2ObjectType.WagonCamp:
            case Mp2ObjectType.DesertTent:
            case Mp2ObjectType.EarthAltar:
            case Mp2ObjectType.AirAltar:
            case Mp2ObjectType.FireAltar:
            case Mp2ObjectType.WaterAltar:
            case Mp2ObjectType.BarrowMounds:
                AiToDwellingRecruitMonster(hero, objectType, dstIndex, world, actions);
                break;

            case Mp2ObjectType.DragonCity:
            case Mp2ObjectType.CityOfDead:
            case Mp2ObjectType.TrollBridge:
                AiToDwellingBattleMonster(hero, objectType, dstIndex, world, actions);
                break;

            case Mp2ObjectType.GenieLamp:
                AiToGenieLamp(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Stables:
                actions.ActionToStables(dstIndex);
                break;

            case Mp2ObjectType.Barrier:
                AiToBarrier(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.TravellerTent:
                actions.ActionToTravellersTent(dstIndex, world);
                break;

            case Mp2ObjectType.Jail:
                AiToJail(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.HutOfMagi:
                actions.ActionToHutMagi(dstIndex, world);
                break;

            case Mp2ObjectType.AlchemistTower:
                AiToAlchemistTower(hero, dstIndex, world, actions);
                break;

            case Mp2ObjectType.Sirens:
                actions.ActionToSirens(dstIndex, world);
                break;

            case Mp2ObjectType.BlackCat:
                actions.ActionToBlackCat(dstIndex);
                break;

            // 参考里这两支什么也不做：AI 不读石碑留言、不看神谕、不答谜语（ai_hero_action.cpp:2130-2136）。
            case Mp2ObjectType.TradingPost:
            case Mp2ObjectType.Oracle:
            case Mp2ObjectType.EyeOfMagi:
            case Mp2ObjectType.Sphinx:
                break;

            default:
                if (isDisembarking)
                {
                    AiToCoast(hero, dstIndex, world, actions);
                }

                break;
        }

        if (isActing)
        {
            hero.Path.Reset();

            AiPlanner.Instance.HeroesActionComplete(hero, dstIndex, objectType);
        }
    }

    /// <summary>
    /// Walks the hero along his route, one action at a time, <c>AI::HeroesMove()</c> (<c>ai_hero_action.cpp:2245-2402</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在这里每走一步都检查「游戏是否结束」，端口还没有结局判定（P5），所以循环只剩「一直走到走不动」。
    /// </para>
    /// <para>
    /// 路线可能是「瞬移跳」（城门户或次元门把目标写成一步跳），那种情况不走路，直接施法。
    /// </para>
    /// </remarks>
    /// <param name="hero">The AI hero whose turn it is.</param>
    /// <returns><see cref="AiTurnOutcome.EndTurn"/> when the hero is done.</returns>
    public static AiTurnOutcome HeroesMove(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;
        HeroPath path = hero.Path;

        if (path.IsValidForTeleportation(world.Size))
        {
            TownPortal(hero, path.FrontIndex);
            return AiTurnOutcome.EndTurn;
        }

        if (!path.IsValidForMovement(world.Size))
        {
            hero.SetMove(false);
            return AiTurnOutcome.EndTurn;
        }

        hero.SetMove(true);

        while (hero.IsActive && hero.IsMoveEnabled)
        {
            if (!hero.Move(true, world))
            {
                break;
            }

            // 参考在这里调 ResetAction() 并查结局（ai_hero_action.cpp:2364-2370）；Hero.Move() 自己会在下一步开头
            // 清掉这个标志，所以端口只需继续走。
        }

        hero.SetMove(false);

        return AiTurnOutcome.EndTurn;
    }

    /// <summary>
    /// Casts dimension door onto <paramref name="targetIndex"/>, <c>AI::HeroesCastDimensionDoor()</c>
    /// (<c>ai_hero_action.cpp:2423-2459</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 落在哪就得把那一格的账结了：参考在最后调 <c>hero.ActionNewPosition( false )</c>
    /// （<c>ai_hero_action.cpp:2456</c>），<see langword="false"/> 表示这次落地不引来守卫的怪 ——
    /// 瞬移过来的英雄没有「走进」谁的领地。
    /// </para>
    /// <para>
    /// 参考在此处 <c>setDimensionDoorUsage( getDimensionDoorUses() + 1 )</c>（<c>:2445</c>）：
    /// 一天的次元门次数是 AI 寻路的射程预算，人类施法不记（见 <see cref="Hero.DimensionDoorUses"/>）。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero to move.</param>
    /// <param name="targetIndex">The destination tile.</param>
    /// <returns><see langword="true"/> when the hero moved.</returns>
    public static bool HeroesCastDimensionDoor(Hero hero, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;
        Spell spell = new(SpellType.DimensionDoor);

        if (!hero.CanCastSpell(spell))
        {
            return false;
        }

        hero.Scout(world, targetIndex);
        hero.MoveToDest(targetIndex, world);
        hero.SpellCasted(spell);

        // 每跳一次记一笔：AI 的次元门射程按这个计数递减（ai_hero_action.cpp:2445，
        // 读数在 AIWorldPathfinder 里）。
        ++hero.DimensionDoorUses;

        hero.Path.Reset();
        hero.MovementHandler.ActionNewPosition(hero, false);

        return true;
    }

    /// <summary>
    /// Casts summon boat, <c>AI::HeroesCastSummonBoat()</c> (<c>ai_hero_action.cpp:2461-2504</c>).
    /// </summary>
    /// <remarks>
    /// 参考返回的是<b>船原来的格子</b>而不是成功与否：AI 要拿它去清路径缓存里那条「这里有条船」的记录
    /// （<c>ai_hero_action.cpp:2221-2225</c>）。参考在这里只调一次 <c>SpellCasted()</c>，
    /// 端口交给 <see cref="Hero.ActionSpellCast"/> 统一收费。
    /// </remarks>
    /// <param name="hero">The hero to move.</param>
    /// <param name="targetIndex">The tile the boat should appear on.</param>
    /// <returns>The tile the boat came from, -1 when the cast did nothing.</returns>
    public static int HeroesCastSummonBoat(Hero hero, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;
        Spell spell = new(SpellType.SummonBoat);

        if (!hero.CanCastSpell(spell))
        {
            return -1;
        }

        int boatSource = SpellInfo.GetSummonableBoat(
            world,
            hero,
            Hero.GetCurrentMapVersion(),
            Players.Get(hero.Color));

        Debug.Assert(boatSource >= 0, "CanCastSpell() must have found a summonable boat.");

        if (boatSource < 0)
        {
            return -1;
        }

        hero.Scout(world, targetIndex);

        if (!hero.ActionSpellCast(spell, world))
        {
            return -1;
        }

        hero.Path.Reset();

        return boatSource;
    }

    /// <summary>
    /// Picks the primary skill for the Arena, <c>AI::AISelectSkillFromArena()</c> (<c>ai_hero_action.cpp:159-229</c>).
    /// </summary>
    /// <remarks>
    /// 参考的规则很朴素：先把本族该补的那一项补到阈值（骑士先补到防御 5、攻击 5、力量 3；野蛮人攻击优先；
    /// 法师先补力量 5；术士/亡灵先补力量 5），三项都够格了就随机挑攻击/防御/力量之一——注意它<b>从不挑知识</b>。
    /// </remarks>
    /// <param name="hero">The visitor.</param>
    /// <returns>The skill to raise.</returns>
    public static PrimarySkill SelectSkillFromArena(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        switch (hero.Race)
        {
            case MapRace.Knight:
                if (hero.GetDefense() < 5)
                {
                    return PrimarySkill.Defense;
                }

                if (hero.GetAttack() < 5)
                {
                    return PrimarySkill.Attack;
                }

                if (hero.GetPower() < 3)
                {
                    return PrimarySkill.Power;
                }

                break;

            case MapRace.Barbarian:
                if (hero.GetAttack() < 5)
                {
                    return PrimarySkill.Attack;
                }

                if (hero.GetDefense() < 5)
                {
                    return PrimarySkill.Defense;
                }

                if (hero.GetPower() < 3)
                {
                    return PrimarySkill.Power;
                }

                break;

            case MapRace.Sorceress:
            case MapRace.Wizard:
                if (hero.GetPower() < 5)
                {
                    return PrimarySkill.Power;
                }

                if (hero.GetDefense() < 3)
                {
                    return PrimarySkill.Defense;
                }

                if (hero.GetAttack() < 3)
                {
                    return PrimarySkill.Attack;
                }

                break;

            case MapRace.Warlock:
            case MapRace.Necromancer:
                if (hero.GetPower() < 5)
                {
                    return PrimarySkill.Power;
                }

                if (hero.GetAttack() < 3)
                {
                    return PrimarySkill.Attack;
                }

                if (hero.GetDefense() < 3)
                {
                    return PrimarySkill.Defense;
                }

                break;

            default:
                break;
        }

        return Rand.Get(1, 3) switch
        {
            1 => PrimarySkill.Attack,
            2 => PrimarySkill.Defense,
            3 => PrimarySkill.Power,
            _ => PrimarySkill.Unknown,
        };
    }

    /// <summary>
    /// Whether a wandering monster joins the hero, <c>AI::canMonsterJoinHero()</c> (<c>ai_hero_action.cpp:231-248</c>).
    /// </summary>
    /// <param name="troop">The monsters on the tile.</param>
    /// <param name="hero">The hero standing next to them.</param>
    /// <returns><see langword="true"/> when they would join.</returns>
    public static bool CanMonsterJoinHero(Troop troop, Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        // 队伍里已经有同种兵，那就一定收——反正也会合并到同一格。
        if (hero.Army.HasMonster(troop.Monster))
        {
            return true;
        }

        // 太弱的兵不值得招（还要占一格）。
        if (troop.GetStrength() < hero.GetAiMinimumJoiningArmyStrength())
        {
            return false;
        }

        // 队满了且挪不出空位，那也收不了。
        return hero.Army.MergeWeakestTroopsIfNeeded();
    }

    /// <summary>
    /// Casts a guarding spell on the mine the hero just took, <c>AI::castGuardianSpellOnCapturedObject()</c>
    /// (<c>ai_hero_action.cpp:250-299</c>).
    /// </summary>
    /// <remarks>
    /// 四种守卫法术在参考里被视为等价，先洗牌再挑第一个负担得起的——所以同一局里不同矿坑的守卫不一样。
    /// 值不值得花这笔法力看英雄的角色：斥候反正活不长，可以大方花；战士要留着法力打仗，就按难度倍率卡住。
    /// </remarks>
    /// <param name="hero">The new owner.</param>
    public static void CastGuardianSpellOnCapturedObject(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (!hero.HasSpellBook)
        {
            return;
        }

        // 守卫法术只能加在矿坑上。
        if (hero.ObjectTypeUnderHero != Mp2ObjectType.Mine)
        {
            return;
        }

        List<Spell> spells =
        [
            new(SpellType.SetAGuardian),
            new(SpellType.SetEGuardian),
            new(SpellType.SetFGuardian),
            new(SpellType.SetWGuardian),
        ];

        Rand.Shuffle(spells);

        double spellMultiplier = Difficulty.GetGuardianSpellMultiplier(Settings.Get().GameDifficulty);

        switch (hero.AiRole)
        {
            case HeroRole.Scout:
                spellMultiplier = Math.Min(1.0, spellMultiplier / 5);
                break;

            case HeroRole.Courier:
                spellMultiplier = Math.Min(1.0, spellMultiplier / 3);
                break;

            case HeroRole.Hunter:
                spellMultiplier = Math.Min(1.0, spellMultiplier / 2);
                break;

            default:
                break;
        }

        World world = World.Current;

        foreach (Spell spell in spells)
        {
            if (hero.CanCastSpell(spell) && hero.SpellPoints > spellMultiplier * spell.SpellPoints(hero.BagArtifacts))
            {
                if (hero.ActionSpellCast(spell, world))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Sends the hero through the town gate, <c>AI::AITownPortal()</c> (<c>ai_hero_action.cpp:301-345</c>).
    /// </summary>
    /// <param name="hero">The caster.</param>
    /// <param name="targetIndex">The castle gate tile.</param>
    private static void TownPortal(Hero hero, int targetIndex)
    {
        World world = World.Current;
        CastleTown? castle = world.GetCastleEntrance(targetIndex);

        Debug.Assert(castle is not null);
        if (castle is null || !hero.IsFriends(castle.Color))
        {
            return;
        }

        SpellType spellType = hero.CanCastSpell(new Spell(SpellType.TownGate))
            && hero.FindNearestCastleTownGate(world)?.TileIndex == castle.TileIndex
            ? SpellType.TownGate
            : SpellType.TownPortal;

        Spell spell = new(spellType);

        hero.Scout(world, targetIndex);
        hero.MoveToDest(targetIndex, world);
        hero.SpellCasted(spell);

        hero.Path.Reset();

        AiPlanner.Instance.HeroesActionComplete(hero, targetIndex, hero.ObjectTypeUnderHero);
    }

    /// <summary>
    /// Exchanges armies and artifacts between two AI heroes of the same kingdom, <c>AI::AIMeeting()</c>
    /// (<c>ai_hero_action.cpp:346-403</c>).
    /// </summary>
    /// <param name="left">The hero who walked onto the other.</param>
    /// <param name="right">The hero standing still.</param>
    private static void Meeting(Hero left, Hero right)
    {
        // 只让「角色高的」从「角色低的」手里拿兵和神器；角色相同时看数值，强的那个才是接收方。
        bool rightToLeft = true;

        if (left.AiRole < right.AiRole)
        {
            rightToLeft = false;
        }
        else if (left.AiRole == right.AiRole)
        {
            rightToLeft = right.GetStatsValue() < left.GetStatsValue();
        }

        Hero giver = rightToLeft ? right : left;
        Hero taker = rightToLeft ? left : right;

        taker.Army.JoinStrongestFromArmy(giver.Army);

        AiCommon.OptimizeTroopsOrder(taker.Army);
        AiCommon.OptimizeTroopsOrder(giver.Army);

        ArtifactExchange.Exchange(taker, giver);

        if (Difficulty.IsArtifactSortingAllowedForAi(Settings.Get().GameDifficulty))
        {
            left.BagArtifacts.SortFromWorstToBest();
            right.BagArtifacts.SortFromWorstToBest();
        }
    }

    /// <summary>
    /// Lets the winner loot the loser, <c>AI::AIBattleLose()</c> (<c>ai_hero_action.cpp:346-360</c>).
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="hero">The hero who lost.</param>
    /// <param name="result">The battle result.</param>
    /// <param name="attacker">Whether <paramref name="hero"/> attacked.</param>
    private static void BattleLose(World world, Hero hero, in BattleResult result, bool attacker) =>
        HeroActions.BattleLose(world, hero, result, attacker);
}
