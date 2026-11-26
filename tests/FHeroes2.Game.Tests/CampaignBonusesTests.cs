using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.System;
using Xunit;

using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;
using TroopArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the scenario bonuses and the campaign awards (<c>game_campaign.cpp:501-780</c>).
/// </summary>
[Collection(CampaignCollection.Name)]
public sealed class CampaignBonusesTests
{
    /// <summary>Every test starts from a clean campaign save, because the carry-over award reads it.</summary>
    public CampaignBonusesTests()
    {
        CampaignSaveData.Get().Reset();
        Settings.Get().Players.Clear();
    }

    [Fact]
    public void ReplaceArmyFillsSlotBySlotAndEmptiesTheRest()
    {
        var army = new TroopArmy(MapRace.Knight);
        Assert.True(army.JoinTroop(MonsterType.Peasant, 10));
        Assert.True(army.JoinTroop(MonsterType.Archer, 5));

        CampaignBonuses.ReplaceArmy(army, [new Troop(MonsterType.Goblin, 7)]);

        Assert.Equal(MonsterType.Goblin, army[0].Monster);
        Assert.Equal(7u, army[0].Count);
        Assert.True(army[1].IsEmpty);
    }

    [Fact]
    public void AResourceBonusGoesToTheHumanKingdom()
    {
        World world = CreateWorld(out PlayerKingdom kingdom, out _);
        int starting = kingdom.GetFunds().Gold;

        CampaignBonuses.SetScenarioBonus(
            world,
            new ScenarioInfoId(CampaignId.Roland, 0),
            new ScenarioBonusData(ScenarioBonusData.BonusType.Resources, (int)Resource.Gold, 1500));

        // 王国开局本来就有一笔金（难度不同数量不同），这里只看加了多少。
        Assert.Equal(starting + 1500, kingdom.GetFunds().Gold);
    }

    [Fact]
    public void APrimarySkillBonusRaisesTheHerosSkillByTheAmount()
    {
        World world = CreateWorld(out _, out Hero hero);
        int attack = hero.BaseAttack;

        CampaignBonuses.SetScenarioBonus(
            world,
            new ScenarioInfoId(CampaignId.Roland, 0),
            new ScenarioBonusData(ScenarioBonusData.BonusType.SkillPrimary, (int)PrimarySkill.Attack, 2));

        Assert.Equal(attack + 2, hero.BaseAttack);
    }

    [Fact]
    public void ATroopBonusJoinsTheHerosArmy()
    {
        World world = CreateWorld(out _, out Hero hero);

        CampaignBonuses.SetScenarioBonus(
            world,
            new ScenarioInfoId(CampaignId.Roland, 0),
            new ScenarioBonusData(ScenarioBonusData.BonusType.Troop, (int)MonsterType.Swordsman, 4));

        Troop troop = Assert.Single(hero.Army.Slots, slot => slot.IsValid);
        Assert.Equal(MonsterType.Swordsman, troop.Monster);
        Assert.Equal(4u, troop.Count);
    }

    [Fact]
    public void AStartingRaceBonusChangesThePlayerRace()
    {
        World world = CreateWorld(out _, out _);

        CampaignBonuses.SetScenarioBonus(
            world,
            new ScenarioInfoId(CampaignId.Roland, 0),
            new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRace, (int)MapRace.Warlock, 0));

