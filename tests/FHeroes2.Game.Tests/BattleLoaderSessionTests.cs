using FHeroes2.Assets.Maps;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The two halves of <see cref="BattleLoader.Load"/> split apart —
/// <see cref="BattleLoader.TryPrepare"/>, <see cref="BattleLoader.Settle"/> and
/// <see cref="BattleLoader.CloseSession"/> — so a frame-driven interface can play the fight itself.
/// </summary>
/// <remarks>
/// 这些测试只证明「拆开与不拆开等价」：<see cref="BattleLoader.Load"/> 仍是参考的直译，两半只是把同一段代码
/// 分成「准备」与「结算」。战斗画面真正接管的那条链由 App 层验证。
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class BattleLoaderSessionTests : IDisposable
{
    private const int MapSize = 5;
    private const int TileIndex = 13;

    private readonly World _previousWorld = World.Current;

    public void Dispose()
    {
        World.Current = _previousWorld;
        Settings.Reset();
    }

    /// <summary>A side with nothing to fight with loses before an arena is even built.</summary>
    [Fact]
    public void ASideWithoutTroopsLosesWithoutAPreparedBattle()
    {
        World world = CreateWorld();

        var request = new BattleLoadRequest
        {
            World = world,
            AttackingArmy = new GameArmy(),
            DefendingArmy = Peasants(5),
            TileIndex = TileIndex,
        };

        BattleSession? session = BattleLoader.TryPrepare(request, out BattleResult immediateResult);

        Assert.Null(session);
        Assert.Equal(BattleOutcome.Wins, immediateResult.Defender);
        Assert.Equal(BattleOutcome.None, immediateResult.Attacker);
    }

    /// <summary>
    /// Prepare builds the arena the loader would build, around the very armies it was handed.
    /// </summary>
    /// <remarks>
    /// 这里不跑 <see cref="BattleLoader.RunToEnd"/>：两方都是电脑时 AI 会走一条「攻击方向非法」的老路
    /// （<c>BattleArena.ApplyActionAttack</c> 拒绝 <c>direction 8</c>，见 bd 里那张缺陷），与「拆开两半」无关。
    /// 整套对战的等价性由 App 的两张战斗像素基线与实机 <c>--battle</c> 兜底。
    /// </remarks>
    [Fact]
    public void PrepareBuildsTheArenaAroundTheSameArmies()
    {
        World world = CreateWorld();
        GameArmy attackers = Peasants(10);
        attackers.Commander = AddHero(world);
        GameArmy defenders = Peasants(1);

        var request = new BattleLoadRequest
        {
            World = world,
            AttackingArmy = attackers,
            DefendingArmy = defenders,
            TileIndex = TileIndex,
            AutoResolve = true,
        };

        BattleSession session = BattleLoader.TryPrepare(request, out BattleResult immediateResult)
            ?? throw new InvalidOperationException("Two armies of peasants must prepare a battle.");

        Assert.Same(request, session.Request);
        Assert.Same(attackers, session.Arena.AttackingForce.OriginalArmy);
        Assert.Same(defenders, session.Arena.DefendingForce.OriginalArmy);
        Assert.True(session.Arena.AttackingForce.HasMonster(MonsterType.Peasant));
        Assert.True(session.Arena.DefendingForce.HasMonster(MonsterType.Peasant));

        // 自动结算没有界面可放，所以窗口与结果屏都不该开。
        Assert.False(session.ShowBattle);
        Assert.Equal(BattleOutcome.None, immediateResult.Attacker);
    }

    /// <summary>Closing a session drops the monsters the fight invalidated, and only them.</summary>
    [Fact]
    public void ClosingASessionDropsInvalidMonsters()
    {
        World world = CreateWorld();
        GameArmy attackers = Peasants(10);
        attackers.Commander = AddHero(world);

        var request = new BattleLoadRequest
        {
            World = world,
            AttackingArmy = attackers,
            DefendingArmy = Peasants(1),
            TileIndex = TileIndex,
            AutoResolve = true,
        };

        BattleSession session = BattleLoader.TryPrepare(request, out _)
            ?? throw new InvalidOperationException("Two armies of peasants must prepare a battle.");

        BattleLoader.CloseSession(session, default);

        // 收尾只清「撤销后变成非法」的兵位，剩下的队伍原样还在。
        Assert.True(attackers.HasMonster(MonsterType.Peasant));
        Assert.True(session.Request.DefendingArmy.HasMonster(MonsterType.Peasant));
    }

    /// <summary>A 5 × 5 grassland with a human blue kingdom — enough for a field battle on tile 12.</summary>
    private static World CreateWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(MapSize);

        for (int index = 0; index < world.Size; ++index)
        {
            Tile tile = world.Tiles[index];
            tile.Index = index;
            tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        }

        world.Kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.None);

        World.Current = world;

        return world;
    }

    /// <summary>Three stacks, the way a hero's army looks: a single stack spreads into a line the arena misplaces.</summary>
    private static GameArmy Peasants(uint count, PlayerColor color = PlayerColor.None)
    {
        var army = new GameArmy { Color = color };
        army.JoinTroop(MonsterType.Peasant, count);
        army.JoinTroop(MonsterType.Archer, 2);
        army.JoinTroop(MonsterType.Swordsman, 2);

        return army;
    }

    /// <summary>
    /// A hero of the blue kingdom to lead the attacking army — a battle force without a commander spreads the
    /// troops in a line the arena misplaces, which makes the computer attack from illegal cells.
    /// </summary>
    private static Hero AddHero(World world)
    {
        world.Heroes.Init(MapVersion.SuccessionWars);

        Hero hero = world.GetHero(Hero.LordKilburn)
            ?? throw new InvalidOperationException("The map has no Kilburn.");

        world.Kingdoms.GetKingdom(PlayerColor.Blue).AddHero(hero);

        return hero;
    }
}
