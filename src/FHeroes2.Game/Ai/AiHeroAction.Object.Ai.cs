using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Core.Random;

using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

using CastleTown = FHeroes2.Game.Castle.Castle;
using GameArmy = FHeroes2.Game.Army.Army;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The <c>AITo*()</c> branches where the AI decides differently from a player who answers "no" to every
/// question: battles, paid joins, paid recruiting, paid objects and skill picks.
/// </summary>
public static partial class AiHeroAction
{
    /// <summary>
    /// The hero walks into a town, <c>AIToCastle()</c> (<c>ai_hero_action.cpp:404-517</c>).
    /// </summary>
    /// <remarks>
    /// 与玩家侧的差别：AI 走进自己的城什么也不做（参考 429-432，不像玩家那样顺便在魔法行会学法术）。
    /// </remarks>
    private static void AiToCastle(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        CastleTown? castle = world.GetCastleEntrance(tileIndex);

        if (castle is null)
        {
            Debug.Assert(false, "A castle action was dispatched to a tile without a castle.");

            return;
        }

        if (castle.Color == hero.Color)
        {
            return;
        }

        if (hero.IsFriends(castle.Color))
        {
            Debug.Assert(false, "A hero cannot enter an allied castle.");

            return;
        }

        GameKingdom kingdom = world.GetKingdom(hero.Color);
        GameKingdom castleKingdom = world.GetKingdom(castle.Color);

        void Capture()
        {
            castleKingdom.RemoveCastle(castle);
            kingdom.AddCastle(castle);

            world.CaptureObject(tileIndex, hero.Color);

            castle.Scout(world);
        }

        Hero? defender = world.GetHero(castle);

        // 站在城门口：城里必定有守军，先攻城（442-508）。
        if (hero.MapIndex != tileIndex)
        {
            GameArmy garrison = castle.GetActualArmy(defender);
            Debug.Assert(garrison.IsValid && garrison.Color != hero.Color, "An enemy castle must hold an enemy garrison.");

            // ActionPreBattle() 必须在开打前调用、ActionAfterBattle() 打完拆掉临时驻军，顺序不可换。
            castle.ActionPreBattle(defender, isControlAi: true);

            BattleResult result = HeroActions.Fight(world, hero, garrison, tileIndex, castle);

            castle.ActionAfterBattle(result.IsAttackerWin);

            if (!result.IsDefenderWin && defender is not null)
            {
                HeroActions.BattleLose(world, defender, result, attacker: false);
            }
            else if (result.IsDefenderWin && defender is not null)
            {
                defender.IncreaseExperience(result.DefenderExperience, world.MapSeed);
            }

            if (!result.IsAttackerWin)
            {
                HeroActions.BattleLose(world, hero, result, attacker: true);

                return;
            }

            Capture();

            hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

            if (Difficulty.IsArtifactSortingAllowedForAi(Settings.Get().GameDifficulty))
            {
                hero.BagArtifacts.SortFromWorstToBest();
            }

            return;
        }

        // 英雄已经站在敌城里：城里只可能是无人指挥的守军，直接接管（511-516）。
        Debug.Assert(castle.GetActualArmy(defender).IsValid, "An enemy castle must hold its garrison.");

        Capture();
    }

    /// <summary>
    /// The hero walks onto another hero, <c>AIToHeroes()</c> (<c>ai_hero_action.cpp:519-594</c>).
    /// </summary>
    private static void AiToHeroes(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        Hero? other = world.GetTile(tileIndex).GetHero(world);

        if (other is null)
        {
            Debug.Assert(false, "A hero action was dispatched to a tile without a hero.");

            return;
        }

        if (other.Color == hero.Color)
        {
            Meeting(hero, other);

            return;
        }

        if (hero.IsFriends(other.Color))
        {
            Debug.Assert(false, "A hero cannot fight an allied hero.");

            return;
        }

        // 守在城里的敌人：要打的其实是那座城（537-541）。
        if (world.GetCastleEntrance(other.MapIndex) is not null)
        {
            AiToCastle(hero, tileIndex, world, actions);

            return;
        }

        BattleResult result = HeroActions.Fight(world, hero, other.Army, tileIndex, castle: null);

        if (!result.IsDefenderWin)
        {
            HeroActions.BattleLose(world, other, result, attacker: false);
        }

        if (!result.IsAttackerWin)
        {
            HeroActions.BattleLose(world, hero, result, attacker: true);

            return;
        }

        hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

        if (Difficulty.IsArtifactSortingAllowedForAi(Settings.Get().GameDifficulty))
        {
            hero.BagArtifacts.SortFromWorstToBest();
            other.BagArtifacts.SortFromWorstToBest();
        }
    }

