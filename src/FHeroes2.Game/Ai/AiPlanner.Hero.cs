using System;
using System.Collections.Generic;
using System.Diagnostics;

using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using ArmyTroops = FHeroes2.Game.Army.Army;
using TroopStack = FHeroes2.Game.Army.Troop;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The hero half of the AI planner: the caches of "may this hero look at this tile" and "what is it worth".
/// </summary>
/// <remarks>
/// <para>
/// This is the first slice of <c>ai_planner_hero.cpp</c>: the two caches the reference keeps per hero
/// (<c>ObjectValidator</c> and <c>ObjectValueStorage</c>) and the small helpers they lean on. The sixteen
/// planner members themselves build on top of these and come with the next slice of the same batch.
/// </para>
/// <para>
/// Both caches are keyed by tile index or by <see cref="IndexObject"/>, exactly like the reference's
/// <c>std::map</c>s: a tile object is asked about many times per turn, and the answer only changes when the
/// hero moves.
/// </para>
/// </remarks>
public sealed partial class AiPlanner
{
    /// <summary>Days in a week, <c>numOfDaysPerWeek</c>.</summary>
    private const int DaysPerWeek = 7;

    /// <summary>Value multiplier for a free upgrade of a stack, <c>freeMonsterUpgradeModifier</c>.</summary>
    internal const double FreeMonsterUpgradeModifier = 3.0;

    /// <summary>The penalty for a task the AI cannot do safely, <c>dangerousTaskPenalty</c>.</summary>
    internal const double DangerousTaskPenalty = 50000.0;

    /// <summary>Base cost of exploring the fog, <c>fogDiscoveryBaseValue</c>.</summary>
    private const double FogDiscoveryBaseValue = -10000.0;

    /// <summary>Days until the next week pays its resources, <c>daysToNextWeek()</c>.</summary>
    /// <remarks>参考数的是「本周还剩几天再加一天」：周日的回答是 1。</remarks>
    /// <returns>The number of days.</returns>
    private static int DaysToNextWeek()
    {
        int currentDay = (int)(World.Current.DayCount % DaysPerWeek);
        return currentDay == 0 ? 1 : DaysPerWeek - currentDay + 1;
    }

    /// <summary>How many whole days a path takes, <c>completedDaysToTarget()</c>.</summary>
    /// <remarks>走一步扣一步的移动力，不够就回满并算一天；走完不够一格路也要再算一天。</remarks>
    /// <param name="steps">The path to walk.</param>
    /// <param name="hero">The hero walking it.</param>
    /// <returns>The number of days.</returns>
    private static int CompletedDaysToTarget(IReadOnlyList<RouteStep> steps, Hero hero)
    {
        int days = 0;
        uint currentMovePoints = hero.MovePoints;

        foreach (RouteStep step in steps)
        {
            if (currentMovePoints > step.Penalty)
            {
                currentMovePoints -= step.Penalty;
            }
            else
            {
                ++days;
                currentMovePoints = hero.GetMaxMovePoints();
            }
        }

        if (currentMovePoints < AdventureMap.MapGrounds.RoadPenalty)
        {
            // 还剩不到一格路的移动力，等于还要再花一天才能继续行动。
            ++days;
        }

        return days;
    }

    /// <summary>Whether the map is won by digging up the ultimate artifact, <c>isFindUltimateArtifactVictoryCondition()</c>.</summary>
    /// <returns>True for the "find the ultimate artifact" victory condition.</returns>
    private static bool IsFindUltimateArtifactVictoryCondition()
    {
        Mp2FileInfo? mapInfo = Settings.Get().CurrentMapFileInfo;
        if (mapInfo == null)
        {
            return false;
        }

        if ((MapConditions.Wins(mapInfo) & GameOver.WinsArtifact) == 0)
        {
            return false;
        }

        return MapConditions.WinsUltimateArtifact(mapInfo);
    }

    /// <summary>Whether an artifact is the one the map asks for, <c>isFindArtifactVictoryCondition()</c>.</summary>
    /// <param name="artifact">The artifact to check.</param>
    /// <returns>True when finding it wins the map.</returns>
    private static bool IsFindArtifactVictoryCondition(ArtifactType artifact)
    {
        Debug.Assert(artifact != ArtifactType.Unknown);

        Mp2FileInfo? mapInfo = Settings.Get().CurrentMapFileInfo;
        if (mapInfo == null)
        {
            return false;
        }

        if ((MapConditions.Wins(mapInfo) & GameOver.WinsArtifact) == 0)
        {
            return false;
        }

        if (MapConditions.WinsUltimateArtifact(mapInfo))
        {
            return ArtifactLevels.IsUltimate(artifact);
        }

        return artifact == MapConditions.WinsArtifact(mapInfo);
    }

    /// <summary>
    /// Whether a castle holds the hero whose defeat wins the map for the human,
    /// <c>isHeroWhoseDefeatIsVictoryConditionForHumanInCastle()</c>.
    /// </summary>
    /// <remarks><c>WINS_HERO</c> 对 AI 不成立：去杀人类的目标英雄等于替对手赢。</remarks>
    /// <param name="castle">The castle to check.</param>
    /// <returns>True when the castle's guest is the human's target hero.</returns>
    private static bool IsHeroWhoseDefeatIsVictoryConditionForHumanInCastle(CastleTown castle)
    {
        // 端口里城堡不存访客英雄，访客就是站在城堡入口上的那个英雄。
        Hero? hero = World.Current.GetTile(castle.TileIndex).GetHero(World.Current);
        return hero != null && ReferenceEquals(hero, World.Current.GetHeroesCondWins());
    }

    /// <summary>
    /// Whether losing a castle loses the map for the human, <c>isCastleLossDefeatConditionForHuman()</c>.
    /// </summary>
    /// <remarks>
    /// 单人图的「丢掉指定城镇」失败条件与「攻下指定城镇」胜利条件都只对人类玩家成立，
    /// AI 不该去抢，所以两种目标城镇都要绕开。
    /// </remarks>
    /// <param name="castle">The castle to check.</param>
    /// <returns>True when the castle is the protected town of the scenario.</returns>
    private static bool IsCastleLossDefeatConditionForHuman(CastleTown castle)
    {
        Mp2FileInfo? mapInfo = Settings.Get().CurrentMapFileInfo;
        if (mapInfo == null)
        {
            return false;
        }

        FHeroes2.Core.Geometry.Point center = World.Current.Geometry!.Value.GetPoint(castle.TileIndex);

        if (PlayerColors.Count(Players.HumanColors()) == 1 && (MapConditions.Loss(mapInfo) & GameOver.LossTown) != 0
            && center == MapConditions.LossPosition(mapInfo))
        {
            return true;
        }

        return MapConditions.ComputerAlsoWins(mapInfo) && (MapConditions.Wins(mapInfo) & GameOver.WinsTown) != 0
               && center == MapConditions.WinsPosition(mapInfo);
    }

    /// <summary>Whether the Dimension Door beats walking, <c>shouldUseDimensionDoor()</c>.</summary>
    /// <param name="regularMovementDist">The walking distance.</param>
    /// <param name="dimensionDoorDist">The Dimension Door distance, or 0 when the spell is not available.</param>
    /// <returns>True when the spell is worth its cost.</returns>
    private static bool ShouldUseDimensionDoor(uint regularMovementDist, uint dimensionDoorDist)
    {
        if (dimensionDoorDist == 0)
        {
            return false;
        }

        // 走不到，或者跳过去不到一半路，才值得花法术点。
        return regularMovementDist == 0 || dimensionDoorDist < regularMovementDist / 2;
    }

    /// <summary>The distance to a tile and whether it is the Dimension Door one, <c>getDistanceToTile()</c>.</summary>
    /// <param name="pathfinder">The AI pathfinder, already re-evaluated for the hero.</param>
    /// <param name="hero">The hero whose spell points are spent.</param>
    /// <param name="index">The tile to measure.</param>
    /// <returns>The distance and the flag telling which of the two it is.</returns>
    private static (uint Distance, bool IsDimensionDoor) GetDistanceToTile(AIWorldPathfinder pathfinder, Hero hero, int index)
    {
        uint regularMovementDist = pathfinder.GetDistance(index);

        List<RouteStep> dimensionDoorPath = pathfinder.BuildDimensionDoorPath(hero, index);
        if (dimensionDoorPath.Count == 0)
        {
            return (regularMovementDist, false);
        }

        // 参考是对整条路径求 Route::calculatePathPenalty()（route.cpp:411），而次元门每一步的 penalty 是
        // Spell( DIMENSIONDOOR ).movePoints() = 225，不是整数 200。写成「跳数 × 200」会把次元门的距离低估
        // 11%，AI 就会比参考更爱跳——这条是 8at.5 对拍出来的量级偏差。
        uint dimensionDoorDist = HeroPath.CalculatePenalty(dimensionDoorPath);

        Debug.Assert(dimensionDoorDist > 0);

        return ShouldUseDimensionDoor(regularMovementDist, dimensionDoorDist)
                   ? (dimensionDoorDist, true)
                   : (regularMovementDist, false);
    }

    /// <summary>Whether a hero should head for a castle, <c>AIShouldVisitCastle()</c>.</summary>
    /// <param name="hero">The hero that would visit.</param>
    /// <param name="castleIndex">The castle's entrance tile.</param>
    /// <param name="heroArmyStrength">The hero's army strength.</param>
    /// <returns>True for an empty own castle or a capturable enemy one.</returns>
    private bool AIShouldVisitCastle(Hero hero, int castleIndex, double heroArmyStrength)
    {
        CastleTown? castle = World.Current.GetCastleEntrance(castleIndex);
        if (castle == null)
        {
            Debug.Fail("Castle entrance without a castle.");
            return false;
        }

        if (hero.Color == castle.Color)
        {
            // 自家空城值得回去驻守。
            return World.Current.GetTile(castle.TileIndex).GetHero(World.Current) == null;
        }

        if (hero.IsFriends(castle.Color) || IsHeroWhoseDefeatIsVictoryConditionForHumanInCastle(castle))
        {
            return false;
        }

        bool isLosingGame = World.Current.GetKingdom(hero.Color).IsLosingGame;
        double advantage = isLosingGame ? AiPlannerInternals.ArmyAdvantageDesperate : AiPlannerInternals.ArmyAdvantageMedium;
        return heroArmyStrength > castle.GetGarrisonStrength(hero) * advantage;
    }

    /// <summary>Whether a hero is garrisoned in a castle, <c>Heroes::inCastle()</c>.</summary>
    /// <remarks>
    /// 参考的 <c>HeroBase</c> 存着所在城堡的指针；端口不存这条反向链接，而是问「他脚下的格子是不是城堡入口」。
    /// 驻守的英雄站在城堡入口格上，所以两种问法在这里答案相同。
    /// </remarks>
    /// <param name="hero">The hero to test.</param>
    /// <returns>True when the hero stands in a castle.</returns>
    private static bool IsHeroInCastle(Hero hero) => World.Current.GetCastleEntrance(hero.MapIndex) != null;

    /// <summary>Whether the hero beats the guards of a tile, <c>isHeroStrongerThan()</c>.</summary>
    /// <param name="tile">The tile whose guards are compared.</param>
    /// <param name="heroArmyStrength">The hero's army strength.</param>
    /// <param name="targetStrengthMultiplier">How much stronger the hero has to be.</param>
    /// <returns>True when the hero wins the fight.</returns>
    private bool IsHeroStrongerThan(Tile tile, double heroArmyStrength, double targetStrengthMultiplier)
    {
        return heroArmyStrength > GetTileArmyStrength(tile) * targetStrengthMultiplier;
    }

    /// <summary>Whether a stack is worth joining an army, <c>isArmyValuableToObtain()</c>.</summary>
    /// <param name="monster">The stack on offer.</param>
    /// <param name="armyStrengthThreshold">The smallest stack the hero cares about.</param>
    /// <param name="armyHasMonster">Whether the army already has this kind of monster.</param>
    /// <returns>True when the stack is above the threshold.</returns>
    private static bool IsArmyValuableToObtain(TroopStack monster, double armyStrengthThreshold, bool armyHasMonster)
    {
        // 已经有同名兵种时，多一队只是加强这一路，门槛减半。
        return monster.GetStrength() > (armyHasMonster ? armyStrengthThreshold / 2 : armyStrengthThreshold);
    }

    /// <summary>Whether a dwelling's stack is worth hiring, <c>isArmyValuableToHire()</c>.</summary>
    /// <param name="army">The army that would hire.</param>
    /// <param name="kingdom">The kingdom that pays.</param>
    /// <param name="tile">The dwelling tile.</param>
    /// <param name="armyStrengthThreshold">The smallest stack the hero cares about.</param>
    /// <returns>True when the kingdom can pay for a stack worth having.</returns>
    private static bool IsArmyValuableToHire(ArmyTroops army, GameKingdom kingdom, Tile tile, double armyStrengthThreshold)
    {
        TroopStack troop = tile.GetTroop(World.Current);
        if (!troop.IsValid)
        {
            return false;
        }

        bool armyHasMonster = army.HasMonster(troop.Monster);
        if (!armyHasMonster && army.OccupiedSlotCount == ArmyTroops.MaximumTroopCount && army.AreAllTroopsUnique())
        {
            // 队伍满员且没有重名兵种，招进来也放不下。
            return false;
        }

        uint recruitTroopCount = (uint)kingdom.Treasury.GetLowestQuotient(troop.GetCost());
        if (recruitTroopCount == 0)
        {
            return false;
        }

        if (recruitTroopCount > troop.Count)
        {
            recruitTroopCount = troop.Count;
        }

        return IsArmyValuableToObtain(new TroopStack(troop.Monster, recruitTroopCount), armyStrengthThreshold, armyHasMonster);
    }

    /// <summary>Whether the AI casts a spell for something other than value, <c>isSpellUsedByAI()</c>.</summary>
    /// <remarks>这几条都是「看一眼」的冒险法术，AI 不会出手，学了等于浪费。</remarks>
    /// <param name="spell">The spell id.</param>
    /// <returns>True for every spell the AI does use.</returns>
    private static bool IsSpellUsedByAi(SpellType spell) => spell switch
    {
        SpellType.Haunt => false,
        SpellType.IdentifyHero => false,
        SpellType.ViewArtifacts => false,
        SpellType.ViewHeroes => false,
        SpellType.ViewMines => false,
        SpellType.ViewResources => false,
        SpellType.ViewTowns => false,
        SpellType.Visions => false,
        _ => true,
    };

    /// <summary>
    /// Whether a hero should consider the object of a tile at all, <c>isValidObjectForHero()</c>
    /// (<c>ai_planner_hero.cpp:328-789</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The "may I even look at it" filter: a shrine the hero cannot learn from, a mine that is already ours, a
    /// dwelling whose stack is too small to matter are all dropped here, before any value is computed. Keeping
    /// it ahead of the value tables is what keeps the priority lists short.
    /// </para>
    /// <para>
    /// The switch follows the reference case for case, in the same alphabetical groups. Objects the AI must
    /// never analyse hit <see cref="Debug.Fail(string)"/> in their <c>assert( 0 )</c> branches, so a debug
    /// build still shouts when a new object type slips through.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that would do the task.</param>
    /// <param name="heroArmyStrength">The hero's army strength.</param>
    /// <param name="index">The tile to check.</param>
    /// <param name="pathfinder">The AI pathfinder, already re-evaluated for the hero.</param>
    /// <param name="armyStrengthThreshold">The smallest stack the hero cares about.</param>
    /// <param name="underHero">Whether the object under the hero should be considered.</param>
    /// <returns>True when the object is worth a closer look.</returns>
    private bool IsValidObjectForHero(Hero hero, double heroArmyStrength, int index, AIWorldPathfinder pathfinder,
        double armyStrengthThreshold, bool underHero)
    {
        World world = World.Current;
        Tile tile = world.GetTile(index);
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(world, !underHero));

        ArmyTroops army = hero.Army;
        GameKingdom kingdom = world.GetKingdom(hero.Color);

