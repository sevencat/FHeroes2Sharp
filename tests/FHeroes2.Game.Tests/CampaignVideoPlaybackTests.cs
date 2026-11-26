using FHeroes2.Assets.Maps;
using FHeroes2.Game.Campaign;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.System;
using Xunit;

using PlayerKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers which campaign clips play when, <c>playPreviousScenarioVideo()</c> / <c>playCurrentScenarioVideo()</c>
/// (<c>game_campaign.cpp:781-828</c>).
/// </summary>
[Collection(CampaignCollection.Name)]
public sealed class CampaignVideoPlaybackTests
{
    /// <summary>Every test starts from a clean campaign save, which is a process-wide singleton.</summary>
    public CampaignVideoPlaybackTests()
    {
        CampaignSaveData.Get().Reset();
        Settings.Get().Players.Clear();
    }

    [Fact]
    public void ANewCampaignPlaysOnlyTheOpeningOfItsFirstScenario()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

        // 第一关的数据表把开场钉在两条：循环背景 GOOD01.SMK 与叠加的 GOOD01V.SMK。
        Assert.Equal(["GOOD01.SMK", "GOOD01V.SMK"], CampaignVideoPlayback.GetCurrentScenarioVideoFiles(saveData));

        // 还没有打完的关，参考在这一步直接返回。
        Assert.Empty(CampaignVideoPlayback.GetPreviousScenarioVideoFiles(saveData));
    }

    [Fact]
    public void ThePreviousClipIsTheEndingOfTheScenarioThatWasFinished()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();

        // 第二关（Annexation）结束于 GOOD03.SMK，打完后回选关屏要先放它。
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 1));

        World world = CreateWorld();
        world.NewDay();

        Assert.True(CampaignProgress.RecordFinishedScenario(world));

        Assert.Equal(["GOOD03.SMK", "GOOD03QW.SMK"], CampaignVideoPlayback.GetPreviousScenarioVideoFiles(saveData));
    }

    [Fact]
    public void AScenarioWithoutEndingClipsPlaysNothingBeforeTheNextOne()
    {
        CampaignSaveData saveData = CampaignSaveData.Get();
        saveData.SetCurrentScenarioInfo(new ScenarioInfoId(CampaignId.Roland, 0));

        World world = CreateWorld();
        world.NewDay();

        Assert.True(CampaignProgress.RecordFinishedScenario(world));

        // 第一关的 _endScenarioVideoPlayback 是空的。
        Assert.Empty(CampaignVideoPlayback.GetPreviousScenarioVideoFiles(saveData));
    }

    /// <summary>An 8 × 8 world with a blue kingdom driven by a person, enough for the day counter of the save.</summary>
    /// <returns>The world.</returns>
    private static World CreateWorld()
    {
        var world = new World();
        world.GenerateUninitializedMap(8);

        world.Kingdoms.Init(PlayerColor.Blue, GameDifficulty.Normal, PlayerColor.None);
        world.Heroes.Init(MapVersion.SuccessionWars);

        Settings.Get().Players.Init(PlayerColor.Blue);

        Player player = Players.Get(PlayerColor.Blue) ?? throw new InvalidOperationException("The blue slot is missing.");
        player.Control = PlayerControl.Human;

        PlayerKingdom blue = world.Kingdoms.GetKingdom(PlayerColor.Blue);
        Hero hero = world.GetHero(Hero.LordKilburn) ?? throw new InvalidOperationException("The map has no Kilburn.");

        blue.AddHero(hero);
        hero.Army.Clean();

        return world;
    }
}