    /// <summary>
    /// The hero walks onto a wandering stack, <c>AIToMonster()</c> (<c>ai_hero_action.cpp:596-696</c>).
    /// </summary>
    /// <remarks>
    /// 参考与玩家侧的唯一差别是「AI 的决定 ≠ 玩家点『否』」：免费加入、付钱加入、让路三种情况 AI 一律接受。
    /// 战败时把剩余兵力写回瓦片，原本免费加入的改为金钱条件（674-690）。
    /// </remarks>
    private static void AiToMonster(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        Tile tile = world.GetTile(tileIndex);
        Troop troop = tile.GetTroop(world);

        if (!troop.IsValid)
        {
            Debug.Assert(false, "A monster action was dispatched to a tile without monsters.");

            return;
        }

        GameKingdom kingdom = world.GetKingdom(hero.Color);

        NeutralMonsterJoiningCondition join = GameArmy.GetJoinSolution(hero, tile, troop, world, kingdom);

        bool destroy = false;

        switch (join.Reason)
        {
            // 同盟怪：队伍装得下就跟来，装不下也让路（604-615）。
            case NeutralMonsterJoiningReason.Alliance:
                if (hero.Army.CanJoinTroop(troop.Monster))
                {
                    hero.Army.JoinTroop(troop);
                }

                destroy = true;
                break;

            // 灾星怪：直接让路，不加入（616-620）。
            case NeutralMonsterJoiningReason.Bane:
                destroy = true;
                break;

            // 免费加入（621-629）。
            case NeutralMonsterJoiningReason.Free:
                Debug.Assert(hero.Army.CanJoinTroop(troop.Monster), "A free stack must fit into the army.");

                hero.Army.JoinTroop(troop);

                destroy = true;
                break;

            // 付钱加入（630-641）：价钱按瓦片上整队算，人数按 GetJoinSolution() 给的算。
            case NeutralMonsterJoiningReason.ForMoney:
            {
                Funds price = new(Resource.Gold, (uint)troop.GetTotalCost().Gold);
                Troop joining = new(troop.Monster, (uint)join.MonsterCount);

                Debug.Assert(
                    hero.Army.CanJoinTroop(joining.Monster) && kingdom.AllowPayment(price),
                    "GetJoinSolution() must not ask the AI to pay what it cannot afford.");

                hero.Army.JoinTroop(joining);
                kingdom.OddFundsResource(price);

                destroy = true;
                break;
            }

            // 让路（642-647）。
            case NeutralMonsterJoiningReason.RunAway:
                destroy = true;
                break;

            default:
                break;
        }

        if (!destroy)
        {
            GameArmy defenders = new();
            defenders.SetFromTile(world, tile);

            BattleResult result = HeroActions.Fight(world, hero, defenders, tileIndex, castle: null);

            if (result.IsAttackerWin)
            {
                hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);

                destroy = true;
            }
            else
            {
                HeroActions.BattleLose(world, hero, result, attacker: true);

                uint monstersLeft = defenders.GetCount();

                if (monstersLeft > 0)
                {
                    tile.SetMonsterCount(world, monstersLeft);

                    if (tile.IsMonsterJoinConditionFree(world))
                    {
                        tile.SetMonsterJoinCondition(world, MonsterJoinConditions.Money);
                    }
                }
                else
                {
                    destroy = true;
                }
            }
        }