        switch (objectType)
        {
        case Mp2ObjectType.Barrel:
        case Mp2ObjectType.Bottle:
        case Mp2ObjectType.Campfire:
        case Mp2ObjectType.Flotsam:
        case Mp2ObjectType.Resource:
        case Mp2ObjectType.SeaChest:
        case Mp2ObjectType.ShipwreckSurvivor:
        case Mp2ObjectType.TreasureChest:
            return true;

        case Mp2ObjectType.Buoy:
        case Mp2ObjectType.Temple:
            return !hero.IsObjectTypeVisited(objectType) && hero.GetMorale() < Morale.Blood && !army.AllTroopsAreUndead();

        case Mp2ObjectType.MagellansMaps:
            return !kingdom.IsVisited(objectType) && kingdom.AllowPayment(PaymentConditions.GetMagellansMapsPurchasePrice());

        case Mp2ObjectType.AlchemistLab:
        case Mp2ObjectType.Lighthouse:
        case Mp2ObjectType.Mine:
        case Mp2ObjectType.Sawmill:
            if (!hero.IsFriends(tile.GetColor(world)))
            {
                return !IsCaptureObjectProtected(tile) || IsHeroStrongerThan(tile, heroArmyStrength, AiPlannerInternals.ArmyAdvantageSmall);
            }

            break;

        case Mp2ObjectType.AbandonedMine:
            // 废弃矿坑的守军对早期英雄太重，要求压倒性优势。
            return IsHeroStrongerThan(tile, heroArmyStrength, AiPlannerInternals.ArmyAdvantageLarge);

        case Mp2ObjectType.LeanTo:
        case Mp2ObjectType.MagicGarden:
        case Mp2ObjectType.Skeleton:
        case Mp2ObjectType.Wagon:
        case Mp2ObjectType.WaterWheel:
        case Mp2ObjectType.Windmill:
            return tile.DoesTileContainValuableItems(world);

        case Mp2ObjectType.Artifact:
            {
                if (hero.BagArtifacts.IsFull)
                {
                    return false;
                }

                ArtifactType artifact = tile.GetArtifactFromTile(world);
                Debug.Assert(artifact != ArtifactType.Unknown);

                if (artifact == ArtifactType.MagicBook && hero.HasSpellBook)
                {
                    // 英雄最多只能带一本魔法书。
                    return false;
                }

                ArtifactCaptureCondition condition = tile.GetArtifactCaptureCondition(world);

                if (condition >= ArtifactCaptureCondition.Pay2000Gold && condition <= ArtifactCaptureCondition.Pay3000GoldAnd5Resources)
                {
                    return kingdom.AllowPayment(tile.GetArtifactResourceRequirement(world));
                }

                if (condition is ArtifactCaptureCondition.HaveWisdomSkill or ArtifactCaptureCondition.HaveLeadershipSkill)
                {
                    SecondarySkillLevel? required = tile.GetArtifactSecondarySkillRequirement(world);
                    return required != null && hero.HasSecondarySkill(required.Value.Skill);
                }

                if (condition >= ArtifactCaptureCondition.Fight50Rogues && condition <= ArtifactCaptureCondition.Fight1BoneDragon)
                {
                    return IsHeroStrongerThan(tile, heroArmyStrength, AiPlannerInternals.ArmyAdvantageLarge);
                }

                // 没有取宝条件，直接走过去就行。
                return true;
            }

        case Mp2ObjectType.ObservationTower:
            {
                FHeroes2.Core.Geometry.Point point = world.Geometry!.Value.GetPoint(index);
                int fogCountToUncover = world.Fog!.CountFoggedAround(point.X, point.Y, MapFog.ObservationTowerRevealDistance, hero.Color);
                return fogCountToUncover > 0;
            }

        case Mp2ObjectType.Obelisk:
            // 方尖碑每座图只能看一次。
            return !kingdom.IsVisited(index, objectType);

        case Mp2ObjectType.Barrier:
            // 屏障只有在自己已经拜访过对应帐篷时才通过。
            return kingdom.IsTravellerTentVisited(tile.GetBarrierColorFromTile(world));

        case Mp2ObjectType.TravellerTent:
            return !kingdom.IsTravellerTentVisited(tile.GetBarrierColorFromTile(world));

        case Mp2ObjectType.ShrineFirstCircle:
        case Mp2ObjectType.ShrineSecondCircle:
        case Mp2ObjectType.ShrineThirdCircle:
            {
                Spell spell = tile.GetSpellFromTile(world);
                if (spell.Type == SpellType.None)
                {
                    Debug.Fail("A shrine without a spell.");
                    return false;
                }

                if (!hero.HasSpellBook)
                {
                    return false;
                }

                if (spell.Level() == 3 && hero.GetLevelSkill(SecondarySkill.Wisdom) == SkillLevel.None)
                {
                    // 高级法术没有智慧术学不了。
                    return false;
                }

                if (!kingdom.IsVisited(index, objectType))
                {
                    // 还没人拜访过：只要这一级还有没学全的法术就去。
                    return hero.MagicBook.GetSpells(spell.Level()).Count < Spell.TotalSpellsPerLevel(spell.Level());
                }

                return !hero.HasSpell(spell, skipBag: true) && IsSpellUsedByAi(spell.Type);
            }

        case Mp2ObjectType.Arena:
            // 竞技场整张图只能进一次。
            return !hero.IsObjectTypeVisited(objectType);

        case Mp2ObjectType.Fort:
        case Mp2ObjectType.Gazebo:
        case Mp2ObjectType.MercenaryCamp:
        case Mp2ObjectType.StandingStones:
        case Mp2ObjectType.WitchDoctorsHut:
            // 一次性给主技能或经验的对象。
            return !hero.IsVisited(tile);

        case Mp2ObjectType.WitchsHut:
            {
                SecondarySkillLevel skill = tile.GetSecondarySkillFromWitchsHut(world);
                SecondarySkill skillType = skill.Skill;
                if (skillType == SecondarySkill.Unknown)
                {
                    Debug.Fail("A witch's hut without a skill.");
                    return false;
                }

                if (hero.HasMaxSecondarySkill())
                {
                    return false;
                }

                if (!kingdom.IsVisited(index, objectType))
                {
                    // 没人拜访过小屋时，AI 不该提前知道它教什么。
                    return true;
                }

                if (hero.HasSecondarySkill(skillType))
                {
                    return false;
                }

                if (skillType == SecondarySkill.Leadership && army.AllTroopsAreUndead())
                {
                    return false;
                }

                if (skillType is SecondarySkill.Mysticism or SecondarySkill.EagleEye && !hero.HasSpellBook)
                {
                    return false;
                }

                // 水面不到全图的 1% 时航海术基本用不上。
                return skillType != SecondarySkill.Navigation || world.WaterPercentage >= 1;
            }

        case Mp2ObjectType.TreeOfKnowledge:
            if (!hero.IsVisited(tile))
            {
                Funds requirements = tile.GetTreeOfKnowledgeRequirement(world);
                // 要掏钱时，参考要求国库付得起五倍才不会把家底掏空。
                if (requirements.GetValidItemsCount() == 0 || kingdom.AllowPayment(requirements * 5))
                {
                    return true;
                }
            }

            break;

        case Mp2ObjectType.FaerieRing:
        case Mp2ObjectType.Fountain:
        case Mp2ObjectType.Idol:
        case Mp2ObjectType.Mermaid:
            // 提升幸运的对象。
            return !hero.IsObjectTypeVisited(objectType) && hero.GetLuck() < Luck.Irish;

        case Mp2ObjectType.Oasis:
        case Mp2ObjectType.WateringHole:
            {
                if (hero.IsObjectTypeVisited(objectType))
                {
                    return false;
                }

                (uint distance, _) = GetDistanceToTile(pathfinder, hero, index);
                if (distance == 0)
                {
                    return false;
                }

                // 走一格路等于两点移动力，加成要跑得比走一趟值。
                double movementPenalty = 2.0 * distance;
                return movementPenalty < GameStatic.GetMovementPointBonus(objectType) || hero.GetMorale() < Morale.Blood;
            }

        case Mp2ObjectType.MagicWell:
            {
                if (hero.IsObjectTypeVisited(objectType))
                {
                    return false;
                }

                if (!hero.HasSpellBook || hero.SpellPoints >= hero.GetMaxSpellPoints())
                {
                    return false;
                }

                (uint distance, _) = GetDistanceToTile(pathfinder, hero, index);
                if (distance == 0)
                {
                    return false;
                }

                if (distance > hero.MovePoints)
                {
                    List<RouteStep> path = pathfinder.BuildPath(index, false);
                    int daysToTarget = CompletedDaysToTarget(path, hero);
                    // 走到那儿法力已经自己回满了，这口井就白喝。
                    if ((uint)daysToTarget * hero.GetDailyRestoredSpellPoints() + hero.SpellPoints >= hero.GetMaxSpellPoints())
                    {
                        return false;
                    }
                }

                return true;
            }

        case Mp2ObjectType.ArtesianSpring:
            return !world.IsAnyKingdomVisited(objectType, index) && hero.HasSpellBook
                   && hero.SpellPoints < 2 * hero.GetMaxSpellPoints();

        case Mp2ObjectType.Xanadu:
            return !hero.IsVisited(tile) && GameStatic.IsHeroWorthyToVisitXanadu(hero);

        case Mp2ObjectType.BlackCat:
            // 士气还能涨、幸运已经满了的时候才去摸黑猫，否则纯亏。
            return !hero.IsVisited(tile) && hero.GetMorale() < Morale.Great && hero.GetLuck() >= Luck.Irish;

        case Mp2ObjectType.ArcherHouse:
        case Mp2ObjectType.Cave:
        case Mp2ObjectType.DwarfCottage:
        case Mp2ObjectType.Excavation:
        case Mp2ObjectType.GoblinHut:
        case Mp2ObjectType.HalflingHole:
        case Mp2ObjectType.PeasantHut:
        case Mp2ObjectType.TreeHouse:
        case Mp2ObjectType.WatchTower:
            {
                // 白送军队的住所。
                TroopStack troop = tile.GetTroop(world);
                if (!troop.IsValid)
                {
                    return false;
                }

                bool armyHasMonster = army.HasMonster(troop.Monster);
                if (!armyHasMonster && army.OccupiedSlotCount == ArmyTroops.MaximumTroopCount && army.AreAllTroopsUnique())
                {
                    return false;
                }

                return IsArmyValuableToObtain(troop, armyStrengthThreshold, armyHasMonster);
            }

        case Mp2ObjectType.AirAltar:
        case Mp2ObjectType.BarrowMounds:
        case Mp2ObjectType.DesertTent:
        case Mp2ObjectType.EarthAltar:
        case Mp2ObjectType.FireAltar:
        case Mp2ObjectType.GenieLamp:
        case Mp2ObjectType.Ruins:
        case Mp2ObjectType.TreeCity:
        case Mp2ObjectType.WagonCamp:
        case Mp2ObjectType.WaterAltar:
            // 可以招募军队的住所。
            return IsArmyValuableToHire(army, kingdom, tile, armyStrengthThreshold);

        case Mp2ObjectType.CityOfDead:
        case Mp2ObjectType.DragonCity:
        case Mp2ObjectType.TrollBridge:
            // 无主的先打赢守军，有主的直接招。
            return tile.GetColor(world) == PlayerColor.None
                       ? IsHeroStrongerThan(tile, heroArmyStrength, AiPlannerInternals.ArmyAdvantageMedium)
                       : IsArmyValuableToHire(army, kingdom, tile, armyStrengthThreshold);

        case Mp2ObjectType.FreemansFoundry:
            // 免费升级枪兵、剑士与铁人。
            return army.HasMonster(MonsterType.Pikeman) || army.HasMonster(MonsterType.Swordsman)
                   || army.HasMonster(MonsterType.IronGolem);

        case Mp2ObjectType.HillFort:
            // 免费升级矮人、半兽人与食人魔。
            return army.HasMonster(MonsterType.Dwarf) || army.HasMonster(MonsterType.Orc) || army.HasMonster(MonsterType.Ogre);

        case Mp2ObjectType.Stables:
            {
                if (army.HasMonster(MonsterType.Cavalry))
                {
                    return true;
                }

                if (hero.IsObjectTypeVisited(objectType))
                {
                    return false;
                }

                (uint distance, _) = GetDistanceToTile(pathfinder, hero, index);
                if (distance == 0)
                {
                    return false;
                }

                // 本周还剩几天就加几天的移动力，再减掉走一趟的代价（world.Day 已经是「本周第几天」）。
                int daysActive = DaysPerWeek - (int)world.Day + 1;
                double movementBonus = daysActive * GameStatic.GetMovementPointBonus(objectType) - 2.0 * distance;
                return movementBonus > 0;
            }

        // 有好东西，但每次拜访都会掉士气，所以只在没去过、战利品还在、且打得过时才值得走一趟。
        case Mp2ObjectType.DerelictShip:
        case Mp2ObjectType.Graveyard:
        case Mp2ObjectType.Shipwreck:
            {
                if (!kingdom.IsVisited(index, objectType) && tile.DoesTileContainValuableItems(world))
                {
                    ArmyTroops enemy = new();
                    enemy.SetFromTile(world, tile);
                    return enemy.IsValid && IsHeroStrongerThan(tile, heroArmyStrength, 2.0);
                }

                break;
            }

        case Mp2ObjectType.Pyramid:
            {
                if (!kingdom.IsVisited(index, objectType) && tile.DoesTileContainValuableItems(world))
                {
                    ArmyTroops enemy = new();
                    enemy.SetFromTile(world, tile);

                    // 没到专家级的智慧术就解不了金字塔的诅咒。
                    return enemy.IsValid && hero.GetLevelSkill(SecondarySkill.Wisdom) == SkillLevel.Expert
                           && IsHeroStrongerThan(tile, heroArmyStrength, AiPlannerInternals.ArmyAdvantageLarge);
                }

                break;
            }

        case Mp2ObjectType.DaemonCave:
            if (tile.DoesTileContainValuableItems(world))
            {
                // 参考里的 AI 从不掷骰子赌运气，总是选择打恶魔的仆从，所以按「中等优势」判断。
                return IsHeroStrongerThan(tile, heroArmyStrength, AiPlannerInternals.ArmyAdvantageMedium);
            }

            break;

        case Mp2ObjectType.Monster:
            // 快输的局只要打得过就去打，否则要求中等优势。
            return IsHeroStrongerThan(tile, heroArmyStrength,
                kingdom.IsLosingGame ? 1.0 : AiPlannerInternals.ArmyAdvantageMedium);

        case Mp2ObjectType.Hero:
            {
                Hero? otherHero = tile.GetHero(world);
                Debug.Assert(otherHero != null);

                if (otherHero == null)
                {
                    return false;
                }

                bool otherHeroInCastle = IsHeroInCastle(otherHero);

                if (hero.Color == otherHero.Color)
                {
                    // 同一王国的两位英雄已经碰过面，就不必再跑一趟。
                    if (hero.HasMetWithHero(otherHero.Id))
                    {
                        return false;
                    }

                    return !otherHeroInCastle;
                }

                if (hero.IsFriends(otherHero.Color))
                {
                    return false;
                }

                // 「击败英雄」的胜利条件不适用于 AI，AI 得让人类玩家的这个英雄活着。
                if (ReferenceEquals(otherHero, world.GetHeroesCondWins()))
                {
                    return false;
                }

                if (otherHeroInCastle)
                {
                    return AIShouldVisitCastle(hero, index, heroArmyStrength);
                }

                return army.IsStrongerThan(otherHero.Army,
                    kingdom.IsLosingGame ? AiPlannerInternals.ArmyAdvantageDesperate : AiPlannerInternals.ArmyAdvantageSmall);
            }

        case Mp2ObjectType.Castle:
            return AIShouldVisitCastle(hero, index, heroArmyStrength);

        case Mp2ObjectType.Jail:
            return kingdom.Heroes.Count < GameKingdom.MaxHeroes;

        case Mp2ObjectType.HutOfMagi:
            return !kingdom.IsVisited(objectType) && world.DoesObjectExistOnMap(Mp2ObjectType.EyeOfMagi);

        case Mp2ObjectType.AlchemistTower:
            {
                int cursed = 0;

                foreach (ArtifactType artifact in hero.BagArtifacts.Slots)
                {
                    if (ArtifactTable.Get(artifact).Curses.Count > 0)
                    {
                        ++cursed;
                    }
                }

                if (cursed == 0)
                {
                    return false;
                }

                return kingdom.AllowPayment(PaymentConditions.ForAlchemist());
            }

        // TODO: AI 还不会用这些对象。
        case Mp2ObjectType.Sirens:
        case Mp2ObjectType.Sphinx:
        case Mp2ObjectType.TradingPost:
            return false;

        // 以下对象 AI 永远不该当作目的地，它只把它们当成路。
        case Mp2ObjectType.Boat:
        // 法师之眼根本不是动作对象。
        case Mp2ObjectType.EyeOfMagi:
        case Mp2ObjectType.Oracle:
        // AI 不读告示牌。
        case Mp2ObjectType.Sign:
        case Mp2ObjectType.StoneLiths:
        case Mp2ObjectType.Whirlpool:
            Debug.Fail($"The AI must never analyse the object {objectType}.");
            return false;

        default:
            // 加了新的动作对象却忘了给 AI 补交互？
            Debug.Fail($"No AI rule for the object {objectType}.");
            break;
        }

