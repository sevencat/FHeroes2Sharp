using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Ai;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Behaviour tests for the part of <c>ai_planner.cpp</c> that is ported: the guardian strength cache and the two
/// resource weights of <see cref="AiPlanner"/>.
/// </summary>
/// <remarks>
/// The planner is a singleton whose caches live until the next turn starts, so every test measures the tiles it
/// broke itself and never the ones another test did.
/// </remarks>
[Collection(SettingsSingletonCollection.Name)]
public sealed class AiPlannerTests : IDisposable
{
    private readonly World _previousWorld = World.Current;

    public void Dispose()
    {
        World.Current = _previousWorld;
        Settings.Reset();
    }

    [Fact]
    public void TheStrengthOfATileIsMeasuredOnceAndThenRemembered()
    {
        World world = NewWorld();

        Tile small = PlaceGuard(world, 40, 1);
        double oneChampion = AiPlanner.Instance.GetTileArmyStrength(small);

        Assert.True(oneChampion > 0.0);

        // The answer is cached: growing the same stack does not change what the planner already knows ...
        small.SetMonsterCount(world, 100);

        Assert.Equal(oneChampion, AiPlanner.Instance.GetTileArmyStrength(small));

        // ... while a stack of the same size on a tile that was not measured yet is worth a hundred times more.
        Assert.True(AiPlanner.Instance.GetTileArmyStrength(PlaceGuard(world, 41, 100)) > oneChampion);
    }

    [Fact]
    public void AMineIsWorthTheInverseOfItsDailyIncome()
    {
        // A gold mine earns 1000 a day and weighs 2; every other mine is worth what its two days of income would
        // be in gold, so the rarer the resource the higher the weight.
        Assert.Equal(2.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Gold, true));
        Assert.Equal(1000.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Wood, true));
        Assert.Equal(1000.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Ore, true));
        Assert.Equal(2000.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Mercury, true));
        Assert.Equal(2000.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Sulfur, true));
        Assert.Equal(2000.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Crystal, true));
        Assert.Equal(2000.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Gems, true));
    }

    [Fact]
    public void APileIsWorthWhatAnOrdinaryPileOfThatResourceHolds()
    {
        // One unit of a rare resource is worth twice one unit of wood, because a pile holds about half as many.
        Assert.Equal(1.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Gold, false));
        Assert.Equal(125.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Wood, false));
        Assert.Equal(125.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Ore, false));
        Assert.Equal(250.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Mercury, false));
        Assert.Equal(250.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Sulfur, false));
        Assert.Equal(250.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Crystal, false));
        Assert.Equal(250.0, AiPlanner.Instance.GetResourcePriorityModifier(Resource.Gems, false));
    }

    [Fact]
    public void TheValueOfAFundsAddsUpTheWeightOfEveryResource()
    {
        // 10 wood * 125 + 2 gems * 250 + 500 gold * 1. Nothing is on the budget yet, so no entry scales these.
        Assert.Equal(2250.0, AiPlanner.Instance.GetFundsValueBasedOnPriority(
            new Funds(ore: 0, wood: 10, mercury: 0, sulfur: 0, crystal: 0, gems: 2, gold: 500)));
    }

    /// <summary>Puts a stack of champions on a tile that is dry land.</summary>
    /// <param name="world">The world to change.</param>
    /// <param name="tileIndex">Index of the tile.</param>
    /// <param name="count">How many champions to put there.</param>
    /// <returns>The tile, so the caller can change it again.</returns>
    private static Tile PlaceGuard(World world, int tileIndex, uint count)
    {
        // An uninitialized map is water under the hood, and a monster cannot stand on water.
        Tile tile = world.GetTile(tileIndex);
        tile.SetTerrain(MapGrounds.GrassStartImageIndex, 0);
        tile.SetInitialPassability();
        tile.SetMonster(world, MonsterType.Champion, count);

        return tile;
    }

    /// <summary>Starts a fresh game world on a square of empty tiles.</summary>
    /// <returns>The world, also installed as <see cref="World.Current"/>.</returns>
    private static World NewWorld()
    {
        Settings.Reset();
        Settings.Get().GameDifficulty = GameDifficulty.Normal;
        Settings.Get().Players.Init(PlayerColor.Blue);

        World world = new();
        world.GenerateUninitializedMap(8);

        // An uninitialized map leaves every index at zero, which is only fine for a battle only game. Loading a
        // map numbers the tiles, and the planner keys its strength cache by that number.
        for (int index = 0; index < 8 * 8; ++index)
        {
            world.GetTile(index).Index = index;
        }

        world.Kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.Blue);
        World.Current = world;

        return world;
    }
}
