using System.Diagnostics;

using FHeroes2.Assets.Maps;

using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Ai;

/// <summary>
/// The per-object half of <see cref="AiHeroAction"/>: what the AI does on each kind of tile,
/// <c>ai_hero_action.cpp:404-1976</c>。
/// </summary>
/// <remarks>
/// <para>
/// 参考把每个对象写成一支 <c>AITo*</c> 函数；这里同构。凡是「AI 的决定与玩家随手点『好』完全一样」的对象，
/// 就直接把玩家侧的 <see cref="HeroActions"/> 拿来用——它带的是 <see cref="NullHeroActionHost"/>，
/// 不会弹窗、不会有提示，只留下模型改动。凡是要打架、招兵、付钱、挑技能的地方，才在
/// <c>AiHeroAction.Object.Ai.cs</c> 里写 AI 自己的决定。
/// </para>
/// <para>
/// 与玩家侧的差异一律在方法注释里标出（例如 AI 从不开城堡屏、从不在战斗前集结守军）。
/// </para>
/// </remarks>
public static partial class AiHeroAction
{
    /// <summary>
    /// A shrine, <c>AIToShrine()</c> (<c>ai_hero_action.cpp:1185-1205</c>).
    /// </summary>
    /// <remarks>神龛必须先把自己的访问登记同步给盟友，规划器才不会让盟友白跑一趟。</remarks>
    private static void AiToShrine(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToShrine(tileIndex, world);

        AiCommon.ShareObjectVisitInfoWithAllies(world.GetKingdom(hero.Color), tileIndex);
    }

    /// <summary>
    /// A witch's hut, <c>AIToWitchsHut()</c> (<c>ai_hero_action.cpp:1158-1184</c>).
    /// </summary>
    private static void AiToWitchsHut(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToWitchsHut(tileIndex, world);

        AiCommon.ShareObjectVisitInfoWithAllies(world.GetKingdom(hero.Color), tileIndex);
    }

    /// <summary>
    /// An obelisk, <c>AIToObelisk()</c> (<c>ai_hero_action.cpp:1478-1491</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 AI 版只在<b>没看过</b>时才登记王国名册并更新拼图（<c>Kingdom::PuzzleMaps().Update()</c>），
    /// 这两步都已经在玩家侧的 <see cref="HeroActions.ActionToObelisk"/> 里，直接转调即可。
    /// </remarks>
    private static void AiToObelisk(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToObelisk(tileIndex, world);
    }

    /// <summary>
    /// A loose resource, a campfire, a barrel or a bottle, <c>AIToPickupResource()</c>
    /// (<c>ai_hero_action.cpp:698-711</c>).
    /// </summary>
    /// <remarks>
    /// 不能直接转调 <see cref="HeroActions.ActionToPickupResource"/>：那里断言的「散落资源/营火/桶」三种不含漂流瓶，
    /// 而参考的第四支正是漂流瓶——它不付钱（付钱的是留言），但同样清元数据、把物件从地图上抹掉。
    /// 参考在这里另加一句 <c>GetPath().Reset()</c>，端口统一在动作收尾处做。
    /// </remarks>
    private static void AiToPickupResource(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        Tile tile = world.GetTile(tileIndex);

        if (tile.GetNonActionObjectType(world) != Mp2ObjectType.Bottle)
        {
            actions.ActionToPickupResource(tileIndex, world);
        }

        tile.ResetObjectMetadata();

        // 参考是 removeMainObjectFromTile()：按「脚下那个对象的类型」找 UID，再顺着 UID 把整件物件抹掉，
        // 顺带重算物件类型与通行性。只按 ICN 摘部件会留下一格「看起来还有、其实已经空了」的物件。
        tile.RemoveMainObject(world);
    }

    /// <summary>
    /// A hill fort or Freeman's foundry, <c>AIToUpgradeArmyObject()</c> (<c>ai_hero_action.cpp:1334-1368</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 AI 版是<b>免费</b>升级：只按对象挑三种兵升级，既不查国库也不扣钱。
    /// 玩家侧的 <see cref="HeroActions.ActionToUpgradeArmyObject"/> 会按等级收钱，这里不能复用。
    /// </remarks>
    private static void AiToUpgradeArmyObject(Hero hero, Mp2ObjectType objectType)
    {
        static void UpgradeIfPresent(Hero hero, MonsterType monster)
        {
            if (hero.Army.HasMonster(monster))
            {
                hero.Army.UpgradeMonsters(monster);
            }
        }

        switch (objectType)
        {
            case Mp2ObjectType.HillFort:
                UpgradeIfPresent(hero, MonsterType.Dwarf);
                UpgradeIfPresent(hero, MonsterType.Orc);
                UpgradeIfPresent(hero, MonsterType.Ogre);
                break;

            case Mp2ObjectType.FreemansFoundry:
                UpgradeIfPresent(hero, MonsterType.Pikeman);
                UpgradeIfPresent(hero, MonsterType.Swordsman);
                UpgradeIfPresent(hero, MonsterType.IronGolem);
                break;

            default:
                Debug.Assert(false, "Only the two upgrade objects have a fixed upgrade table.");
                break;
        }
    }