        return false;
    }

    /// <summary>
    /// Whether a capturable object still has to be fought for, <c>isCaptureObjectProtected()</c>
    /// (<c>maps_tiles_helper.cpp</c>).
    /// </summary>
    /// <remarks>
    /// 参考只认「有守军」这一种保护：城堡看访客，其余可占领对象看瓦片上的军队。
    /// </remarks>
    /// <param name="tile">The object's tile.</param>
    /// <returns>True when the object cannot be taken without a fight.</returns>
    private static bool IsCaptureObjectProtected(Tile tile)
    {
        World world = World.Current;
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tile.GetMainObjectType(world, false));

        if (!Mp2ObjectTypes.IsCaptureObject(objectType))
        {
            return false;
        }

        if (objectType == Mp2ObjectType.Castle)
        {
            CastleTown? castle = world.GetCastleEntrance(tile.Index);
            return castle != null && tile.GetHero(world) != null;
        }

        return tile.GetTroop(world).IsValid;
    }

    // =========================================================================================================
    // 价值计算辅助函数（ai_planner_hero.cpp:940-1144 的匿名命名空间）
    // =========================================================================================================

    /// <summary>
    /// What upgrading one stack of a monster would add to an army, <c>getMonsterUpgradeValue()</c>
    /// (<c>ai_planner_hero.cpp:940-957</c>).
    /// </summary>
    /// <param name="army">The army to look at.</param>
    /// <param name="monster">The monster whose upgrade is priced.</param>
    /// <returns>The strength difference times the stack size, 0 when there is nothing to upgrade.</returns>
    private static double GetMonsterUpgradeValue(ArmyTroops army, MonsterType monster)
    {
        uint monsterCount = army.GetCountMonsters(monster);

        if (monsterCount == 0)
        {
            // 没有可升级的兵。
            return 0;
        }

        MonsterType upgraded = MonsterUpgrades.GetUpgrade(monster);

        if (upgraded == monster)
        {
            // 这种兵没有升级形态。
            return 0;
        }

        return (MonsterStatistics.GetMonsterStrength(upgraded) - MonsterStatistics.GetMonsterStrength(monster))
               * monsterCount;
    }

    /// <summary>
    /// How much distance should count for an object, <c>getDistanceModifier()</c> (<c>ai_planner_hero.cpp:966-1005</c>).
    /// </summary>
    /// <remarks>
    /// 大于 1 表示「只有近处才值得去」，小于 1 表示「离得远也别放弃」。
    /// </remarks>
    /// <param name="objectType">The object type.</param>
    /// <returns>The multiplier applied to the distance.</returns>
    internal static double GetDistanceModifier(Mp2ObjectType objectType)
    {
        // 参考手上的是瓦片原值（带动作位）；端口的调用点已经折过位，所以这里不能再用 IsInGameActionObject
        // （它要求带位，折过的类型一律 false），改用它拆开后的等价判定。
        if (!Mp2ObjectTypes.CanBeActionObject(objectType) || objectType == Mp2ObjectType.Event)
        {
            return 1.0;
        }

        switch (objectType)
        {
        case Mp2ObjectType.Castle:
            return 0.8;

        case Mp2ObjectType.AlchemistLab:
        case Mp2ObjectType.Artifact:
        case Mp2ObjectType.Hero:
        case Mp2ObjectType.Mine:
        case Mp2ObjectType.Sawmill:
            return 0.9;

        case Mp2ObjectType.Barrel:
        case Mp2ObjectType.Campfire:
        case Mp2ObjectType.Flotsam:
        case Mp2ObjectType.GenieLamp:
        case Mp2ObjectType.Resource:
        case Mp2ObjectType.SeaChest:
        case Mp2ObjectType.TreasureChest:
            return 0.95;

        case Mp2ObjectType.BlackCat:
        case Mp2ObjectType.Buoy:
        case Mp2ObjectType.FaerieRing:
        case Mp2ObjectType.Fountain:
        case Mp2ObjectType.Idol:
        case Mp2ObjectType.Mermaid:
        case Mp2ObjectType.Temple:
            // 士气和幸运类的对象只有在很近的时候才值得绕路。
            return 1.1;

        default:
            break;
        }

        return 1.0;
    }

    /// <summary>
    /// Discounts a value by the distance to the object, <c>scaleWithDistanceAndTime()</c>
    /// (<c>ai_planner_hero.cpp:1007-1023</c>), with the day taken from the world.
    /// </summary>
    /// <param name="value">The undiscounted value.</param>
    /// <param name="distance">The pathfinder distance, 0 for "the hero is standing on it".</param>
    /// <param name="objectType">The object type.</param>
    /// <returns>The discounted value.</returns>
    private static double ScaleWithDistanceAndTime(double value, uint distance, Mp2ObjectType objectType)
    {
        return ScaleWithDistanceAndTime(value, distance, objectType, World.Current.DayCount);
    }

    /// <summary>
    /// Discounts a value by the distance to the object, <c>scaleWithDistanceAndTime()</c>
    /// (<c>ai_planner_hero.cpp:1007-1023</c>), with the day passed in.
    /// </summary>
    /// <remarks>
    /// 参考从 <c>world.CountDay()</c> 直接读天数；端口把天数提成参数只是为了能让测试在固定天数下对拍量级
    /// （生产调用走上面那个重载，读的还是 <see cref="World.Current"/>）。公式一个字都没改。
    /// </remarks>
    /// <param name="value">The undiscounted value.</param>
    /// <param name="distance">The pathfinder distance, 0 for "the hero is standing on it".</param>
    /// <param name="objectType">The object type.</param>
    /// <param name="dayCount">Days since the start of the game, <c>world.CountDay()</c>.</param>
    /// <returns>The discounted value.</returns>
    internal static double ScaleWithDistanceAndTime(double value, uint distance, Mp2ObjectType objectType, uint dayCount)
    {
        if (distance == 0)
        {
            return value;
        }

        // 有些对象不会随距离急剧贬值，这样 AI 英雄才能一直盯着重要目标。
        double correctedDistance = distance * GetDistanceModifier(objectType);

        // 随着日子过去，距离的权重缓慢变小，AI 会越来越愿意跑远路。0.5 是上限（约 5000 天）。
        double timeCorrectionCoeff = 1 - Math.Min(0.5, dayCount * 0.0001);
        correctedDistance *= timeCorrectionCoeff;

        // 非线性衰减：距离越大，丢的分越多。
        return value - (correctedDistance * Math.Log10(correctedDistance));
    }

    /// <summary>
    /// What discovering the fog around the hero is worth to him, <c>getFogDiscoveryValue()</c>
    /// (<c>ai_planner_hero.cpp:1025-1044</c>).
    /// </summary>
    /// <param name="hero">The hero.</param>
    /// <returns>The value of one step into the fog.</returns>
    private static double GetFogDiscoveryValue(Hero hero)
    {
        switch (hero.AiRole)
        {
        case HeroRole.Scout:
            // 侦察兵的职责就是探图，所以探图不扣分。
            return 0;

        case HeroRole.Hunter:
        case HeroRole.Fighter:
        case HeroRole.Champion:
            return FogDiscoveryBaseValue;

        case HeroRole.Courier:
            return FogDiscoveryBaseValue * 2;

        default:
            // 加了新的英雄类型就必须在这里补上逻辑。
            Debug.Fail($"Unknown hero role {hero.AiRole}.");
            break;
        }

        return FogDiscoveryBaseValue;
    }

    /// <summary>
    /// What learning a secondary skill is worth to a hero, <c>getSecondarySkillValue()</c>
    /// (<c>ai_planner_hero.cpp:1046-1122</c>).
    /// </summary>
    /// <param name="hero">The hero who would learn it.</param>
    /// <param name="skill">The skill on offer.</param>
    /// <param name="level">The level on offer.</param>
    /// <returns>The value, 0 when the hero already knows that level or better.</returns>
    private static double GetSecondarySkillValue(Hero hero, SecondarySkill skill, SkillLevel level)
    {
        if (hero.GetLevelSkill(skill) >= level)
        {
            return 0;
        }

        bool isCourierOrScout = hero.AiRole is HeroRole.Courier or HeroRole.Scout;

        switch (skill)
        {
        case SecondarySkill.Wisdom:
            // 不查城堡和法术，因为「高智慧术」本身就是造魔法行会的优先级依据。
            return level == SkillLevel.Basic ? 2500.0 : 1000.0;

        case SecondarySkill.Logistics:
            return 1500.0;

        case SecondarySkill.Leadership:
            return hero.Army.AllTroopsAreUndead() ? 100.0 : 1000.0;

        case SecondarySkill.Necromancy:
            return hero.Army.AllTroopsAreUndead() ? 1000.0 : 100.0;

        case SecondarySkill.Luck:
            return isCourierOrScout ? 100.0 : 500.0;

        case SecondarySkill.Ballistics:
            return isCourierOrScout ? 100.0 : hero.Army.IsMeleeDominantArmy() ? 1250.0 : 250.0;

        case SecondarySkill.Archery:
            return isCourierOrScout ? 100.0 : hero.Army.IsMeleeDominantArmy() ? 100.0 : 500.0;

        case SecondarySkill.Estates:
            return hero.AiRole is HeroRole.Champion or HeroRole.Fighter ? 0.0 : 1000.0;

        case SecondarySkill.Pathfinding:
            {
                double roughness = World.Current.LandRoughness;
                return roughness > 1.25 ? 1000.0 : roughness > 1.1 ? 250.0 : 100.0;
            }

        case SecondarySkill.Navigation:
            {
                byte waterPercentage = World.Current.WaterPercentage;
                return waterPercentage > 60 ? 1000.0 : waterPercentage > 25 ? 100.0 : 0.0;
            }

        case SecondarySkill.Scouting:
            if (hero.AiRole is HeroRole.Champion or HeroRole.Fighter)
            {
                return 0.0;
            }

            return hero.AiRole == HeroRole.Scout ? 1250.0 : 100.0;

        case SecondarySkill.Mysticism:
            return hero.HasSpellBook ? 500.0 : 100.0;

        case SecondarySkill.EagleEye:
            return hero.HasSpellBook ? 250.0 : 0.0;

        case SecondarySkill.Diplomacy:
            // 不鼓励 AI 去捡，但既然已经会了就优先升级，好省下金币。
            return level == SkillLevel.Basic ? 100.0 : 1250.0;

        case SecondarySkill.Unknown:
            return 0;

        default:
            // 加了新的二级技能就必须在这里补上。
            Debug.Fail($"Unknown secondary skill {skill}.");
            break;
        }

        return 0;
    }

    /// <summary>
    /// How many days an idle hero may wander before looking into the fog becomes urgent,
    /// <c>getTimeoutBeforeFogDiscoveryIntensification()</c> (<c>ai_planner_hero.cpp:1124-1143</c>).
    /// </summary>
    /// <param name="hero">The hero.</param>
    /// <returns>The number of days.</returns>
    private static uint GetTimeoutBeforeFogDiscoveryIntensification(Hero hero)
    {
        switch (hero.AiRole)
        {
        case HeroRole.Scout:
            return 30;

        case HeroRole.Hunter:
            return 90;

        case HeroRole.Fighter:
        case HeroRole.Champion:
            return 60;

        case HeroRole.Courier:
            return 120;

        default:
            // 加了新的英雄类型就必须在这里补上逻辑。
            Debug.Fail($"Unknown hero role {hero.AiRole}.");
            break;
        }

        return 30;
    }

    // =========================================================================================================
    // 对象价值表（ai_planner_hero.cpp:1147-2311）
    // =========================================================================================================

    /// <summary>
    /// The value of an object for a hero, <c>Planner::getObjectValue()</c> (<c>ai_planner_hero.cpp:2240-2266</c>).
    /// </summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="valueToIgnore">Value returned for "not worth a detour".</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, the caller divides it by the distance.</returns>
    public double GetObjectValue(Hero hero, int index, Mp2ObjectType objectType, double valueToIgnore, uint distanceToObject)
    {
        ArgumentNullException.ThrowIfNull(hero);

        Debug.Assert(objectType == Mp2ObjectTypes.NonActionType(World.Current.GetTile(index).MainObjectType));

        switch (hero.AiRole)
        {
        case HeroRole.Hunter:
            return GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject);

        case HeroRole.Scout:
            return GetScoutObjectValue(hero, index, valueToIgnore, distanceToObject);

        case HeroRole.Champion:
        case HeroRole.Fighter:
            return GetFighterObjectValue(hero, index, valueToIgnore, distanceToObject);

        case HeroRole.Courier:
            return GetCourierObjectValue(hero, index, valueToIgnore, distanceToObject);

        default:
            // 加了新的英雄类型就必须在这里补上逻辑。
            Debug.Fail($"Unknown hero role {hero.AiRole}.");
            break;
        }

        return 0;
    }

    /// <summary>
    /// The value of an object for a hero that cannot visit it now but might visit it later, so its weekly refill is
    /// taken into account, <c>Planner::getFutureObjectValue()</c> (<c>ai_planner_hero.cpp:2268-2311</c>).
    /// </summary>
    /// <param name="hero">The hero that would visit it later.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="objectType">Type of the object.</param>
    /// <param name="valueToIgnore">Value returned for "not worth a detour".</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value as if the object had just been refilled.</returns>
    public double GetFutureObjectValue(Hero hero, int index, Mp2ObjectType objectType, double valueToIgnore, uint distanceToObject)
    {
        ArgumentNullException.ThrowIfNull(hero);

        switch (objectType)
        {
        case Mp2ObjectType.AirAltar:
        case Mp2ObjectType.ArcherHouse:
        case Mp2ObjectType.ArtesianSpring:
        case Mp2ObjectType.BarrowMounds:
        case Mp2ObjectType.Cave:
        case Mp2ObjectType.CityOfDead:
        case Mp2ObjectType.DesertTent:
        case Mp2ObjectType.DragonCity:
        case Mp2ObjectType.DwarfCottage:
        case Mp2ObjectType.EarthAltar:
        case Mp2ObjectType.Excavation:
        case Mp2ObjectType.FireAltar:
        case Mp2ObjectType.GoblinHut:
        case Mp2ObjectType.HalflingHole:
        case Mp2ObjectType.MagicGarden:
        case Mp2ObjectType.PeasantHut:
        case Mp2ObjectType.Ruins:
        case Mp2ObjectType.Stables:
        case Mp2ObjectType.TreeCity:
        case Mp2ObjectType.TreeHouse:
        case Mp2ObjectType.TrollBridge:
        case Mp2ObjectType.WagonCamp:
        case Mp2ObjectType.WatchTower:
        case Mp2ObjectType.WaterAltar:
        case Mp2ObjectType.WaterWheel:
        case Mp2ObjectType.Windmill:
            {
                Debug.Assert(Mp2ObjectTypes.IsWeekLife(objectType));

                // 参考用 TileRestorer 把瓦片临时换成本周刷新后的样子再估值，估值完再换回去。
                // 端口没有 restorer，存一份元数据快照就够了：每周刷新的对象只改元数据里那几个格子。
                Tile tile = World.Current.GetTile(index);
                uint[] saved = (uint[])tile.Metadata.Clone();

                try
                {
                    tile.UpdateObjectInfoTile(World.Current);

                    return GetObjectValue(hero, index, objectType, valueToIgnore, distanceToObject);
                }
                finally
                {
                    Array.Copy(saved, tile.Metadata, saved.Length);
                }
            }

        default:
            break;
        }

        return GetObjectValue(hero, index, objectType, valueToIgnore, distanceToObject);
    }

    /// <summary>
    /// The value of an object for any hero, <c>Planner::getGeneralObjectValue()</c>
    /// (<c>ai_planner_hero.cpp:1147-1781</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这张表是「物件本身值多少分」，与距离无关：1 格距离大约折 100 分，由 <see cref="ScaleWithDistanceAndTime"/>
    /// 在外面扣。负数不是「没价值」而是「亏本」——扣掉 <see cref="DangerousTaskPenalty"/> 之后无论多近都不会被选中。
    /// </para>
    /// <para>
    /// 参考的城堡估值是递归的：被威胁的城堡要按「敌人正在威胁它」再估一遍。端口用本地函数照抄这个递归。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value returned for "not worth a detour".</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, the caller divides it by the distance.</returns>
    private double GetGeneralObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject)
    {
        World world = World.Current;
        Tile tile = world.GetTile(index);
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tile.MainObjectType);

        const double defenselessCastleModifier = 1.25;

        // ── 城堡估值：被威胁的城堡按「最值钱的自家城堡」计价，打掉威胁比守着更划算 ──
        double CalculateCastleValue(CastleTown castle)
        {
            double value = (castle.GetBuildingValue() * 150.0) + 3000;

            if (world.GetKingdom(hero.Color).IsLosingGame)
            {
                value += 15000;
            }

            if (IsCastleLossDefeatConditionForHuman(castle))
            {
                value += 20000;
            }

            if (hero.Color == castle.Color)
            {
                // 这是自家需要保护的城：按「被敌人拿走后要抢回来」的价钱算，但不加太多，免得玩家钓鱼。
                value *= defenselessCastleModifier;
            }
            else
            {
                int castleIndex = castle.TileIndex;

                if (IsCriticalTask(castleIndex) && _priorityTargets.TryGetValue(castleIndex, out PriorityTask? attackTask))
                {
                    Debug.Assert(attackTask.Type == PriorityTaskType.Attack);

                    foreach (int secondaryTaskTileIndex in attackTask.SecondaryTaskTileIndexes)
                    {
                        CastleTown? castleUnderThreat = world.GetCastleEntrance(secondaryTaskTileIndex);
                        if (castleUnderThreat == null)
                        {
                            continue;
                        }

                        Debug.Assert(hero.Color == castleUnderThreat.Color);

                        // 给威胁目标加个奖励，让 AI 有机会就把它拔掉，而不是干等着守城。
                        value = Math.Max(value, CalculateCastleValue(castleUnderThreat) * 2);
                    }
                }

                if (!castle.GetActualArmy(AiCommon.FindGuestHero(world, castle)).IsValid)
                {
                    // 空城，值得顺手拿走。
                    value *= defenselessCastleModifier;
                }
            }

            return value;
        }

        switch (objectType)
        {
        case Mp2ObjectType.Castle:
            {
                CastleTown? castle = world.GetCastleEntrance(index);
                if (castle == null)
                {
                    // 怎么会有这种瓦片？
                    Debug.Fail("A castle tile without a castle.");
                    return valueToIgnore;
                }

                if (hero.Color == castle.Color)
                {
                    // 自家城堡告急时不看英雄的眼前好处（比如有没有援军），而是按「丢了要抢回来」的价钱算。
                    if (IsCriticalTask(index))
                    {
                        return CalculateCastleValue(castle);
                    }

                    double value = castle.GetVisitValue(hero, world.GetKingdom(hero.Color).Treasury);

                    if (!IsPriorityTask(index) && value < 500)
                    {
                        return valueToIgnore;
                    }

                    return value;
                }

                // 友军王国的城堡永远不该是目的地。
                if (hero.IsFriends(castle.Color))
                {
                    Debug.Fail("The hero should never consider a friendly castle.");
                    return -DangerousTaskPenalty;
                }

                // 「击败英雄」的胜利条件不适用于 AI，所以带着人类胜利条件英雄的城堡不能碰。
                if (IsHeroWhoseDefeatIsVictoryConditionForHumanInCastle(castle))
                {
                    Debug.Fail("The hero should never consider a castle holding the human's winning hero.");
                    return -DangerousTaskPenalty;
                }

                return CalculateCastleValue(castle);
            }

        case Mp2ObjectType.Hero:
            {
                Hero? otherHero = tile.GetHero(world);
                if (otherHero == null)
                {
                    // 怎么会有这种瓦片？
                    Debug.Fail("A hero tile without a hero.");
                    return valueToIgnore;
                }

                if (hero.Color == otherHero.Color)
                {
                    if (hero.AiRole > otherHero.AiRole)
                    {
                        // 对方级别更低，让他自己找过来。
                        return valueToIgnore;
                    }

                    if (hero.AiRole == otherHero.AiRole
                        && hero.GetStatsValue() + Difficulty.GetMinStatDiffForAiHeroesMeeting(Settings.Get().GameDifficulty)
                        > otherHero.GetStatsValue())
                    {
                        // 本方更强或差不多，让对方过来更省时间。
                        return valueToIgnore;
                    }

                    double meetingValue = hero.GetMeetingValue(otherHero);

                    // 友军会面的价值封顶 10000 分（大约 30 格），小于 250 分就当没这回事。
                    return meetingValue < 250 ? valueToIgnore : Math.Min(meetingValue, 10000.0);
                }

                // 友军王国的英雄永远不该碰。
                if (hero.IsFriends(otherHero.Color))
                {
                    Debug.Fail("The hero should never consider a friendly hero.");
                    return -DangerousTaskPenalty;
                }

                // 「击败英雄」的胜利条件不适用于 AI，得给人类玩家留着这个英雄的命。
                if (ReferenceEquals(otherHero, world.GetHeroesCondWins()))
                {
                    Debug.Fail("The hero should never consider the human's winning hero.");
                    return -DangerousTaskPenalty;
                }

                double enemyValue = 5000;

                if (IsCriticalTask(index) && _priorityTargets.TryGetValue(index, out PriorityTask? attackTask))
                {
                    Debug.Assert(attackTask.Type == PriorityTaskType.Attack);

                    foreach (int secondaryTaskTileIndex in attackTask.SecondaryTaskTileIndexes)
                    {
                        CastleTown? castle = world.GetCastleEntrance(secondaryTaskTileIndex);
                        if (castle == null)
                        {
                            continue;
                        }

                        Debug.Assert(hero.Color == castle.Color);

                        // 打掉这个威胁比守城划得多，所以按自家城堡的两倍计价。
                        enemyValue = Math.Max(enemyValue, CalculateCastleValue(castle) * 2);
                    }
                }
                else
                {
                    CastleTown? castle = IsHeroInCastle(otherHero) ? world.GetCastleEntrance(otherHero.MapIndex) : null;

                    if (castle != null)
                    {
                        // 敌将在城里：打赢他等于顺手拿城，所以把城的价值加进来。
                        enemyValue += CalculateCastleValue(castle);
                    }
                    else if (world.GetKingdom(otherHero.Color).IsControlAi)
                    {
                        // AI 之间不该像人类那样急着互相残杀，否则人类玩家只要等 AI 自己清理门户就行了。
                        enemyValue *= 0.8;
                    }
                }

                return enemyValue;
            }

        case Mp2ObjectType.Monster:
            {
                ArmyTroops monsters = new();
                monsters.SetFromTile(world, tile);

                if (!monsters.IsValid)
                {
                    Debug.Fail("A monster tile without a monster stack.");
                    return -DangerousTaskPenalty;
                }

                // 参考 TODO：还没比较双方兵力，先按「血量的百分之一 + 1000」估。
                return 1000.0 + (monsters.GetTotalHitPoints() / 100.0);
            }

        case Mp2ObjectType.AlchemistLab:
        case Mp2ObjectType.Mine:
        case Mp2ObjectType.Sawmill:
        case Mp2ObjectType.AbandonedMine:
            {
                int resourceType;
                int resourceAmount;

                if (objectType == Mp2ObjectType.AbandonedMine)
                {
                    // 弃矿本质上就是金矿。
                    resourceType = Resource.Gold;
                    resourceAmount = ProfitConditions.FromMine(Resource.Gold).Get(Resource.Gold);
                }
                else
                {
                    if (tile.GetColor(world) == hero.Color)
                    {
                        // 已经是我们的了，连路过都不必。
                        return valueToIgnore;
                    }

                    (resourceType, resourceAmount) = tile.GetDailyIncomeObjectResources(world).GetFirstValidResource();
                }

                if (resourceType == Resource.Unknown || resourceAmount == 0)
                {
                    // 参考在这里 assert：真正的矿场一定带着产出元数据。但地图里确实存在「矿场」类型却只剩
                    // 山体精灵、元数据被清空的瓦片（多格矿场旁边的山体就长这样，见 Tile.InitIncomeObjectMetadata），
                    // 参考的 Debug 版同样会炸。这里退化成「不值得去」，免得一个坏瓦片卡死整局 AI。
                    return 0;
                }

                // 矿场天天进账，所以按产量计价。
                return resourceAmount * GetResourcePriorityModifier(resourceType, true);
            }

        case Mp2ObjectType.Artifact:
        case Mp2ObjectType.ShipwreckSurvivor:
            {
                ArtifactType artifact = tile.GetArtifactFromTile(world);
                Debug.Assert(ArtifactTypes.IsValid(artifact));

                double multiplier = IsFindArtifactVictoryCondition(artifact) ? 3000.0 : 1000.0;

                return multiplier * ArtifactValues.Value(artifact);
            }

        case Mp2ObjectType.SeaChest:
        case Mp2ObjectType.TreasureChest:
            // 宝箱和海上宝箱平均能开出这么多金币。
            return GetFundsValueBasedOnPriority(FundsOf(gold: 1500));

        case Mp2ObjectType.DaemonCave:
            {
                // 洞穴已经空了就永远别来。
                if (!tile.DoesTileContainValuableItems(world))
                {
                    Debug.Fail("An empty Daemon Cave considered as a target.");
                    return -DangerousTaskPenalty;
                }

                // 打完恶魔总给 2500 金，而且 AI 从不掷骰子赌运气，所以直接按金币计价。
                return GetFundsValueBasedOnPriority(FundsOf(gold: 2500));
            }

        case Mp2ObjectType.Graveyard:
        case Mp2ObjectType.Shipwreck:
        case Mp2ObjectType.Skeleton:
            {
                ArtifactType artifact = tile.GetArtifactFromTile(world);
                if (!ArtifactTypes.IsValid(artifact))
                {
                    // 没东西可捡，别浪费时间。
                    return -DangerousTaskPenalty;
                }

                return 1000.0 * ArtifactValues.Value(artifact);
            }

        case Mp2ObjectType.Wagon:
            {
                ArtifactType artifact = tile.GetArtifactFromTile(world);
                if (ArtifactTypes.IsValid(artifact))
                {
                    return 1000.0 * ArtifactValues.Value(artifact);
                }

                Funds loot = tile.GetFundsFromTile(world);
                double value = GetFundsValueBasedOnPriority(loot);

                // 已经被别人翻过了。
                return value < 1 ? valueToIgnore : value;
            }

        case Mp2ObjectType.Bottle:
            // 漂流瓶只有一段文字，对 AI 毫无用处，但它会挡路，所以给 0 而不是负分。
            return 0;

        case Mp2ObjectType.Barrel:
        case Mp2ObjectType.Campfire:
            // 木桶/营火给 4 到 6 份随机资源加 400 到 600 金，取平均：每种资源 1 份、金 400。
            {
                Funds loot = FundsOf(wood: 1, mercury: 1, ore: 1, sulfur: 1, crystal: 1, gems: 1, gold: 400);

                double value = GetFundsValueBasedOnPriority(loot);
                Debug.Assert(value > 0);

                return value;
            }

        case Mp2ObjectType.MagicGarden:
            {
                Debug.Assert(tile.DoesTileContainValuableItems(world));

                // 魔法花园要么给 5 颗宝石要么给 500 金，取一半：3 颗宝石 + 250 金。
                Funds loot = FundsOf(gems: 3, gold: 250);

                double value = GetFundsValueBasedOnPriority(loot);
                Debug.Assert(value > 0);

                return value;
            }

        case Mp2ObjectType.Resource:
            {
                Funds loot = tile.GetFundsFromTile(world);

                // 资源点的图标就写着是什么资源，所以不必猜类型，只按「典型数量」估。
                (int resourceType, _) = loot.GetFirstValidResource();

                Funds estimatedResource;

                switch (resourceType)
                {
                case Resource.Gold:
                    estimatedResource = FundsOf(gold: 750);
                    break;

                case Resource.Wood:
                case Resource.Ore:
                    estimatedResource = FundsOf(ore: resourceType == Resource.Ore ? 7 : 0, wood: resourceType == Resource.Wood ? 7 : 0);
                    break;

                case Resource.Mercury:
                case Resource.Sulfur:
                case Resource.Crystal:
                case Resource.Gems:
                    estimatedResource = resourceType switch
                    {
                        Resource.Mercury => FundsOf(mercury: 4),
                        Resource.Sulfur => FundsOf(sulfur: 4),
                        Resource.Crystal => FundsOf(crystal: 4),
                        _ => FundsOf(gems: 4),
                    };

                    break;

                default:
                    Debug.Fail($"Unexpected resource type {resourceType}.");
                    estimatedResource = default;
                    break;
                }

                double value = GetFundsValueBasedOnPriority(estimatedResource);
                Debug.Assert(value > 0);

                return value;
            }

        case Mp2ObjectType.DerelictShip:
        case Mp2ObjectType.LeanTo:
        case Mp2ObjectType.WaterWheel:
        case Mp2ObjectType.Windmill:
            {
                Funds loot = tile.GetFundsFromTile(world);
                double value = GetFundsValueBasedOnPriority(loot);

                // 已经被别人拿走了。
                return value < 1 ? valueToIgnore : value;
            }

        case Mp2ObjectType.Flotsam:
            // 浮木的四种可能各占 25%：500 金 + 10 木、200 金 + 5 木、5 木、空。
            // AI 不该知道具体结果（否则等于作弊），所以取平均：175 金 + 5 木。
            {
                Funds loot = FundsOf(wood: 5, gold: 175);

                double value = GetFundsValueBasedOnPriority(loot);
                Debug.Assert(value > 0);

                return value;
            }

        case Mp2ObjectType.Lighthouse:
            // 参考 TODO：等 AI 会造船之后再细化。
            if (tile.GetColor(world) == hero.Color)
            {
                return -DangerousTaskPenalty;
            }

            return 500;

        case Mp2ObjectType.Xanadu:
            return 3000;

        case Mp2ObjectType.ShrineFirstCircle:
        case Mp2ObjectType.ShrineSecondCircle:
        case Mp2ObjectType.ShrineThirdCircle:
            {
                Spell spell = tile.GetSpellFromTile(world);

                return spell.GetStrategicValue(hero.Army.GetStrength(), hero.GetMaxSpellPoints(), hero.GetPower());
            }

        case Mp2ObjectType.Arena:
        case Mp2ObjectType.Fort:
        case Mp2ObjectType.MercenaryCamp:
        case Mp2ObjectType.StandingStones:
        case Mp2ObjectType.TreeOfKnowledge:
        case Mp2ObjectType.WitchDoctorsHut:
        case Mp2ObjectType.WitchsHut:
            return 500.0;

        // 可以花钱招兵的地方。
        case Mp2ObjectType.AirAltar:
        case Mp2ObjectType.BarrowMounds:
        case Mp2ObjectType.CityOfDead:
        case Mp2ObjectType.DesertTent:
        case Mp2ObjectType.DragonCity:
        case Mp2ObjectType.EarthAltar:
        case Mp2ObjectType.FireAltar:
        case Mp2ObjectType.GenieLamp:
        case Mp2ObjectType.Ruins:
        case Mp2ObjectType.TreeCity:
        case Mp2ObjectType.TrollBridge:
        case Mp2ObjectType.WagonCamp:
        case Mp2ObjectType.WaterAltar:
            {
                TroopStack troop = tile.GetTroop(world);
                if (!troop.IsValid)
                {
                    // 兵营里必须有货才会走到这里。
                    Debug.Fail("A dwelling without a monster stack considered as a target.");
                    return -DangerousTaskPenalty;
                }

                Funds singleMonsterCost = troop.GetCost();

                uint recruitTroopCount = (uint)world.GetKingdom(hero.Color).Treasury.GetLowestQuotient(singleMonsterCost);
                if (recruitTroopCount == 0)
                {
                    // AI 在访问别的对象时把钱花掉了，确实会这样。
                    return -DangerousTaskPenalty;
                }

                uint availableTroopCount = troop.Count;
                if (recruitTroopCount > availableTroopCount)
                {
                    recruitTroopCount = availableTroopCount;
                }

                return new TroopStack(troop.Monster, recruitTroopCount).GetStrength();
            }

        // 免费送兵的地方。
        case Mp2ObjectType.ArcherHouse:
        case Mp2ObjectType.Cave:
        case Mp2ObjectType.DwarfCottage:
        case Mp2ObjectType.Excavation:
        case Mp2ObjectType.GoblinHut:
        case Mp2ObjectType.HalflingHole:
        case Mp2ObjectType.PeasantHut:
        case Mp2ObjectType.TreeHouse:
        case Mp2ObjectType.WatchTower:
            return tile.GetTroop(world).GetStrength();

        case Mp2ObjectType.StoneLiths:
            // 石阵不算动作对象，走到这里说明前面的逻辑出了问题。
            Debug.Fail("Stone Liths must never be valued.");
            return -DangerousTaskPenalty;

        case Mp2ObjectType.ObservationTower:
            {
                int fogCountToUncoverByTower = GetFogTileCountToBeRevealed(index, MapFog.ObservationTowerRevealDistance, hero.Color);

                if (fogCountToUncoverByTower == 0)
                {
                    // 周围已经全看见了。
                    return -DangerousTaskPenalty;
                }

                return fogCountToUncoverByTower;
            }

        case Mp2ObjectType.Obelisk:
            return IsFindUltimateArtifactVictoryCondition() ? 2500 : 1000;

        case Mp2ObjectType.MagellansMaps:
            // 非常值钱。
            return 5000;

        case Mp2ObjectType.Boat:
        case Mp2ObjectType.Coast:
        case Mp2ObjectType.Whirlpool:
            // 海岸不是对象，船和漩涡也不算动作对象，走到这里说明前面的逻辑出了问题。
            Debug.Fail($"{objectType} must never be valued.");
            return -DangerousTaskPenalty;

        case Mp2ObjectType.ArtesianSpring:
            // 没有魔法书，或者加不满魔法点，都不值得去。
            if (!hero.HasSpellBook || (hero.SpellPoints * 2) >= hero.GetMaxSpellPoints())
            {
                return -DangerousTaskPenalty;
            }

            return hero.IsPotentSpellcaster() ? 1500 : 0;

        case Mp2ObjectType.MagicWell:
            // 没有魔法书，或者魔法点已经满了，都不值得去。
            if (!hero.HasSpellBook || hero.SpellPoints >= hero.GetMaxSpellPoints())
            {
                return -DangerousTaskPenalty;
            }

            return hero.IsPotentSpellcaster() ? 1500 : 0;

        case Mp2ObjectType.BlackCat:
        case Mp2ObjectType.Buoy:
        case Mp2ObjectType.Temple:
            {
                if (hero.Army.AllTroopsAreUndead())
                {
                    // 全员亡灵，士气没用。
                    return 0;
                }

                int morale = hero.GetMorale();

                if (morale >= Morale.Blood)
                {
                    // 士气已经满了，没必要去。
                    return -DangerousTaskPenalty;
                }

                if (morale == Morale.Great)
                {
                    // 士气够好了，不值得再去。
                    return -4000;
                }

                if (morale == Morale.Good)
                {
                    // 士气略高于平常，还值得去吗？
                    return -2000;
                }

                return morale == Morale.Normal ? 50 : 100;
            }

        case Mp2ObjectType.Stables:
            {
                // 本周还剩几天可以吃到加速，扣掉来回的路。
                int daysActive = DaysPerWeek - (int)world.Day + 1;
                double movementBonus = (daysActive * GameStatic.GetMovementPointBonus(objectType)) - (2.0 * distanceToObject);

                double upgradeValue = GetMonsterUpgradeValue(hero.Army, MonsterType.Champion);
                if (upgradeValue > 0.0001)
                {
                    // 就算没有加速，光升级骑兵也值得跑一趟。
                    return Math.Max(movementBonus, 0.0) + (FreeMonsterUpgradeModifier * upgradeValue);
                }

                // 连路费都赚不回来。
                return movementBonus < 0 ? -DangerousTaskPenalty : movementBonus;
            }

        case Mp2ObjectType.FreemansFoundry:
            {
                double upgradePikemanValue = GetMonsterUpgradeValue(hero.Army, MonsterType.Pikeman);
                double upgradeSwordsmanValue = GetMonsterUpgradeValue(hero.Army, MonsterType.Swordsman);
                double upgradeGolemValue = GetMonsterUpgradeValue(hero.Army, MonsterType.IronGolem);

                return FreeMonsterUpgradeModifier * (upgradePikemanValue + upgradeSwordsmanValue + upgradeGolemValue);
            }

        case Mp2ObjectType.HillFort:
            {
                double upgradeDwarfValue = GetMonsterUpgradeValue(hero.Army, MonsterType.Dwarf);
                double upgradeOrcValue = GetMonsterUpgradeValue(hero.Army, MonsterType.Orc);
                double upgradeOgreValue = GetMonsterUpgradeValue(hero.Army, MonsterType.Ogre);

                return FreeMonsterUpgradeModifier * (upgradeDwarfValue + upgradeOrcValue + upgradeOgreValue);
            }

        case Mp2ObjectType.Barrier:
        case Mp2ObjectType.TravellerTent:
            // 开了它多半能进更多地方。
            return 1000;

        case Mp2ObjectType.Oasis:
        case Mp2ObjectType.WateringHole:
            {
                double movementBonus = GameStatic.GetMovementPointBonus(objectType) - (2.0 * distanceToObject);

                // 连路费都赚不回来。
                return movementBonus < 0 ? -DangerousTaskPenalty : movementBonus;
            }

        case Mp2ObjectType.Jail:
            // 白送一个英雄，而且可能是个狠角色。
            return 3000;

        case Mp2ObjectType.HutOfMagi:
            {
                int fogCountToUncover = 0;
                PlayerColor heroColor = hero.Color;

                foreach (int eyeIndex in world.AllEyeOfMagi)
                {
                    fogCountToUncover += GetFogTileCountToBeRevealed(eyeIndex, MapFog.MagiEyesRevealDistance, heroColor);
                }

                return fogCountToUncover;
            }

        case Mp2ObjectType.Gazebo:
            {
                // 白送 1000 经验：英雄等级越高，这 1000 点越不值钱。
                uint heroExperience = hero.Experience;
                uint nextLevelExperience = HeroExperience.FromLevel(HeroExperience.LevelFrom(heroExperience));
                uint neededExperience = nextLevelExperience - heroExperience;

                if (neededExperience < 1000)
                {
                    // 直接升级，必须去。
                    return 1000;
                }

                return 1000.0 * 1000.0 / neededExperience;
            }

        case Mp2ObjectType.Pyramid:
            return 1500;

        case Mp2ObjectType.FaerieRing:
        case Mp2ObjectType.Fountain:
        case Mp2ObjectType.Idol:
        case Mp2ObjectType.Mermaid:
            {
                int luck = hero.GetLuck();

                if (luck >= Luck.Irish)
                {
                    // 幸运已经满了，没必要去。
                    return -DangerousTaskPenalty;
                }

                if (luck == Luck.Great)
                {
                    return -4000;
                }

                if (luck == Luck.Good)
                {
                    return -2000;
                }

                return luck == Luck.Normal ? 50 : 100;
            }

        case Mp2ObjectType.AlchemistTower:
            {
                int cursed = 0;

                foreach (ArtifactType artifact in hero.BagArtifacts.Slots)
                {
                    if (ArtifactTable.Get(artifact).Curses.Count > 0)
                    {
                        ++cursed;
                    }
                }

                if (cursed == 0)
                {
                    return -DangerousTaskPenalty;
                }

                // 参考 TODO：还没给这个对象定价。
                return 0;
            }

        case Mp2ObjectType.EyeOfMagi:
        case Mp2ObjectType.Oracle:
        case Mp2ObjectType.Sign:
            // 对 AI 毫无用处。
            return -DangerousTaskPenalty;

        case Mp2ObjectType.Sirens:
        case Mp2ObjectType.Sphinx:
        case Mp2ObjectType.TradingPost:
            // 参考 TODO：还没给这些对象定价，暂时一律避开。
            return -DangerousTaskPenalty;

        default:
            // 加了新的动作对象却忘了定价？
            Debug.Fail($"No AI value for the object {objectType}.");
            break;
        }

        return 0;
    }

    /// <summary>
    /// The value of an object for a fighting hero, <c>Planner::getFighterObjectValue()</c>
    /// (<c>ai_planner_hero.cpp:1783-2082</c>).
    /// </summary>
    /// <remarks>战士更看重战斗和城堡，其他对象一律打折——除了少数「打了就赚」的。</remarks>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value returned for "not worth a detour".</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, the caller divides it by the distance.</returns>
    private double GetFighterObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject)
    {
        Debug.Assert(hero.AiRole is HeroRole.Fighter or HeroRole.Champion);

        World world = World.Current;
        Tile tile = world.GetTile(index);
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tile.MainObjectType);

        const double defenselessCastleModifier = 1.5;

        // 与通用表相比，战士愿意为城堡多付三倍多的价钱。
        double CalculateCastleValue(CastleTown castle)
        {
            double value = (castle.GetBuildingValue() * 500.0) + 15000;

            if (world.GetKingdom(hero.Color).IsLosingGame)
            {
                value += 15000;
            }

            if (IsCastleLossDefeatConditionForHuman(castle))
            {
                value += 20000;
            }

            if (hero.Color == castle.Color)
            {
                value *= defenselessCastleModifier;
            }
            else
            {
                int castleIndex = castle.TileIndex;

                if (IsCriticalTask(castleIndex) && _priorityTargets.TryGetValue(castleIndex, out PriorityTask? attackTask))
                {
                    Debug.Assert(attackTask.Type == PriorityTaskType.Attack);

                    foreach (int secondaryTaskTileIndex in attackTask.SecondaryTaskTileIndexes)
                    {
                        CastleTown? castleUnderThreat = world.GetCastleEntrance(secondaryTaskTileIndex);
                        if (castleUnderThreat == null)
                        {
                            continue;
                        }

                        Debug.Assert(hero.Color == castleUnderThreat.Color);

                        value = Math.Max(value, CalculateCastleValue(castleUnderThreat) * 2);
                    }
                }

                if (!castle.GetActualArmy(AiCommon.FindGuestHero(world, castle)).IsValid)
                {
                    value *= defenselessCastleModifier;
                }
            }

            return value;
        }

        switch (objectType)
        {
        case Mp2ObjectType.Castle:
            {
                CastleTown? castle = world.GetCastleEntrance(index);
                if (castle == null)
                {
                    // 怎么会有这种瓦片？
                    Debug.Fail("A castle tile without a castle.");
                    return valueToIgnore;
                }

                if (hero.Color == castle.Color)
                {
                    if (IsCriticalTask(index))
                    {
                        return CalculateCastleValue(castle);
                    }

                    double value = castle.GetVisitValue(hero, world.GetKingdom(hero.Color).Treasury);

                    if (!IsPriorityTask(index) && value < 500)
                    {
                        return valueToIgnore;
                    }

                    // 战士跑回自家城堡要打对折：他只是来取兵或升级，不是来收益的。
                    return value / 2;
                }

                if (hero.IsFriends(castle.Color))
                {
                    Debug.Fail("The hero should never consider a friendly castle.");
                    return -DangerousTaskPenalty;
                }

                if (IsHeroWhoseDefeatIsVictoryConditionForHumanInCastle(castle))
                {
                    Debug.Fail("The hero should never consider a castle holding the human's winning hero.");
                    return -DangerousTaskPenalty;
                }

                return CalculateCastleValue(castle);
            }

        case Mp2ObjectType.Hero:
            {
                Hero? otherHero = tile.GetHero(world);
                if (otherHero == null)
                {
                    // 怎么会有这种瓦片？
                    Debug.Fail("A hero tile without a hero.");
                    return valueToIgnore;
                }

                if (hero.Color == otherHero.Color)
                {
                    if (hero.AiRole > otherHero.AiRole)
                    {
                        // 对方级别更低，让他自己找过来。
                        return valueToIgnore;
                    }

                    if (hero.AiRole == otherHero.AiRole
                        && hero.GetStatsValue() + Difficulty.GetMinStatDiffForAiHeroesMeeting(Settings.Get().GameDifficulty) + 1
                        > otherHero.GetStatsValue())
                    {
                        // 本方更强或差不多，让对方过来更省时间。
                        return valueToIgnore;
                    }

                    double meetingValue = hero.GetMeetingValue(otherHero);

                    // 友军会面的价值封顶 5000 分（大约 30 格）。
                    return meetingValue < 250 ? valueToIgnore : Math.Min(meetingValue, 5000.0);
                }

                if (hero.IsFriends(otherHero.Color))
                {
                    Debug.Fail("The hero should never consider a friendly hero.");
                    return -DangerousTaskPenalty;
                }

                if (ReferenceEquals(otherHero, world.GetHeroesCondWins()))
                {
                    Debug.Fail("The hero should never consider the human's winning hero.");
                    return -DangerousTaskPenalty;
                }

                double enemyValue = 12000;

                if (IsCriticalTask(index) && _priorityTargets.TryGetValue(index, out PriorityTask? attackTask))
                {
                    Debug.Assert(attackTask.Type == PriorityTaskType.Attack);

                    foreach (int secondaryTaskTileIndex in attackTask.SecondaryTaskTileIndexes)
                    {
                        CastleTown? castle = world.GetCastleEntrance(secondaryTaskTileIndex);
                        if (castle == null)
                        {
                            continue;
                        }

                        Debug.Assert(hero.Color == castle.Color);

                        enemyValue = Math.Max(enemyValue, CalculateCastleValue(castle) * 2);
                    }
                }
                else
                {
                    CastleTown? castle = IsHeroInCastle(otherHero) ? world.GetCastleEntrance(otherHero.MapIndex) : null;

                    if (castle != null)
                    {
                        enemyValue += CalculateCastleValue(castle);
                    }
                    else if (world.GetKingdom(otherHero.Color).IsControlAi)
                    {
                        // AI 之间不该像人类那样急着互相残杀。
                        enemyValue *= 0.8;
                    }
                }

                return enemyValue;
            }

        case Mp2ObjectType.Monster:
            {
                ArmyTroops monsters = new();
                monsters.SetFromTile(world, tile);

                if (!monsters.IsValid)
                {
                    Debug.Fail("A monster tile without a monster stack.");
                    return -DangerousTaskPenalty;
                }

                // 参考 TODO：还没比较双方兵力。战士在自己辖区里还有别的英雄时更该清怪。
                bool anotherFriendlyHeroPresent = _regions[(int)tile.Region].FriendlyHeroes > 1;

                return (anotherFriendlyHeroPresent ? 4000.0 : 1000.0) + (monsters.GetTotalHitPoints() / 100.0);
            }

        case Mp2ObjectType.AbandonedMine:
            return 5000.0;

        case Mp2ObjectType.Artifact:
            {
                ArtifactType artifact = tile.GetArtifactFromTile(world);
                Debug.Assert(ArtifactTypes.IsValid(artifact));

                return (IsFindArtifactVictoryCondition(artifact) ? 3000.0 : 1500.0) * ArtifactValues.Value(artifact);
            }

        case Mp2ObjectType.Barrel:
        case Mp2ObjectType.Campfire:
        case Mp2ObjectType.Flotsam:
        case Mp2ObjectType.LeanTo:
        case Mp2ObjectType.MagicGarden:
        case Mp2ObjectType.Resource:
        case Mp2ObjectType.WaterWheel:
        case Mp2ObjectType.Windmill:
            // 辖区里有别的英雄，就让给他捡。
            if (_regions[(int)tile.Region].FriendlyHeroes > 1)
            {
                return 100.0;
            }

            // 否则按通用表算。
            break;

        case Mp2ObjectType.Lighthouse:
            if (tile.GetColor(world) == hero.Color)
            {
                return -DangerousTaskPenalty;
            }

            return 250;

        case Mp2ObjectType.Xanadu:
            return 3500;

        case Mp2ObjectType.ShrineFirstCircle:
        case Mp2ObjectType.ShrineSecondCircle:
        case Mp2ObjectType.ShrineThirdCircle:
            {
                Spell spell = tile.GetSpellFromTile(world);

                // 上限 8500，略低于金字塔的 10000（五级法术），免得 AI 把学四级法术当成头等大事。
                return Math.Min(8500.0, spell.GetStrategicValue(hero.Army.GetStrength(), hero.GetMaxSpellPoints(), hero.GetPower()) * 1.1);
            }

        case Mp2ObjectType.Arena:
        case Mp2ObjectType.Fort:
        case Mp2ObjectType.MercenaryCamp:
        case Mp2ObjectType.StandingStones:
        case Mp2ObjectType.TreeOfKnowledge:
        case Mp2ObjectType.WitchDoctorsHut:
        case Mp2ObjectType.WitchsHut:
            return 1250.0;

        case Mp2ObjectType.ObservationTower:
            return GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject) / 2;

        case Mp2ObjectType.ArtesianSpring:
            if (!hero.HasSpellBook || (hero.SpellPoints * 2) >= hero.GetMaxSpellPoints())
            {
                return -DangerousTaskPenalty;
            }

            return hero.IsPotentSpellcaster() ? 2000 : 0;

        case Mp2ObjectType.MagicWell:
            if (!hero.HasSpellBook || hero.SpellPoints >= hero.GetMaxSpellPoints())
            {
                return -DangerousTaskPenalty;
            }

            return hero.IsPotentSpellcaster() ? 2000 : 0;

        case Mp2ObjectType.Buoy:
        case Mp2ObjectType.Temple:
            {
                if (hero.Army.AllTroopsAreUndead())
                {
                    return 0;
                }

                int morale = hero.GetMorale();

                if (morale >= Morale.Blood)
                {
                    return -DangerousTaskPenalty;
                }

                if (morale == Morale.Great)
                {
                    return -4000;
                }

                if (morale == Morale.Good)
                {
                    return -2000;
                }

                return morale == Morale.Normal ? 50 : 200;
            }

        case Mp2ObjectType.HutOfMagi:
            return GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject) / 2;

        case Mp2ObjectType.Pyramid:
            return 10000;

        default:
            break;
        }

        return GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject);
    }

    /// <summary>
    /// The value of an object for a courier hero, <c>Planner::getCourierObjectValue()</c>
    /// (<c>ai_planner_hero.cpp:2084-2191</c>).
    /// </summary>
    /// <remarks>信使的主职是跑腿，只有在「顺路」的距离档位里才考虑绕道。</remarks>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value returned for "not worth a detour".</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, the caller divides it by the distance.</returns>
    private double GetCourierObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject)
    {
        Debug.Assert(hero.AiRole == HeroRole.Courier);

        World world = World.Current;
        Tile tile = world.GetTile(index);
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tile.MainObjectType);

        // 参考把这些分数按「信使愿意绕多远」写成档位：2 格 500，5 格 1400，10 格 3000。
        const double TwoTiles = 500;
        const double FiveTiles = 1400;
        const double TenTiles = 3000;

        switch (objectType)
        {
        case Mp2ObjectType.Hero:
            {
                Hero? otherHero = tile.GetHero(world);
                if (otherHero == null)
                {
                    Debug.Fail("A hero tile without a hero.");
                    return valueToIgnore;
                }

                if (hero.Color == otherHero.Color)
                {
                    // 会面是主任务的事，这里不重复计。
                    return valueToIgnore;
                }

                if (hero.IsFriends(otherHero.Color))
                {
                    Debug.Fail("The hero should never consider a friendly hero.");
                    return -DangerousTaskPenalty;
                }

                if (ReferenceEquals(otherHero, world.GetHeroesCondWins()))
                {
                    Debug.Fail("The hero should never consider the human's winning hero.");
                    return -DangerousTaskPenalty;
                }

                // 这个英雄威胁着自家城堡，交给通用表仔细算。
                if (IsCriticalTask(index))
                {
                    break;
                }

                return TenTiles;
            }

        case Mp2ObjectType.Monster:
            {
                ArmyTroops monsters = new();
                monsters.SetFromTile(world, tile);

                if (!monsters.IsValid)
                {
                    Debug.Fail("A monster tile without a monster stack.");
                    return -DangerousTaskPenalty;
                }

                // 参考 TODO：还没比较双方兵力。
                return TwoTiles + (monsters.GetTotalHitPoints() / 100.0);
            }

        case Mp2ObjectType.AlchemistLab:
        case Mp2ObjectType.Mine:
        case Mp2ObjectType.Sawmill:
            if (tile.GetColor(world) == hero.Color)
            {
                return -DangerousTaskPenalty;
            }

            // 产金的矿更值得绕远。
            return tile.GetDailyIncomeObjectResources(world).Gold > 0 ? TenTiles : FiveTiles;

        case Mp2ObjectType.Barrel:
        case Mp2ObjectType.Campfire:
        case Mp2ObjectType.Flotsam:
        case Mp2ObjectType.GenieLamp:
        case Mp2ObjectType.Resource:
        case Mp2ObjectType.SeaChest:
        case Mp2ObjectType.TreasureChest:
            return TwoTiles;

        case Mp2ObjectType.Arena:
        case Mp2ObjectType.Fort:
        case Mp2ObjectType.MercenaryCamp:
        case Mp2ObjectType.StandingStones:
        case Mp2ObjectType.TreeOfKnowledge:
        case Mp2ObjectType.WitchDoctorsHut:
        case Mp2ObjectType.WitchsHut:
        case Mp2ObjectType.Xanadu:
            return FiveTiles;

        case Mp2ObjectType.ArtesianSpring:
            if (!hero.HasSpellBook || (hero.SpellPoints * 2) >= hero.GetMaxSpellPoints())
            {
                return -DangerousTaskPenalty;
            }

            return hero.IsPotentSpellcaster() ? FiveTiles : 0;

        case Mp2ObjectType.MagicWell:
            if (!hero.HasSpellBook || hero.SpellPoints >= hero.GetMaxSpellPoints())
            {
                return -DangerousTaskPenalty;
            }

            return hero.IsPotentSpellcaster() ? FiveTiles : 0;

        default:
            break;
        }

        return GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject);
    }

    /// <summary>
    /// The value of an object for a scouting hero, <c>Planner::getScoutObjectValue()</c>
    /// (<c>ai_planner_hero.cpp:2193-2238</c>).
    /// </summary>
    /// <remarks>侦察兵唯一的私心是女巫小屋给出的侦察/后勤/寻路——那三种技能直接放大他的本行。</remarks>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="valueToIgnore">Value returned for "not worth a detour".</param>
    /// <param name="distanceToObject">Distance the hero has to travel.</param>
    /// <returns>The value, the caller divides it by the distance.</returns>
    private double GetScoutObjectValue(Hero hero, int index, double valueToIgnore, uint distanceToObject)
    {
        Debug.Assert(hero.AiRole == HeroRole.Scout);

        World world = World.Current;
        Tile tile = world.GetTile(index);
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(tile.MainObjectType);

        switch (objectType)
        {
        case Mp2ObjectType.WitchsHut:
            {
                if (!hero.IsVisited(index, objectType))
                {
                    // 还没去过，按通用表估。
                    return GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject);
                }

                SecondarySkillLevel skill = tile.GetSecondarySkillFromWitchsHut(world);
                if (!skill.IsValid || hero.HasMaxSecondarySkill())
                {
                    // 前面的检查应该已经挡住了。
                    Debug.Fail("A Witch's Hut that offers nothing useful.");
                    return -DangerousTaskPenalty;
                }

                if (hero.HasSecondarySkill(skill.Skill))
                {
                    // 前面的检查应该已经挡住了。
                    Debug.Fail("A Witch's Hut that offers a skill the hero already knows.");
                    return -DangerousTaskPenalty;
                }

                double value = GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject);

                // 侦察兵最该拿的就是这三种技能。
                if (skill.Skill is SecondarySkill.Scouting or SecondarySkill.Logistics)
                {
                    value *= 3;
                }
                else if (skill.Skill == SecondarySkill.Pathfinding)
                {
                    value *= 2;
                }

                return value;
            }

        default:
            break;
        }

        return GetGeneralObjectValue(hero, index, valueToIgnore, distanceToObject);
    }

    /// <summary>
    /// Builds a <see cref="Funds"/> from named amounts, <c>Funds{ Resource::X, amount }</c> in the reference.
    /// </summary>
    /// <remarks>
    /// 端口的 <see cref="Funds"/> 构造函数是七个位置参数且没有默认值；这个入口让调用点能写清「只给某一种资源」。
    /// </remarks>
    /// <param name="wood">Wood.</param>
    /// <param name="mercury">Mercury.</param>
    /// <param name="ore">Ore.</param>
    /// <param name="sulfur">Sulfur.</param>
    /// <param name="crystal">Crystal.</param>
    /// <param name="gems">Gems.</param>
    /// <param name="gold">Gold.</param>
    /// <returns>The prepared resource bundle.</returns>
    private static Funds FundsOf(int wood = 0, int mercury = 0, int ore = 0, int sulfur = 0, int crystal = 0, int gems = 0, int gold = 0) =>
        new(ore, wood, mercury, sulfur, crystal, gems, gold);

    /// <summary>
    /// How many tiles around a map position the hero would still uncover, <c>Maps::getFogTileCountToBeRevealed()</c>.
    /// </summary>
    /// <param name="tileIndex">Center of the disc.</param>
    /// <param name="distance">Radius of the disc in tiles.</param>
    /// <param name="color">The player whose fog counts.</param>
    /// <returns>How many tiles of the disc are still undiscovered for <paramref name="color"/>.</returns>
    private static int GetFogTileCountToBeRevealed(int tileIndex, int distance, PlayerColor color)
    {
        World world = World.Current;
        FHeroes2.Core.Geometry.Point point = world.Geometry!.Value.GetPoint(tileIndex);

        return world.Fog!.CountFoggedAround(point.X, point.Y, distance, color);
    }

    /// <summary>
    /// Picks one of two secondary skills, <c>Planner::pickSecondarySkill()</c> (<c>ai_planner_hero.cpp:2882-2907</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 分数差不到 0.001 时按等级取：两个都不值钱（低于 300）就挑那个英雄已经学过的低等级技能——反正学不下去，
    /// 至少别把宝贵的技能格浪费在新技能上。否则优先选基础技能，把已有的技能往上堆。
    /// </para>
    /// <para>
    /// 只有一边有效时说明这次升级只给了一个选择，那就没得挑。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that levels up.</param>
    /// <param name="left">First offered skill, invalid when the level gives only one choice.</param>
    /// <param name="right">Second offered skill.</param>
    /// <returns>The skill the AI takes.</returns>
    public static SecondarySkillLevel PickSecondarySkill(Hero hero, SecondarySkillLevel left, SecondarySkillLevel right)
    {
        ArgumentNullException.ThrowIfNull(hero);

        // 有些等级只给一个选择，这时 left 是空槽，两边都无效时返回哪个都一样。
        if (!right.IsValid)
        {
            return left;
        }

        double leftValue = GetSecondarySkillValue(hero, left.Skill, left.Level);
        double rightValue = GetSecondarySkillValue(hero, right.Skill, right.Level);

        if (Math.Abs(leftValue - rightValue) < 0.001)
        {
            // 分数低到这个程度说明这技能不值得学，尽量别把技能格花掉。
            if (leftValue < 300.0)
            {
                return left.Level == SkillLevel.Basic ? right : left;
            }

            return left.Level == SkillLevel.Basic ? left : right;
        }

        return leftValue > rightValue ? left : right;
    }

    // -----------------------------------------------------------------------------------------------------------------
    // The turn of the heroes (ai_planner_hero.cpp:2756-3150): what a hero does when it starts moving, when it reaches
    // a tile, when it finishes an action, and how the planner hands out the tasks of a turn one hero at a time.
    // -----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Turns an object a hero just acted on into tasks, <c>Planner::updatePriorityTargets()</c>
    /// (<c>ai_planner_hero.cpp:2756-2880</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only castles and heroes carry a priority task: a castle that changed hands is worth defending or attacking, and
    /// an enemy hero is worth chasing. Everything else is answered by its value alone.
    /// </para>
    /// <para>
    /// When the hero acted on the tile of his own task, the task is finished: a hero who walked into his castle is put
    /// to sleep there until the next turn, and an attack that has been carried out is dropped. A castle that was taken
    /// from under the hero — the hero is still on the tile below it — keeps its task, because the hero has not reached
    /// the castle yet.
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that acted.</param>
    /// <param name="tileIndex">Tile the action happened on.</param>
    /// <param name="objectType">Object on that tile.</param>
    private void UpdatePriorityTargets(Hero hero, int tileIndex, Mp2ObjectType objectType)
    {
        ArgumentNullException.ThrowIfNull(hero);

        // 调用点给的是折过动作位的类型（ADR-133），参考那边比的是瓦片原值 OBJ_CASTLE / OBJ_HERO。
        // 这里再折一次是幂等的：直接拿 Castle / Hero 来比，不然英雄刚打完一仗那条分支永远进不去，
        // 输了的一方会以 MapIndex == -1 的残影留在 _enemyArmies 里，下一个英雄估威胁时拿它去问寻路器就炸。
        Mp2ObjectType nonActionType = Mp2ObjectTypes.NonActionType(objectType);

        if (nonActionType is not (Mp2ObjectType.Castle or Mp2ObjectType.Hero))
        {
            // 目前只有城堡和英雄会带任务。
            return;
        }

        World world = World.Current;

        void UpdateCastle()
        {
            CastleTown? castle = world.GetCastleEntrance(tileIndex);

            if (castle is null)
            {
                Debug.Fail("A castle tile without a castle.");

                _enemyArmies.Remove(tileIndex);
                return;
            }

            if (hero.IsFriends(castle.Color))
            {
                _enemyArmies.Remove(tileIndex);

                UpdatePriorityForCastle(castle);
            }
            else
            {
                UpdatePriorityAttackTarget(world.GetKingdom(hero.Color), world.GetTile(tileIndex));
            }
        }

        void UpdateTile()
        {
            if (nonActionType == Mp2ObjectType.Castle)
            {
                UpdateCastle();
            }
            else
            {
                Debug.Assert(nonActionType == Mp2ObjectType.Hero);

                Tile tile = world.GetTile(tileIndex);
                Hero? anotherHero = tile.GetHero(world);

                if (anotherHero is null)
                {
                    // 另一个英雄打输了，但他守的城堡可能还在（城门格存的是带动作位的城堡，要折一次再比）。
                    if (Mp2ObjectTypes.NonActionType(tile.MainObjectType) == Mp2ObjectType.Castle)
                    {
                        UpdateCastle();
                    }
                    else
                    {
                        _enemyArmies.Remove(tileIndex);
                    }

                    return;
                }

                if (!hero.IsFriends(anotherHero.Color))
                {
                    UpdatePriorityAttackTarget(world.GetKingdom(hero.Color), tile);
                }
            }
        }

        if (!_priorityTargets.TryGetValue(tileIndex, out PriorityTask? task) || task is null)
        {
            // 这个格子原本不是任务，但打完一仗之后可能是了（尤其是敌人刚把骷髅或幽灵攒起来）。
            UpdateTile();

            if (!_priorityTargets.TryGetValue(tileIndex, out task) || task is null)
            {
                return;
            }
        }

        switch (task.Type)
        {
        case PriorityTaskType.Defend:
        case PriorityTaskType.Reinforce:
            if (hero.MapIndex != tileIndex)
            {
                // 要么城堡刚被夺走，要么英雄遇上了友方城堡的客座英雄。
                Debug.Assert(world.GetTile(tileIndex).MainObjectType == Mp2ObjectType.Castle,
                    "The tile of a defend task is a castle.");
                Debug.Assert(world.Geometry!.Value.IsValidDirection(tileIndex, Direction.Bottom)
                             && hero.MapIndex == world.Geometry!.Value.GetDirectionIndex(tileIndex, Direction.Bottom),
                    "A hero that has not reached the castle stands on the tile below it.");

                // 城堡刚换了主人，格子上的信息要跟着更新。
                UpdateTile();

                // 英雄还没走到任务所在的格子，任务也就没完成，先别删。
                return;
            }

            Debug.Assert(objectType == Mp2ObjectType.Castle, "Defend and reinforce tasks are for castles only.");
            Debug.Assert(hero.IsActive, "A hero cannot die on his way into his own castle.");

            hero.SetModes(HeroModes.Sleeper);

            _priorityTargets.Remove(tileIndex);

            break;

        case PriorityTaskType.Attack:
            RemovePriorityAttackTarget(tileIndex);

            UpdateTile();

            break;

        default:
            Debug.Fail("A new kind of priority task needs its own logic here.");
            break;
        }
    }

    /// <summary>
    /// Puts a hero to bed for the rest of the turn when his route leads to a task, and otherwise asks him to stay
    /// put, <c>Planner::addHeroToMove()</c> (<c>ai_planner_hero.cpp:833-843</c>).
    /// </summary>
    /// <remarks>
    /// 两种英雄这一回合不动：已经走到巡逻点、并且夜里不往回走的巡逻兵，以及接到守着某个城堡任务的英雄。
    /// 「守着城堡」是任务本身的报酬——他明天从城里出发，比在野外过夜强。
    /// </remarks>
    /// <param name="hero">The hero to consider.</param>
    /// <param name="heroesToMove">The list the hero is added to when he still has something to do.</param>
    /// <param name="world">The world the hero lives in.</param>
    private static void AddHeroToMove(Hero hero, List<Hero> heroesToMove, World world)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(heroesToMove);
        ArgumentNullException.ThrowIfNull(world);

        if (hero.Modes.HasFlag(HeroModes.Patrol) && hero.PatrolCenterIndex == hero.MapIndex)
        {
            // 巡逻兵已经到了巡逻点，这一回合没必要再动。
            hero.SetModes(HeroModes.Sleeper);
            return;
        }

        if (!hero.MayStillMove(world, false, false))
        {
            return;
        }

        heroesToMove.Add(hero);
    }

    /// <summary>
    /// What the AI does when a hero starts moving, <c>Planner::HeroesBeginMovement()</c>
    /// (<c>ai_planner_hero.cpp:2909-2940</c>): the transparent <c>SUMMON_BOAT</c> cast.
    /// </summary>
    /// <remarks>
    /// 英雄正走向一格可以召唤船只的水面时，AI 替他把船悄悄召过来，玩家看不到这一步。召唤之后英雄走上船，
    /// 所以这一格必须是他的最后一步。
    /// </remarks>
    /// <param name="hero">The hero that starts moving.</param>
    public void HeroesBeginMovement(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        Debug.Assert(hero.IsActive, "Only a hero in the game moves.");

        World world = World.Current;
        HeroPath path = hero.Path;

        if (!path.IsValidForMovement(world.Size))
        {
            return;
        }

        int heroIndex = hero.MapIndex;
        MapGeometry geometry = world.Geometry!.Value;

        Direction frontDirection = path.FrontDirection;
        Debug.Assert(geometry.IsValidDirection(heroIndex, frontDirection), "The front step of a valid route leads somewhere.");

        int nextTileIndex = geometry.GetDirectionIndex(heroIndex, frontDirection);

        if (world.GetTile(heroIndex).IsWater || !world.GetTile(nextTileIndex).IsSuitableForSummoningBoat())
        {
            return;
        }

        // 船召在下一格，英雄走上去就下不来了，所以这必须是最后一步。
        Debug.Assert(path.Count == 1, "A hero that summons a boat moves onto it and stops.");

        int formerBoatIndex = AiHeroAction.HeroesCastSummonBoat(hero, nextTileIndex);

        GameKingdom kingdom = world.GetKingdom(hero.Color);

        UpdateMapActionObjectCache(kingdom, formerBoatIndex);
        UpdateMapActionObjectCache(kingdom, nextTileIndex);
    }

    /// <summary>
    /// What the AI does after its hero finished an action, <c>Planner::HeroesActionComplete()</c>
    /// (<c>ai_planner_hero.cpp:2942-3003</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 英雄进城时，AI 顺手把「魔法书 / 魔法行会 / 守军」三件事办完：买到书就学会城里所有法术，
    /// 再把能招的兵招了、把守军排好。
    /// </para>
    /// <para>
    /// 然后是「留在城里过夜」的判断：怪物速度每级值 100 点移动力，如果队伍里最慢的兵拖累了明天的行动力，
    /// 就把慢兵留在城里——留之前先试着搬一次，搬完还值不回行动力就把两边都还原。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that acted.</param>
    /// <param name="tileIndex">Tile the action happened on.</param>
    /// <param name="objectType">Object on that tile, <see cref="Mp2ObjectType.None"/> when there is none.</param>
    public void HeroesActionComplete(Hero hero, int tileIndex, Mp2ObjectType objectType)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;

        // 这个回调可能在英雄已经退场之后才到，所以先确认他还在。
        if (hero.IsActive)
        {
            CastleTown? castle = world.GetCastleEntrance(hero.MapIndex);

            if (castle is not null)
            {
                // 买书成功时会立刻学会城里所有法术；买不到（比如已经有书了）就只教法术。
                if (!hero.BuySpellBook(castle, world.GetKingdom(hero.Color)))
                {
                    castle.TrainHeroInMageGuild(hero);
                }

                ReinforceCastle(castle);

                // 队伍里最慢的兵和最快的兵差几级速度，就说明明天的行动力可能差多少。
                ArmyTroops heroArmy = hero.Army;
                TroopStack? slowestTroop = heroArmy.GetSlowestTroop();
                TroopStack? fastestTroop = heroArmy.GetFastestTroop();

                Debug.Assert(slowestTroop is not null && fastestTroop is not null, "A hero always has at least one stack.");

                if (slowestTroop is not null && fastestTroop is not null)
                {
                    uint slowestSpeed = (uint)slowestTroop.GetSpeed();
                    uint speedDifference = (uint)fastestTroop.GetSpeed() - slowestSpeed;

                    if (speedDifference * 100U >= hero.MovePoints)
                    {
                        // 把慢兵留在城里，明天也许能多走一截。
                        TroopStack[] castleBackup = [.. castle.Army];
                        TroopStack[] heroBackup = [.. heroArmy];

                        AiCommon.TransferSlowestTroopsToGarrison(hero, castle);

                        // 搬完再算一次，确认这份行动力真的换回来了。
                        slowestTroop = heroArmy.GetSlowestTroop();
                        Debug.Assert(slowestTroop is not null, "The fastest stack of the hero stays with him.");

                        if (slowestTroop is not null)
                        {
                            speedDifference = (uint)slowestTroop.GetSpeed() - slowestSpeed;

                            if (speedDifference * 100U >= hero.MovePoints)
                            {
                                hero.SetModes(HeroModes.Sleeper);
                            }
                            else
                            {
                                // 换不回来，两边都还原。
                                castle.Army.Assign(castleBackup);
                                heroArmy.Assign(heroBackup);
                            }
                        }
                    }
                }
            }
            else
            {
                AiCommon.OptimizeTroopsOrder(hero.Army);
            }
        }

        _tileArmyStrengthValues.Remove(tileIndex);

        UpdatePriorityTargets(hero, tileIndex, objectType);

        UpdateMapActionObjectCache(world.GetKingdom(hero.Color), tileIndex);
    }

    /// <summary>
    /// What the AI does every time its hero reaches a new tile, <c>Planner::HeroesActionNewPosition()</c>
    /// (<c>ai_planner_hero.cpp:3005-3080</c>): preparing the dig for the ultimate artifact and the transparent
    /// <c>SUMMON_BOAT</c> cast.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 走到埋着终极宝物的格子上、而且是专程走过来的（不是路过），就把英雄留到下一回合——挖宝要一整天。
    /// </para>
    /// <para>
    /// 剩下的情况都是「边走边召船」。路上经过有守卫的格子可能把法术点花光，那时只能停下来。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero that moved.</param>
    public void HeroesActionNewPosition(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (!hero.IsActive)
        {
            return;
        }

        World world = World.Current;
        HeroPath path = hero.Path;
        int heroIndex = hero.MapIndex;

        // 英雄是专程走到这一格的（走路或传送），不是路过。走过来的话这一步还没结算完。
        if (path.Count <= 1)
        {
            Debug.Assert(path.IsEmpty || path.FrontIndex == heroIndex, "A route of one step ends where the hero stands.");

            UltimateArtifact ultimateArtifact = world.UltimateArtifact;

            if (ultimateArtifact.IsAt(heroIndex) && AiCommon.IsUltimateArtifactAvailableToHero(ultimateArtifact, hero))
            {
                // 留住这位英雄，让他下回合挖。
                hero.SetModes(HeroModes.Sleeper);

                return;
            }
        }

        if (!path.IsValidForMovement(world.Size))
        {
            return;
        }

        Debug.Assert(path.FrontIndex == heroIndex, "A valid route starts where the hero stands.");

        MapGeometry geometry = world.Geometry!.Value;

        Direction nextStepDirection = path.NextStepDirection;
        if (!geometry.IsValidDirection(heroIndex, nextStepDirection))
        {
            return;
        }

        int nextTileIndex = geometry.GetDirectionIndex(heroIndex, nextStepDirection);

        if (world.GetTile(heroIndex).IsWater || !world.GetTile(nextTileIndex).IsSuitableForSummoningBoat())
        {
            return;
        }

        // 船召在下一格，英雄走上去就停在那里（当前这一步还没结算完，所以路上还剩两步）。
        Debug.Assert(path.Count == 2, "A hero that summons a boat moves onto it and stops.");

        // 出发时法术点还够，走到半路可能已经被守卫耗掉了。
        if (!hero.CanCastSpell(new Spell(SpellType.SummonBoat)))
        {
            path.Reset();
            hero.SetMove(false);

            return;
        }

        int formerBoatIndex = AiHeroAction.HeroesCastSummonBoat(hero, nextTileIndex);

        GameKingdom kingdom = world.GetKingdom(hero.Color);

        UpdateMapActionObjectCache(kingdom, formerBoatIndex);
        UpdateMapActionObjectCache(kingdom, nextTileIndex);
    }

    /// <summary>
    /// Gives the kingdom's heroes their tasks one at a time, <c>Planner::HeroesTurn()</c>
    /// (<c>ai_planner_hero.cpp:3094-3150</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每一轮从「还能动的英雄」里挑一个：先用最保守的寻路设定（大军优势、法术点留一半）试，找不到目标就一步步
    /// 放宽，直到「拼了」的设定。挑中的英雄身上放一个 <see cref="AIWorldPathfinderStateRestorer"/>，于是下一次
    /// 估分从干净的寻路器开始。
    /// </para>
    /// <para>
    /// 一个目标都找不到时，先看有没有英雄挡了别人要去的路——那种英雄要挪开。还找不到就结束回合。
    /// </para>
    /// <para>
    /// 进度条的两段数字只是给调用方的：端口没有状态栏，画进度是表现层的事。
    /// </para>
    /// </remarks>
    /// <param name="heroes">The kingdom's heroes, the ones that can still act first.</param>
    /// <param name="currentProgressValue">How far the turn has got, raised while the turn runs.</param>
    /// <param name="endProgressValue">What <paramref name="currentProgressValue"/> reaches at the end of the turn.</param>
    /// <param name="moreTasksAvailable">Set when a hero was given a task and other heroes may still have one.</param>
    /// <returns><see cref="AiTurnOutcome.EndTurn"/> when no hero is left with something to do, or the outcome of the
    /// action that ended the turn.</returns>
    private AiTurnOutcome HeroesTurn(IReadOnlyList<Hero> heroes, ref uint currentProgressValue, uint endProgressValue, ref bool moreTasksAvailable)
    {
        ArgumentNullException.ThrowIfNull(heroes);

        World world = World.Current;
        AIWorldPathfinder pathfinder = Pathfinder;

        // 默认英雄总还有事可做。
        moreTasksAvailable = true;

        if (heroes.Count == 0)
        {
            return AiTurnOutcome.EndTurn;
        }

        List<Hero> availableHeroes = new(heroes.Count);

        foreach (Hero hero in heroes)
        {
            Debug.Assert(hero is not null, "The kingdom's list of heroes has no holes.");

            AddHeroToMove(hero, availableHeroes, world);
        }

        uint heroesToMoveTotalCount = (uint)availableHeroes.Count;
        uint startProgressValue = currentProgressValue;

        if (endProgressValue - currentProgressValue >= heroesToMoveTotalCount * 2)
        {
            ++startProgressValue;
        }

        uint turnProgressScale = 4 * (endProgressValue - startProgressValue);

        // 每个英雄最多试这几组寻路设定：先保守，再一步步放宽。
        (double MinimalAdvantage, double SpellPointsReserveRatio)[] commonConfigurations =
        [
            (AiPlannerInternals.ArmyAdvantageLarge, 0.5),
            (AiPlannerInternals.ArmyAdvantageMedium, 0.25),
            (AiPlannerInternals.ArmyAdvantageSmall, 0.0),
        ];

        // 快输了的时候，敌人不会替我们保守估计，我们也不留后手。
        (double MinimalAdvantage, double SpellPointsReserveRatio)[] emergencyConfigurations =
        [
            (AiPlannerInternals.ArmyAdvantageDesperate, 0.0),
        ];

        while (availableHeroes.Count > 0)
        {
            // 这一轮结束后，寻路器要回到干净的设定。
            using AIWorldPathfinderStateRestorer pathfinderStateRestorer = new(pathfinder);

            Hero bestHero = availableHeroes[0];
            int bestTargetIndex = -1;

            {
                bool isLosingGame = world.GetKingdom(bestHero.Color).IsLosingGame;

                foreach ((double minimalAdvantage, double spellPointsReserveRatio) in isLosingGame ? emergencyConfigurations : commonConfigurations)
                {
                    pathfinder.SetMinimalArmyStrengthAdvantage(minimalAdvantage);
                    pathfinder.SetSpellPointsReserveRatio(spellPointsReserveRatio);

                    double maxPriority = 0;

                    foreach (Hero hero in availableHeroes)
                    {
                        int targetIndex = GetPriorityTarget(hero, out double priority);

                        if (targetIndex != -1 && (priority > maxPriority || bestTargetIndex == -1))
                        {
                            maxPriority = priority;
                            bestTargetIndex = targetIndex;
                            bestHero = hero;
                        }
                    }

                    if (bestTargetIndex != -1)
                    {
                        break;
                    }
                }
            }

            currentProgressValue = Math.Max(currentProgressValue,
                ((turnProgressScale * (heroesToMoveTotalCount - (uint)availableHeroes.Count)) + (3 * heroesToMoveTotalCount))
                / (4 * heroesToMoveTotalCount)
                + startProgressValue);

            // 没有目标就退而求其次：看看是不是有英雄挡住了别人的路。
            if (bestTargetIndex == -1)
            {
                Rand.Shuffle(availableHeroes);

                foreach (Hero hero in availableHeroes)
                {
                    if (IsHeroInCastle(hero) || !pathfinder.IsHeroPossiblyBlockingWay(hero))
                    {
                        continue;
                    }

                    if (hero.Modes.HasFlag(HeroModes.Patrol) && hero.PatrolCenterIndex == hero.MapIndex)
                    {
                        // 巡逻兵不动，别去挪他。
                        continue;
                    }

                    int targetIndex = pathfinder.GetNearestTileToMove(hero);

                    if (targetIndex != -1)
                    {
                        bestTargetIndex = targetIndex;
                        bestHero = hero;

                        break;
                    }
                }

                if (bestTargetIndex == -1)
                {
                    break;
                }
            }

            int heroesBefore = heroes.Count;

            pathfinder.ReEvaluateIfNeeded(bestHero);

            int previousHeroPosition = bestHero.MapIndex;
            GameKingdom kingdom = world.GetKingdom(bestHero.Color);

            List<RouteStep> dimensionDoorPath = pathfinder.BuildDimensionDoorPath(bestHero, bestTargetIndex);
            uint regularMovementDist = pathfinder.GetDistance(bestTargetIndex);
            uint dimensionDoorDist = HeroPath.CalculatePenalty(dimensionDoorPath);

            if (ShouldUseDimensionDoor(regularMovementDist, dimensionDoorDist))
            {
                while (ShouldUseDimensionDoor(regularMovementDist, dimensionDoorDist))
                {
                    Debug.Assert(dimensionDoorPath.Count > 0 && bestHero.MayStillMove(world, false, false),
                        "A dimension door step is only taken when the hero can still move.");

                    AiHeroAction.HeroesCastDimensionDoor(bestHero, dimensionDoorPath[0].Index);

                    dimensionDoorDist -= dimensionDoorPath[0].Penalty;

                    pathfinder.ReEvaluateIfNeeded(bestHero);
                    regularMovementDist = pathfinder.GetDistance(bestTargetIndex);

                    dimensionDoorPath.RemoveAt(0);

                    Debug.Assert(bestHero.IsActive && bestHero.MapIndex != previousHeroPosition,
                        "A dimension door step moves the hero.");

                    UpdateMapActionObjectCache(kingdom, previousHeroPosition);
                    UpdateMapActionObjectCache(kingdom, bestHero.MapIndex);

                    previousHeroPosition = bestHero.MapIndex;
                }

                if (dimensionDoorDist > 0)
                {
                    // 次元门跳不到目标旁边，最后一段还是得走过去。
                    bestHero.Path.SetPath(pathfinder.BuildPath(bestTargetIndex, true));

                    AiTurnOutcome gameState = AiHeroAction.HeroesMove(bestHero);

                    if (gameState != AiTurnOutcome.EndTurn)
                    {
                        moreTasksAvailable = false;
                        return gameState;
                    }
                }
            }
            else
            {
                bestHero.Path.SetPath(pathfinder.BuildPath(bestTargetIndex, true));

                AiTurnOutcome gameState = AiHeroAction.HeroesMove(bestHero);

                if (gameState != AiTurnOutcome.EndTurn)
                {
                    moreTasksAvailable = false;
                    return gameState;
                }
            }

            if (!bestHero.IsActive || bestHero.MapIndex != previousHeroPosition)
            {
                UpdateMapActionObjectCache(kingdom, previousHeroPosition);

                if (bestHero.IsActive)
                {
                    UpdateMapActionObjectCache(kingdom, bestHero.MapIndex);
                }
            }

            // 监牢里的英雄刚刚入伙，这一回合也算他一个。
            if (heroes.Count > heroesBefore)
            {
                int availableCountBefore = availableHeroes.Count;

                AddHeroToMove(heroes[^1], availableHeroes, world);

                if (availableCountBefore < availableHeroes.Count)
                {
                    ++heroesToMoveTotalCount;

                    // 进度条的刻度也跟着涨：四个人一组，再加一组。
                    if (endProgressValue < 8)
                    {
                        ++endProgressValue;
                        turnProgressScale += 4;
                    }
                }
            }

            // 这一回合再也走不动的英雄就别再排了。
            availableHeroes.RemoveAll(hero => !hero.MayStillMove(world, false, false));

            currentProgressValue = Math.Max(currentProgressValue,
                ((turnProgressScale * (heroesToMoveTotalCount - (uint)availableHeroes.Count)) + heroesToMoveTotalCount)
                / (4 * heroesToMoveTotalCount)
                + startProgressValue);
        }

        moreTasksAvailable = availableHeroes.Count == 0;

        return AiTurnOutcome.EndTurn;
    }

    // -----------------------------------------------------------------------------------------------------------------
    // The caches of a single decision (ai_planner_hero.cpp:846-938): the two questions "may this hero look at this
    // tile" and "what is it worth" are asked about the same object several times per hero, and the answers can only
    // change when the hero moves, so they live as long as one getPriorityTarget() call.
    // -----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Caches the "may this hero look at this tile" answer, <c>ObjectValidator</c> (<c>ai_planner_hero.cpp:846-898</c>).
    /// </summary>
    /// <remarks>
    /// 两个数字在构造时就算好：英雄的兵力将来会被反复用来判断「这堆兵值不值得捡」，
    /// 而一次决策之内英雄不会动，所以兵力与门槛都不会变。
    /// </remarks>
    private sealed class ObjectValidator
    {
        private readonly AiPlanner _planner;
        private readonly Hero _hero;
        private readonly AIWorldPathfinder _pathfinder;
        private readonly double _heroArmyStrength;
        private readonly double _armyStrengthThreshold;

        private readonly Dictionary<int, bool> _currentValidObjects = [];
        private readonly Dictionary<int, int> _futureValidObjects = [];

        /// <summary>Prepares the cache for one hero.</summary>
        /// <param name="planner">The planner the validation rules live on.</param>
        /// <param name="hero">The hero whose point of view is used.</param>
        /// <param name="pathfinder">The pathfinder, already re-evaluated for <paramref name="hero"/>.</param>
        public ObjectValidator(AiPlanner planner, Hero hero, AIWorldPathfinder pathfinder)
        {
            _planner = planner;
            _hero = hero;
            _pathfinder = pathfinder;
            _heroArmyStrength = hero.Army.GetStrength();
            _armyStrengthThreshold = hero.GetAiMinimumJoiningArmyStrength();
        }

        /// <summary>Whether the object can be visited right now, <c>isCurrentlyValid()</c>.</summary>
        /// <param name="index">Tile index of the object.</param>
        /// <returns>The cached answer.</returns>
        public bool IsCurrentlyValid(int index)
        {
            if (_currentValidObjects.TryGetValue(index, out bool cached))
            {
                return cached;
            }

            bool valid = _planner.IsValidObjectForHero(_hero, _heroArmyStrength, index, _pathfinder, _armyStrengthThreshold, false);
            _currentValidObjects[index] = valid;

            return valid;
        }

        /// <summary>In how many days the object becomes worth visiting, <c>whenGoingToBeValidInDays()</c>.</summary>
        /// <param name="index">Tile index of the object.</param>
        /// <returns>The cached number of days, <c>0</c> when it never does.</returns>
        public int WhenGoingToBeValidInDays(int index)
        {
            if (_futureValidObjects.TryGetValue(index, out int cached))
            {
                return cached;
            }

            int days = GetDaysWhenObjectWillBeValid(index, false);
            _futureValidObjects[index] = days;

            return days;
        }
    }

    /// <summary>
    /// Caches the value of an object for one hero, <c>ObjectValueStorage</c> (<c>ai_planner_hero.cpp:901-938</c>).
    /// </summary>
    /// <remarks>
    /// 参考用 <see cref="IndexObject"/> 做键，因为同一个格子先后可能摆着不同东西；端口照样用这个键，
    /// 于是「宝箱换成了金币」不会被当成同一个对象。
    /// </remarks>
    private sealed class ObjectValueStorage
    {
        private readonly AiPlanner _planner;
        private readonly Hero _hero;
        private readonly double _ignoreValue;

        private readonly Dictionary<IndexObject, double> _objectValue = [];
        private readonly Dictionary<IndexObject, double> _futureObjectValue = [];

        /// <summary>Prepares the cache for one hero.</summary>
        /// <param name="planner">The planner whose value tables are used.</param>
        /// <param name="hero">The hero whose point of view is used.</param>
        /// <param name="ignoreValue">What "not worth a detour" is worth.</param>
        public ObjectValueStorage(AiPlanner planner, Hero hero, double ignoreValue)
        {
            _planner = planner;
            _hero = hero;
            _ignoreValue = ignoreValue;
        }

        /// <summary>The value of the object as it is now, <c>value()</c>.</summary>
        /// <param name="index">Tile index of the object.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="distance">Distance the hero has to travel.</param>
        /// <returns>The cached value.</returns>
        public double Value(int index, Mp2ObjectType objectType, uint distance)
        {
            IndexObject key = new(index, objectType);

            if (_objectValue.TryGetValue(key, out double cached))
            {
                return cached;
            }

            double value = _planner.GetObjectValue(_hero, index, objectType, _ignoreValue, distance);
            _objectValue[key] = value;

            return value;
        }

        /// <summary>The value of the object once it refills, <c>futureValue()</c>.</summary>
        /// <param name="index">Tile index of the object.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="distance">Distance the hero has to travel.</param>
        /// <returns>The cached value.</returns>
        public double FutureValue(int index, Mp2ObjectType objectType, uint distance)
        {
            IndexObject key = new(index, objectType);

            if (_futureObjectValue.TryGetValue(key, out double cached))
            {
                return cached;
            }

            double value = _planner.GetFutureObjectValue(_hero, index, objectType, _ignoreValue, distance);
            _futureObjectValue[key] = value;

            return value;
        }
    }

    /// <summary>
    /// In how many days a tile becomes worth visiting, <c>getDaysWhenObjectWillBeValid()</c>
    /// (<c>ai_planner_hero.cpp:791-831</c>).
    /// </summary>
    /// <remarks>
    /// 只有每周刷新的那些对象会「过几天又能拿一次」，它们的等待时间就是离下周一还有几天。
    /// 其余对象要么现在就能用，要么永远不能用，所以是 0。
    /// </remarks>
    /// <param name="index">The tile to ask about.</param>
    /// <param name="underHero">Whether the object under the hero should be considered.</param>
    /// <returns>The number of days, <c>0</c> for objects that never come back.</returns>
    private static int GetDaysWhenObjectWillBeValid(int index, bool underHero)
    {
        World world = World.Current;
        Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(world.GetTile(index).GetMainObjectType(world, !underHero));

        switch (objectType)
        {
        case Mp2ObjectType.AirAltar:
        case Mp2ObjectType.ArcherHouse:
        case Mp2ObjectType.ArtesianSpring:
        case Mp2ObjectType.BarrowMounds:
        case Mp2ObjectType.Cave:
        case Mp2ObjectType.CityOfDead:
        case Mp2ObjectType.DesertTent:
        case Mp2ObjectType.DragonCity:
        case Mp2ObjectType.DwarfCottage:
        case Mp2ObjectType.EarthAltar:
        case Mp2ObjectType.Excavation:
        case Mp2ObjectType.FireAltar:
        case Mp2ObjectType.GoblinHut:
        case Mp2ObjectType.HalflingHole:
        case Mp2ObjectType.MagicGarden:
        case Mp2ObjectType.PeasantHut:
        case Mp2ObjectType.Ruins:
        case Mp2ObjectType.Stables:
        case Mp2ObjectType.TreeCity:
        case Mp2ObjectType.TreeHouse:
        case Mp2ObjectType.TrollBridge:
        case Mp2ObjectType.WagonCamp:
        case Mp2ObjectType.WatchTower:
        case Mp2ObjectType.WaterAltar:
        case Mp2ObjectType.WaterWheel:
        case Mp2ObjectType.Windmill:
            Debug.Assert(Mp2ObjectTypes.IsWeekLife(objectType));

            return DaysToNextWeek();

        default:
            break;
        }

        return 0;
    }

    /// <summary>
    /// Whether an object is worth visiting for a hero, <c>Planner::isValidHeroObject()</c>
    /// (<c>ai_planner_hero.cpp:3082-3086</c>).
    /// </summary>
    /// <param name="hero">The hero that would visit it.</param>
    /// <param name="index">Tile index of the object.</param>
    /// <param name="underHero">Whether the object is under the hero.</param>
    /// <returns><see langword="true"/> when the object is a valid target.</returns>
    public bool IsValidHeroObject(Hero hero, int index, bool underHero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        // 阈值来自英雄自己的兵力：太弱的兵堆不值得为一支大军绕路。
        return IsValidObjectForHero(hero, hero.Army.GetStrength(), index, Pathfinder, hero.GetAiMinimumJoiningArmyStrength(), underHero);
    }

    /// <summary>
    /// The task a courier hero should take, <c>Planner::getCourierMainTarget()</c>
    /// (<c>ai_planner_hero.cpp:2313-2400</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 信使不抢地盘，他的活是「把兵送到能打的英雄手里」和「补齐空城」。所以先看会面：只有双方兵力都值得交换
    /// （<see cref="Hero.GetMeetingValue"/> 到 500 分以上）才跑，冠军英雄手里的兵最要紧，分数乘 2.5。
    /// </para>
    /// <para>
    /// 没有会面可谈才看自家的空城。信使天生就该跑远路，所以这里只减距离、不再按距离打折。
    /// </para>
    /// <para>
    /// 参考假定路径库已经按本方英雄重算过（<c>getPriorityTarget()</c> 就在它前面算好了），端口也是。
    /// </para>
    /// </remarks>
    /// <param name="hero">The courier hero.</param>
    /// <param name="lowestPossibleValue">Below this value nothing is worth a trip.</param>
    /// <returns>Tile index of the target, <c>-1</c> when there is none.</returns>
    private int GetCourierMainTarget(Hero hero, double lowestPossibleValue)
    {
        Debug.Assert(hero.AiRole == HeroRole.Courier);

        World world = World.Current;
        AIWorldPathfinder pathfinder = Pathfinder;
        GameKingdom kingdom = world.GetKingdom(hero.Color);

        int targetIndex = -1;
        double bestTargetValue = lowestPossibleValue;

        // 手上的兵先送给能打的英雄，信使自己打不了几仗。
        foreach (Hero otherHero in kingdom.Heroes)
        {
            if (otherHero is null || hero.Id == otherHero.Id)
            {
                continue;
            }

            if (otherHero.AiRole is HeroRole.Courier or HeroRole.Scout)
            {
                continue;
            }

            int otherHeroIndex = otherHero.MapIndex;

            (uint Distance, bool _) = GetDistanceToTile(pathfinder, hero, otherHeroIndex);
            if (Distance == 0 || hero.HasMetWithHero(otherHero.Id))
            {
                continue;
            }

            double value = hero.GetMeetingValue(otherHero);
            if (value < 500)
            {
                continue;
            }

            if (otherHero.AiRole == HeroRole.Champion)
            {
                value *= 2.5;
            }

            value -= Distance;

            if (value > bestTargetValue)
            {
                bestTargetValue = value;
                targetIndex = otherHeroIndex;
            }
        }

        if (targetIndex != -1)
        {
            return targetIndex;
        }

        bestTargetValue = lowestPossibleValue;

        // 没有会面可谈，就去补齐自家的空城。
        foreach (CastleTown castle in kingdom.Castles)
        {
            if (AiCommon.FindGuestHero(world, castle) is not null)
            {
                continue;
            }

            int castleIndex = castle.TileIndex;

            (uint Distance, bool _) = GetDistanceToTile(pathfinder, hero, castleIndex);
            if (Distance == 0)
            {
                continue;
            }

            double value = castle.GetVisitValue(hero, kingdom.Treasury);
            if (value < 250)
            {
                continue;
            }

            int safetyFactor = _regions[(int)world.GetTile(castleIndex).Region].SafetyFactor;

            if (safetyFactor > 100)
            {
                value *= 2;
            }
            else if (safetyFactor < 0)
            {
                value /= 2;
            }

            // 信使本来就该跑远路，不再按距离额外打折。
            value -= Distance;

            if (value > bestTargetValue)
            {
                bestTargetValue = value;
                targetIndex = castleIndex;
            }
        }

        return targetIndex;
    }

    /// <summary>
    /// Picks the most valuable task for a hero, <c>Planner::getPriorityTarget()</c>
    /// (<c>ai_planner_hero.cpp:2402-2754</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是 AI 每回合真正的「拍板」：把地图上记得的对象一个个估分，扣掉路上和敌人的风险，再除以距离，
    /// 取最高的那个。分数已经扣过距离，所以并列时近的那个赢。
    /// </para>
    /// <para>
    /// 还有三条候选不进 <c>_mapActionObjects</c>：埋在土里的终极宝物、地图上还剩的雾、以及巡逻兵的巡逻点。
    /// 前两个要单独算（它们不是「记得的动作对象」），最后一个只在什么也没找到时才用。
    /// </para>
    /// <para>
    /// 允许「预测未来」的难度下，每周刷新的对象即使现在没用也会被估一次「下周值多少」，
    /// 但只有英雄能在它刷新之前赶到才算分。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero to give a task to.</param>
    /// <param name="maxPriority">Receives the priority of the task that was picked.</param>
    /// <returns>Tile index of the target, <c>-1</c> when nothing is worth doing.</returns>
    private int GetPriorityTarget(Hero hero, out double maxPriority)
    {
        ArgumentNullException.ThrowIfNull(hero);

        World world = World.Current;
        AIWorldPathfinder pathfinder = Pathfinder;

        // 「什么都不做」的底分：用最慢的地形走满整张地图也扣不到这么低。
        double lowestPossibleValue = -1.0 * MapGrounds.SlowestMovePenalty * world.Size;

        int priorityTarget = -1;
        maxPriority = lowestPossibleValue;

        // 先把「敌人一回合能打到哪」这张表算出来，后面每估一个目标都要扣它。
        double[] enemyThreatPenalties = GetEnemyThreatPenalties(hero);

        // 路径库要按本方英雄重算，后面的距离全靠它。
        pathfinder.ReEvaluateIfNeeded(hero);

        ObjectValidator objectValidator = new(this, hero, pathfinder);
        ObjectValueStorage valueStorage = new(this, hero, lowestPossibleValue);

        bool isFutureObjectPredictionAllowed = Difficulty.IsFutureObjectPredictionAllowedForAi(Settings.Get().GameDifficulty);

        // 参考的 getObjectValue 闭包：扣掉敌人威胁、把超过一回合的远距离打折，最后按距离标准化。
        void ConsiderObject(int destination, ref uint distance, ref double value, Mp2ObjectType objectType, bool isDimensionDoor)
        {
            // 次元门是跳过去的，路上拿不到任何东西。
            if (!isDimensionDoor)
            {
                foreach ((int Index, Mp2ObjectType ObjectType) onTheWay in pathfinder.GetObjectsOnTheWay(destination))
                {
                    int onTheWayIndex = onTheWay.Index;

                    bool isValidObject = objectValidator.IsCurrentlyValid(onTheWayIndex);
                    int dayToBecomeValid = isFutureObjectPredictionAllowed ? objectValidator.WhenGoingToBeValidInDays(onTheWayIndex) : 0;

                    if (!isValidObject && dayToBecomeValid < 1)
                    {
                        // 现在没用，将来也不会变得有用。
                        continue;
                    }

                    // 路上记着的对象必须和地图上现在的类型一致，否则那段记忆已经过期了。
                    if (!_mapActionObjects.TryGetValue(onTheWayIndex, out Mp2ObjectType knownType) || knownType != onTheWay.ObjectType)
                    {
                        continue;
                    }

                    // 路径记的是瓦片上的原值，估价按不带位的那套算，和 GetPriorityTarget 里折的理由一样。
                    Mp2ObjectType onTheWayType = Mp2ObjectTypes.NonActionType(onTheWay.ObjectType);

                    double extraValue = 0;

                    if (isValidObject)
                    {
                        extraValue = valueStorage.Value(onTheWayIndex, onTheWayType, 0);
                    }
                    else
                    {
                        Debug.Assert(isFutureObjectPredictionAllowed);

                        List<RouteStep> path = pathfinder.BuildPath(onTheWayIndex, false);
                        Debug.Assert(path.Count > 0);
                        Debug.Assert(path[^1].Index == onTheWayIndex);

                        int daysToReachObject = CompletedDaysToTarget(path, hero);
                        if (daysToReachObject < dayToBecomeValid)
                        {
                            extraValue = valueStorage.FutureValue(onTheWayIndex, onTheWayType, 0);
                        }
                    }

                    if (extraValue > 0)
                    {
                        // 顺路的不折价：它不占额外的路程。
                        value += extraValue;
                    }
                }
            }

            uint heroMovePoints = hero.MovePoints;

            // 自家城堡一回合能到就不用怕敌人：他进城就有援军，值得去守。
            double enemyThreatPenalty = 0;

            if (objectType == Mp2ObjectType.Castle)
            {
                CastleTown? castle = world.GetCastleEntrance(destination);
                Debug.Assert(castle is not null);

                if (castle is not null && castle.Color == hero.Color && distance <= heroMovePoints)
                {
                    enemyThreatPenalty = 0;
                }
                else
                {
                    enemyThreatPenalty = enemyThreatPenalties[destination];
                }
            }
            else
            {
                enemyThreatPenalty = enemyThreatPenalties[destination];
            }

            value -= enemyThreatPenalty;

            // 这一回合到不了的目标要降级：走来回路上的时间也值钱。
            if (distance > heroMovePoints)
            {
                distance = heroMovePoints + ((distance - heroMovePoints) * 2);
            }

            value = ScaleWithDistanceAndTime(value, distance, objectType);
        }

        // 信使有固定主线，先把它摆上桌，其他目标只要正分就能改写它。
        if (hero.AiRole == HeroRole.Courier)
        {
            int courierTarget = GetCourierMainTarget(hero, lowestPossibleValue);

            if (courierTarget != -1)
            {
                priorityTarget = courierTarget;
                maxPriority = 0;
            }
            else
            {
                // 当信使已经没事可做了，改回猎手。
                hero.AiRole = HeroRole.Hunter;
            }
        }

        foreach ((int index, Mp2ObjectType storedType) in _mapActionObjects)
        {
            // 缓存里存的是瓦片上的原值（带动作位），后面的估价、折算和「这是不是自己的城堡」全按不带位的那套
            // 比（ADR-133），所以在这里折一次。
            Mp2ObjectType objectType = Mp2ObjectTypes.NonActionType(storedType);

            bool isCurrentlyValid = objectValidator.IsCurrentlyValid(index);
            int daysToBeAvailable = isFutureObjectPredictionAllowed ? objectValidator.WhenGoingToBeValidInDays(index) : 0;

            if (!isCurrentlyValid && daysToBeAvailable < 1)
            {
                // 现在没用，将来也不会变得有用。
                continue;
            }

            (uint dist, bool useDimensionDoor) = GetDistanceToTile(pathfinder, hero, index);
            if (dist == 0)
            {
                continue;
            }

            if (!isCurrentlyValid && useDimensionDoor)
            {
                // 次元门的路线上没有对象，暂时估不出「将来会值多少」。
                continue;
            }

            if (!isCurrentlyValid)
            {
                List<RouteStep> path = pathfinder.BuildPath(index, false);
                Debug.Assert(path.Count > 0);
                Debug.Assert(path[^1].Index == index);

                int daysToReachObject = CompletedDaysToTarget(path, hero);
                if (daysToReachObject < daysToBeAvailable)
                {
                    // 赶不上它刷新，别白跑。
                    continue;
                }
            }

            double value = isCurrentlyValid
                               ? valueStorage.Value(index, objectType, dist)
                               : valueStorage.FutureValue(index, objectType, dist);

            ConsiderObject(index, ref dist, ref value, objectType, useDimensionDoor);

            if (dist > 0 && value > maxPriority)
            {
                priorityTarget = index;
                maxPriority = value;
            }
        }

        UltimateArtifact ultimateArtifact = world.UltimateArtifact;

        if (AiCommon.IsUltimateArtifactAvailableToHero(ultimateArtifact, hero))
        {
            int artifactIndex = ultimateArtifact.Position;

            // 这个格子上要是有动作对象（比如英雄），它已经在上面算过了。
            if (!_mapActionObjects.ContainsKey(artifactIndex))
            {
                (uint dist, bool useDimensionDoor) = GetDistanceToTile(pathfinder, hero, artifactIndex);

                if (dist > 0)
                {
                    double value = (IsFindUltimateArtifactVictoryCondition() ? 3000.0 : 1500.0)
                                   * ArtifactValues.Value(ultimateArtifact.Artifact);

                    ConsiderObject(artifactIndex, ref dist, ref value, Mp2ObjectType.Artifact, useDimensionDoor);

                    if (dist > 0 && (priorityTarget == -1 || value > maxPriority))
                    {
                        priorityTarget = artifactIndex;
                        maxPriority = value;
                    }
                }
            }
        }

        {
            (int fogIndex, bool isTerritoryExpansion) = pathfinder.GetFogDiscoveryTile(hero);

            if (world.Geometry?.IsValidAbsIndex(fogIndex) == true)
            {
                (uint dist, bool useDimensionDoor) = GetDistanceToTile(pathfinder, hero, fogIndex);
                Debug.Assert(dist > 0);

                double value = GetFogDiscoveryValue(hero);

                if (isTerritoryExpansion)
                {
                    // 日子越往后，AI 越该往外扩地盘。
                    uint period = GetTimeoutBeforeFogDiscoveryIntensification(hero);
                    Debug.Assert(period > 0);

                    if (value < 0)
                    {
                        // 这其实是个很值的揭雾动作，可能揭出新对象来。
                        if (world.DayCount > period)
                        {
                            value = 0;
                        }
                        else
                        {
                            value = value / 2 * (period - world.DayCount) / period;
                        }
                    }
                    else
                    {
                        // 侦察兵要在一个月内把注意力转到扩张上，所以奖励随时间线性涨到 1000。
                        value += Math.Min(1000.0 * world.DayCount / period, 1000.0);
                    }
                }

                ConsiderObject(fogIndex, ref dist, ref value, Mp2ObjectType.None, useDimensionDoor);

                if (dist > 0 && (priorityTarget == -1 || value > maxPriority))
                {
                    priorityTarget = fogIndex;
                    maxPriority = value;
                }
            }
        }

        if (priorityTarget == -1)
        {
            // 巡逻兵没事可做就回巡逻点站着，别在外面乱逛。
            if (hero.Modes.HasFlag(HeroModes.Patrol) && hero.PatrolCenterIndex != hero.MapIndex)
            {
                int patrolIndex = hero.PatrolCenterIndex;

                (uint dist, bool _) = GetDistanceToTile(pathfinder, hero, patrolIndex);

                if (dist == 0 || Mp2ObjectTypes.IsActionObject(world.GetTile(patrolIndex).MainObjectType))
                {
                    // 巡逻点回不去，也没别的活可干。
                    return -1;
                }

                priorityTarget = patrolIndex;

                // 让这个任务的分数刚好高过下限。
                maxPriority = -DangerousTaskPenalty + 1;
            }
        }

        return priorityTarget;
    }

    /// <summary>
    /// How much every tile loses because enemy heroes could reach it, <c>getPriorityTarget()</c>'s first lambda
    /// (<c>ai_planner_hero.cpp:2429-2507</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只有比本方英雄还强的敌人才算威胁，而且按「敌人一回合能走到的范围再加两格」划界：圈外的格子一分不扣，
    /// 圈内的格子按距离线性加罚，贴身的格子正好扣两倍 <see cref="DangerousTaskPenalty"/>。
    /// </para>
    /// <para>
    /// 敌人也按最乐观的设定走（最小兵力优势、法术点一点不留），因为对手不会替我们保守估计。
    /// </para>
    /// </remarks>
    /// <param name="hero">The hero whose move is being planned.</param>
    /// <returns>One penalty per tile of the map.</returns>
    private double[] GetEnemyThreatPenalties(Hero hero)
    {
        World world = World.Current;
        AIWorldPathfinder pathfinder = Pathfinder;
        MapGeometry geometry = world.Geometry!.Value;

        double[] result = new double[world.Size];

        using AIWorldPathfinderStateRestorer pathfinderStateRestorer = new(pathfinder);

        pathfinder.SetMinimalArmyStrengthAdvantage(AiPlannerInternals.ArmyAdvantageDesperate);
        pathfinder.SetSpellPointsReserveRatio(0.0);

        double heroStrength = hero.Army.GetStrength();

        foreach (EnemyArmy enemyArmy in _enemyArmies.Values)
        {
            // 中立兵不会走动，只有敌方英雄才谈得上威胁。
            if (enemyArmy.Hero is null)
            {
                continue;
            }

            // 和我们差不多强、或者更弱的敌人不构成威胁。
            if (heroStrength * AiPlannerInternals.ArmyAdvantageSmall >= enemyArmy.Strength)
            {
                continue;
            }

            if (enemyArmy.MovePoints == 0)
            {
                Debug.Fail("An enemy army that has already moved cannot threaten anything.");
                continue;
            }

            // 安全的格子不能贴着敌人一回合的移动范围，留两格余量。
            uint movePointsThreshold = enemyArmy.MovePoints + (MapGrounds.SlowestMovePenalty * 2);

            // 两边一回合内不可能碰面，就用粗估的快算法；能碰面才花时间算准的。
            bool useRoughEstimate = (geometry.GetApproximateDistance(hero.MapIndex, enemyArmy.Index) * MapGrounds.FastestMovePenalty)
                                    > hero.MovePoints + movePointsThreshold;

            if (!useRoughEstimate)
            {
                // 先把敌人的路径库算好，后面每个格子都要问它。
                pathfinder.ReEvaluateIfNeeded(enemyArmy.Hero);
            }

            for (int tileIndex = 0; tileIndex < result.Length; ++tileIndex)
            {
                uint distToTile;
                bool isTileConsideredSafe;

                if (tileIndex == enemyArmy.Index)
                {
                    // 敌人自己站的格子永远不安全。
                    distToTile = 0;
                    isTileConsideredSafe = false;
                }
                else if (useRoughEstimate)
                {
                    distToTile = geometry.GetApproximateDistance(tileIndex, enemyArmy.Index) * MapGrounds.FastestMovePenalty;

                    // 粗估时不考虑敌人的传送法术，一回合走不到就算安全。
                    isTileConsideredSafe = distToTile > movePointsThreshold;
                }
                else
                {
                    distToTile = pathfinder.GetDistance(tileIndex);

                    // 走不到（比如还在雾里）或者一回合到不了，就算安全。
                    isTileConsideredSafe = distToTile == 0 || distToTile > movePointsThreshold;
                }

                if (isTileConsideredSafe)
                {
                    continue;
                }

                // 威胁是累加的：几个敌人都能打到同一格时，罚分相加。
                result[tileIndex] += DangerousTaskPenalty * (2.0 - ((double)distToTile / movePointsThreshold));
            }
        }

        return result;
    }
}
