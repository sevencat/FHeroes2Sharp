using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;

using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;
using TroopArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Campaign;

/// <summary>
/// The scenario bonuses and the campaign awards, <c>game_campaign.cpp:501-780</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把这一整段放在 <c>game_campaign.cpp</c> 的匿名命名空间里，它们都是「战役要开始这一关时，把玩家在选关界面上挑的
/// 奖励、和之前攒下的奖励物，落到人和王国身上」的收尾动作。端口单独放一个静态类：界面还没搬进来，但这段逻辑本身完全
/// 是数据动作，可以单独测。
/// </para>
/// <para>
/// 参考用的是全局 <c>world</c> 与 <c>Settings::Get().GetPlayers()</c>，端口把世界做成显式参数，玩家与设置仍旧读全局
/// 单例——和 <see cref="CampaignSaveData"/> 同一个口径。
/// </para>
/// </remarks>
public static class CampaignBonuses
{
    /// <summary>
    /// Applies the bonus the player picked for a scenario, <c>SetScenarioBonus()</c>
    /// (<c>game_campaign.cpp:605-702</c>).
    /// </summary>
    /// <param name="world">The world the scenario is about to start on.</param>
    /// <param name="scenarioInfoId">The scenario the bonus belongs to.</param>
    /// <param name="scenarioBonus">The bonus to apply.</param>
    /// <remarks>
    /// <para>
    /// 只给人控制的玩家加：电脑玩家的关卡本来也不走选奖励那条路（<c>SelectScenarioDialog</c> 只对人有意义），参考在这里
    /// 显式跳过 <c>!isControlHuman()</c>。
    /// </para>
    /// <para>
    /// <c>STARTING_RACE</c> 改的是玩家槽位上的种族而不是英雄：下一关重新读地图表头时，那条槽位就会按新种族摆城堡和兵，
    /// 所以它走 <see cref="Players.SetPlayerRace"/>。
    /// </para>
    /// <para>
    /// 宝物奖励在参考里会为卷轴补上法术（<c>Artifact::SetSpell()</c>）。端口的宝物栏只存编号
    /// （<see cref="ArtifactBag"/> 的槽位是 <see cref="ArtifactType"/>），没有地方放「这一份卷轴携带的法术」，
    /// 所以卷轴只发本体，法术这一层与 <see cref="ArtifactScroll"/> 的说明一致地留着——见 ADR-249。
    /// </para>
    /// </remarks>
    public static void SetScenarioBonus(World world, ScenarioInfoId scenarioInfoId, ScenarioBonusData scenarioBonus)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scenarioBonus);

        foreach (Player? player in Settings.Get().Players.All)
        {
            if (player is null || !player.IsControlHuman)
            {
                continue;
            }

            GameKingdom kingdom = world.GetKingdom(player.Color);

            switch (scenarioBonus.Type)
            {
                case ScenarioBonusData.BonusType.Resources:
                    kingdom.AddFundsResource(new Funds(scenarioBonus.SubType, (uint)scenarioBonus.Amount));
                    break;

                case ScenarioBonusData.BonusType.Artifact:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(scenarioInfoId, kingdom);

                    if (hero is not null)
                    {
                        hero.PickupArtifact((ArtifactType)scenarioBonus.SubType, world);
                    }

                    break;
                }

                case ScenarioBonusData.BonusType.Troop:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(scenarioInfoId, kingdom);

                    hero?.Army.JoinTroop((MonsterType)scenarioBonus.SubType, (uint)scenarioBonus.Amount);
                    break;
                }

                case ScenarioBonusData.BonusType.Spell:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(scenarioInfoId, kingdom);

                    hero?.AppendSpellToBook(new Spell((SpellType)scenarioBonus.SubType), ignoreWisdom: true);
                    break;
                }

                case ScenarioBonusData.BonusType.StartingRace:
                    Players.SetPlayerRace(player.Color, (MapRace)scenarioBonus.SubType);
                    break;

                case ScenarioBonusData.BonusType.StartingRaceAndArmy:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(scenarioInfoId, kingdom);

                    if (hero is not null)
                    {
                        SetHeroAndArmyBonus(world, hero, scenarioInfoId);
                    }

                    break;
                }

                case ScenarioBonusData.BonusType.SkillPrimary:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(scenarioInfoId, kingdom);

                    if (hero is not null)
                    {
                        for (int i = 0; i < scenarioBonus.Amount; ++i)
                        {
                            hero.IncreasePrimarySkill((PrimarySkill)scenarioBonus.SubType);
                        }
                    }

                    break;
                }

                case ScenarioBonusData.BonusType.SkillSecondary:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(scenarioInfoId, kingdom);

                    hero?.LearnSkill(new SecondarySkillLevel(
                        (SecondarySkill)scenarioBonus.SubType,
                        (SkillLevel)scenarioBonus.Amount));
                    break;
                }

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Sets the starting race the player picked, the half of <c>SetScenarioBonus()</c> that has to happen before the
    /// map is loaded (<c>game_campaign.cpp:1631-1635</c>).
    /// </summary>
    /// <param name="scenarioBonus">A <see cref="ScenarioBonusData.BonusType.StartingRace"/> reward.</param>
    /// <remarks>
    /// <para>
    /// 参考在这里只把玩家种族改掉，地图还没读，所以用不到世界。带部队的那一档（
    /// <see cref="ScenarioBonusData.BonusType.StartingRaceAndArmy"/>）也是「先只设种族」，部队要等地图读进来、
    /// 英雄就位之后才由 <see cref="SetScenarioBonus"/> 补（<c>:1633-1635</c>）。
    /// </para>
    /// <para>
    /// 只改人类玩家：参考遍历的就是 <c>Settings::Get().GetPlayers()</c> 里 <c>isControlHuman()</c> 的那几个。
    /// </para>
    /// </remarks>
    public static void SetStartingRaceBonus(ScenarioBonusData scenarioBonus)
    {
        ArgumentNullException.ThrowIfNull(scenarioBonus);

        foreach (Player? player in Settings.Get().Players.All)
        {
            if (player is null || !player.IsControlHuman)
            {
                continue;
            }

            Players.SetPlayerRace(player.Color, (MapRace)scenarioBonus.SubType);
        }
    }

    /// <summary>
    /// Applies the awards that are handed out when a scenario starts, <c>applyObtainedCampaignAwards()</c>
    /// (<c>game_campaign.cpp:703-780</c>).
    /// </summary>
    /// <param name="world">The world the scenario is about to start on.</param>
    /// <param name="currentScenarioInfoId">The scenario being started.</param>
    /// <param name="awards">The awards collected so far, <see cref="CampaignSaveData.GetObtainedCampaignAwards"/>.</param>
    /// <remarks>
    /// <para>
    /// 参考的注释写明这里只放「开局就要立刻兑现」的那几类：宝物、法术、消灭某个敌方英雄、携带部队。其余的
    /// （额外收入、可招募英雄、生物诅咒与联盟、资源奖励）在游戏进行中按条件生效，不在这条路上。
    /// </para>
    /// <para>
    /// 奖励物在它自己的 <see cref="CampaignAwardData.StartScenarioId"/> 之后才开始给：同一件奖励在拿到的当下那一关
    /// 不生效，从下一关起才生效，所以这里按关卡编号过滤，而不是「拿到就给」。
    /// </para>
    /// </remarks>
    public static void ApplyObtainedCampaignAwards(
        World world,
        ScenarioInfoId currentScenarioInfoId,
        IReadOnlyList<CampaignAwardData> awards)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(awards);

        PlayerColor humanColor = Players.HumanColors();
        GameKingdom humanKingdom = world.GetKingdom(humanColor);

        foreach (CampaignAwardData award in awards)
        {
            if (currentScenarioInfoId.ScenarioId < award.StartScenarioId)
            {
                continue;
            }

            switch (award.Type)
            {
                case CampaignAwardData.AwardType.GetArtifact:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(currentScenarioInfoId, humanKingdom);

                    if (hero is not null)
                    {
                        hero.PickupArtifact((ArtifactType)award.SubType, world);

                        // Some artifacts raise spell power, and the extra points only show up when the pool is
                        // refilled; the reference tops it up right here.
                        hero.SetSpellPoints(Math.Max(hero.SpellPoints, hero.GetMaxSpellPoints()));
                    }

                    break;
                }

                case CampaignAwardData.AwardType.GetSpell:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(currentScenarioInfoId, humanKingdom);

                    hero?.AppendSpellToBook(new Spell((SpellType)award.SubType), ignoreWisdom: true);
                    break;
                }

                case CampaignAwardData.AwardType.DefeatEnemyHero:
                {
                    // 奖励物在任意一方的英雄身上都可能兑现（比如「上一关打倒的那个敌方英雄本关不再出场」）。
                    foreach (Player? player in Settings.Get().Players.All)
                    {
                        if (player is null)
                        {
                            continue;
                        }

                        foreach (Hero hero in world.GetKingdom(player.Color).Heroes)
                        {
                            if (hero.Id == award.SubType)
                            {
                                hero.Dismiss(world, BattleOutcome.Loss);
                                break;
                            }
                        }
                    }

                    break;
                }

                case CampaignAwardData.AwardType.CarryOverForces:
                {
                    Hero? hero = GetHeroToApplyBonusOrAwards(currentScenarioInfoId, humanKingdom);

                    if (hero is not null)
                    {
                        IReadOnlyList<Troop> carryOver = CampaignSaveData.Get().CarryOverTroops;

                        if (carryOver.Any(troop => troop.IsValid))
                        {
                            ReplaceArmy(hero.Army, carryOver);
                        }
                        else
                        {
                            hero.Army.Reset();
                        }
                    }

                    break;
                }

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Swaps a hero's whole army for a given one, <c>replaceArmy()</c> (<c>game_campaign.cpp:501-515</c>).
    /// </summary>
    /// <param name="army">The army to overwrite.</param>
    /// <param name="troops">The troops to put in, slot by slot; extra slots are emptied.</param>
    /// <remarks>
    /// 参考先 <c>Clean()</c> 再逐格 <c>Set()</c>，格子数取两边的较小值：多出来的新兵不要，多出来的旧格清空。
    /// </remarks>
    public static void ReplaceArmy(TroopArmy army, IReadOnlyList<Troop> troops)
    {
        ArgumentNullException.ThrowIfNull(army);
        ArgumentNullException.ThrowIfNull(troops);

        army.Clean();

        int size = Math.Min(TroopArmy.MaximumTroopCount, troops.Count);

        for (int i = 0; i < size; ++i)
        {
            army[i].Set(troops[i]);
        }
    }

    /// <summary>
    /// Gives Archibald's seventh scenario its hard-coded race and army, <c>setHeroAndArmyBonus()</c>
    /// (<c>game_campaign.cpp:517-547</c>).
    /// </summary>
    /// <param name="world">The world, for the map seed the level-up draws use.</param>
    /// <param name="hero">The hero to rebuild.</param>
    /// <param name="scenarioInfoId">The scenario; only Archibald 7 has this bonus.</param>
    /// <remarks>
    /// 只有阿基巴德战役第七关用得上：那一关的英雄由玩家在上一步选的种族决定，三套兵表跟着种族走，另外经验补到 5000。
    /// 参考里种族不在 Necromancer / Warlock / Barbarian 三者时是 <c>assert(0)</c>，端口按「什么都不改」处理。
    /// </remarks>
    private static void SetHeroAndArmyBonus(World world, Hero hero, ScenarioInfoId scenarioInfoId)
    {
        if (scenarioInfoId.CampaignId != CampaignId.Archibald || scenarioInfoId.ScenarioId != 6)
        {
            return;
        }

        switch (hero.Race)
        {
            case MapRace.Necromancer:
                ReplaceArmy(hero.Army,
                [
                    new Troop(MonsterType.Skeleton, 50),
                    new Troop(MonsterType.RoyalMummy, 18),
                    new Troop(MonsterType.VampireLord, 8),
                ]);
                break;

            case MapRace.Warlock:
                ReplaceArmy(hero.Army,
                [
                    new Troop(MonsterType.Centaur, 40),
                    new Troop(MonsterType.Gargoyle, 24),
                    new Troop(MonsterType.Griffin, 18),
                ]);
                break;

            case MapRace.Barbarian:
                ReplaceArmy(hero.Army,
                [
                    new Troop(MonsterType.OrcChief, 12),
                    new Troop(MonsterType.Ogre, 18),
                    new Troop(MonsterType.Goblin, 40),
                ]);
                break;

            default:
                break;
        }

        uint experience = hero.Experience;
        if (experience < 5000)
        {
            hero.IncreaseExperience(5000 - experience, world.MapSeed);
        }
    }

    /// <summary>
    /// The hero a bonus or an award goes to, <c>getHeroToApplyBonusOrAwards()</c> (<c>game_campaign.cpp:549-603</c>).
    /// </summary>
    /// <param name="scenarioInfoId">The scenario being started.</param>
    /// <param name="kingdom">The human kingdom.</param>
    /// <returns>The hero, or <see langword="null"/> when the kingdom has none.</returns>
    /// <remarks>
    /// <para>
    /// 三张表，按顺序查：先看「这一关有指定的主角」，再看「这一关的奖励该给指定种族的人」，都不匹配就给王国里最强的英雄。
    /// 前两张表都是原版写死的剧情需要（罗兰第九关要给罗兰本人、阿基巴德第十关要给阿基巴德、回乡第三关要给加拉凡特）。
    /// </para>
    /// <para>
    /// 按头像编号而不是英雄编号找人：那几关的主角的编号可能与地图里别的英雄相同，只有头像与名字是定制的
    /// （<c>heroes.h</c> 里那几个 <c>CAMPAIGN</c> 英雄）。
    /// </para>
    /// </remarks>
    private static Hero? GetHeroToApplyBonusOrAwards(ScenarioInfoId scenarioInfoId, GameKingdom kingdom)
    {
        int targetPortrait = GetTargetHeroPortrait(scenarioInfoId);
        if (targetPortrait != Hero.UnknownId)
        {
            foreach (Hero hero in kingdom.Heroes)
            {
                if (hero.PortraitId == targetPortrait)
                {
                    return hero;
                }
            }
        }

        MapRace targetRace = GetTargetHeroRace(scenarioInfoId);
        if (targetRace != MapRace.None)
        {
            foreach (Hero hero in kingdom.Heroes)
            {
                if (hero.Race == targetRace)
                {
                    return hero;
                }
            }
        }

        return kingdom.GetBestHero();
    }

    /// <summary>The hero a scenario hands its bonuses to by portrait, <c>targetHeroes</c> (<c>game_campaign.cpp:551-556</c>).</summary>
    /// <param name="scenarioInfoId">The scenario being started.</param>
    /// <returns>The portrait id, or <see cref="Hero.UnknownId"/> when the scenario names nobody.</returns>
    private static int GetTargetHeroPortrait(ScenarioInfoId scenarioInfoId)
    {
        return (scenarioInfoId.CampaignId, scenarioInfoId.ScenarioId) switch
        {
            (CampaignId.Roland, 9) => Hero.Roland,
            (CampaignId.Archibald, 10) => Hero.Archibald,
            (CampaignId.VoyageHome, 3) => Hero.Gallavant,
            _ => Hero.UnknownId,
        };
    }

    /// <summary>The hero race a scenario hands its bonuses to, <c>targetRaces</c> (<c>game_campaign.cpp:576-582</c>).</summary>
    /// <param name="scenarioInfoId">The scenario being started.</param>
    /// <returns>The race, or <see cref="MapRace.None"/> when the scenario names none.</returns>
    private static MapRace GetTargetHeroRace(ScenarioInfoId scenarioInfoId)
    {
        return (scenarioInfoId.CampaignId, scenarioInfoId.ScenarioId) switch
        {
            (CampaignId.Roland, 5) => MapRace.Sorceress,
            (CampaignId.Descendants, 2) => MapRace.Sorceress,
            (CampaignId.Descendants, 7) => MapRace.Sorceress,
            _ => MapRace.None,
        };
    }
}