        Assert.Equal(MapRace.Warlock, Players.GetPlayerRace(PlayerColor.Blue));
    }

    [Fact]
    public void TheArchibaldSeventhScenarioTopsTheHerosExperienceUpToFiveThousand()
    {
        World world = CreateWorld(out _, out Hero hero);

        // 那一关的兵表按种族分三套，骑士不在其中：参考是 assert(0)，端口不动部队，经验照旧补上。
        CampaignBonuses.SetScenarioBonus(
            world,
            new ScenarioInfoId(CampaignId.Archibald, 6),
            new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRaceAndArmy, (int)MapRace.Knight, 0));

        Assert.Equal(5000u, hero.Experience);
        Assert.DoesNotContain(hero.Army.Slots, troop => troop.IsValid);
    }

    [Fact]
    public void TheBonusIsSkippedForScenariosThatAreNotTheArchibaldSeventh()
    {
        World world = CreateWorld(out _, out Hero hero);
        uint starting = hero.Experience;

        CampaignBonuses.SetScenarioBonus(
            world,
            new ScenarioInfoId(CampaignId.Archibald, 5),
            new ScenarioBonusData(ScenarioBonusData.BonusType.StartingRaceAndArmy, (int)MapRace.Knight, 0));

        Assert.Equal(starting, hero.Experience);
    }

    [Fact]
    public void TheCarryOverAwardSwapsTheHerosArmyForTheSavedOne()
    {
        World world = CreateWorld(out _, out Hero hero);

        var saved = new TroopArmy(MapRace.Knight);
        Assert.True(saved.JoinTroop(MonsterType.Crusader, 12));
        CampaignSaveData.Get().SetCarryOverTroops(saved);

        CampaignBonuses.ApplyObtainedCampaignAwards(
            world,
            new ScenarioInfoId(CampaignId.Roland, 4),
            [new CampaignAwardData(0, CampaignAwardData.AwardType.CarryOverForces, 0, 12, 3)]);

        Troop troop = Assert.Single(hero.Army.Slots, slot => slot.IsValid);
        Assert.Equal(MonsterType.Crusader, troop.Monster);
        Assert.Equal(12u, troop.Count);
    }

    [Fact]
    public void TheCarryOverAwardFallsBackToTheRacesBasicTroopWhenNothingWasSaved()
    {
        World world = CreateWorld(out _, out Hero hero);

        Assert.True(hero.Army.JoinTroop(MonsterType.Peasant, 30));

        CampaignBonuses.ApplyObtainedCampaignAwards(
            world,
            new ScenarioInfoId(CampaignId.Roland, 4),
            [new CampaignAwardData(0, CampaignAwardData.AwardType.CarryOverForces, 0, 0, 3)]);

        // 参考没有携带部队时调的是 `Army::Reset()`，那不是清空而是「给一个一级兵」（army.cpp 的 reset）。
        Troop troop = Assert.Single(hero.Army.Slots, slot => slot.IsValid);
        Assert.Equal(1u, troop.Count);
        Assert.Equal(MonsterType.Peasant, troop.Monster);
    }

    [Fact]
    public void AnAwardNeverAppliesToTheScenarioItWasWonIn()
    {
        World world = CreateWorld(out _, out Hero hero);

        var saved = new TroopArmy(MapRace.Knight);
        Assert.True(saved.JoinTroop(MonsterType.Crusader, 12));
        CampaignSaveData.Get().SetCarryOverTroops(saved);

        // 奖励物从 StartScenarioId 那一关起才生效（`scenarioId < startScenarioId` 就跳过），所以在它之前的那一关不给。
        CampaignBonuses.ApplyObtainedCampaignAwards(
            world,
            new ScenarioInfoId(CampaignId.Roland, 2),
            [new CampaignAwardData(0, CampaignAwardData.AwardType.CarryOverForces, 0, 12, 3)]);

        Assert.DoesNotContain(hero.Army.Slots, troop => troop.IsValid);
    }

    /// <summary>An 8 × 8 world with a blue kingdom driven by a person and one hero in it.</summary>
    /// <param name="kingdom">The blue kingdom.</param>
    /// <param name="hero">The hero standing in that kingdom.</param>
    /// <returns>The world.</returns>
    private static World CreateWorld(out PlayerKingdom kingdom, out Hero hero)
    {
        var world = new World();
        world.GenerateUninitializedMap(8);

        world.Kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.None);
        world.Heroes.Init(MapVersion.SuccessionWars);

        Settings.Get().Players.Init(PlayerColor.Blue);

        Player player = Players.Get(PlayerColor.Blue) ?? throw new InvalidOperationException("The blue slot is missing.");
        player.Control = PlayerControl.Human;

        kingdom = world.Kingdoms.GetKingdom(PlayerColor.Blue);
        hero = world.GetHero(Hero.LordKilburn) ?? throw new InvalidOperationException("The map has no Kilburn.");

        kingdom.AddHero(hero);

        // 地图生成时英雄本来带一队兵，测试要看的是奖励自己动的手，先清空。
        hero.Army.Clean();

        return world;
    }
}
