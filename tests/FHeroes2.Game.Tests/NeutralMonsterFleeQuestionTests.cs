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
/// The fleeing half of <see cref="HeroActions.ActionToMonster"/>: a neutral stack that thinks better of it asks
/// <see cref="HeroActionQuestion.PursueFleeingMonsters"/> and the answer decides between a battle and a scatter,
/// <c>heroes_action.cpp:387-407</c>.
/// </summary>
/// <remarks>
/// <para>
/// 为什么要问：<c>Army::GetJoinSolution()</c> 在英雄是人类、兵力比大于 5 时判「逃跑」（<c>army.cpp:1725</c>），
/// 这之后参考只有一处岔口——<c>Dialog::YES</c> 就去追（照打），<c>NO</c> 就让它们散掉（<c>455</c> 的
/// <c>destroy = true</c>）。两条都不会留下经验，只有 YES 会真打。
/// </para>
/// <para>
/// 问句窗口本身由界面层验过（问句窗的 YES/NO 与点击）；这里验的是游戏层拿到答案之后走哪一支，
/// 以及宿主确实被问到了、问的是哪一句。战斗没有观战者（<see cref="IHeroActionHost.BattlePresenter"/> 为
/// <see langword="null"/>），与 AI 打的仗同一段代码。
/// </para>
/// <para>
/// <see cref="World.Current"/> 是进程级的，战斗又要在里面找英雄与瓦片，所以整类挂到
/// <see cref="SettingsSingletonCollection"/> 上串行跑。
/// </para>
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class NeutralMonsterFleeQuestionTests : IDisposable
{
    private const int MapSize = 5;
    private const int HeroTile = 12;
    private const int MonsterTile = 13;

    private readonly World _previousWorld = World.Current;

    public void Dispose()
    {
        World.Current = _previousWorld;
        Settings.Reset();
    }

    /// <summary>Answering YES chases them down: the battle runs and pays experience.</summary>
    [Fact]
    public void ChasingFleeingMonstersStartsTheBattle()
    {
        (World world, Hero hero) = CreateWorld();
        var host = new RecordingHeroActionHost(answer: true);
        world.HeroActionHost = host;

        uint experienceBefore = hero.Experience;

        hero.MovementHandler.ActionNewPosition(hero, allowMonsterAttack: true);

        Assert.Equal(HeroActionQuestion.PursueFleeingMonsters, host.Question);
        Assert.Equal(MonsterType.Peasant, host.Troop.Monster);
        Assert.Equal(1u, host.Troop.Count);
        Assert.True(hero.Experience > experienceBefore, "Chasing must end in a fight, and a won fight pays experience.");
        Assert.False(world.GetTile(MonsterTile).GetTroop(world).IsValid, "The beaten stack leaves the tile.");
    }

    /// <summary>Answering NO scatters them: no battle, no experience, the tile is cleared all the same.</summary>
    [Fact]
    public void LettingFleeingMonstersGoScattersThem()
    {
        (World world, Hero hero) = CreateWorld();
        var host = new RecordingHeroActionHost(answer: false);
        world.HeroActionHost = host;

        uint experienceBefore = hero.Experience;

        hero.MovementHandler.ActionNewPosition(hero, allowMonsterAttack: true);

        Assert.Equal(HeroActionQuestion.PursueFleeingMonsters, host.Question);
        Assert.Equal(experienceBefore, hero.Experience);
        Assert.Equal(-1, hero.AttackedMonsterTileIndex);
        Assert.False(world.GetTile(MonsterTile).GetTroop(world).IsValid, "The scattered stack leaves the tile too.");
    }

    /// <summary>
    /// A 5 × 5 grassland with a human blue kingdom, a hero with ten peasants on tile 12 and one peasant on tile 13.
    /// </summary>
    /// <remarks>
    /// 兵力比挑成 10 比 1：大于 5 才会走到「逃跑」那一支（<c>army.cpp:1725</c>），小于等于 5 就只是普通的野怪。
    /// </remarks>
    private static (World World, Hero Hero) CreateWorld()
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
        hero.Army.JoinTroop(MonsterType.Peasant, 10);

        Tile monsterTile = world.GetTile(MonsterTile);
        monsterTile.SetMainObjectPart(new ObjectPart(
            ObjectLayerType.Object,
            (uint)(MonsterTile + 1),
            Mp2ObjectIcnType.Mons32,
            (byte)((int)MonsterType.Peasant - 1)));
        monsterTile.SetMainObjectType(Mp2ObjectType.ActionMonster);
        monsterTile.Metadata[0] = 1;

        // 守卫就在正右方一格，走路那一步的扫描才会把它当成要处理的对象。
        hero.Path.SetPath([new RouteStep(MonsterTile, HeroTile, Direction.Right, 100)]);

        World.Current = world;

        return (world, hero);
    }

    /// <summary>Answers the one question this test asks and remembers which one it was.</summary>
    private sealed class RecordingHeroActionHost(bool answer) : IHeroActionHost
    {
        public HeroActionQuestion? Question { get; private set; }

        public Troop Troop { get; private set; } = new();

        public IBattlePresenter? BattlePresenter => null;

        public bool Ask(HeroActionQuestion question, Troop troop, uint gold = 0)
        {
            Question = question;
            Troop = troop;

            return answer;
        }

        public bool Ask(HeroActionQuestion question, uint gold = 0) => answer;

        public uint AskRecruitCount(MonsterType monster, uint available, PlayerKingdom kingdom) => 0;

        public string AskRiddle(string riddle) => string.Empty;

        public void OpenCastle(CastleTown castle) => throw new InvalidOperationException("Nothing here opens a castle.");

        public void OpenMarketplace(PlayerKingdom kingdom, bool fromTradingPost)
            => throw new InvalidOperationException("Nothing here opens a marketplace.");

        public void OpenThievesGuild(PlayerKingdom kingdom)
            => throw new InvalidOperationException("Nothing here opens a guild.");
    }
}