        if (destroy)
        {
            tile.RemoveMainObject(world);
            tile.ResetObjectMetadata();
        }
    }

    /// <summary>
    /// A treasure chest, <c>AIToTreasureChest()</c> (<c>ai_hero_action.cpp:713-825</c>).
    /// </summary>
    /// <remarks>
    /// 参考在 AI 侧重写了整支流程（玩家侧要先问宿主「要钱还是要经验」），所以这里不能复用
    /// <see cref="HeroActions.ActionToTreasureChest"/>：箱子浮在水上时必然给物件、背包满时折成金币、
    /// 陆地上按「要经验的概率」掷骰——这些都在下面按行照抄。
    /// </remarks>
    private static void AiToTreasureChest(Hero hero, Mp2ObjectType objectType, int tileIndex, World world)
    {
        Tile tile = world.GetTile(tileIndex);

        ArtifactType artifact = tile.GetArtifactFromTile(world);
        uint gold = (uint)tile.GetFundsFromTile(world).Gold;

        bool hasArtifact = artifact != ArtifactType.Unknown;
        bool isBagFull = hero.BagArtifacts.IsFull;

        uint? goldReward = null;
        uint? experienceReward = null;
        ArtifactType? artifactReward = null;

        if (tile.IsWater)
        {
            // 水上的箱子：拿不了物件就把物件折成金币（731-745）。
            if (gold == 0)
            {
                // 什么都没有，只把物件抹掉。
            }
            else if (hasArtifact && !isBagFull)
            {
                goldReward = gold;
                artifactReward = artifact;
            }
            else
            {
                goldReward = hasArtifact ? ArtifactGold.InsteadOf(objectType) : gold;
            }
        }
        else if (gold > 0 || (hasArtifact && isBagFull))
        {
            if (hasArtifact && isBagFull)
            {
                gold = ArtifactGold.InsteadOf(objectType);
            }

            uint experience = gold > 500 ? gold - 500 : 500;
            uint chance = ChanceToChooseExperience(hero, experience, world);

            if ((uint)Rand.Get(1, 100) > chance)
            {
                goldReward = gold;
            }
            else
            {
                experienceReward = experience;
            }
        }
        else if (hasArtifact)
        {
            artifactReward = artifact;
        }

        if (goldReward is { } goldValue)
        {
            world.GetKingdom(hero.Color).AddFundsResource(new Funds(Resource.Gold, goldValue));
        }

        if (experienceReward is { } experienceValue)
        {
            hero.IncreaseExperience(experienceValue, world.MapSeed);
        }

        if (artifactReward is { } artifactValue && !hero.PickupArtifact(artifactValue, world))
        {
            Debug.Assert(false, "A treasure chest artifact must be picked up.");
        }

        tile.RemoveMainObject(world);
        tile.ResetObjectMetadata();
    }

    /// <summary>
    /// The coin flip inside <c>AIToTreasureChest()</c>: how much the AI prefers experience over gold, 0–100
    /// (<c>ai_hero_action.cpp:753-787</c>).
    /// </summary>
    private static uint ChanceToChooseExperience(Hero hero, uint experience, World world)
    {
        int kingdomGold = world.GetKingdom(hero.Color).GetFunds().Gold;

        // 经验与上限 500 的差，下面多处复用（参考的 max( exp, 500 ) - 500）。
        uint baseValue = experience > 500 ? experience - 500 : 0;

        switch (hero.AiRole)
        {
            // 侦察兵与信使基本不要经验，把钱留给王国（757-764）。
            case HeroRole.Scout:
            case HeroRole.Courier:
                return kingdomGold > 10000 && experience >= 1500 ? 10u : 0u;

            // 冠军只在国库极度空虚时考虑拿钱（766-775）。
            case HeroRole.Champion:
                return kingdomGold < 3000 ? baseValue / 15 : 100u;

            case HeroRole.Fighter:
                if (kingdomGold < 3000)
                {
                    return experience >= 1500 ? 10u : 0u;
                }

                return (baseValue + 500) / 15;

            default:
                if (kingdomGold < 3000)
                {
                    return 0u;
                }

                return baseValue / 15;
        }
    }

    /// <summary>
    /// A loose artifact on the ground, <c>AIToArtifact()</c> (<c>ai_hero_action.cpp:1750-1803</c>).
    /// </summary>
    /// <remarks>
    /// AI 只在「钱不够」和「打不过」这两种情况下停手：付得起就先扣钱再捡，其余条件直接捡。
    /// 背包已满、或者已经有法术书时再来捡法术书，参考都直接掉头（1754-1763）。
    /// </remarks>
    private static void AiToArtifact(Hero hero, int tileIndex, World world)
    {
        if (hero.BagArtifacts.IsFull)
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);
        ArtifactType artifact = tile.GetArtifactFromTile(world);

        if (artifact == ArtifactType.Unknown)
        {
            Debug.Assert(false, "An artifact action was dispatched to a tile without an artifact.");

            return;
        }

        if (artifact == ArtifactType.MagicBook && hero.HasSpellBook)
        {
            return;
        }

        ArtifactCaptureCondition condition = tile.GetArtifactCaptureCondition(world);
        GameKingdom kingdom = world.GetKingdom(hero.Color);

        bool picked;

        if (condition is >= ArtifactCaptureCondition.Pay2000Gold and <= ArtifactCaptureCondition.Pay3000GoldAnd5Resources)
        {
            Funds payment = tile.GetArtifactResourceRequirement(world);

            picked = kingdom.AllowPayment(payment);

            if (picked)
            {
                kingdom.OddFundsResource(payment);
            }
        }
        else if (condition is ArtifactCaptureCondition.HaveWisdomSkill or ArtifactCaptureCondition.HaveLeadershipSkill)
        {
            SecondarySkillLevel? required = tile.GetArtifactSecondarySkillRequirement(world);

            Debug.Assert(required is not null, "A skill-gated artifact must name its skill.");

            picked = required is not null && hero.HasSecondarySkill(required.Value.Skill);
        }
        else if (condition is >= ArtifactCaptureCondition.Fight50Rogues and <= ArtifactCaptureCondition.Fight1BoneDragon)
        {
            GameArmy guardians = new();
            guardians.SetFromTile(world, tile);

            BattleResult result = HeroActions.Fight(world, hero, guardians, tileIndex, castle: null);

            picked = result.IsAttackerWin;

            if (picked)
            {
                hero.IncreaseExperience(result.AttackerExperience, world.MapSeed);
            }
            else
            {
                HeroActions.BattleLose(world, hero, result, attacker: true);
            }
        }
        else
        {
            picked = true;
        }

        if (picked && hero.PickupArtifact(artifact, world))
        {
            tile.RemoveMainObject(world);
            tile.ResetObjectMetadata();
        }
    }
}
