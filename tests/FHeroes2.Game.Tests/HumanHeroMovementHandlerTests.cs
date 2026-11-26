using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Army;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The human half of the walking handlers, <see cref="HumanHeroMovementHandler"/>: the shared action table of
/// <c>Heroes::Action()</c> as a human player reaches it (<c>heroes_action.cpp:3747-3777</c>) and the guard scan
/// of <c>Heroes::ActionNewPosition( bool )</c> (<c>heroes.cpp:2019-2046</c>).
/// </summary>
/// <remarks>
/// <see cref="World.Current"/> 是进程级的，战斗又要在里面找英雄与瓦片，所以整类挂到
/// <see cref="SettingsSingletonCollection"/> 上串行跑。
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class HumanHeroMovementHandlerTests : IDisposable
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

    /// <summary>Walking onto a resource takes it — the handler reaches the shared <see cref="HeroActions"/> table.</summary>
    [Fact]
    public void WalkingOntoAResourcePicksItUp()
    {
        (World world, PlayerKingdom kingdom, Hero hero) = CreateWorld();

        int woodBefore = kingdom.Treasury.Wood;

        hero.MovementHandler.Action(hero, ResourceTile);

        Assert.Equal(woodBefore + 5, kingdom.Treasury.Wood);
    }

    /// <summary>A teleport never starts a fight, <c>heroes.cpp:2021</c>.</summary>
    [Fact]
    public void ATeleportDoesNotStartAFight()
    {
        (World world, _, Hero hero) = CreateWorld();
        PutMonster(world, ResourceTile);

        hero.MovementHandler.ActionNewPosition(hero, allowMonsterAttack: false);

        Assert.Equal(-1, hero.AttackedMonsterTileIndex);
    }

    /// <summary>Landing next to guards starts the fight and pays its spoils — the battle really ran.</summary>
    /// <remarks>
    /// <para>
    /// 断言经验而不是 <see cref="Hero.AttackedMonsterTileIndex"/>：战斗结束那一支会把它复位成 -1
    /// （<c>HeroActions.Battle.cs:252</c>），而这场的兵力比让守卫既不肯加入也不逃跑，会真的打完、真的给经验
    /// （<c>214</c>）。
    /// </para>
    /// <para>
    /// 两边的兵力要挑：<c>Army::GetJoinSolution()</c> 在英雄是人类、兵力比大于 5 时判守卫逃跑、大于 2 时判加入
    /// （<c>army.cpp:1690-1729</c>），两条都不会开战，也就没有经验。2 农民对 1 农民的比值正好是 2，落在
    /// 「既不加入也不逃跑」那一段。
    /// </para>
    /// </remarks>
    [Fact]
    public void AGuardOnTheDestinationPullsTheHeroIntoTheFight()
    {
        (World world, _, Hero hero) = CreateWorld(peasants: 2);
        PutMonster(world, ResourceTile);
        hero.Path.SetPath([new RouteStep(ResourceTile, HeroTile, Direction.Right, 100)]);

        uint experienceBefore = hero.Experience;

        hero.MovementHandler.ActionNewPosition(hero, allowMonsterAttack: true);

        Assert.True(hero.Experience > experienceBefore, "The guard must have been fought and paid for.");
    }

    /// <summary>A tile without guards leaves the hero standing where he is.</summary>
    [Fact]
    public void WithoutGuardsNothingHappens()
    {
        (_, _, Hero hero) = CreateWorld();

        hero.MovementHandler.ActionNewPosition(hero, allowMonsterAttack: true);

        Assert.Equal(-1, hero.AttackedMonsterTileIndex);
    }

    /// <summary>A 5 × 5 grassland with a human blue kingdom, a hero on tile 12 and five wood on tile 13.</summary>
    /// <param name="peasants">Size of the hero's only stack, see <see cref="AGuardOnTheDestinationPullsTheHeroIntoTheFight"/>.</param>
    private static (World World, PlayerKingdom Kingdom, Hero Hero) CreateWorld(uint peasants = 10)
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

        for (int index = 0; index < world.Size; ++index)
        {
            Tile tile = world.Tiles[index];
            tile.Index = index;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        // 第三个参数是交给电脑的颜色：不传就等于人类自己操作（参考看的是 GetKingdom().isControlHuman()）。
        world.Kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.None);

        PlayerKingdom kingdom = world.Kingdoms.GetKingdom(PlayerColor.Blue);

        world.Heroes.Init(MapVersion.SuccessionWars);

        Hero hero = world.GetHero(Hero.LordKilburn) ?? throw new InvalidOperationException("The map has no Kilburn.");

        kingdom.AddHero(hero);

        hero.IsControlHuman = true;
        hero.SetMapIndex(HeroTile);
        world.GetTile(HeroTile).SetHero(world, hero);
        hero.MovementHandler = new HumanHeroMovementHandler(world);

        // 空军队一开战就判负，还会在 BattleLose 里撞上「被解散的英雄必须有理由」的断言。
        hero.Army.JoinTroop(MonsterType.Peasant, peasants);

        Tile resource = world.GetTile(ResourceTile);
        resource.SetMainObjectPart(new ObjectPart(ObjectLayerType.Object, 1, Mp2ObjectIcnType.ObjnRsrc, 1));
        resource.SetMainObjectType(Mp2ObjectType.ActionResource);
        resource.Metadata[0] = Resource.Wood;
        resource.Metadata[1] = 5;

        World.Current = world;

        return (world, kingdom, hero);
    }

    /// <summary>A wandering stack on a tile: the creature is the sprite index plus one, the count the first word.</summary>
    private static void PutMonster(World world, int index, int count = 1)
    {
        Tile tile = world.GetTile(index);

        tile.SetMainObjectPart(new ObjectPart(
            ObjectLayerType.Object,
            (uint)(index + 1),
            Mp2ObjectIcnType.Mons32,
            (byte)((int)MonsterType.Peasant - 1)));
        tile.SetMainObjectType(Mp2ObjectType.ActionMonster);
        tile.Metadata[0] = (uint)count;
    }

}
