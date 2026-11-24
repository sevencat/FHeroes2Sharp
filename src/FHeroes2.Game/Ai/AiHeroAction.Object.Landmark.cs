using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

using GameArmy = FHeroes2.Game.Army.Army;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The <c>AITo*()</c> branches that change the world without asking: mines, chests, events and dwellings.
/// </summary>
public static partial class AiHeroAction
{
    /// <summary>
    /// The hero captured a mine, sawmill, lighthouse or alchemist lab, <c>AIToCaptureObject()</c>
    /// (<c>ai_hero_action.cpp:827-883</c>).
    /// </summary>
    /// <remarks>
    /// 玩家侧那条流程与参考的 AI 版逐行一致，缺的只有「赢了之后自动施守卫法术」一步——参考把它放在 AI 侧
    ///（<c>883</c> 的 <c>castGuardianSpellOnCapturedObject()</c>），所以这里补上。
    /// </remarks>
    private static void AiToCaptureObject(Hero hero, Mp2ObjectType objectType, int tileIndex, World world, HeroActions actions)
    {
        CaptureObjectOutcome outcome = actions.ActionToCaptureObject(objectType, tileIndex, world);

        if (outcome is CaptureObjectOutcome.Won or CaptureObjectOutcome.Captured)
        {
            CastGuardianSpellOnCapturedObject(hero);
        }
    }

    /// <summary>
    /// An abandoned mine, <c>AIToAbandonedMine()</c> (<c>ai_hero_action.cpp:1680-1710</c>).
    /// </summary>
    /// <remarks>AI 只走「打下来」这一条：赢了把矿恢复成金矿并占领，输了撤。</remarks>
    private static void AiToAbandonedMine(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        if (!AiPlanner.Instance.IsValidHeroObject(hero, tileIndex, hero.MapIndex == tileIndex))
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);

        GameArmy defenders = new();
        defenders.SetFromTile(world, tile);

        BattleResult result = HeroActions.Fight(world, hero, defenders, tileIndex, castle: null);

        if (!result.IsAttackerWin)
        {
            HeroActions.BattleLose(world, hero, result, attacker: true);

            return;
        }

        hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

        tile.RestoreAbandonedMine(world, Resource.Gold);

        hero.SetObjectTypeUnderHero(Mp2ObjectType.Mine);
        world.CaptureObject(tileIndex, hero.Color);

