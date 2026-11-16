using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using Xunit;

using ArmyStack = FHeroes2.Game.Army.Army;
using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <c>Army::GetJoinSolution()</c> (<c>army.cpp:1641-1731</c>) — the four questions a wandering stack asks
/// before it joins, charges or runs.
/// </summary>
/// <remarks>
/// The campaign-award block in front of the strength ratio is not ported, so no test here produces
/// <see cref="NeutralMonsterJoiningReason.Alliance"/> or <see cref="NeutralMonsterJoiningReason.Bane"/>.
/// </remarks>
public sealed class ArmyJoinSolutionTests
{
    [Fact]
    public void AnEmptyHeroArmyCannotScareThem()
    {
        World world = MakeWorld();
        Hero hero = new Hero(1, MapRace.Knight);
        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 5);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world);

        Assert.Equal(NeutralMonsterJoiningReason.None, condition.Reason);
        Assert.Equal(0u, condition.MonsterCount);
    }

    [Fact]
    public void AFreeTileGivesTheWholeStack()
    {
        World world = MakeWorld();
        Hero hero = MakeStrongHero();
        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 10);

        tile.SetMonsterJoinCondition(world, MonsterJoinConditions.Free);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world);

        Assert.Equal(NeutralMonsterJoiningReason.Free, condition.Reason);
        Assert.Equal(10u, condition.MonsterCount);
    }

    [Fact]
    public void GhostsNeverJoinEvenForFree()
    {
        World world = MakeWorld();
        Hero hero = MakeStrongHero();
        Tile tile = MakeMonsterTile(world, MonsterType.Ghost, 5);

        // A wandering ghost is marked SKIP when it is placed, whatever else the tile says.
        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world);

        Assert.Equal(NeutralMonsterJoiningReason.None, condition.Reason);
        Assert.Equal(0u, condition.MonsterCount);
    }

    [Fact]
    public void AFiveTimesStrongerAiHeroIsFledFrom()
    {
        World world = MakeWorld();
        Hero hero = MakeStrongHero();
        hero.IsControlHuman = false;
        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 5);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world);

        Assert.Equal(NeutralMonsterJoiningReason.RunAway, condition.Reason);
        Assert.Equal(0u, condition.MonsterCount);
    }

    [Fact]
    public void AFiveTimesStrongerHumanHeroIsNotFledFrom()
    {
        World world = MakeWorld();
        Hero hero = MakeStrongHero();
        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 5);

        Assert.True(hero.IsControlHuman);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world);

        // The monsters would have joined for money if they could, but the hero has no diplomacy.
        Assert.Equal(NeutralMonsterJoiningReason.None, condition.Reason);
    }

    [Fact]
    public void AFullHeroArmyGivesThemNowhereToGo()
    {
        World world = MakeWorld();
        Hero hero = new Hero(1, MapRace.Knight);

        for (int slot = 0; slot < ArmyStack.MaximumTroopCount; ++slot)
        {
            hero.Army.GetTroop(slot).Set(MonsterType.BlackDragon, 20);
        }

        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 5);

        tile.SetMonsterJoinCondition(world, MonsterJoinConditions.Free);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world);

        Assert.Equal(NeutralMonsterJoiningReason.None, condition.Reason);
    }

    [Fact]
    public void DiplomacyBuysAQuarterOfTheStackWithTheKingdomGold()
    {
        World world = MakeWorld();
        Hero hero = MakeStrongHero();
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Diplomacy, SkillLevel.Basic));

        PlayerKingdom kingdom = new();
        kingdom.Treasury.Gold = 5000;

        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 40);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world, kingdom);

        Assert.Equal(NeutralMonsterJoiningReason.ForMoney, condition.Reason);

        // Basic diplomacy buys 25 percent of the stack's hit points: 40 goblins of 3 hit points are 120, and
        // 30 hit points of goblins are ten creatures.
        Assert.Equal(10u, condition.MonsterCount);
    }

    [Fact]
    public void DiplomacyCannotPayWithoutAKingdom()
    {
        World world = MakeWorld();
        Hero hero = MakeStrongHero();
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Diplomacy, SkillLevel.Basic));

        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 40);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world);

        Assert.Equal(NeutralMonsterJoiningReason.None, condition.Reason);
    }

    [Fact]
    public void DiplomacyCannotPayWithoutTheGold()
    {
        World world = MakeWorld();
        Hero hero = MakeStrongHero();
        hero.SecondarySkills.Add(new SecondarySkillLevel(SecondarySkill.Diplomacy, SkillLevel.Basic));

        PlayerKingdom kingdom = new();

        Tile tile = MakeMonsterTile(world, MonsterType.Goblin, 40);

        NeutralMonsterJoiningCondition condition =
            ArmyStack.GetJoinSolution(hero, tile, tile.GetTroop(world), world, kingdom);

        Assert.Equal(NeutralMonsterJoiningReason.None, condition.Reason);
    }

    private static World MakeWorld()
    {
        World world = new();
        world.GenerateUninitializedMap(4);

        return world;
    }

    /// <summary>A hero whose army outweighs any of the small neutral stacks below by more than five times.</summary>
    private static Hero MakeStrongHero()
    {
        Hero hero = new Hero(1, MapRace.Knight);
        hero.Army.GetTroop(0).Set(MonsterType.BlackDragon, 20);

        return hero;
    }

    private static Tile MakeMonsterTile(World world, MonsterType monster, uint count)
    {
        Tile tile = world.GetTile(5);

        tile.Index = 5;
        tile.SetMonster(world, monster, count);

        return tile;
    }
}
