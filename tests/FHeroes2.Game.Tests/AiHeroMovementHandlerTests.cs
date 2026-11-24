using FHeroes2.Assets.Maps;

using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="AiHeroMovementHandler"/>: the AI branch of <c>Heroes::Action()</c>
/// (<c>heroes_action.cpp:3778-3785</c>) as it is reached from the walking frame loop.
/// </summary>
/// <remarks>
/// <para>
/// 这是 P2 收口的连接点：参考里 <c>AI::HeroesMove()</c> 只是反复调 <c>hero.Move()</c>，脚下的对象由
/// <c>Heroes::Action()</c> 自己按控制权分派。端口把那条分派放在英雄的 <see cref="IHeroMovementHandler"/> 上，
/// 所以「AI 英雄走过去、资源进了国库、路线被清空」这一整条链只有装上这个处理器才会成立。
/// </para>
/// <para>
/// <see cref="World.Current"/> 是进程级的，AI 的动作完成后还要按它刷新行动对象缓存，所以整类挂到
/// <see cref="SettingsSingletonCollection"/> 上串行跑，免得和别的换世界的测试撞车。
/// </para>
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AiHeroMovementHandlerTests : IDisposable
{
    private const int MapSize = 5;
    private const int HeroTile = 12;
    private const int ResourceTile = 13;

    private readonly World _previousWorld = World.Current;

    public void Dispose()
    {
        World.Current = _previousWorld;
        Settings.Reset();
    }

    [Fact]
    public void WalkingOntoAResourcePicksItUpAndEndsTheRoute()
    {
        (World world, PlayerKingdom kingdom, Hero hero) = CreateWorldWithWalkingHero();

        int woodBefore = kingdom.Treasury.Wood;

        Assert.Equal(AiTurnOutcome.EndTurn, AiHeroAction.HeroesMove(hero));

        // 资源堆是「站在前面拿」的物件：`MP2::isNeedStayFront` 把拾取类物件全算进去（mp2.cpp:793），
        // 所以 Heroes::Move() 在下一格前停步、直接对那一格 Action()，人留在原地不动。
        Assert.Equal(HeroTile, hero.MapIndex);
        Assert.Equal(woodBefore + 5, kingdom.Treasury.Wood);

        // 物件本身已经从瓦片上抹掉：参考的 AIToPickupResource() 就是 removeMainObjectFromTile() +
        // resetObjectMetadata()（ai_hero_action.cpp:706-709）。
        Assert.Equal(Mp2ObjectIcnType.Unknown, world.GetTile(ResourceTile).MainObjectPart.IcnType);

        // 处理完对象之后参考会清掉路线（ai_hero_action.cpp:2213-2220 的那一句 Reset 的另一半理由：
        // 走过一格可能掉兵、掉法术点，目标得重新估）。
        Assert.True(hero.Path.IsEmpty);
    }

    /// <summary>A 5 × 5 grassland with a computer blue kingdom, a hero on tile 12 and five wood on tile 13.</summary>
    private static (World World, PlayerKingdom Kingdom, Hero Hero) CreateWorldWithWalkingHero()
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

        for (int index = 0; index < world.Size; ++index)
        {
            Tile tile = world.Tiles[index];
            tile.Index = index;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        // 第三个参数把蓝色交给电脑：参考的 Heroes::Action() 正是看这个答案把动作转给 AI::HeroesAction()。
        world.Kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);

        PlayerKingdom kingdom = world.Kingdoms.GetKingdom(PlayerColor.Blue);

        // 名册里的英雄才算「在世界里」：瓦片要靠 id 找回站着的人（Tile.GetHero），
        // 而 AI 寻路、访问名册也一样都走这张表。
        world.Heroes.Init(MapVersion.SuccessionWars);

        Hero hero = world.GetHero(Hero.LordKilburn) ?? throw new InvalidOperationException("The map has no Kilburn.");

        kingdom.AddHero(hero);

        hero.IsControlHuman = false;
        hero.SetMapIndex(HeroTile);
        world.GetTile(HeroTile).SetHero(world, hero);

        // 地图文件里的物件类型永远带动作位（`MP2::OBJ_RESOURCE` 本身就在 128 以上，mp2.h:48-96），
        // 而且抹掉物件要靠部件的 UID 认出它（removeMainObjectFromTile() → removeObjectFromTileByType()）：
        // 后者拿 ICN + 帧号去物件表里查类型，所以这一格必须放一个真正会出现在地图上的资源帧
        // （OBJNRSRC 的偶数帧是影子，查出来是 None，删不掉）。
        Tile resource = world.GetTile(ResourceTile);
        resource.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnRsrc, 1));
        resource.SetMainObjectType(Mp2ObjectType.ActionResource);
        resource.Metadata[0] = Resource.Wood;
        resource.Metadata[1] = 5;

        hero.MovementHandler = new AiHeroMovementHandler(world);
        hero.Path.SetPath([new RouteStep(ResourceTile, HeroTile, Direction.Right, 100)]);
        hero.SetMove(true);

        World.Current = world;

        return (world, kingdom, hero);
    }
}