    /// <summary>
    /// A wagon, <c>AIToWagon()</c> (<c>ai_hero_action.cpp:934-950</c>).
    ///
    /// <remarks>
    /// 取物的模型改动与玩家侧相同（<see cref="HeroActions.ActionToWagon"/>），参考的 AI 版末尾多一句
    /// <c>removeMainObjectFromTile()</c>：AI 不需要在地图上留个空壳让玩家再点一次。
    /// </remarks>
    private static void AiToWagon(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToWagon(tileIndex, world);

        world.GetTile(tileIndex).RemoveMainObject(world);
    }

    /// <summary>
    /// A skeleton, <c>AIToSkeleton()</c> (<c>ai_hero_action.cpp:912-933</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 AI 版先看背包：满了就只翻出金币（不碰神器车道），没满才谈神器；金币为零说明这条路是空的。
    /// </remarks>
    private static void AiToSkeleton(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToSkeleton(tileIndex, world);

        world.GetTile(tileIndex).RemoveMainObject(world);
    }

    /// <summary>
    /// Flotsam, <c>AIToFlotSam()</c> (<c>ai_hero_action.cpp:952-963</c>).
    /// </summary>
    /// <remarks>AI 版同样只多一句删物件；给什么钱由 <see cref="HeroActions.ActionToFlotsam"/> 按参考的查表决定。</remarks>
    private static void AiToFlotsam(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToFlotsam(tileIndex, world);

        world.GetTile(tileIndex).RemoveMainObject(world);
    }

    /// <summary>
    /// The survivor of a wreck, <c>AIToShipwreckSurvivor()</c> (<c>ai_hero_action.cpp:1735-1748</c>).
    /// </summary>
    /// <remarks>背包满就把物件折成金币，否则收下神器；两支都要删物件、清元数据。</remarks>
    private static void AiToShipwreckSurvivor(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToShipwreckSurvivor(tileIndex, world);

        Tile tile = world.GetTile(tileIndex);
        tile.RemoveMainObject(world);
        tile.ResetObjectMetadata();
    }

    /// <summary>
    /// A sign, <c>AIToSign()</c> (<c>ai_hero_action.cpp:952-963</c>).
    /// </summary>
    /// <remarks>
    /// AI 不读牌子上的字（没有界面），但参考仍把这块牌子记进<b>王国</b>名册，规划器因此不会让英雄再跑一趟。
    /// </remarks>
    private static void AiToSign(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        world.GetKingdom(hero.Color).SetVisited(tileIndex, Mp2ObjectType.Sign);
    }

    /// <summary>
    /// An observation tower, <c>AIToObservationTower()</c> (<c>ai_hero_action.cpp:1099-1110</c>).
    /// </summary>
    /// <remarks>
    /// 揭雾与玩家侧共用 <see cref="HeroActions.ActionToObservationTower"/>；AI 版多的是把那格记进王国名册，
    /// 免得规划器把「已经爬过的塔」当成待办。
    /// </remarks>
    private static void AiToObservationTower(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToObservationTower(tileIndex, world);

        world.GetKingdom(hero.Color).SetVisited(tileIndex, Mp2ObjectType.ObservationTower);
    }

    /// <summary>
    /// A barrier, <c>AIToBarrier()</c> (<c>ai_hero_action.cpp:1898-1910</c>).
    /// </summary>
    /// <remarks>
    /// 参考 AI 不弹「你打不开这道关卡」：帐篷颜色对上了就直接把路障从地图上删掉。
    /// <see cref="HeroActions.ActionToBarrier"/> 只回答「帐篷开过没有」，删物件这一步得 AI 自己做。
    /// </remarks>
    private static void AiToBarrier(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        if (!actions.ActionToBarrier(tileIndex, world))
        {
            return;
        }

        Tile tile = world.GetTile(tileIndex);

        tile.RemoveMainObject(world);
        tile.ResetObjectMetadata();
    }

    /// <summary>
    /// Boarding a boat, <c>AIToBoat()</c> (<c>ai_hero_action.cpp:1805-1845</c>).
    /// </summary>
    /// <remarks>
    /// 参考的 AI 版与玩家版只差过场动画，模型改动一模一样，所以转调玩家侧
    /// <see cref="HeroActions.ActionToBoat"/>——那一支本来也不问宿主。
    /// </remarks>
    private static void AiToBoat(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToBoat(tileIndex, world);
    }

    /// <summary>
    /// Stepping ashore, <c>AIToCoast()</c> (<c>ai_hero_action.cpp:1847-1873</c>).
    /// </summary>
    /// <remarks>
    /// 同上：船留在下水处、朝向由「船指向岸边」决定，两支的实现完全一致，只是动画不同。
    /// </remarks>
    private static void AiToCoast(Hero hero, int tileIndex, World world, HeroActions actions)
    {
        actions.ActionToCoast(tileIndex, world);
    }
}