        CastGuardianSpellOnCapturedObject(hero);
    }

    /// <summary>
    /// A map event, <c>AIToEvent()</c> (<c>ai_hero_action.cpp:1280-1332</c>).
    /// </summary>
    /// <remarks>
    /// 与玩家侧的唯一差别：AI 走完事件<b>不会停下</b>（参考这里没有 <c>setMove( false )</c>），
    /// 且「电脑玩家无权触发」的事件直接跳过。
    /// </remarks>
    private static void AiToEvent(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        if (world.GetMapEvent(tileIndex) is not MapEvent mapEvent)
        {
            hero.SetObjectTypeUnderHero(Mp2ObjectType.None);

            return;
        }

        if (!mapEvent.IsComputerPlayerAllowed || !mapEvent.IsAllow(hero.Color))
        {
            return;
        }

        GameKingdom kingdom = world.GetKingdom(hero.Color);

        if (mapEvent.Resources.GetValidItemsCount() > 0)
        {
            kingdom.AddFundsResource(mapEvent.Resources);
        }

        if (mapEvent.Artifact != ArtifactType.Unknown)
        {
            hero.PickupArtifact(mapEvent.Artifact, world);
        }

        SecondarySkillLevel taught = mapEvent.SecondarySkill;

        if (taught.IsValid)
        {
            bool learns = hero.HasSecondarySkill(taught.Skill)
                ? (int)hero.SecondarySkills.GetLevel(taught.Skill) < (int)taught.Level
                : !hero.HasMaxSecondarySkill();

            if (learns)
            {
                hero.LearnSkill(taught);

                if (taught.Skill == SecondarySkill.Scouting)
                {
                    hero.Scout(world, tileIndex);
                }
            }
        }

        if (mapEvent.Experience > 0)
        {
            hero.IncreaseExperience((uint)mapEvent.Experience, world.MapSeed);
        }

        mapEvent.SetVisited();

        if (mapEvent.IsSingleTimeEvent)
        {
            hero.SetObjectTypeUnderHero(Mp2ObjectType.None);
            world.MapObjects.Remove(tileIndex);
        }
    }

    /// <summary>
    /// Magellan's maps, <c>AIToMagellanMaps()</c> (<c>ai_hero_action.cpp:970-990</c>).
    /// </summary>
    /// <remarks>
    /// AI 不弹「买不买」：没买过且付得起就直接买。「买过没有」是<b>王国级</b>的判断
    /// （参考的 <c>isObjectTypeVisited( MAGELLAN_MAPS, Visit::GLOBAL )</c>），所以问
    /// <see cref="GameKingdom.IsVisited(Mp2ObjectType)"/> 而不是英雄自己的名册。
    /// </remarks>
    private static void AiToMagellanMaps(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        GameKingdom kingdom = world.GetKingdom(hero.Color);

        if (kingdom.IsVisited(Mp2ObjectType.MagellansMaps))
        {
            return;
        }

        if (kingdom.AllowPayment(PaymentConditions.GetMagellansMapsPurchasePrice()))
        {
            actions.ActionToMagellanMaps(tileIndex, world, confirmed: true);
        }
    }

    /// <summary>
    /// A whirlpool, <c>AIToWhirlpools()</c> (<c>ai_hero_action.cpp:1062-1097</c>).
    /// </summary>
    /// <remarks>
    /// AI 版比玩家版多一步 <see cref="GameArmy.ArrangeForWhirlpool"/>：参考的
    /// <c>AIWhirlpoolTroopLoseEffect()</c>（<c>ai_hero_action.cpp:1035-1061</c>）先把军队按强弱重排，
    /// 再沉掉最弱的一队——玩家侧不做这个整理。
    /// </remarks>
    private static void AiToWhirlpools(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        hero.Army.ArrangeForWhirlpool();

        actions.ActionToWhirlpools(tileIndex, world);
    }

    /// <summary>
    /// Arena, fort, mercenary camp, witch doctors' hut or standing stones, <c>AIToPrimarySkillObject()</c>
    /// (<c>ai_hero_action.cpp:1098-1132</c>).
    /// </summary>
    /// <remarks>竞技场的技能由 <see cref="SelectSkillFromArena"/> 挑；其余四种对象的技能是固定的。</remarks>
    private static void AiToPrimarySkillObject(
        Hero hero,
        Mp2ObjectType objectType,
        int tileIndex,
        World world,
        HeroActions actions)
    {
        if (objectType == Mp2ObjectType.Arena)
        {
            if (!hero.IsObjectTypeVisited(Mp2ObjectType.Arena))
            {
                actions.ActionToArena(tileIndex, SelectSkillFromArena(hero));
            }

            return;
        }

        actions.ActionToPrimarySkillObject(objectType, tileIndex, world);
    }

    /// <summary>
    /// A magic well, <c>AIToMagicWell()</c> (<c>ai_hero_action.cpp:1238-1250</c>).
    /// </summary>
    /// <remarks>井水一次喝满，且无论喝没喝都要登记为已访问，免得英雄反复来回。</remarks>
    private static void AiToMagicWell(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToMagicWell(tileIndex);

        hero.SetVisited(tileIndex, Mp2ObjectType.MagicWell);
    }

    /// <summary>
    /// A wreck, graveyard or derelict ship, <c>AIToPoorMoraleObject()</c> (<c>ai_hero_action.cpp:1370-1431</c>).
    /// </summary>
    /// <remarks>
    /// AI 不弹「要不要搜」的确认框：计划器认为值得停就直接搜。玩家侧同一动作会先问宿主，
    /// 失败时返回「拒绝」而不改模型，所以不能复用。
    /// </remarks>
    private static void AiToPoorMoraleObject(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        Tile tile = world.GetTile(tileIndex);
        Mp2ObjectType objectType = tile.GetNonActionObjectType(world);

        if (!AiPlanner.Instance.IsValidHeroObject(hero, tileIndex, hero.MapIndex == tileIndex))
        {
            return;
        }

        GameKingdom kingdom = world.GetKingdom(hero.Color);

        uint gold = (uint)tile.GetFundsFromTile(world).Gold;
        bool complete = false;

        if (gold > 0)
        {
            GameArmy defenders = new();
            defenders.SetFromTile(world, tile);

            BattleResult result = HeroActions.Fight(world, hero, defenders, tileIndex, castle: null);

            if (!result.IsAttackerWin)
            {
                HeroActions.BattleLose(world, hero, result, attacker: true);

                return;
            }

            hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

            complete = true;

            ArtifactType artifact = Mp2ObjectTypes.IsArtifactObject(objectType)
                ? tile.GetArtifactFromTile(world)
                : ArtifactType.Unknown;

            if (artifact != ArtifactType.Unknown && !hero.PickupArtifact(artifact, world))
            {
                gold = ArtifactGold.InsteadOf(objectType);
            }

            kingdom.AddFundsResource(new Funds(Resource.Gold, gold));
        }

        if (complete)
        {
            tile.ResetObjectMetadata();
        }
        else if (gold == 0 && !hero.IsObjectTypeVisited(objectType))
        {
            hero.SetVisited(tileIndex, objectType);
        }

        kingdom.SetVisited(tileIndex, objectType);
    }

    /// <summary>
    /// A pyramid, <c>AIToPyramid()</c> (<c>ai_hero_action.cpp:1433-1476</c>).
    /// </summary>
    /// <remarks>
    /// 有法术就有守卫：打赢得经验，带魔法书且智慧专家时抄进书里；没法术说明已经被人拿过了。
    /// 最后无论哪一支都把金字塔记成王国级已访问（参考 1475）。
    /// </remarks>
    private static void AiToPyramid(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        if (!AiPlanner.Instance.IsValidHeroObject(hero, tileIndex, hero.MapIndex == tileIndex))
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);
        Spell spell = tile.GetSpellFromTile(world);

        if (spell.Type != SpellType.None)
        {
            GameArmy guardians = new();
            guardians.SetFromTile(world, tile);

            BattleResult result = HeroActions.Fight(world, hero, guardians, tileIndex, castle: null);

            if (result.IsAttackerWin)
            {
                hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

                if (hero.HasSpellBook && hero.GetLevelSkill(SecondarySkill.Wisdom) == SkillLevel.Expert)
                {
                    hero.MagicBook.Append(spell);
                }

                hero.SetVisited(tileIndex, Mp2ObjectType.Pyramid);
                world.GetKingdom(hero.Color).SetVisited(tileIndex, Mp2ObjectType.Pyramid);

                tile.ResetObjectMetadata();
            }
            else
            {
                HeroActions.BattleLose(world, hero, result, attacker: true);
            }
        }
        else
        {
            hero.SetVisited(tileIndex, Mp2ObjectType.Pyramid);
        }

        world.GetKingdom(hero.Color).SetVisited(tileIndex, Mp2ObjectType.Pyramid);
    }

    /// <summary>
    /// The tree of knowledge, <c>AIToTreeKnowledge()</c> (<c>ai_hero_action.cpp:1492-1512</c>).
    /// </summary>
    /// <remarks>
    /// 玩家侧的 <see cref="HeroActions.ActionToTreeKnowledge"/> 本来就是参考的 AI 版：不论付不付得起都先登记
    /// <b>王国</b>名册，付得起（免费树视为付得起）才记<b>本地</b>名册并加经验。规划器已经挡过「没钱的英雄别来」，
    /// 所以这里直接转调，让「记王国名册」这一步照参考走。
    /// </remarks>
    private static void AiToTreeKnowledge(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToTreeKnowledge(tileIndex, world);
    }

    /// <summary>
    /// The daemon cave, <c>AIToDaemonCave()</c> (<c>ai_hero_action.cpp:1513-1547</c>).
    /// </summary>
    /// <remarks>AI 没有「付赎金」这一支：洞里有东西就直接打，打完（无论胜负）把洞登记为王国级已访问。</remarks>
    private static void AiToDaemonCave(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        if (!AiPlanner.Instance.IsValidHeroObject(hero, tileIndex, hero.MapIndex == tileIndex))
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);

        if (tile.DoesTileContainValuableItems(world))
        {
            GameArmy defenders = new();
            defenders.SetFromTile(world, tile);

            BattleResult result = HeroActions.Fight(world, hero, defenders, tileIndex, castle: null);

            if (result.IsAttackerWin)
            {
                hero.IncreaseExperience(2500, world.MapSeed);

                world.GetKingdom(hero.Color).AddFundsResource(new Funds(Resource.Gold, 2500));
            }
            else
            {
                HeroActions.BattleLose(world, hero, result, attacker: true);
            }

            tile.ResetObjectMetadata();
        }

        world.GetKingdom(hero.Color).SetVisited(tileIndex, Mp2ObjectType.DaemonCave);
    }

    /// <summary>
    /// A dwelling whose monsters join for free, <c>AIToDwellingJoinMonster()</c>
    /// (<c>ai_hero_action.cpp:1549-1573</c>).
    /// </summary>
    private static void AiToDwellingJoinMonster(
        Hero hero,
        Mp2ObjectType objectType,
        int tileIndex,
        World world,
        HeroActions actions)
    {
        Tile tile = world.GetTile(tileIndex);
        Troop troop = tile.GetTroop(world);

        if (!troop.IsValid || !CanMonsterJoinHero(troop, hero))
        {
            return;
        }

        if (!hero.Army.JoinTroop(troop))
        {
            Debug.Assert(false, "An army with a free slot must accept joining monsters.");

            return;
        }

        tile.SetMonsterCount(world, 0);

        world.GetKingdom(hero.Color).SetVisited(tileIndex, objectType);
    }

    /// <summary>
    /// A dwelling where monsters are hired for money, <c>AIToDwellingRecruitMonster()</c>
    /// (<c>ai_hero_action.cpp:1574-1622</c>).
    /// </summary>
    /// <remarks>
    /// AI 按国库能买多少就买多少（<c>getLowestQuotient()</c>），再把剩下的怪写回瓦片。
    /// 精灵灯买空后连同物件一起消失（1615-1619），其它居所只记王国级访问。
    /// </remarks>
    private static void AiToDwellingRecruitMonster(
        Hero hero,
        Mp2ObjectType objectType,
        int tileIndex,
        World world,
        HeroActions actions,
        bool isGenieLamp = false)
    {
        Tile tile = world.GetTile(tileIndex);
        Troop troop = tile.GetTroop(world);

        if (!troop.IsValid)
        {
            return;
        }

        GameKingdom kingdom = world.GetKingdom(hero.Color);

        Funds singleMonsterCost = troop.GetCost();
        uint recruitCount = (uint)kingdom.GetFunds().GetLowestQuotient(singleMonsterCost);

        if (recruitCount == 0)
        {
            return;
        }

        uint availableCount = troop.Count;

        if (recruitCount > availableCount)
        {
            recruitCount = availableCount;
        }

        Troop toHire = new(troop.Monster, recruitCount);

        if (!CanMonsterJoinHero(toHire, hero))
        {
            return;
        }

        if (!hero.Army.JoinTroop(toHire))
        {
            Debug.Assert(false, "An army with a free slot must accept hired monsters.");

            return;
        }

        tile.SetMonsterCount(world, availableCount - recruitCount);

        kingdom.OddFundsResource(toHire.GetTotalCost());

        if (isGenieLamp && availableCount == recruitCount)
        {
            tile.RemoveMainObject(world);
            tile.ResetObjectMetadata();
        }
        else
        {
            kingdom.SetVisited(tileIndex, objectType);
        }
    }

    /// <summary>
    /// A dwelling guarded by monsters, <c>AIToDwellingBattleMonster()</c> (<c>ai_hero_action.cpp:1623-1665</c>).
    /// </summary>
    /// <remarks>
    /// 打之前先看计划器是否允许（守卫太强就别碰）。赢了把居所变成无主，然后照常招兵；
    /// 输了先撤，只有「允许招兵」时才继续（<c>1649-1662</c>）。
    /// </remarks>
    private static void AiToDwellingBattleMonster(
        Hero hero,
        Mp2ObjectType objectType,
        int tileIndex,
        World world,
        HeroActions actions)
    {
        if (!AiPlanner.Instance.IsValidHeroObject(hero, tileIndex, hero.MapIndex == tileIndex))
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);

        bool recruitmentAllowed = true;

        if (tile.GetColor(world) == PlayerColor.None)
        {
            GameArmy defenders = new();
            defenders.SetFromTile(world, tile);

            BattleResult result = HeroActions.Fight(world, hero, defenders, tileIndex, castle: null);

            if (result.IsAttackerWin)
            {
                hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

                // 居所变无主（参考的 setColorOnTile( tile, PlayerColor::UNUSED )）。
                world.CaptureObject(tileIndex, PlayerColor.Unused);
            }
            else
            {
                HeroActions.BattleLose(world, hero, result, attacker: true);

                recruitmentAllowed = false;
            }
        }

        if (recruitmentAllowed)
        {
            if (tile.GetTroop(world).IsValid)
            {
                AiToDwellingRecruitMonster(hero, objectType, tileIndex, world, actions);
            }

            world.GetKingdom(hero.Color).SetVisited(tileIndex, objectType);
        }
    }

    /// <summary>
    /// The genie lamp, dispatched as a dwelling (<c>ai_hero_action.cpp:2176</c> and 1574-1622).
    /// </summary>
    private static void AiToGenieLamp(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        AiToDwellingRecruitMonster(hero, Mp2ObjectType.GenieLamp, tileIndex, world, actions, isGenieLamp: true);
    }

    /// <summary>
    /// A jail, <c>AIToJail()</c> (<c>ai_hero_action.cpp:1875-1896</c>).
    /// </summary>
    /// <remarks>
    /// AI 先看英雄名额（<c>Kingdom::GetMaxHeroes()</c>），有空位才放人；放出后把军队整理成 AI 阵型。
    /// 参考会顺手把监狱从地图上抹掉（<c>1890</c> 的 <c>removeMainObjectFromTile()</c>），端口照做。
    /// </remarks>
    private static void AiToJail(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        GameKingdom kingdom = world.GetKingdom(hero.Color);

        if (kingdom.Heroes.Count >= GameKingdom.MaxHeroes)
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);
        tile.RemoveMainObject(world);
        tile.ResetObjectMetadata();

        Hero? prisoner = world.FromJailHeroes(tileIndex);

        if (prisoner is null)
        {
            return;
        }

        if (!prisoner.Recruit(kingdom, tileIndex, world))
        {
            Debug.Assert(false, "A jailed hero refused to join the kingdom.");

            return;
        }

        AiCommon.OptimizeTroopsOrder(prisoner.Army);
    }

    /// <summary>
    /// The Alchemist's Tower, <c>AIToAlchemistTower()</c> (<c>ai_hero_action.cpp:1912-1937</c>).
    /// </summary>
    /// <remarks>
    /// 与玩家侧的差别只在「要不要烧」：AI 付得起就一定烧。其余（固定价 750 金、逐槽清掉诅咒物品、
    /// 既不记访问也不清元数据）与 <see cref="HeroActions.ActionToAlchemistTower"/> 相同，但那里的确认
    /// 走宿主，<see cref="NullHeroActionHost"/> 一律答「否」，所以这里自己写。
    /// </remarks>
    private static void AiToAlchemistTower(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        Funds payment = PaymentConditions.ForAlchemist();

        if (!world.GetKingdom(hero.Color).AllowPayment(payment))
        {
            return;
        }

        world.GetKingdom(hero.Color).OddFundsResource(payment);

        foreach (ArtifactType artifact in hero.BagArtifacts.Slots)
        {
            if (ArtifactTable.Get(artifact).Curses.Count > 0)
            {
                hero.BagArtifacts.Remove(artifact);
            }
        }
    }
}
