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
/// Covers the scenario accounting of <c>CompleteCampaignScenario()</c> (<c>game_campaign.cpp:1160-1282</c>).
/// </summary>
[Collection(CampaignCollection.Name)]
public sealed class CampaignProgressTests
{
    /// <summary>Every test starts from a clean campaign save, which is a process-wide singleton.</summary>
    public CampaignProgressTests()
    {
        CampaignSaveData.Get().Reset();
        Settings.Get().Players.Clear();
    }

    [Fact]
    public void AScenarioThatHandsOverToAnotherCampaignIsABetrayalOne()
    {
        // 罗兰第十一关「Betrayal」打完进阿基巴德的战役，阿基巴德第十二关反向同理。
        Assert.True(CampaignData.IsBetrayalScenario(new ScenarioInfoId(CampaignId.Roland, 10)));
        Assert.True(CampaignData.IsBetrayalScenario(new ScenarioInfoId(CampaignId.Archibald, 11)));

        Assert.False(CampaignData.IsBetrayalScenario(new ScenarioInfoId(CampaignId.Roland, 0)));
        Assert.False(CampaignData.IsBetrayalScenario(new ScenarioInfoId(CampaignId.Roland, 9)));
    }

    [Fact]
    public void CompletingAScenarioAddsItsDaysAndMovesTheCampaignOn()
    {
        World world = CreateWorld();
        CampaignSaveData saveData = CampaignSaveData.Get();

        ScenarioInfoId finished = new(CampaignId.Roland, 0);
        saveData.SetCurrentScenarioInfo(finished);

        world.NewDay();
        world.NewDay();

        CampaignScenarioCompletion completion = CampaignProgress.CompleteScenario(world, isLoadingSaveFile: false);

        Assert.False(completion.IsCampaignFinished);
        Assert.Equal(new ScenarioInfoId(CampaignId.Roland, 1), completion.NextScenario);
        Assert.True(completion.ShouldSave);

        Assert.Equal([finished], saveData.FinishedMaps);
        Assert.Equal(2u, saveData.DaysPassed);
        Assert.Equal(new ScenarioInfoId(CampaignId.Roland, 1), saveData.CurrentScenarioInfoId);
    }

    [Fact]
    public void CompletingTheLastScenarioEndsTheCampaignWithoutASaveToWrite()
    {
        World world = CreateWorld();
        CampaignSaveData saveData = CampaignSaveData.Get();

        ScenarioInfoId last = new(CampaignId.Roland, 9);
        Assert.True(CampaignData.GetCampaignData(CampaignId.Roland).IsLastScenario(last));

        saveData.SetCurrentScenarioInfo(last);

        CampaignScenarioCompletion completion = CampaignProgress.CompleteScenario(world, isLoadingSaveFile: false);

        Assert.True(completion.IsCampaignFinished);
        Assert.Null(completion.NextScenario);
        Assert.False(completion.ShouldSave);

        // 战役结束时当前关不动，等结算界面接手。
        Assert.Equal(last, saveData.CurrentScenarioInfoId);
    }

    [Fact]
    public void ResumingFromASaveDoesNotRecordTheScenarioTwice()
    {
        World world = CreateWorld();
        CampaignSaveData saveData = CampaignSaveData.Get();

        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));
        CampaignProgress.CompleteScenario(world, isLoadingSaveFile: false);

        uint daysAfterFirstCompletion = saveData.DaysPassed;

        // 读档进下一关时，上一关的完成记录在写档那一刻就已经写好了，不能再来一遍。
        world.NewDay();
        CampaignScenarioCompletion completion = CampaignProgress.CompleteScenario(world, isLoadingSaveFile: true);

        Assert.Single(saveData.FinishedMaps);
        Assert.Equal(daysAfterFirstCompletion, saveData.DaysPassed);
        Assert.Equal(new ScenarioInfoId(CampaignId.Roland, 1), completion.NextScenario);
    }

    [Fact]
    public void TheCarryOverAwardTakesTheOldestHerosArmy()
    {
        World world = CreateWorld(out PlayerKingdom kingdom, out Hero hero);
        CampaignSaveData saveData = CampaignSaveData.Get();

        Assert.True(hero.Army.JoinTroop(MonsterType.Crusader, 12));
        Assert.Same(hero, kingdom.Heroes[0]);

        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 6));
        CampaignProgress.CompleteScenario(world, isLoadingSaveFile: false);

        Troop troop = Assert.Single(saveData.CarryOverTroops, slot => slot.IsValid);
        Assert.Equal(MonsterType.Crusader, troop.Monster);
        Assert.Equal(12u, troop.Count);
    }

    [Fact]
    public void TheArtifactAwardsAreAssembledIntoTheBattleGarb()
    {
        World world = CreateWorld();
        CampaignSaveData saveData = CampaignSaveData.Get();

        // 普莱斯忠诚战役的三件安德兰套装分别在第 2、6、7 关发放（编号 0、3、4），凑齐时会被合成成「战斗装束」。
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.PriceOfLoyalty, 1));
        CampaignProgress.CompleteScenario(world, isLoadingSaveFile: false);
        Assert.Equal([0], saveData.GetObtainedCampaignAwards().Select(award => award.Id));

        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.PriceOfLoyalty, 5));
        CampaignProgress.CompleteScenario(world, isLoadingSaveFile: false);
        Assert.Equal([0, 3], saveData.GetObtainedCampaignAwards().Select(award => award.Id));

        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.PriceOfLoyalty, 6));
        CampaignProgress.CompleteScenario(world, isLoadingSaveFile: false);

        // 三件凑齐就被换掉，留下的是「战役额外奖励」里那条战斗装束（编号 10）。
        CampaignAwardData assembled = Assert.Single(saveData.GetObtainedCampaignAwards());
        Assert.Equal(10, assembled.Id);
        Assert.Equal((int)ArtifactType.BattleGarb, assembled.SubType);
    }

    [Fact]
    public void TheSelectableScenariosFollowTheCampaignState()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        CampaignData campaignData = CampaignData.GetCampaignData(CampaignId.Roland);

        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

        // 战役刚开头：所有起始关（罗兰只有一个）。
        Assert.Equal(campaignData.GetStartingScenarios(), CampaignProgress.GetSelectableScenarios(saveData, allowToRestart: false));

        // 打到一半：上一关之后开放的那几关。
        saveData.AddCurrentMapToFinished();
        Assert.Equal(
            CampaignData.GetScenariosAfter(new ScenarioInfoId(CampaignId.Roland, 0)),
            CampaignProgress.GetSelectableScenarios(saveData, allowToRestart: false));

        // 重开关卡：只有正在打的这一关。
        Assert.Equal(
            [new ScenarioInfoId(CampaignId.Roland, 0)],
            CampaignProgress.GetSelectableScenarios(saveData, allowToRestart: true));
    }

    /// <summary>An 8 × 8 world with a blue kingdom driven by a person and one hero in it.</summary>
    /// <returns>The world.</returns>
    private static World CreateWorld() => CreateWorld(out _, out _);

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
        hero.Army.Clean();

        return world;
    }
}
